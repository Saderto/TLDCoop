namespace NoodlesByEMPEROR;

public enum Kind { Noodle, Brewed }

/// <summary>One item of the mod: a copy of a game prefab with our model, label and values.</summary>
public class Def
{
    public string Id = "";
    public Kind Kind;
    /// <summary>The package: brick, kpack, jpack, mini, long, big (packs); cup, minicup, tallcup, bigcup, bowl, kbowl, bucket, ufo; tray, box.</summary>
    public string Shape = "";
    public string Brand = "";
    /// <summary>Eaten without broth (fried, mixed with sauce).</summary>
    public bool Dry;
    public string Mesh = "", Texture = "";
    public float Kg, Kcal, Thirst, Eat, Water, Warm;
    public int Spicy;
    /// <summary>Effects of the brewed one, space separated (Effects.cs).</summary>
    public string Fx = "";
    /// <summary>A brewed one: the dry noodles it was made of.</summary>
    public string Raw = "";
    public string Ru = "", En = "", RuF = "", EnF = "";
    /// <summary>The description: a joke of its own for the pack and for the cooked bowl (tools/memes).</summary>
    public string RuM = "", EnM = "";
    /// <summary>The broth's colour, "#rrggbb": the liquid in the pot while it cooks.</summary>
    public string Broth = "";

    public string Base => Kind switch
    {
        Kind.Noodle => "GEAR_GreenTeaPackage",
        _ => "GEAR_GreenTeaCup",
    };
    public string Icon => "icons/" + Id + ".png";
    public bool Pack => Shape is "brick" or "kpack" or "jpack" or "mini" or "long" or "big";
    public string Gear => Defs.Prefix + Id;
    public string Brewed => Id + "Hot";
}

public static partial class Defs
{
    public const string Prefix = "GEAR_NDL_";
    public const string RefPrefix = "EMPERORNOODLES:";

    static Dictionary<string, Def>? byName;
    static Dictionary<string, Def> ByName
    {
        get
        {
            if (byName != null) return byName;
            byName = new(StringComparer.OrdinalIgnoreCase);
            foreach (var d in All) byName[Prefix + d.Id] = d;
            return byName;
        }
    }

    public static IEnumerable<Def> Sellable => All.Where(d => d.Kind != Kind.Brewed);

    public static Def? ById(string id) => All.FirstOrDefault(d => d.Id == id);

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
        return ByName.TryGetValue(Clean(name), out def!);
    }

    public static bool Is(GearItem? g, out Def def)
    {
        def = null!;
        return g != null && TryGet(g.name, out def);
    }
}
