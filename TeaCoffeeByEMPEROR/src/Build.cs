using System.Reflection;

namespace TeaCoffeeByEMPEROR;

/// <summary>What each of our items is made of: our data, model, texture and numbers over the game prefab it copies.</summary>
public static partial class Items
{
    static readonly Dictionary<string, GearItemData> dataByName = new();

    /// <summary>restored: from a save, its servings, calories and condition are already right.</summary>
    public static void Customize(GearItem g, Def def, bool restored)
    {
        string name = Defs.Prefix + def.Id;
        g.gameObject.name = name;
        if (!dataByName.TryGetValue(name, out var data) || data == null)
        {
            // our own data object: the game tells item types apart by it, so sharing it would stack us with the source item
            data = UnityEngine.Object.Instantiate(g.m_GearItemData);
            data.name = Defs.DataPrefix + def.Id;
            data.hideFlags = HideFlags.HideAndDontSave;
            data.m_LocalizedName = new LocalizedString { m_LocalizationID = "TEACOFFEE_" + def.Id + "_NAME" };
            data.m_LocalizedDescription = new LocalizedString { m_LocalizationID = "TEACOFFEE_" + def.Id + "_DESC" };
            data.m_PrefabReference = new AssetReferenceGearItem(Defs.RefPrefix + def.Id);
            if (def.Kind == Kind.Pack) data.m_BaseWeight = ItemWeight.FromKilograms(def.P.Kg);
            dataByName[name] = data;
        }
        g.m_GearItemData = data;
        g.transform.localScale = Vector3.one;
        SingleMesh(g, Assets.LoadMesh(def.Mesh, inside: def.Kind == Kind.Cup), Assets.Texture(def.Texture));
        switch (def.Kind)
        {
            case Kind.Pack: Pack(g, def, restored); break;
            case Kind.Cup: Drink(g, def, restored); break;
            case Kind.Can: Drink(g, def, restored); break;
        }
        RefreshRenderers(g);
    }

    static void Pack(GearItem g, Def def, bool restored)
    {
        g.WeightKG = ItemWeight.FromKilograms(def.P.Kg);
        if (g.m_StackableItem is { } st)
        {
            st.m_DefaultUnitsInItem = def.P.Servings;
            if (!restored) st.m_Units = def.P.Servings;
        }
        else Safe.Once("nostack" + def.Base, $"{def.Base} has no stack: one serving per pack");
        var cook = g.GetComponent<Cookable>();
        var cup = Template(def.Id + "Cup");
        if (cook != null && cup != null) cook.m_CookedPrefab = cup;
        else MelonLogger.Error($"{def.Id}: {(cook == null ? "no Cookable on " + def.Base : "cup template missing")}");
    }

    static void Drink(GearItem g, Def def, bool restored)
    {
        var p = def.P;
        var fi = g.m_FoodItem;
        if (fi != null)
        {
            // never zero: the game shares a drink's buffs out by the calories drunk, and nothing of nothing gives no buffs at all
            float kcal = Mathf.Max(p.Kcal, 5f);
            fi.m_CaloriesTotal = kcal;
            if (!restored || fi.m_CaloriesRemaining <= 0f) fi.m_CaloriesRemaining = kcal;
            fi.m_ReduceThirst = p.Thirst;
            if (p.Cold) fi.m_HeatedWhenCooked = false;
            var basis = Buffs.Game(def.Base)?.m_FoodItem;
            if (Effects.Has(def, "longheat") && basis != null)
            {
                fi.m_PercentHeatLossPerMinuteIndoors = basis.m_PercentHeatLossPerMinuteIndoors * .5f;
                fi.m_PercentHeatLossPerMinuteOutdoors = basis.m_PercentHeatLossPerMinuteOutdoors * .5f;
            }
        }
        Buffs.Apply(g, def);
    }
}

/// <summary>The effects are the game's own buffs, so the game shows and saves them itself and a coop friend gets them the usual way.
/// Each is copied from the game item that has it and scaled: warmth from the item's own (tea or coffee), alertness from the game's coffee,
/// rest and condition over time from whichever game drink carries them (looked up once, logged).</summary>
public static class Buffs
{
    static readonly Dictionary<string, GearItem?> refs = new();
    static readonly string[] Candidates =
    {
        "GEAR_RoseHipTea", "GEAR_ReishiTea", "GEAR_BirchbarkTea", "GEAR_HerbalTea", "GEAR_BurdockTea", "GEAR_GreenTeaCup", "GEAR_CoffeeCup",
        "GEAR_AcornCoffeeCup", "GEAR_CookedPumpkinSpiceLatte", "GEAR_MapleSyrup",
    };

