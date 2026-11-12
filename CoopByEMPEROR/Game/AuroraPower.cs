using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.ModularElectrolizer;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Электричество в полярное сияние у всех одинаковое (0.19.26).
    //  — Случайное в лампах (порог, при котором загорается, частота и сила мерцания, цвет, набор вспышек) игра
    //    берёт из UnityEngine.Random в Awake/Initialize. У каждой лампы теперь свой поток случайных чисел, начатый
    //    от её места, — у всех игроков одинаковый. Значит, лампы загораются и гаснут при той же силе сияния.
    //  — Общие часы: хост шлёт свои часы вместе с сиянием (GlobalSync), у друга они сдвинуты на половину пинга.
    //    Простые лампы (AuroraLightingSimple) мерцают синусом от таймера — раз в секунду ставим таймер по общим часам.
    //    Новые лампы (AuroraModularElectrolizer) идут по набору вспышек — каждые 30 с общих часов у всех заново
    //    начинается одинаковый набор.
    //  — Выключатели света в сияние (AuroraActivatedToggle), питание электролизёров по сюжету
    //    (AuroraElectrolizerController) и зоны сияния (AuroraFieldsSceneManager) — через Game/WorldMechanics.cs.
    //  — Какую картинку показывает экран (AuroraScreenManager): назначения общие на весь мир, первый, кто назначил, и прав.
    public static class AuroraPower
    {
        private static bool Shared => Session.Mode != Mode.Off && Session.InSharedWorld;

        // ---------- Общие часы ----------

        private static double _offset;
        private static bool _haveOffset;

        public static double Clock => Time.realtimeSinceStartupAsDouble + (Session.Mode == Mode.Client ? _offset : 0.0);
        private static bool ClockReady => Session.Mode == Mode.Host || _haveOffset;

        internal static void OnHostClock(double hostClock)
        {
            if (Session.Mode != Mode.Client) return;
            int ping = Session.PingMs();
            double sample = hostClock + (ping > 0 ? ping / 2000.0 : 0.0) - Time.realtimeSinceStartupAsDouble;
            if (!_haveOffset || Math.Abs(sample - _offset) > 0.5) { _offset = sample; _haveOffset = true; }
            else _offset += (sample - _offset) * 0.2;
        }

        // ---------- Свой поток случайных чисел у каждой лампы ----------

        internal struct Saved
        {
            public bool On;
            public UnityEngine.Random.State Global;
            public IntPtr Key;
        }

        private static readonly Dictionary<IntPtr, UnityEngine.Random.State> Streams = new();

        // fresh — начать поток заново от места (Awake/Initialize), иначе продолжить уже начатый.
        internal static Saved Enter(Component c, bool fresh, int salt)
        {
            if (!Shared || c == null) return default;
            var s = new Saved { On = true, Global = UnityEngine.Random.state, Key = c.Pointer };
            if (!fresh && Streams.TryGetValue(s.Key, out var st)) UnityEngine.Random.state = st;
            else UnityEngine.Random.InitState(Seed(c, salt));
            return s;
        }

        internal static void Leave(Saved s)
        {
            if (!s.On) return;
            if (Streams.Count > 20000) Streams.Clear();
            Streams[s.Key] = UnityEngine.Random.state;
            UnityEngine.Random.state = s.Global;
        }

        private static int Seed(Component c, int salt)
        {
            var p = c.transform.position;
            unchecked
            {
                uint h = 2166136261;
                h = (h ^ (uint)Mathf.RoundToInt(p.x * 20f)) * 16777619;
                h = (h ^ (uint)Mathf.RoundToInt(p.y * 20f)) * 16777619;
                h = (h ^ (uint)Mathf.RoundToInt(p.z * 20f)) * 16777619;
                h = (h ^ (uint)salt) * 16777619;
                return (int)h;
            }
        }

        // ---------- Мерцание по общим часам ----------

        private const double EpochLen = 30.0;
        private static float _nextPhase, _nextLog;
        private static long _epoch = long.MinValue;

        public static void Tick(float now)
        {
            if (!GameApi.InGame()) return;
            var am = GameManager.GetAuroraManager();
            if (am == null) return;
            TickScreens(now);
            if (am.m_NormalizedActive <= 0.001f || !ClockReady) { _epoch = long.MinValue; return; }
            double t = Clock;
            if (now >= _nextPhase)
            {
                _nextPhase = now + 1f;
                AlignSimple(t);
            }
            long ep = (long)Math.Floor(t / EpochLen);
            if (ep == _epoch) return;
            bool first = _epoch == long.MinValue;
            _epoch = ep;
            if (!first) ResyncFlicker(ep, now);   // в середине отрезка не трогаем: у других этот отрезок уже идёт
        }

        // Простые лампы: таймер синуса = общие часы по модулю периода (у всех одна частота — поток от места).
        private static void AlignSimple(double t)
        {
            var list = AuroraManager.m_AuroraLightSimpleList;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var l = list[i];
                if (l == null) continue;
                float per = l.m_PriPeriod;
                if (per <= 0.0001f) continue;
                l.m_PhiTimer = (float)(t % per);
            }
        }

        // Новые лампы: у всех в один момент общих часов заново и одинаково начинается набор вспышек.
        private static void ResyncFlicker(long ep, float now)
        {
            var list = AuroraManager.m_AuroraElectrolizerList;
            if (list == null) return;
            int n = 0;
            var global = UnityEngine.Random.state;
            try
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    if (e == null || !e.m_IsInitialized || !e.m_HasFlickerSet) continue;
                    UnityEngine.Random.InitState(Seed(e, unchecked((int)ep * 7919 + 3)));
                    Streams[e.Pointer] = UnityEngine.Random.state;
                    try { e.InitializeFlickerSet(); n++; } catch { }
                }
            }
            finally { UnityEngine.Random.state = global; }
            if (n > 0 && now >= _nextLog)
            {
                _nextLog = now + 300f;
                MelonLogger.Msg($"[сияние] мерцание выровнено по общим часам: ламп {n}");
            }
        }

        // ---------- Выключатели, электролизёры, зоны (собирает и ставит Game/WorldMechanics.cs) ----------

        private static string Key(Vector3 p) => Mathf.RoundToInt(p.x * 10f) + "_" + Mathf.RoundToInt(p.y * 10f) + "_" + Mathf.RoundToInt(p.z * 10f);

        internal static string CollectToggles()
        {
            var list = AuroraManager.GetAuroraActivatedToggleList();
            if (list == null) return "";
            var parts = new List<string>();
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null) continue;
                parts.Add(Key(t.transform.position) + "=" + (int)t.m_ToggleState);
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join(";", parts);
        }

        internal static int ApplyToggles(string s)
        {
            var want = new Dictionary<string, int>();
            foreach (var cell in s.Split(';'))
            {
                int eq = cell.IndexOf('=');
                if (eq > 0 && int.TryParse(cell.Substring(eq + 1), out int v)) want[cell.Substring(0, eq)] = v;
            }
            var list = AuroraManager.GetAuroraActivatedToggleList();
            if (list == null) return 0;
            int changed = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null || !want.TryGetValue(Key(t.transform.position), out int v) || (int)t.m_ToggleState == v) continue;
                t.m_ToggleState = (AuroraActivatedToggleState)v;
                try { t.UpdateChildStatus(true); } catch { }
                changed++;
            }
            return changed;
        }

        internal static string CollectElectro() => AuroraElectrolizerController.SerializeAll() ?? "";

        internal static void ApplyElectro(string s)
        {
            if (s.Length > 0 && s != CollectElectro()) AuroraElectrolizerController.DeserializeAll(s);
        }

        internal static string CollectFields()
        {
            var m = AuroraManager.GetAuroraFieldsSceneManager();
            return m == null ? "" : m.Serialize() ?? "";
        }

        internal static bool ApplyFields(string s)
        {
            var m = AuroraManager.GetAuroraFieldsSceneManager();
            if (m == null || s.Length == 0 || s == (m.Serialize() ?? "")) return false;
            m.Deserialize(s);
            return true;
        }

        // ---------- Картинки на экранах ----------

        private static float _nextScreens, _screensBroadcastAt;
        private static string _screensSent = "";

        private static string ScreensText()
        {
            var d = AuroraScreenManager.s_AssignedPrefabs;
            if (d == null) return "";
            var parts = new List<string>();
            foreach (var kv in d) parts.Add(kv.Key + "=" + kv.Value);
            parts.Sort(StringComparer.Ordinal);
            return string.Join("\n", parts);
        }

        private static void TickScreens(float now)
        {
            if (now < _nextScreens) return;
            _nextScreens = now + 5f;
            string s;
            try { s = ScreensText(); } catch { return; }
            bool host = Session.Mode == Mode.Host;
            if (s == _screensSent && !(host && now - _screensBroadcastAt > 60f && s.Length > 0)) return;
            _screensSent = s;
            if (host) _screensBroadcastAt = now;
            var w = Msg.New(MsgType.AuroraScreens);
            w.Write(s);
            if (host) Session.SendGameEvent(Msg.Done(w));
            else Session.SendToHost(Msg.Done(w));
        }

        private static Dictionary<string, string> Parse(string s)
        {
            var d = new Dictionary<string, string>();
            foreach (var line in s.Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq > 0) d[line.Substring(0, eq)] = line.Substring(eq + 1);
            }
            return d;
        }

        // Хост: друг назначил экранам картинки, которых у хоста ещё нет, — берём их (первый назначивший прав).
        public static void HostReceiveScreens(BinaryReader r)
        {
            string s = r.ReadString();
            if (!Shared || !GameApi.InGame()) return;
            try
            {
                var have = AuroraScreenManager.s_AssignedPrefabs;
                int added = 0;
                foreach (var kv in Parse(s))
                {
                    if (have != null && have.ContainsKey(kv.Key)) continue;
                    AuroraScreenManager.AssociatePrefabWithId(kv.Key, kv.Value);
                    added++;
                }
                if (added > 0) { _nextScreens = 0f; MelonLogger.Msg($"[сияние] экраны: взял у друга {added} назначений"); }
            }
            catch (Exception e) { MelonLogger.Warning("[сияние] экраны (хост): " + e.Message); }
        }

        // Друг: назначения хоста — главные.
        public static void ClientReceiveScreens(BinaryReader r)
        {
            string s = r.ReadString();
            if (!Shared || !GameApi.InGame()) return;
            try
            {
                var have = AuroraScreenManager.s_AssignedPrefabs;
                int changed = 0;
                foreach (var kv in Parse(s))
                {
                    if (have != null && have.ContainsKey(kv.Key))
                    {
                        if (have[kv.Key] == kv.Value) continue;
                        AuroraScreenManager.RemovePrefabAssociation(kv.Key);
                    }
                    AuroraScreenManager.AssociatePrefabWithId(kv.Key, kv.Value);
                    changed++;
                }
                _screensSent = ScreensText();   // своё не шлём обратно, пока не появится новое
                if (changed > 0) MelonLogger.Msg($"[сияние] экраны: картинки как у хоста ({changed})");
            }
            catch (Exception e) { MelonLogger.Warning("[сияние] экраны (друг): " + e.Message); }
        }

        public static void Reset()
        {
            _haveOffset = false;
            _offset = 0.0;
            _epoch = long.MinValue;
            _screensSent = "";
            _nextScreens = 0f;
            _screensBroadcastAt = 0f;
            Streams.Clear();
        }
    }

    // ---------- Случайное в лампах и экранах — из потока лампы ----------

    [HarmonyPatch(typeof(AuroraLightingSimple), nameof(AuroraLightingSimple.Awake))]
    internal static class Patch_AuroraSimpleSeed
    {
        private static void Prefix(AuroraLightingSimple __instance, out AuroraPower.Saved __state) => __state = AuroraPower.Enter(__instance, true, 1);
        private static void Postfix(AuroraPower.Saved __state) => AuroraPower.Leave(__state);
    }

    [HarmonyPatch(typeof(AuroraModularElectrolizer), nameof(AuroraModularElectrolizer.Initialize))]
    internal static class Patch_AuroraModularSeed
    {
        private static void Prefix(AuroraModularElectrolizer __instance, out AuroraPower.Saved __state) => __state = AuroraPower.Enter(__instance, true, 2);
        private static void Postfix(AuroraPower.Saved __state) => AuroraPower.Leave(__state);
    }

    [HarmonyPatch(typeof(AuroraModularElectrolizer), nameof(AuroraModularElectrolizer.InitializeFlickerSet))]
    internal static class Patch_AuroraFlickerSet
    {
        private static void Prefix(AuroraModularElectrolizer __instance, out AuroraPower.Saved __state) => __state = AuroraPower.Enter(__instance, false, 2);
        private static void Postfix(AuroraPower.Saved __state) => AuroraPower.Leave(__state);
    }

    [HarmonyPatch(typeof(AuroraScreenDisplay), nameof(AuroraScreenDisplay.Start))]
    internal static class Patch_AuroraScreenSeed
    {
        private static void Prefix(AuroraScreenDisplay __instance, out AuroraPower.Saved __state) => __state = AuroraPower.Enter(__instance, true, 3);
        private static void Postfix(AuroraPower.Saved __state) => AuroraPower.Leave(__state);
    }

    [HarmonyPatch(typeof(AuroraScreenPrefabPool), nameof(AuroraScreenPrefabPool.InstantiateRandomPrefabForScreen))]
    internal static class Patch_AuroraScreenPick
    {
        private static void Prefix(AuroraScreenDisplay screen, out AuroraPower.Saved __state) => __state = AuroraPower.Enter(screen, true, 4);
        private static void Postfix(AuroraPower.Saved __state) => AuroraPower.Leave(__state);
    }

    // ---------- Выключатели и питание — в Game/WorldMechanics.cs ----------

    [HarmonyPatch(typeof(AuroraActivatedToggle), nameof(AuroraActivatedToggle.PerformInteraction))]
    internal static class Patch_AuroraToggleUsed { private static void Postfix() => WorldMechanics.OnAuroraToggle(); }

    [HarmonyPatch(typeof(AuroraElectrolizerController), nameof(AuroraElectrolizerController.SetPowerState), new[] { typeof(string), typeof(bool) })]
    internal static class Patch_ElectroPowerName { private static void Postfix() => WorldMechanics.OnElectro(); }

    [HarmonyPatch(typeof(AuroraElectrolizerController), nameof(AuroraElectrolizerController.SetPowerState), new[] { typeof(int), typeof(bool) })]
    internal static class Patch_ElectroPowerId { private static void Postfix() => WorldMechanics.OnElectro(); }

    [HarmonyPatch(typeof(AuroraElectrolizerController), nameof(AuroraElectrolizerController.SetPowerStateAll))]
    internal static class Patch_ElectroPowerAll { private static void Postfix() => WorldMechanics.OnElectro(); }
}
