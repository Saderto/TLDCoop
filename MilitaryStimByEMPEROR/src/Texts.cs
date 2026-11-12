using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace MilitaryStimByEMPEROR
{
    /// <summary>Russian and English texts (game language decides), word for word from episode 5.</summary>
    internal static class Texts
    {
        static readonly Dictionary<string, (string ru, string en)> T = new()
        {
            ["GAMEPLAY_MilitaryStim"] = ("Военный стимулятор", "Military Stim"),
            ["GAMEPLAY_MilitaryStimDescription"] = (
                "Экспериментальный военный стимулятор, испытание №2.003\nВнимание: используйте только в экстренных ситуациях для восстановления боеспособности.",
                "Experimental Military Stim - Test # 2.003\nCaution: Use only in extreme circumstances to restore Condition."),
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

        static void GetPostfix(string key, ref string __result)
        {
            if (key == null || !T.TryGetValue(key, out var v)) return;
            __result = IsRussian() ? v.ru : v.en;
        }
    }
}
