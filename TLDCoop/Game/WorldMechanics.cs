using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Механизмы локаций, которые без мода у каждого свои.
    //  — Паровые трубы (Tales): повернул вентиль, поставил или снял ручку вентиля — состояние вентилей
    //    и труб локации у всех.
    //  — Логово волков: поджёг — у всех оно сгорело (и волки оттуда больше не идут).
    //  — Рубильники и свет (SwitchBox, ActiveSwitch): щёлкнул — у всех тот же рубильник и те же лампы.
    //    Хост хранит последнее состояние каждого механизма в progress/<мир>/mechanics.txt и присылает
    //    его каждому, кто заходит в локацию, — так и друг, которого тогда не было, увидит то же.
    //  — Полярное сияние (0.19.26, Game/AuroraPower.cs): настенные выключатели света, питание электролизёров
    //    по сюжету — как рубильники, у хоста хранятся. Зоны сияния (DLC, включаются по случаю) решает тот, кто
    //    решает за локацию (WorldStates.Decider), и раз в 15 с повторяет; их не храним.
    //  — Граффити баллончиком: нарисовал — у всех на том же месте; стёр — пропало у всех.
    //  — Ядовитый туман (Tales): кто дольше в регионе, у того больше тумана; у всех в том же регионе
    //    берётся больший счётчик. Знак, разгоняющий туман, сбрасывает счётчик у всех.
    //  — Вещи, которые сдвинула физика (скатилась по склону, упала с полки): кто ближе всех к вещи,
    //    тот после остановки рассылает новое место.
    public static class WorldMechanics
    {
        private const byte KMech = 12, KDecal = 13, KFog = 14;   // подвиды WorldEvent
        private const byte Valves = 1, Den = 2, Switches = 3, Toggles = 4, Electro = 5, Fields = 6;   // Fields не храним

        private static bool Active => Session.Mode != Mode.Off && Session.InSharedWorld && GameApi.InGame();
        private static bool _applying;

        public static bool Handles(byte kind) => kind == KMech || kind == KDecal || kind == KFog;
        public static bool Deferred(byte kind) => kind == KMech || kind == KDecal;

        // ---------- Механизмы ----------

        private static readonly float[] _due = new float[6];
        private static float _settledAt;

        private static void Schedule(byte which)
        {
            if (_applying || !Active) return;
            _due[which] = Time.unscaledTime + 1f;
        }

        internal static void OnValve() => Schedule(Valves);
        internal static void OnDen() => Schedule(Den);
        internal static void OnSwitch() => Schedule(Switches);
        internal static void OnAuroraToggle() => Schedule(Toggles);
        // Питание электролизёров ставит и сама игра при загрузке локации — это не слать.
        internal static void OnElectro() { if (Time.unscaledTime - _settledAt > 5f) Schedule(Electro); }

        private static string[] Collect(byte which)
        {
            try
            {
                if (which == Valves) return new[] { SteamPipeValveManager.SerializeAll() ?? "", SteamPipeManager.SerializeAll() ?? "" };
                if (which == Den) return new[] { WolfDen.SerializeAll() ?? "" };
                if (which == Switches) return new[] { SwitchBoxManager.SerializeAll() ?? "", ActiveSwitch.SerializeAll() ?? "" };
                if (which == Toggles) return new[] { AuroraPower.CollectToggles() };
                if (which == Electro) return new[] { AuroraPower.CollectElectro() };
                if (which == Fields) return new[] { AuroraPower.CollectFields() };
            }
            catch (Exception e) { MelonLogger.Warning("[механизмы] собрать: " + e.Message); }
            return null;
        }

        private static void SendMech(byte which, string scene, string[] parts, Peer to = null)
        {
            var w = Msg.New(MsgType.WorldEvent);
            w.Write(KMech);
            w.Write(scene);
            w.Write(which);
            w.Write((byte)parts.Length);
            foreach (var p in parts) w.Write(p);
            if (to != null) to.Send(Msg.Done(w));
            else Session.SendGameEvent(Msg.Done(w));
        }

        private static void TickMech(float now)
        {
            for (byte which = 1; which < _due.Length; which++)
            {
                if (_due[which] <= 0f || now < _due[which]) continue;
                _due[which] = 0f;
                var parts = Collect(which);
                if (parts == null) continue;
                string scene = ItemSync.SceneKey();
                if (Session.Mode == Mode.Host) Store(scene, which, parts);
                SendMech(which, scene, parts);
                MelonLogger.Msg($"[механизмы] {Name(which)}: отправил всем");
            }
        }

        private static string Name(byte which) => which switch
        {
            Valves => "вентили", Den => "логово волков", Switches => "рубильники",
            Toggles => "выключатели света в сияние", Electro => "питание электролизёров", Fields => "зоны сияния", _ => "механизм " + which,
        };

        // Зоны сияния: только тот, кто решает за локацию, и только когда рядом есть другие.
        private static float _nextFields, _fieldsResendAt;
        private static string _fieldsSent = "";

        private static void TickFields(float now)
        {
            if (now < _nextFields) return;
            _nextFields = now + 3f;
            if (!WorldStates.Decider || !WorldStates.OthersNear) { _fieldsSent = ""; return; }
            var parts = Collect(Fields);
            if (parts == null || parts[0].Length == 0) return;
            if (parts[0] == _fieldsSent && now < _fieldsResendAt) return;
            bool changed = parts[0] != _fieldsSent;
            _fieldsSent = parts[0];
            _fieldsResendAt = now + 15f;
            SendMech(Fields, ItemSync.SceneKey(), parts);
            if (changed) MelonLogger.Msg("[механизмы] зоны сияния: отправил всем");
        }

        private static void ApplyMech(BinaryReader r)
        {
            byte which = r.ReadByte();
            int n = r.ReadByte();
            var parts = new string[n];
            for (int i = 0; i < n; i++) parts[i] = r.ReadString();
            ApplyParts(which, parts);
        }

        private static void ApplyParts(byte which, string[] parts)
        {
            _applying = true;
            try
            {
                if (which == Valves)
                {
                    if (parts.Length > 0 && parts[0].Length > 0) SteamPipeValveManager.DeserializeAll(parts[0]);
                    if (parts.Length > 1 && parts[1].Length > 0) SteamPipeManager.DeserializeAll(parts[1]);
                }
                else if (which == Den)
                {
                    if (parts.Length > 0 && parts[0].Length > 0) WolfDen.DeserializeAll(parts[0]);
                }
                else if (which == Switches)
                {
                    if (parts.Length > 0 && parts[0].Length > 0) SwitchBoxManager.DeserializeAll(parts[0]);
                    if (parts.Length > 1 && parts[1].Length > 0) ActiveSwitch.DeserializeAll(parts[1]);
                }
                else if (which == Toggles)
                {
                    if (parts.Length > 0) AuroraPower.ApplyToggles(parts[0]);
                }
                else if (which == Electro)
                {
                    if (parts.Length > 0) AuroraPower.ApplyElectro(parts[0]);
                }
                else if (which == Fields)
                {
                    if (parts.Length == 0 || !AuroraPower.ApplyFields(parts[0])) return;   // уже так же — молча
                }
                MelonLogger.Msg($"[механизмы] {Name(which)}: как у друга");
            }
            catch (Exception e) { MelonLogger.Warning("[механизмы] применить: " + e.Message); }
            finally { _applying = false; }
        }

        // Хост: друг что-то переключил — запомнить, даже если сам хост в другой локации.
        public static void HostObserve(byte kind, string scene, byte[] raw)
        {
            if (kind != KMech || Session.Mode != Mode.Host) return;
            try
            {
                using var r = Msg.Read(raw, out _);
                r.ReadByte(); r.ReadString();
                byte which = r.ReadByte();
                int n = r.ReadByte();
                var parts = new string[n];
                for (int i = 0; i < n; i++) parts[i] = r.ReadString();
                if (which == Fields) return;
                Store(scene, which, parts);
            }
            catch (Exception e) { MelonLogger.Warning("[механизмы] запомнить: " + e.Message); }
        }

        // Хост: друг зашёл в локацию — последние состояния механизмов этой локации.
        public static void SendSceneTo(Peer to, string scene)
        {
            if (Session.Mode != Mode.Host || to == null) return;
            LoadStore();
            for (byte which = 1; which < _due.Length; which++)
                if (Stored.TryGetValue(scene + "|" + which, out var parts)) SendMech(which, scene, parts, to);
        }

        // Хост сам зашёл в локацию: если тут без него что-то переключили — поставить так же.
        public static void OnSceneSettled(string scene)
        {
            Array.Clear(_due, 0, _due.Length);
            _settledAt = Time.unscaledTime;
            _fieldsSent = "";
            _fogSeen = -1f;
            if (Session.Mode != Mode.Host || !Active) return;
            LoadStore();
            for (byte which = 1; which < _due.Length; which++)
            {
                if (!Stored.TryGetValue(scene + "|" + which, out var parts)) continue;
                var mine = Collect(which);
                if (mine != null && string.Join("\n", mine) == string.Join("\n", parts)) continue;
                ApplyParts(which, parts);
            }
        }

        // ---------- Хранение у хоста ----------

        private static readonly Dictionary<string, string[]> Stored = new();
        private static string _loadedFor;

        private static string FileName()
        {
            string dir = PlayerProgress.WorldFolder();
            return dir == null ? null : Path.Combine(dir, "mechanics.txt");
        }

        private static void LoadStore()
        {
            string f = FileName();
            if (f == null || _loadedFor == f) return;
            _loadedFor = f;
            Stored.Clear();
            try
            {
                if (!File.Exists(f)) return;
                foreach (var line in File.ReadAllLines(f))
                {
                    var p = line.Split('\t');
                    if (p.Length < 2) continue;
                    var parts = new string[p.Length - 1];
                    for (int i = 1; i < p.Length; i++) parts[i - 1] = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(p[i]));
                    Stored[p[0]] = parts;
                }
            }
            catch (Exception e) { MelonLogger.Warning("[механизмы] чтение: " + e.Message); }
        }

        private static void Store(string scene, byte which, string[] parts)
        {
            LoadStore();
            Stored[scene + "|" + which] = parts;
            string f = FileName();
            if (f == null) return;
            var lines = new List<string>();
            foreach (var kv in Stored)
            {
                var cells = new List<string> { kv.Key };
                foreach (var s in kv.Value) cells.Add(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s)));
                lines.Add(string.Join("\t", cells));
            }
            try { File.WriteAllLines(f, lines); } catch (Exception e) { MelonLogger.Warning("[механизмы] запись: " + e.Message); }
        }

        // ---------- Граффити ----------

        private static int _removedFrame = -1, _removedCount;

        internal static void OnDecalPlaced(DecalProjectorInstance d)
        {
            if (_applying || !Active || d == null || d.m_DecalProjectorType != DecalProjectorType.SprayPaint) return;
            if (string.IsNullOrEmpty(d.m_Guid)) return;
            var w = Msg.New(MsgType.WorldEvent);
            w.Write(KDecal);
            w.Write(ItemSync.SceneKey());
            w.Write((byte)1);
            w.Write(d.m_Guid);
            w.Write(d.m_DecalName ?? "");
            WriteV(w, d.m_Pos);
            w.Write(d.m_Yaw);
            WriteV(w, d.m_Normal);
            WriteV(w, d.m_Scale);
            w.Write(d.m_Indoors);
            w.Write((int)d.m_ProjectileType);
            var c = d.m_ColorTint;
            w.Write(c.r); w.Write(c.g); w.Write(c.b); w.Write(c.a);
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg("[граффити] нарисовал, отправил всем");
        }

        internal static void OnDecalRemoved(DecalProjectorInstance d)
        {
            if (_applying || !Active || d == null || d.m_DecalProjectorType != DecalProjectorType.SprayPaint) return;
            if (string.IsNullOrEmpty(d.m_Guid)) return;
            // Игрок стирает рядом с собой и по одному. Много сразу или далеко — это игра чистит локацию, не шлём.
            if (Time.frameCount != _removedFrame) { _removedFrame = Time.frameCount; _removedCount = 0; }
            if (++_removedCount > 2) return;
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return;
            if ((d.m_Pos - new Vector3(x, y, z)).sqrMagnitude > 8f * 8f) return;
            var w = Msg.New(MsgType.WorldEvent);
            w.Write(KDecal);
            w.Write(ItemSync.SceneKey());
            w.Write((byte)0);
            w.Write(d.m_Guid);
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg("[граффити] стёр, отправил всем");
        }

        private static void WriteV(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        private static Vector3 ReadV(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        private static void ApplyDecal(BinaryReader r)
        {
            byte add = r.ReadByte();
            string guid = r.ReadString();
            var ddm = GameManager.GetDynamicDecalsManager();
            if (ddm == null) return;
            _applying = true;
            try
            {
                if (add == 0) { ddm.RemoveDynamicDecal(guid); return; }
                string name = r.ReadString();
                var pos = ReadV(r);
                float yaw = r.ReadSingle();
                var normal = ReadV(r);
                var scale = ReadV(r);
                bool indoors = r.ReadBoolean();
                int pt = r.ReadInt32();
                var tint = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                ddm.RemoveDynamicDecal(guid);   // уже есть такой — перерисуем
                var d = ddm.CreateCustomDecal(name, pos, yaw, normal, scale, DecalProjectorType.SprayPaint, indoors, (ProjectileType)pt);
                if (d == null) return;
                d.m_Guid = guid;
                d.m_ColorTint = tint;
                ddm.PlaceDownSprayPaintDecal(d);
                MelonLogger.Msg("[граффити] рисунок друга");
            }
            catch (Exception e) { MelonLogger.Warning("[граффити] " + e.Message); }
            finally { _applying = false; }
        }

        // ---------- Ядовитый туман ----------

        private static float _nextFog, _fogSeen = -1f;

        private static void TickFog(float now)
        {
            if (now < _nextFog) return;
            _nextFog = now + 5f;
            try
            {
                var fog = GameManager.GetToxicFogManager();
                if (fog == null || !fog.IsActive() || !fog.IsCurrentRegionValid() || fog.m_CurrentSceneSet == null) { _fogSeen = -1f; return; }
                float s = fog.GetSecondsInCurrentRegion();
                bool reset = _fogSeen >= 0f && s < _fogSeen - 30f;
                _fogSeen = s;
                var w = Msg.New(MsgType.WorldEvent);
                w.Write(KFog);
                w.Write(fog.m_CurrentSceneSet.name ?? "");
                w.Write(s);
                w.Write(reset);
                Session.SendGameEvent(Msg.Done(w));
            }
            catch { }
        }

        private static void ReceiveFog(string set, BinaryReader r)
        {
            float s = r.ReadSingle();
            bool reset = r.ReadBoolean();
            try
            {
                var fog = GameManager.GetToxicFogManager();
                if (fog == null || !fog.IsActive() || !fog.IsCurrentRegionValid() || fog.m_CurrentSceneSet == null) return;
                if (fog.m_CurrentSceneSet.name != set) return;
                float mine = fog.GetSecondsInCurrentRegion();
                if (reset ? s < mine - 1f : s > mine + 1f)
                {
                    fog.SetSecondsInCurrentRegion(s);
                    _fogSeen = s;
                    if (reset) MelonLogger.Msg("[туман] друг разогнал туман знаком — у нас тоже");
                }
            }
            catch (Exception e) { MelonLogger.Warning("[туман] " + e.Message); }
        }

        // ---------- Вещи, сдвинутые физикой ----------

        private static float _nextPhys;
        private static readonly HashSet<IntPtr> _physSent = new();
        // Где вещь лежала спокойно (последний раз, когда её тело спало): по этому месту остальные найдут свою копию.
        private static readonly Dictionary<IntPtr, Vector3> _restPos = new();

        private static void TickPhysics(float now)
        {
            if (now < _nextPhys) return;
            _nextPhys = now + 1f;
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return;
            var me = new Vector3(x, y, z);
            string scene = GameApi.Scene();
            Collider[] hits;
            try { hits = Physics.OverlapSphere(me, 4f); } catch { return; }
            if (hits == null) return;
            foreach (var c in hits)
            {
                if (c == null) continue;
                var rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                var gi = rb.GetComponent<GearItem>();
                if (gi == null || !WorldExtra.InWorld(gi) || FlareShots.IsFiredRound(gi)) continue;
                if (rb.IsSleeping())
                {
                    // Улеглась: запоминаем место, и если её снова заденут, снова перешлём.
                    _restPos[gi.Pointer] = gi.transform.position;
                    _physSent.Remove(gi.Pointer);
                    continue;
                }
                if (_physSent.Contains(gi.Pointer) || WorldExtra.IsWatched(gi)) continue;
                if (!NearestToMe(gi.transform.position, me, scene, now)) continue;
                _physSent.Add(gi.Pointer);
                // Вещь лежала в локации и сдвинулась сама: уходит заменой с тем же номером и старым местом,
                // у остальных их копия переезжает (раньше уходила как новая выброшенная, и копий становилось две).
                WorldExtra.OnPushed(gi, _restPos.TryGetValue(gi.Pointer, out var rest) ? rest : gi.transform.position);
            }
            if (_physSent.Count > 200) _physSent.Clear();
            if (_restPos.Count > 400) _restPos.Clear();
        }

        // Сдвинутые вещи пересылает один — тот, кто ближе всех.
        private static bool NearestToMe(Vector3 p, Vector3 me, string scene, float now)
        {
            float mine = (p - me).sqrMagnitude;
            foreach (var pl in Session.Players.Values)
            {
                if (pl.Id == Session.LocalId || !Session.With(pl, scene) || now - pl.LastState > 5f) continue;
                float d = (p - new Vector3(pl.X, pl.Y, pl.Z)).sqrMagnitude;
                if (d < mine || (d == mine && pl.Id < Session.LocalId)) return false;
            }
            return true;
        }

        // ---------- Общее ----------

        public static void Tick(float now)
        {
            if (!Active) return;
            TickMech(now);
            try { TickFields(now); } catch (Exception e) { MelonLogger.Warning("[механизмы] зоны сияния: " + e.Message); _nextFields = now + 30f; }
            TickFog(now);
            TickPhysics(now);
            if (_physSent.Count > 0 && Time.frameCount % 600 == 0) _physSent.Clear();
        }

        // Туман — по региону, а не по локации, поэтому до проверки локации.
        public static bool ReceiveEarly(byte kind, string scene, BinaryReader r)
        {
            if (kind != KFog) return false;
            if (Active) ReceiveFog(scene, r);
            return true;
        }

        public static void Apply(byte kind, BinaryReader r)
        {
            if (kind == KMech) ApplyMech(r);
            else if (kind == KDecal) ApplyDecal(r);
        }

        public static void Reset()
        {
            Array.Clear(_due, 0, _due.Length);
            Stored.Clear();
            _loadedFor = null;
            _physSent.Clear();
            _restPos.Clear();
            _fogSeen = -1f;
        }
    }

    [HarmonyPatch(typeof(SteamPipeValve), nameof(SteamPipeValve.OnOpenValveComplete))]
    internal static class Patch_ValveOpened { private static void Postfix() => WorldMechanics.OnValve(); }

    [HarmonyPatch(typeof(SteamPipeValve), nameof(SteamPipeValve.OnCloseValveComplete))]
    internal static class Patch_ValveClosed { private static void Postfix() => WorldMechanics.OnValve(); }

    [HarmonyPatch(typeof(SteamPipeValveSocket), nameof(SteamPipeValveSocket.OnAttachValveComplete))]
    internal static class Patch_ValveAttached { private static void Postfix() => WorldMechanics.OnValve(); }

    [HarmonyPatch(typeof(SteamPipeValveSocket), nameof(SteamPipeValveSocket.OnDetachValveComplete))]
    internal static class Patch_ValveDetached { private static void Postfix() => WorldMechanics.OnValve(); }

    [HarmonyPatch(typeof(WolfDen), nameof(WolfDen.OnStartFireComplete))]
    internal static class Patch_DenBurned
    {
        private static void Postfix(bool success) { if (success) WorldMechanics.OnDen(); }
    }

    [HarmonyPatch(typeof(SwitchBox), nameof(SwitchBox.PerformInteraction))]
    internal static class Patch_SwitchUsed { private static void Postfix() => WorldMechanics.OnSwitch(); }

    [HarmonyPatch(typeof(DynamicDecalsManager), nameof(DynamicDecalsManager.PlaceDownSprayPaintDecal))]
    internal static class Patch_SprayPlaced
    {
        private static void Postfix(DecalProjectorInstance instance) => WorldMechanics.OnDecalPlaced(instance);
    }

    [HarmonyPatch(typeof(DynamicDecalsManager), nameof(DynamicDecalsManager.RemoveDynamicDecal), new[] { typeof(DecalProjectorInstance) })]
    internal static class Patch_SprayRemoved
    {
        private static void Prefix(DecalProjectorInstance decalInstance) => WorldMechanics.OnDecalRemoved(decalInstance);
    }
}
