using System;
using HarmonyLib;
using Il2CppTLD.Gameplay;

namespace ShotgunByEMPEROR
{
    /// <summary>
    /// Episode 5 shotgun prefabs carry DisableObjectForGameMode: outside the story it switches the
    /// shotgun, its ammo and casings off (SetActive false) on the first Update. That made the gun
    /// invisible in inspect, impossible to pick up or equip. Keep our items on.
    /// </summary>
    internal static class KeepEnabled
    {
        static bool _logged;
        internal static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(DisableObjectForGameMode), nameof(DisableObjectForGameMode.ShouldDisableForCurrentMode)),
                prefix: new HarmonyMethod(typeof(KeepEnabled), nameof(Prefix)));
        }

        static bool Prefix(DisableObjectForGameMode __instance, ref bool __result)
        {
            try
            {
                var n = __instance.gameObject.name;
                if (n.IndexOf("Shotgun", StringComparison.OrdinalIgnoreCase) < 0) return true;
                __result = false;
                if (!_logged) { _logged = true; ShotgunMod.Log("[diag] выключатель режима обойдён / mode switch bypassed: " + n); }
                return false;
            }
            catch { return true; }
        }
    }
}
