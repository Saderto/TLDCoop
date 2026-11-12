using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace JellyfishByEMPEROR;

/// <summary>Aquarium at the workbench: 4 glass jars (their glass), 8 scrap metal, 4 reclaimed wood, 8 hours (my choice, 11-12).
/// A copy of the game's own recipes, added the first time the game asks for the list.</summary>
[HarmonyPatch]
internal static class Crafting
{
    const string NamePrefix = "BLUEPRINT_GEAR_JF_";
    record Recipe(string Id, (string gear, int n)[] Needs, int Minutes, string? Tool, CraftingLocation Where, int Count = 1);
    static readonly Recipe[] Recipes =
    {
        new("Aquarium", new[] { ("GEAR_JF_Jar", 4), ("GEAR_ScrapMetal", 8), ("GEAR_ReclaimedWoodB", 4) }, 8 * 60, null, CraftingLocation.Workbench),
    };
    static readonly List<BlueprintData> made = new();
    static bool built;
    static int failed;
    static readonly Dictionary<string, GearItem> parts = new();

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
            if (built)
            {
                foreach (var m in made) if (!__result.Contains(m)) __result.Add(m);
                return;
            }
            if (GameManager.GetPlayerManagerComponent() == null || GameManager.GetInventoryComponent() == null) return;   // not in a game yet
            foreach (var r in Recipes)
            {
                if (made.Any(m => m.name == NamePrefix + r.Id)) continue;
                BlueprintData? src = null;
                foreach (var b in __result)
                    if (b != null && b.m_RequiredCraftingLocation == r.Where && !b.m_RequiresLitFire && b.m_CraftedResultGear != null && !b.name.StartsWith(NamePrefix)) { src = b; break; }
                if (src == null) { Safe.Info("No recipe to copy for " + r.Id); continue; }
                var n = Build(src, r);
                made.Add(n);
                __result.Add(n);
                Safe.Info($"Recipe added: {r.Id} (copied from {src.name}).");
            }
            built = true;
        }
        catch (Exception e)
        {
            failed++;
            MelonLogger.Error("Station recipes: " + e);
        }
    }

    static BlueprintData Build(BlueprintData src, Recipe r)
    {
        var result = Items.Template(r.Id) ?? throw new InvalidOperationException("no item " + r.Id);
        var n = UnityEngine.Object.Instantiate(src);
        n.name = NamePrefix + r.Id;
        n.hideFlags = HideFlags.HideAndDontSave;
        n.SetCraftedResult(result);
        n.m_CraftedResultGear = result;
        n.m_CraftedResultCount = r.Count;
        n.m_DurationMinutes = r.Minutes;
        n.m_RequiredCraftingLocation = r.Where;
        n.m_RequiresLitFire = false;
        n.m_RequiresLight = r.Where == CraftingLocation.Workbench;
        n.m_RequiredTool = null;
        if (r.Tool != null)
        {
            try
            {
                var tool = Part(r.Tool).GetComponent<ToolsItem>();
                if (tool != null) n.m_RequiredTool = tool;
                else Safe.Info($"Recipe {r.Id}: {r.Tool} has no tool part, no tool is asked");
            }
            catch (Exception e) { Safe.Log("Recipe tool", e); }
        }
        n.m_OptionalTools = new Il2CppReferenceArray<ToolsItem>(0L);
        n.m_Locked = false;
        n.m_IgnoreLockInSurvival = true;
        n.m_AppearsInStoryOnly = false;
        n.m_AppearsInSurvivalOnly = false;
        n.m_XPModesToDisable?.Clear();
        n.m_UsesPhoto = false;
        n.m_CanIncreaseRepairSkill = false;
        n.m_RequiredGear = Needs(r);
        n.m_RequiredLiquid = new Il2CppReferenceArray<BlueprintData.RequiredLiquid>(0L);
        n.m_RequiredPowder = new Il2CppReferenceArray<BlueprintData.RequiredPowder>(0L);
        return n;
    }

    static Il2CppReferenceArray<BlueprintData.RequiredGearItem> Needs(Recipe r)
    {
        var req = new Il2CppReferenceArray<BlueprintData.RequiredGearItem>(r.Needs.Length);
        for (int i = 0; i < r.Needs.Length; i++)
            req[i] = new BlueprintData.RequiredGearItem { m_Item = Part(r.Needs[i].gear), m_Count = r.Needs[i].n, m_Units = BlueprintData.RequiredGearItem.Units.Count, m_Weight = ItemWeight.FromKilograms(0f) };
        return req;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(BlueprintData), nameof(BlueprintData.GetCraftingIcon))]
    static bool Icon(BlueprintData __instance, ref Texture2D __result)
    {
        try
        {
            if (__instance == null || __instance.name == null || !__instance.name.StartsWith(NamePrefix, StringComparison.Ordinal)) return true;
            var t = Assets.Texture("icons/" + __instance.name.Substring(NamePrefix.Length) + ".png", compress: false);
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
