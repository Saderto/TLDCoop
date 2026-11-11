using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Хроника вылазки и общие заметки (окно мода, вкладка «Игра»).
    //  - Хроника: заметные события — кто кого добыл, кто был ранен и кто поднял, кто погиб, новый день и погода,
    //    кто пришёл и ушёл. Хост ставит отметку времени (день и час игры) и хранит ленту вместе с миром
    //    (UserData/TLDCoop/progress/<мир>/chronicle.txt). По ней же удобно искать ошибки.
    //  - Заметки: что кто-то из вас записал («тайник с патронами у моста», «завтра идём на плотину»).
    //    Видны всем, хранятся у хоста (notes.txt), любой может удалить.
    //
    // Сообщение Chronicle: 0 друг → хост «событие», 1 хост → все «запись хроники», 2 друг → хост «заметка»,
    // 3 хост → все «все заметки», 4 друг → хост «удалить заметку», 5 хост → новому «вся хроника».
    public static class Chronicle
    {
        private const int Keep = 200;
        public static readonly List<string> Entries = new();
        public static readonly List<string> Notes = new();
        private static string _loadedFor = "";
        private static int _lastDay = -1;
        private static float _nextDayCheck;

        // ---------- События ----------

        // Любой игрок: случилось что-то заметное с нами (текст уже с именем).
        public static void Add(string text)
        {
            if (Session.Mode == Mode.Off || string.IsNullOrEmpty(text)) return;
            if (Session.Mode == Mode.Host) { HostAdd(text); return; }
            var w = Msg.New(MsgType.Chronicle);
            w.Write((byte)0);
            w.Write(Clip(text));
            Session.SendGameEvent(Msg.Done(w));
        }

        private static string Clip(string s) => s.Length > 160 ? s.Substring(0, 160) : s;

        private static string Stamp()
        {
            try
            {
                if (!GameApi.InGame()) return "";
                float tod = GameApi.TimeOfDay() * 24f;
                int h = (int)tod, m = (int)((tod - h) * 60f);
                return $"день {GameApi.DayNumber()}, {h:00}:{m:00} — ";
            }
            catch { return ""; }
        }

        private static void HostAdd(string text)
        {
            string entry = Stamp() + Clip(text);
            Push(entry);
            Append(entry);
            var w = Msg.New(MsgType.Chronicle);
            w.Write((byte)1);
            w.Write(entry);
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg("[хроника] " + entry);
        }

        private static void Push(string entry)
        {
            Entries.Add(entry);
            while (Entries.Count > Keep) Entries.RemoveAt(0);
        }

        // Хост: новый день — запись с погодой.
        public static void Tick(float now)
        {
            if (Session.Mode != Mode.Host || !GameApi.InGame() || now < _nextDayCheck) return;
            _nextDayCheck = now + 10f;
            HostLoad();
            int day;
            try { day = GameApi.DayNumber(); } catch { return; }
            if (_lastDay > 0 && day > _lastDay && Session.Players.Count > 0) HostAdd($"Новый день {day}: {WeatherName(GameApi.WeatherStage())}");
            _lastDay = day;
        }

        private static string WeatherName(int stage)
        {
            string n = Enum.GetName(typeof(Il2Cpp.WeatherStage), stage) ?? "";
            return n switch
            {
                "Clear" => "ясно",
                "PartlyCloudy" => "переменная облачность",
                "Cloudy" => "облачно",
                "LightSnow" => "лёгкий снег",
                "HeavySnow" => "сильный снегопад",
                "Blizzard" => "метель",
                "LightFog" => "лёгкий туман",
                "DenseFog" => "густой туман",
                "ClearAurora" => "ясно, полярное сияние",
                "ElectrostaticFog" => "электрический туман",
                "ToxicFog" => "ядовитый туман",
                _ => n.Length > 0 ? n : "погода неизвестна",
            };
        }

        // Добыча: кого добыли (винительный падеж).
        public static string Prey(string prefab)
        {
            string p = prefab ?? "";
            if (p.Contains("Bear")) return "медведя";
            if (p.Contains("Moose")) return "лося";
            if (p.Contains("Cougar")) return "пуму";
            if (p.Contains("Doe")) return "олениху";
            if (p.Contains("Stag") || p.Contains("Deer")) return "оленя";
            if (p.Contains("Rabbit")) return "кролика";
            if (p.Contains("Ptarmigan")) return "куропатку";
            if (p.Contains("grey") || p.Contains("Timber")) return "лесного волка";
            if (p.Contains("Wolf")) return "волка";
            return p.Replace("WILDLIFE_", "");
        }

        // ---------- Заметки ----------

        public static void AddNote(string text)
        {
            text = (text ?? "").Replace('\n', ' ').Trim();
            if (text.Length == 0 || Session.Mode == Mode.Off) return;
            string note = $"{Session.LocalName}: {Clip(text)}";
            if (Session.Mode == Mode.Host) { Notes.Add(note); HostNotesChanged(); return; }
            var w = Msg.New(MsgType.Chronicle);
            w.Write((byte)2);
            w.Write(note);
            Session.SendGameEvent(Msg.Done(w));
        }

        public static void RemoveNote(int i)
        {
            if (i < 0 || i >= Notes.Count) return;
            if (Session.Mode == Mode.Host) { Notes.RemoveAt(i); HostNotesChanged(); return; }
            var w = Msg.New(MsgType.Chronicle);
            w.Write((byte)4);
            w.Write(Notes[i]);
            Session.SendGameEvent(Msg.Done(w));
        }

        private static void HostNotesChanged()
        {
            SaveNotes();
            Session.SendGameEvent(NotesMessage());
        }

        private static byte[] NotesMessage()
        {
            var w = Msg.New(MsgType.Chronicle);
            w.Write((byte)3);
            w.Write(Notes.Count);
            foreach (var n in Notes) w.Write(n);
            return Msg.Done(w);
        }

        // ---------- Сеть ----------

        public static void HostReceive(BinaryReader r, string from)
        {
            byte kind = r.ReadByte();
            switch (kind)
            {
                case 0: HostAdd(r.ReadString()); break;
                case 2: { string n = Clip(r.ReadString()); Notes.Add(n); HostNotesChanged(); MelonLogger.Msg($"[заметки] {from}: {n}"); break; }
                case 4: { string n = r.ReadString(); if (Notes.Remove(n)) HostNotesChanged(); break; }
            }
        }

        public static void ClientReceive(BinaryReader r)
        {
            byte kind = r.ReadByte();
            switch (kind)
            {
                case 1: Push(r.ReadString()); break;
                case 3:
                {
                    int n = r.ReadInt32();
                    Notes.Clear();
                    for (int i = 0; i < n; i++) Notes.Add(r.ReadString());
                    break;
                }
                case 5:
                {
                    int n = r.ReadInt32();
                    Entries.Clear();
                    for (int i = 0; i < n; i++) Entries.Add(r.ReadString());
                    break;
                }
            }
        }

        // Хост: новому игроку — последние записи хроники и все заметки.
        public static void SendHistory(Peer peer)
        {
            HostLoad();
            peer.Send(HistoryMessage());
            peer.Send(NotesMessage());
        }

        private static byte[] HistoryMessage()
        {
            var w = Msg.New(MsgType.Chronicle);
            w.Write((byte)5);
            int from = Math.Max(0, Entries.Count - 60);
            w.Write(Entries.Count - from);
            for (int i = from; i < Entries.Count; i++) w.Write(Entries[i]);
            return Msg.Done(w);
        }

        // ---------- Хранение у хоста ----------

        private static void HostLoad()
        {
            string dir;
            try { dir = PlayerProgress.WorldFolder(); } catch { return; }
            if (dir == null || dir == _loadedFor) return;
            bool switched = _loadedFor.Length > 0;
            _loadedFor = dir;
            Entries.Clear();
            Notes.Clear();
            _lastDay = -1;
            try
            {
                string f = Path.Combine(dir, "chronicle.txt");
                if (File.Exists(f))
                {
                    var lines = File.ReadAllLines(f, Encoding.UTF8);
                    for (int i = Math.Max(0, lines.Length - Keep); i < lines.Length; i++) if (lines[i].Length > 0) Entries.Add(lines[i]);
                }
                string nf = Path.Combine(dir, "notes.txt");
                if (File.Exists(nf)) foreach (var l in File.ReadAllLines(nf, Encoding.UTF8)) if (l.Length > 0) Notes.Add(l);
                MelonLogger.Msg($"[хроника] загружено записей {Entries.Count}, заметок {Notes.Count}");
            }
            catch (Exception e) { MelonLogger.Warning("[хроника] чтение: " + e.Message); }
            // Хост сменил мир, не выходя из коопа: у друзей хроника и заметки прошлого мира — заменить.
            if (switched && Session.Mode == Mode.Host && Session.Players.Count > 0)
            {
                Session.SendGameEvent(HistoryMessage());
                Session.SendGameEvent(NotesMessage());
            }
        }

        private static void Append(string entry)
        {
            try
            {
                string dir = PlayerProgress.WorldFolder();
                if (dir != null) File.AppendAllText(Path.Combine(dir, "chronicle.txt"), entry + "\n", Encoding.UTF8);
            }
            catch (Exception e) { MelonLogger.Warning("[хроника] запись: " + e.Message); }
        }

        private static void SaveNotes()
        {
            try
            {
                string dir = PlayerProgress.WorldFolder();
                if (dir != null) File.WriteAllLines(Path.Combine(dir, "notes.txt"), Notes, Encoding.UTF8);
            }
            catch (Exception e) { MelonLogger.Warning("[заметки] запись: " + e.Message); }
        }

        public static void Reset()
        {
            if (Session.Mode != Mode.Host) { Entries.Clear(); Notes.Clear(); }
            _loadedFor = "";
            _lastDay = -1;
        }
    }
}
