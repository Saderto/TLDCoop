using UnityEngine.AddressableAssets;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace NoodlesByEMPEROR;

/// <summary>A mod item is a copy of a game prefab with our model, label and values (the scheme of the other EMPEROR mods, with our own prefix and reference).
/// Dry noodles are copies of the green tea package (cooked in a pot with water, as tea is); brewed noodles of the cup of tea
/// (hot: the tea's warmth, scaled; eaten, not drunk).</summary>
public static class Items
{
    static readonly Dictionary<string, GearItemData> dataByName = new();
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

    /// <summary>A hidden copy kept alive: what a trade shows and gives.</summary>
    public static GearItem? Template(string id)
    {
        if (templates.TryGetValue(id, out var t) && t != null) return t;
        t = Spawn(id);
        if (t == null) return null;
        t.gameObject.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(t.gameObject);
        return templates[id] = t;
    }

    static GameObject? shelf;
    static readonly Dictionary<string, GearItem> prefabs = new();

    /// <summary>What the pot makes of dry noodles: an item that stays switched on itself but sits under a switched-off holder, so it never shows or runs,
    /// while a copy the game makes of it comes out switched on (as a copy of a real prefab does).</summary>
    public static GearItem? CookedPrefab(string id)
    {
        if (prefabs.TryGetValue(id, out var p) && p != null) return p;
        if (shelf == null)
        {
            shelf = new GameObject("NoodlesByEMPEROR_Prefabs");
            shelf.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(shelf);
            shelf.hideFlags = HideFlags.HideAndDontSave;
        }
        p = Spawn(id);
        if (p == null) return null;
        p.transform.SetParent(shelf.transform, false);
        p.gameObject.name = Defs.Prefix + id;
        return prefabs[id] = p;
    }

    public static bool IsHidden(GearItem g)
    {
        foreach (var t in templates.Values) if (t != null && t.Pointer == g.Pointer) return true;
        foreach (var t in prefabs.Values) if (t != null && t.Pointer == g.Pointer) return true;
        return false;
    }

    static float teaWarmDecrease = -1f, teaWarmHours = -1f;

    /// <summary>restored: from a save, its condition and calories left are already right.</summary>
    public static void Customize(GearItem g, Def def, bool restored)
    {
        g.gameObject.name = def.Gear;
        if (!dataByName.TryGetValue(def.Gear, out var data) || data == null)
        {
            // Our own data object: the game tells item types apart by it, so sharing it would stack us with the source item.
            data = UnityEngine.Object.Instantiate(g.m_GearItemData);
            data.name = "GearItemData_NDL_" + def.Id;
            data.hideFlags = HideFlags.HideAndDontSave;
            data.m_LocalizedName = new LocalizedString { m_LocalizationID = "NOODLES_" + def.Id + "_NAME" };
            data.m_LocalizedDescription = new LocalizedString { m_LocalizationID = "NOODLES_" + def.Id + "_DESC" };
            data.m_PrefabReference = new AssetReferenceGearItem(Defs.RefPrefix + def.Id);
            data.m_BaseWeight = ItemWeight.FromKilograms(def.Kg);
            dataByName[def.Gear] = data;
        }
        g.m_GearItemData = data;
        g.WeightKG = ItemWeight.FromKilograms(def.Kg);
        g.transform.localScale = Vector3.one;

        switch (def.Kind)
        {
            case Kind.Noodle: Noodle(g, def, restored); break;
            case Kind.Brewed: Brewed(g, def, restored); break;
        }

        var tex = Assets.Texture("textures/" + def.Texture + ".jpg", compress: true);
        // Open shapes (cups, bowls, trays, every cooked one) get their faces a second time turned inside out: the game does not draw a face from
        // its back, and through the underside of a rolled rim one could see right through the wall (user 11-15: «прозрачность сверху во внутренней стороне»).
        var mesh = Assets.LoadMesh("meshes/" + def.Mesh + ".fpmesh", inside: def.Kind == Kind.Brewed || !def.Pack);
        SingleMesh(g, mesh, tex);
        RefreshRenderers(g);
    }

