namespace JellyfishByEMPEROR;

/// <summary>What each of our items is made of: our data, model and texture over the game prefab it copies.</summary>
public static partial class Items
{
    static readonly Dictionary<string, GearItemData> dataByName = new();

    /// <summary>restored: from a save, its weight, calories and condition are already right.</summary>
    public static void Customize(GearItem g, Def def, bool restored)
    {
        string name = Defs.Prefix + def.Id;
        g.gameObject.name = name;
        if (!dataByName.TryGetValue(name, out var data) || data == null)
        {
            // our own data object: the game tells item types apart by it, so sharing it would stack us with the source item
            data = UnityEngine.Object.Instantiate(g.m_GearItemData);
            data.name = "GearItemData_JF_" + def.Id;
            data.hideFlags = HideFlags.HideAndDontSave;
            data.m_LocalizedName = new LocalizedString { m_LocalizationID = "JELLYFISH_" + def.Id + "_NAME" };
            data.m_LocalizedDescription = new LocalizedString { m_LocalizationID = "JELLYFISH_" + def.Id + "_DESC" };
            data.m_PrefabReference = new AssetReferenceGearItem(Defs.RefPrefix + def.Id);
            if (def.IsDecor)
            {
                data.m_BaseWeight = ItemWeight.FromKilograms(def.Kg);
                data.m_DailyHPDecay = 0f;
                data.m_Type = GearType.Tool;
            }
            dataByName[name] = data;
        }
        g.m_GearItemData = data;

        switch (def.Kind)
        {
            case Kind.Jar:
            case Kind.Live:
            case Kind.Aquarium:
                Decor(g, def, restored);
                break;
        }
        RefreshRenderers(g);
    }

    static void Decor(GearItem g, Def def, bool restored)
    {
        g.WeightKG = ItemWeight.FromKilograms(def.Kg);
        if (!restored)
        {
            g.m_StartCondition = GearStartCondition.Perfect;
            g.CurrentHP = MaxHP(g);
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
        if (g.GetComponent<BoxCollider>() == null) g.gameObject.AddComponent<BoxCollider>();
        bool aquarium = def.Kind == Kind.Aquarium;
        SingleMesh(g, Assets.LoadMesh(aquarium ? "meshes/AquaFrame.fpmesh" : "meshes/JarLid.fpmesh"), Assets.Texture(aquarium ? "textures/AquaFrame.png" : "textures/JarLid.png"));
        // the collider: the whole jar, the whole aquarium (jellyfish are poured in through its window, nothing is set down inside it)
        if (g.GetComponent<BoxCollider>() is { } box)
        {
            if (aquarium) { box.center = new Vector3(0f, .22f, 0f); box.size = new Vector3(.64f, .44f, .38f); }
            else { box.center = new Vector3(0f, .09f, 0f); box.size = new Vector3(.11f, .18f, .11f); }
        }
        Visual.Attach(g, def);
        if (g.m_Inspect != null)
        {
            g.m_Inspect.m_DistanceFromCamera = aquarium ? 1.1f : .45f;
            // m_Offset did nothing visible (0.2.3): left as it was, logged; the jar is moved left by InspectShift
            Safe.Debug($"inspect offset of {g.name}: {g.m_Inspect.m_Offset}, angles {g.m_Inspect.m_Angles}");
        }
    }
}

/// <summary>The owner (11-12): in the inspect view the jar stood right of the middle, close to the text: moved left along the camera's right.
/// The game turns the item from where it stands, outside the methods patched here: a shift left in place was turned with it and added up
/// (0.2.6, 0.2.7: the jar flew off to the left). So before each of the game's own steps the item is put back exactly where the game last had it,
/// and only after the step is it moved left again.</summary>
[HarmonyPatch]
internal static class InspectShift
{
    static GearItem? shifted;
    static Vector3 gamePos;

    static void Undo()
    {
        try { if (shifted != null && shifted.transform != null) shifted.transform.position = gamePos; } catch { }
        shifted = null;
    }

    static void Apply(PlayerManager pm)
    {
        try
        {
            var g = pm.GearItemBeingInspected();
            if (g == null || !Defs.Is(g, out var d) || d.Kind == Kind.Aquarium) return;
            var cam = GameManager.GetMainCamera();
            if (cam == null) return;
            gamePos = g.transform.position;
            g.transform.position = gamePos + cam.transform.right * Tune.InspectLeft;
            shifted = g;
        }
        catch (Exception e) { Safe.Log("Inspect shift", e); }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.SetGearTransformForInspection))]
    static void SetBefore() => Undo();
    [HarmonyPostfix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.SetGearTransformForInspection))]
    static void SetAfter(PlayerManager __instance) => Apply(__instance);

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.UpdateInspectGear))]
    static void UpdateBefore() => Undo();
    [HarmonyPostfix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.UpdateInspectGear))]
    static void UpdateAfter(PlayerManager __instance) => Apply(__instance);

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.ExitInspectGearMode))]
    static void Exit() => Undo();
}
