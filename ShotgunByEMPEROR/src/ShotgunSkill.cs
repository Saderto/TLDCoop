using System;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace ShotgunByEMPEROR
{
    /// <summary>
    /// Survival has no shotgun skill (SkillsManager.m_Skill_Shotgun_Prefab is empty, GetSkillShotgun() was null and every
    /// shot crashed in GearItem.DegradeOnUse). Since 0.1.16 we add a real «Дробовик / Shotgun» skill: own points, shown
    /// in the journal right after the rifle, saved by the game itself (SkillsManagerSaveData.m_Skill_ShotgunSerialized
    /// exists in survival too, the game writes/reads it when m_Skill_Shotgun is set). Bonus tables are the rifle's.
    /// Before 0.1.16 the hidden skill mirrored rifle points, so an old save starts with the rifle's level.
    /// </summary>
    internal static class ShotgunSkill
    {
        static Skill_Shotgun _skill;
        internal const string NameKey = "TLDSHOTGUN_SkillName";
        internal const string DescKey = "TLDSHOTGUN_SkillDesc_";
        internal const string BenefitKey = "TLDSHOTGUN_SkillBenefit_";

        internal static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(SkillsManager), nameof(SkillsManager.Awake)),
                postfix: new HarmonyMethod(typeof(ShotgunSkill), nameof(AwakePostfix)));
            h.Patch(AccessTools.Method(typeof(SkillsManager), nameof(SkillsManager.Deserialize)),
                prefix: new HarmonyMethod(typeof(ShotgunSkill), nameof(BeforeLoad)));
            h.Patch(AccessTools.Method(typeof(GameManager), nameof(GameManager.GetSkillShotgun)),
                prefix: new HarmonyMethod(typeof(ShotgunSkill), nameof(GetPrefix)));
        }

        static void AwakePostfix(SkillsManager __instance) => Create(__instance);
        static void BeforeLoad(SkillsManager __instance) => Create(__instance);
        static void GetPrefix() { try { Create(GameManager.GetSkillsManager()); } catch { } }

        static LocalizedString L(string key) { var l = new LocalizedString(); l.m_LocalizationID = key; return l; }

        internal static void Create(SkillsManager sm)
        {
            try
            {
                if (sm == null) return;
                var rifle = sm.m_Skill_Rifle;
                if (rifle == null) return;
                if (sm.m_Skill_Shotgun != null && (_skill == null || sm.m_Skill_Shotgun.Pointer != _skill.Pointer)) return; // someone else made one
                var s = sm.m_Skill_Shotgun;
                if (s == null)
                {
                    var go = new GameObject("Skill_Shotgun (ShotgunByEMPEROR)");
                    go.transform.SetParent(sm.transform, false);
                    s = go.AddComponent<Skill_Shotgun>();
                    s.m_SkillType = SkillType.Shotgun;
                    s.m_LocalizedDisplayName = L(NameKey);
                    s.m_SkillImage = rifle.m_SkillImage;
                    s.m_SkillIcon = rifle.m_SkillIcon;
                    s.m_SkillIconBackground = rifle.m_SkillIconBackground;
                    s.m_TierPoints = rifle.m_TierPoints;
                    int tiers = rifle.m_TierPoints != null ? rifle.m_TierPoints.Length : 5;
                    var desc = new Il2CppReferenceArray<LocalizedString>(tiers);
                    var ben = new Il2CppReferenceArray<LocalizedString>(tiers);
                    for (int i = 0; i < tiers; i++) { desc[i] = L(DescKey + i); ben[i] = L(BenefitKey + i); }
                    s.m_TierLocalizedDescriptions = desc;
                    s.m_TierLocalizedBenefits = ben;
                    s.m_CriticalHitChanceIncrease = rifle.m_CriticalHitChanceIncrease;
                    s.m_ConditionRepairBonus = rifle.m_ConditionRepairBonus;
                    s.m_ConditionDegradeOnUseReduction = rifle.m_ConditionDegradeOnUseReduction;
                    s.m_DamageIncrease = rifle.m_DamageIncrease;
                    s.m_AimAssistAngleDegrees = rifle.m_AimAssistAngleDegrees;
                    int n = rifle.m_AimAssistAngleDegrees != null ? rifle.m_AimAssistAngleDegrees.Length : tiers;
                    var txt = new Il2CppStringArray(n);
                    for (int i = 0; i < n; i++) txt[i] = "";
                    s.m_AimAssistTierText = txt;
                    s.m_CurrentPoints = 0;
                    sm.m_Skill_Shotgun = s;
                    _skill = s;
                    ShotgunMod.Log("навык «Дробовик» создан / shotgun skill created");
                }
                // journal list: right after the rifle
                var list = sm.m_Skills;
                if (list != null && !list.Contains(s))
                {
                    int at = list.IndexOf(rifle);
                    if (at < 0) list.Add(s); else list.Insert(at + 1, s);
                }
            }
            catch (Exception e) { ShotgunMod.Warn("shotgun skill: " + e.Message); }
        }

        static int At(Il2CppStructArray<int> a, int i) => a != null && a.Length > 0 ? a[Math.Min(i, a.Length - 1)] : 0;

        // benefits of a level, built from the bonus tables (same numbers as the rifle)
        internal static string Benefits(int tier, bool ru)
        {
            var s = _skill;
            if (s == null) return "";
            int crit = At(s.m_CriticalHitChanceIncrease, tier), repair = At(s.m_ConditionRepairBonus, tier),
                wear = At(s.m_ConditionDegradeOnUseReduction, tier), dmg = At(s.m_DamageIncrease, tier);
            var sb = new StringBuilder();
            void Line(string t) { if (sb.Length > 0) sb.Append('\n'); sb.Append(t); }
            if (crit > 0) Line(ru ? $"Шанс убить зверя одним попаданием +{crit}%" : $"+{crit}% chance to kill an animal with one hit");
            if (dmg > 0) Line(ru ? $"Урон дробью +{dmg}%" : $"+{dmg}% shot damage");
            if (wear > 0) Line(ru ? $"Дробовик изнашивается от стрельбы на {wear}% медленнее" : $"Shotgun wears {wear}% slower when fired");
            if (repair > 0) Line(ru ? $"Починка дробовика +{repair}%" : $"+{repair}% shotgun repair");
            if (sb.Length == 0) Line(ru ? "Пока никаких преимуществ." : "No benefits yet.");
            return sb.ToString();
        }
    }
}
