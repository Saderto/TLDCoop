using UnityEngine.AddressableAssets;

namespace SnacksByEMPEROR;

[HarmonyPatch(typeof(GearItem), nameof(GearItem.InstantiateGearItem), new[] { typeof(string) })]
internal static class InstantiatePatch
{
    static void Prefix(ref string prefabName, out Def? __state)
    {
        __state = Defs.TryGet(prefabName, out var def) ? def : null;
        if (__state != null) prefabName = __state.Base;
    }

    static void Postfix(GearItem __result, Def? __state)
    {
        if (__state == null) return;
        if (__result == null)
        {
            MelonLogger.Error($"{__state.Id}: prefab {__state.Base} not found (is its DLC missing?).");
            return;
        }
        try { Items.Customize(__result, __state, false); }
        catch (Exception e) { Safe.Log("Item " + __state.Id, e); }
    }
}

[HarmonyPatch(typeof(GearItem), nameof(GearItem.InstantiateAndDeserializeGearItem), new[] { typeof(string), typeof(string), typeof(Transform), typeof(bool), typeof(bool) })]
internal static class LoadFromTextPatch
{
    static void Prefix(ref string prefabName, out Def? __state)
    {
        __state = Defs.TryGet(prefabName, out var def) ? def : null;
        if (__state != null) prefabName = __state.Base;
    }

    static void Postfix(GearItem __result, Def? __state)
    {
        if (__state == null || __result == null) return;
        try { Items.Customize(__result, __state, true); }
        catch (Exception e) { Safe.Log("Saved item " + __state.Id, e); }
    }
}

[HarmonyPatch(typeof(GearItem), nameof(GearItem.InstantiateAndDeserializeGearItem), new[] { typeof(string), typeof(GearItemSaveDataProxy), typeof(Transform), typeof(bool), typeof(bool) })]
internal static class LoadFromProxyPatch
{
    static void Prefix(ref string prefabName, out Def? __state)
    {
        __state = Defs.TryGet(prefabName, out var def) ? def : null;
        if (__state != null) prefabName = __state.Base;
    }

    static void Postfix(GearItem __result, Def? __state)
    {
        if (__state == null || __result == null) return;
        try { Items.Customize(__result, __state, true); }
        catch (Exception e) { Safe.Log("Saved item " + __state.Id, e); }
    }
}

[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.InstantiateItemInPlayerInventory), new[] { typeof(string), typeof(int), typeof(float), typeof(PlayerManager.InventoryInstantiateFlags) })]
internal static class AddToInventoryPatch
{
    static bool Prefix(PlayerManager __instance, string name, int units, float normalizedCondition, PlayerManager.InventoryInstantiateFlags flags, ref GearItem __result)
    {
        if (!Defs.TryGet(name, out var def)) return true;
        var template = Items.Spawn(def.Id);
        if (template == null) return false;
        __result = __instance.InstantiateItemInPlayerInventoryFromPrefab(template, 1, normalizedCondition, flags);
        UnityEngine.Object.Destroy(template.gameObject);
        return false;
    }
}

[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.AddItemCONSOLE))]
internal static class ConsoleAddPatch
{
    static bool Prefix(PlayerManager __instance, string __0, int __1, float __2, ref GearItem __result)
    {
        if (!Defs.TryGet(__0, out var def)) return true;
        __result = null!;
        for (int i = 0; i < Math.Max(1, __1); i++)
            __result = __instance.InstantiateItemInPlayerInventory(def.Gear, 1, __2, PlayerManager.InventoryInstantiateFlags.None);
        return false;
    }
}

[HarmonyPatch]
internal static class ReferencePatches
{
    [HarmonyPrefix, HarmonyPatch(typeof(GearItem), nameof(GearItem.InstantiateGearItem), new[] { typeof(AssetReference) })]
    static bool Spawn(AssetReference assetReference, ref GearItem __result)
    {
        if (!Items.FromReference(assetReference, out var name)) return true;
        __result = GearItem.InstantiateGearItem(name);
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(GearItem), nameof(GearItem.InstantiateAndDeserializeGearItem), new[] { typeof(AssetReference), typeof(string), typeof(Transform), typeof(bool), typeof(bool) })]
    static bool LoadText(AssetReference assetReference, string serializedGearItem, Transform parent, bool applyPositioningFix, bool instantiatedInContainer, ref GearItem __result)
    {
        if (!Items.FromReference(assetReference, out var name)) return true;
        __result = GearItem.InstantiateAndDeserializeGearItem(name, serializedGearItem, parent, applyPositioningFix, instantiatedInContainer);
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(GearItem), nameof(GearItem.InstantiateAndDeserializeGearItem), new[] { typeof(AssetReference), typeof(GearItemSaveDataProxy), typeof(Transform), typeof(bool), typeof(bool) })]
    static bool LoadProxy(AssetReference assetReference, GearItemSaveDataProxy proxy, Transform parent, bool applyPositioningFix, bool instantiatedInContainer, ref GearItem __result)
    {
        if (!Items.FromReference(assetReference, out var name)) return true;
        __result = GearItem.InstantiateAndDeserializeGearItem(name, proxy, parent, applyPositioningFix, instantiatedInContainer);
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.InstantiateItemInPlayerInventory), new[] { typeof(AssetReferenceGearItem), typeof(int), typeof(float), typeof(PlayerManager.InventoryInstantiateFlags) })]
    static bool Inventory(PlayerManager __instance, AssetReferenceGearItem assetReference, int units, float normalizedCondition, PlayerManager.InventoryInstantiateFlags flags, ref GearItem __result)
    {
        if (!Items.FromReference(assetReference, out var name)) return true;
        __result = __instance.InstantiateItemInPlayerInventory(name, units, normalizedCondition, flags);
        return false;
    }
}

/// <summary>The inventory icon wherever the game asks: copied item data keeps pointing at the source item's icon.</summary>
[HarmonyPatch]
internal static class IconPatches
{
    static Texture2D? Icon(string? name)
    {
        if (name != null && name.StartsWith("GearItemData_SNK_", StringComparison.Ordinal)) name = Defs.Prefix + name.Substring("GearItemData_SNK_".Length);
        return Defs.TryGet(name, out var def) ? Assets.Texture(def.Icon, compress: false) : null;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(GearItem), nameof(GearItem.GetInventoryIconTexture))]
    static void OfItem(GearItem __instance, ref Texture2D __result) => __result = Icon(__instance.name) ?? __result;

    [HarmonyPostfix, HarmonyPatch(typeof(Utils), nameof(Utils.GetInventoryIconTexture), new[] { typeof(GearItem) })]
    static void ByItem(GearItem gi, ref Texture2D __result)
    {
        if (gi != null) __result = Icon(gi.name) ?? __result;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Utils), nameof(Utils.GetInventoryIconTexture), new[] { typeof(GearItem), typeof(float) })]
    static void ByItemCondition(GearItem gi, ref Texture2D __result)
    {
        if (gi != null) __result = Icon(gi.name) ?? __result;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Utils), nameof(Utils.GetInventoryIconTexture), new[] { typeof(GearItemData), typeof(float) })]
    static void ByData(GearItemData gearItemData, ref Texture2D __result)
    {
        if (gearItemData != null) __result = Icon(gearItemData.name) ?? __result;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Utils), nameof(Utils.GetInventoryIconTextureFromPrefabName))]
    static void ByName(string prefabName, ref Texture2D __result) => __result = Icon(prefabName) ?? __result;
}

