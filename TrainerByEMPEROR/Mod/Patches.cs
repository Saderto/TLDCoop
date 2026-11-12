using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using UnityEngine;

namespace TrainerByEMPEROR
{
    // Патчи Harmony для того, что нельзя сделать простой записью значений.

    // ---------- Урон и смерть ----------

    internal static class DamageRules
    {
        public static void Adjust(ref float hp)
        {
            if (hp >= 0f) return;
            if (State.On("god")) hp = 0f;
            else hp *= State.Get("dmgmult");
        }
    }

    [HarmonyPatch(typeof(Condition), nameof(Condition.AddHealth), typeof(float), typeof(DamageSource))]
    internal static class Patch_AddHealth2
    {
        private static void Prefix(ref float hp) => DamageRules.Adjust(ref hp);
    }

    [HarmonyPatch(typeof(Condition), nameof(Condition.AddHealth), typeof(float), typeof(DamageSource), typeof(bool))]
    internal static class Patch_AddHealth3
    {
        private static void Prefix(ref float hp) => DamageRules.Adjust(ref hp);
    }

    [HarmonyPatch(typeof(Condition), nameof(Condition.AddHealthWithNoHudNotification))]
    internal static class Patch_AddHealthNoHud
    {
        private static void Prefix(ref float hp) => DamageRules.Adjust(ref hp);
    }

    [HarmonyPatch(typeof(Condition), nameof(Condition.PlayerDeath))]
    internal static class Patch_PlayerDeath
    {
        private static bool Prefix() => !State.On("god");
    }

    [HarmonyPatch(typeof(Condition), nameof(Condition.KillPlayer))]
    internal static class Patch_KillPlayer
    {
        private static bool Prefix() => !State.On("god");
    }

    [HarmonyPatch(typeof(FallDamage), nameof(FallDamage.ApplyFallDamage))]
    internal static class Patch_ApplyFallDamage
    {
        private static bool Prefix()
        {
            if (Fly.Active || Time.unscaledTime < Fly.NoFallDamageUntil) return false;
            return !State.On("god");
        }
    }

    // ---------- Болезни и травмы ----------

    [HarmonyPatch(typeof(Infection), nameof(Infection.InfectionStart))]
    internal static class Patch_InfectionStart
    {
        private static bool Prefix() => !State.On("noinfection");
    }

    [HarmonyPatch(typeof(InfectionRisk), nameof(InfectionRisk.InfectionRiskStart))]
    internal static class Patch_InfectionRiskStart
    {
        private static bool Prefix() => !State.On("noinfection");
    }

    [HarmonyPatch(typeof(InfectionRisk), nameof(InfectionRisk.ConstantInfectionRiskStart))]
    internal static class Patch_ConstantInfectionRiskStart
    {
        private static bool Prefix() => !State.On("noinfection");
    }

    [HarmonyPatch(typeof(SprainedWrist), nameof(SprainedWrist.SprainedWristStart))]
    internal static class Patch_SprainedWristStart
    {
        private static bool Prefix() => !State.On("nowrist");
    }

    [HarmonyPatch(typeof(SprainedAnkle), nameof(SprainedAnkle.SprainedAnkleStart))]
    internal static class Patch_SprainedAnkleStart
    {
        private static bool Prefix() => !State.On("noankle");
    }

    [HarmonyPatch(typeof(SprainPain), nameof(SprainPain.ApplyAffliction))]
    internal static class Patch_SprainPainApply
    {
        private static bool Prefix() => !State.On("nopain");
    }

    // ---------- Вес ----------

    [HarmonyPatch(typeof(Encumber), nameof(Encumber.IsEncumbered))]
    internal static class Patch_IsEncumbered
    {
        private static void Postfix(ref bool __result)
        {
            if (State.On("weight")) __result = false;
        }
    }

    [HarmonyPatch(typeof(Encumber), nameof(Encumber.IsEncumberedIgnoringPlaceMesh))]
    internal static class Patch_IsEncumberedIgnoringPlaceMesh
    {
        private static void Postfix(ref bool __result)
        {
            if (State.On("weight")) __result = false;
        }
    }

    [HarmonyPatch(typeof(Encumber), nameof(Encumber.GetEncumberLevel))]
    internal static class Patch_GetEncumberLevel
    {
        private static void Postfix(ref EncumberLevel __result)
        {
            if (State.On("weight")) __result = EncumberLevel.None;
        }
    }

    [HarmonyPatch(typeof(Encumber), nameof(Encumber.GetEncumbranceSlowdownMultiplier))]
    internal static class Patch_EncumbranceSlowdown
    {
        private static void Postfix(ref float __result)
        {
            if (State.On("weight")) __result = 1f;
        }
    }

