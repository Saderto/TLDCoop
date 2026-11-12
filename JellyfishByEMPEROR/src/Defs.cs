namespace JellyfishByEMPEROR;

/// <summary>A colour of a species: its texture, how often it comes up and the colour of its glow.</summary>
public record Colour(string Id, string Ru, string En, int Weight, Color Glow);

/// <summary>A species: how often it comes up, whether it rises only at night, and how it sits in the jar (its size there and the height of its centre),
/// worked out from each model so it fills the water without touching the glass.</summary>
public record Species(string Id, string Ru, string En, int Weight, bool NightOnly, float JarK, float Y, Colour[] Colours);

public enum Kind { Jar, Live, Aquarium }

/// <summary>One item of the mod: a copy of a game prefab with our model, texture and values.</summary>
public class Def
{
    public string Id = "";
    public string Base = "";
    public Kind Kind;
    public Species? Species;
    public Colour? Colour;
    public float Kg;
    public string Icon => "icons/" + Id + ".png";
    /// <summary>Stands in the world as decoration: no stack, own weight, «Поставить» in the pack.</summary>
    public bool IsDecor => Kind is Kind.Jar or Kind.Live or Kind.Aquarium;
}

/// <summary>The numbers of the mod (11-12, my choice unless the owner said otherwise).</summary>
public static class Tune
{
    // the owner (11-12): the jellyfish stays in the fresh hole 3 to 7 seconds, then sinks
    public const float ShowMin = 3f, ShowMax = 7f;
    public const float JarKg = .35f, LiveKg = 1.1f, AquariumKg = 14f;
    // the owner (11-12): below −5° the jellyfish dies, from full condition in 90 minutes; in the warmth it comes back slowly
    public const float FreezeAt = -5f, LossPerHour = 100f / 1.5f, GainPerHour = 6f, PackWarmth = 2f, InspectLeft = -.05f, InspectUp = .085f;
    // the owner (11-12): no more than three in one aquarium
    public const int TankMax = 3;
    public const float AquariumScale = 2.4f, HoleScale = 2.2f;
    public const float JarLoot = 4f, JarLootCoast = 10f;       // % of containers searched for the first time
}

public static class Defs
{
    public const string Prefix = "GEAR_JF_";
    public const string RefPrefix = "EMPERORJELLYFISH:";
    static Color C(float r, float g, float b) => new(r, g, b, 1f);

    // rarity: the first colour is the common one, the last the rare one
    public static readonly Species[] AllSpecies =
    {
        new("LionsMane", "Львиная грива", "Lion's mane jellyfish", 22, false, .99f, .1032f, new[]
        {
            new Colour("Amber", "рыжий", "amber", 55, C(1f, .55f, .25f)),
            new Colour("Crimson", "бордовый", "crimson", 25, C(1f, .30f, .25f)),
            new Colour("Gold", "золотистый", "golden", 15, C(1f, .85f, .40f)),
            new Colour("Violet", "фиолетовый", "violet", 5, C(.75f, .50f, 1f)),
        }),
        new("Aurelia", "Ушастая аурелия", "Moon jellyfish", 30, false, 1.25f, .1113f, new[]
        {
            new Colour("Milky", "молочный", "milky", 55, C(.75f, .85f, 1f)),
            new Colour("Pink", "розовый", "pink", 25, C(1f, .65f, .85f)),
            new Colour("Lilac", "сиреневый", "lilac", 15, C(.75f, .60f, 1f)),
            new Colour("Blue", "голубой", "blue", 5, C(.40f, .70f, 1f)),
        }),
        new("Crystal", "Хрустальная медуза", "Crystal jelly", 15, false, 1.05f, .1115f, new[]
        {
            new Colour("Green", "зелёное свечение", "green glow", 65, C(.30f, 1f, .50f)),
            new Colour("Turquoise", "бирюзовое свечение", "turquoise glow", 28, C(.20f, .95f, .95f)),
            new Colour("Purple", "фиолетовое свечение", "violet glow", 7, C(.70f, .35f, 1f)),
        }),
        new("SeaNettle", "Северная морская крапива", "Northern sea nettle", 18, false, 1.08f, .0993f, new[]
        {
            new Colour("Brown", "полосатый коричневый", "striped brown", 65, C(1f, .70f, .40f)),
            new Colour("Honey", "медовый", "honey", 28, C(1f, .75f, .30f)),
            new Colour("Maroon", "тёмно-бордовый", "maroon", 7, C(1f, .35f, .30f)),
        }),
        new("Periphylla", "Шлемовидная медуза", "Helmet jellyfish", 4, true, 1.01f, .0858f, new[]
        {
            new Colour("Red", "тёмно-красный", "deep red", 65, C(.25f, .55f, 1f)),
            new Colour("Purple", "пурпурный", "purple", 28, C(.35f, .55f, 1f)),
            new Colour("Black", "чёрный", "black", 7, C(.20f, .45f, 1f)),
        }),
        new("CombJelly", "Морской крыжовник", "Sea gooseberry", 12, false, 1.34f, .1025f, new[]
        {
            new Colour("Clear", "прозрачный", "clear", 65, C(.70f, .90f, 1f)),
            new Colour("Rosy", "розоватый", "rosy", 28, C(1f, .70f, .85f)),
            new Colour("Milky", "молочный", "milky", 7, C(.85f, .90f, 1f)),
        }),
    };

