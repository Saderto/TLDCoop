using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppNodeCanvas.Framework;
using Il2CppNodeCanvas.Tasks.Actions;
using Il2CppTLD.Missions;
using MelonLoader;
using MelonLoader.Utils;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // «Задания вместе» (правило хоста QuestWait): пункт задания не закрывается у того, кто ушёл вперёд, пока его не
    // сделают остальные. Ход заданий у каждого свой (Game/PlayerProgress.cs), имена пунктов у всех одинаковые.
    //
    // Пункт закрывает действие сюжетной цепочки Action_ObjectiveCompleteMain (ActiveMissionManager.MarkObjectiveComplete +
    // EndAction). ActionTask.ExecuteAction ставит статус Running до OnExecute; если OnExecute не вызван, действие так и
    // остаётся «выполняется», а цепочка дальше не идёт: следующий пункт не появляется, и то, что игра включает для него,
    // не включается. Когда остальные дошли, вызываем настоящий OnExecute — пункт закрывается, цепочка идёт дальше.
    // Пока ждём, действию ставится _ignoreSaveCheck, иначе игра откладывала бы сохранение (GameManager.SaveShouldBePending
    // считает выполняющиеся действия заданий). После загрузки цепочка заново входит в тот же шаг и снова ждёт.
    //
    // Кого ждём: другой игрок мешает закрыть пункт X задания M, если у него в журнале есть задание M, оно не закончено,
    // пункт X у него не закрыт и он сам не стоит на X в ожидании. Нет у него задания M — не ждём (побочные задания,
    // которые он не начинал). Решает хост по отчётам игроков (MsgType QuestState → хост, QuestGate → игроку).
    public static class QuestWait
    {
        private class Report
        {
            public string Name = "";
            public readonly Dictionary<string, bool> Missions = new();                  // задание → закончено
            public readonly Dictionary<string, (string mission, bool done)> Obj = new(); // пункт → (задание, закрыт)
            public readonly HashSet<string> Wait = new();
        }

        private class Hold
        {
            public Action_ObjectiveCompleteMain Task;
            public string Mission = "";
            public string Waiting = "";   // кого ждём (от хоста)
        }

        private const string HostKey = "host";
        private static readonly Dictionary<string, Hold> Held = new();
        // Хост: последний известный журнал каждого игрока этого мира, и тех, кто сейчас не в игре
        // (progress/<мир>/quest_wait.txt): неделю ждать, выходить и заходить — ожидание не теряется.
        private static readonly Dictionary<string, Report> Known = new();
        private static readonly Dictionary<int, string> KeyById = new();
        private static readonly Dictionary<int, string> GateSent = new();
        private static readonly HashSet<string> Shown = new();   // о каких пунктах уже предупредили (один раз)
        private static string _knownFor, _savedSig = "";
        private static bool _shownLoaded, _bypass;
        private static string _sentSig = "";
        private static float _nextReport, _nextEval, _nextSave;
        internal static int Holds, Releases;

        // Хост — в своём мире (и когда ещё не открыл игру для друзей: мир тот же, ожидание не снимается).
        private static bool HostSide => (Session.Mode == Mode.Host || Session.Mode == Mode.Off) && GameApi.InGame();
        private static bool ClientSide => Session.Mode == Mode.Client && Session.InSharedWorld && GameApi.InGame();
        private static bool CanHold() =>
            HostSide ? Rules.Host.QuestWait : ClientSide && Session.CurrentRules.QuestWait;

        public static void Reset()
        {
            GateSent.Clear();
            KeyById.Clear();
            _sentSig = "";
        }

        // ---------- Удержание ----------

        internal static bool OnComplete(Action_ObjectiveCompleteMain task)
        {
            if (_bypass) return true;
            try
            {
                if (!CanHold()) return true;
                string id = task.objectiveID?.value;
                if (string.IsNullOrEmpty(id)) return true;
                var obj = FindObjective(id);
                if (obj == null || obj.completed) return true;          // не знаем пункт или он уже закрыт — как обычно
                string mission = obj.missionID ?? "";
                if (HostSide)
                {
                    EnsureKnown();
                    if (Blockers(HostKey, id, mission).Count == 0) return true;
                }
                Held[id] = new Hold { Task = task, Mission = mission };
                try { task._ignoreSaveCheck = true; } catch { }
                Holds++;
                MelonLogger.Msg($"[задания] пункт «{Describe(obj)}» ({id}, задание {mission}) сделан, жду остальных");
                _nextReport = 0f;
                _nextEval = 0f;
                return false;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[задания] удержание пункта: " + e.Message);
                return true;
            }
        }

        private static void Release(string id, string why)
        {
            if (!Held.TryGetValue(id, out var h)) return;
            Held.Remove(id);
            Releases++;
            try
            {
                var task = h.Task;
                bool running = task != null && task.status == Status.Running;
                if (running)
                {
                    try { task._ignoreSaveCheck = false; } catch { }
                    _bypass = true;
                    try { task.OnExecute(); }
                    finally { _bypass = false; }
                }
                else
                {
                    // Цепочка ушла из этого шага сама (другой переход) — пункт всё равно закрываем, чтобы он не завис.
                    bool suppress = false;
                    try { suppress = task != null && task.suppressUIPopup != null && task.suppressUIPopup.value; } catch { }
                    ActiveMissionManager.Instance?.MarkObjectiveComplete(id, suppress);
                }
                MelonLogger.Msg($"[задания] пункт {id} закрыт ({why}{(running ? "" : ", цепочка уже ушла дальше")})");
            }
            catch (Exception e) { MelonLogger.Warning($"[задания] закрыть пункт {id}: " + e.Message); }
            _nextReport = 0f;
        }

        private static void ReleaseAll(string why)
        {
            foreach (var id in Held.Keys.ToList()) Release(id, why);
        }

        // Предупреждение — один раз на пункт (и после перезахода не повторяется).
        private static void WarnOnce(string id, string who)
        {
            if (string.IsNullOrEmpty(who)) return;
            LoadShown();
            if (!Shown.Add(id)) return;
            try { File.AppendAllText(ShownFile(), id + "\n"); } catch { }
            var obj = FindObjective(id);
            GameApi.HudMessage($"Кооп: пункт «{(obj != null ? Describe(obj) : "задания")}» закроется, когда его сделает {who}");
        }

        private static string ShownFile() => Path.Combine(MelonEnvironment.UserDataDirectory, "TLDCoop", "quest_wait_shown.txt");

        private static void LoadShown()
        {
            if (_shownLoaded) return;
            _shownLoaded = true;
            try { if (File.Exists(ShownFile())) foreach (var l in File.ReadAllLines(ShownFile())) if (l.Length > 0) Shown.Add(l.Trim()); } catch { }
        }

        // ---------- Каждый кадр ----------

        public static void Tick(float now)
        {
            if (Held.Count > 0)
            {
                if (!CanHold()) { ReleaseAll("правило выключено или нет связи с хостом"); return; }
                foreach (var kv in Held.ToList())
                {
                    bool alive;
                    try { alive = kv.Value.Task != null && kv.Value.Task.status == Status.Running; } catch { alive = false; }
                    if (!alive) Release(kv.Key, "цепочка вышла из шага");
                }
            }
            if (ClientSide && now >= _nextReport)
            {
                _nextReport = now + (Held.Count > 0 ? 5f : 15f);
                var rep = Mine();
                string sig = Signature(rep);
                if (sig != _sentSig || Held.Count > 0 || now % 60f < 15f)
                {
                    _sentSig = sig;
                    Session.SendToHost(Encode(rep));
                }
            }
            if (HostSide && now >= _nextEval)
            {
                _nextEval = now + 1f;
                EnsureKnown();
                if (Rules.Host.QuestWait) Evaluate();
                if (now >= _nextSave) { _nextSave = now + 10f; SaveKnown(); }
            }
        }

        // ---------- Хост: кто кого ждёт ----------

        private static void Evaluate()
        {
            var me = Mine();
            me.Name = Session.LocalName;
            Known[HostKey] = me;

            foreach (var g in Gate(HostKey))
            {
                if (g.release) Release(g.id, "все дошли");
                else if (Held.TryGetValue(g.id, out var h)) { h.Waiting = g.who; WarnOnce(g.id, g.who); }
            }
            foreach (var kv in KeyById)
            {
                if (!Session.Players.TryGetValue(kv.Key, out var pl) || !pl.InWorld) continue;
                if (!Known.TryGetValue(kv.Value, out var rep) || rep.Wait.Count == 0) { GateSent.Remove(kv.Key); continue; }
                var gate = Gate(kv.Value);
                string sig = string.Join("|", gate.Select(g => g.id + (g.release ? "+" : "-") + g.who));
                bool anyRelease = gate.Any(g => g.release);
                if (!anyRelease && GateSent.TryGetValue(kv.Key, out var prev) && prev == sig) continue;
                GateSent[kv.Key] = sig;
                var peer = Session.PeerById(kv.Key);
                if (peer == null) continue;
                var w = Msg.New(MsgType.QuestGate);
                w.Write(gate.Count);
                foreach (var g in gate) { w.Write(g.id); w.Write(g.release); w.Write(g.who); }
                peer.Send(Msg.Done(w));
                if (anyRelease) rep.Wait.RemoveWhere(id => gate.Any(g => g.release && g.id == id));
            }
        }

        private static List<(string id, bool release, string who)> Gate(string key)
        {
            var gate = new List<(string id, bool release, string who)>();
            if (!Known.TryGetValue(key, out var rep)) return gate;
            foreach (var id in rep.Wait)
            {
                string mission = rep.Obj.TryGetValue(id, out var o) ? o.mission : "";
                var names = Blockers(key, id, mission);
                gate.Add((id, names.Count == 0, string.Join(", ", names)));
            }
            return gate;
        }

        // Кто из остальных игроков мира (и тех, кто сейчас не в игре) ещё не сделал пункт id задания mission.
        private static List<string> Blockers(string who, string id, string mission)
        {
            var names = new List<string>();
            if (!Rules.Host.QuestWait || mission.Length == 0) return names;
            foreach (var kv in Known)
            {
                if (kv.Key == who) continue;
                var r = kv.Value;
                if (!r.Missions.TryGetValue(mission, out bool missionDone) || missionDone) continue;
                if (r.Obj.TryGetValue(id, out var o) && o.done) continue;
                if (r.Wait.Contains(id)) continue;
                names.Add(r.Name.Length > 0 ? r.Name : kv.Key);
            }
            return names;
        }

        // ---------- Хост: хранение ----------

        private static string KnownFile()
        {
            string dir = PlayerProgress.WorldFolderPath();
            return dir == null ? null : Path.Combine(dir, "quest_wait.txt");
        }

        private static void EnsureKnown()
        {
            string file = KnownFile();
            if (file == null || file == _knownFor) return;
            if (_knownFor != null) SaveKnown();
            _knownFor = file;
            Known.Clear();
            _savedSig = "";
            try
            {
                if (!File.Exists(file)) return;
                foreach (var line in File.ReadAllLines(file))
                {
                    var f = line.Split('|');
                    if (f.Length < 2) continue;
                    if (!Known.TryGetValue(f[1], out var r)) Known[f[1]] = r = new Report();
                    switch (f[0])
                    {
                        case "P": if (f.Length >= 3) r.Name = f[2]; break;
                        case "M": if (f.Length >= 4) r.Missions[f[2]] = f[3] == "1"; break;
                        case "O": if (f.Length >= 5) r.Obj[f[2]] = (f[3], f[4] == "1"); break;
                        case "W": if (f.Length >= 3) r.Wait.Add(f[2]); break;
                    }
                }
                Known.Remove(HostKey);   // свой журнал хост берёт из игры
                MelonLogger.Msg($"[задания] известные журналы игроков этого мира: {string.Join(", ", Known.Values.Select(v => v.Name))}");
            }
            catch (Exception e) { MelonLogger.Warning("[задания] чтение quest_wait.txt: " + e.Message); }
        }

        private static void SaveKnown()
        {
            if (_knownFor == null) return;
            var sb = new StringBuilder();
            foreach (var kv in Known)
            {
                if (kv.Key == HostKey) continue;
                var r = kv.Value;
                sb.Append("P|").Append(kv.Key).Append('|').Append(r.Name.Replace('|', '/')).Append('\n');
                foreach (var m in r.Missions) sb.Append("M|").Append(kv.Key).Append('|').Append(m.Key).Append('|').Append(m.Value ? "1" : "0").Append('\n');
                foreach (var o in r.Obj) sb.Append("O|").Append(kv.Key).Append('|').Append(o.Key).Append('|').Append(o.Value.mission).Append('|').Append(o.Value.done ? "1" : "0").Append('\n');
                foreach (var w in r.Wait) sb.Append("W|").Append(kv.Key).Append('|').Append(w).Append('\n');
            }
            string text = sb.ToString();
            if (text == _savedSig) return;
            if (text.Length == 0 && !File.Exists(_knownFor)) { _savedSig = text; return; }
            try { Directory.CreateDirectory(Path.GetDirectoryName(_knownFor)); File.WriteAllText(_knownFor, text); _savedSig = text; }
            catch (Exception e) { MelonLogger.Warning("[задания] запись quest_wait.txt: " + e.Message); }
        }

        // ---------- Сообщения ----------

        // Хост: отчёт друга.
        public static void HostReceive(Peer peer, BinaryReader r)
        {
            string name = Session.Players.TryGetValue(peer.Id, out var pl) ? pl.Name : "игрок " + peer.Id;
            var rep = new Report { Name = name };
            int nm = r.ReadInt32();
            for (int i = 0; i < nm; i++) { string m = r.ReadString(); rep.Missions[m] = r.ReadBoolean(); }
            int no = r.ReadInt32();
            for (int i = 0; i < no; i++) { string id = r.ReadString(); string m = r.ReadString(); rep.Obj[id] = (m, r.ReadBoolean()); }
            int nw = r.ReadInt32();
            for (int i = 0; i < nw; i++) rep.Wait.Add(r.ReadString());
            if (!HostSide) return;
            EnsureKnown();
            string key = PlayerProgress.KeyOf(peer, name);
            KeyById[peer.Id] = key;
            bool newWait = !Known.TryGetValue(key, out var old) || !rep.Wait.SetEquals(old.Wait);
            Known[key] = rep;
            if (newWait && rep.Wait.Count > 0)
                MelonLogger.Msg($"[задания] {name} ждёт на пунктах: {string.Join(", ", rep.Wait)}");
            if (newWait) _nextEval = 0f;
        }

        // Друг: решение хоста.
        public static void ClientGate(BinaryReader r)
        {
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                string id = r.ReadString();
                bool release = r.ReadBoolean();
                string who = r.ReadString();
                if (release) Release(id, "все дошли");
                else if (Held.TryGetValue(id, out var h))
                {
                    if (h.Waiting != who) MelonLogger.Msg($"[задания] пункт {id}: ждём {who}");
                    h.Waiting = who;
                    WarnOnce(id, who);
                }
            }
        }

        private static byte[] Encode(Report rep)
        {
            var w = Msg.New(MsgType.QuestState);
            w.Write(rep.Missions.Count);
            foreach (var kv in rep.Missions) { w.Write(kv.Key); w.Write(kv.Value); }
            w.Write(rep.Obj.Count);
            foreach (var kv in rep.Obj) { w.Write(kv.Key); w.Write(kv.Value.mission); w.Write(kv.Value.done); }
            w.Write(rep.Wait.Count);
            foreach (var id in rep.Wait) w.Write(id);
            return Msg.Done(w);
        }

        private static string Signature(Report rep) =>
            string.Join(";", rep.Missions.Select(kv => kv.Key + (kv.Value ? "+" : "-"))) + "#" +
            string.Join(";", rep.Obj.Where(kv => kv.Value.done).Select(kv => kv.Key)) + "#" + string.Join(";", rep.Wait);

        // ---------- Свой журнал ----------

        private static Report Mine()
        {
            var rep = new Report();
            try
            {
                var amm = ActiveMissionManager.Instance;
                if (amm != null)
                {
                    var ms = amm.m_MissionInfoList;
                    if (ms != null)
                        for (int i = 0; i < ms.Count; i++)
                        {
                            var m = ms[i];
                            if (m == null || string.IsNullOrEmpty(m.missionID)) continue;
                            rep.Missions[m.missionID] = m.completedSuccessfully || !m.isActive;
                        }
                    var os = amm.m_ObjectiveInfoList;
                    if (os != null)
                        for (int i = 0; i < os.Count; i++)
                        {
                            var o = os[i];
                            if (o == null || string.IsNullOrEmpty(o.objectiveID)) continue;
                            rep.Obj[o.objectiveID] = (o.missionID ?? "", o.completed);
                        }
                }
            }
            catch (Exception e) { MelonLogger.Warning("[задания] журнал: " + e.Message); }
            foreach (var id in Held.Keys) rep.Wait.Add(id);
            return rep;
        }

        private static StoryMissionObjective FindObjective(string id)
        {
            try
            {
                var os = ActiveMissionManager.Instance?.m_ObjectiveInfoList;
                if (os == null) return null;
                for (int i = 0; i < os.Count; i++) { var o = os[i]; if (o != null && o.objectiveID == id) return o; }
            }
            catch { }
            return null;
        }

        private static string Describe(StoryMissionObjective o)
        {
            try
            {
                string loc = !string.IsNullOrEmpty(o.hudDescriptionLocID) ? o.hudDescriptionLocID : o.descriptionLocID;
                string s = string.IsNullOrEmpty(loc) ? "" : Localization.Get(loc);
                if (!string.IsNullOrWhiteSpace(s) && s != loc) return s.Trim();
            }
            catch { }
            return o.objectiveID;
        }

        internal static string StatusText() =>
            Held.Count == 0 ? "" : "ждём на пунктах: " + string.Join(", ", Held.Select(kv => kv.Key + (kv.Value.Waiting.Length > 0 ? " (" + kv.Value.Waiting + ")" : "")));
    }

    [HarmonyPatch(typeof(Action_ObjectiveCompleteMain), "OnExecute")]
    internal static class Patch_ObjectiveCompleteMain
    {
        private static bool Prefix(Action_ObjectiveCompleteMain __instance) => QuestWait.OnComplete(__instance);
    }
}
