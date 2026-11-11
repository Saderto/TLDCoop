using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace TLDShotgun
{
    /// <summary>Russian and English texts for the shotgun and its ammo (game language decides).</summary>
    internal static class Texts
    {
        // key -> (ru, en). Descriptions are always ours (they mention the shot); names only fill gaps.
        static readonly Dictionary<string, (string ru, string en, bool force)> T = new()
        {
            ["GAMEPLAY_Shotgun"] = ("Дробовик", "Shotgun", false),
            ["GAMEPLAY_ShotgunDescription"] = (
                "Старое одноствольное переломное ружьё. Бьёт дробью: в упор валит даже крупного зверя, но уже за тридцать шагов дробь теряет силу. После каждого выстрела ружьё нужно переломить и зарядить заново.",
                "An old single-barrel break-action shotgun. It fires shot: deadly up close, even against big game, but the pellets lose their bite past thirty paces. It has to be broken open and reloaded after every shot.", true),
            ["GAMEPLAY_ShotgunAmmunition"] = ("Патроны для дробовика", "Shotgun Shells", false),
            ["GAMEPLAY_ShotgunAmmunitionDescription"] = (
                "Коробка дробовых патронов. Подходят только к дробовику.",
                "A box of shotgun shells. They only fit the shotgun.", true),
            ["GAMEPLAY_ShotgunRound"] = ("Патрон для дробовика", "Shotgun Shell", false),
            ["GAMEPLAY_ShotgunRoundDescription"] = (
                "Дробовой патрон. Можно разобрать на порох и гильзу.",
                "A shotgun shell. Can be broken down into gunpowder and a casing.", true),
            ["GAMEPLAY_ShotgunCasing"] = ("Гильза от дробовика", "Shotgun Shell Casing", false),
            ["GAMEPLAY_ShotgunCasingDescription"] = (
                "Стреляная гильза дробового патрона. Пригодится, чтобы снарядить новый.",
                "A spent shotgun shell casing. Useful for loading a new one.", true),
            [ShotgunSkill.NameKey] = ("Дробовик", "Shotgun", true),
            [ShotgunSkill.DescKey + "0"] = ("Из дробовика вы почти не стреляли. Отдача сбивает прицел, и дробь часто уходит мимо.",
                "You have barely fired a shotgun. The kick throws off your aim and the shot often goes wide.", true),
            [ShotgunSkill.DescKey + "1"] = ("Вы привыкаете к тяжёлому стволу и отдаче.",
                "You are getting used to the heavy barrel and the kick.", true),
            [ShotgunSkill.DescKey + "2"] = ("На ближней дистанции вы бьёте уверенно.",
                "You shoot with confidence at close range.", true),
            [ShotgunSkill.DescKey + "3"] = ("Вы знаете, когда подпустить зверя ближе, чтобы дробь легла кучно.",
                "You know when to let an animal come closer so the shot lands tight.", true),
            [ShotgunSkill.DescKey + "4"] = ("Вы мастер дробовика. Каждый выстрел на счету, и вы не тратите его зря.",
                "You are a master of the shotgun. Every shell counts, and you never waste one.", true),
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
            if (key == null) return;
            if (key.StartsWith(ShotgunSkill.BenefitKey, StringComparison.Ordinal))
            {
                if (int.TryParse(key.Substring(ShotgunSkill.BenefitKey.Length), out int tier)) __result = ShotgunSkill.Benefits(tier, IsRussian());
                return;
            }
            if (!T.TryGetValue(key, out var v)) return;
            bool missing = string.IsNullOrEmpty(__result) || __result == key || __result.StartsWith("[", StringComparison.Ordinal) && __result.Contains(key);
            if (!v.force && !missing) return;
            __result = IsRussian() ? v.ru : v.en;
        }
    }
}
