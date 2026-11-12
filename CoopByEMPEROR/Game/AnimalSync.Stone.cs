using System;
using System.Globalization;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Брошенный камень (и другая брошенная вещь) у копии зверя.
    //
    // Камень летит и падает только у бросившего: StoneItem.OnCollisionEnter у него зовёт BaseAi.MaybeFleeFromThrownItem
    // (зверь рядом пугается и убегает — BaseAi.FleeFrom) и BaseAi.MaybeStunnedByStone (попал в кролика — BaseAi.Stun).
    // Если мозг зверя не у бросившего, у него это копия: её FleeFrom/Stun ничего не меняли, и кролик у ведущего
    // продолжал спокойно пастись (11-10: «кролики не пугались кинутого в них камня»). Урон (ApplyDamage) уже
    // уходил ведущему, а испуг и оглушение — нет. Теперь они уходят тем же сообщением попадания (AnimalHit)
    // с особым «источником»: ведущий сам зовёт FleeFrom (от точки, где упала вещь) или Stun.
    public static partial class AnimalSync
    {
        internal const int SourceFleeThrown = -1001, SourceStoneStun = -1002;
        private static int _stoneSent, _stoneApplied;

        // Копия зверя: FleeFrom/Stun у неё — отправить ведущему. true — не наша копия, пусть игра делает своё.
        internal static bool OnCopyScare(BaseAi ai, int kind, Vector3 from)
        {
            if (_applyingHit || ai == null || !ByAi.TryGetValue(ai.Pointer, out long gid) || !Animals.TryGetValue(gid, out var a)) return true;
            if (a.Real || a.Dead) return a.Real;
            int to = a.Controller >= 0 ? a.Controller : _authority;
            if (to < 0 || to == Session.LocalId) return false;
            string at = string.Format(CultureInfo.InvariantCulture, "{0:0.##};{1:0.##};{2:0.##}", from.x, from.y, from.z);
            SendHit(to, gid, 0f, 0f, (DamageSource)kind, at, Session.LocalId, (Session.LocalId << 24) | (++_hitSeq & 0xFFFFFF), 0);
            _stoneSent++;
            MelonLogger.Msg($"[звери] {(kind == SourceStoneStun ? "оглушил камнем" : "напугал брошенной вещью")} {a.Prefab}: отправил {NameOf(to)}");
            return false;
        }

        // Ведущий: пришёл испуг/оглушение от игрока, у которого была копия.
        private static bool ApplyScare(Animal a, int source, string at, string who)
        {
            if (source != SourceFleeThrown && source != SourceStoneStun) return false;
            var p = (at ?? "").Split(';');
            Vector3 pos = a.Ai.transform.position;
            if (p.Length == 3 &&
                float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) pos = new Vector3(x, y, z);
            if (source == SourceStoneStun)
            {
                if (a.Ai.CurrentAIModeAllowStunFromThrownStone()) a.Ai.Stun(pos);
                MelonLogger.Msg($"[звери] {who} оглушил камнем {a.Prefab}: режим теперь {a.Ai.m_CurrentMode}");
            }
            else
            {
                // Бежит от места падения вещи (у игры — от самой вещи: FleeFrom(Transform)).
                a.Ai.FleeFrom(pos);
                MelonLogger.Msg($"[звери] {who} напугал {a.Prefab} брошенной вещью: режим теперь {a.Ai.m_CurrentMode}");
            }
            _stoneApplied++;
            return true;
        }

        internal static string StoneStats => $"камни и брошенные вещи: отправил {_stoneSent}, применил {_stoneApplied}";
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.Stun))]
    internal static class Patch_CopyStun
    {
        private static bool Prefix(BaseAi __instance, Vector3 position)
        {
            try { return AnimalSync.OnCopyScare(__instance, AnimalSync.SourceStoneStun, position); }
            catch (Exception e) { MelonLogger.Warning("[звери] оглушение копии: " + e.Message); return true; }
        }
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.FleeFrom), new[] { typeof(Transform) })]
    internal static class Patch_CopyFleeFrom
    {
        private static bool Prefix(BaseAi __instance, Transform __0)
        {
            try { return AnimalSync.OnCopyScare(__instance, AnimalSync.SourceFleeThrown, __0 != null ? __0.position : __instance.transform.position); }
            catch (Exception e) { MelonLogger.Warning("[звери] испуг копии: " + e.Message); return true; }
        }
    }
}
