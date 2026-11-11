using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace TLDCoop.Game
{
    // Пума. Когда в игре включена пума, игра каждый кадр (BaseAiManager.MaybeSetAiToFleeOrDespawn) велит бежать
    // или убирает каждого живого зверя, чьё место появления выключено: так звери уходят с территории, которую заняла пума.
    // У второго игрока места появления выпадают не так, как у хозяина зверей локации, и место «общего» волка у него
    // часто выключено — одолженный ему зверь уже в следующем кадре убегал (и медведь тоже). Для общих зверей эту проверку
    // пропускаем, если место было выключено уже тогда, когда зверь стал у нас настоящим. Выключилось при нас
    // (у хозяина локации пришла пума) — пусть игра делает своё.
    public static partial class AnimalSync
    {
        private static bool _regionCheck;
        private static int _regionBlocked, _regionLogs;

        internal static void BeginRegionCheck()
        {
            if (!Active) return;
            _regionCheck = true;
            foreach (var a in Animals.Values)
            {
                if (!a.Real || a.Ai == null || a.RegionAt == a.RealSince) continue;
                a.RegionAt = a.RealSince;
                a.RegionOnAtReal = RegionOn(a.Ai);
            }
        }

        internal static void EndRegionCheck() => _regionCheck = false;

        private static bool RegionOn(BaseAi ai)
        {
            try
            {
                var sr = ai.m_SpawnRegionParent;
                return sr == null || sr.isActiveAndEnabled;
            }
            catch { return true; }
        }

        // true — не даём игре прогнать или убрать общего зверя из-за выключенного у нас места появления.
        internal static bool RegionCheckBlocks(BaseAi ai, string what)
        {
            if (!_regionCheck || !Active || ai == null) return false;
            try
            {
                if (!ByAi.TryGetValue(ai.Pointer, out var g) || !Animals.TryGetValue(g, out var a) || !a.Real || a.Dead) return false;
                bool leased = !IsAuthority;
                if (!leased && a.RegionOnAtReal) return false;
                _regionBlocked++;
                if (_regionLogs++ < 20)
                    MelonLogger.Msg($"[звери] {a.Prefab}: игра хотела {what} — его место появления у меня выключено "
                        + $"({(leased ? "зверь одолжен мне" : "было выключено ещё до того, как зверь стал моим")}), не дал");
                return true;
            }
            catch { return false; }
        }

        private static string RegionStats => $"не дал прогнать из-за выключенного места появления {_regionBlocked}";
    }

    [HarmonyPatch(typeof(BaseAiManager), nameof(BaseAiManager.MaybeSetAiToFleeOrDespawn))]
    internal static class Patch_RegionFleeCheck
    {
        private static void Prefix()
        {
            AnimalSync.WhyEnter("MaybeSetAiToFleeOrDespawn");
            try { AnimalSync.BeginRegionCheck(); } catch { }
        }

        private static Exception Finalizer(Exception __exception)
        {
            AnimalSync.EndRegionCheck();
            AnimalSync.WhyExit();
            return __exception;
        }
    }
}
