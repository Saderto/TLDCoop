using System;
using HarmonyLib;
using Il2Cpp;

namespace ShotgunByEMPEROR
{
    /// <summary>
    /// Wear per shot (0.1.19, user 11-08): 1.5 % of the shotgun's max condition per shot (ep5 value was 2 HP of 500 = 0.4 %).
    /// Set as DegradeOnUse.m_DegradeHP just before the game applies it, so the «Дробовик» skill still lowers it on higher
    /// levels the same way the rifle skill does for the rifle.
    /// </summary>
    internal static class Wear
    {
        internal static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(GearItem), nameof(GearItem.DegradeOnUse)),
                prefix: new HarmonyMethod(typeof(Wear), nameof(Prefix)));
        }

        static void Prefix(GearItem __instance)
        {
            try
            {
                if (__instance == null || __instance.m_GunItem == null || __instance.m_GunItem.m_GunType != GunType.Shotgun) return;
                var d = __instance.m_DegradeOnUse;
                var data = __instance.GearItemData;
                if (d == null || data == null) return;
                float hp = data.MaxHP * ShotgunMod.WearPercent.Value / 100f;
                if (Math.Abs(d.m_DegradeHP - hp) > 0.001f)
                {
                    d.m_DegradeHP = hp;
                    ShotgunMod.Log($"износ за выстрел: {ShotgunMod.WearPercent.Value}% = {hp:0.##} из {data.MaxHP:0} / wear per shot");
                }
            }
            catch (Exception e) { ShotgunMod.Warn("wear: " + e.Message); }
        }
    }
}