    static void SetCalories(GearItem g, float kcal, bool restored)
    {
        if (g.m_FoodItem is not { } fi) return;
        if (restored)
        {
            // keep the share already eaten
            float share = fi.m_CaloriesTotal > 0f ? fi.m_CaloriesRemaining / fi.m_CaloriesTotal : 1f;
            fi.m_CaloriesTotal = kcal;
            fi.m_CaloriesRemaining = kcal * Mathf.Clamp01(share);
        }
        else
        {
            fi.m_CaloriesTotal = kcal;
            fi.m_CaloriesRemaining = kcal;
        }
    }

    static void NoStack(GearItem g)
    {
        if (g.m_StackableItem is { } st)
        {
            UnityEngine.Object.DestroyImmediate(st);
            g.m_StackableItem = null;
        }
    }

    static bool loggedNoodle, loggedBrewed;

    static void Noodle(GearItem g, Def def, bool restored)
    {
        // one block per item, stacks of the same brand are fine
        if (g.m_StackableItem is { } st)
        {
            st.m_DefaultUnitsInItem = 1;
            if (!restored) st.m_Units = 1;
        }
        var c = g.m_Cookable;
        if (c == null) { MelonLogger.Error($"{def.Id}: the tea package has no Cookable"); return; }
        c.m_PotableWaterRequired = ItemLiquidVolume.FromLiters(def.Water);
        c.m_NumUnitsRequired = 1;
        var cooked = CookedPrefab(def.Brewed);
        if (cooked != null) c.m_CookedPrefab = cooked;
        if (!loggedNoodle)
        {
            loggedNoodle = true;
            Safe.Info($"Noodle {def.Id}: water {def.Water} L, cook {c.m_CookTimeMinutes} min, ready {c.m_ReadyTimeMinutes} min, type {c.m_CookableType}, cooked prefab {(cooked != null ? cooked.name : "none")}, stack {(g.m_StackableItem != null)}");
        }
    }

    static void Brewed(GearItem g, Def def, bool restored)
    {
        NoStack(g);
        SetCalories(g, def.Kcal, restored);
        if (g.m_FoodItem is { } fi)
        {
            fi.m_IsDrink = false;
            fi.m_ReduceThirst = def.Thirst;
            fi.m_TimeToEatSeconds = 6f;
            fi.m_TimeToOpenAndEatSeconds = 6f;
            // a meal goes off in a few days, as cooked food does (the tea's cup never spoils)
            if (fi.m_DailyHPDecayInside < 5f) { fi.m_DailyHPDecayInside = 8f; fi.m_DailyHPDecayOutside = 6f; }
        }
        // the tea's warmth, scaled; every other buff the tea carries goes
        foreach (var comp in g.GetComponents<Component>())
        {
            if (comp == null) continue;
            string tn = comp.GetIl2CppType().Name;
            if (tn == "FreezingBuff")
            {
                var fb = comp.Cast<FreezingBuff>();
                if (teaWarmDecrease < 0f) { teaWarmDecrease = fb.m_InitialPercentDecrease; teaWarmHours = fb.m_DurationHours; }
                fb.m_InitialPercentDecrease = teaWarmDecrease * def.Warm;
                fb.m_DurationHours = teaWarmHours * Mathf.Max(1f, def.Warm);
            }
            else if (tn.EndsWith("Buff", StringComparison.Ordinal))
            {
                if (!loggedBrewed) Safe.Info($"Brewed: tea's {tn} removed");
                UnityEngine.Object.DestroyImmediate(comp);
            }
        }
        if (!loggedBrewed)
        {
            loggedBrewed = true;
            Safe.Info($"Brewed {def.Id}: warmth {teaWarmDecrease}% for {teaWarmHours} h x{def.Warm}, food {(g.m_FoodItem != null)}");
        }
    }

    static readonly HashSet<string> inspectLogged = new();

