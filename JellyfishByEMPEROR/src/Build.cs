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

/// <summary>The owner (11-12): in the inspect view the jar stood right of the middle and, when turned, went off to the side and nearer/further.
/// The game turns an item about its own point, and a jar's point is at its bottom: the jar swung round it. While a jar is looked at, everything
/// it is made of is lowered by half its height, so it turns about its middle and stays in the middle of the view (0.2.6–0.2.9 moved the item
/// itself, which the game's turning undid or added up).</summary>
[HarmonyPatch]
internal static class InspectCenter
{
    const float Half = .09f;
    static GearItem? cur;
    static readonly List<(Transform t, Vector3 lp)> moved = new();

    static Vector3 off;

    public static Vector3 Drop(GearItem g)
    {
        try { return cur != null && g != null && cur.Pointer == g.Pointer ? off : Vector3.zero; } catch { return Vector3.zero; }
    }

    /// <summary>Every frame while a jar is looked at: its parts sit half its height lower (it turns about its middle) and a little left and up
    /// on the screen (the owner, 11-12), worked out in the jar's own space so the game's turning cannot add it up.</summary>
    public static void Tick()
    {
        if (cur == null) return;
        try
        {
            var cam = GameManager.GetMainCamera();
            var t = cur.transform;
            off = new Vector3(0f, -Half, 0f);
            if (cam != null) off += t.InverseTransformVector(cam.transform.right * Tune.InspectLeft + cam.transform.up * Tune.InspectUp);
            foreach (var (c, lp) in moved) if (c != null) c.localPosition = lp + off;
        }
        catch (Exception e) { Safe.Log("Inspect centre tick", e); cur = null; }
    }

    static void Restore()
    {
        foreach (var (t, lp) in moved) try { if (t != null) t.localPosition = lp; } catch { }
        moved.Clear();
        cur = null;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.SetGearTransformForInspection))]
    static void Set(PlayerManager __instance)
    {
        try
        {
            Restore();
            var g = __instance.GearItemBeingInspected();
            if (g == null || !Defs.Is(g, out var d) || d.Kind == Kind.Aquarium) return;
            var t = g.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                moved.Add((c, c.localPosition));
            }
            cur = g;
            Tick();
        }
        catch (Exception e) { Safe.Log("Inspect centre", e); }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.ExitInspectGearMode))]
    static void Exit() => Restore();
}
