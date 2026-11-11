using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Квестовые вещи у каждого свои (ItemSync.IsPersonal), но мир у друга — копия мира хоста. Поэтому:
    //  — кто первым загрузил локацию (хост или друг), тот сообщает хосту, какие квестовые вещи в ней лежат
    //    (на земле и в уже обысканных контейнерах). Хост запоминает их навсегда (progress/<мир>/quest_items.txt);
    //  — кто подобрал такую вещь, о том хост помнит, что он её взял;
    //  — каждый раз, когда игрок заходит в локацию, хост присылает ему: какие вещи вернуть на место (он их не
    //    брал, а у него их нет — например, их подобрал хост ещё до первого прихода друга) и какие убрать
    //    (он их уже взял, но копия мира от хоста снова их содержит).
    public static class QuestItems
    {
        private const byte KSeen = 1, KTaken = 2, KFix = 3, KDropped = 4;
        private const float Near = 1.5f;

        private class Entry
        {
            public string Key = "", Scene = "", Prefab = "", Container = "", Json = "";
            public Vector3 Pos;
            public Quaternion Rot = Quaternion.identity;
        }

        // Хост: вещи по локациям и кто что взял (имя → ключи).
        private static readonly Dictionary<string, List<Entry>> Known = new();
        private static readonly Dictionary<string, HashSet<string>> Taken = new();
        // Хост: свои квестовые вещи, которые игроки сами выложили (на землю или в ящик) — это не вещи локации.
        private static readonly Dictionary<string, List<Entry>> Dropped = new();
        private static string _loadedFor;

        private static string _scene = "";
        private static float _reportAt = -1f;
        private static readonly Dictionary<IntPtr, List<(string prefab, string json)>> OpenedWith = new();
        private static readonly HashSet<IntPtr> OpenedPopulated = new();

        private static bool Active => Session.Mode != Mode.Off && Session.InSharedWorld && GameApi.InGame();
        private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // ---------- Свои события ----------

        public static void OnSceneSettled(string scene)
        {
            _scene = scene;
            _reportAt = Time.unscaledTime + 3f;   // пусть задания успеют разложить свои вещи
        }

        public static void Tick(float now)
        {
            if (!Active) { _reportAt = -1f; return; }
            if (_reportAt < 0f || now < _reportAt) return;
            _reportAt = -1f;
            if (ItemSync.SceneKey() != _scene) return;
            try { Report(); } catch (Exception e) { MelonLogger.Warning("[квест] " + e.Message); }
        }

        private static List<Entry> Present()
        {
            var list = new List<Entry>();
            foreach (var gi in UnityEngine.Object.FindObjectsOfType<GearItem>())
            {
                if (gi == null || gi.m_InPlayerInventory || !ItemSync.IsPersonal(gi)) continue;
                string container = "";
                if (gi.m_InsideContainer)
                {
                    var c = gi.GetComponentInParent<Container>();
                    if (c == null || c.m_NotPopulated) continue;
                    container = WorldObjects.IdOf(c);
                }
                else if (!gi.gameObject.activeInHierarchy) continue;
                var t = gi.transform;
                list.Add(new Entry { Scene = _scene, Prefab = ItemSync.PrefabName(gi), Container = container, Pos = t.position, Rot = t.rotation, Json = gi.SerializeToString() ?? "" });
            }
            return list;
        }

        private static void Report()
        {
            var items = Present();
            if (Session.Mode == Mode.Host) { HostSeen(Session.LocalName, Session.LocalId, _scene, items); return; }
            var w = Msg.New(MsgType.Quest);
            w.Write(KSeen);
            w.Write(_scene);
            WriteEntries(w, items, true);
            Session.SendGameEvent(Msg.Done(w));
        }

        // Подобрал с земли (ItemSync.OnLocalPickup для своих вещей).
        internal static void OnTakenFromGround(GearItem gi, Vector3 pos)
        {
            if (!Active || gi == null) return;
            SendTaken(ItemSync.SceneKey(), ItemSync.PrefabName(gi), pos, "");
        }

        internal static void OnContainerOpened(Container c)
        {
            if (!Active || c == null) return;
            var list = new List<(string, string)>();
            var items = new Il2CppSystem.Collections.Generic.List<GearItem>();
            c.GetItems(items);
            foreach (var gi in items) if (ItemSync.IsPersonal(gi)) list.Add((ItemSync.PrefabName(gi), ""));
            OpenedWith[c.Pointer] = list;
            if (c.m_NotPopulated) OpenedPopulated.Remove(c.Pointer); else OpenedPopulated.Add(c.Pointer);
        }

        // Выложил свою квестовую вещь на землю (ItemSync.OnLocalDrop).
        internal static void OnDropped(GearItem gi)
        {
            if (!Active || gi == null) return;
            SendDropped(ItemSync.SceneKey(), ItemSync.PrefabName(gi), gi.transform.position, "");
        }

        private static void SendDropped(string scene, string prefab, Vector3 pos, string container)
        {
            if (Session.Mode == Mode.Host) { HostDropped(Session.LocalName, scene, prefab, pos, container); return; }
            var w = Msg.New(MsgType.Quest);
            w.Write(KDropped);
            w.Write(scene);
            w.Write(prefab);
            w.Write(pos.x); w.Write(pos.y); w.Write(pos.z);
            w.Write(container);
            Session.SendGameEvent(Msg.Done(w));
        }

        // Закрыл контейнер: свои вещи, которых в нём больше нет, — взяты. Новые (контейнер только что обыскан) — в отчёт.
        internal static void OnContainerClosed(Container c)
        {
            if (!Active || c == null) return;
            OpenedWith.TryGetValue(c.Pointer, out var before);
            OpenedWith.Remove(c.Pointer);
            bool wasPopulated = OpenedPopulated.Remove(c.Pointer);
            var now = new List<string>();
            var items = new Il2CppSystem.Collections.Generic.List<GearItem>();
            c.GetItems(items);
            foreach (var gi in items) if (ItemSync.IsPersonal(gi)) now.Add(ItemSync.PrefabName(gi));
            string id = WorldObjects.IdOf(c), scene = ItemSync.SceneKey();
            if (before != null)
                foreach (var (prefab, _) in before)
                {
                    if (now.Remove(prefab)) continue;
                    SendTaken(scene, prefab, c.transform.position, id);
                }
            if (now.Count == 0) return;
            if (wasPopulated && before != null)
                foreach (var prefab in now) SendDropped(scene, prefab, c.transform.position, id);   // положил сам
            else _reportAt = Time.unscaledTime + 0.5f;   // контейнер только что обыскан — сообщим хосту, что в нём
        }

        private static void SendTaken(string scene, string prefab, Vector3 pos, string container)
        {
            if (Session.Mode == Mode.Host) { HostTaken(Session.LocalName, scene, prefab, pos, container); return; }
            var w = Msg.New(MsgType.Quest);
            w.Write(KTaken);
            w.Write(scene);
            w.Write(prefab);
            w.Write(pos.x); w.Write(pos.y); w.Write(pos.z);
            w.Write(container);
            Session.SendGameEvent(Msg.Done(w));
        }

        private static void WriteEntries(BinaryWriter w, List<Entry> list, bool json)
        {
            w.Write(list.Count);
            foreach (var e in list)
            {
                w.Write(e.Key);
                w.Write(e.Prefab);
                w.Write(e.Container);
                w.Write(e.Pos.x); w.Write(e.Pos.y); w.Write(e.Pos.z);
                w.Write(e.Rot.x); w.Write(e.Rot.y); w.Write(e.Rot.z); w.Write(e.Rot.w);
                w.Write(json ? e.Json : "");
            }
        }

        private static List<Entry> ReadEntries(BinaryReader r, string scene)
        {
            int n = r.ReadInt32();
            var list = new List<Entry>(n);
            for (int i = 0; i < n; i++)
                list.Add(new Entry
                {
                    Key = r.ReadString(), Scene = scene, Prefab = r.ReadString(), Container = r.ReadString(),
                    Pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
                    Rot = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
                    Json = r.ReadString(),
                });
            return list;
        }

        // ---------- Сеть ----------

        public static void Receive(BinaryReader r, Peer from, string fromName)
        {
            byte kind = r.ReadByte();
            string scene = r.ReadString();
            if (Session.Mode == Mode.Host && from != null)
            {
                if (kind == KSeen) HostSeen(fromName, from.Id, scene, ReadEntries(r, scene), from);
                else if (kind == KTaken)
                    HostTaken(fromName, scene, r.ReadString(), new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), r.ReadString());
                else if (kind == KDropped)
                    HostDropped(fromName, scene, r.ReadString(), new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), r.ReadString());
                return;
            }
            if (kind == KFix && Session.Mode == Mode.Client)
            {
                int to = r.ReadByte();
                if (to != Session.LocalId) return;
                var add = ReadEntries(r, scene);
                var remove = ReadEntries(r, scene);
                if (ItemSync.SceneKey() == scene) Fix(add, remove);
            }
        }

        // ---------- Хост ----------

        private static Entry Match(string scene, string prefab, Vector3 pos, string container)
        {
            if (!Known.TryGetValue(scene, out var list)) return null;
            Entry best = null;
            float bd = Near * Near;
            foreach (var e in list)
            {
                if (e.Prefab != prefab || e.Container != container) continue;
                if (container.Length > 0) return e;
                float d = (e.Pos - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        private static void HostSeen(string name, int id, string scene, List<Entry> present, Peer peer = null)
        {
            Load();
            if (!Known.TryGetValue(scene, out var list)) Known[scene] = list = new List<Entry>();
            int added = 0;
            foreach (var e in present)
            {
                if (Match(scene, e.Prefab, e.Pos, e.Container) != null || IsDropped(scene, e)) continue;
                var p = e.Pos;
                e.Key = e.Container.Length > 0 ? $"{e.Prefab}#{e.Container}" : $"{e.Prefab}@{Mathf.RoundToInt(p.x)}|{Mathf.RoundToInt(p.y)}|{Mathf.RoundToInt(p.z)}";
                list.Add(e);
                Append("I", scene, e);
                added++;
            }
            if (added > 0) MelonLogger.Msg($"[квест] {scene}: запомнил квестовых вещей {added} (первым был {name})");
            Taken.TryGetValue(name, out var mine);
            var add = new List<Entry>();
            var remove = new List<Entry>();
            foreach (var e in list) ((mine != null && mine.Contains(e.Key)) ? remove : add).Add(e);
            if (peer == null) { if (ItemSync.SceneKey() == scene) Fix(add, remove); return; }
            if (add.Count == 0 && remove.Count == 0) return;
            var w = Msg.New(MsgType.Quest);
            w.Write(KFix);
            w.Write(scene);
            w.Write((byte)id);
            WriteEntries(w, add, true);
            WriteEntries(w, remove, false);
            peer.Send(Msg.Done(w));
        }

        private static bool IsDropped(string scene, Entry e)
        {
            if (!Dropped.TryGetValue(scene, out var list)) return false;
            foreach (var d in list)
            {
                if (d.Prefab != e.Prefab || d.Container != e.Container) continue;
                if (d.Container.Length > 0 || (d.Pos - e.Pos).sqrMagnitude < 9f) return true;
            }
            return false;
        }

        private static void HostDropped(string name, string scene, string prefab, Vector3 pos, string container)
        {
            Load();
            var e = new Entry { Scene = scene, Prefab = prefab, Pos = pos, Container = container ?? "", Key = "-" };
            if (!Dropped.TryGetValue(scene, out var list)) Dropped[scene] = list = new List<Entry>();
            list.Add(e);
            Append("X", scene, e);
            // Положил взятую вещь обратно туда, где она лежала: она снова его, а не «уже взята». Иначе при следующем
            // заходе Fix убирал её из ящика (или с земли) как взятую, и вещь пропадала совсем.
            var orig = Match(scene, prefab, pos, container ?? "");
            if (orig != null && Taken.TryGetValue(name, out var set) && set.Remove(orig.Key))
            {
                Append("R", scene, orig, name);
                MelonLogger.Msg($"[квест] {name} положил {prefab} обратно в {scene}");
            }
        }

        private static void HostTaken(string name, string scene, string prefab, Vector3 pos, string container)
        {
            Load();
            var e = Match(scene, prefab, pos, container);
            if (e == null) return;   // не из тех, что лежали в локации (например, принёс с собой)
            if (!Taken.TryGetValue(name, out var set)) Taken[name] = set = new HashSet<string>();
            if (!set.Add(e.Key)) return;
            Append("T", scene, e, name);
            MelonLogger.Msg($"[квест] {name} взял {prefab} в {scene}");
        }

        // ---------- У себя: вернуть и убрать ----------

        private static void Fix(List<Entry> add, List<Entry> remove)
        {
            var mine = Present();
            int restored = 0, removed = 0;
            foreach (var e in remove)
            {
                GearItem gi = FindLocal(e);
                if (gi == null) continue;
                var c = gi.m_InsideContainer ? gi.GetComponentInParent<Container>() : null;
                if (c != null) c.DestroyGear(gi); else UnityEngine.Object.Destroy(gi.gameObject);
                removed++;
            }
            foreach (var e in add)
            {
                if (HasLocal(mine, e)) continue;
                try
                {
                    if (e.Container.Length > 0)
                    {
                        var c = WorldObjects.Find<Container>(e.Container);
                        if (c == null || c.m_NotPopulated) continue;   // не обыскан — вещь появится сама
                        var gi = ItemSync.Spawn(e.Prefab, e.Json, null, false, true);
                        if (gi == null) continue;
                        c.AddGear(gi);
                    }
                    else
                    {
                        var gi = ItemSync.Spawn(e.Prefab, e.Json, null, false, false);
                        if (gi == null) continue;
                        gi.transform.SetPositionAndRotation(e.Pos, e.Rot);
                        gi.gameObject.SetActive(true);
                    }
                    restored++;
                }
                catch (Exception ex) { MelonLogger.Warning($"[квест] вернуть {e.Prefab}: {ex.Message}"); }
            }
            if (restored + removed > 0) MelonLogger.Msg($"[квест] {ItemSync.SceneKey()}: вернул квестовых вещей {restored}, убрал уже взятых {removed}");
        }

        private static bool HasLocal(List<Entry> mine, Entry e)
        {
            foreach (var m in mine)
            {
                if (m.Prefab != e.Prefab || m.Container != e.Container) continue;
                if (e.Container.Length > 0 || (m.Pos - e.Pos).sqrMagnitude < Near * Near) return true;
            }
            return false;
        }

        private static GearItem FindLocal(Entry e)
        {
            foreach (var gi in UnityEngine.Object.FindObjectsOfType<GearItem>())
            {
                if (gi == null || gi.m_InPlayerInventory || !ItemSync.IsPersonal(gi) || ItemSync.PrefabName(gi) != e.Prefab) continue;
                if (e.Container.Length > 0)
                {
                    var c = gi.m_InsideContainer ? gi.GetComponentInParent<Container>() : null;
                    if (c != null && WorldObjects.IdOf(c) == e.Container) return gi;
                }
                else if (!gi.m_InsideContainer && (gi.transform.position - e.Pos).sqrMagnitude < Near * Near) return gi;
            }
            return null;
        }

        // ---------- Хранение у хоста ----------

        private static string FileName()
        {
            string dir = PlayerProgress.WorldFolder();
            return dir == null ? null : Path.Combine(dir, "quest_items.txt");
        }

        private static void Load()
        {
            string f = FileName();
            if (f == null || _loadedFor == f) return;
            _loadedFor = f;
            Known.Clear();
            Taken.Clear();
            Dropped.Clear();
            try
            {
                if (!File.Exists(f)) return;
                foreach (var line in File.ReadAllLines(f))
                {
                    var p = line.Split('\t');
                    if (p[0] == "I" && p.Length >= 13)
                    {
                        var e = new Entry
                        {
                            Scene = p[1], Key = p[2], Prefab = p[3], Container = p[4],
                            Pos = new Vector3(F(p[5]), F(p[6]), F(p[7])),
                            Rot = new Quaternion(F(p[8]), F(p[9]), F(p[10]), F(p[11])),
                            Json = Encoding.UTF8.GetString(Convert.FromBase64String(p[12])),
                        };
                        if (!Known.TryGetValue(e.Scene, out var list)) Known[e.Scene] = list = new List<Entry>();
                        list.Add(e);
                    }
                    else if (p[0] == "X" && p.Length >= 13)
                    {
                        var e = new Entry { Scene = p[1], Key = p[2], Prefab = p[3], Container = p[4], Pos = new Vector3(F(p[5]), F(p[6]), F(p[7])) };
                        if (!Dropped.TryGetValue(e.Scene, out var list)) Dropped[e.Scene] = list = new List<Entry>();
                        list.Add(e);
                    }
                    else if (p[0] == "T" && p.Length >= 4)
                    {
                        if (!Taken.TryGetValue(p[1], out var set)) Taken[p[1]] = set = new HashSet<string>();
                        set.Add(p[3]);
                    }
                    else if (p[0] == "R" && p.Length >= 4)
                    {
                        if (Taken.TryGetValue(p[1], out var set)) set.Remove(p[3]);
                    }
                }
            }
            catch (Exception e) { MelonLogger.Warning("[квест] чтение: " + e.Message); }
        }

        private static float F(string s) => float.Parse(s, IC);
        private static string S(float v) => v.ToString("R", IC);

        private static void Append(string kind, string scene, Entry e, string name = null)
        {
            string f = FileName();
            if (f == null) return;
            string line = kind == "T" || kind == "R"
                ? string.Join("\t", kind, name.Replace("\t", " "), scene, e.Key)
                : string.Join("\t", kind, scene, e.Key, e.Prefab, e.Container, S(e.Pos.x), S(e.Pos.y), S(e.Pos.z),
                    S(e.Rot.x), S(e.Rot.y), S(e.Rot.z), S(e.Rot.w), Convert.ToBase64String(Encoding.UTF8.GetBytes(e.Json ?? "")));
            try { File.AppendAllText(f, line + "\n"); } catch (Exception ex) { MelonLogger.Warning("[квест] запись: " + ex.Message); }
        }

        public static void Reset()
        {
            _reportAt = -1f;
            OpenedWith.Clear();
            OpenedPopulated.Clear();
            Dropped.Clear();
            Known.Clear();
            Taken.Clear();
            _loadedFor = null;
        }
    }
}
