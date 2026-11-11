using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.IntBackedUnit;
using Il2CppTLD.SteamTrain;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Поезд из DLC Iron Road — один на всех.
    // В каждой локации у поезда есть «машинист» — игрок, чья игра считает его ход. Это тот, кто первым
    // оказался в кабине; если в кабине никого — хост (или игрок с меньшим номером, если хоста тут нет).
    // Машинист 10 раз в секунду рассылает, где стоят вагоны на путях, скорость, давление пара, рычаги,
    // топливо и воду. У остальных поезд едет сам с той же скоростью, а если разошёлся — переставляется.
    // Свои рычаги у пассажира не работают, и котёл у него не считается (иначе топливо тратилось бы дважды).
    // Что меняют пассажиры и те, кто снаружи (починка, залитое топливо и вода, растопка), их игра замечает
    // и рассылает: части поезда целиком, топливо и воду — прибавкой, которую машинист добавляет к себе.
    // Стрелки и поворотные круги общие для всех (TrackElementManager).
    // Переезд в другую локацию: когда поезд машиниста въезжает в переход (LoadScene.PrepareTrainTransition),
    // все, кто сидит в кабине у себя, переезжают вместе с ним через тот же переход. Кто снаружи — остаётся.
    // Где поезд в мире: машинист раз в 5 секунд сообщает, в какой локации и как стоит каждый вагон, а хост
    // хранит это у себя (UserData/TLDCoop/progress/<мир>/trains.txt). Кто заходит в локацию, через 3 секунды
    // ставит вагоны по этому списку: увезённого отсюда поезда нет, привезённый сюда стоит, где оставили.
    // Так поезд один на весь мир, даже если хоста не было там, куда его угнали.
    public static class TrainSync
    {
        private const float FastRate = 0.1f, SlowRate = 1f, OwnerTimeout = 2.5f;

        private const byte KMove = 1, KPart = 2, KLiquid = 3, KTrack = 4, KTransit = 5, KWhere = 6, KAsk = 7;

        private static int _owner = -1;
        private static bool _ownerInside;
        private static float _ownerSeen = -100f, _nextMove, _nextPoll, _nextLookup, _nextTrack;
        private static string _scene = "";

        private static readonly List<TrainCar> Cars = new();
        private static readonly Dictionary<string, CurvySplineRef> Splines = new();
        private static readonly Dictionary<string, string> PartSent = new();       // guid#часть → что уже знают все
        private static readonly Dictionary<string, (long fuel, long water)> Known = new();
        private static string _trackSent;
        private static int _logged;

        private class CurvySplineRef
        {
            public Il2CppFluffyUnderware.Curvy.CurvySpline Spline;
        }

        public static bool IsOwner => _owner == Session.LocalId;
        private static bool Active => Session.Mode != Mode.Off && Session.InSharedWorld && GameApi.InGame();

        // Чужой поезд: свои рычаги и котёл не трогаем.
        internal static bool Passive(TrainCar car) => Active && car != null && Cars.Count > 0 && !IsOwner && _owner >= 0;

        public static void Tick(float now)
        {
            if (!Active) { if (_scene.Length > 0) { Flush(); Reset(); } return; }
            ApplyPending();
            TickTransit(now);
            SaveRegistry(now);
            string scene = GameApi.Scene();
            if (scene != _scene)
            {
                Reset();
                _scene = scene;
                _following = false;
                _pendingExit = null;
                _sceneSince = now;
                _placed = false;
                if (Session.Mode == Mode.Client) Ask();
            }
            if (!_placed && now - _sceneSince > 3f) { _placed = true; try { PlaceFromRegistry(); } catch (Exception e) { Warn("расстановка: " + e.Message); } }
            // Поиск вагонов по всей сцене дорогой (подлагивало каждые 2 с): сразу после входа, потом раз в 20 с.
            if (now >= _nextLookup) { _nextLookup = now + (now - _sceneSince < 10f ? 2f : 20f); Lookup(); }
            try { TickTrack(now); } catch (Exception e) { Warn("стрелки: " + e.Message); }
            if (Cars.Count == 0) return;
            // Приехали в кабине: поезд здесь, это и есть правда — сообщаем сразу, не ждём 3 с
            // (иначе оставшийся в прошлой локации успевал объявить поезд своим).
            if (!_placed && SteamTrain.IsInsideSteamTrain) { _placed = true; _nextWhere = 0f; }

            bool inside = SteamTrain.IsInsideSteamTrain;
            bool stale = now - _ownerSeen > OwnerTimeout;
            if (!IsOwner && ((inside && (stale || !_ownerInside)) || (stale && IAmFallback())))
            {
                _owner = Session.LocalId;
                MelonLogger.Msg($"[поезд] веду поезд сам ({(inside ? "я в кабине" : "в кабине никого")})");
            }

            try
            {
                if (IsOwner)
                {
                    bool moving = false;
                    foreach (var c in Cars) if (c != null && Math.Abs(c.Velocity) > 0.01f) { moving = true; break; }
                    if (now >= _nextMove) { _nextMove = now + (moving ? FastRate : SlowRate); SendMove(inside); }
                    if (_placed && now >= _nextWhere) { _nextWhere = now + 5f; SendWhere(); }
                }
                if (now >= _nextPoll) { _nextPoll = now + 0.5f; PollParts(); PollLiquids(); }
            }
            catch (Exception e) { Warn(e.Message); }
        }

        // Если в кабине никого: ведёт хост, а без хоста — игрок с меньшим номером в этой локации.
        private static bool IAmFallback()
        {
            int best = Session.LocalId;
            float now = Time.unscaledTime;
            foreach (var p in Session.Players.Values)
                if (Session.With(p, _scene) && now - p.LastState < 5f && p.Id < best) best = p.Id;
            return best == Session.LocalId;
        }

        private static void Lookup()
        {
            Cars.Clear();
            foreach (var c in UnityEngine.Object.FindObjectsOfType<TrainCar>())
                if (c != null && !string.IsNullOrEmpty(c.GUID)) Cars.Add(c);
            Splines.Clear();
            foreach (var s in UnityEngine.Object.FindObjectsOfType<SteamTrainTrackSpline>())
                if (s != null && !string.IsNullOrEmpty(s.GUID) && s.TrackSpline != null) Splines[s.GUID] = new CurvySplineRef { Spline = s.TrackSpline };
            if (Cars.Count > 0 && _logged++ < 3) MelonLogger.Msg($"[поезд] в локации вагонов: {Cars.Count}, путей: {Splines.Count}");
        }

        private static string SplineGuid(Il2CppFluffyUnderware.Curvy.CurvySpline sp)
        {
            if (sp == null) return "";
            foreach (var kv in Splines) if (kv.Value.Spline != null && kv.Value.Spline.Pointer == sp.Pointer) return kv.Key;
            return "";
        }

        private static TrainCar Car(string guid)
        {
            foreach (var c in Cars) if (c != null && c.GUID == guid) return c;
            return null;
        }

        // ---------- Ход поезда (машинист → все) ----------

        private static void SendMove(bool inside)
        {
            var w = Msg.New(MsgType.TrainSync);
            w.Write(KMove);
            w.Write(_scene);
            w.Write((byte)Session.LocalId);
            w.Write(inside);
            var list = new List<TrainCar>();
            foreach (var c in Cars) if (c != null && c.SplineController != null) list.Add(c);
            w.Write((byte)list.Count);
            foreach (var c in list)
            {
                var sc = c.SplineController;
                w.Write(c.GUID);
                w.Write(SplineGuid(sc.Spline));
                w.Write(sc.Position);
                w.Write((byte)sc.MovementDirection);
                var st = c.TryCast<SteamTrain>();
                var eng = st?.Engine?.CurrentState;
                w.Write(eng != null);
                if (eng != null)
                {
                    w.Write(eng.m_Velocity);
                    w.Write(eng.m_LastVelocitySign);
                    w.Write(eng.m_SteamPressure);
                    w.Write((byte)eng.m_ThrottlePosition);
                    w.Write(eng.m_BrakesActive);
                    long fuel = st.FuelSource != null ? st.FuelSource.CurrentVolume.m_Units : 0;
                    long water = st.WaterSource != null ? st.WaterSource.CurrentVolume.m_Units : 0;
                    w.Write(fuel);
                    w.Write(water);
                    Known[c.GUID] = (fuel, water);
                }
            }
            Session.SendFast(Msg.Done(w));
        }

        private static void ReceiveMove(BinaryReader r)
        {
            string scene = r.ReadString();
            int from = r.ReadByte();
            bool inside = r.ReadBoolean();
            int n = r.ReadByte();
            bool mine = scene == _scene && from != Session.LocalId;
            if (mine)
            {
                bool meInside = SteamTrain.IsInsideSteamTrain;
                if (IsOwner)
                {
                    // Двое ведут сразу: кабина важнее, при равенстве — меньший номер.
                    if ((inside && !meInside) || (inside == meInside && from < Session.LocalId)) SetOwner(from, inside);
                }
                else SetOwner(from, inside);
            }
            for (int i = 0; i < n; i++)
            {
                string guid = r.ReadString(), spline = r.ReadString();
                float pos = r.ReadSingle();
                var dir = r.ReadByte();
                bool eng = r.ReadBoolean();
                float vel = 0f, sign = 1f, psi = 0f; byte thr = 0; bool brakes = false; long fuel = 0, water = 0;
                if (eng)
                {
                    vel = r.ReadSingle(); sign = r.ReadSingle(); psi = r.ReadSingle(); thr = r.ReadByte(); brakes = r.ReadBoolean();
                    fuel = r.ReadInt64(); water = r.ReadInt64();
                }
                if (!mine || IsOwner) continue;
                var car = Car(guid);
                if (car == null) continue;
                try { ApplyMove(car, spline, pos, dir, eng, vel, sign, psi, thr, brakes, fuel, water); }
                catch (Exception e) { Warn("ход: " + e.Message); }
            }
        }

        private static void SetOwner(int id, bool inside)
        {
            if (_owner != id) MelonLogger.Msg($"[поезд] ведёт игрок {id}" + (inside ? " (в кабине)" : ""));
            _owner = id;
            _ownerInside = inside;
            _ownerSeen = Time.unscaledTime;
        }

        private static void ApplyMove(TrainCar car, string spline, float pos, byte dir, bool eng, float vel, float sign, float psi, byte thr, bool brakes, long fuel, long water)
        {
            var sc = car.SplineController;
            if (sc == null) return;
            if (spline.Length > 0 && Splines.TryGetValue(spline, out var sr) && sr.Spline != null &&
                (sc.Spline == null || sc.Spline.Pointer != sr.Spline.Pointer))
            {
                sc.Spline = sr.Spline;
                sc.Position = pos;
            }
            else if (Math.Abs(sc.Position - pos) > 0.6f) sc.TeleportTo(pos);
            sc.MovementDirection = (Il2CppFluffyUnderware.Curvy.Controllers.MovementDirection)dir;

            var st = car.TryCast<SteamTrain>();
            if (!eng || st == null || st.Engine == null) return;
            var e = new SteamTrainEngine.EngineState
            {
                m_Velocity = vel, m_LastVelocitySign = sign, m_SteamPressure = psi,
                m_ThrottlePosition = (ThrottlePosition)thr, m_BrakesActive = brakes,
            };
            st.Engine.Deserialize(e);
            SendLiquid(car, st);   // своё залитое — машинисту, прежде чем перезаписать его значениями
            if (st.FuelSource != null) st.FuelSource.Set(new ItemLiquidVolume(fuel));
            if (st.WaterSource != null) st.WaterSource.Set(new ItemLiquidVolume(water));
            Known[car.GUID] = (fuel, water);
        }

        // ---------- Части поезда: починка, поломка, котёл (все → все) ----------

        // Плиту в кабине не трогаем: готовка на ней у каждого своя.
        private static readonly string[] PartNames = { "тормоз", "котёл", "отбойник", "колёса" };

        private static SteamTrainComponent Part(SteamTrain st, int i) => i switch
        {
            0 => st.BrakeShoe, 1 => st.Boiler, 2 => st.CowCatcher, 3 => st.Wheels, _ => null,
        };

        // Что поменялось у меня — рассылаем. У пассажира котёл не считается, поэтому его изменения — это
        // его собственные действия (растопил, починил); у машиниста — всё подряд, но не чаще раза в секунду.
        private static void PollParts()
        {
            foreach (var c in Cars)
            {
                var st = c?.TryCast<SteamTrain>();
                if (st == null) continue;
                for (int i = 0; i < PartNames.Length; i++)
                {
                    var part = Part(st, i);
                    if (part == null) continue;
                    var s = part.SerializeComponent();
                    if (s == null) continue;
                    string data = s.m_ComponentDataSerialized ?? "";
                    string key = c.GUID + "#" + i;
                    string sig = s.m_Condition.ToString("0.0") + "|" + data;
                    if (PartSent.TryGetValue(key, out var old) && old == sig) continue;
                    bool first = !PartSent.ContainsKey(key);
                    PartSent[key] = sig;
                    if (first) continue;   // только запомнили, с чего начинаем
                    var w = Msg.New(MsgType.TrainSync);
                    w.Write(KPart);
                    w.Write(_scene);
                    w.Write(c.GUID);
                    w.Write((byte)i);
                    w.Write(s.m_Condition);
                    w.Write(data);
                    Session.SendGameEvent(Msg.Done(w));
                }
            }
        }

        private static void ReceivePart(BinaryReader r)
        {
            string scene = r.ReadString(), guid = r.ReadString();
            int i = r.ReadByte();
            float cond = r.ReadSingle();
            string data = r.ReadString();
            if (scene != _scene) return;
            var st = Car(guid)?.TryCast<SteamTrain>();
            var part = st != null ? Part(st, i) : null;
            if (part == null) return;
            bool changed = Math.Abs(part.Condition - cond) >= 1f;
            part.DeserializeComponent(new SteamTrainComponentState { m_Condition = cond, m_ComponentDataSerialized = data });
            PartSent[guid + "#" + i] = cond.ToString("0.0") + "|" + data;
            if (changed) MelonLogger.Msg($"[поезд] {PartNames[i]}: {cond:0}%");
        }

        // ---------- Топливо и вода, залитые не машинистом ----------

        private static void PollLiquids()
        {
            if (IsOwner) return;
            foreach (var c in Cars)
            {
                var st = c?.TryCast<SteamTrain>();
                if (st != null) SendLiquid(c, st);
            }
        }

        // Залитое сверх того, что прислал машинист, — прибавкой машинисту. Зовётся и перед каждым ходом от машиниста:
        // раньше ход приходил раньше опроса (раз в 0,5 с) и затирал залитое, а прибавка не уходила.
        private static void SendLiquid(TrainCar c, SteamTrain st)
        {
            if (!Known.TryGetValue(c.GUID, out var k)) return;
            long fuel = st.FuelSource != null ? st.FuelSource.CurrentVolume.m_Units : 0;
            long water = st.WaterSource != null ? st.WaterSource.CurrentVolume.m_Units : 0;
            long df = Math.Max(0, fuel - k.fuel), dw = Math.Max(0, water - k.water);
            long eps = ItemLiquidVolume.Centiliter.m_Units;
            if (df <= eps && dw <= eps) return;
            Known[c.GUID] = (Math.Max(fuel, k.fuel), Math.Max(water, k.water));
            var w = Msg.New(MsgType.TrainSync);
            w.Write(KLiquid);
            w.Write(_scene);
            w.Write(c.GUID);
            w.Write(df > eps ? df : 0L);
            w.Write(dw > eps ? dw : 0L);
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg("[поезд] залил в поезд, отправляю машинисту");
        }

        private static void ReceiveLiquid(BinaryReader r)
        {
            string scene = r.ReadString(), guid = r.ReadString();
            long df = r.ReadInt64(), dw = r.ReadInt64();
            if (scene != _scene || !IsOwner) return;
            var st = Car(guid)?.TryCast<SteamTrain>();
            if (st == null) return;
            if (df > 0 && st.FuelSource != null) st.FuelSource.Add(new ItemLiquidVolume(df));
            if (dw > 0 && st.WaterSource != null) st.WaterSource.Add(new ItemLiquidVolume(dw));
            MelonLogger.Msg("[поезд] друг залил топливо или воду — добавил");
        }

        // ---------- Стрелки и поворотные круги (все → все) ----------

        private static void TickTrack(float now)
        {
            if (now < _nextTrack) return;
            _nextTrack = now + 1f;
            var all = TrackElementManager.SerializeAll();
            if (all == null || all.Count == 0) return;
            var sb = new StringBuilder();
            for (int i = 0; i < all.Count; i++) { var t = all[i]; if (t != null) sb.Append(t.m_Guid).Append('=').Append(t.m_ElementDataSerialized).Append('\n'); }
            string sig = sb.ToString();
            if (_trackSent == null) { _trackSent = sig; return; }
            if (sig == _trackSent) return;
            _trackSent = sig;
            var w = Msg.New(MsgType.TrainSync);
            w.Write(KTrack);
            w.Write(_scene);
            w.Write(all.Count);
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                w.Write(t?.m_Guid ?? "");
                w.Write(t?.m_ElementDataSerialized ?? "");
            }
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg("[поезд] перевёл стрелку — отправил всем");
        }

        private static void ReceiveTrack(BinaryReader r)
        {
            string scene = r.ReadString();
            int n = r.ReadInt32();
            var list = new Il2CppSystem.Collections.Generic.List<TrackElementState>();
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                string g = r.ReadString(), d = r.ReadString();
                list.Add(new TrackElementState { m_Guid = g, m_ElementDataSerialized = d });
                sb.Append(g).Append('=').Append(d).Append('\n');
            }
            if (scene != _scene) return;
            TrackElementManager.DeserializeAll(list);
            _trackSent = sb.ToString();
            MelonLogger.Msg("[поезд] друг перевёл стрелку");
        }

        // ---------- Переезд в другую локацию ----------

        private static bool _following;
        private static LoadScene _pendingExit;
        private static string _pendingScene;
        private static float _pendingAt;

        internal static void OnTrainTransition(LoadScene ls)
        {
            if (!Active || ls == null) return;
            if (_following) { _following = false; return; }   // сами едем за машинистом
            if (!SteamTrain.IsInsideSteamTrain) return;
            string guid = "";
            try { guid = ls.GetGUID() ?? ""; } catch { }
            var w = Msg.New(MsgType.TrainSync);
            w.Write(KTransit);
            w.Write(_scene);
            w.Write((byte)Session.LocalId);
            w.Write(guid);
            var p = ls.transform.position;
            w.Write(p.x); w.Write(p.y); w.Write(p.z);
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg($"[поезд] поезд уезжает в {ls.GetSceneToLoad()} — зову пассажиров");
        }

        private static void ReceiveTransit(BinaryReader r)
        {
            string scene = r.ReadString();
            int from = r.ReadByte();
            string guid = r.ReadString();
            var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            if (scene != _scene || from == Session.LocalId || !SteamTrain.IsInsideSteamTrain) return;
            LoadScene ls = null;
            try { if (guid.Length > 0) ls = LoadScene.FindLoadSceneByGuid(guid); } catch { }
            if (ls == null)
            {
                float best = 400f;
                foreach (var l in UnityEngine.Object.FindObjectsOfType<LoadScene>())
                {
                    if (l == null) continue;
                    float d = (l.transform.position - pos).sqrMagnitude;
                    if (d < best) { best = d; ls = l; }
                }
            }
            if (ls == null) { Warn("не нашёл переход, по которому уехал поезд"); return; }
            string name = Session.Players.TryGetValue(from, out var pl) ? pl.Name : "машинист";
            GameApi.HudMessage($"Едем дальше с {name}");
            MelonLogger.Msg($"[поезд] еду в кабине за {name} в {ls.GetSceneToLoad()}");
            _following = true;
            try { ls.PrepareTrainTransition(); }
            catch (Exception e) { _following = false; Warn("переезд: " + e.Message); return; }
            // Если игра сама не довела переезд до загрузки, доводим через 5 секунд.
            _pendingExit = ls;
            _pendingScene = _scene;
            _pendingAt = Time.unscaledTime + 5f;
        }

        private static void TickTransit(float now)
        {
            if (_pendingExit == null || now < _pendingAt) return;
            var ls = _pendingExit;
            _pendingExit = null;
            if (GameApi.Scene() != _pendingScene || !SteamTrain.IsInsideSteamTrain) return;
            MelonLogger.Msg("[поезд] переезд сам не начался — запускаю");
            _following = true;
            try { ls.HandleTrainTransition(); }
            catch (Exception e) { _following = false; Warn("переезд: " + e.Message); }
        }

        // ---------- Где стоит поезд (список у хоста) ----------

        private class Entry { public string Scene, Json, Sig; }
        private static readonly Dictionary<string, Entry> Registry = new();
        private static bool _placed, _registryLoaded, _registryDirty;
        private static float _sceneSince, _nextWhere, _nextSave;
        private static string _registryWorld;   // файл мира, к которому относится список (у хоста)

        private static void SendWhere()
        {
            foreach (var c in Cars)
            {
                if (c == null || string.IsNullOrEmpty(c.GUID)) continue;
                var st = c.Serialize();
                if (st == null) continue;
                var sc = c.SplineController;
                string sig = _scene + "|" + (sc != null ? Mathf.RoundToInt(sc.Position * 2f) : 0) + "|" + (st.m_TrainCarDataSerialized?.Length ?? 0);
                if (Registry.TryGetValue(c.GUID, out var old) && old.Sig == sig) continue;
                string json = FastJson.Serialize(st) ?? "";
                if (json.Length == 0) continue;
                Store(c.GUID, _scene, json, sig);
                var w = Msg.New(MsgType.TrainSync);
                w.Write(KWhere);
                w.Write(c.GUID);
                w.Write(_scene);
                w.Write(json);
                Session.SendGameEvent(Msg.Done(w));
            }
        }

        // Пришло, пока я на экране загрузки (раньше терялось, и список хоста оставался старым): применяем после загрузки.
        private static readonly Dictionary<string, (string scene, string json)> PendingWhere = new();
        private static bool _askPending;

        private static void ApplyPending()
        {
            if (PendingWhere.Count > 0)
            {
                foreach (var kv in PendingWhere) Store(kv.Key, kv.Value.scene, kv.Value.json);
                PendingWhere.Clear();
            }
            if (_askPending) { _askPending = false; try { AnswerAsk(); } catch (Exception e) { Warn("список: " + e.Message); } }
        }

        private static void Store(string guid, string scene, string json, string sig = "")
        {
            CheckWorld();
            if (Session.Mode == Mode.Host && _registryWorld == null) _registryWorld = RegistryFile();
            if (Registry.TryGetValue(guid, out var old) && old.Scene != scene)
                MelonLogger.Msg($"[поезд] вагон {guid} теперь в {scene} (был в {old.Scene})");
            Registry[guid] = new Entry { Scene = scene, Json = json, Sig = sig };
            _registryDirty = true;
        }

        private static void ReceiveWhere(BinaryReader r)
        {
            string guid = r.ReadString(), scene = r.ReadString(), json = r.ReadString();
            PendingWhere.Remove(guid);   // живое свежее отложенного с загрузки
            Store(guid, scene, json);
            if (_placed && scene.Length > 0) { try { Reconcile(guid, scene, json); } catch (Exception e) { Warn("вагон: " + e.Message); } }
        }

        // Уже стою в локации, а вагон переехал: увезли отсюда — убираю свою копию (раньше она оставалась,
        // её начинал вести оставшийся, и место в списке хоста скакало); привезли сюда — ставлю. Кабина важнее.
        private static void Reconcile(string guid, string scene, string json)
        {
            if (SteamTrain.IsInsideSteamTrain) return;
            var car = Car(guid);
            if (scene != _scene)
            {
                if (car == null) return;
                MelonLogger.Msg($"[поезд] вагон {guid} уехал в {scene} — убираю отсюда");
                TrainCar.RemoveTrainCar(car);
                UnityEngine.Object.Destroy(car.gameObject);
                Cars.RemoveAll(c => c != null && c.Pointer == car.Pointer);   // Destroy — в конце кадра, Lookup нашёл бы его снова
                return;
            }
            if (car != null) return;
            var st = FastJson.Deserialize<SavedTrainCarState>(json);
            if (st == null) return;
            string asset = "";
            try { asset = st.m_AssetReference?.AssetGUID ?? ""; } catch { }
            car = TrainCar.FindOrCreateTrainCar(guid, asset, out bool made);
            if (car == null) return;
            car.Deserialize(st);
            if (made) MelonLogger.Msg($"[поезд] вагон {guid} привезли сюда — поставил");
            Lookup();
        }

        // Клиент зашёл в локацию: просит у хоста весь список (его поезд мог увезти кто-то ещё).
        private static void Ask()
        {
            var w = Msg.New(MsgType.TrainSync);
            w.Write(KAsk);
            w.Write((byte)Session.LocalId);
            Session.SendGameEvent(Msg.Done(w));
        }

        private static void ReceiveAsk(BinaryReader r)
        {
            r.ReadByte();
            AnswerAsk();
        }

        private static void AnswerAsk()
        {
            if (Session.Mode != Mode.Host) return;
            LoadRegistry();
            foreach (var kv in Registry)
            {
                var w = Msg.New(MsgType.TrainSync);
                w.Write(KWhere);
                w.Write(kv.Key);
                w.Write(kv.Value.Scene);
                w.Write(kv.Value.Json);
                Session.SendGameEvent(Msg.Done(w));
            }
        }

        private static void PlaceFromRegistry()
        {
            if (Session.Mode == Mode.Host) LoadRegistry();
            if (Registry.Count == 0 || SteamTrain.IsInsideSteamTrain) return;
            Lookup();
            int moved = 0, removed = 0;
            var gone = new List<TrainCar>();
            foreach (var kv in Registry)
            {
                var car = Car(kv.Key);
                if (kv.Value.Scene == _scene)
                {
                    var st = FastJson.Deserialize<SavedTrainCarState>(kv.Value.Json);
                    if (st == null) continue;
                    if (car == null)
                    {
                        string asset = "";
                        try { asset = st.m_AssetReference?.AssetGUID ?? ""; } catch { }
                        car = TrainCar.FindOrCreateTrainCar(kv.Key, asset, out bool made);
                        if (car != null && made) MelonLogger.Msg($"[поезд] вагон {kv.Key} привезли сюда — поставил");
                    }
                    if (car == null) continue;
                    car.Deserialize(st);
                    moved++;
                }
                else if (car != null)
                {
                    MelonLogger.Msg($"[поезд] вагон {kv.Key} увезли в {kv.Value.Scene} — убираю отсюда");
                    TrainCar.RemoveTrainCar(car);
                    UnityEngine.Object.Destroy(car.gameObject);
                    gone.Add(car);
                    removed++;
                }
            }
            if (moved + removed > 0) MelonLogger.Msg($"[поезд] по списку хоста: поставил {moved}, убрал {removed}");
            Lookup();
            foreach (var g in gone) Cars.RemoveAll(c => c != null && c.Pointer == g.Pointer);   // уничтожатся только в конце кадра
        }

        // Хранение у хоста: строки «guid \t локация \t json».
        private static string RegistryFile()
        {
            string dir = PlayerProgress.WorldFolder();
            return dir == null ? null : Path.Combine(dir, "trains.txt");
        }

        private static void LoadRegistry()
        {
            CheckWorld();
            string file = RegistryFile();
            if (file == null || (_registryLoaded && _registryWorld == file)) return;
            _registryWorld = file;
            MergeFile(file);
            MelonLogger.Msg($"[поезд] список вагонов хоста: {Registry.Count}");
        }

        // Читаем файл, а свежие, ещё не записанные строки оставляем поверх него.
        private static void MergeFile(string file)
        {
            var mine = new Dictionary<string, Entry>(Registry);
            Registry.Clear();
            _registryLoaded = true;
            try
            {
                if (File.Exists(file))
                    foreach (var line in File.ReadAllLines(file))
                    {
                        var p = line.Split('\t');
                        if (p.Length == 3) Registry[p[0]] = new Entry { Scene = p[1], Json = p[2], Sig = "" };
                    }
            }
            catch (Exception e) { Warn("чтение списка: " + e.Message); }
            foreach (var kv in mine) Registry[kv.Key] = kv.Value;
        }

        private static void SaveRegistry(float now)
        {
            if (Session.Mode != Mode.Host || !_registryDirty || now < _nextSave) return;
            _nextSave = now + 10f;
            CheckWorld();
            if (!_registryDirty) return;
            string file = RegistryFile();
            if (file == null) return;
            if (!_registryLoaded || _registryWorld != file) { _registryWorld = file; MergeFile(file); }
            WriteRegistry(file);
        }

        private static void WriteRegistry(string file)
        {
            _registryDirty = false;
            var lines = new List<string>();
            foreach (var kv in Registry) lines.Add(kv.Key + "\t" + kv.Value.Scene + "\t" + kv.Value.Json.Replace("\t", " ").Replace("\n", " "));
            try { File.WriteAllLines(file, lines); } catch (Exception e) { Warn("запись списка: " + e.Message); }
        }

        // Хост загрузил другой мир, не останавливая сервер: список прошлого мира дописываем в его файл
        // и забываем. Раньше он уходил в trains.txt нового мира, и поезд нового мира вставал по старому.
        private static void CheckWorld()
        {
            if (Session.Mode != Mode.Host || _registryWorld == null) return;
            string file = RegistryFile();
            if (file == null || file == _registryWorld) return;
            MelonLogger.Msg("[поезд] мир сменился: список вагонов прошлого мира записан в его файл и забыт");
            ForgetWorld();
        }

        // Несохранённое — в файл своего мира (хост вышел в меню: поезд мог ехать в последние 10 с).
        private static void Flush()
        {
            if (!_registryDirty || _registryWorld == null) return;
            if (!_registryLoaded) MergeFile(_registryWorld);
            WriteRegistry(_registryWorld);
        }

        // Мир сменился или сервер остановлен: несохранённое — в файл своего мира, список — забыть.
        internal static void ForgetWorld()
        {
            Flush();
            Registry.Clear();
            PendingWhere.Clear();
            _registryLoaded = false;
            _registryDirty = false;
            _registryWorld = null;
            _placed = false;
        }

        // ---------- Сеть ----------

        public static void Receive(BinaryReader r)
        {
            if (Session.Mode == Mode.Off || !Session.InSharedWorld) return;
            byte k = r.ReadByte();
            if (!GameApi.InGame())
            {
                // Экран загрузки: место вагонов и вопрос друга не теряем, остальное здесь не нужно.
                try
                {
                    if (k == KWhere) { string g = r.ReadString(), sc = r.ReadString(), js = r.ReadString(); PendingWhere[g] = (sc, js); }
                    else if (k == KAsk && Session.Mode == Mode.Host) _askPending = true;
                }
                catch (Exception e) { Warn("приём: " + e.Message); }
                return;
            }
            try
            {
                switch (k)
                {
                    case KMove: ReceiveMove(r); break;
                    case KPart: ReceivePart(r); break;
                    case KLiquid: ReceiveLiquid(r); break;
                    case KTrack: ReceiveTrack(r); break;
                    case KTransit: ReceiveTransit(r); break;
                    case KWhere: ReceiveWhere(r); break;
                    case KAsk: ReceiveAsk(r); break;
                }
            }
            catch (Exception e) { Warn("приём: " + e.Message); }
        }

        private static float _nextWarn;
        private static void Warn(string text)
        {
            if (Time.unscaledTime < _nextWarn) return;
            _nextWarn = Time.unscaledTime + 10f;
            MelonLogger.Warning("[поезд] " + text);
        }

        public static void Reset()
        {
            _owner = -1;
            _ownerInside = false;
            _ownerSeen = -100f;
            _scene = "";
            _trackSent = null;
            Cars.Clear();
            Splines.Clear();
            PartSent.Clear();
            Known.Clear();
            _nextLookup = 0f;
            _nextTrack = 0f;
        }
    }

    // Пассажир: рычаги не работают, котёл и двигатель не считаются — их считает машинист.
    [HarmonyPatch(typeof(SteamTrain), nameof(SteamTrain.UpdateControls))]
    internal static class Patch_TrainControls
    {
        private static bool Prefix(SteamTrain __instance) => !TrainSync.Passive(__instance);
    }

    [HarmonyPatch(typeof(SteamTrainEngine), nameof(SteamTrainEngine.UpdateEngine))]
    internal static class Patch_TrainEngine
    {
        private static bool Prefix(SteamTrainEngine __instance) => !TrainSync.Passive(__instance.m_SteamTrain);
    }

    [HarmonyPatch(typeof(SteamTrainBoiler), nameof(SteamTrainBoiler.UpdateComponent))]
    internal static class Patch_TrainBoiler
    {
        private static bool Prefix(SteamTrainBoiler __instance) => !TrainSync.Passive(__instance.Train);
    }

    [HarmonyPatch(typeof(LoadScene), nameof(LoadScene.PrepareTrainTransition))]
    internal static class Patch_TrainTransition
    {
        private static void Prefix(LoadScene __instance) => TrainSync.OnTrainTransition(__instance);
    }
}
