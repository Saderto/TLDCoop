namespace TeaCoffeeByEMPEROR;

public enum Kind { Pack, Cup, Can }

/// <summary>One item of the mod: a copy of a game prefab with our model, texture and values.</summary>
public class Def
{
    public string Id = "";
    public string Base = "";
    public string? Fallback;
    public Kind Kind;
    public Catalog.P P = null!;
    public string Mesh = "", Texture = "";
    public string Icon = "";
}

public static class Defs
{
    public const string Prefix = "GEAR_TC_";
    public const string RefPrefix = "EMPERORTEA:";
    public const string DataPrefix = "GearItemData_TC_";

    public static readonly List<Def> All = new();
    static readonly Dictionary<string, Def> byName = new(StringComparer.OrdinalIgnoreCase);

    static Defs()
    {
        foreach (var p in Catalog.All)
        {
            string tex = "textures/" + p.Id + ".png", icon = "icons/" + p.Id + ".png", mesh = "meshes/" + p.Model + ".fpmesh";
            if (p.Cup == null)
            {
                // drunk from the pack itself: a cold can, made from the game's energy drink (or a soda when it is missing)
                All.Add(new Def { Id = p.Id, Base = "GEAR_EnergyDrink", Fallback = "GEAR_SodaGrape", Kind = Kind.Can, P = p, Mesh = mesh, Texture = tex, Icon = icon });
                continue;
            }
            All.Add(new Def { Id = p.Id, Base = p.Tea ? "GEAR_GreenTeaPackage" : "GEAR_CoffeeTin", Kind = Kind.Pack, P = p, Mesh = mesh, Texture = tex, Icon = icon });
            // the drink: its vessel's model; the big catalogue shares one texture and icon per vessel and colour of drink
            string cup = p.CupTex ?? p.Id + "Cup";
            All.Add(new Def
            {
                Id = p.Id + "Cup", Base = p.Tea ? "GEAR_GreenTeaCup" : "GEAR_CoffeeCup", Kind = Kind.Cup, P = p,
                Mesh = p.Cup == "glass" ? "meshes/tea_glass.fpmesh" : "meshes/" + p.Cup + ".fpmesh",
                Texture = "textures/" + cup + ".png", Icon = "icons/" + cup + ".png",
            });
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
        if (name == null) return false;
        if (name.StartsWith(DataPrefix, StringComparison.Ordinal)) name = Prefix + name.Substring(DataPrefix.Length);
        if (!name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return false;
        return byName.TryGetValue(Clean(name), out def!);
    }

    public static bool Is(GearItem? g, out Def def)
    {
        def = null!;
        return g != null && TryGet(g.name, out def);
    }

    public static Def? ById(string id) => byName.TryGetValue(Prefix + id, out var d) ? d : null;

    /// <summary>The products found in the world or sold (packs and the can), each once.</summary>
    public static IEnumerable<Def> Goods => All.Where(d => d.Kind != Kind.Cup);
}
