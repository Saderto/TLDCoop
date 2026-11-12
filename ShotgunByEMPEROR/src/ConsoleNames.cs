using System;
using HarmonyLib;
using Il2Cpp;

namespace ShotgunByEMPEROR
{
    /// <summary>
    /// The dev console «add» looks gear up in ConsoleManager.m_SearchStringToGearNames, a list the game builds
    /// once from its own catalog. Our items are not in it, so «add GEAR_Shotgun» silently did nothing.
    /// </summary>
    internal static class ConsoleNames
    {
        static readonly string[] Gear = { "GEAR_Shotgun", "GEAR_ShotgunAmmoBox", "GEAR_ShotgunAmmoSingle", "GEAR_ShotgunAmmoCasing" };

        internal static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(ConsoleManager), "StoreAutoCompleteGearNames"),
                postfix: new HarmonyMethod(typeof(ConsoleNames), nameof(Fill)));
            Fill();
        }

        internal static void Fill()
        {
            try
            {
                var d = ConsoleManager.m_SearchStringToGearNames;
                if (d == null) return;
                int added = 0;
                foreach (var g in Gear)
                {
                    foreach (var k in new[] { g.ToLowerInvariant(), g.Substring(5).ToLowerInvariant() })
                        if (!d.ContainsKey(k)) { d[k] = g; added++; }
                }
                if (added > 0) ShotgunMod.Log($"консоль: добавлено имён {added} / console names added");
            }
            catch (Exception e) { ShotgunMod.Warn("console names: " + e.Message); }
        }
    }
}
