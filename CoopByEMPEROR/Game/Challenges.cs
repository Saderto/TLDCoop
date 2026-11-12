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
    // Кооп-испытания: хост выбирает в окне простую задачу на всех, ход и итог видят все в окне мода.
    //  — Встретиться у метки: все игроки ближе 15 м к последней метке хоста (на карте или «сюда»).
    //  — 3 дня без ранений: прошло 72 игровых часа, и никто не был ранен (не лежал «ранен — подними»).
    //  — Добыть медведя / трёх волков: по счётчикам «Итогов вылазки» (у хоста они обновляются раз в 20 с).
    //  — Пройти вместе 5 км: сумма пройденного всеми с начала испытания.
    // Выполненные хост хранит в progress/<мир>/challenges.txt.
    public static class Challenges
    {
        public const byte KChallenge = 3;   // подвид MsgType.Summary
        public enum State : byte { None, Active, Done, Failed }

        public static string[] Titles => UI.L.En
            ? new[] { "Meet at my marker", "3 days without injuries", "Hunt a bear", "Hunt three wolves", "Walk 5 km together" }
            : new[] { "Встретиться у моей метки", "3 дня без ранений", "Добыть медведя", "Добыть трёх волков", "Пройти вместе 5 км" };

        // Что видят все.
        public static string Title { get; private set; } = "";
        public static string Progress { get; private set; } = "";
        public static State Now { get; private set; }
        public static readonly List<string> Completed = new();

        // Хост: условия текущего.
        private static int _kind = -1;
        private static float _startHours, _startKm, _nextCheck, _nextSend;
        private static int _startBears, _startWolves;
        private static Vector3 _target;
        private static string _targetScene = "";
        private static string _loadedFor;

        public static bool CanStart => Session.Mode == Mode.Host && Session.InSharedWorld && GameApi.InGame();

        public static void Start(int kind)
        {
            if (!CanStart || kind < 0 || kind >= Titles.Length) return;
            if (kind == 0 && !Together.OwnMarker(out _target, out _targetScene))
            {
                GameApi.HudMessage("Сначала поставь метку: на карте или «сюда»");
                return;
            }
            _kind = kind;
            _startHours = Hours();
            Totals(out _startKm, out _startBears, out _startWolves);
            Title = Titles[kind];
            Now = State.Active;
            Progress = UI.L.T("началось", "started");
            Session.Log("Хост начал испытание: " + Title);
            Send();
        }

        public static void Cancel()
        {
            if (Session.Mode != Mode.Host || Now != State.Active) return;
            Finish(State.Failed, UI.L.T("отменено хостом", "cancelled by the host"));
        }

        public static void Tick(float now)
        {
            if (Session.Mode != Mode.Host || Now != State.Active || !GameApi.InGame()) return;
            if (now >= _nextCheck)
            {
                _nextCheck = now + 2f;
                try { Check(); } catch (Exception e) { MelonLogger.Warning("[испытания] " + e.Message); }
            }
            if (now >= _nextSend) Send();
        }

        private static void Check()
        {
            Totals(out float km, out int bears, out int wolves);
            switch (_kind)
            {
                case 0:
                {
                    int far = 0, all = 0;
                    foreach (var (scene, pos) in Everyone())
                    {
                        all++;
                        if (scene != _targetScene || Vector3.Distance(pos, _target) > 15f) far++;
                    }
                    Progress = far == 0 ? UI.L.T("все на месте", "everyone is there") : UI.L.T($"на месте {all - far} из {all}", $"{all - far} of {all} there");
                    if (far == 0 && all > 1) Finish(State.Done, UI.L.T("все встретились у метки", "everyone met at the marker"));
                    break;
                }
                case 1:
                {
                    string hurt = WhoIsDowned();
                    if (hurt != null) { Finish(State.Failed, UI.L.T("ранение: ", "injured: ") + hurt); return; }
                    float h = Hours() - _startHours;
                    Progress = UI.L.T($"прошло {Mathf.FloorToInt(h / 24f)} дн. {Mathf.FloorToInt(h % 24f)} ч. из 3 дней", $"{Mathf.FloorToInt(h / 24f)} d {Mathf.FloorToInt(h % 24f)} h of 3 days");
                    if (h >= 72f) Finish(State.Done, UI.L.T("3 дня без единого ранения", "3 days without a single injury"));
                    break;
                }
                case 2:
                    Progress = bears > _startBears ? UI.L.T("медведь добыт", "bear hunted") : UI.L.T("медведь ещё бродит", "the bear is still out there");
                    if (bears > _startBears) Finish(State.Done, UI.L.T("медведь добыт", "bear hunted"));
                    break;
                case 3:
                    Progress = UI.L.T($"волков {Math.Min(3, wolves - _startWolves)} из 3", $"wolves {Math.Min(3, wolves - _startWolves)} of 3");
                    if (wolves - _startWolves >= 3) Finish(State.Done, UI.L.T("три волка добыты", "three wolves hunted"));
                    break;
                case 4:
                    Progress = UI.L.T($"пройдено {km - _startKm:0.0} из 5 км", $"{km - _startKm:0.0} of 5 km walked");
                    if (km - _startKm >= 5f) Finish(State.Done, UI.L.T("5 км пройдено", "5 km walked"));
                    break;
            }
        }

        private static IEnumerable<(string scene, Vector3 pos)> Everyone()
        {
            if (GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) yield return (GameApi.Scene(), new Vector3(x, y, z));
            float now = Time.unscaledTime;
            foreach (var pl in Session.Players.Values)
                if (pl.Id != Session.LocalId && now - pl.LastState < 10f) yield return (pl.InWorld ? pl.Scene : "", new Vector3(pl.X, pl.Y, pl.Z));
        }

        private static string WhoIsDowned()
        {
            if (Death.IsDowned) return Session.LocalName;
            foreach (var pl in Session.Players.Values) if (pl.Action == Activity.Downed) return pl.Name;
            return null;
        }

        private static void Totals(out float km, out int bears, out int wolves)
        {
            km = 0f; bears = 0; wolves = 0;
            foreach (var row in Summary.All) { km += row.Km; wolves += row.Kills[0]; bears += row.Kills[3]; }
        }

        private static float Hours()
        {
            try { return GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused(); } catch { return 0f; }
        }

        private static void Finish(State st, string why)
        {
            Now = st;
            Progress = why;
            _kind = -1;
            if (st == State.Done)
            {
                Load();
                Completed.Add($"{Title} ({DateTime.Now:dd.MM})");
                Save();
            }
            string line = (st == State.Done ? "Испытание выполнено: " : "Испытание не удалось: ") + Title + " — " + why;
            Session.Log(line);
            GameApi.HudMessage(line);
            Send();
        }

        // ---------- Сеть ----------

        private static void Send()
        {
            _nextSend = Time.unscaledTime + 5f;
            if (Session.Mode != Mode.Host || Session.Players.Count == 0) return;
            var w = Msg.New(MsgType.Summary);
            w.Write(KChallenge);
            w.Write(Title);
            w.Write(Progress);
            w.Write((byte)Now);
            Load();
            int n = Math.Min(Completed.Count, 20);
            w.Write((byte)n);
            for (int i = Completed.Count - n; i < Completed.Count; i++) w.Write(Completed[i]);
            Session.SendGameEvent(Msg.Done(w));
        }

        public static void Receive(BinaryReader r)
        {
            string title = r.ReadString(), progress = r.ReadString();
            var st = (State)r.ReadByte();
            if (st != Now || title != Title)
            {
                if (st == State.Active) GameApi.HudMessage("Хост начал испытание: " + title);
                else if (st == State.Done && Now == State.Active) GameApi.HudMessage("Испытание выполнено: " + title);
                else if (st == State.Failed && Now == State.Active) GameApi.HudMessage("Испытание не удалось: " + title);
            }
            Title = title; Progress = progress; Now = st;
            Completed.Clear();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++) Completed.Add(r.ReadString());
        }

        // ---------- Хранение у хоста ----------

        private static string FileName()
        {
            string dir = PlayerProgress.WorldFolder();
            return dir == null ? null : Path.Combine(dir, "challenges.txt");
        }

        private static void Load()
        {
            if (Session.Mode != Mode.Host) return;
            string f = FileName();
            if (f == null || _loadedFor == f) return;
            _loadedFor = f;
            Completed.Clear();
            try { if (File.Exists(f)) Completed.AddRange(File.ReadAllLines(f).Where(l => l.Length > 0)); } catch { }
        }

        private static void Save()
        {
            string f = FileName();
            if (f == null) return;
            try { File.WriteAllLines(f, Completed); } catch (Exception e) { MelonLogger.Warning("[испытания] запись: " + e.Message); }
        }

        // Хост загрузил другой мир, не выходя из коопа (Game/WorldReset.cs): испытание и выполненные — того мира.
        public static void ForgetWorld()
        {
            if (Session.Mode != Mode.Host) return;
            Reset();
            Send();
        }

        public static void Reset()
        {
            _kind = -1;
            Now = State.None;
            Title = Progress = "";
            Completed.Clear();
            _loadedFor = null;
        }
    }
}
