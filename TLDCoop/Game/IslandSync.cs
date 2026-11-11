using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.IslandTransit;
using Il2CppTLD.Trader;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Лодка на остров (DLC Iron Road).
    //  — Ящик обмена общий, как у торговца: что положил один, видят все, плата засчитывается один раз.
    //    Общий ящик ведёт хост: друг шлёт, что поменял («было → стало»), хост накладывает это на свой ящик
    //    и рассылает итог целиком. Кто пришёл к лодке, спрашивает итог у хоста (своё сохранение могло устареть).
    //  — Кто отплывает, тот зовёт с собой: у друзей в 15 м от него появляется «Плыть вместе? Да / Нет».
    //    «Да» перевозит без оплаты. Кто отказался или был далеко (в другой локации тоже), получает один
    //    бесплатный рейс: сможет приплыть сам позже без оплаты и без нужного предмета.
    public static class IslandSync
    {
        private const byte KBox = 1, KGo = 2, KBoxChange = 3, KBoxAsk = 4;
        private const float Range = 15f;

        private static string _boxSent;      // ящик, который знают все (без моих ещё не отправленных изменений)
        private static string _boxPending;   // итог от хоста, ждёт закрытия окна ящика
        private static string _hostBox;      // хост: общий ящик, и когда хост далеко от лодки
        private static float _nextPoll, _lastGo = -100f;
        private static bool _following, _freeCan, _freeItem;

        private static string _offerFrom;
        private static float _offerUntil;

        private static MelonPreferences_Category _cat;
        private static MelonPreferences_Entry<int> _credit;

        public static bool PromptOpen => _offerFrom != null && Time.unscaledTime < _offerUntil;

        private static MelonPreferences_Entry<int> _creditWorld;

        // Бесплатный рейс действует только в том мире, где его дали (зерно мира одно у хоста и друзей):
        // раньше он хранился на компьютере просто так и переезжал в чужие миры и старые сохранения.
        private static int WorldSeed()
        {
            try { return GameManager.m_SceneTransitionData?.m_GameRandomSeed ?? 0; } catch { return 0; }
        }

        private static int Credit
        {
            get { Prefs(); return _creditWorld.Value == WorldSeed() ? _credit.Value : 0; }
            set { Prefs(); _credit.Value = Math.Clamp(value, 0, 1); _creditWorld.Value = WorldSeed(); _cat.SaveToFile(false); }
        }

        private static void Prefs()
        {
            if (_cat != null) return;
            _cat = MelonPreferences.CreateCategory("TLDCoopIsland");
            _credit = _cat.CreateEntry("FreeBoatTrip", 0, "Бесплатный рейс на лодке (друг уже заплатил)");
            _creditWorld = _cat.CreateEntry("FreeBoatTripWorld", 0, "Зерно мира, в котором дан бесплатный рейс");
        }

        private static bool Active => Session.Mode != Mode.Off && Session.InSharedWorld && GameApi.InGame();

        public static void Tick(float now)
        {
            if (!Active || now < _nextPoll) return;
            _nextPoll = now + 1f;
            try { PollBox(); } catch (Exception e) { Warn("ящик: " + e.Message); }
        }

        // ---------- Общий ящик обмена ----------

        private static bool BoxPanelOpen()
        {
            try { return InterfaceManager.TryGetPanel<Panel_IslandTransitExchangeBox>(out var p) && p != null && p.IsEnabled(); }
            catch { return false; }
        }

        private static void PollBox()
        {
            if (BoxPanelOpen()) return;
            var data = IslandTransitExchangeBox.SerializeAll();
            // Ящика в этой локации нет. Раньше отсюда уходил пустой ящик и стирал его у всех, кто стоит у лодки.
            if (data == null) { _boxSent = null; _boxPending = null; return; }
            string cur = FastJson.Serialize(data) ?? "";
            if (_boxSent == null)
            {
                // Пришёл к лодке: ящик в своём сохранении мог устареть (менялся, пока меня не было) — берём общий.
                _boxSent = cur;
                if (Session.Mode == Mode.Host) { if (_hostBox == null) _hostBox = cur; else _boxPending = _hostBox; }
                else Send(KBoxAsk);
            }
            if (_boxPending != null)
            {
                string full = _boxPending;
                _boxPending = null;
                // Своё, ещё не отправленное (клал, пока окно ящика было открыто), кладём поверх пришедшего, а не теряем.
                string result = Same(cur, _boxSent) ? full : Merge(full, _boxSent, cur);
                if (!Same(result, cur)) { if (!Apply(result)) return; cur = result; }   // не легло — своё старое не рассылаем
                _boxSent = full;
            }
            if (Same(cur, _boxSent)) { _boxSent = cur; return; }
            SendOwn(cur, true);
        }

        // Своё изменение: друг шлёт хосту «было → стало», хост накладывает на общий ящик и рассылает итог.
        private static void SendOwn(string cur, bool apply)
        {
            if (Session.Mode == Mode.Host)
            {
                _hostBox = Merge(_hostBox ?? _boxSent, _boxSent, cur);
                if (apply && !Same(_hostBox, cur)) Apply(_hostBox);
                _boxSent = _hostBox;
                Send(KBox, _hostBox);
            }
            else
            {
                Send(KBoxChange, _boxSent, cur);
                _boxSent = cur;
            }
            MelonLogger.Msg("[лодка] поменял ящик обмена — отправил");
        }

        // Заплатил ящиком и отплываю: игра опустошила ящик — сообщаем сразу, после загрузки ящика здесь уже не будет.
        internal static void OnBoxPaid()
        {
            if (Session.Mode == Mode.Off || !Session.InSharedWorld || _boxSent == null) return;
            try
            {
                var data = IslandTransitExchangeBox.SerializeAll();
                if (data == null) return;
                _boxPending = null;
                string cur = FastJson.Serialize(data) ?? "";
                if (!Same(cur, _boxSent)) SendOwn(cur, false);
            }
            catch (Exception e) { Warn("ящик: " + e.Message); }
        }

        // Хост: друг поменял ящик — накладываем на общий и рассылаем итог (и тому, кто менял).
        private static void HostChange(string from, string to)
        {
            _hostBox = Merge(_hostBox ?? from, from, to);
            Send(KBox, _hostBox);
            MelonLogger.Msg("[лодка] друг поменял ящик обмена — разослал всем");
            if (Active && _boxSent != null) { _boxPending = _hostBox; if (!BoxPanelOpen()) PollBox(); }
        }

        private static void Send(byte kind, string a = null, string b = null)
        {
            var w = Msg.New(MsgType.IslandSync);
            w.Write(kind);
            if (a != null) w.Write(a);
            if (b != null) w.Write(b);
            Session.SendGameEvent(Msg.Done(w));
        }

        private static bool Apply(string json)
        {
            try
            {
                var d = json.Length > 0 ? FastJson.Deserialize<IslandTransitExchangeBoxSaveData>(json) : null;
                d ??= new IslandTransitExchangeBoxSaveData();
                // Без списка игра сначала очищала ящик, а потом падала на пустом списке.
                if (d.m_ExchangedItems == null) d.m_ExchangedItems = new Il2CppSystem.Collections.Generic.List<ActiveTradeState.PlayerExchangedItem>();
                IslandTransitExchangeBox.DeserializeAll(d);
                MelonLogger.Msg("[лодка] ящик обмена обновлён");
                return true;
            }
            catch (Exception e) { Warn("ящик: " + e.Message); return false; }
        }

        // Вещи сравниваем по предмету, состоянию и количеству: игра у каждого пересоздаёт вещи ящика сама,
        // поэтому номера и места вещей у всех разные.
        private static List<ActiveTradeState.PlayerExchangedItem> Items(string json)
        {
            var res = new List<ActiveTradeState.PlayerExchangedItem>();
            if (string.IsNullOrEmpty(json)) return res;
            var l = FastJson.Deserialize<IslandTransitExchangeBoxSaveData>(json)?.m_ExchangedItems;
            if (l != null) for (int i = 0; i < l.Count; i++) if (l[i] != null) res.Add(l[i]);
            return res;
        }

        private static string Key(ActiveTradeState.PlayerExchangedItem it)
        {
            string asset = "";
            try { asset = it.m_PrefabReference?.AssetGUID ?? ""; } catch { }
            var g = it.m_SerializedGear;
            if (g == null) return asset;
            return asset + "|" + Mathf.RoundToInt(g.m_NormalizedCondition * 100f) + "|" + g.m_StackableItemSerialized + "|" + g.m_LiquidItemSerialized + "|" + g.m_WaterSupplySerialized;
        }

        private static string Sig(string json)
        {
            var keys = Items(json).ConvertAll(Key);
            keys.Sort(StringComparer.Ordinal);
            return string.Join("\n", keys);
        }

        private static bool Same(string a, string b) => a == b || (a != null && b != null && Sig(a) == Sig(b));

        // Что поменялось from → to (убранные и добавленные вещи), то же делаем с full.
        private static string Merge(string full, string from, string to)
        {
            var toItems = Items(to);
            var left = new Dictionary<string, int>();
            foreach (var it in toItems) { string k = Key(it); left[k] = left.GetValueOrDefault(k) + 1; }
            var removed = new List<string>();
            foreach (var it in Items(from)) { string k = Key(it); if (left.GetValueOrDefault(k) > 0) left[k]--; else removed.Add(k); }
            var result = Items(full);
            foreach (var k in removed) { int i = result.FindIndex(x => Key(x) == k); if (i >= 0) result.RemoveAt(i); }
            foreach (var it in toItems) { string k = Key(it); if (left.GetValueOrDefault(k) > 0) { left[k]--; result.Add(it); } }
            var list = new Il2CppSystem.Collections.Generic.List<ActiveTradeState.PlayerExchangedItem>();
            foreach (var it in result) list.Add(it);
            return FastJson.Serialize(new IslandTransitExchangeBoxSaveData { m_ExchangedItems = list }) ?? "";
        }

        // ---------- Плыть вместе ----------

        internal static void OnTransit()
        {
            if (!Active) return;
            if (_freeCan || _freeItem) { Credit--; MelonLogger.Msg("[лодка] плыву по бесплатному рейсу"); }
            _freeCan = _freeItem = false;
            if (_following) { _following = false; return; }   // сам ответил «Да» — никого больше не зовём
            float now = Time.unscaledTime;
            if (now - _lastGo < 5f) return;
            _lastGo = now;
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return;
            var w = Msg.New(MsgType.IslandSync);
            w.Write(KGo);
            w.Write((byte)Session.LocalId);
            w.Write(GameApi.Scene());
            w.Write(x); w.Write(y); w.Write(z);
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg("[лодка] отплываю — зову друзей");
        }

        private static void ReceiveGo(BinaryReader r)
        {
            int from = r.ReadByte();
            string scene = r.ReadString();
            var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            if (from == Session.LocalId) return;
            string name = Session.Players.TryGetValue(from, out var pl) ? pl.Name : "Друг";
            bool near = scene == GameApi.Scene() && GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)
                        && (new Vector3(x, y, z) - pos).sqrMagnitude <= Range * Range;
            if (near)
            {
                _offerFrom = name;
                _offerUntil = Time.unscaledTime + 20f;
                try { GameAudioManager.PlayGuiConfirm(); } catch { }
            }
            else
            {
                Credit = 1;
                GameApi.HudMessage($"{name} уплыл на лодке. Тебе можно приплыть потом бесплатно");
            }
        }

        private static void Accept()
        {
            _offerFrom = null;
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return;
            var me = new Vector3(x, y, z);
            IslandTransitInteraction best = null;
            float bd = float.MaxValue;
            foreach (var t in UnityEngine.Object.FindObjectsOfType<IslandTransitInteraction>())
            {
                if (t == null) continue;
                float d = (t.transform.position - me).sqrMagnitude;
                if (d < bd) { bd = d; best = t; }
            }
            if (best == null) { GameApi.HudMessage("Не нашёл лодку рядом"); return; }
            _following = true;
            try { best.HandleOnDoTransit(); }
            catch (Exception e) { _following = false; Warn("плыть: " + e.Message); }
        }

        private static void Decline()
        {
            _offerFrom = null;
            Credit = 1;
            GameApi.HudMessage("Остаёшься. Потом сможешь приплыть бесплатно");
        }

        // Кнопки «Плыть вместе?» (из OnGUI).
        public static void Draw()
        {
            if (!PromptOpen) { _offerFrom = null; return; }
            UI.Theme.Apply();
            float w = 420f, h = 110f, x = (Screen.width - w) / 2f, y = Screen.height * 0.25f;
            UI.Widgets.Panel(new Rect(x, y, w, h), "Лодка");
            UI.Theme.Centered(new Rect(x, y + 30f, w, 24f), $"{_offerFrom.Replace("<", "‹")} отплывает. Плыть вместе?");
            if (UI.Widgets.Button(new Rect(x + 20f, y + 64f, w / 2f - 30f, 30f), "Да")) Accept();
            if (UI.Widgets.Button(new Rect(x + w / 2f + 10f, y + 64f, w / 2f - 30f, 30f), "Нет")) Decline();
        }

        // Бесплатный рейс: игра пускает без оплаты и без нужного предмета. Каждая проверка игры перезаписывает свой флаг,
        // поэтому при отплытии видно, пустила ли бы игра сама в последний раз. Раньше рейс помечался «использован»
        // при первой же проверке без предмета и сгорал, даже если игрок потом заплатил сам.
        internal static bool FreePass(bool original, bool item)
        {
            bool free = !original && Active && Credit > 0;
            if (item) _freeItem = free; else _freeCan = free;
            return original || free;
        }

        // ---------- Сеть ----------

        public static void Receive(BinaryReader r)
        {
            if (Session.Mode == Mode.Off || !Session.InSharedWorld) return;
            byte k = r.ReadByte();
            try
            {
                // Хост ведёт общий ящик и на экране загрузки, и вдали от лодки.
                if (k == KBoxChange)
                {
                    string from = r.ReadString(), to = r.ReadString();
                    if (Session.Mode == Mode.Host) HostChange(from, to);
                    return;
                }
                if (k == KBoxAsk) { if (Session.Mode == Mode.Host && _hostBox != null) Send(KBox, _hostBox); return; }
                if (!Active) return;
                if (k == KBox)
                {
                    string json = r.ReadString();
                    if (Session.Mode == Mode.Host) return;   // итог рассылает только хост
                    _boxPending = json;
                    if (!BoxPanelOpen()) PollBox();
                }
                else if (k == KGo) ReceiveGo(r);
            }
            catch (Exception e) { Warn("приём: " + e.Message); }
        }

        private static float _nextWarn;
        private static void Warn(string text)
        {
            if (Time.unscaledTime < _nextWarn) return;
            _nextWarn = Time.unscaledTime + 10f;
            MelonLogger.Warning("[лодка] " + text);
        }

        public static void Reset()
        {
            ForgetWorld();
            _offerFrom = null;
            _following = false;
            _freeCan = _freeItem = false;
        }

        // Мир сменился: ящик прошлого мира забываем.
        internal static void ForgetWorld()
        {
            _boxSent = null;
            _boxPending = null;
            _hostBox = null;
        }
    }

    [HarmonyPatch(typeof(IslandTransitInteraction), nameof(IslandTransitInteraction.HandleOnDoTransit))]
    internal static class Patch_IslandTransit
    {
        private static void Prefix() => IslandSync.OnTransit();
    }

    [HarmonyPatch(typeof(IslandTransitExchangeBox), nameof(IslandTransitExchangeBox.TriggerTransit))]
    internal static class Patch_IslandBoxTransit
    {
        private static void Prefix() => IslandSync.OnTransit();
        private static void Postfix() => IslandSync.OnBoxPaid();
    }

    [HarmonyPatch(typeof(IslandTransitInteraction), nameof(IslandTransitInteraction.CanPlayerTransit))]
    internal static class Patch_IslandCanTransit
    {
        private static void Postfix(ref bool __result) => __result = IslandSync.FreePass(__result, false);
    }

    [HarmonyPatch(typeof(IslandTransitInteraction), nameof(IslandTransitInteraction.HasRequiredCollectible))]
    internal static class Patch_IslandCollectible
    {
        private static void Postfix(ref bool __result) => __result = IslandSync.FreePass(__result, true);
    }
}