    /// <summary>One model and one texture in place of the source item's parts.</summary>
    static void SingleMesh(GearItem g, Mesh? mesh, Texture2D? tex)
    {
        if (g.GetComponent<LODGroup>() is { } lod) UnityEngine.Object.DestroyImmediate(lod);
        var filters = g.GetComponentsInChildren<MeshFilter>(true);
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
                    if (tex != null)
                    {
                        var m = r.material;
                        m.mainTexture = tex;
                        Plain(m);
                    }
                }
            }
            else
            {
                if (r != null) UnityEngine.Object.DestroyImmediate(r);
                UnityEngine.Object.DestroyImmediate(f);
            }
        }
        // skinned or extra parts of the source (a tea bag string, a lid) go too
        foreach (var sr in g.GetComponentsInChildren<SkinnedMeshRenderer>(true)) UnityEngine.Object.DestroyImmediate(sr);
        if (mesh != null && g.m_Inspect != null)
        {
            try
            {
                var ins = g.m_Inspect;
                ins.m_InspectModeMesh = null;
                ins.m_NormalMesh = null;
                float k = Mathf.Max(.1f, Mathf.Abs(ins.m_Scale.x));
                float want = mesh.bounds.size.magnitude * k * 3.6f;
                if (inspectLogged.Add(g.name)) Safe.Info($"inspect {g.name}: size {mesh.bounds.size.magnitude:0.00} m, inspect scale {k:0.00}, distance {ins.m_DistanceFromCamera:0.00} -> {Mathf.Max(want, ins.m_DistanceFromCamera):0.00}");
                if (want > ins.m_DistanceFromCamera) ins.m_DistanceFromCamera = want;
            }
            catch (Exception e) { Safe.Log("inspect " + g.name, e); }
        }
        if (mesh != null && g.GetComponent<BoxCollider>() is { } box)
        {
            var b = mesh.bounds;
            box.center = b.center;
            box.size = new Vector3(Math.Max(b.size.x, .01f), Math.Max(b.size.y, .01f), Math.Max(b.size.z, .01f));
        }
    }

    /// <summary>Our painting alone on the source's material: its other maps were laid out for the source model.</summary>
    static void Plain(Material m)
    {
        try
        {
            var main = m.mainTexture;
            foreach (var n in m.GetTexturePropertyNames())
            {
                var t = m.GetTexture(n);
                if (t != null && main != null && t.Pointer != main.Pointer) m.SetTexture(n, null);
            }
            // not pure white: the game lights items brightly and a white print washed out to a blank
            if (m.HasProperty("_Color")) m.color = new Color(.74f, .74f, .74f, 1f);
        }
        catch (Exception e) { Safe.Log("Plain material", e); }
    }

    static void RefreshRenderers(GearItem g)
    {
        var mr = new List<MeshRenderer>();
        foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true)) if (r != null) mr.Add(r);
        g.m_MeshRenderers = new Il2CppReferenceArray<MeshRenderer>(mr.ToArray());
        g.m_SkinnedMeshRenderers = new Il2CppReferenceArray<SkinnedMeshRenderer>(Array.Empty<SkinnedMeshRenderer>());
    }

    public static void Tick() { }
}

/// <summary>The pot turns our dry noodles into the game's cooked item: if that came out as a cup of tea (the game made it from the tea's own reference
/// rather than from our prefab), it is turned into our brewed noodles here.</summary>
[HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.SetCookedGearProperties))]
internal static class CookedPatch
{
    static void Postfix(GearItem rawItem, GearItem cookedItem)
    {
        try
        {
            if (cookedItem == null) return;
            if (Defs.Is(cookedItem, out var cooked))
            {
                cookedItem.gameObject.name = cooked.Gear;
                Safe.Info($"Brewed {cooked.Id} from the pot");
                return;
            }
            if (rawItem != null && Defs.Is(rawItem, out var raw) && raw.Kind == Kind.Noodle && Defs.ById(raw.Brewed) is { } hot)
            {
                Items.Customize(cookedItem, hot, false);
                Safe.Info($"Brewed {hot.Id} from the pot (turned from {cookedItem.name})");
            }
        }
        catch (Exception e) { Safe.Log("Cooked noodles", e); }
    }
}