    public static GearItem? Game(string prefab)
    {
        if (refs.TryGetValue(prefab, out var g)) return g;
        try
        {
            g = GearItem.InstantiateGearItem(prefab);
            if (g != null)
            {
                g.gameObject.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(g.gameObject);
            }
        }
        catch (Exception e) { Safe.Log("Game item " + prefab, e); g = null; }
        return refs[prefab] = g;
    }

    static T? Find<T>(string what) where T : Component
    {
        foreach (var c in Candidates)
        {
            var g = Game(c);
            var comp = g != null ? g.GetComponent<T>() : null;
            if (comp != null)
            {
                Safe.Once("ref" + what, $"{what} taken from {c}");
                return comp;
            }
        }
        Safe.Once("ref" + what, $"No game drink has {what}: left out");
        return null;
    }

    /// <summary>A copy of a game component: every value the interop exposes as a read/write property on its own type.</summary>
    static T Copy<T>(T src, GameObject to) where T : Component
    {
        var dst = to.GetComponent<T>() ?? to.AddComponent<T>();
        foreach (var pr in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (!pr.CanRead || !pr.CanWrite || pr.GetIndexParameters().Length > 0) continue;
            try { pr.SetValue(dst, pr.GetValue(src)); } catch { }
        }
        return dst;
    }

    static void Remove<T>(GearItem g) where T : Component
    {
        foreach (var c in g.GetComponentsInChildren<T>(true)) UnityEngine.Object.DestroyImmediate(c);
    }

    public static void Apply(GearItem g, Def def)
    {
        var p = def.P;
        // what the source item brought that is not ours
        g.m_ConditionRestBuff = null; Remove<ConditionRestBuff>(g);
        g.m_ConditionOverTimeBuff = null; Remove<ConditionOverTimeBuff>(g);
        // warmth
        if (p.Warm <= 0f || p.Cold)
        {
            g.m_FreezingBuff = null; Remove<FreezingBuff>(g);
        }
        else
        {
            var src = g.m_FreezingBuff ?? Game("GEAR_CoffeeCup")?.m_FreezingBuff;
            if (src != null)
            {
                var fb = g.m_FreezingBuff ?? Copy(src, g.gameObject);
                var basis = Game(def.Base)?.m_FreezingBuff ?? src;
                fb.m_InitialPercentDecrease = basis.m_InitialPercentDecrease * p.Warm;
                fb.m_RateOfIncreaseScale = basis.m_RateOfIncreaseScale;
                fb.m_DurationHours = p.WarmHours > 0f ? p.WarmHours : basis.m_DurationHours;
                g.m_FreezingBuff = fb;
            }
        }
        // alertness: the game's coffee times ours
        if (p.Awake <= 0f)
        {
            g.m_FatigueBuff = null; Remove<FatigueBuff>(g);
        }
        else
        {
            var src = Game("GEAR_CoffeeCup")?.m_FatigueBuff;
            if (src != null)
            {
                var fb = g.m_FatigueBuff ?? Copy(src, g.gameObject);
                fb.m_InitialPercentDecrease = src.m_InitialPercentDecrease * p.Awake;
                fb.m_RateOfIncreaseScale = src.m_RateOfIncreaseScale;
                fb.m_DurationHours = src.m_DurationHours;
                fb.m_HUD = src.m_HUD;
                g.m_FatigueBuff = fb;
            }
        }
        if (p.Rest && Find<ConditionRestBuff>("rest buff") is { } rest) g.m_ConditionRestBuff = Copy(rest, g.gameObject);
        if (p.Heal && Find<ConditionOverTimeBuff>("condition over time") is { } heal) g.m_ConditionOverTimeBuff = Copy(heal, g.gameObject);
        Safe.Once("fx" + def.Id, $"{def.Id}: warm {g.m_FreezingBuff?.m_InitialPercentDecrease:0.#}% {g.m_FreezingBuff?.m_DurationHours:0.#}h, awake {g.m_FatigueBuff?.m_InitialPercentDecrease:0.#}%, rest {g.m_ConditionRestBuff != null}, heal {g.m_ConditionOverTimeBuff != null}");
    }
}
