namespace MatchboxesByEMPEROR;

public record KindDef(string Id, string Ru, string En, string Base, int Matches, int LootWeight);
public record SeriesDef(string Id, string Kind, string Ru, string En, int Count);
public record LabelDef(string Id, string Series, string Kind, int N, string Ru, string En);

/// <summary>One matchbox item: a label on a kind of box.</summary>
public class Def
{
    public LabelDef Label = null!;
    public KindDef Kind = null!;
    public SeriesDef Series = null!;
    public string Id => Label.Id;
    public string Icon => "icons/" + Id + ".png";
    public string Texture => "textures/" + Id + ".jpg";
    public string Flat => "labels/" + Id + ".jpg";
    public string Mesh => "meshes/" + Kind.Id + ".fpmesh";
    public string Base => Kind.Base;
}

public static class Defs
{
    public const string Prefix = "GEAR_EMB_";
    public const string RefPrefix = "EMPERORMATCHBOX:";
    public static readonly List<Def> All = new();
    static readonly Dictionary<string, Def> byName = new(StringComparer.OrdinalIgnoreCase);

    // gameplay by kind (my choice 11-14): storm matches don't blow out and light fires better, fireplace matches burn longer
    // weight of a full box, kg (0.3.4, the owner: all boxes weighed the same; the game takes it as base weight × matches left / matches in a full box)
    public static float WeightKg(string kind) => kind switch { "Storm" => .05f, "Long" => .08f, "Book" => .02f, "Souvenir" => .15f, _ => .04f };
    public static float FireBonus(string kind) => kind switch { "Storm" => 10f, "Long" => 5f, _ => 0f };
    public static float BurnK(string kind) => kind switch { "Long" => 2.5f, "Storm" => 1.5f, _ => 1f };
    public static float WindK(string kind) => kind == "Storm" ? 4f : 1f;

    static Defs()
    {
        foreach (var l in Catalog.Labels)
        {
            var d = new Def { Label = l, Kind = Catalog.Kinds.First(k => k.Id == l.Kind), Series = Catalog.Series.First(s => s.Id == l.Series) };
            All.Add(d);
            byName[Prefix + d.Id] = d;
        }
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

    public static Def? ById(string id) => byName.TryGetValue(Prefix + id, out var d) ? d : null;

    static readonly System.Random rnd = new();
    public static float Rand(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

    /// <summary>A box for loot: a kind by its weight, then any label of that kind, all equally likely. The roll comes from the caller's
    /// seeded generator and does not look at the player's collection, so everyone in a co-op world finds the same box.
    /// cardboard: true/false in place of the game's own matchbook or wooden box.</summary>
    public static Def Roll(System.Random r, bool? cardboard = null)
    {
        var kinds = Catalog.Kinds.Where(k => cardboard == null || (k.Id == "Book") == cardboard.Value).ToList();
        int sum = kinds.Sum(k => k.LootWeight), x = r.Next(sum);
        var kind = kinds[^1];
        foreach (var k in kinds) { if (x < k.LootWeight) { kind = k; break; } x -= k.LootWeight; }
        var pool = All.Where(d => d.Kind.Id == kind.Id).ToList();
        return pool[r.Next(pool.Count)];
    }
}
