using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Il2Cpp;
using Il2CppTLD.Stats;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Итоги вылазки в окне мода: сколько игровых дней прожили вместе (хост считает время, пока в мире есть
    // друзья), сколько каждый прошёл и сколько зверей добыл в этом мире. Каждый считает своё сам (шаги — по
    // перемещению, звери — по счётчикам игры) и раз в 20 секунд шлёт хосту прибавку. Хост складывает
    // и хранит в progress/<мир>/summary.txt, а таблицу рассылает всем.
    public static class Summary
    {
        private const byte KReport = 1, KTable = 2;
        private const byte KKill = 4;       // друг → хост: зверь умер в моей игре, стрелял такой-то (3 занят испытаниями)

        // Крупных зверей считаем по смерти зверя (AnimalSync.Killed), а не по счётчикам игры: мозг зверя бывает
        // в чужой игре, и тогда игра засчитывала добычу не тому, кто стрелял, а при передаче зверя — дважды.
        // Кролики и куропатки (камни, силки) — по-прежнему по счётчикам своей игры.
        private static readonly bool[] ByEvent = { true, true, false, true, true, true, false };
        private static readonly HashSet<long> CountedGids = new();

        static Summary()
        {
            AnimalSync.Killed += OnKilled;
        }

        private static readonly StatID[] Kills =
        {
            StatID.WolvesKilled, StatID.StagsKilled, StatID.RabbitsKilled, StatID.BearsKilled,
            StatID.MooseKilled, StatID.CougarsKilled, StatID.PtarmigansKilled,
        };
        private static string[] KillNames => UI.L.En
            ? new[] { "wolves", "deer", "rabbits", "bears", "moose", "cougars", "ptarmigans" }
            : new[] { "волки", "олени", "кролики", "медведи", "лоси", "пумы", "куропатки" };

        public class Row { public string Name = ""; public float Km; public int[] Kills = new int[7]; }

        // Своё: прибавка с прошлой отправки.
        private static float _km;
        private static readonly int[] _kills = new int[7];
        private static float[] _statBase;
        private static Vector3 _lastPos;
        private static string _lastScene = "";
        private static float _nextReport, _nextTable, _lastHours = -1f;

        // Хост: таблица мира. Друг: последняя присланная таблица.
        private static readonly Dictionary<string, Row> Rows = new();
        private static float _hoursTogether;
        private static string _loadedFor;
        private static bool _dirty;
        public static float HoursTogether => _hoursTogether;
        public static IEnumerable<Row> All => Rows.Values;

        private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        public static void Tick(float now)
        {
            if (Session.Mode == Mode.Off || !Session.InSharedWorld || !GameApi.InGame()) { _lastScene = ""; _statBase = null; return; }
            TrackWalk();
            TrackKills();
            if (Session.Mode == Mode.Host)
            {
                LoadTable();
                TrackHours();
                if (now >= _nextTable)
                {
                    _nextTable = now + 20f;
                    Add(Session.LocalName, TakeKm(), TakeKills());
                    SaveTable();
                    SendTable();
                }
            }
            else if (now >= _nextReport)
            {
                _nextReport = now + 20f;
                float km = TakeKm();
                var k = TakeKills();
                if (km <= 0f && Array.TrueForAll(k, v => v == 0)) return;
                var w = Msg.New(MsgType.Summary);
                w.Write(KReport);
                w.Write(km);
                foreach (var v in k) w.Write((short)v);
                Session.SendGameEvent(Msg.Done(w));
            }
        }

        private static void TrackWalk()
        {
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return;
            var p = new Vector3(x, y, z);
            string scene = GameApi.Scene();
            bool train = false;
            try { train = Il2CppTLD.SteamTrain.SteamTrain.IsInsideSteamTrain; } catch { }
            if (scene == _lastScene && !train)
            {
                float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(_lastPos.x, 0f, _lastPos.z));
                if (d < 15f) _km += d / 1000f;   // больше — телепорт или переход
            }
            _lastScene = scene;
            _lastPos = p;
        }

        private static void TrackKills()
        {
            try
            {
                if (!StatsManager.CanTrackStats()) return;
                if (_statBase == null)
                {
                    _statBase = new float[Kills.Length];
                    for (int i = 0; i < Kills.Length; i++) _statBase[i] = StatsManager.GetValue(Kills[i]);
                    return;
                }
                for (int i = 0; i < Kills.Length; i++)
                {
                    float v = StatsManager.GetValue(Kills[i]);
                    if (v > _statBase[i] && !ByEvent[i]) _kills[i] += Mathf.RoundToInt(v - _statBase[i]);
                    _statBase[i] = v;
                }
            }
            catch { }
        }

        private static void TrackHours()
        {
            float h;
            try { h = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused(); } catch { return; }
            if (_lastHours >= 0f && Session.Players.Count > 0 && h > _lastHours && h - _lastHours < 48f)
            {
                _hoursTogether += h - _lastHours;
                _dirty = true;
            }
            _lastHours = h;
        }

        private static float TakeKm() { float v = _km; _km = 0f; return v; }

        private static int KillIndex(string prefab)
        {
            string p = prefab ?? "";
            if (p.Contains("Wolf")) return 0;
            if (p.Contains("Stag") || p.Contains("Doe") || p.Contains("Deer")) return 1;
            if (p.Contains("Rabbit")) return 2;
            if (p.Contains("Bear")) return 3;
            if (p.Contains("Moose")) return 4;
            if (p.Contains("Cougar")) return 5;
            if (p.Contains("Ptarmigan")) return 6;
            return -1;
        }

        // Зверь умер в нашей игре (мы держали его мозг). Хост засчитывает сам, друг сообщает хосту.
        private static void OnKilled(int shooter, string prefab, long gid)
        {
            try
            {
                if (Session.Mode == Mode.Off || !Session.InSharedWorld || shooter < 0) return;
                int idx = KillIndex(prefab);
                if (idx < 0 || !ByEvent[idx]) return;
                if (Session.Mode == Mode.Host) { CountKill(shooter, idx, gid); return; }
                var w = Msg.New(MsgType.Summary);
                w.Write(KKill);
                w.Write(shooter);
                w.Write((byte)idx);
                w.Write(gid);
                Session.SendGameEvent(Msg.Done(w));
            }
            catch (Exception e) { MelonLogger.Warning("[итоги] добыча: " + e.Message); }
        }

        // Хост: один зверь — одна добыча, тому, кто попал последним.
        private static void CountKill(int shooter, int idx, long gid)
        {
            if (idx < 0 || idx >= 7 || !CountedGids.Add(gid)) return;
            string name = shooter == Session.LocalId ? Session.LocalName
                : Session.Players.TryGetValue(shooter, out var pl) ? pl.Name : null;
            if (string.IsNullOrEmpty(name)) return;
            LoadTable();
            var k = new int[7];
            k[idx] = 1;
            Add(name, 0f, k);
        }

        private static int[] TakeKills()
        {
            var k = (int[])_kills.Clone();
            Array.Clear(_kills, 0, _kills.Length);
            return k;
        }

        private static void Add(string name, float km, int[] kills)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!Rows.TryGetValue(name, out var row)) Rows[name] = row = new Row { Name = name };
            row.Km += Math.Max(0f, km);
            for (int i = 0; i < row.Kills.Length && i < kills.Length; i++) row.Kills[i] += Math.Max(0, kills[i]);
            if (km > 0f || Array.Exists(kills, v => v > 0)) _dirty = true;
        }

        // ---------- Сеть ----------

        public static void Receive(BinaryReader r, string fromName)
        {
            byte kind = r.ReadByte();
            if (kind == KReport && Session.Mode == Mode.Host)
            {
                float km = r.ReadSingle();
                var k = new int[7];
                for (int i = 0; i < 7; i++) k[i] = r.ReadInt16();
                LoadTable();
                Add(fromName, km, k);
            }
            else if (kind == KKill && Session.Mode == Mode.Host)
            {
                int shooter = r.ReadInt32();
                int idx = r.ReadByte();
                long gid = r.ReadInt64();
                CountKill(shooter, idx, gid);
            }
            else if (kind == Challenges.KChallenge && Session.Mode == Mode.Client) Challenges.Receive(r);
            else if (kind == KTable && Session.Mode == Mode.Client)
            {
                _hoursTogether = r.ReadSingle();
                int n = r.ReadByte();
                Rows.Clear();
                for (int i = 0; i < n; i++)
                {
                    var row = new Row { Name = r.ReadString(), Km = r.ReadSingle() };
                    for (int j = 0; j < 7; j++) row.Kills[j] = r.ReadInt16();
                    Rows[row.Name] = row;
                }
            }
        }

        private static void SendTable()
        {
            if (Session.Players.Count == 0) return;
            var w = Msg.New(MsgType.Summary);
            w.Write(KTable);
            w.Write(_hoursTogether);
            int n = Math.Min(Rows.Count, 32);
            w.Write((byte)n);
            int c = 0;
            foreach (var row in Rows.Values)
            {
                if (c++ >= n) break;
                w.Write(row.Name);
                w.Write(row.Km);
                foreach (var v in row.Kills) w.Write((short)Math.Min(v, short.MaxValue));
            }
            Session.SendGameEvent(Msg.Done(w));
        }

        // ---------- Хранение у хоста ----------

        private static string File_()
        {
            string dir = PlayerProgress.WorldFolder();
            return dir == null ? null : Path.Combine(dir, "summary.txt");
        }

        private static void LoadTable()
        {
            string f = File_();
            if (f == null || _loadedFor == f) return;
            _loadedFor = f;
            Rows.Clear();
            _hoursTogether = 0f;
            try
            {
                if (!File.Exists(f)) return;
                foreach (var line in File.ReadAllLines(f))
                {
                    var p = line.Split('\t');
                    if (p.Length == 2 && p[0] == "#hours") { float.TryParse(p[1], NumberStyles.Float, IC, out _hoursTogether); continue; }
                    if (p.Length < 9) continue;
                    var row = new Row { Name = p[0], Km = float.Parse(p[1], IC) };
                    for (int i = 0; i < 7; i++) int.TryParse(p[2 + i], out row.Kills[i]);
                    Rows[row.Name] = row;
                }
            }
            catch (Exception e) { MelonLogger.Warning("[итоги] чтение: " + e.Message); }
        }

        private static void SaveTable(string f = null)
        {
            if (!_dirty) return;
            f ??= File_();
            if (f == null) return;
            _dirty = false;
            var sb = new StringBuilder();
            sb.Append("#hours\t").Append(_hoursTogether.ToString("R", IC)).Append('\n');
            foreach (var row in Rows.Values)
            {
                sb.Append(row.Name.Replace("\t", " ")).Append('\t').Append(row.Km.ToString("R", IC));
                foreach (var v in row.Kills) sb.Append('\t').Append(v);
                sb.Append('\n');
            }
            try { File.WriteAllText(f, sb.ToString()); } catch (Exception e) { MelonLogger.Warning("[итоги] запись: " + e.Message); }
        }

        // ---------- Для окна ----------

        public static string Together()
        {
            int h = Mathf.FloorToInt(_hoursTogether);
            return UI.L.T($"{h / 24} дн. {h % 24} ч.", $"{h / 24} d {h % 24} h");
        }

        public static string Line(Row row)
        {
            int total = 0;
            var parts = new List<string>();
            for (int i = 0; i < row.Kills.Length; i++)
            {
                if (row.Kills[i] <= 0) continue;
                total += row.Kills[i];
                parts.Add($"{KillNames[i]} {row.Kills[i]}");
            }
            string km = row.Km.ToString("0.0", IC);
            if (!UI.L.En) km = km.Replace('.', ',');   // ru-RU в игре недоступна (globalization-invariant)
            return row.Name.Replace("<", "‹") + UI.L.T($": пешком {km} км, зверей {total}", $": walked {km} km, animals {total}") + (parts.Count > 0 ? " (" + string.Join(", ", parts) + ")" : "");
        }

        public static void Reset()
        {
            _statBase = null;
            _lastScene = "";
            _lastHours = -1f;
            _km = 0f;
            Array.Clear(_kills, 0, _kills.Length);
            ForgetWorld();
        }

        // Сервер остановлен или мир сменился. Reset идёт уже после Mode = Off, поэтому раньше здесь
        // чистилась и таблица хоста, а _loadedFor оставался: после нового запуска сервера в том же мире
        // LoadTable думал, что файл уже прочитан, и через 20 с SaveTable затирал summary.txt новыми цифрами.
        // Теперь несохранённое дописываем в файл того мира, откуда оно, и при следующем старте читаем файл заново.
        internal static void ForgetWorld()
        {
            if (_loadedFor != null) SaveTable(_loadedFor);
            _loadedFor = null;
            _dirty = false;
            CountedGids.Clear();
            Rows.Clear();
            _hoursTogether = 0f;
        }
    }
}
