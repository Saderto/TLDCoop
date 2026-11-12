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
            // the owner (11-12): the jar a little to the left in the inspect view
            if (!aquarium) g.m_Inspect.m_Offset = new Vector3(Tune.InspectLeft, 0f, 0f);
        }
    }
}
