using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace CrankLampByEMPEROR
{
    /// <summary>Russian and English texts (game language decides).</summary>
    internal static class Texts
    {
        static readonly Dictionary<string, (string ru, string en)> T = new()
        {
            ["GAMEPLAY_Flashlight_CrankLamp"] = ("Фонарь-динамо", "Crank Lamp"),
            ["GAMEPLAY_Flashlight_CrankLampDescription"] = (
                "Фонарь с ручной динамо-машиной. Батареек не нужно: покрутите ручку, и он снова светит.",
                "A lamp with a hand-crank dynamo. No batteries needed: turn the crank and it shines again."),
            ["TLDCRANK_Hint"] = ("Удерживайте ПКМ, чтобы крутить ручку", "Hold RMB to turn the crank"),
            ["TLDCRANK_Broken"] = ("Фонарь сломан. Его нужно починить", "The lamp is broken. It needs repair"),
            ["TLDCRANK_Empty"] = ("Фонарь разряжен. Покрутите ручку (ПКМ)", "The lamp is out of charge. Turn the crank (RMB)"),
            ["TLDCRANK_Charge"] = ("Заряд: {0}%", "Charge: {0}%"),
            ["TLDCRANK_PromptToggle"] = ("Включить / выключить", "On / off"),
            ["TLDCRANK_PromptCrank"] = ("Крутить ручку", "Turn the crank"),
        };

        internal static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(Localization), nameof(Localization.Get), new[] { typeof(string) }),
                postfix: new HarmonyMethod(typeof(Texts), nameof(GetPostfix)));
        }

        internal static bool IsRussian()
        {
            try
            {
                var l = Localization.Language;
                return l != null && l.IndexOf("russian", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        internal static string Get(string key) => T.TryGetValue(key, out var v) ? (IsRussian() ? v.ru : v.en) : key;

        static void GetPostfix(string key, ref string __result)
        {
            if (key == null || !T.TryGetValue(key, out var v)) return;
            __result = IsRussian() ? v.ru : v.en;
        }
    }
}
