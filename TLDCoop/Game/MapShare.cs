using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Общая карта (правило хоста «Общая карта углём»): кто-то зарисовал местность углём — у остальных на карте
    // открывается то же место. Карта у каждого своя (хранится в его сохранении), поэтому зарисовку повторяем
    // в игре каждого: та же проверка окрестностей, что делает игра после угля, только в точке друга.
    // Хост хранит все зарисовки мира (progress/<мир>/surveys.txt) и присылает зарисовки локации каждому,
    // кто в неё заходит. Уже применённые у себя зарисовки помним в UserData/TLDCoop/map_applied.txt.
    public static class MapShare
    {
        private const byte KMap = 10;   // подвид WorldEvent

        private class Survey { public string Id, Scene; public Vector3 Pos; }

        private static readonly Dictionary<string, Survey> Stored = new();   // хост: все зарисовки мира
        private static string _storedFor, _storedSave;
        private static readonly HashSet<string> Applied = new();
        private static bool _appliedLoaded;
        private static readonly List<Survey> Waiting = new();                // для текущей локации, карта ещё не готова
        private static float _nextRetry;
        private static bool _applying;

        private static bool Enabled => Session.Mode != Mode.Off && Session.InSharedWorld && GameApi.InGame() && Session.CurrentRules.SharedMap;
        private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // ---------- Своя зарисовка ----------

        internal static void OnLocalSurvey()
        {
            if (_applying || !Enabled) return;
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return;
            string scene = ItemSync.SceneKey();
            var pos = new Vector3(x, y, z);
            string id = $"{Session.LocalName}|{scene}|{Mathf.RoundToInt(x)}|{Mathf.RoundToInt(z)}|{GameApi.TimeOfDay():0.00}";
            MarkApplied(id);
            if (Session.Mode == Mode.Host) Store(new Survey { Id = id, Scene = scene, Pos = pos });
            var w = Msg.New(MsgType.WorldEvent);
            w.Write(KMap);
            w.Write(scene);
            w.Write(1);
            Write(w, id, pos);
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg("[карта] зарисовал углём, отправил всем");
        }

        private static void Write(BinaryWriter w, string id, Vector3 p)
        {
            w.Write(id);
            w.Write(p.x); w.Write(p.y); w.Write(p.z);
        }

        // ---------- Чужие зарисовки ----------

        public static bool Handles(byte kind) => kind == KMap;

        public static void Receive(string scene, BinaryReader r)
        {
            int n = r.ReadInt32();
            int fresh = 0;
            for (int i = 0; i < n; i++)
            {
                var s = new Survey { Id = r.ReadString(), Scene = scene, Pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()) };
                if (Session.Mode == Mode.Host) Store(s);
                if (!Enabled || IsApplied(s.Id) || scene != ItemSync.SceneKey()) continue;   // в другой локации пришлют, когда зайдём
                Waiting.Add(s);
                fresh++;
            }
            if (fresh > 0) TryApply();
        }

        // Хост: друг зашёл в локацию — зарисовки этой локации.
        public static void SendSceneTo(Peer to, string scene)
        {
            if (Session.Mode != Mode.Host || to == null || !Session.CurrentRules.SharedMap) return;
            LoadStored();
            var list = new List<Survey>();
            foreach (var s in Stored.Values) if (s.Scene == scene) list.Add(s);
            if (list.Count == 0) return;
            var w = Msg.New(MsgType.WorldEvent);
            w.Write(KMap);
            w.Write(scene);
            w.Write(list.Count);
            foreach (var s in list) Write(w, s.Id, s.Pos);
            to.Send(Msg.Done(w));
        }

        // Хост сам зашёл в локацию: применить зарисовки друзей, которых у него ещё нет.
        public static void OnSceneSettled(string scene)
        {
            Waiting.Clear();
            if (Session.Mode != Mode.Host || !Enabled) return;
            LoadStored();
            foreach (var s in Stored.Values)
                if (s.Scene == ItemSync.SceneKey() && !IsApplied(s.Id)) Waiting.Add(s);
            TryApply();
        }

        public static void Tick(float now)
        {
            if (Waiting.Count == 0 || now < _nextRetry) return;
            _nextRetry = now + 5f;
            TryApply();
        }

        private static void TryApply()
        {
            if (Waiting.Count == 0) return;
            Panel_Map panel = null;
            try { InterfaceManager.TryGetPanel<Panel_Map>(out panel); } catch { }
            if (panel == null) return;
            int done = 0;
            _applying = true;
            try
            {
                foreach (var s in Waiting)
                {
                    if (s.Scene != ItemSync.SceneKey()) continue;
                    try
                    {
                        panel.DoNearbyDetailsCheck(panel.m_DetailSurveyRadiusMeters, true, true, s.Pos, false);
                        MarkApplied(s.Id);
                        done++;
                    }
                    catch (Exception e) { MelonLogger.Warning("[карта] зарисовка друга: " + e.Message); }
                }
            }
            finally { _applying = false; }
            Waiting.Clear();
            if (done > 0)
            {
                MelonLogger.Msg($"[карта] открыл на карте зарисовки друзей: {done}");
                GameApi.HudMessage(done == 1 ? "На карте появилась зарисовка друга" : $"На карте появились зарисовки друзей: {done}");
            }
        }

        // ---------- Хранение ----------

        private static string StoreFile()
        {
            string dir = PlayerProgress.WorldFolder();
            return dir == null ? null : Path.Combine(dir, "surveys.txt");
        }

        private static void LoadStored()
        {
            string file = StoreFile();
            if (file == null || _storedFor == file) return;
            string save = "";
            try { save = SaveGameSystem.GetCurrentSaveName() ?? ""; } catch { }
            // Свои зарисовки переносим, только если сменился ключ того же сохранения (номер игры, зерно появились
            // позже). Другой мир — зарисовки прошлого сюда не относятся.
            var mine = _storedFor == null || save == _storedSave ? new List<Survey>(Stored.Values) : new List<Survey>();
            Stored.Clear();
            _storedFor = file;
            _storedSave = save;
            try
            {
                if (File.Exists(file))
                    foreach (var line in File.ReadAllLines(file))
                    {
                        var p = line.Split('\t');
                        if (p.Length < 5) continue;
                        Stored[p[0]] = new Survey { Id = p[0], Scene = p[1], Pos = new Vector3(float.Parse(p[2], IC), float.Parse(p[3], IC), float.Parse(p[4], IC)) };
                    }
            }
            catch (Exception e) { MelonLogger.Warning("[карта] чтение зарисовок: " + e.Message); }
            foreach (var s in mine) Stored[s.Id] = s;
        }

        private static void Store(Survey s)
        {
            LoadStored();
            if (Stored.ContainsKey(s.Id)) return;
            Stored[s.Id] = s;
            string file = StoreFile();
            if (file == null) return;
            try
            {
                File.AppendAllText(file, string.Join("\t", s.Id.Replace("\t", " "), s.Scene,
                    s.Pos.x.ToString("R", IC), s.Pos.y.ToString("R", IC), s.Pos.z.ToString("R", IC)) + "\n");
            }
            catch (Exception e) { MelonLogger.Warning("[карта] запись зарисовки: " + e.Message); }
        }

        private static string AppliedFile() => Path.Combine(MelonEnvironment.UserDataDirectory, "TLDCoop", "map_applied.txt");

        private static bool IsApplied(string id)
        {
            if (!_appliedLoaded)
            {
                _appliedLoaded = true;
                try { if (File.Exists(AppliedFile())) foreach (var l in File.ReadAllLines(AppliedFile())) Applied.Add(l); } catch { }
            }
            return Applied.Contains(id);
        }

        private static void MarkApplied(string id)
        {
            if (IsApplied(id)) return;
            Applied.Add(id);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AppliedFile()));
                File.AppendAllText(AppliedFile(), id + "\n");
            }
            catch { }
        }

        public static void Reset()
        {
            Waiting.Clear();
            Stored.Clear();
            _storedFor = null;
            _storedSave = null;
        }
    }

    [HarmonyPatch(typeof(Panel_Map), nameof(Panel_Map.DoDetailSurvey))]
    internal static class Patch_MapSurvey
    {
        private static void Postfix(SurveyType surveyType)
        {
            if (surveyType == SurveyType.Charcoal) MapShare.OnLocalSurvey();
        }
    }
}
