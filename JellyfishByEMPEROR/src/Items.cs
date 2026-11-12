using UnityEngine.AddressableAssets;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace JellyfishByEMPEROR;

/// <summary>A mod item is a copy of a game prefab with our model, texture and values. The game finds items by name, so those lookups are intercepted
/// (the same scheme as SmokesByEMPEROR and FoodPackByEMPEROR, with our own prefix and reference so the mods never claim each other's items).</summary>
public static partial class Items
{
    static readonly Dictionary<string, GearItem> templates = new();

    public static bool FromReference(AssetReference? r, out string name)
    {
        name = "";
        var guid = r?.AssetGUID;
        if (guid == null || !guid.StartsWith(Defs.RefPrefix, StringComparison.Ordinal)) return false;
        name = Defs.Prefix + guid.Substring(Defs.RefPrefix.Length);
        return true;
    }

    public static GearItem? Spawn(string id) => GearItem.InstantiateGearItem(Defs.Prefix + id);

    /// <summary>A hidden copy kept alive: what a recipe or a trade shows and gives.</summary>
    public static GearItem? Template(string id)
    {
        if (templates.TryGetValue(id, out var t) && t != null) return t;
        t = Spawn(id);
        if (t == null) return null;
        t.gameObject.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(t.gameObject);
        return templates[id] = t;
    }

    public static bool IsTemplate(GearItem g)
    {
        foreach (var t in templates.Values) if (t != null && t.Pointer == g.Pointer) return true;
        return false;
    }

    // Customize lives in Build.cs

    public static void SetFood(GearItem g, float kg, float calories)
    {
        g.WeightKG = ItemWeight.FromKilograms(kg);
        if (g.m_FoodItem is { } fi)
        {
            fi.m_CaloriesTotal = calories;
            fi.m_CaloriesRemaining = calories;
        }
        if (g.m_FoodWeight is { } fw && kg > 0f) fw.m_CaloriesPerKG = calories / kg;
    }

    /// <summary>One model and one texture in place of the source item's parts. The model is in the item's own space as the game's was, so nothing is moved.</summary>
    public static void SingleMesh(GearItem g, Mesh? mesh, Texture2D? tex)
    {
        if (g.GetComponent<LODGroup>() is { } lod) UnityEngine.Object.DestroyImmediate(lod);
        var filters = g.GetComponentsInChildren<MeshFilter>(true);
        float before = 0f;
        foreach (var f in filters)
            if (f.sharedMesh != null && f.GetComponent<MeshRenderer>() is { enabled: true })
                before = Math.Max(before, f.sharedMesh.bounds.size.magnitude * f.transform.lossyScale.x / Math.Max(.0001f, g.transform.lossyScale.x));
        bool placed = false;
        foreach (var f in filters)
        {
            var r = f.GetComponent<MeshRenderer>();
            if (!placed && r != null)
            {
                placed = true;
                if (f.transform.parent != g.transform) f.transform.SetParent(g.transform, false);
                f.transform.localPosition = Vector3.zero;
                f.transform.localRotation = Quaternion.identity;
                f.transform.localScale = Vector3.one;
                f.gameObject.SetActive(true);
                if (mesh != null) f.sharedMesh = mesh;
                r.enabled = true;
                if (r.sharedMaterial != null)
                {
                    r.sharedMaterials = new Il2CppReferenceArray<Material>(new[] { r.sharedMaterial });
                    if (tex != null) r.material.mainTexture = tex;
                }
            }
            else
            {
                // gone, not hidden: the inspect view frames hidden renderers too
                if (r != null) UnityEngine.Object.DestroyImmediate(r);
                UnityEngine.Object.DestroyImmediate(f);
            }
        }
        if (mesh != null && g.m_Inspect != null)
        {
            // some fish are shown in the inspect view by a second, larger model or scaled up: ours is one model, shown as it lies
            try
            {
                var ins = g.m_Inspect;
                if (ins.m_Scale != Vector3.one || ins.m_InspectModeMesh != null || ins.m_NormalMesh != null)
                    MelonLoader.MelonLogger.Msg($"inspect {g.name}: scale {ins.m_Scale} dist {ins.m_DistanceFromCamera} inspectMesh {(ins.m_InspectModeMesh != null)} normalMesh {(ins.m_NormalMesh != null)}");
                ins.m_Scale = Vector3.one;
                ins.m_InspectModeMesh = null;
                ins.m_NormalMesh = null;
            }
            catch (Exception e) { Safe.Log("inspect " + g.name, e); }
            float after = mesh.bounds.size.magnitude;
            if (before > 0f && after > before * 1.1f) g.m_Inspect.m_DistanceFromCamera *= after / before;
        }
        // the collider fits the new model
        if (mesh != null && g.GetComponent<BoxCollider>() is { } box)
        {
            var b = mesh.bounds;
            box.center = b.center;
            box.size = new Vector3(Math.Max(b.size.x, .01f), Math.Max(b.size.y, .01f), Math.Max(b.size.z, .01f));
        }
    }

