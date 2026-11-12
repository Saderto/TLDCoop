using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Проверка синхронизации. Раз в 20 секунд друг присылает хосту, что у него лежит на земле
    // в текущей локации, время суток и погоду. Хост, если он в той же локации, сравнивает со своим
    // и пишет итог в журнал мода и в лог MelonLoader (строки «[проверка]»).
    // Расхождение должно повториться две проверки подряд: предмет, который как раз летит
    // по сети, ошибкой не считается.
    // Повторилось расхождение в вещах на земле — хост сам присылает другу правильную раскладку вокруг него
    // (как при входе в локацию), не чаще раза в 2 минуты.
    public static class SyncCheck
    {
        // Хэш, одинаковый у всех игроков: string.GetHashCode в .NET 6 свой в каждом запуске игры,
        // и с ним любой непустой ящик у хоста и у друга всегда выглядел «разным».
        private static int Fnv(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in s ?? "") { h ^= ch; h *= 16777619; }
                return (int)h;
            }
        }

        private const float Interval = 20f;
        private const float Tolerance = 1f;    // метры
        private static float _next;
        private static readonly Dictionary<int, string> LastProblem = new();
        private static readonly Dictionary<int, float> LastFix = new();
        // Какие расхождения ушли с последней починкой. 0.19.36: на вышке в CoastalRegion у GNOM после починки шиповника
        // (00:07:58) не было патронов и досок, которые лежали у хоста, а починка ждала 120 с до 00:09:58 — вещи появились
        // перед глазами. Теперь, если в расхождении есть вещи, которых не было в прошлой починке, ждём только 15 с.
        private static readonly Dictionary<int, HashSet<string>> LastFixKeys = new();
        private static int _fixesEarly;
        public static int FixesEarly => _fixesEarly;

        private static bool FixDue(int playerId, float now, float cooldown, List<Entry> onlyMine, List<Entry> left)
        {
            if (!LastFix.TryGetValue(playerId, out float lf) || now - lf > cooldown) return true;
            if (now - lf <= 15f || !LastFixKeys.TryGetValue(playerId, out var keys)) return false;
            bool fresh = onlyMine.Any(e => !keys.Contains(StateKey(e))) || left.Any(e => !keys.Contains(StateKey(e)));
            if (fresh) { _fixesEarly++; MelonLogger.Msg($"[проверка] после прошлой починки ({now - lf:0} с назад) разошлись другие вещи — чиню, не дожидаясь 120 с"); }
            return fresh;
        }

        private static void NoteFix(int playerId, float now, List<Entry> onlyMine, List<Entry> left)
        {
            LastFix[playerId] = now;
            var keys = new HashSet<string>();
            foreach (var e in onlyMine) keys.Add(StateKey(e));
            foreach (var e in left) keys.Add(StateKey(e));
            LastFixKeys[playerId] = keys;
        }
        private static readonly Dictionary<int, (string scene, float since)> SceneSince = new();

        public static string LastResult { get; private set; } = "";
        public static string LastShort { get; private set; } = "";

        private struct Entry
        {
            public string Prefab;
            public Vector3 Pos;
            public bool Radial;   // разложено спавнером (палки, перья): у хоста вдали от него такие убраны
            public string State;  // котелок, факел, лампа: что в нём / горит ли (0.19.23); "" — не сравниваем
            public GearItem Gi;   // только свои
        }

        // Короткое состояние вещи для сверки (0.19.23). Котелок, которым я сейчас пользуюсь, не сравниваем:
        // он меняется прямо сейчас. Литры в котелке не берём — снег тает у каждого по своим часам.
        private static string StateOf(GearItem gi)
        {
            try
            {
                var pot = gi.GetComponent<CookingPotItem>();
                if (pot != null)
                {
                    if (WorldExtra.PotInUseByMe(gi, out _)) return "";
                    var p = WorldExtra.PotSig(gi).Split('|');
                    return p.Length >= 5 ? "P" + p[0] + "|" + p[1] + "|" + p[4] : "";
                }
                var torch = gi.m_TorchItem;
                if (torch != null) return "T" + (int)torch.m_State;
                var lamp = gi.GetComponent<Il2CppTLD.Gear.KeroseneLampItem>();
                if (lamp != null) return "L" + (lamp.IsOn() ? 1 : 0);
                var flare = gi.GetComponent<FlareItem>();
                if (flare != null) return "F" + (int)flare.m_State;
                var fl = gi.GetComponent<FlashlightItem>();
                if (fl != null) return "S" + (fl.IsOn() ? 1 : 0);
            }
            catch { }
            return "";
        }

        private static List<Entry> LooseItems()
        {
            var list = new List<Entry>();
            var radial = RadialSync.SpawnedObjects();
            foreach (var gi in UnityEngine.Object.FindObjectsOfType<GearItem>())
            {
                if (gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer || !gi.gameObject.activeInHierarchy || ItemSync.IsPersonal(gi) || ItemSync.InPot(gi) || AnimalSync.InAnimal(gi) || FlareShots.IsFiredRound(gi)) continue;
                list.Add(new Entry { Prefab = ItemSync.PrefabName(gi), Pos = ItemSync.InspectingByMe(gi) && ItemSync.Inspecting != null && ItemSync.Inspecting.Pointer == gi.Pointer ? ItemSync.InspectPos : gi.transform.position, Radial = radial.Contains(gi.gameObject.Pointer), State = StateOf(gi), Gi = gi });
            }
            return list;
        }

        // Сбор идёт по кадрам: вещи на земле, потом по одному виду объектов за кадр
        // (раньше всё в одном кадре — рывок 85-115 мс раз в 20 с и у друга, и у хоста).
        private const int Steps = 7;   // 0 — вещи на земле, 1-6 — ящики, двери, костры, замки, растения, бачки с водой
        private sealed class Job
        {
            public string Scene;
            public Vector3 Around;
            public bool Outdoors;
            public int Step;
            public List<Entry> Items = new();
            public readonly List<Obj> Objs = new();
            // Хост: отчёт друга.
            public Peer Peer;
            public string Name;
            public float Tod;
            public int Weather;
            public List<Entry> Theirs;
            public List<Obj> TheirObjs;
            // Сверка без хоста (меня попросил хост): чей отчёт и только ли что он пришёл.
            public int Who = -1;
            public bool Fresh;
            public bool TheirCalm;   // у проверяемого все его события дошли и подтверждены, обрыва нет (0.19.23)
        }
        private static Job _relayJob;                         // друг: сверяю чужой отчёт за хоста
        private static Job _job;                              // друг: свой отчёт
        private static Job _hostJob;                          // хост: отчёт друга, который сейчас сравниваем
        private static readonly List<Job> HostJobs = new();   // хост: отчёты в очереди

        private static void StepJob(Job j)
        {
            if (j.Step == 0) j.Items = LooseItems();
            else AddObjects(j.Step, j.Around, j.Outdoors, j.Objs);
            j.Step++;
        }

        // Друг: каждый кадр. Хост: досравнивает отчёты друзей.
        public static void Tick(float now)
        {
            if (Session.Mode == Mode.Host) { TickHost(); return; }
            if (Session.Mode != Mode.Client || !Session.InHostWorld || !GameApi.InGame() || !ItemSync.Settled) { _job = null; _relayJob = null; return; }
            if (_relayJob != null)
            {
                var rj = _relayJob;
                try
                {
                    if (rj.Scene != ItemSync.SceneKey()) { _relayJob = null; return; }
                    if (rj.Step < Steps) { StepJob(rj); return; }
                    _relayJob = null;
                    RelayCompare(rj);
                }
                catch (Exception e) { _relayJob = null; MelonLogger.Warning("[проверка] сверка без хоста: " + e.Message); }
                return;
            }
            try
            {
                if (_job == null)
                {
                    if (now < _next) return;
                    _next = now + Interval;
                    _job = new Job { Scene = ItemSync.SceneKey(), Around = GameManager.GetPlayerTransform().position, Outdoors = Outdoors() };
                }
                var j = _job;
                if (j.Scene != ItemSync.SceneKey()) { _job = null; return; }
                if (j.Step < Steps) { StepJob(j); return; }
                _job = null;
                var items = j.Items;
                var w = Msg.New(MsgType.Check);
                w.Write(j.Scene);
                w.Write(GameApi.TimeOfDay());
                w.Write(GameApi.WeatherStage());
                var me = j.Around;
                w.Write(me.x); w.Write(me.y); w.Write(me.z);
                w.Write(items.Count);
                foreach (var e in items)
                {
                    w.Write(e.Prefab);
                    w.Write(e.Pos.x); w.Write(e.Pos.y); w.Write(e.Pos.z);
                    w.Write(e.Radial);
                }
                // Сводка объектов вокруг: ящики, двери, костры, замки, растения.
                var objs = j.Objs;
                w.Write(objs.Count);
                foreach (var o in objs) { w.Write((byte)o.Kind); w.Write(o.Id); w.Write(o.Value); }
                // Состояния вещей (котелки, факелы, лампы) в том же порядке, что вещи выше (0.19.23).
                w.Write(items.Count);
                foreach (var e in items) w.Write(e.State ?? "");
                w.Write(MyCalm());
                Session.SendGameEvent(Msg.Done(w));
            }
            catch (Exception e)
            {
                _job = null;
                MelonLogger.Warning("[проверка] " + e.Message);
            }
        }

        private static void TickHost()
        {
            if (_hostJob == null)
            {
                if (HostJobs.Count == 0) return;
                _hostJob = HostJobs[0];
                HostJobs.RemoveAt(0);
            }
            var j = _hostJob;
            try
            {
                if (j.Peer.Closed || !GameApi.InGame() || !ItemSync.Settled || j.Scene != ItemSync.SceneKey()) { _hostJob = null; return; }
                if (j.Step < Steps) { StepJob(j); return; }
                _hostJob = null;
                // Хост сам событий не ждёт (он и есть правда); всё зависит от того, дошли ли до него события друга.
                _calm = j.TheirCalm;
                try { CompareObjects(j.Peer, j.Name, j.TheirObjs, j.Objs, j.Tod); } catch (Exception e) { MelonLogger.Warning("[проверка] объекты: " + e.Message); }
                CompareItems(j);
            }
            catch (Exception e)
            {
                _hostJob = null;
                MelonLogger.Warning("[проверка] " + e.Message);
            }
        }

        // Хост: пришёл отчёт друга.
        public static void Receive(Peer peer, string playerName, BinaryReader r)
        {
            int playerId = peer.Id;
            string scene = r.ReadString();
            float tod = r.ReadSingle();
            int weather = r.ReadInt32();
            var theirPos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            int n = r.ReadInt32();
            var theirs = new List<Entry>(n);
            for (int i = 0; i < n; i++)
                theirs.Add(new Entry { Prefab = r.ReadString(), Pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), Radial = r.ReadBoolean() });

            var theirObjs = new List<Obj>();
            if (r.BaseStream.Position < r.BaseStream.Length)
            {
                int no = r.ReadInt32();
                for (int i = 0; i < no; i++) theirObjs.Add(new Obj { Kind = (WorldObjects.Kind)r.ReadByte(), Id = r.ReadString(), Value = r.ReadInt32() });
            }
            if (r.BaseStream.Position < r.BaseStream.Length)
            {
                int ns = r.ReadInt32();
                for (int i = 0; i < ns; i++)
                {
                    string st = r.ReadString();
                    if (ns == n) { var e = theirs[i]; e.State = st; theirs[i] = e; }
                }
            }
            bool calm = r.BaseStream.Position < r.BaseStream.Length && r.ReadBoolean();

            // Когда игрок впервые прислал отчёт из этой локации — по этому видно, кто в ней раньше.
            if (!FirstSeen.TryGetValue(playerId, out var fs) || fs.scene != scene) FirstSeen[playerId] = (scene, Time.unscaledTime);
            if (!GameApi.InGame() || !ItemSync.Settled || scene != ItemSync.SceneKey())
            {
                RelayToFirst(peer, playerName, scene, theirPos, theirs, theirObjs, tod, calm);
                return;
            }
            // Своё собираем по кадрам в TickHost; новый отчёт того же друга заменяет старый в очереди.
            HostJobs.RemoveAll(q => q.Peer == peer);
            if (_hostJob != null && _hostJob.Peer == peer) _hostJob = null;
            HostJobs.Add(new Job { Scene = scene, Around = theirPos, Outdoors = Outdoors(), Peer = peer, Name = playerName, Tod = tod, Weather = weather, Theirs = theirs, TheirObjs = theirObjs, TheirCalm = calm });
        }

        private static void CompareItems(Job j)
        {
            var peer = j.Peer;
            int playerId = peer.Id;
            string playerName = j.Name, scene = j.Scene;
            float tod = j.Tod;
            int weather = j.Weather;
            var theirPos = j.Around;
            var theirs = j.Theirs;
            var mine = j.Items;
            // Когда игрок пришёл в эту локацию (по первой проверке в ней).
            float enteredAt;
            if (SceneSince.TryGetValue(playerId, out var ss0) && ss0.scene == scene) enteredAt = ss0.since;
            else { enteredAt = Time.unscaledTime; SceneSince[playerId] = (scene, enteredAt); }
            // На большой карте сравниваем вещи вокруг друга (40 м), где бы он ни был. Палки и перья
            // игра выкладывает только рядом с игроком, поэтому их сравниваем лишь там, где рядом и хост.
            bool outdoors = false;
            try { outdoors = GameManager.IsOutDoorsScene(GameManager.m_ActiveScene); } catch { }
            if (outdoors)
            {
                var myPos = GameManager.GetPlayerTransform().position;
                bool Keep(Entry e) => Vector3.Distance(e.Pos, theirPos) <= 40f && (!e.Radial || Vector3.Distance(e.Pos, myPos) <= 40f);
                mine = mine.Where(Keep).ToList();
                theirs = theirs.Where(Keep).ToList();
            }
            var onlyMine = new List<Entry>();
            var left = new List<Entry>(theirs);
            var stateDiff = new List<Entry>();
            Match(mine, left, onlyMine, stateDiff);
            try
            {
                FixStates(peer.Id, playerName, stateDiff, LastStateDiff, ask =>
                {
                    var w = Msg.New(MsgType.CheckResend);
                    WriteResend(w, ask);
                    peer.Send(Msg.Done(w));
                });
            } catch (Exception e) { MelonLogger.Warning("[проверка] состояния вещей: " + e.Message); }

            float dt = Math.Abs(GameApi.TimeOfDay() - tod);
            dt = Math.Min(dt, 1f - dt) * 24f * 60f;   // минуты
            int myWeather = GameApi.WeatherStage();

            var problems = new List<string>();
            if (onlyMine.Count > 0) problems.Add($"у {playerName} нет {onlyMine.Count}: " + Describe(onlyMine));
            if (left.Count > 0) problems.Add($"у хоста нет {left.Count}: " + Describe(left));
            if (dt > 5f) problems.Add($"время отличается на {dt:0} мин");
            if (myWeather != weather) problems.Add($"погода {myWeather} / {weather}");

            string where = scene;
            if (outdoors)
            {
                float apart = Vector3.Distance(GameManager.GetPlayerTransform().position, theirPos);
                if (apart > 60f) where += UI.L.T($", вы в {apart:0} м друг от друга", $", {apart:0} m apart");
            }
            if (problems.Count == 0)
            {
                LastProblem.Remove(playerId);
                LastShort = UI.L.T("проверка ок ", "check ok ") + $"{DateTime.Now:HH:mm}";
                LastResult = $"{DateTime.Now:HH:mm:ss} {playerName}: " + UI.L.T($"совпадает ({mine.Count} предметов на земле, {where})", $"matches ({mine.Count} items on the ground, {where})");
                MelonLogger.Msg("[проверка] " + LastResult);
                return;
            }
            string text = string.Join("; ", problems);
            MelonLogger.Msg($"[проверка] {playerName}, {where}: возможно расхождение: {text}");
            // Игрок только что пришёл в локацию: вещи не «в полёте», ждать второй проверки незачем — чиним сразу и молча,
            // чтобы лишнее не успели подобрать, а недостающее не появилось через 40 с перед лицом.
            {
                float now0 = Time.unscaledTime;
                // 0.19.23: и позже, если вещи ни у кого не менялись последние 5 с — тогда это не «вещь в полёте».
                bool justCame = now0 - enteredAt < 60f;
                if ((justCame || ItemsQuiet) && (onlyMine.Count > 0 || left.Count > 0) && FixDue(playerId, now0, justCame ? 15f : 120f, onlyMine, left))
                {
                    NoteFix(playerId, now0, onlyMine, left);
                    SceneSnapshot.SendFix(peer, scene, theirPos);
                    MelonLogger.Msg(justCame ? $"[проверка] {playerName} только что пришёл в {scene}: сразу отправил правильную раскладку"
                                             : $"[проверка] {playerName}, {scene}: вещи 5 с не менялись — сразу отправил правильную раскладку");
                    LastProblem.Remove(playerId);
                    return;
                }
            }
            if (LastProblem.TryGetValue(playerId, out var prev) && prev == text)
            {
                LastShort = UI.L.T("расхождение ", "mismatch ") + $"{DateTime.Now:HH:mm}";
                LastResult = $"{DateTime.Now:HH:mm:ss} {playerName}: " + UI.L.T("РАСХОЖДЕНИЕ", "MISMATCH") + $" ({where}): {text}";
                Session.Log("Проверка: " + LastResult);
                GameApi.HudMessage("Кооп: расхождение с " + playerName);
                float now = Time.unscaledTime;
                if ((onlyMine.Count > 0 || left.Count > 0) && FixDue(playerId, now, 120f, onlyMine, left))
                {
                    NoteFix(playerId, now, onlyMine, left);
                    SceneSnapshot.SendFix(peer, scene, theirPos);
                    Session.Log($"Проверка: отправил {playerName} правильную раскладку вещей вокруг него");
                }
            }
            LastProblem[playerId] = text;
        }

        // ---------- Локация без хоста ----------
        // Хоста в локации нет — сверяет тот, кто вошёл в неё раньше: хост пересылает ему отчёт, он сравнивает со своими
        // вещами и при расхождении (сразу в первую минуту после входа проверяемого, иначе со второго раза) отправляет
        // через хоста свою раскладку вокруг проверяемого. Время, погоду и объекты здесь не сверяем — их ведёт хост.

        private static readonly Dictionary<int, (string scene, float at)> FirstSeen = new();
        private static readonly Dictionary<int, string> RelayProblem = new();
        private static readonly Dictionary<int, float> RelayFix = new();
        private static int _relayed, _relayFixes;

        private static void RelayToFirst(Peer peer, string playerName, string scene, Vector3 theirPos, List<Entry> theirs, List<Obj> theirObjs, float tod, bool calm)
        {
            if (!FirstSeen.TryGetValue(peer.Id, out var me)) return;
            float now = Time.unscaledTime;
            int best = -1;
            float bestAt = me.at;
            foreach (var kv in FirstSeen)
            {
                if (kv.Key == peer.Id || kv.Value.scene != scene || kv.Value.at >= bestAt) continue;
                if (!Session.Players.TryGetValue(kv.Key, out var pl) || now - pl.LastState > 5f || pl.SceneKey != scene) continue;
                best = kv.Key; bestAt = kv.Value.at;
            }
            if (best < 0) return;
            var target = Session.PeerById(best);
            if (target == null) return;
            var w = Msg.New(MsgType.CheckRelay);
            w.Write((byte)peer.Id);
            w.Write(playerName);
            w.Write(scene);
            w.Write(now - me.at < 60f);   // только что пришёл
            w.Write(theirPos.x); w.Write(theirPos.y); w.Write(theirPos.z);
            w.Write(theirs.Count);
            foreach (var e in theirs) { w.Write(e.Prefab); w.Write(e.Pos.x); w.Write(e.Pos.y); w.Write(e.Pos.z); w.Write(e.Radial); }
            // 0.19.23: и объекты (костры, бачки, двери…), и состояния вещей — сверять их без хоста тоже.
            w.Write(tod);
            w.Write(theirObjs.Count);
            foreach (var o in theirObjs) { w.Write((byte)o.Kind); w.Write(o.Id); w.Write(o.Value); }
            w.Write(theirs.Count);
            foreach (var e in theirs) w.Write(e.State ?? "");
            w.Write(calm);
            target.Send(Msg.Done(w));
            _relayed++;
        }

        // Друг (вошедший раньше): сверить отчёт другого игрока со своими вещами.
        public static void ClientRelay(BinaryReader r)
        {
            int who = r.ReadByte();
            string name = r.ReadString();
            string scene = r.ReadString();
            bool fresh = r.ReadBoolean();
            var theirPos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            int n = r.ReadInt32();
            var theirs = new List<Entry>(n);
            for (int i = 0; i < n; i++) theirs.Add(new Entry { Prefab = r.ReadString(), Pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), Radial = r.ReadBoolean() });
            float tod = -1f;
            var theirObjs = new List<Obj>();
            if (r.BaseStream.Position < r.BaseStream.Length)
            {
                tod = r.ReadSingle();
                int no = r.ReadInt32();
                for (int i = 0; i < no; i++) theirObjs.Add(new Obj { Kind = (WorldObjects.Kind)r.ReadByte(), Id = r.ReadString(), Value = r.ReadInt32() });
                int ns = r.ReadInt32();
                for (int i = 0; i < ns; i++) { string st = r.ReadString(); if (ns == n) { var e = theirs[i]; e.State = st; theirs[i] = e; } }
            }
            bool calm = r.BaseStream.Position < r.BaseStream.Length && r.ReadBoolean();
            if (Session.Mode != Mode.Client || !Session.InHostWorld || !GameApi.InGame() || !ItemSync.Settled || scene != ItemSync.SceneKey()) return;
            // Своё собираем по кадрам, как хост (за один кадр — рывок).
            _relayJob = new Job { Scene = scene, Around = theirPos, Outdoors = Outdoors(), Who = who, Name = name, Fresh = fresh, Tod = tod, Theirs = theirs, TheirObjs = theirObjs, TheirCalm = calm };
        }

        // Друг (вошедший раньше): сверить отчёт другого игрока со своим — вещи на земле, объекты, состояния вещей.
        private static void RelayCompare(Job j)
        {
            // Сверяю я, а не хост: и у меня не должно быть недошедших событий.
            _calm = j.TheirCalm && MyCalm();
            int who = j.Who;
            string name = j.Name, scene = j.Scene;
            bool fresh = j.Fresh;
            var theirPos = j.Around;
            var theirs = j.Theirs;
            var mine = j.Items;
            if (j.Outdoors)
            {
                var myPos = GameManager.GetPlayerTransform().position;
                bool Keep(Entry e) => Vector3.Distance(e.Pos, theirPos) <= 40f && (!e.Radial || Vector3.Distance(e.Pos, myPos) <= 40f);
                mine = mine.Where(Keep).ToList();
                theirs = theirs.Where(Keep).ToList();
            }
            var onlyMine = new List<Entry>();
            var left = new List<Entry>(theirs);
            var stateDiff = new List<Entry>();
            Match(mine, left, onlyMine, stateDiff);
            // Объекты и состояния вещей: правильное у меня (я в локации раньше) — рассылаю всем через хоста.
            if (j.Tod >= 0f)
            {
                try
                {
                    Session.Players.TryGetValue(who, out var them);
                    var fix = DiffObjects(j.TheirObjs, j.Objs, j.Tod, them?.Busy, RelayObjDiff, who);
                    int sentObj = 0;
                    foreach (var m in fix)
                    {
                        string st = FixState(m);
                        if (st == null) continue;
                        WorldObjects.Send(m.Kind, m.C, st);
                        sentObj++;
                    }
                    if (sentObj > 0)
                    {
                        _objFixed += sentObj;
                        MelonLogger.Msg($"[проверка] {name} (хоста здесь нет, сверяю я): объекты разошлись, разослал своё состояние ({sentObj}): " + string.Join(", ", fix.Take(6).Select(o => o.Kind + " " + o.Id)));
                    }
                }
                catch (Exception e) { MelonLogger.Warning("[проверка] объекты без хоста: " + e.Message); }
                try
                {
                    FixStates(who, name, stateDiff, RelayStateDiff, ask =>
                    {
                        var w = Msg.New(MsgType.CheckResend);
                        w.Write((byte)who);
                        WriteResend(w, ask);
                        Session.SendToHost(Msg.Done(w));
                    });
                }
                catch (Exception e) { MelonLogger.Warning("[проверка] состояния вещей без хоста: " + e.Message); }
            }
            if (onlyMine.Count == 0 && left.Count == 0)
            {
                RelayProblem.Remove(who);
                MelonLogger.Msg($"[проверка] {name} (хоста здесь нет, сверяю я): совпадает ({mine.Count} предметов на земле, {scene})");
                return;
            }
            var parts = new List<string>();
            if (onlyMine.Count > 0) parts.Add($"у {name} нет {onlyMine.Count}: " + Describe(onlyMine));
            if (left.Count > 0) parts.Add($"у меня нет {left.Count}: " + Describe(left));
            string text = string.Join("; ", parts);
            MelonLogger.Msg($"[проверка] {name} (хоста здесь нет, сверяю я), {scene}: возможно расхождение: {text}");
            bool repeated = RelayProblem.TryGetValue(who, out var prev) && prev == text;
            RelayProblem[who] = text;
            float now = Time.unscaledTime;
            // Вещи ни у кого не менялись последние 5 с — расхождение не «в полёте», чиним сразу (0.19.23).
            bool quiet = ItemsQuiet;
            if (!(fresh || repeated || quiet) || RelayFix.TryGetValue(who, out float lf) && now - lf < (fresh ? 15f : 120f)) return;
            RelayFix[who] = now;
            RelayProblem.Remove(who);
            var packed = SceneSnapshot.BuildAround(theirPos);
            var fw = Msg.New(MsgType.CheckFix);
            fw.Write((byte)who);
            fw.Write(scene);
            fw.Write(packed.Length);
            fw.Write(packed);
            Session.SendToHost(Msg.Done(fw));
            _relayFixes++;
            MelonLogger.Msg($"[проверка] {name}: отправил свою раскладку вокруг него ({(fresh ? "только что пришёл" : repeated ? "расхождение повторилось" : "вещи 5 с не менялись")})");
            if (!fresh) GameApi.HudMessage("Кооп: расхождение с " + name + ", поправил");
        }

        private static readonly Dictionary<int, HashSet<string>> RelayObjDiff = new();
        private static readonly Dictionary<int, HashSet<string>> RelayStateDiff = new();

        // Сопоставить вещи: одинаковые в пределах 1 м уходят из left, мои без пары — в onlyMine,
        // пары с разным состоянием (котелок, факел, лампа) — в stateDiff.
        private static void Match(List<Entry> mine, List<Entry> left, List<Entry> onlyMine, List<Entry> stateDiff)
        {
            foreach (var m in mine)
            {
                int best = -1;
                float bestD = Tolerance;
                for (int i = 0; i < left.Count; i++)
                {
                    if (left[i].Prefab != m.Prefab) continue;
                    float d = Vector3.Distance(left[i].Pos, m.Pos);
                    if (d <= bestD) { bestD = d; best = i; }
                }
                if (best >= 0)
                {
                    var t = left[best];
                    if (!string.IsNullOrEmpty(m.State) && !string.IsNullOrEmpty(t.State) && m.State != t.State) stateDiff.Add(m);
                    left.RemoveAt(best);
                }
                else onlyMine.Add(m);
            }
        }

        // Хост: переслать «пришли заново» от сверявшего тому, кто эти вещи менял последним.
        public static void HostForwardResend(BinaryReader r)
        {
            int who = r.ReadByte();
            int n = r.ReadInt32();
            var list = new List<Entry>(n);
            for (int i = 0; i < n; i++) list.Add(new Entry { Prefab = r.ReadString(), Pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()) });
            if (who == Session.LocalId) { ResendMine(list); return; }
            var target = Session.PeerById(who);
            if (target == null) return;
            var w = Msg.New(MsgType.CheckResend);
            WriteResend(w, list);
            target.Send(Msg.Done(w));
        }

        private static void WriteResend(BinaryWriter w, List<Entry> list)
        {
            w.Write(list.Count);
            foreach (var e in list) { w.Write(e.Prefab); w.Write(e.Pos.x); w.Write(e.Pos.y); w.Write(e.Pos.z); }
        }

        // Хост: переслать раскладку от вошедшего раньше тому, кого он сверял.
        public static void HostForwardFix(Peer from, BinaryReader r)
        {
            int who = r.ReadByte();
            string scene = r.ReadString();
            var packed = r.ReadBytes(r.ReadInt32());
            var target = Session.PeerById(who);
            if (target == null) return;
            target.Send(SceneSnapshot.FixMessage(scene, packed));
            MelonLogger.Msg($"[проверка] {scene}: переслал раскладку от игрока {from.Id} игроку {who} (меня там нет)");
        }

        // ---------- Сводка объектов ----------
        // Раз в 20 секунд вместе с вещами на земле друг присылает короткую сводку объектов вокруг себя.
        // Разошлось две проверки подряд — хост присылает ему своё состояние именно этих объектов.
        // Так тихие ошибки чинятся сами, даже те, которые мы ещё не нашли.

        private struct Obj { public WorldObjects.Kind Kind; public string Id; public int Value; public Component C; }

        private static bool Outdoors()
        {
            try { return GameManager.IsOutDoorsScene(GameManager.m_ActiveScene); } catch { return false; }
        }

        // Один вид объектов за вызов (step 1-5), чтобы поиск по сцене шёл по кадрам.
        private static void AddObjects(int step, Vector3 around, bool outdoors, List<Obj> list)
        {
            bool Near(Component c) => !outdoors || (c.transform.position - around).sqrMagnitude < 40f * 40f;
            if (step == 1)
            {
                foreach (var c in UnityEngine.Object.FindObjectsOfType<Container>())
                {
                    if (c == null || !Near(c)) continue;
                    int v = -1;
                    try
                    {
                        // Неразложенный ящик (никто не открывал) не сравниваем: его разложат при обыске.
                        if (!c.m_NotPopulated)
                        {
                            var items = new Il2CppSystem.Collections.Generic.List<GearItem>();
                            c.GetItems(items);
                            // Личные вещи (квестовые, записки-коллекции) у каждого свои — их не считаем, как и на земле.
                            int cnt = 0, h = 0;
                            foreach (var gi in items) if (gi != null && !ItemSync.IsPersonal(gi)) { cnt++; h += Fnv(ItemSync.PrefabName(gi)) & 0xffff; }
                            // Вещи, которые у меня не создались (нет DLC или мода): они в ящике у остальных, считаем и их.
                            foreach (var n in WorldObjects.UncreatedPrefabs(ItemSync.SceneKey(), WorldObjects.IdOf(c))) { cnt++; h += Fnv(n) & 0xffff; }
                            h += cnt * 1000003;
                            v = h & 0x7fffffff;
                        }
                    }
                    catch { }
                    list.Add(new Obj { Kind = WorldObjects.Kind.Container, Id = WorldObjects.IdOf(c), Value = v, C = c });
                }
            }
            else if (step == 2)
            {
                foreach (var c in UnityEngine.Object.FindObjectsOfType<OpenClose>())
                    if (c != null && Near(c)) { int v = -1; try { v = c.IsOpen() ? 1 : 0; } catch { } list.Add(new Obj { Kind = WorldObjects.Kind.Door, Id = WorldObjects.IdOf(c), Value = v, C = c }); }
            }
            else if (step == 3)
            {
                foreach (var c in UnityEngine.Object.FindObjectsOfType<Fire>())
                {
                    // Горит — 1 + сколько ещё гореть в десятых минуты (0.19.23): раньше сверяли только «горит / нет»,
                    // и подброшенные у одного дрова, не дошедшие до другого, не замечались.
                    if (c == null || !Near(c)) continue;
                    int v = -1;
                    try { v = c.IsBurning() ? 1 + Mathf.Clamp(Mathf.RoundToInt(Patch_ClaimTorch.MinutesLeft(c) * 10f), 0, 100000000) : 0; } catch { }
                    list.Add(new Obj { Kind = WorldObjects.Kind.Fire, Id = WorldObjects.IdOf(c), Value = v, C = c });
                }
            }
            else if (step == 6)
            {
                // Бачки с водой (0.19.23): сколько литров, в десятых.
                foreach (var c in UnityEngine.Object.FindObjectsOfType<WaterSource>())
                    if (c != null && Near(c)) list.Add(new Obj { Kind = WorldObjects.Kind.Water, Id = WorldObjects.IdOf(c), Value = WorldObjects.ProbeOf(WorldObjects.Kind.Water, c), C = c });
            }
            else if (step == 4)
            {
                foreach (var c in UnityEngine.Object.FindObjectsOfType<Lock>())
                    if (c != null && Near(c)) { int v = -1; try { v = c.m_LockStateRolled ? (int)c.m_LockState : -1; } catch { } list.Add(new Obj { Kind = WorldObjects.Kind.Lock, Id = WorldObjects.IdOf(c), Value = v, C = c }); }
            }
            else if (step == 5)
            {
                // Сорванное растение игра выключает: ищем и выключенные, иначе расхождение в растениях не видно.
                foreach (var c in UnityEngine.Object.FindObjectsOfType<Harvestable>(true))
                    if (c != null && Near(c)) { int v = -1; try { v = c.IsHarvested() ? 1 : 0; } catch { } list.Add(new Obj { Kind = WorldObjects.Kind.Harvest, Id = WorldObjects.IdOf(c), Value = v, C = c }); }
            }
        }

        private static readonly Dictionary<int, HashSet<string>> LastObjDiff = new();
        private static int _objFixed;

        private static void CompareObjects(Peer peer, string playerName, List<Obj> theirs, List<Obj> myObjs, float theirTod)
        {
            Session.Players.TryGetValue(peer.Id, out var them);
            var fix = DiffObjects(theirs, myObjs, theirTod, them?.Busy, LastObjDiff, peer.Id);
            if (fix.Count == 0) return;
            int sent = 0;
            foreach (var m in fix)
            {
                string state = FixState(m);
                if (state == null) continue;
                peer.Send(WorldObjects.Build(m.Kind, m.C, state));
                _objFixed++;
                sent++;
            }
            if (sent == 0) return;
            string what = string.Join(", ", fix.Take(6).Select(o => o.Kind + " " + o.Id)) + (fix.Count > 6 ? " …" : "");
            MelonLogger.Msg($"[проверка] {playerName}: объекты разошлись, отправил своё состояние ({sent}): {what}");
            Session.Log($"Проверка: поправил у {playerName} объектов: {sent}");
        }

        // Какие объекты чинить. Сразу — если объект последние 5 с никто не менял (значит, расхождение не «в полёте»),
        // иначе — когда расхождение повторилось и в следующей проверке.
        private static List<Obj> DiffObjects(List<Obj> theirs, List<Obj> myObjs, float theirTod, string theirBusy, Dictionary<int, HashSet<string>> last, int who)
        {
            var fix = new List<Obj>();
            if (theirs == null || theirs.Count == 0) return fix;
            var mine = new Dictionary<string, Obj>();
            foreach (var o in myObjs) mine[(int)o.Kind + o.Id] = o;
            var diff = new HashSet<string>();
            last.TryGetValue(who, out var prev);
            // Минуты костра сверяем до 1 минуты: таймеры костров и так выравнивает хозяин локации раз в минуту.
            // Только если часы у нас идут вместе: при ускоренном времени (сон, долгое действие) или разных часах
            // за доли секунды, пока шёл отчёт, костёр «сгорает» на минуты — тогда сверяем лишь «горит / нет».
            float clock = Math.Abs(GameApi.TimeOfDay() - theirTod);
            clock = Math.Min(clock, 1f - clock) * 24f * 60f;
            bool minutes = clock <= 2f && !SleepSync.HoldClock && !ActionTime.Active;
            string myBusy = Busy.Current();
            foreach (var t in theirs)
            {
                if (t.Value < 0 || !mine.TryGetValue((int)t.Kind + t.Id, out var m) || m.Value < 0 || m.Value == t.Value) continue;
                if (t.Kind == WorldObjects.Kind.Fire)
                {
                    // Оба горят и времени почти поровну — совпадает. Костёр, у которого кто-то стоит с окном, не трогаем.
                    if (t.Value > 0 && m.Value > 0 && (!minutes || Math.Abs(t.Value - m.Value) <= 10)) continue;
                    if (myBusy == "fire:" + t.Id || theirBusy == "fire:" + t.Id) continue;
                }
                // Из бачка сейчас набирают — его количество вот-вот поменяется.
                if (t.Kind == WorldObjects.Kind.Water && (myBusy == "water:" + t.Id || theirBusy == "water:" + t.Id)) continue;
                // Открытый у кого-то ящик меняется прямо сейчас — не трогаем.
                if (t.Kind == WorldObjects.Kind.Container && (myBusy == "box:" + t.Id || theirBusy == "box:" + t.Id)) continue;
                // У хоста цело, у друга сорвано: вернуть растение можно только «отросло» (PlantRegrow); не растение — не вернуть, не трогаем.
                if (t.Kind == WorldObjects.Kind.Harvest && m.Value == 0 && !Regrowable(m.C)) continue;
                // У хоста сорвано, но срок отрастания игры уже прошёл: Deserialize у друга всё равно вернёт растение — не чиним по кругу.
                if (t.Kind == WorldObjects.Kind.Harvest && m.Value == 1 && RefreshDue(m.C)) continue;
                string key = (int)t.Kind + t.Id;
                if ((prev != null && prev.Contains(key)) || ObjQuiet(t.Kind, t.Id)) { if (m.C != null) fix.Add(m); }
                else diff.Add(key);
            }
            last[who] = diff;
            return fix;
        }

        private static string FixState(Obj m)
        {
            if (m.C == null) return null;   // пропал, пока собирали сводку по кадрам
            return m.Kind switch
            {
                WorldObjects.Kind.Container => ((Container)m.C).Serialize(),
                WorldObjects.Kind.Door => m.Value == 1 ? "1" : "0",
                WorldObjects.Kind.Fire => WorldObjects.FireState((Fire)m.C),
                WorldObjects.Kind.Lock => "S" + ((Lock)m.C).Serialize(),
                // Обычный Deserialize сорванное не возвращает — у меня цело, значит шлём «отросло».
                WorldObjects.Kind.Harvest => (m.Value == 0 ? PlantRegrow.Mark : "") + ((Harvestable)m.C).Serialize(),
                WorldObjects.Kind.Water => ((WaterSource)m.C).Serialize(),
                _ => null,
            };
        }

        // ---------- Что менялось недавно (0.19.23) ----------
        // События мира идут по порядку вместе с отчётом проверки: изменение у друга доходит до хоста раньше его отчёта,
        // а своё изменение хост помечает при отправке. Объект или вещи, которые 5 с никто не трогал, — не «в полёте».
        private static readonly Dictionary<string, float> ObjTouched = new();
        private static float _itemEventAt = -100f;

        internal static void TouchObj(WorldObjects.Kind kind, string id)
        {
            if (ObjTouched.Count > 5000) ObjTouched.Clear();
            float now = Time.unscaledTime;
            ObjTouched[(int)kind + id] = now;
            if (kind == WorldObjects.Kind.FireClock) ObjTouched[(int)WorldObjects.Kind.Fire + id] = now;
        }

        internal static void ItemEvent() => _itemEventAt = Time.unscaledTime;

        // Session.SendGameEvent: что я сам отправил.
        internal static void NoteSent(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            var t = (MsgType)data[0];
            if (t == MsgType.ItemDrop || t == MsgType.ItemPick || t == MsgType.ItemUpdate || t == MsgType.SceneState) { ItemEvent(); return; }
            if (t != MsgType.ObjectState) return;
            try
            {
                using var r = Msg.Read(data, out _);
                r.ReadString();
                var kind = (WorldObjects.Kind)r.ReadByte();
                TouchObj(kind, r.ReadString());
            }
            catch { }
        }

        private static bool ObjQuiet(WorldObjects.Kind kind, string id) =>
            _calm && (!ObjTouched.TryGetValue((int)kind + id, out float at) || Time.unscaledTime - at > 5f);

        private static bool ItemsQuiet => _calm && Time.unscaledTime - _itemEventAt > 5f;

        // «Спокойно» — все мои события мира хост получил и подтвердил, я не жду пропущенных событий от хоста,
        // связь не рвалась (или после обрыва уже всё дослано). Иначе после переподключения события идут пачкой,
        // и правило 5 секунд могло бы откатить свежее изменение — тогда чиним только со второй проверки.
        private static bool _calm;

        internal static bool MyCalm()
        {
            if (Session.Mode == Mode.Host) return true;
            try { return !Session.Reconnecting && Session.OutboxEmpty && ClientLog.Unacked == 0 && !EventLog.ClientHasGap; }
            catch { return false; }
        }

        public static int ObjectsFixed => _objFixed;

        private static bool Regrowable(Component c)
        {
            try { return c != null && ((Harvestable)c).RegisterAsPlantsHaversted; } catch { return false; }
        }

        private static bool RefreshDue(Component c)
        {
            try { var h = (Harvestable)c; return h.m_RefreshTime > 0f && GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused() >= h.m_RefreshTime; } catch { return false; }
        }

        // ---------- Состояния вещей (0.19.23) ----------
        // Котелок, факел или лампа на земле у хоста и у друга разные две проверки подряд — правильным считаем того,
        // кто последним эту вещь менял: менял друг — просим его прислать вещь заново, иначе её присылает хост.
        private static readonly Dictionary<int, HashSet<string>> LastStateDiff = new();
        private static int _statesFixed;
        public static int StatesFixed => _statesFixed;

        private static string StateKey(Entry e) => e.Prefab + "|" + Mathf.RoundToInt(e.Pos.x) + "|" + Mathf.RoundToInt(e.Pos.y) + "|" + Mathf.RoundToInt(e.Pos.z);

        private static void FixStates(int who, string playerName, List<Entry> diff, Dictionary<int, HashSet<string>> last, Action<List<Entry>> ask)
        {
            last.TryGetValue(who, out var prev);
            var keep = new HashSet<string>();
            var askList = new List<Entry>();
            var names = new List<string>();
            int sent = 0;
            bool quiet = ItemsQuiet;
            foreach (var m in diff)
            {
                string key = StateKey(m);
                // Сразу, если вещи 5 с никто не трогал; иначе — когда расхождение повторится.
                if (m.Gi == null || !(quiet || (prev != null && prev.Contains(key)))) { keep.Add(key); continue; }
                string mySig = WorldExtra.PotSig(m.Gi);
                names.Add(mySig.Length > 0 ? $"{m.Prefab} (у меня {mySig})" : m.Prefab);
                if (ItemSync.OwnerOf(m.Gi) == who) { askList.Add(m); continue; }
                if (WorldExtra.PotInUseByMe(m.Gi, out _)) continue;
                ItemSync.Resend(m.Gi, m.Gi.transform.position);
                sent++;
            }
            last[who] = keep;
            if (askList.Count > 0) ask(askList);
            if (sent + askList.Count == 0) return;
            _statesFixed += sent + askList.Count;
            MelonLogger.Msg($"[проверка] {playerName}: состояние вещей разошлось (котелок/факел/лампа/фальшфейер/фонарик): отправил своё {sent}, попросил у него {askList.Count}: {string.Join(", ", names.Take(5))}");
            Session.Log($"Проверка: поправил у {playerName} вещей: {sent + askList.Count}");
        }

        // Друг: хост просит прислать заново вещи, которые последним менял я.
        public static void ClientResend(BinaryReader r)
        {
            int n = r.ReadInt32();
            var list = new List<Entry>(n);
            for (int i = 0; i < n; i++) list.Add(new Entry { Prefab = r.ReadString(), Pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()) });
            if (Session.Mode != Mode.Client || !Session.InHostWorld || !GameApi.InGame()) return;
            ResendMine(list);
        }

        private static void ResendMine(List<Entry> list)
        {
            int done = 0;
            foreach (var e in list)
            {
                try
                {
                    var gi = ItemSync.FindNear(e.Prefab, e.Pos);
                    if (gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer || WorldExtra.PotInUseByMe(gi, out _)) continue;
                    ItemSync.Resend(gi, gi.transform.position);
                    done++;
                }
                catch { }
            }
            MelonLogger.Msg($"[проверка] попросили прислать заново {list.Count} вещей, отправил {done}");
        }

        private static string Describe(List<Entry> list) =>
            string.Join(", ", list.Take(5).Select(e => $"{e.Prefab} ({e.Pos.x:0.0} {e.Pos.y:0.0} {e.Pos.z:0.0})")) + (list.Count > 5 ? " …" : "");

        public static void Reset()
        {
            LastProblem.Clear();
            LastFix.Clear();
            LastFixKeys.Clear();
            LastObjDiff.Clear();
            LastStateDiff.Clear();
            RelayObjDiff.Clear();
            RelayStateDiff.Clear();
            ObjTouched.Clear();
            _relayJob = null;
            SceneSince.Clear();
            FirstSeen.Clear();
            RelayProblem.Clear();
            RelayFix.Clear();
            HostJobs.Clear();
            _hostJob = null;
            _job = null;
            LastResult = "";
            LastShort = "";
            _next = 0f;
        }
    }
}
