using System;
using System.Collections.Generic;
using System.IO;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Случайные объекты локации (RandomSpawnObject): при первом заходе игра выбирает, какая машина, куча хлама
    // или ящик появятся. Без мода у друга мог выпасть другой набор (вместе с контейнерами в них).
    // Правило: набор хоста главнее; если хост в локации ещё не был, решает тот, кто зашёл первым.
    // Хост хранит наборы в progress/<мир>/random_objects.txt и присылает набор каждому, кто заходит в локацию.
    public static class RandomObjects
    {
        private const byte KRandom = 11;          // подвид WorldEvent
        private const byte Claim = 0, Layout = 1;

        private static readonly Dictionary<string, string> Layouts = new();   // хост: локация → FastJson набора
        private static string _loadedFor;

        public static bool Handles(byte kind) => kind == KRandom;

        // ---------- Свой набор ----------

        private static string Current(out bool fresh)
        {
            fresh = false;
            foreach (var rso in UnityEngine.Object.FindObjectsOfType<RandomSpawnObject>(true))
                if (rso != null && rso.m_Inited && !rso.m_HadSaveData) { fresh = true; break; }
            try { return RandomSpawnObjectManager.Serialize() ?? ""; } catch { return ""; }
        }

        // Набор по объектам: PDID → имена включённых.
        private static Dictionary<string, List<RandomSpawnObjectSaveData>> Parse(string json)
        {
            var d = new Dictionary<string, List<RandomSpawnObjectSaveData>>();
            if (string.IsNullOrEmpty(json)) return d;
            var list = FastJson.Deserialize<RandomSpawnObjectSaveList>(json)?.m_SaveDataList;
            if (list == null) return d;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || string.IsNullOrEmpty(e.m_PDID)) continue;
                if (!d.TryGetValue(e.m_PDID, out var l)) d[e.m_PDID] = l = new List<RandomSpawnObjectSaveData>();
                l.Add(e);
            }
            return d;
        }

        private static string Names(List<RandomSpawnObjectSaveData> l)
        {
            var n = new List<string>();
            foreach (var e in l) n.Add(e.m_ObjectName);
            n.Sort(StringComparer.Ordinal);
            return string.Join("|", n);
        }

        public static void OnSceneSettled(string scene)
        {
            if (Session.Mode == Mode.Off || !Session.InSharedWorld) return;
            try
            {
                string mine = Current(out bool fresh);
                if (mine.Length == 0) return;
                if (Session.Mode == Mode.Host)
                {
                    LoadStore();
                    // Свой набор из сохранения — он главный. Свежий бросок уступает набору друга, который был тут первым.
                    if (fresh && Layouts.TryGetValue(scene, out var stored)) Apply(stored);
                    else Store(scene, mine);
                    return;
                }
                var w = Msg.New(MsgType.WorldEvent);
                w.Write(KRandom);
                w.Write(scene);
                w.Write(Claim);
                w.Write(mine);
                Session.SendGameEvent(Msg.Done(w));
            }
            catch (Exception e) { MelonLogger.Warning("[случайные объекты] " + e.Message); }
        }

        // ---------- Сеть ----------

        public static void Receive(string scene, BinaryReader r, Peer from)
        {
            byte what = r.ReadByte();
            string json = r.ReadString();
            if (what == Claim)
            {
                if (Session.Mode != Mode.Host || from == null) return;
                LoadStore();
                if (!Layouts.TryGetValue(scene, out var stored))
                {
                    // Хоста здесь не было (или в его сохранении нет набора): решает пришедший первым.
                    Store(scene, json);
                    if (ItemSync.SceneKey() == scene) { Current(out bool hostFresh); if (hostFresh) Apply(json); }
                    return;
                }
                var w = Msg.New(MsgType.WorldEvent);
                w.Write(KRandom);
                w.Write(scene);
                w.Write(Layout);
                w.Write(stored);
                from.Send(Msg.Done(w));
            }
            else if (what == Layout && Session.Mode == Mode.Client && ItemSync.SceneKey() == scene)
                Apply(json);
        }

        // ---------- Применить набор ----------

        private static void Apply(string json)
        {
            try
            {
                var want = Parse(json);
                var have = Parse(Current(out _));
                int changed = 0;
                foreach (var rso in UnityEngine.Object.FindObjectsOfType<RandomSpawnObject>(true))
                {
                    if (rso == null) continue;
                    string pdid = ObjectGuid.MaybeGetGuidFromGameObject(rso.gameObject);
                    if (string.IsNullOrEmpty(pdid) || !want.TryGetValue(pdid, out var w)) continue;
                    if (have.TryGetValue(pdid, out var h) && Names(h) == Names(w)) continue;
                    rso.DisableAll();
                    foreach (var e in w) rso.LoadObject(e.m_ObjectName, e.m_Position, e.m_ElapsedHoursAtLastReroll);
                    changed++;
                }
                if (changed > 0)
                {
                    WorldObjects.ForgetCache(typeof(Container));
                    MelonLogger.Msg($"[случайные объекты] {ItemSync.SceneKey()}: поставил как у первого пришедшего, объектов {changed}");
                }
            }
            catch (Exception e) { MelonLogger.Warning("[случайные объекты] применить: " + e.Message); }
        }

        // ---------- Хранение у хоста ----------

        private static string FileName()
        {
            string dir = PlayerProgress.WorldFolder();
            return dir == null ? null : Path.Combine(dir, "random_objects.txt");
        }

        private static void LoadStore()
        {
            string f = FileName();
            if (f == null || _loadedFor == f) return;
            _loadedFor = f;
            Layouts.Clear();
            try
            {
                if (!File.Exists(f)) return;
                foreach (var line in File.ReadAllLines(f))
                {
                    int tab = line.IndexOf('\t');
                    if (tab > 0) Layouts[line.Substring(0, tab)] = line.Substring(tab + 1);
                }
            }
            catch (Exception e) { MelonLogger.Warning("[случайные объекты] чтение: " + e.Message); }
        }

        private static void Store(string scene, string json)
        {
            if (Layouts.TryGetValue(scene, out var old) && old == json) return;
            Layouts[scene] = json.Replace("\n", " ").Replace("\t", " ");
            string f = FileName();
            if (f == null) return;
            var lines = new List<string>();
            foreach (var kv in Layouts) lines.Add(kv.Key + "\t" + kv.Value);
            try { File.WriteAllLines(f, lines); } catch (Exception e) { MelonLogger.Warning("[случайные объекты] запись: " + e.Message); }
        }

        public static void Reset()
        {
            Layouts.Clear();
            _loadedFor = null;
        }
    }
}