    /// <summary>The item's own lists of its renderers, which the game walks to hide it in the inventory and show it again: made anew.</summary>
    public static void RefreshRenderers(GearItem g)
    {
        var mr = new List<MeshRenderer>();
        foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true))
            if (r != null) mr.Add(r);
        var sr = new List<SkinnedMeshRenderer>();
        foreach (var r in g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (r != null) sr.Add(r);
        g.m_MeshRenderers = new Il2CppReferenceArray<MeshRenderer>(mr.ToArray());
        g.m_SkinnedMeshRenderers = new Il2CppReferenceArray<SkinnedMeshRenderer>(sr.ToArray());
    }

    public static float MaxHP(GearItem g) => g.m_GearItemData != null && g.m_GearItemData.m_MaxHP > 0f ? g.m_GearItemData.m_MaxHP : 100f;

    // ---- weight and calories of a piece just handed over, held for a few frames: the game rolls a new piece's weight when it first wakes
    static readonly List<(GearItem g, float kg, float cal, int frames)> pinned = new();

    public static void Pin(GearItem g, float kg, float cal)
    {
        SetFood(g, kg, cal);
        pinned.Add((g, kg, cal, 4));
    }

    public static void Tick()
    {
        for (int i = pinned.Count - 1; i >= 0; i--)
        {
            var p = pinned[i];
            if (p.g == null || p.frames <= 0) { pinned.RemoveAt(i); continue; }
            try { SetFood(p.g, p.kg, p.cal); } catch (Exception e) { Safe.Log("Pin food", e); }
            pinned[i] = (p.g, p.kg, p.cal, p.frames - 1);
        }
    }

    /// <summary>A food piece into the pack with exactly this weight, calories and condition (0..1).</summary>
    public static GearItem? GiveFood(string gearName, float kg, float cal, float condition)
    {
        var pm = GameManager.GetPlayerManagerComponent();
        if (pm == null) return null;
        var g = pm.InstantiateItemInPlayerInventory(gearName, 1, Mathf.Clamp(condition, .01f, 1f), PlayerManager.InventoryInstantiateFlags.None);
        if (g == null) return null;
        Pin(g, kg, cal);
        try { g.CurrentHP = MaxHP(g) * Mathf.Clamp(condition, .01f, 1f); } catch (Exception e) { Safe.Log("Give condition", e); }
        return g;
    }
}

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
        // the game copies the template: the copy has our parts but nobody animates it, lights it or keeps its jellyfish alive
        // (0.2.1 in game: a jar from the pack did not freeze and an aquarium showed nothing poured into it)
        if (__result != null)
        {
            try
            {
                __result.gameObject.name = Defs.Prefix + def.Id;
                Visual.Attach(__result, def);
                Items.RefreshRenderers(__result);
            }
            catch (Exception e) { Safe.Log("Pack copy " + def.Id, e); }
        }
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
            __result = __instance.InstantiateItemInPlayerInventory(Defs.Prefix + def.Id, 1, __2, PlayerManager.InventoryInstantiateFlags.None);
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

/// <summary>Our items never get the game's random weight.</summary>
[HarmonyPatch]
internal static class NoRollPatches
{
    [HarmonyPrefix, HarmonyPatch(typeof(GearItem), nameof(GearItem.MaybeRollRandomWeightAndCalories))]
    static bool Weight(GearItem __instance) => !Defs.Is(__instance, out _);

    [HarmonyPrefix, HarmonyPatch(typeof(GearItem), nameof(GearItem.RollGearConditionAndWeight))]
    static bool Both(GearItem __instance) => !Defs.Is(__instance, out _);

    [HarmonyPrefix, HarmonyPatch(typeof(GearItem), nameof(GearItem.RollSeededGearConditionAndWeight))]
    static bool Seeded(GearItem __instance) => !Defs.Is(__instance, out _);
}

/// <summary>The inventory icon wherever the game asks: copied item data keeps pointing at the source item's icon.</summary>
[HarmonyPatch]
internal static class IconPatches
{
    static Texture2D? Icon(string? name)
    {
        if (name != null && name.StartsWith("GearItemData_JF_", StringComparison.Ordinal)) name = Defs.Prefix + name.Substring("GearItemData_JF_".Length);
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