    // ---------- Расходники ----------

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveUnits))]
    internal static class Patch_RemoveUnits
    {
        private static bool Prefix() => !State.On("uses");
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveGearFromInventory))]
    internal static class Patch_RemoveGearFromInventory
    {
        private static bool Prefix() => !State.On("uses");
    }

    // ---------- Крафт ----------

    [HarmonyPatch(typeof(BlueprintData), nameof(BlueprintData.HasRequiredMaterials))]
    internal static class Patch_HasRequiredMaterials
    {
        private static void Postfix(ref bool __result)
        {
            if (State.On("easycraft")) __result = true;
        }
    }

    [HarmonyPatch(typeof(BlueprintData), nameof(BlueprintData.HasRequiredTool))]
    internal static class Patch_HasRequiredTool
    {
        private static void Postfix(ref bool __result)
        {
            if (State.On("easycraft")) __result = true;
        }
    }

    [HarmonyPatch(typeof(CraftingOperation), nameof(CraftingOperation.ConsumeMaterialsUsedForCrafting))]
    internal static class Patch_ConsumeMaterials
    {
        private static bool Prefix() => !State.On("easycraft");
    }

    [HarmonyPatch(typeof(CraftingOperation), nameof(CraftingOperation.GetModifiedCraftingDuration))]
    internal static class Patch_CraftingDuration
    {
        private static void Postfix(ref int __result)
        {
            if (State.On("instantcraft")) __result = 1;
        }
    }

    // ---------- Оружие ----------

    [HarmonyPatch(typeof(GunItem), nameof(GunItem.RemoveNextFromClip))]
    internal static class Patch_RemoveNextFromClip
    {
        // false = пропустить оригинальный метод, патрон не тратится.
        private static bool Prefix() => !State.On("ammo");
    }

    [HarmonyPatch(typeof(GunItem), nameof(GunItem.RollForMisfire))]
    internal static class Patch_RollForMisfire
    {
        private static void Postfix(ref bool __result)
        {
            if (State.On("nojam")) __result = false;
        }
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), typeof(float), typeof(float), typeof(DamageSource), typeof(string))]
    internal static class Patch_AiApplyDamage4
    {
        private static void Prefix(BaseAi __instance, ref float damage)
        {
            if (State.On("onehit") && damage > 0f) damage = Mathf.Max(damage, __instance.m_MaxHP * 10f + 1000f);
        }
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), typeof(float), typeof(DamageSource), typeof(string))]
    internal static class Patch_AiApplyDamage3
    {
        private static void Prefix(BaseAi __instance, ref float damage)
        {
            if (State.On("onehit") && damage > 0f) damage = Mathf.Max(damage, __instance.m_MaxHP * 10f + 1000f);
        }
    }

    // ---------- Сохранение при смерти ----------

    internal static class SaveGuard
    {
        public static bool PlayerDied;

        public static bool AllowDelete()
        {
            if (State.On("keepsave") && PlayerDied)
            {
                MelonLoader.MelonLogger.Msg("Удаление сохранения после смерти заблокировано трейнером.");
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.HandlePlayerDeath))]
    internal static class Patch_HandlePlayerDeath
    {
        private static void Prefix() => SaveGuard.PlayerDied = true;
    }

    [HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.DeleteSaveFiles))]
    internal static class Patch_DeleteSaveFiles
    {
        private static bool Prefix() => SaveGuard.AllowDelete();
    }

    [HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.DeleteSaveFilesForGameId))]
    internal static class Patch_DeleteSaveFilesForGameId
    {
        private static bool Prefix() => SaveGuard.AllowDelete();
    }

    [HarmonyPatch(typeof(SaveGameSlots), nameof(SaveGameSlots.DeleteSlot), typeof(string))]
    internal static class Patch_DeleteSlotByName
    {
        private static bool Prefix() => SaveGuard.AllowDelete();
    }

    [HarmonyPatch(typeof(SaveGameSlots), nameof(SaveGameSlots.DeleteAllSlotsForGameId))]
    internal static class Patch_DeleteAllSlotsForGameId
    {
        private static bool Prefix() => SaveGuard.AllowDelete();
    }

    // Полёт и сохранение: игра не сохраняется с выключенным контроллером игрока.
    [HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.PrepareSaveData))]
    internal static class Patch_PrepareSaveData_Fly
    {
        private static void Prefix() => Fly.Pause(1.5f);
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadSceneWithLoadingScreen))]
    internal static class Patch_LoadSceneWithLoadingScreen_Fly
    {
        private static void Prefix() => Fly.Pause(12f);
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadScene))]
    internal static class Patch_LoadScene_Fly
    {
        private static void Prefix() => Fly.Pause(12f);
    }
}
