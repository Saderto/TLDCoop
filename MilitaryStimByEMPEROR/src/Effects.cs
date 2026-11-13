using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MilitaryStimByEMPEROR
{
    /// <summary>
    /// In the episode 5 data the military stim has no stimulated period (0 hours, pulse 0), so the game shows no heartbeat,
    /// no blurred screen edges and no «Fatigue reduced» / «Energy boost» buffs. When ours is injected it gets the vanilla
    /// stim's pulse and a stimulated period (StimulatedMinutes); its ending fatigue/stamina penalty stays 0, so no crash after.
    /// </summary>
    internal static class Effects
    {
        static AsyncOperationHandle<GameObject> _load;
        static bool _loading, _have;
        static float _pulseStart, _pulseEnd;

        internal static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(EmergencyStim), nameof(EmergencyStim.ApplyEmergencyStim)),
                prefix: new HarmonyMethod(typeof(Effects), nameof(Prefix)));
            try { _load = Addressables.LoadAssetAsync<GameObject>("GEAR_EmergencyStim"); _loading = true; }
            catch (Exception e) { StimMod.Warn("vanilla stim load: " + e.Message); }
        }

        // called every frame until the vanilla prefab is in (one async load, no frame stall)
        internal static void Tick()
        {
            if (!_loading || !_load.IsDone) return;
            _loading = false;
            try
            {
                var go = _load.Result;
                var item = go != null ? go.GetComponent<EmergencyStimItem>() : null;
                if (item != null) Take(item);
                else StimMod.Warn("vanilla stim has no EmergencyStimItem");
            }
            catch (Exception e) { StimMod.Warn("vanilla stim read: " + e.Message); }
        }

        static void Take(EmergencyStimItem v)
        {
            if (v.m_PulseFrequencyStart <= 0f && v.m_PulseFrequencyEnd <= 0f) return;
            _pulseStart = v.m_PulseFrequencyStart; _pulseEnd = v.m_PulseFrequencyEnd; _have = true;
            StimMod.Log($"пульс обычного шприца / vanilla pulse {_pulseStart}..{_pulseEnd}");
        }

        static void Prefix(EmergencyStimItem emergencyStim)
        {
            try
            {
                if (emergencyStim == null) return;
                var gi = emergencyStim.m_GearItem != null ? emergencyStim.m_GearItem : emergencyStim.GetComponent<GearItem>();
                string name = gi != null ? gi.name : emergencyStim.name;
                if (name == null || !name.Contains("GEAR_MilitaryStim"))
                {
                    if (!_have) Take(emergencyStim);   // a vanilla stim used first: take its pulse from it
                    return;
                }
                float minutes = StimMod.StimulatedMinutes.Value;
                if (minutes <= 0f) return;
                emergencyStim.m_HoursStimulatedWhenInjected = minutes / 60f;
                if (_have) { emergencyStim.m_PulseFrequencyStart = _pulseStart; emergencyStim.m_PulseFrequencyEnd = _pulseEnd; }
                emergencyStim.m_FatigueEndingIncrease = 0f;
                emergencyStim.m_StaminaEndingDecrease = 0f;
                StimMod.Log($"военный стимулятор: {minutes} мин, пульс {(_have ? "как у обычного" : "нет данных")} / military stim stimulated");
            }
            catch (Exception e) { StimMod.Warn("stim effects: " + e.Message); }
        }
    }
}
