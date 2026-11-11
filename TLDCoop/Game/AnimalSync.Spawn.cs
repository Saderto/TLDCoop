using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Места появления зверей в коопе.
    //
    // Создаёт зверей только игра хозяина животных локации (MaySpawn). Но её места появления решают, создать
    // или убрать зверя, по расстоянию до СВОЕЙ камеры и по тому, видит ли место СВОЯ камера
    // (Utils.DistanceToMainCamera / PositionIsOnscreen / PositionIsInLOSOfPlayer — так в коде игры).
    // На время работы мест появления и менеджера зверей (MultiView) эти проверки учитывают всех игроков
    // локации: расстояние — до ближайшего, «на виду» — если видит хоть кто-то. Поэтому вокруг друга за километр
    // звери появляются так же, как вокруг хозяина, а у него на глазах не возникают и не исчезают.
    public static partial class AnimalSync
    {
        internal static bool MultiView;
        private static int _viewDist, _viewSeen;

        private static string SpawnStats => $"места появления: учёт всех игроков {_viewDist}, «на виду у другого» {_viewSeen}, не дал создать на виду {SpawnVisibility.Blocked}";

        internal static bool Suppress()
        {
            if (MaySpawn) return false;
            _blockedSpawns++;
            return true;
        }

        // Расстояние от точки до ближайшего игрока локации, кроме того, у кого сейчас камера.
        internal static float NearestOther(Vector3 pos)
        {
            float best = float.MaxValue;
            foreach (var p in HereNow())
            {
                if (p.id == Session.LocalId) continue;
                if (Borrowing && p.id == BorrowId) continue;   // камера сейчас у него
                best = Mathf.Min(best, Vector3.Distance(p.pos + Vector3.up * 1.6f, pos));
            }
            // Во время подмены наш настоящий игрок стоит не у камеры.
            if (Borrowing && RealLocalPos(out var me)) best = Mathf.Min(best, Vector3.Distance(me + Vector3.up * 1.6f, pos));
            return best;
        }

        // Видит ли точку кто-то из других игроков (по направлению взгляда, без камеры их игры).
        internal static bool SeenByOther(Vector3 pos, float range, bool lineOfSight)
        {
            foreach (var p in HereNow())
            {
                if (p.id == Session.LocalId || (Borrowing && p.id == BorrowId)) continue;
                if (Looks(p.pos, p.yaw, p.id, pos, range, lineOfSight)) return true;
            }
            if (Borrowing && RealLocalPos(out var me) && _bPlayer != null)
            {
                float yaw = _bCamRot.eulerAngles.y;
                if (Looks(me, yaw, Session.LocalId, pos, range, lineOfSight)) return true;
            }
            return false;
        }

        private static bool Looks(Vector3 at, float yaw, int id, Vector3 pos, float range, bool lineOfSight)
        {
            var eye = at + Vector3.up * 1.6f;
            var d = pos - eye;
            float dist = d.magnitude;
            if (dist > range) return false;
            var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            if (dist > 3f && Vector3.Angle(fwd, new Vector3(d.x, 0f, d.z)) > 60f) return false;
            if (!lineOfSight) return true;
            try
            {
                // Что-то заслоняет точку дальше чем в полутора метрах от неё — не видно.
                if (Physics.Raycast(eye, d / Mathf.Max(dist, 0.01f), out var hit, dist, ~0, QueryTriggerInteraction.Ignore) && hit.distance < dist - 1.5f) return false;
            }
            catch { }
            return true;
        }

        internal static void ViewDistance(Vector3 pos, ref float result)
        {
            if (!MultiView) return;
            float d = NearestOther(pos);
            if (d < result) { result = d; _viewDist++; }
        }

        internal static void ViewOnscreen(Vector3 pos, ref bool result, bool lineOfSight)
        {
            if (!MultiView || result) return;
            if (SeenByOther(pos, lineOfSight ? 150f : 250f, lineOfSight)) { result = true; _viewSeen++; }
        }

        internal static void BeginManager()
        {
            MultiView = Active && HereNow().Count >= 2;
        }

        // ---------- Места появления текущей локации: номер ↔ объект ----------

        private static readonly Dictionary<string, SpawnRegion> RegionIndex = new();
        private static readonly Dictionary<int, string> RegionByHash = new();

        public static string KeyOf(SpawnRegion sr) => sr == null ? null : WorldObjects.IdOf(sr);

        public static string KeyOf(BaseAi ai)
        {
            try { return KeyOf(ai?.m_SpawnRegionParent); } catch { return null; }
        }

        // Стабильный короткий номер места для сообщений о звере (4 байта вместо строки).
        public static int Hash(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in key) { h ^= c; h *= 16777619; }
                return (int)(h == 0 ? 1 : h);
            }
        }

        private static void IndexRegions()
        {
            if (RegionIndex.Count > 0) return;
            RegionByHash.Clear();
            try
            {
                foreach (var sr in UnityEngine.Object.FindObjectsOfType<SpawnRegion>(true))
                {
                    string k = KeyOf(sr);
                    if (string.IsNullOrEmpty(k)) continue;
                    RegionIndex[k] = sr;
                    RegionByHash[Hash(k)] = k;
                }
            }
            catch (Exception e) { MelonLogger.Warning("[места зверей] " + e.Message); }
        }

        private static SpawnRegion FindRegion(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            IndexRegions();
            return RegionIndex.TryGetValue(key, out var sr) && sr != null ? sr : null;
        }

        private static string KeyOfHash(int hash)
        {
            if (hash == 0) return null;
            IndexRegions();
            return RegionByHash.TryGetValue(hash, out var k) ? k : null;
        }
    }

    // Игра проверяет, не на виду ли место нового зверя, только по своей камере. Добавляем остальных игроков:
    // ближе 60 м к любому из них или в поле зрения до 200 м — зверя там не создаём.
    public static class SpawnVisibility
    {
        public static int Blocked;
        public static bool Off;   // пока мы сами создаём копию зверя, пришедшего от другого игрока (он уже на виду)

        public static bool SeenByOthers(Vector3 pos)
        {
            if (Off || Session.Mode == Mode.Off || !Session.InSharedWorld) return false;
            string scene = GameApi.Scene();
            float now = Time.unscaledTime;
            foreach (var pl in Session.Players.Values)
            {
                if (pl.Id == Session.LocalId || !pl.InWorld || pl.Scene != scene || now - pl.LastState > 6f) continue;
                var eye = new Vector3(pl.X, pl.Y + 1.6f, pl.Z);
                var d = pos - eye;
                float dist = d.magnitude;
                if (dist < 60f) return true;
                if (dist > 200f) continue;
                var fwd = Quaternion.Euler(0f, pl.Yaw, 0f) * Vector3.forward;
                if (Vector3.Angle(fwd, new Vector3(d.x, 0f, d.z)) < 55f) return true;
            }
            return false;
        }
    }

    // ---------- Патчи ----------

    [HarmonyPatch(typeof(SpawnRegionManager), nameof(SpawnRegionManager.Update))]
    internal static class Patch_SpawnManagerView
    {
        private static void Prefix()
        {
            try { AnimalSync.ApplyLateRegions(); } catch { }
            AnimalSync.BeginManager();
        }
        private static Exception Finalizer(Exception __exception) { AnimalSync.MultiView = false; return __exception; }
    }

    [HarmonyPatch(typeof(BaseAiManager), nameof(BaseAiManager.Update))]
    internal static class Patch_AiManagerView
    {
        private static void Prefix()
        {
            AnimalSync.PurgeDeadAi();
            AnimalSync.PurgeHowl();
            AnimalSync.BeginManager();
        }
        private static Exception Finalizer(Exception __exception) { AnimalSync.MultiView = false; return __exception; }
    }

    // Повтор в списке зверей игры не вносим (AnimalSync.AllowAiAdd).
    [HarmonyPatch(typeof(BaseAiManager), nameof(BaseAiManager.Add))]
    internal static class Patch_AiAddOnce
    {
        private static bool Prefix(BaseAi bai) => AnimalSync.AllowAiAdd(bai);
    }

    [HarmonyPatch(typeof(Utils), nameof(Utils.DistanceToMainCamera))]
    internal static class Patch_ViewDistance
    {
        private static void Postfix(Vector3 pos, ref float __result) => AnimalSync.ViewDistance(pos, ref __result);
    }

    [HarmonyPatch(typeof(Utils), nameof(Utils.PositionIsOnscreen), new[] { typeof(Vector3) })]
    internal static class Patch_ViewOnscreen
    {
        private static void Postfix(Vector3 pos, ref bool __result) => AnimalSync.ViewOnscreen(pos, ref __result, false);
    }

    [HarmonyPatch(typeof(Utils), nameof(Utils.PositionIsOnscreen), new[] { typeof(Vector3), typeof(float) })]
    internal static class Patch_ViewOnscreen2
    {
        private static void Postfix(Vector3 pos, ref bool __result) => AnimalSync.ViewOnscreen(pos, ref __result, false);
    }

    [HarmonyPatch(typeof(Utils), nameof(Utils.PositionIsInLOSOfPlayer))]
    internal static class Patch_ViewLos
    {
        private static void Postfix(Vector3 pos, ref bool __result) => AnimalSync.ViewOnscreen(pos, ref __result, true);
    }

    // Не хозяин животных локации: его места появления зверей не создают и не убирают (у него только копии).
    [HarmonyPatch(typeof(SpawnRegion), nameof(SpawnRegion.Spawn))]
    internal static class Patch_SpawnRegionSpawn
    {
        private static bool Prefix(ref bool __result)
        {
            if (!AnimalSync.Suppress()) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(SpawnRegion), nameof(SpawnRegion.AddActiveSpawns))]
    internal static class Patch_SpawnRegionAdd
    {
        private static bool Prefix() => !AnimalSync.Suppress();
    }

    [HarmonyPatch(typeof(SpawnRegion), nameof(SpawnRegion.RemoveActiveSpawns))]
    internal static class Patch_SpawnRegionRemove
    {
        private static bool Prefix() => !AnimalSync.Suppress();
    }

    [HarmonyPatch(typeof(SpawnRegion), nameof(SpawnRegion.Respawn))]
    internal static class Patch_SpawnRegionRespawn
    {
        private static bool Prefix() => !AnimalSync.Suppress();
    }

    [HarmonyPatch(typeof(SpawnRegion), nameof(SpawnRegion.SpawnWithSavedPositions))]
    internal static class Patch_SpawnRegionSaved
    {
        private static bool Prefix() => !AnimalSync.Suppress();
    }

    [HarmonyPatch(typeof(SpawnRegion), nameof(SpawnRegion.SpawnWithRandomPositions))]
    internal static class Patch_SpawnRegionRandom
    {
        private static bool Prefix() => !AnimalSync.Suppress();
    }

    [HarmonyPatch(typeof(SpawnRegion), nameof(SpawnRegion.MaybeSpawnPendingSerializedRespawn))]
    internal static class Patch_SpawnRegionPending
    {
        private static bool Prefix(ref BaseAi __result)
        {
            if (!AnimalSync.Suppress()) return true;
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(SpawnRegion), nameof(SpawnRegion.InstantiateAndPlaceSpawn))]
    internal static class Patch_SpawnRegionPlace
    {
        private static bool Prefix(ref BaseAi __result)
        {
            if (!AnimalSync.Suppress()) return true;
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(SpawnRegion), nameof(SpawnRegion.SpawnPositionOnScreenTooClose))]
    internal static class Patch_SpawnOnScreen
    {
        private static void Postfix(Vector3 spawnPos, ref bool __result)
        {
            if (!__result && SpawnVisibility.SeenByOthers(spawnPos)) { __result = true; SpawnVisibility.Blocked++; }
        }
    }

    [HarmonyPatch(typeof(SpawnRegion), nameof(SpawnRegion.SpawnPositionTooCloseToCamera))]
    internal static class Patch_SpawnNearCamera
    {
        private static void Postfix(Vector3 pos, ref bool __result)
        {
            if (!__result && SpawnVisibility.SeenByOthers(pos)) { __result = true; SpawnVisibility.Blocked++; }
        }
    }

    [HarmonyPatch(typeof(SpawnRegion), nameof(SpawnRegion.PositionValidForSpawn))]
    internal static class Patch_SpawnValid
    {
        private static void Postfix(Vector3 pos, ref bool __result)
        {
            if (__result && SpawnVisibility.SeenByOthers(pos)) { __result = false; SpawnVisibility.Blocked++; }
        }
    }
}
