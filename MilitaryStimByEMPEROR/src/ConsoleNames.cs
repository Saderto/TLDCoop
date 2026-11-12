using System;
using HarmonyLib;
using Il2Cpp;

namespace MilitaryStimByEMPEROR
{
    /// <summary>Dev console «add GEAR_MilitaryStim» / «add militarystim»: the console only knows names from the game's own catalog.</summary>
    internal static class ConsoleNames
    {
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
                foreach (var k in new[] { "gear_militarystim", "militarystim" })
                    if (!d.ContainsKey(k)) d[k] = Assets.Gear;
            }
            catch (Exception e) { StimMod.Warn("console names: " + e.Message); }
        }
    }
}
