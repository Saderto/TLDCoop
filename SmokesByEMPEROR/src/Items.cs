using UnityEngine.AddressableAssets;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SmokesByEMPEROR;

/// <summary>A mod item is a copy of a game prefab with our model, texture and values. The game finds items by name, so those lookups are intercepted
/// (the same scheme as FoodPackByEMPEROR, with our own prefix and reference so the two mods never claim each other's items).</summary>
public static class Items
{
    static readonly Dictionary<string, GearItemData> dataByName = new();

    public static bool FromReference(AssetReference? r, out string name)
    {
        name = "";
        var guid = r?.AssetGUID;
        if (guid == null || !guid.StartsWith(Defs.RefPrefix, StringComparison.Ordinal)) return false;
        name = Defs.Prefix + guid.Substring(Defs.RefPrefix.Length);
        return true;
    }

    public static GearItem? Spawn(string id) => GearItem.InstantiateGearItem(Defs.Prefix + id);

    /// <summary>restored: from a save, its units and condition are already right.</summary>
    public static void Customize(GearItem g, Def def, bool restored)
    {
        string name = Defs.Prefix + def.Id;
        g.gameObject.name = name;
        if (!dataByName.TryGetValue(name, out var data) || data == null)
        {
            // Our own data object: the game tells item types apart by it, so sharing it would stack us with the source item.
            data = UnityEngine.Object.Instantiate(g.m_GearItemData);
            data.name = "GearItemData_SeagullSmoke_" + def.Id;
            data.hideFlags = HideFlags.HideAndDontSave;
            data.m_BaseWeight = ItemWeight.FromKilograms(def.WeightKg);
            data.m_LocalizedName = new LocalizedString { m_LocalizationID = "SMOKES_" + def.Id + "_NAME" };
            data.m_LocalizedDescription = new LocalizedString { m_LocalizationID = "SMOKES_" + def.Id + "_DESC" };
            data.m_PrefabReference = new AssetReferenceGearItem(Defs.RefPrefix + def.Id);
            data.m_DailyHPDecay = 0f;
            // not medicine: the inventory sorts and filters by this
            if (def.Kind != Kind.Lit) data.m_Type = def.Kind switch { Kind.Lighter or Kind.Refill => GearType.Firestarting, Kind.Cutter => GearType.Tool, _ => GearType.Other };
            SoundsFrom(data, def);
            dataByName[name] = data;
        }
        g.m_GearItemData = data;
        g.WeightKG = ItemWeight.FromKilograms(def.WeightKg);
        if (!restored)
        {
            g.m_StartCondition = GearStartCondition.Perfect;
            g.CurrentHP = data.m_MaxHP > 0f ? data.m_MaxHP : 100f;
        }

        // packs, the pouch and the lighter are one item each with what is left inside, kept as its condition (it is in every save):
        // a stack would read as 20 packs and could be split when dropped
        if (g.m_StackableItem is { } st)
        {
            if (def.Units > 1 || def.Kind == Kind.Pipe || def.Kind == Kind.Cutter)
            {
                // a pack, pouch or lighter keeps its count as condition; a pipe or the cutter is a tool, one per tile
                UnityEngine.Object.DestroyImmediate(st);
                g.m_StackableItem = null;
            }
            else
            {
                // cigars and tubes: one per unit, so several lie as «3 шт.», never the painkiller bottle's 6
                st.m_DefaultUnitsInItem = 1;
                if (!restored) st.m_Units = 1;
            }
            if (g.m_StackableItem is { } st1 && def.UnitKey.Length > 0)
            {
                st1.m_LocalizedSingleUnitText = new LocalizedString { m_LocalizationID = "SMOKES_UNIT_" + def.UnitKey + "_ONE" };
                st1.m_LocalizedMultipleUnitText = new LocalizedString { m_LocalizationID = "SMOKES_UNIT_" + def.UnitKey + "_MANY" };
            }
        }
        if (def.Kind == Kind.Lighter && g.m_FireStarterItem is { } fs)
        {
            float max = data.m_MaxHP > 0f ? data.m_MaxHP : 100f;
            fs.m_ConsumeOnUse = false;
            fs.m_RequiresSunLight = false;
            fs.m_IsAccelerant = false;
            fs.m_ConditionDegradeOnUse = max / def.Units;
            if (boosted.Add(g.GetInstanceID()))
                fs.m_FireStartSkillModifier += def.FireBonus;   // wind-proof and jet lighters: a better chance, as the game's own better starters
        }
        if (g.m_FirstAidItem is { } fa)
        {
            fa.m_LocalizedInspectModeUseText = new LocalizedString { m_LocalizationID = "SMOKES_ACT_" + def.Kind.ToString().ToUpperInvariant() };
            fa.m_LocalizedProgressBarMessage = new LocalizedString { m_LocalizationID = "SMOKES_ACT_" + def.Kind.ToString().ToUpperInvariant() };
            fa.m_KillsPain = false;
            fa.m_AppliesSutures = false;
            fa.m_AppliesBandage = false;
            fa.m_ProvidesAntibiotics = false;
            fa.m_StabalizesSprains = false;
            fa.m_CleansWounds = false;
            fa.m_HPIncrease = 0f;
            fa.m_UnitsPerUse = 1;
            fa.m_UseAudio = "";              // no pill bottle rattle: our own sounds play instead
        }

        var tex = Assets.Texture(Def.TexPath(def.Texture));
        var mesh = Assets.LoadMesh("meshes/" + def.Mesh + ".fpmesh");
        if (def.Kind == Kind.Lit) LitItem(g, def, mesh, tex);
        else SingleMesh(g, mesh, tex);
        RefreshRenderers(g);
    }

