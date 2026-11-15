namespace SnacksByEMPEROR;

public enum Kind { Snack, Noodle, Brewed }

/// <summary>One item of the mod: a copy of a game prefab with our model, label and values.</summary>
public class Def
{
    public string Id = "";
    public Kind Kind;
    /// <summary>The pack: bag, sachet, tube, can, box.</summary>
    public string Shape = "";
    public string Mesh = "", Texture = "";
    public float Kg, Kcal, Thirst, Eat, Water, Warm;
    public int Spicy;
    /// <summary>A brewed one: the dry noodles it was made of.</summary>
    public string Raw = "";
    public string Ru = "", En = "", RuF = "", EnF = "";
    /// <summary>Where the brand is from (or that it is sold worldwide).</summary>
    public string RuC = "", EnC = "";
    /// <summary>The effects eating it leaves (ids of Effects.All).</summary>
    public string[] Fx = Array.Empty<string>();
    /// <summary>The joke of the description.</summary>
    public string RuJ = "", EnJ = "";
    /// <summary>A seasonal pack: found several times less often.</summary>
    public bool Rare;

    public string Base => Kind switch
    {
        Kind.Snack => "GEAR_KetchupChips",
        Kind.Noodle => "GEAR_GreenTeaPackage",
        _ => "GEAR_GreenTeaCup",
    };
    public string Icon => "icons/" + Id + ".png";
    public string Gear => Defs.Prefix + Id;
    public string Brewed => Id + "Hot";
}

public static partial class Defs
{
    public const string Prefix = "GEAR_SNK_";
    public const string RefPrefix = "EMPERORSNACKS:";

    static Dictionary<string, Def>? byName;
    static Dictionary<string, Def> ByName
    {
        get
        {
            if (byName != null) return byName;
            byName = new(StringComparer.OrdinalIgnoreCase);
            foreach (var d in All) byName[Prefix + d.Id] = d;
            // 0.1.x had one item per brand: those left in a save become the brand's first flavour
            foreach (var d in All)
            {
                string old = Prefix + (d.Mesh == "Martyn" ? "Seeds" : d.Mesh);
                if (!byName.ContainsKey(old)) byName[old] = d;
            }
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