    /// <summary>The coast: salt water under the ice. Lakes and rivers are fresh, no jellyfish there.</summary>
    public static readonly HashSet<string> CoastScenes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CoastalRegion", "WhalingStationRegion", "CanneryRegion", "AirfieldRegion",
    };

    public static readonly List<Def> All = new();
    static readonly Dictionary<string, Def> byName = new(StringComparer.OrdinalIgnoreCase);

    public static string LiveId(Species s, Colour c) => "Jelly_" + s.Id + "_" + c.Id;

    static Defs()
    {
        All.Add(new Def { Id = "Jar", Base = "GEAR_ScrapMetal", Kind = Kind.Jar, Kg = Tune.JarKg });
        foreach (var s in AllSpecies)
            foreach (var c in s.Colours)
                All.Add(new Def { Id = LiveId(s, c), Base = "GEAR_ScrapMetal", Kind = Kind.Live, Species = s, Colour = c, Kg = Tune.LiveKg });
        All.Add(new Def { Id = "Aquarium", Base = "GEAR_ScrapMetal", Kind = Kind.Aquarium, Kg = Tune.AquariumKg });
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

    public static bool IsDecor(GearItem? g) => Is(g, out var d) && d.IsDecor;

    public static Def? ById(string id) => byName.TryGetValue(Prefix + id, out var d) ? d : null;

    public static Species? SpeciesById(string id) => AllSpecies.FirstOrDefault(s => s.Id == id);
    public static Colour? ColourOf(Species s, string id) => s.Colours.FirstOrDefault(c => c.Id == id);

    static readonly System.Random rnd = new();

    /// <summary>A species and colour by their weights; the helmet jellyfish comes up from the deep only at night.</summary>
    public static (Species, Colour) Roll(bool night)
    {
        var pool = AllSpecies.Where(s => night || !s.NightOnly).ToList();
        int sum = pool.Sum(s => s.Weight), k = rnd.Next(sum);
        var sp = pool[^1];
        foreach (var s in pool) { if (k < s.Weight) { sp = s; break; } k -= s.Weight; }
        int cs = sp.Colours.Sum(c => c.Weight), q = rnd.Next(cs);
        var col = sp.Colours[^1];
        foreach (var c in sp.Colours) { if (q < c.Weight) { col = c; break; } q -= c.Weight; }
        return (sp, col);
    }

    /// <summary>How hard it is to catch: 0 for the commonest (moon jellyfish, milky), 1 for the rarest (a black helmet jellyfish).
    /// From how often it comes up: the owner (11-12), the rarer it is, the harder.</summary>
    public static float Difficulty(Species s, Colour c)
    {
        float ps = (float)s.Weight / AllSpecies.Sum(x => x.Weight), pc = (float)c.Weight / s.Colours.Sum(x => x.Weight);
        float p = Math.Max(1e-5f, ps * pc);
        return Mathf.Clamp01((-Mathf.Log10(p) - .8f) / 1.75f);
    }

    public static float Rand(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
}
