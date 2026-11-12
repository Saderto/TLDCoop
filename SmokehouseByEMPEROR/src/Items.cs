using UnityEngine.AddressableAssets;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SmokehouseByEMPEROR;

/// <summary>A mod item is a copy of a game prefab with our model, texture and values. The game finds items by name, so those lookups are intercepted
/// (the same scheme as SmokesByEMPEROR and FoodPackByEMPEROR, with our own prefix and reference so the mods never claim each other's items).</summary>
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

    /// <summary>restored: from a save, its weight, calories and condition are already right.</summary>
    public static void Customize(GearItem g, Def def, bool restored)
    {
        string name = Defs.Prefix + def.Id;
        g.gameObject.name = name;
        if (!dataByName.TryGetValue(name, out var data) || data == null)
        {
            // Our own data object: the game tells item types apart by it, so sharing it would stack us with the source item.
            data = UnityEngine.Object.Instantiate(g.m_GearItemData);
            data.name = "GearItemData_SH_" + def.Id;
            data.hideFlags = HideFlags.HideAndDontSave;
            data.m_LocalizedName = new LocalizedString { m_LocalizationID = "SMOKEHOUSE_" + def.Id + "_NAME" };
            data.m_LocalizedDescription = new LocalizedString { m_LocalizationID = "SMOKEHOUSE_" + def.Id + "_DESC" };
            data.m_PrefabReference = new AssetReferenceGearItem(Defs.RefPrefix + def.Id);
            if (def.Bulky)
            {
                data.m_BaseWeight = ItemWeight.FromKilograms(def.Kg);
                data.m_DailyHPDecay = 0f;
                data.m_Type = GearType.Tool;
            }
            else data.m_DailyHPDecay *= def.Decay;
            if (def.Empty == Defs.Can)
            {
                // 11-12 owner: our cans sounded like raw meat when picked up. The sounds of the game's own tinned food instead
                try
                {
                    var tin = GearItem.InstantiateGearItem("GEAR_PinnacleCanPeaches");
                    var td = tin != null ? tin.m_GearItemData : null;
                    if (td != null) { data.m_PickupAudio = td.m_PickupAudio; data.m_StowAudio = td.m_StowAudio; data.m_PutBackAudio = td.m_PutBackAudio; }
                    if (tin != null) UnityEngine.Object.Destroy(tin.gameObject);
                    MelonLogger.Msg($"Can {def.Id}: sounds of the game's tin {(td != null ? "taken" : "not found")}");
                }
                catch (Exception e) { Safe.Log("Can sounds", e); }
            }
            dataByName[name] = data;
        }
        g.m_GearItemData = data;

        if (def.Bulky)
        {
            g.WeightKG = ItemWeight.FromKilograms(def.Kg);
            if (!restored)
            {
                g.m_StartCondition = GearStartCondition.Perfect;
                g.CurrentHP = data.m_MaxHP > 0f ? data.m_MaxHP : 100f;
            }
            if (g.m_StackableItem is { } st)
            {
                UnityEngine.Object.DestroyImmediate(st);
                g.m_StackableItem = null;
            }
            void Strip<T>() where T : Component
            {
                foreach (var c in g.GetComponentsInChildren<T>(true)) UnityEngine.Object.DestroyImmediate(c);
            }
            Strip<FuelSourceItem>();
            Strip<Harvest>();
            g.m_FuelSourceItem = null;
            g.m_Harvest = null;
            g.transform.localScale = Vector3.one;
            // a box of the barrel's size on the item itself: whatever collider the scrap had fits a handful of scrap
            if (g.GetComponent<BoxCollider>() == null) g.gameObject.AddComponent<BoxCollider>();
        }
        else Food(g, def, restored);

        // Our own dark paintings (stations, cans, sausages) stay uncompressed: the game's DXT packing keeps 5-6 bits per colour and turned dark browns
        // into red and green grain (11-12 owner: «как текстура из хоррора на телефон»). The smoked pieces of the game's own meat pack well.
        bool pieces = def.Species != null && def.Kind == null && !def.Bulky;
        var tex = Assets.Texture("textures/" + def.Texture + ".png", compress: pieces);
        var mesh = Assets.LoadMesh("meshes/" + def.Mesh + ".fpmesh", def.Bulky);      // stations are seen from all sides and from inside: every face is drawn from behind too
        SingleMesh(g, mesh, tex, def.Bulky);
        RefreshRenderers(g);
    }

    static bool loggedFood;

    /// <summary>The cooked piece's food values turned into the smoked one's: lighter for the same calories, slower to spoil, hot smoke leaves no parasites.</summary>
    static void Food(GearItem g, Def def, bool restored)
    {
        var fi = g.m_FoodItem;
        if (fi == null) return;
        float f = def.Weight;
        fi.m_DailyHPDecayInside *= def.Decay;
        fi.m_DailyHPDecayOutside *= def.Decay;
        fi.m_HeatPercent = 0f;
        try
        {
            // a raw sausage is smoked, never fried into a steak
            if (def.NoCook && g.m_Cookable != null) { UnityEngine.Object.DestroyImmediate(g.m_Cookable); g.m_Cookable = null; }
            // a sealed tin does not smell
            if (def.NoScent && g.m_Scent != null) { UnityEngine.Object.DestroyImmediate(g.m_Scent); g.m_Scent = null; }
        }
        catch (Exception e) { Safe.Log("Food parts " + def.Id, e); }
        if (def.NoParasites && fi.m_ParasiteRiskPercentIncrease is { } risk && risk.Length > 0)
        {
            // the same length, all zero: the game indexes it by how many pieces were eaten
            var none = new Il2CppStructArray<float>(risk.Length);
            for (int i = 0; i < risk.Length; i++) none[i] = 0f;
            fi.m_ParasiteRiskPercentIncrease = none;
        }
        var fw = g.m_FoodWeight;
        float perKg = 0f, rawKg = 1f;
        if (fw != null)
        {
            float lo = fw.m_MinWeight.ToQuantity(1f), hi = fw.m_MaxWeight.ToQuantity(1f);
            perKg = fw.m_CaloriesPerKG;
            rawKg = Math.Max(.05f, (lo + hi) / 2f);
            fw.m_MinWeight = ItemWeight.FromKilograms(lo * f);
            fw.m_MaxWeight = ItemWeight.FromKilograms(hi * f);
            fw.m_CaloriesPerKG = perKg / f;
        }
        if (!restored)
        {
            // a piece made out of nothing (console, trader): the middle of the game's range; one out of the barrel is set after this by what went in
            float cal = perKg > 0f ? perKg * rawKg : fi.m_CaloriesTotal;
            SetFood(g, rawKg * f + def.Tare, cal);
            g.m_StartCondition = GearStartCondition.Perfect;
            g.CurrentHP = MaxHP(g);
        }
        if (!loggedFood)
        {
            loggedFood = true;
            Safe.Info($"Food {def.Id}: base {def.Base}, {perKg:0} kcal/kg, raw {rawKg:0.00} kg, weight now {g.WeightKG.ToQuantity(1f):0.00} kg, {fi.m_CaloriesRemaining:0}/{fi.m_CaloriesTotal:0} kcal, decay {fi.m_DailyHPDecayInside:0.00}/{fi.m_DailyHPDecayOutside:0.00}, food weight {(fw != null)}");
        }
    }

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
    static readonly HashSet<string> plainLogged = new();

    /// <summary>Our painting alone on the source item's material: whatever other pictures it carries (a detail, a relief, a mask laid out for the source
    /// model) are taken off. On our own layout they showed as red and green grain all over the table (11-12 owner).</summary>
    static void Plain(Material m, string name)
    {
        try
        {
            var main = m.mainTexture;
            var names = m.GetTexturePropertyNames();
            var said = new List<string>();
            foreach (var n in names)
            {
                var t = m.GetTexture(n);
                if (t == null) continue;
                said.Add(n + "=" + t.name);
                if (t.Pointer != main.Pointer) m.SetTexture(n, null);
            }
            if (plainLogged.Add(name)) MelonLogger.Msg($"material of {name}: shader {m.shader?.name}, colour {(m.HasProperty("_Color") ? m.color.ToString() : "none")}, textures {string.Join(", ", said)}, keywords {string.Join(" ", m.shaderKeywords)}");
        }
        catch (Exception e) { Safe.Log("Plain material " + name, e); }
    }

    static readonly HashSet<string> inspectLogged = new();
    static void SingleMesh(GearItem g, Mesh? mesh, Texture2D? tex, bool plain = false)
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
                    if (tex != null)
                    {
                        var m = r.material;
                        m.mainTexture = tex;
                        if (plain) Plain(m, g.name);
                    }
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
                // 0.4.5 set the scale to one, yet the game's own is 0.5 for every item (log 11-12): everything was shown twice its size and a big fish
                // filled the screen (11-12 owner). The game's scale stays; the distance is what the model's size asks for.
                ins.m_InspectModeMesh = null;
                ins.m_NormalMesh = null;
                float k = Mathf.Max(.1f, Mathf.Abs(ins.m_Scale.x));
                float world = Mathf.Max(.05f, Mathf.Abs(g.transform.lossyScale.x));
                float want = mesh.bounds.size.magnitude * world * k * 3.6f;
                if (inspectLogged.Add(g.name)) MelonLoader.MelonLogger.Msg($"inspect {g.name}: size {mesh.bounds.size.magnitude:0.00} m, item scale {world:0.00}, inspect scale {k:0.00}, distance {ins.m_DistanceFromCamera:0.00} -> {Mathf.Max(want, ins.m_DistanceFromCamera):0.00}");
                if (want > ins.m_DistanceFromCamera) ins.m_DistanceFromCamera = want;
            }
            catch (Exception e) { Safe.Log("inspect " + g.name, e); }
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