    static readonly HashSet<int> boosted = new();

    // the painkiller bottle's pill rattle on pick up / select / put back: the sounds of a game item made of the same stuff instead
    static readonly Dictionary<string, GearItemData?> soundSources = new();

    static void SoundsFrom(GearItemData data, Def def)
    {
        string? from = def.Kind switch
        {
            Kind.Lit or Kind.Lighter => null,
            Kind.Pipe => "GEAR_Stick",
            Kind.Cutter => "GEAR_CanOpener",
            _ => "GEAR_PackMatches",
        };
        if (from == null) return;
        try
        {
            if (!soundSources.TryGetValue(from, out var src))
            {
                src = null;
                // a loaded data object of that item first (nothing made in the world); else a copy made and thrown away at once
                string dn = "GearItemData_" + from.Substring(5);
                foreach (var d in Resources.FindObjectsOfTypeAll<GearItemData>())
                    if (d != null && d.name == dn) { src = d; break; }
                var g = src != null ? null : GearItem.InstantiateGearItem(from);
                if (g != null)
                {
                    src = UnityEngine.Object.Instantiate(g.m_GearItemData);
                    src.hideFlags = HideFlags.HideAndDontSave;
                    UnityEngine.Object.Destroy(g.gameObject);
                }
                soundSources[from] = src;
                Safe.Info($"Item sounds from {from}: {(src != null ? "taken" : "not found")}");
            }
            if (src == null) return;
            data.m_PickupAudio = src.m_PickupAudio;
            data.m_StowAudio = src.m_StowAudio;
            data.m_PutBackAudio = src.m_PutBackAudio;
            data.m_MovementSoundCategory = src.m_MovementSoundCategory;
        }
        catch (Exception e) { Safe.Log("Item sounds " + def.Id, e); }
    }

