using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Палки, камни, ветки у деревьев и скал (RadialObjectSpawner). Игра раскладывает их сама,
    // когда игрок подходит близко, и у каждого случайно. Здесь раскладку каждого такого места
    // решает тот, кто оказался рядом первым, а остальные берут её и своё не раскладывают:
    //  - друг: прежде чем игра разложит палки, спрашивает хоста (SpawnClaim). Если место ещё
    //    никто не раскладывал, хост разрешает (SpawnGrant), друг раскладывает и рассылает
    //    раскладку. Если раскладка уже есть, хост присылает её, и друг ставит её у себя;
    //  - хост: решает сам по тем же правилам;
    //  - хост запоминает раскладку каждого места (и хранит её в pending.bin вместе с миром),
    //    поэтому её получит и тот, кто придёт туда через день, когда первого там уже нет;
    //  - подобрали палку — раскладка места рассылается заново, уже без неё;
    //  - когда пройдёт время, за которое игра выкладывает палки снова (m_MinRespawnTimeGameHours),
    //    новую раскладку опять делает тот, кто первым окажется рядом;
    //  - мелочь, которую море выносит на берег (спавнеры внутри BeachcombingSpawner), раскладывается
    //    заново после каждой новой бури у хоста (номер бури BeachcombingManager).
    public static class RadialSync
    {
        private class Entry
        {
            public byte[] Raw;       // последнее состояние (сообщение ObjectState), null — ещё не прислано
            public int Owner;        // кто раскладывал
            public float Hours;      // когда (часы игры хоста)
            public float GrantedAt;  // когда разрешили (реальное время), чтобы не ждать ушедшего вечно
            public int Blizzard = int.MinValue;   // номер бури у хоста, когда раскладывали (для берега)
        }

        // Хост: раскладки по ключу «сцена|id».
        private static readonly Dictionary<string, Entry> Layouts = new();
        // Хост: свои попытки, которые ждут применения чужой раскладки (применяем в Tick, не внутри раскладки игры).
        private static readonly List<byte[]> ApplyQueue = new();
        // Друг: разрешённые ему места и когда он последний раз спрашивал.
        private static readonly HashSet<string> Granted = new();
        private static readonly Dictionary<string, float> Asked = new();
        // Все: места, где мы только что разложили или подобрали, — рассылаем через 1,5 с тишины.
        private static readonly Dictionary<IntPtr, RadialObjectSpawner> Dirty = new();
        private static float _sendAt;

        private static string Key(string scene, string id) => scene + "|" + id;

        private static float HostHours()
        {
            try { return GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused(); } catch { return 0f; }
        }

        private static int BlizzardNow()
        {
            try { return Il2CppTLD.Gameplay.BeachcombingManager.m_CurrentState?.m_CurrentBlizzardID ?? 0; } catch { return 0; }
        }

        private static bool IsBeach(RadialObjectSpawner sp)
        {
            try { return sp != null && sp.GetComponentInParent<Il2CppTLD.Gameplay.BeachcombingSpawner>(true) != null; } catch { return false; }
        }

        // Берег: после новой бури раскладка устарела.
        private static bool NewBlizzard(Entry e, bool beach)
        {
            if (!beach || e.Raw == null) return false;
            // Номер бури неизвестен (pending.bin прежней версии): буря могла пройти, пока хост был выключен, — раскладываем заново один раз.
            if (e.Blizzard == int.MinValue) return true;
            return BlizzardNow() > e.Blizzard;
        }

        internal static bool HasPending(RadialObjectSpawner sp)
        {
            try { return sp.m_PendingSpawns != null && sp.m_PendingSpawns.Count > 0; } catch { return false; }
        }

        private static float MinRespawn(RadialObjectSpawner sp)
        {
            try { return sp.m_MinRespawnTimeGameHours; } catch { return 0f; }
        }

        // ---------- Игра собирается выложить палку ----------

        // false — не выкладывать (ждём решения хоста или чужую раскладку).
        public static bool AllowSpawn(RadialObjectSpawner sp)
        {
            if (sp == null || WorldObjects.Applying || !GameApi.InGame()) return true;
            // Раскладка, полученная от другого (Deserialize), тоже выкладывается через эту же функцию:
            // её палки ждут в m_PendingSpawns, их не держим.
            if (HasPending(sp)) return true;
            if (Session.Mode == Mode.Client && Session.InHostWorld) return ClientAllow(sp);
            if (Session.Mode == Mode.Host) return HostAllow(sp);
            return true;
        }

        private static bool ClientAllow(RadialObjectSpawner sp)
        {
            string scene = ItemSync.SceneKey(), id = WorldObjects.IdOf(sp);
            string key = Key(scene, id);
            if (Granted.Contains(key)) return true;
            float now = Time.unscaledTime;
            if (!Asked.TryGetValue(key, out float t) || now - t > 5f)
            {
                Asked[key] = now;
                var w = Msg.New(MsgType.SpawnClaim);
                w.Write(scene);
                w.Write(id);
                w.Write(MinRespawn(sp));
                w.Write(IsBeach(sp));
                Session.SendGameEvent(Msg.Done(w));
            }
            return false;
        }

        private static bool HostAllow(RadialObjectSpawner sp)
        {
            string id = WorldObjects.IdOf(sp);
            string key = Key(ItemSync.SceneKey(), id);
            if (!Layouts.TryGetValue(key, out var e) || e.Owner == 0) { Claim(key, 0); return true; }
            if (Due(e, MinRespawn(sp)) || NewBlizzard(e, IsBeach(sp))) { Claim(key, 0); return true; }
            if (e.Raw != null) { if (!ApplyQueue.Contains(e.Raw)) ApplyQueue.Add(e.Raw); return false; }
            if (Stale(e)) { Claim(key, 0); return true; }
            return false;   // друг уже раскладывает, его раскладка вот-вот придёт
        }

        private static bool Due(Entry e, float minRespawn) => e.Raw != null && minRespawn > 0f && HostHours() - e.Hours >= minRespawn;

        private static bool Stale(Entry e) =>
            e.Raw == null && ((e.Owner != 0 && !Session.Players.ContainsKey(e.Owner)) || Time.unscaledTime - e.GrantedAt > 15f);

        private static void Claim(string key, int owner)
        {
            if (!Layouts.TryGetValue(key, out var e)) Layouts[key] = e = new Entry();
            // Своё место, раскладываем дальше. Раскладываем сейчас — значит, раскладка уже после текущей бури
            // (иначе после бури друг получал разрешение и заменял новую раскладку хоста своей).
            if (e.Owner == owner && e.Raw != null && owner == 0) { e.Blizzard = BlizzardNow(); return; }
            e.Owner = owner;
            e.Hours = HostHours();
            e.Blizzard = BlizzardNow();
            e.GrantedAt = Time.unscaledTime;
        }

        // ---------- Хост: сообщения друзей ----------

        public static void HostClaim(Peer peer, BinaryReader r)
        {
            string scene = r.ReadString(), id = r.ReadString();
            float minRespawn = r.ReadSingle();
            bool beach = r.BaseStream.Position < r.BaseStream.Length && r.ReadBoolean();
            string key = Key(scene, id);
            if (!Layouts.TryGetValue(key, out var e))
            {
                // Хост стоит в этом месте и у него уже разложено (ещё до того, как друг подключился).
                if (HostOwn(key, scene, id) != null) return;
                Grant(peer, key, scene, id);
                return;
            }
            if (e.Owner == peer.Id && e.Raw == null) { Grant(peer, key, scene, id); return; }
            if (Due(e, minRespawn) || NewBlizzard(e, beach) || Stale(e)) { Grant(peer, key, scene, id); return; }
            if (e.Raw != null) peer.Send(e.Raw);
            // иначе раскладывает другой, его раскладка придёт всем
        }

        private static void Grant(Peer peer, string key, string scene, string id)
        {
            Claim(key, peer.Id);
            var w = Msg.New(MsgType.SpawnGrant);
            w.Write(scene);
            w.Write(id);
            peer.Send(Msg.Done(w));
        }

        // Друг прислал раскладку места. Берём её от того, кому разрешено, а от остальных —
        // только если место уже разложено (значит, это подобранная палка, а не новая раскладка).
        // Раскладка без разрешения бывает, когда игра друга выложила палки прямо при загрузке мира, ещё до вопроса
        // хосту. Тогда у друга палки лежат по-своему: если у хоста это место уже разложено (он стоит рядом),
        // хост рассылает свою раскладку, и у друга она заменяет его. Если ни у кого раскладки нет, берём эту.
        public static bool HostAccept(Peer peer, byte[] raw)
        {
            var (scene, _, id) = WorldObjects.Header(raw);
            string key = Key(scene, id);
            Layouts.TryGetValue(key, out var e);
            if (e != null && (e.Owner == peer.Id || e.Raw != null)) { e.Raw = raw; return true; }
            if (HostOwn(key, scene, id) != null)
            {
                MelonLogger.Msg($"[палки] {scene} {id}: раскладка от игрока {peer.Id} без разрешения, а у меня место разложено — отправил свою");
                return false;
            }
            if (e != null && e.Raw == null && !Stale(e))
            {
                MelonLogger.Msg($"[палки] {scene} {id}: раскладка от игрока {peer.Id} без разрешения, место раскладывает другой — не беру");
                return false;
            }
            Layouts[key] = new Entry { Raw = raw, Owner = peer.Id, Hours = HostHours(), GrantedAt = Time.unscaledTime, Blizzard = BlizzardNow() };
            MelonLogger.Msg($"[палки] {scene} {id}: раскладка от игрока {peer.Id} без разрешения, но место ничьё — беру");
            return true;
        }

        // Хост стоит в этом месте и у него палки уже выложены: запоминаем его раскладку и рассылаем всем.
        private static byte[] HostOwn(string key, string scene, string id)
        {
            if (!GameApi.InGame() || !ItemSync.Settled || scene != ItemSync.SceneKey()) return null;
            var sp = WorldObjects.FindSpawner(id);
            if (sp == null || !sp.HasSpawned()) return null;
            var raw = WorldObjects.Send(WorldObjects.Kind.Spawner, sp, sp.Serialize(), id);
            if (raw != null) Layouts[key] = new Entry { Raw = raw, Owner = 0, Hours = HostHours(), GrantedAt = Time.unscaledTime, Blizzard = BlizzardNow() };
            return raw;
        }

        // ---------- Друг: ответы хоста ----------

        public static void ClientGrant(BinaryReader r)
        {
            string key = Key(r.ReadString(), r.ReadString());
            Granted.Add(key);
            Asked.Remove(key);
        }

        // Раскладка места применена (у кого угодно): своё разрешение на него больше не действует.
        public static void OnApplied(string scene, string id)
        {
            Granted.Remove(Key(scene, id));
            // Asked не сбрасываем: иначе игра тут же спросит снова и хост снова пришлёт то же самое.
        }

        // ---------- Рассылка своих раскладок ----------

        public static void MarkDirty(RadialObjectSpawner sp)
        {
            if (Session.Mode == Mode.Off || WorldObjects.Applying || ItemSync.Applying || sp == null) return;
            Dirty[sp.Pointer] = sp;
            _sendAt = Time.unscaledTime + 1.5f;   // палки выкладываются по одной, ждём, пока закончит
        }

        public static void Tick()
        {
            if (ApplyQueue.Count > 0)
            {
                var list = new List<byte[]>(ApplyQueue);
                ApplyQueue.Clear();
                foreach (var raw in list) WorldObjects.ApplyNow(raw);
            }
            if (_sendAt == 0f || Time.unscaledTime < _sendAt) return;
            _sendAt = 0f;
            foreach (var sp in Dirty.Values)
            {
                try
                {
                    if (sp == null) continue;
                    string id = WorldObjects.IdOf(sp);
                    var raw = WorldObjects.Send(WorldObjects.Kind.Spawner, sp, sp.Serialize(), id);
                    if (raw != null && Session.Mode == Mode.Host)
                    {
                        string key = Key(ItemSync.SceneKey(), id);
                        if (!Layouts.TryGetValue(key, out var e)) Layouts[key] = e = new Entry { Owner = 0, Hours = HostHours(), Blizzard = BlizzardNow() };
                        e.Raw = raw;
                    }
                }
                catch (Exception e) { MelonLogger.Warning("[палки] " + e.Message); }
            }
            Dirty.Clear();
        }

        // Для лога: ближайший спавнер с тем же именем (id вида «@имя|x|y|z», координаты ×10).
        public static string Nearest(string id)
        {
            try
            {
                var parts = id.TrimStart('@').Split('|');
                if (parts.Length < 4) return "id без места";
                var p = new Vector3(int.Parse(parts[1]) / 10f, int.Parse(parts[2]) / 10f, int.Parse(parts[3]) / 10f);
                RadialObjectSpawner best = null; float bestD = float.MaxValue; int same = 0;
                foreach (var sp in UnityEngine.Object.FindObjectsOfType<RadialObjectSpawner>(true))
                {
                    if (sp == null || sp.gameObject.name != parts[0]) continue;
                    same++;
                    float d = Vector3.Distance(sp.transform.position, p);
                    if (d < bestD) { bestD = d; best = sp; }
                }
                if (best == null) return "спавнеров с таким именем нет";
                return $"с таким именем {same}, ближайший в {bestD:0.00} м ({WorldObjects.IdOf(best)}, {(best.gameObject.activeInHierarchy ? "включён" : "выключен")})";
            }
            catch (Exception e) { return e.Message; }
        }

        // Все вещи, которые сейчас разложены спавнерами (указатели GameObject).
        public static HashSet<IntPtr> SpawnedObjects()
        {
            var set = new HashSet<IntPtr>();
            try
            {
                foreach (var sp in UnityEngine.Object.FindObjectsOfType<RadialObjectSpawner>())
                {
                    var list = sp?.m_Spawns;
                    if (list == null) continue;
                    for (int i = 0; i < list.Count; i++) if (list[i] != null) set.Add(list[i].Pointer);
                }
            }
            catch (Exception e) { MelonLogger.Warning("[палки] список: " + e.Message); }
            return set;
        }

        // ---------- Хранение у хоста ----------

        public static int StoredCount { get { int n = 0; foreach (var e in Layouts.Values) if (e.Raw != null) n++; return n; } }

        public static void Write(BinaryWriter w)
        {
            int n = 0;
            foreach (var e in Layouts.Values) if (e.Raw != null) n++;
            w.Write(n);
            foreach (var kv in Layouts)
            {
                if (kv.Value.Raw == null) continue;
                w.Write(kv.Key);
                w.Write(kv.Value.Owner);
                w.Write(kv.Value.Hours);
                w.Write(kv.Value.Raw.Length);
                w.Write(kv.Value.Raw);
            }
            // Номера бурь — отдельным блоком после записей (с меткой): прежняя версия файла читается как раньше.
            w.Write(BlizzardMark);
            foreach (var e in Layouts.Values) if (e.Raw != null) w.Write(e.Blizzard);
        }

        private const int BlizzardMark = 0x424C5A31;

        public static void Read(BinaryReader r)
        {
            Layouts.Clear();
            int n = r.ReadInt32();
            var read = new List<Entry>(n);
            for (int i = 0; i < n; i++)
            {
                string key = r.ReadString();
                var e = new Entry { Owner = r.ReadInt32(), Hours = r.ReadSingle() };
                e.Raw = r.ReadBytes(r.ReadInt32());
                // Номера игроков после перезапуска другие: место считается разложенным, но ничьим.
                if (e.Owner != 0) e.Owner = -1;
                Layouts[key] = e;
                read.Add(e);
            }
            // Номера бурь (если файл их содержит; в старом файле дальше идёт другое — возвращаемся назад).
            if (r.BaseStream.Length - r.BaseStream.Position >= 4)
            {
                if (r.ReadInt32() == BlizzardMark && r.BaseStream.Length - r.BaseStream.Position >= 4L * n)
                    foreach (var e in read) e.Blizzard = r.ReadInt32();
                else r.BaseStream.Position -= 4;
            }
            MelonLogger.Msg($"[палки] загружено раскладок: {n}");
        }

        public static void Reset()
        {
            Layouts.Clear();
            ApplyQueue.Clear();
            Granted.Clear();
            Asked.Clear();
            Dirty.Clear();
            _sendAt = 0f;
        }
    }

    [HarmonyPatch(typeof(RadialObjectSpawner), nameof(RadialObjectSpawner.SpawnObjectAttempt))]
    internal static class Patch_RadialSpawn
    {
        // __state: выкладываются палки из чужой раскладки — рассылать потом нечего.
        private static bool Prefix(RadialObjectSpawner __instance, ref bool __result, out bool __state)
        {
            __state = __instance != null && RadialSync.HasPending(__instance);
            bool allow = true;
            try { allow = RadialSync.AllowSpawn(__instance); } catch (Exception e) { MelonLogger.Warning("[палки] " + e.Message); }
            if (!allow) __result = false;
            return allow;
        }

        private static void Postfix(RadialObjectSpawner __instance, bool __result, bool __state)
        {
            if (__result && !__state) RadialSync.MarkDirty(__instance);
        }
    }

    [HarmonyPatch(typeof(RadialObjectSpawner), nameof(RadialObjectSpawner.RemoveFromSpawns))]
    internal static class Patch_RadialRemove
    {
        private static void Postfix(RadialObjectSpawner __instance) => RadialSync.MarkDirty(__instance);
    }
}