/// <summary>Our pieces never get the game's random weight: theirs comes from what went into the barrel.</summary>
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
        if (name != null && name.StartsWith("GearItemData_SH_", StringComparison.Ordinal)) name = Defs.Prefix + name.Substring("GearItemData_SH_".Length);
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

/// <summary>A can eaten to the end leaves its empty tin in the pack (our cans are copies of cooked meat, which leaves nothing).</summary>
[HarmonyPatch]
internal static class EmptyTin
{
    static IntPtr last;
    static int lastFrame;

    static void Check(GameObject? go)
    {
        try
        {
            var g = go != null ? go.GetComponent<GearItem>() : null;
            if (g == null || !Defs.Is(g, out var def) || def.Empty.Length == 0 || !g.m_InPlayerInventory || Items.IsTemplate(g)) return;
            var fi = g.m_FoodItem;
            if (fi == null || fi.m_CaloriesRemaining > 1f) return;
            if (g.Pointer == last && Time.frameCount - lastFrame < 5) return;
            last = g.Pointer; lastFrame = Time.frameCount;
            GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory(def.Empty, 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
            Safe.Debug("Empty tin left by " + def.Id);
        }
        catch (Exception e) { Safe.Log("Empty tin", e); }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Inventory), nameof(Inventory.DestroyGear), new[] { typeof(GameObject) })]
    static void ByObject(GameObject go) => Check(go);

    [HarmonyPrefix, HarmonyPatch(typeof(Inventory), nameof(Inventory.DestroyGear), new[] { typeof(GearItem) })]
    static void ByItem(GearItem gi) => Check(gi != null ? gi.gameObject : null);
}