    /// <summary>A torch copy that is only ever a smoke in the hand: no light, heat, fuel, fire starting, swings or wind; our timer ends it, not its own.</summary>
    static void LitItem(GearItem g, Def def, Mesh? mesh, Texture2D? tex)
    {
        var t = g.m_TorchItem;
        if (t != null)
        {
            t.m_BurnLifetimeMinutes = 100000f;
            t.m_CanBeRelit = false;
            t.m_CanBeHolstered = false;
            t.m_CanThrow = false;
            t.m_WindSpeedCannotIgnite = 9999f;
            t.m_WindSpeedExtinguish = 9999f;
            t.m_StaminaDrainPerSecond = 0f;
            t.m_ReduceLifetimeMinutesPerSwing = 0f;
        }
        void Strip<T>() where T : Component
        {
            foreach (var c in g.GetComponentsInChildren<T>(true)) UnityEngine.Object.DestroyImmediate(c);
        }
        Strip<FuelSourceItem>();
        Strip<FireStarterItem>();
        Strip<Harvest>();
        g.m_FuelSourceItem = null;
        g.m_FireStarterItem = null;
        g.m_Harvest = null;
        // The torch's own model and its burnt one both become ours: TorchItem switches between the two, so neither may go.
        foreach (var mf in g.GetComponentsInChildren<MeshFilter>(true))
        {
            var r = mf.GetComponent<MeshRenderer>();
            if (r == null || mesh == null) continue;
            if (IsFx(mf.transform, g.transform)) continue;
            mf.sharedMesh = mesh;
            mf.transform.localPosition = Vector3.zero;
            mf.transform.localRotation = Quaternion.identity;
            mf.transform.localScale = Vector3.one;
            if (r.sharedMaterial != null)
            {
                r.sharedMaterials = new Il2CppReferenceArray<Material>(new[] { r.sharedMaterial });
                if (tex != null) r.material.mainTexture = tex;
            }
        }
        if (g.GetComponent<LODGroup>() is { } lod) lod.enabled = false;
        // the glowing tip, as a light: the coop mod's copy in a friend's hand keeps lights (not scripts), and Visual finds it there by name
        try
        {
            foreach (var old in g.GetComponentsInChildren<Light>(true)) if (old.gameObject.name == EmberName) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var e = new GameObject(EmberName);
            e.transform.SetParent(g.transform, false);
            e.transform.localPosition = mesh != null ? mesh.bounds.center : Vector3.zero;
            var l = e.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, .45f, .12f);
            l.range = def.Smoke == SmokeType.Cigarette ? .25f : .35f;
            l.intensity = .6f;
            l.shadows = LightShadows.None;
        }
        catch (Exception ex) { Safe.Log("Ember light", ex); }
    }

    public const string EmberName = "SeagullSmokeEmber";

    static bool IsFx(Transform t, Transform root)
    {
        for (var p = t; p != null && p != root; p = p.parent)
            if (p.name == "FX" || p.name.StartsWith("Light", StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>One model and one texture in place of the source item's parts; its middle over the origin, bottom on the ground.</summary>
    static void SingleMesh(GearItem g, Mesh? mesh, Texture2D? tex)
    {
        if (g.GetComponent<LODGroup>() is { } lod) UnityEngine.Object.DestroyImmediate(lod);
        var filters = g.GetComponentsInChildren<MeshFilter>(true);
        float before = 0f;
        foreach (var f in filters)
            if (f.sharedMesh != null && f.GetComponent<MeshRenderer>() is { enabled: true })
                before = Math.Max(before, f.sharedMesh.bounds.size.magnitude * f.transform.lossyScale.x);
        bool placed = false;
        foreach (var f in filters)
        {
            var r = f.GetComponent<MeshRenderer>();
            if (!placed && r != null)
            {
                placed = true;
                if (f.transform.parent != g.transform) f.transform.SetParent(g.transform, false);
                var mid = mesh != null ? mesh.bounds.center : Vector3.zero;
                f.transform.localPosition = new Vector3(-mid.x, 0f, -mid.z);
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
            float after = mesh.bounds.size.magnitude;
            if (before > 0f && after > before * 1.1f) g.m_Inspect.m_DistanceFromCamera *= after / before;
            var o = g.m_Inspect.m_Offset;
            g.m_Inspect.m_Offset = new Vector3(0f, o.y, 0f);
        }
        // the collider fits the new model
        if (mesh != null && g.GetComponent<BoxCollider>() is { } box)
        {
            var b = mesh.bounds;
            box.center = new Vector3(0f, b.center.y, 0f);
            box.size = new Vector3(Math.Max(b.size.x, .01f), Math.Max(b.size.y, .01f), Math.Max(b.size.z, .01f));
        }
    }

    /// <summary>The item's own lists of its renderers, which the game walks to hide it in the inventory and show it again: made anew.</summary>
    static void RefreshRenderers(GearItem g)
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

    /// <summary>What is left: cigarettes in a pack, portions in a pouch, lights in the lighter (by its condition); units in a stack; else 1.</summary>
    public static int Units(GearItem g)
    {
        if (Defs.Is(g, out var d) && d.Units > 1 && g.m_StackableItem == null)
            return Math.Max(0, (int)Math.Round(g.CurrentHP / MaxHP(g) * d.Units - .3f, MidpointRounding.AwayFromZero));
        return g.m_StackableItem != null ? g.m_StackableItem.m_Units : 1;
    }

    /// <summary>One out of the item; the item goes when it was the last. Returns what gives it back.</summary>
    public static Action? TakeOne(GearItem g)
    {
        if (Defs.Is(g, out var d) && d.Units > 1 && g.m_StackableItem == null)
        {
            float step = MaxHP(g) / d.Units;
            if (Units(g) > 1)
            {
                g.CurrentHP = Math.Max(0.01f, g.CurrentHP - step);
                return () =>
                {
                    try { if (g != null) g.CurrentHP = Math.Min(MaxHP(g), g.CurrentHP + step); }
                    catch (Exception e) { Safe.Log("Give back", e); }
                };
            }
            if (d.Kind == Kind.Lighter)
            {
                // 11-10: an empty lighter stays (a collectible, refilled with fluid or gas)
                float was = g.CurrentHP;
                g.CurrentHP = 0f;
                return () =>
                {
                    try { if (g != null) g.CurrentHP = was; }
                    catch (Exception e) { Safe.Log("Give back", e); }
                };
            }
            string last = d.Id;
            GameManager.GetInventoryComponent().DestroyGear(g.gameObject);
            return () =>
            {
                try { Give(last, 1); }
                catch (Exception e) { Safe.Log("Give back", e); }
            };
        }
        if (g.m_StackableItem != null && g.m_StackableItem.m_Units > 1)
        {
            g.m_StackableItem.m_Units--;
            return () =>
            {
                try { if (g != null && g.m_StackableItem != null) g.m_StackableItem.m_Units++; }
                catch (Exception e) { Safe.Log("Give back", e); }
            };
        }
        string? id = Defs.Is(g, out var d2) ? d2.Id : null;
        GameManager.GetInventoryComponent().DestroyGear(g.gameObject);
        return id == null ? null : () =>
        {
            try { Give(id); }
            catch (Exception e) { Safe.Log("Give back", e); }
        };
    }

    /// <summary>The first of ours of this kind in the pack.</summary>
    public static GearItem? Find(Func<Def, bool> pick)
    {
        var inv = GameManager.GetInventoryComponent();
        if (inv == null) return null;
        foreach (var o in inv.m_Items)
        {
            var gi = o?.m_GearItem;
            if (gi != null && Defs.Is(gi, out var d) && pick(d) && Units(gi) > 0) return gi;
        }
        return null;
    }

    public static void Give(string id, int units = 1)
    {
        var pm = GameManager.GetPlayerManagerComponent();
        var t = Spawn(id);
        if (t == null) return;
        float cond = 1f;
        if (t.m_StackableItem != null) t.m_StackableItem.m_Units = units;
        else if (Defs.TryGet(t.name, out var d) && d.Units > 1) cond = Math.Clamp((float)units / d.Units, .01f, 1f);
        var given = pm.InstantiateItemInPlayerInventoryFromPrefab(t, t.m_StackableItem != null ? units : 1, cond, PlayerManager.InventoryInstantiateFlags.None);
        if (given != null && cond < 1f) given.CurrentHP = MaxHP(given) * cond;
        UnityEngine.Object.Destroy(t.gameObject);
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
            MelonLogger.Error($"{__state.Id}: prefab {__state.Base} not found.");
            return;
        }
        try
        {
            Items.Customize(__result, __state, false);
        }
        catch (Exception e)
        {
            Safe.Log("Item " + __state.Id, e);
        }
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
        try
        {
            Items.Customize(__result, __state, true);
            if (__state.Kind == Kind.Lit) Smoking.Orphan(__result);
        }
        catch (Exception e)
        {
            Safe.Log("Saved item " + __state.Id, e);
        }
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
        try
        {
            Items.Customize(__result, __state, true);
            if (__state.Kind == Kind.Lit) Smoking.Orphan(__result);
        }
        catch (Exception e)
        {
            Safe.Log("Saved item " + __state.Id, e);
        }
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
        if (units <= 1 && template.m_StackableItem is { } stack) units = Math.Max(1, stack.m_DefaultUnitsInItem);
        __result = __instance.InstantiateItemInPlayerInventoryFromPrefab(template, units, normalizedCondition, flags);
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

/// <summary>The inventory icon wherever the game asks: copied item data keeps pointing at the source item's icon.</summary>
[HarmonyPatch]
internal static class IconPatches
{
    static Texture2D? Icon(string? name) => Defs.TryGet(name, out var def) ? Assets.Texture(def.Icon, compress: false) : null;

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

/// <summary>The inventory tile shows what is left as «20 сиг.», «150 зар.», not a pill icon and a bare number that reads as 20 packs.</summary>
[HarmonyPatch]
internal static class GridUnits
{
    static void Fix(InventoryGridItem g)
    {
        try
        {
            var gi = g.m_GearItem;
            if (gi == null || !Defs.Is(gi, out var def)) return;
            if (def.Kind == Kind.Lit)
            {
                // the smoke in the hand is an item of its own: marked as burning so it does not read as a second pipe or cigar
                if (g.m_UnitSprite != null && g.m_UnitSprite.gameObject.activeSelf) g.m_UnitSprite.gameObject.SetActive(false);
                if (g.m_UnitLabel != null)
                {
                    var lt = Texts.Get("SMOKES_UNIT_LIT_SHORT");
                    if (g.m_UnitLabel.text != lt) g.m_UnitLabel.text = lt;
                    if (!g.m_UnitLabel.gameObject.activeSelf) g.m_UnitLabel.gameObject.SetActive(true);
                }
                return;
            }
            if (def.UnitKey.Length == 0) return;
            if (g.m_UnitSprite != null && g.m_UnitSprite.gameObject.activeSelf) g.m_UnitSprite.gameObject.SetActive(false);
            if (g.m_UnitLabel != null)
            {
                int n = Items.Units(gi);
                // a single cigar or pipe needs no number
                string text = def.Units <= 1 && n <= 1 ? "" : n + " " + Texts.Get("SMOKES_UNIT_" + def.UnitKey + "_SHORT");
                if (g.m_UnitLabel.text != text) g.m_UnitLabel.text = text;
                if (!g.m_UnitLabel.gameObject.activeSelf) g.m_UnitLabel.gameObject.SetActive(true);
            }
        }
        catch (Exception e)
        {
            Safe.Log("Inventory units", e);
        }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(InventoryGridItem), nameof(InventoryGridItem.Refresh))]
    static void Refresh(InventoryGridItem __instance) => Fix(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(InventoryGridItem), nameof(InventoryGridItem.RefreshDataItem))]
    static void RefreshData(InventoryGridItem __instance) => Fix(__instance);

    [HarmonyPostfix, HarmonyPatch(typeof(InventoryGridItem), nameof(InventoryGridItem.UpdateConditionDisplay))]
    static void Condition(InventoryGridItem __instance) => Fix(__instance);
}
