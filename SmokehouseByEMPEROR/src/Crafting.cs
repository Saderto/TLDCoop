using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SmokehouseByEMPEROR;

/// <summary>The barrel at the workbench: 30 scrap metal, 6 reclaimed wood, 6 cloth, 4 stones, a hacksaw, 64 hours. A copy of one of the game's own workbench recipes,
/// added to the list the first time the game asks for it in a loaded game.</summary>
[HarmonyPatch]
internal static class Crafting
{
    const string Name = "BLUEPRINT_GEAR_SH_Barrel";
    static BlueprintData? bp;
    static int failed;
    static readonly Dictionary<string, GearItem> parts = new();
    static readonly (string gear, int n)[] Needs = { ("GEAR_ScrapMetal", 30), ("GEAR_ReclaimedWoodB", 6), ("GEAR_Cloth", 6), ("GEAR_Stone", 4) };

    static GearItem Part(string gear)
    {
        if (parts.TryGetValue(gear, out var g) && g != null) return g;
        g = GearItem.InstantiateGearItem(gear) ?? throw new InvalidOperationException("no " + gear);
        g.gameObject.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(g.gameObject);
        return parts[gear] = g;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(BlueprintManager), nameof(BlueprintManager.GetAllBlueprints))]
    static void All(Il2CppSystem.Collections.Generic.List<BlueprintData> __result)
    {
        if (__result == null || failed >= 3) return;
        try
        {
            if (bp != null) { if (!__result.Contains(bp)) __result.Add(bp); return; }
            if (GameManager.GetPlayerManagerComponent() == null || GameManager.GetInventoryComponent() == null) return;   // not in a game yet
            BlueprintData? src = null;
            foreach (var b in __result)
                if (b != null && b.m_RequiredCraftingLocation == CraftingLocation.Workbench && !b.m_RequiresLitFire && b.m_CraftedResultGear != null) { src = b; break; }
            if (src == null) return;
            var result = Items.Template(Defs.Barrel) ?? throw new InvalidOperationException("no barrel item");
            var n = UnityEngine.Object.Instantiate(src);
            n.name = Name;
            n.hideFlags = HideFlags.HideAndDontSave;
            n.SetCraftedResult(result);
            n.m_CraftedResultGear = result;
            n.m_CraftedResultCount = 1;
            n.m_DurationMinutes = 64 * 60;
            n.m_RequiredCraftingLocation = CraftingLocation.Workbench;
            n.m_RequiresLitFire = false;
            n.m_RequiresLight = true;
            n.m_RequiredTool = null;
            try
            {
                // the barrel is cut with a hacksaw
                var saw = Part("GEAR_Hacksaw").GetComponent<ToolsItem>();
                if (saw != null) n.m_RequiredTool = saw;
                else Safe.Info("Barrel recipe: the hacksaw has no tool part, no tool is asked");
            }
            catch (Exception e) { Safe.Log("Recipe tool", e); }
            n.m_OptionalTools = new Il2CppReferenceArray<ToolsItem>(0L);
            n.m_Locked = false;
            n.m_IgnoreLockInSurvival = true;
            n.m_AppearsInStoryOnly = false;
            n.m_AppearsInSurvivalOnly = false;
            n.m_XPModesToDisable?.Clear();
            n.m_UsesPhoto = false;
            n.m_CanIncreaseRepairSkill = false;
            var req = new Il2CppReferenceArray<BlueprintData.RequiredGearItem>(Needs.Length);
            for (int i = 0; i < Needs.Length; i++)
                req[i] = new BlueprintData.RequiredGearItem { m_Item = Part(Needs[i].gear), m_Count = Needs[i].n, m_Units = BlueprintData.RequiredGearItem.Units.Count, m_Weight = ItemWeight.FromKilograms(0f) };
            n.m_RequiredGear = req;
            n.m_RequiredLiquid = new Il2CppReferenceArray<BlueprintData.RequiredLiquid>(0L);
            n.m_RequiredPowder = new Il2CppReferenceArray<BlueprintData.RequiredPowder>(0L);
            bp = n;
            __result.Add(n);
            Safe.Info($"Barrel recipe added (copied from {src.name}).");
        }
        catch (Exception e)
        {
            failed++;
            MelonLogger.Error("Barrel recipe: " + e);
        }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(BlueprintData), nameof(BlueprintData.GetCraftingIcon))]
    static bool Icon(BlueprintData __instance, ref Texture2D __result)
    {
        try
        {
            if (__instance == null || __instance.name != Name) return true;
            var t = Assets.Texture("icons/Barrel.png", compress: false);
            if (t == null) return true;
            __result = t;
            return false;
        }
        catch (Exception e)
        {
            Safe.Log("Recipe icon", e);
            return true;
        }
    }
}
