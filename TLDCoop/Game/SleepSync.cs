using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Совместный сон и ожидание. Время в мире одно (его ведёт хост), поэтому спать или пропускать время
    // можно только всем вместе. Игрок выбрал «Спать»/«Ждать» в меню отдыха — сон не начинается, игрок
    // «готов» и видит «Ждём остальных игроков: …». Когда готовы все, хост рассылает «начали»:
    // каждый засыпает (или ждёт) на столько часов, сколько выбрал хост, и время идёт у всех одинаково.
    // Закрыл меню отдыха — больше не готов.
    // «До полного отдыха» вместе: каждый присылает, сколько часов нужно ему, и спят все столько, сколько
    // нужно самому уставшему; кто отдохнул раньше, досыпает со всеми (просыпаются все одновременно).
    // Спать отдельно (если хост разрешил): пока открыто меню отдыха, сверху выбор «Со всеми / Отдельно»
    // (каждый раз сначала «Со всеми»), и кнопка «Спать отдельно», пока ждёшь остальных. Отдельно игрок
    // спит у себя как в одиночной игре, сколько выбрал, а у остальных время не проматывается — как в
    // долгом действии (Game/ActionTime.cs).
    public static class SleepSync
    {
        private enum Kind : byte { Sleep = 0, PassTime = 1 }

        // Exact — точное время ожидания в часах (у ожидания у котелка бывают минуты: 0,25 ч). 0 — нет (старое сообщение).
        private class Ready { public Kind Kind; public int Hours; public bool Full; public int Need; public int Asked; public float Exact; }

        private static float _hostExact;

        // «20 мин», «1 ч», «1 ч 30 мин» (и по-английски).
        private static string Dur(float hours)
        {
            int min = Mathf.Max(1, Mathf.RoundToInt(hours * 60f));
            int h = min / 60, m = min % 60;
            if (h == 0) return UI.L.T($"{m} мин", $"{m} min");
            if (m == 0) return UI.L.T($"{h} ч", $"{h} h");
            return UI.L.T($"{h} ч {m} мин", $"{h} h {m} min");
        }

        private static float ExactOf(Ready r) => r == null ? 0f : r.Kind == Kind.PassTime && r.Exact > 0f ? r.Exact : r.Hours;

        // Своё: готовы ли мы и с какими часами.
        private static Ready _mine;
        private static Panel_Rest _panel;
        private static bool _starting, _forceSolo, _soloChoice, _panelWasOpen;
        public static bool Starting => _starting;
        private static float _nextHint, _nextWait;
        private static string _waitText = "";
        // Что выбрал хост (приходит вместе со списком ожидания): часы, 0 — хост ещё не выбрал.
        private static int _hostHours;
        private static bool _hostFull, _hostPass;
        private static bool _allPass;   // все, кто уже готов, просто ждут (пропускают время), а не спят
        // Пока ждём остальных, меню отдыха игры спрятано (его дети выключены), поверх — тёмный экран с «Назад».
        // Иначе клик по «Назад» попадал бы ещё и в кнопку «Спать» игры под ним.
        private static readonly List<GameObject> _hidden = new();

        // Хост: кто готов.
        private static readonly Dictionary<int, Ready> HostReady = new();

        // Грузящиеся тоже считаются: иначе, пока друг грузится дольше 10 с, сон шёл бы без ожидания, как в одиночке.
        private static bool Coop => Session.Mode != Mode.Off && Session.InSharedWorld && (OthersInGame().Any() || OthersLoading().Any());

        // Игроки, которые сейчас грузятся (переход между локациями): в общем мире, но состояние давно не приходило.
        // Общий сон без них не начинаем: проснулись бы в ускоренном времени не в кровати.
        private static IEnumerable<Player> OthersLoading()
        {
            float now = Time.unscaledTime;
            return Session.Players.Values.Where(p => p.Id != Session.LocalId && p.InWorld && p.LastState > 0f && now - p.LastState >= 1.5f && now - p.LastState < 60f);
        }

        // Общий сон идёт: кто-то проснулся раньше (зверь, холод, отмена) — будим всех, иначе у проснувшегося
        // время пошло бы обычным ходом, а у остальных ещё ускоренно.
        private static bool _inShared, _sawResting, _stopSent;
        private static float _sharedSince;
        // Когда общий сон должен кончиться (день + доля суток, как GameApi.TotalTime). Сон в игре идёт несколько секунд
        // на любое число часов, а ожидание — около секунды за час: кто кончил в срок, тот остальных не будит.
        private static float _plannedEnd;
        // Мы кончили в срок, а остальные ещё спят или ждут: их часы у хоста ещё не дошли — не подгоняем наши назад.
        private static float _holdClockUntil;

        public static void HostWoke(string who)
        {
            if (Session.Mode != Mode.Host || _stopSent) return;
            _stopSent = true;
            var w = Msg.New(MsgType.SleepStop);
            w.Write(who ?? "");
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg($"[сон] {who} проснулся раньше — будим всех");
            Wake(who);
        }

        public static void ReceiveStop(BinaryReader r) => Wake(r.ReadString());

        private static void Wake(string who)
        {
            _holdClockUntil = 0f;   // общий сон окончен: часы снова как у хоста
            if (!_inShared) return;
            _inShared = false;
            try
            {
                var rest = GameManager.GetRestComponent();
                if (rest != null && rest.IsSleeping()) { rest.EndSleeping(false); GameApi.HudMessage($"{who} проснулся — общий сон окончен"); }
                var pt = GameManager.GetPassTime();
                if (pt != null && pt.IsPassingTime()) { pt.End(); GameApi.HudMessage($"{who} перестал ждать — ожидание окончено"); }
            }
            catch (Exception e) { MelonLogger.Warning("[сон] разбудить: " + e.Message); }
        }

        private static void TickShared(float now)
        {
            if (!_inShared) return;
            bool resting = LocalResting;
            if (resting) { _sawResting = true; return; }
            if (!_sawResting) { if (now - _sharedSince > 5f) _inShared = false; return; }   // сон так и не начался
            _inShared = false;
            // Сон или ожидание кончились сами в срок (15 игровых минут запаса) — остальных не будим, они досыпают своё.
            float at = 0f;
            try { at = GameApi.TotalTime(); } catch { }
            if (_plannedEnd > 0f && at >= _plannedEnd - 0.25f / 24f)
            {
                _holdClockUntil = now + 30f;
                MelonLogger.Msg("[сон] проснулся в срок, остальных не бужу");
                return;
            }
            // Мы проснулись раньше (зверь, холод, отмена): сообщаем хосту, он будит остальных.
            if (Session.Mode == Mode.Host) HostWoke(Session.LocalName);
            else Session.SendGameEvent(Msg.Done(Msg.New(MsgType.SleepStop)));
        }

        private static IEnumerable<Player> OthersInGame()
        {
            float now = Time.unscaledTime;
            return Session.Players.Values.Where(p => p.Id != Session.LocalId && p.InWorld && p.Scene.Length > 0 && now - p.LastState < 10f);
        }

        // Не подгонять наши часы под хоста: спим или ждём, или кончили в срок, а остальные ещё нет (до 30 с).
        public static bool HoldClock => LocalResting || Time.unscaledTime < _holdClockUntil;

        // Мы сейчас спим или пропускаем время (на это время хост не подгоняет наши часы).
        public static bool LocalResting
        {
            get
            {
                try
                {
                    var rest = GameManager.GetRestComponent();
                    if (rest != null && rest.IsSleeping()) return true;
                    var pt = GameManager.GetPassTime();
                    return pt != null && pt.IsPassingTime();
                }
                catch { return false; }
            }
        }

        public static void Reset()
        {
            ShowPanel();
            _mine = null;
            _panel = null;
            _hostHours = 0;
            HostReady.Clear();
            _waitText = "";
            _holdClockUntil = 0f;
        }

        // ---------- Меню отдыха ----------

        // Патч: игрок нажал «Спать» или «Ждать». В кооперативе — только отмечаемся и ждём остальных.
        public static bool OnChoose(Panel_Rest panel, bool passTime, int hours, bool full, float exact = 0f)
        {
            if (_starting || !Coop) return true;
            // Игра сама не даст лечь (не устал): пусть скажет об этом своим сообщением. Раньше такой игрок
            // становился «готов» и ждал остальных, а «спать отдельно» молча не срабатывало.
            if (!passTime && GameRefusesSleep(panel, out string why))
            {
                _forceSolo = false;
                _soloChoice = false;
                MelonLogger.Msg("[сон] игра не даёт лечь: " + why);
                return true;
            }
            if ((_soloChoice || _forceSolo) && Session.CurrentRules.SoloSleep)
            {
                _forceSolo = false;
                _soloChoice = false;
                bool solo = ActionTime.BeginSoloRest(hours);
                MelonLogger.Msg($"[сон] {(passTime ? "жду" : "сплю")} отдельно {hours} ч" + (solo ? "" : " (без учёта общего времени)"));
                if (solo) GameApi.HudMessage((passTime ? "Ждёшь" : "Спишь") + " отдельно: у остальных время идёт как обычно");
                return true;
            }
            _panel = panel;
            _mine = new Ready { Kind = passTime ? Kind.PassTime : Kind.Sleep, Hours = Math.Max(1, hours), Full = full, Need = NeedHours(), Asked = hours, Exact = passTime ? exact : 0f };
            SendReady();
            HidePanel();
            MelonLogger.Msg($"[сон] готов: {(passTime ? "ждать" : "спать")} {(passTime && exact > 0f ? Dur(exact) : hours + " ч")}, жду остальных");
            return false;
        }

        // Кнопка «Ждём остальных»: часы выбирать не нужно, спим столько, сколько выбрал хост.
        // Не устал — значит, пока остальные спят, просто ждём (игра не даст лечь).
        private static void BeginWait()
        {
            if (!InterfaceManager.TryGetPanel<Panel_Rest>(out var panel) || panel == null) return;
            bool passTab = false;
            int hours = 8;
            try { passTab = panel.m_ShowPassTime; hours = panel.m_SleepHours; } catch { }
            bool tired = !GameRefusesSleep(panel, out _);
            bool pass = passTab || !tired;
            _panel = panel;
            _mine = new Ready { Kind = pass ? Kind.PassTime : Kind.Sleep, Hours = Math.Max(1, hours), Full = false, Need = NeedHours(), Asked = hours };
            SendReady();
            HidePanel();
            MelonLogger.Msg($"[сон] «Ждём остальных»: {(pass ? "ждать" : "спать")}" + (Session.Mode == Mode.Host ? $" {hours} ч" : ", часы выберет хост") + (passTab || tired ? "" : " (не устал)"));
        }

        // «Назад» на экране ожидания: больше не готовы, меню отдыха снова видно.
        private static void CancelWait()
        {
            if (_mine == null) return;
            _mine = null;
            SendReady();
            ShowPanel();
            MelonLogger.Msg("[сон] «Назад»: больше не жду");
        }

        private static void HidePanel()
        {
            if (_panel == null || _hidden.Count > 0) return;
            try
            {
                var t = _panel.transform;
                for (int i = 0; i < t.childCount; i++)
                {
                    var go = t.GetChild(i).gameObject;
                    if (!go.activeSelf) continue;
                    go.SetActive(false);
                    _hidden.Add(go);
                }
            }
            catch (Exception e) { MelonLogger.Warning("[сон] спрятать меню: " + e.Message); ShowPanel(); }
        }

        private static void ShowPanel()
        {
            if (_hidden.Count == 0) return;
            foreach (var go in _hidden)
                try { if (go != null) go.SetActive(true); } catch { }
            _hidden.Clear();
        }

        // Патч: меню отдыха закрыто не нами — больше не готовы.
        public static void OnPanelClosed()
        {
            if (_starting) return;
            ShowPanel();
            if (_mine == null) return;
            _mine = null;
            _panel = null;
            SendReady();
            MelonLogger.Msg("[сон] передумал");
        }

        private static void SendReady()
        {
            if (Session.Mode == Mode.Host) { HostSet(Session.LocalId, _mine); return; }
            Session.SendGameEvent(ReadyMsg());
        }

        private static byte[] ReadyMsg()
        {
            var w = Msg.New(MsgType.SleepReady);
            w.Write(_mine != null);
            w.Write((byte)(_mine?.Kind ?? Kind.Sleep));
            w.Write(_mine?.Hours ?? 0);
            w.Write(_mine?.Full ?? false);
            w.Write(_mine?.Need ?? 0);
            w.Write(_mine?.Exact ?? 0f);   // 0.19.27
            return Msg.Done(w);
        }

        // Переподключились к хосту: он нас удалял и забыл, что мы готовы, — а мы всё ещё на экране ожидания.
        public static byte[] ReadyAgain()
        {
            if (Session.Mode != Mode.Client || _mine == null) return null;
            MelonLogger.Msg("[сон] переподключился — снова сообщаю хосту, что готов");
            return ReadyMsg();
        }

        // Те же проверки, что делает Panel_Rest.DoRest перед сном (игра 2.59): не устал — «Вы не устали».
        private static bool GameRefusesSleep(Panel_Rest panel, out string why)
        {
            why = "";
            try
            {
                var rest = GameManager.GetRestComponent();
                var fat = GameManager.GetFatigueComponent();
                if (panel == null || rest == null || fat == null) return false;
                if (rest.AllowUnlimitedSleep() || rest.RestNeededForAffliction()) return false;
                if (panel.m_AllowRestFatigueThreshold < fat.m_CurrentFatigue) return false;
                why = $"не устал (усталость {fat.m_CurrentFatigue:0}, нужно больше {panel.m_AllowRestFatigueThreshold:0})";
                return true;
            }
            catch { return false; }
        }

        // Сколько часов сна нужно нам до полного отдыха.
        private static int NeedHours()
        {
            try
            {
                var f = GameManager.GetFatigueComponent();
                var rest = GameManager.GetRestComponent();
                float per = rest != null ? rest.m_ReduceFatiguePerHourRest : 0f;
                if (f == null || per <= 0f) return 8;
                return Math.Clamp(Mathf.CeilToInt(f.m_CurrentFatigue / per), 1, 12);
            }
            catch { return 8; }
        }

        // ---------- Хост ----------

        public static void HostReceive(int id, BinaryReader r)
        {
            bool ready = r.ReadBoolean();
            var kind = (Kind)r.ReadByte();
            int hours = r.ReadInt32();
            bool full = r.ReadBoolean();
            int need = r.BaseStream.Position < r.BaseStream.Length ? r.ReadInt32() : hours;
            float exact = r.BaseStream.Length - r.BaseStream.Position >= 4 ? r.ReadSingle() : 0f;
            HostSet(id, ready ? new Ready { Kind = kind, Hours = hours, Full = full, Need = need, Exact = exact } : null);
            if (Session.Players.TryGetValue(id, out var pl)) MelonLogger.Msg($"[сон] {pl.Name}: " + (ready ? $"готов ({hours} ч)" : "передумал"));
        }

        private static void HostSet(int id, Ready r)
        {
            if (r == null) HostReady.Remove(id);
            else HostReady[id] = r;
            _nextWait = 0f;
        }

        public static void Tick(float now)
        {
            if (Session.Mode == Mode.Off || !GameApi.InGame()) { _panelWasOpen = false; return; }
            // Открыто ли меню отдыха — здесь, а не в OnGUI: раньше это считалось только на событии Layout,
            // и если его не было, выбор «Со всеми / Отдельно» не появлялся.
            bool open = RestPanelOpen();
            if (!open) _soloChoice = false;   // каждый раз заново начинаем с общего сна
            _panelWasOpen = open;
            if (Session.Mode == Mode.Host) HostTick(now);
            TickShared(now);
            if (_mine != null)
            {
                // Меню закрыли, а патч закрытия не сработал — тоже передумали.
                if (_panel == null || !_panel.gameObject.activeInHierarchy) { OnPanelClosed(); return; }
            }
            else if (_hidden.Count > 0) ShowPanel();
            else if (_waitText.Length > 0 && now >= _nextHint)
            {
                // Нас ждут, чтобы лечь спать или пропустить время.
                _nextHint = now + 15f;
                GameApi.HudMessage(_allPass
                    ? UI.L.T("Игроки ждут, чтобы пропустить время: зайди в меню отдыха, вкладка «Ждать»", "Players are waiting to pass time: open the rest menu, Pass Time tab")
                    : UI.L.T("Игроки ждут, чтобы лечь спать: зайди в меню отдыха", "Players are waiting to sleep: open the rest menu"));
            }
        }

        private static void HostTick(float now)
        {
            // Ушедшие и отключившиеся не считаются.
            foreach (var id in HostReady.Keys.ToList())
                if (id != Session.LocalId && !Session.Players.ContainsKey(id)) HostReady.Remove(id);
            if (HostReady.Count == 0) { if (_waitText.Length > 0 || _hostHours != 0) Broadcast(""); return; }
            if (now < _nextWait) return;
            _nextWait = now + 1f;
            var waiting = new List<string>();
            if (!HostReady.ContainsKey(Session.LocalId)) waiting.Add(Session.LocalName);
            foreach (var p in OthersInGame()) if (!HostReady.ContainsKey(p.Id)) waiting.Add(p.Name);
            foreach (var p in OthersLoading()) if (!waiting.Contains(p.Name)) waiting.Add(p.Name + " (грузится)");
            if (waiting.Count > 0) { Broadcast(string.Join(", ", waiting)); return; }
            // Готовы все: время сна задаёт хост (если хост просто ждёт — его часы).
            // Хост выбрал «до полного отдыха» — спим, сколько нужно самому уставшему из спящих.
            var host = HostReady[Session.LocalId];
            int hours = host.Hours;
            if (host.Kind == Kind.Sleep && host.Full)
            {
                hours = 1;
                foreach (var r in HostReady.Values) if (r.Kind == Kind.Sleep) hours = Math.Max(hours, r.Need);
                hours = Math.Clamp(hours, 1, 12);
            }
            float exactStart = host.Kind == Kind.PassTime ? ExactOf(host) : hours;
            var w = Msg.New(MsgType.SleepStart);
            w.Write(hours);
            w.Write(false);
            w.Write(exactStart);   // 0.19.27: ожидание в минутах не округляется до часа
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg($"[сон] все готовы, начинаем: {hours} ч" + (host.Full ? " (до отдыха самого уставшего)" : ""));
            HostReady.Clear();
            Broadcast("");
            Start(hours, false, exactStart);
        }

        private static void Broadcast(string waiting)
        {
            _waitText = waiting;
            HostReady.TryGetValue(Session.LocalId, out var host);
            _hostHours = waiting.Length > 0 && host != null ? host.Hours : 0;
            _hostFull = host != null && host.Full;
            _hostPass = host != null && host.Kind == Kind.PassTime;
            _hostExact = waiting.Length > 0 && host != null ? ExactOf(host) : 0f;
            _allPass = HostReady.Count > 0 && HostReady.Values.All(r => r != null && r.Kind == Kind.PassTime);
            var w = Msg.New(MsgType.SleepWait);
            w.Write(waiting);
            // 0.18.5: что выбрал хост — для экрана ожидания («Хост выбрал: спать 8 ч»). Старые версии это не читают.
            w.Write(_hostHours);
            w.Write(_hostFull);
            w.Write(_hostPass);
            w.Write(_allPass);   // 0.19.27
            w.Write(_hostExact);
            Session.SendGameEvent(Msg.Done(w));
        }

        // ---------- Все ----------

        public static void ReceiveWait(BinaryReader r)
        {
            _waitText = r.ReadString();
            _hostHours = 0; _hostFull = false; _hostPass = false; _allPass = false;
            if (r.BaseStream.Length - r.BaseStream.Position >= 6)
            {
                _hostHours = r.ReadInt32();
                _hostFull = r.ReadBoolean();
                _hostPass = r.ReadBoolean();
            }
            _hostExact = 0f;
            if (r.BaseStream.Length - r.BaseStream.Position >= 1) _allPass = r.ReadBoolean();
            if (r.BaseStream.Length - r.BaseStream.Position >= 4) _hostExact = r.ReadSingle();
            if (Bot.Active) Bot.OnSleepWait(_waitText);
        }

        public static void ReceiveStart(BinaryReader r)
        {
            int hours = r.ReadInt32();
            bool full = r.ReadBoolean();
            float exact = r.BaseStream.Length - r.BaseStream.Position >= 4 ? r.ReadSingle() : hours;
            _waitText = "";
            if (Bot.Active) { Bot.OnSleepStart(hours); return; }
            Start(hours, full, exact);
        }

        private static void Start(int hours, bool full, float exact)
        {
            if (exact <= 0f) exact = hours;
            var mine = _mine;
            var panel = _panel;
            ShowPanel();
            _mine = null;
            _panel = null;
            _hostHours = 0;
            if (mine == null || panel == null || !panel.gameObject.activeInHierarchy)
            {
                MelonLogger.Msg("[сон] остальные легли, а мы уже не в меню отдыха");
                return;
            }
            _starting = true;
            try
            {
                _plannedEnd = 0f;
                bool pass = mine.Kind == Kind.PassTime;
                try { _plannedEnd = GameApi.TotalTime() + (pass ? exact : hours) / 24f; } catch { }
                if (pass) panel.DoPassTime(exact);
                else panel.DoRest(hours, full);
                _inShared = true;
                _sawResting = false;
                _stopSent = false;
                _sharedSince = Time.unscaledTime;
                MelonLogger.Msg($"[сон] начали все вместе: {(pass ? "ждём " + Dur(exact) : "спим " + hours + " ч")}");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[сон] начать: " + e.Message);
            }
            finally
            {
                _starting = false;
            }
        }

        private static bool RestPanelOpen()
        {
            try { return InterfaceManager.TryGetPanel<Panel_Rest>(out var p) && p != null && p.IsEnabled(); }
            catch { return false; }
        }

        // Поверх меню отдыха: кнопка «Ждём остальных» (и выбор «Со всеми / Отдельно», если хост разрешил),
        // а пока ждём — тёмный экран с именами тех, кого ждём, и одной кнопкой «Назад».
        // Вид как у меню отдыха игры, в точках 1280×720 (UI/Widgets.cs), тексты на двух языках (0.19.24).
        public static void Draw()
        {
            UI.Widgets.Scaled();
            try
            {
                var mine = _mine;
                if (mine == null || _panel == null) DrawChoice();
                else DrawWait(mine);
            }
            finally { UI.Widgets.Unscaled(); }
        }

        private static void DrawWait(Ready mine)
        {
            UI.Theme.Apply();
            var full = new Rect(0f, 0f, Screen.width, Screen.height);
            UI.Theme.Fill(full, new Color(0f, 0f, 0f, 0.80f));
            UI.Theme.Sprite(full, "vignetteFullscreen", new Color(0f, 0f, 0f, 0.74f));
            float sw = UI.Widgets.SW, sh = UI.Widgets.SH, cx = 40f, cw = sw - 80f, y = sh * 0.24f;
            UI.Widgets.Centered(new Rect(cx, y, cw, 44f), UI.L.T("Ждём остальных", "Waiting for the others"), 30f, UI.Theme.White);
            y += 50f;
            UI.Widgets.Line(sw / 2f - 160f, y, 320f);
            y += 14f;
            var names = _waitText.Length > 0
                ? _waitText.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).Select(n => n.Replace("<", "‹")).ToList()
                : new List<string>();
            if (names.Count == 0) names.Add("…");
            foreach (var n in names.Take(8))
            {
                UI.Widgets.Centered(new Rect(cx, y, cw, 30f), n, 20f, UI.Theme.White);
                y += 30f;
            }
            if (names.Count > 8) { UI.Widgets.Centered(new Rect(cx, y, cw, 26f), UI.L.T($"и ещё {names.Count - 8}", $"and {names.Count - 8} more"), 15f, UI.Theme.Caption); y += 26f; }
            y += 14f;
            bool pass = mine.Kind == Kind.PassTime;
            string verb = pass ? UI.L.T("ждать", "wait") : UI.L.T("спать", "sleep");
            string plan;
            if (Session.Mode == Mode.Host)
                plan = mine.Full && mine.Kind == Kind.Sleep ? UI.L.T("Спим до отдыха самого уставшего, когда будут готовы все", "We sleep until the most tired one is rested, once everyone is ready")
                     : UI.L.T($"{(pass ? "Ждём" : "Спим")} {Dur(ExactOf(mine))}, когда будут готовы все", $"We {(pass ? "wait" : "sleep")} {Dur(ExactOf(mine))} once everyone is ready");
            else if (_hostHours > 0)
                plan = _hostFull && !_hostPass ? UI.L.T($"Хост выбрал: спать до отдыха самого уставшего. Ты будешь {verb}", $"The host chose: sleep until the most tired one is rested. You will {verb}")
                     : UI.L.T($"Хост выбрал: {(_hostPass ? "ждать" : "спать")} {Dur(_hostExact > 0f ? _hostExact : _hostHours)}. Ты будешь {verb}", $"The host chose: {(_hostPass ? "wait" : "sleep")} {Dur(_hostExact > 0f ? _hostExact : _hostHours)}. You will {verb}");
            else plan = UI.L.T($"Сколько спать, выберет хост. Ты будешь {verb}", $"The host will choose how long. You will {verb}");
            UI.Widgets.Centered(new Rect(cx, y, cw, 26f), plan, 15f, UI.Theme.Body);
            float bw = 214f, by = Mathf.Max(y + 50f, sh * 0.68f);
            if (UI.Widgets.Button(new Rect((sw - bw) / 2f, by, bw, 38f), UI.L.T("Назад", "Back"))) CancelWait();
        }

        // Выбор над меню отдыха игры, полосой в её стиле: «КООП · КАК СПАТЬ», две кнопки и пояснение.
        // Меню игры занимает середину экрана (вкладки «Сон/Ожидание» — до 223 точек над центром), полоса — выше.
        private static void DrawChoice()
        {
            if (!_panelWasOpen || !Coop || LocalResting) return;
            UI.Theme.Apply();
            bool solo = Session.CurrentRules.SoloSleep;
            float sw = UI.Widgets.SW, sh = UI.Widgets.SH;
            float w = 470f, h = solo ? 98f : 64f, x = (sw - w) / 2f;
            float y = Math.Max(8f, sh / 2f - 263f - h - 4f);
            UI.Widgets.Window(new Rect(x, y, w, h), null, null, false);
            UI.Widgets.Centered(new Rect(x, y + 6f, w, 20f), solo ? UI.L.T("КООП · КАК СПАТЬ", "CO-OP · HOW TO SLEEP") : UI.L.T("КООП · СОН", "CO-OP · SLEEP"), 12f, UI.Theme.Caption);
            float ry = y + 26f;
            if (solo)
            {
                float bw = 150f;
                if (UI.Widgets.Button(new Rect(x + w / 2f - bw - 8f, ry, bw, 30f), UI.L.T("Со всеми", "Together"), !_soloChoice)) _soloChoice = false;
                if (UI.Widgets.Button(new Rect(x + w / 2f + 8f, ry, bw, 30f), UI.L.T("Отдельно", "Alone"), _soloChoice)) _soloChoice = true;
                ry += 34f;
                if (_soloChoice)
                {
                    UI.Widgets.Centered(new Rect(x + 10f, ry, w - 20f, 24f), UI.L.T("Спишь отдельно, сколько указано ниже. У остальных время не проматывается", "You sleep alone for as long as set below. Time does not skip for the others"), 13f, UI.Theme.Body);
                    return;
                }
            }
            string label = Session.Mode == Mode.Host ? UI.L.T("Ждём остальных (спим, сколько выбрано ниже)", "Wait for the others (sleep as long as chosen below)")
                                                     : UI.L.T("Ждём остальных (спим, сколько выберет хост)", "Wait for the others (sleep as long as the host chooses)");
            if (UI.Widgets.Button(new Rect(x + 20f, ry, w - 40f, 30f), label, false, 13f)) BeginWait();
        }

        // Бот: сам в меню отдыха не ходит, отмечается готовым по просьбе хоста.
        public static void BotReady(bool ready, int hours)
        {
            _mine = null;
            var w = Msg.New(MsgType.SleepReady);
            w.Write(ready);
            w.Write((byte)Kind.PassTime);
            w.Write(hours);
            w.Write(false);
            w.Write(hours);
            Session.SendGameEvent(Msg.Done(w));
        }
    }

    [HarmonyPatch(typeof(Panel_Rest), nameof(Panel_Rest.DoRest))]
    internal static class Patch_RestDoRest
    {
        private static bool Prefix(Panel_Rest __instance, int restAmount, bool wakeUpAtFullRest) =>
            SleepSync.OnChoose(__instance, false, restAmount, wakeUpAtFullRest);
    }

    [HarmonyPatch(typeof(Panel_Rest), nameof(Panel_Rest.DoPassTime))]
    internal static class Patch_RestDoPassTime
    {
        private static bool Prefix(Panel_Rest __instance, float sleepHours, ref bool __result)
        {
            if (SleepSync.OnChoose(__instance, true, Mathf.RoundToInt(sleepHours), false, sleepHours)) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Panel_Rest), nameof(Panel_Rest.OnCancel))]
    internal static class Patch_RestCancel
    {
        private static void Postfix() => SleepSync.OnPanelClosed();
    }
}
