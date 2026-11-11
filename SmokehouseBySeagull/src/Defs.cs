namespace SmokehouseBySeagull;

/// <summary>A kind of meat or fish the barrel takes.</summary>
public record Species(string Id, string Raw, string Cooked, bool Fish, string Ru, string En);

/// <summary>One item of the mod: a copy of a game prefab with our model, texture and values.</summary>
public class Def
{
    public string Id = "";
    public string Base = "";
    public string Mesh = "";
    public string Texture = "";
    public string IconId = "";
    public Species? Species;      // null: the barrel
    public bool Hot, Salted;
    public bool IsBarrel => Species == null;
    public string Icon => "icons/" + IconId + ".png";
    /// <summary>What is left of the raw weight after smoking.</summary>
    public float WeightFactor => Hot ? Tune.HotWeight : Tune.ColdWeight;
    /// <summary>How much slower than the game's cooked piece it spoils.</summary>
    public float DecayFactor => Hot ? 1f / Tune.HotKeeps : 0f;
}

/// <summary>The numbers the owner agreed to (11-11).</summary>
public static class Tune
{
    public const int Slots = 6;
    public const float HotHours = 32f, ColdHours = 52f;
    public const float HotWeight = .7f, ColdWeight = .55f;
    public const float HotKeeps = 5f;      // hot: spoils 80% slower than the game's cooked piece; cold never spoils
    public const float FuelPerBurnHour = 2f;      // a log that burns an hour in a fire smoulders two here
    public const float FuelMaxHours = 80f;
    public const int ColdSkillLevel = 4;          // Cooking level, as the game shows it (1..5)
    public const float SaltKg = .3f, BarrelKg = 15f;
    public const float ConditionGain = .25f;
}

public static class Defs
{
    public const string Prefix = "GEAR_SH_";
    public const string RefPrefix = "SEAGULLSMOKEHOUSE:";
    public const string Barrel = "Barrel";

    public static readonly Species[] AllSpecies =
    {
        new("Deer", "GEAR_RawMeatDeer", "GEAR_CookedMeatDeer", false, "Оленина", "venison"),
        new("Rabbit", "GEAR_RawMeatRabbit", "GEAR_CookedMeatRabbit", false, "Крольчатина", "rabbit"),
        new("Wolf", "GEAR_RawMeatWolf", "GEAR_CookedMeatWolf", false, "Волчатина", "wolf meat"),
        new("Bear", "GEAR_RawMeatBear", "GEAR_CookedMeatBear", false, "Медвежатина", "bear meat"),
        new("Moose", "GEAR_RawMeatMoose", "GEAR_CookedMeatMoose", false, "Лосятина", "moose meat"),
        new("Cougar", "GEAR_RawMeatCougar", "GEAR_CookedMeatCougar", false, "Мясо пумы", "cougar meat"),
        new("Ptarmigan", "GEAR_RawMeatPtarmigan", "GEAR_CookedMeatPtarmigan", false, "Куропатка", "ptarmigan"),
        new("Coho", "GEAR_RawCohoSalmon", "GEAR_CookedCohoSalmon", true, "Кижуч", "coho salmon"),
        new("WhiteFish", "GEAR_RawLakeWhiteFish", "GEAR_CookedLakeWhiteFish", true, "Сиг", "lake whitefish"),
        new("Trout", "GEAR_RawRainbowTrout", "GEAR_CookedRainbowTrout", true, "Радужная форель", "rainbow trout"),
        new("Bass", "GEAR_RawSmallMouthBass", "GEAR_CookedSmallMouthBass", true, "Малоротый окунь", "smallmouth bass"),
        new("Burbot", "GEAR_RawBurbot", "GEAR_CookedBurbot", true, "Налим", "burbot"),
        new("Goldeye", "GEAR_RawGoldeye", "GEAR_CookedGoldeye", true, "Золотоглазка", "goldeye"),
        new("Rockfish", "GEAR_RawRockfish", "GEAR_CookedRockfish", true, "Морской окунь", "rockfish"),
        new("IrishLord", "GEAR_RawRedIrishLord", "GEAR_CookedRedIrishLord", true, "Красный ирландский лорд", "red Irish lord"),
    };

    public static readonly List<Def> All = new();
    static readonly Dictionary<string, Def> byName = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Species> byRaw = new(StringComparer.OrdinalIgnoreCase);

    static Defs()
    {
        // the barrel is a copy of scrap metal: a plain item with a metal's sounds and nothing to use
        All.Add(new Def { Id = Barrel, Base = "GEAR_ScrapMetal", Mesh = "Barrel", Texture = "Barrel", IconId = "Barrel" });
        foreach (var s in AllSpecies)
        {
            byRaw[s.Raw] = s;
            foreach (bool hot in new[] { true, false })
                foreach (bool salted in new[] { false, true })
                {
                    string look = (hot ? "Hot" : "Cold") + s.Id;
                    All.Add(new Def { Id = look + (salted ? "_S" : ""), Base = s.Cooked, Mesh = look, Texture = look, IconId = look, Species = s, Hot = hot, Salted = salted });
                }
        }
        foreach (var d in All) byName[Prefix + d.Id] = d;
    }

    public static string Clean(string? name)
    {
        if (name == null) return "";
        int i = name.IndexOf("(Clone)", StringComparison.Ordinal);
        return (i >= 0 ? name.Substring(0, i) : name).Trim();
    }

    public static bool TryGet(string? name, out Def def)
    {
        def = null!;
        if (name == null || !name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return false;
        return byName.TryGetValue(Clean(name), out def!);
    }

    public static bool Is(GearItem? g, out Def def)
    {
        def = null!;
        return g != null && TryGet(g.name, out def);
    }

    public static bool IsBarrel(GearItem? g) => Is(g, out var d) && d.IsBarrel;

    public static Def? ById(string id) => byName.TryGetValue(Prefix + id, out var d) ? d : null;

    public static Species? ByRaw(string? gearName) => gearName != null && byRaw.TryGetValue(Clean(gearName), out var s) ? s : null;

    public static Species? SpeciesById(string id) => AllSpecies.FirstOrDefault(s => s.Id == id);

    public static string OutId(Species s, bool hot, bool salted) => (hot ? "Hot" : "Cold") + s.Id + (salted ? "_S" : "");
}
