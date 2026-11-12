namespace SmokesByEMPEROR;

public enum Kind { Pack, Cigar, CigarCut, Tube, Pipe, Pouch, Lighter, Cutter, Lit, Box, Refill }

public enum SmokeType { Cigarette, Cigar, Pipe }

/// <summary>One item of the mod: a copy of a game prefab with our model, texture and values.</summary>
public class Def
{
    public string Id = "";
    public Kind Kind;
    public string Base = "GEAR_BottlePainKillers";
    public string Mesh = "";          // meshes/<Mesh>.fpmesh, lying at rest, bottom on y=0
    public string Texture = "";       // textures/<Texture>.png
    public string? HeldMesh;          // lit items: the model in the hand, mouth end at the origin, lit end towards +Z
    public float WeightKg;
    public int Units = 1;             // a new one holds this many (cigarettes, portions, lighter charges)
    public string UnitKey = "";       // SMOKES_UNIT_<x>_ONE / _MANY
    public string? Lit;               // what lighting it makes (a lit item's id)
    public string? Next;              // tube -> its cigar, uncut cigar -> cut cigar
    public SmokeType Smoke;           // lit items
    // cigarette varieties (Catalog): how the cigarette from this pack looks and works
    public string? CigTexture, CigMesh, Brand;
    public int Strength = 1;          // 0 light .. 3 very strong
    public string Bonus = "";         // fresh | coffee | sweet | warm | calm | hands | steady
    public string Side = "";          // downside: dry | dizzy
    public float[]? PipeStats;        // tobacco pouches: fatigue %, health %, hunger minutes, bonus hours
    public Color? Tobacco;            // tobacco pouches: the colour of the tobacco in the bowl
    public bool Rare;
    // lighters (11-10 collection) and their refills: "petrol" | "gas"; the bonus to the fire starting chance, %; rarity 1..3
    public string Fuel = "";
    public float FireBonus;
    public int Rarity;
    /// <summary>A texture file under textures/: names without an extension are .png.</summary>
    public static string TexPath(string name) => "textures/" + (name.Contains('.') ? name : name + ".png");
    public string? IconId;            // another item's icon (the per-variety lit cigarettes share one)
    public string Icon => "icons/" + (IconId ?? Id) + ".png";
}

public class SmokeStats
{
    public float RealMinutes, GameMinutes, FatiguePct, HealthPct, HungerMinutes, Counts;
    public string Bonus = "";
    public float BonusHours;
    public string Side = "";
    public float SideHours;
}

public static class Defs
{
    public const string Prefix = "GEAR_SeagullSmoke_";
    public const string RefPrefix = "SEAGULLSMOKE:";

    public static readonly List<Def> All = new();
    static readonly Dictionary<string, Def> byName = new();

    public static readonly string[] Brands = { "Chapgirl", "Milston", "Marlbrrro", "Parliamint", "Bent", "Caramel" };
    public static readonly string[] Pipes = { "Billiard", "Bent", "Churchwarden", "Corncob" };
    public static readonly string[] Cigars = { "Cohibra", "Montecrusto" };

    static Defs()
    {
        foreach (var v in Catalog.All)
        {
            // each variety lights its own lit cigarette (its look in the world and in a coop friend's hand: the held item goes by name)
            Add(new Def { Id = "LitCig_" + v.Id, Kind = Kind.Lit, Base = "GEAR_Torch", Mesh = v.CigMesh, Texture = v.CigTexture, HeldMesh = v.CigMesh + "_held", WeightKg = .001f,
                Smoke = SmokeType.Cigarette, IconId = "LitCigarette" });
            Add(new Def { Id = v.Id, Kind = Kind.Pack, Mesh = v.PackMesh, Texture = v.PackTexture, WeightKg = .025f, Units = 20, UnitKey = "CIG", Lit = "LitCig_" + v.Id,
                CigTexture = v.CigTexture, CigMesh = v.CigMesh, Strength = v.Strength, Bonus = v.Bonus, Side = v.Side, Rare = v.Rare, Brand = v.Brand });
        }
        foreach (var b in Brands)
            if (ById("Pack" + b) == null) Add(new Def { Id = "Pack" + b, Kind = Kind.Pack, Mesh = "pack_" + b.ToLowerInvariant(), Texture = "pack_" + b.ToLowerInvariant(), WeightKg = .025f, Units = 20, UnitKey = "CIG", Lit = "LitCigarette" });
        foreach (var c in CigarCatalog.All)
        {
            if (!c.New) continue;
            string lc = c.Key.ToLowerInvariant(), k = c.Key;
            Add(new Def { Id = "Tube" + k, Kind = Kind.Tube, Mesh = "tube_" + lc, Texture = "tube_" + lc + ".jpg", WeightKg = .04f, UnitKey = "TUBE", Next = "Cigar" + k });
            Add(new Def { Id = "Cigar" + k, Kind = Kind.Cigar, Mesh = "cigar_" + lc, Texture = "cigar_" + lc + ".jpg", WeightKg = .02f, UnitKey = "CIGAR", Next = "CigarCut" + k });
            Add(new Def { Id = "CigarCut" + k, Kind = Kind.CigarCut, Mesh = "cigar_cut_" + lc, Texture = "cigar_cut_" + lc + ".jpg", WeightKg = .02f, UnitKey = "CIGAR", Lit = "LitCigar" + k,
                Strength = c.Strength, Bonus = c.Bonus, Side = c.Side, Brand = lc });
            Add(new Def { Id = "LitCigar" + k, Kind = Kind.Lit, Base = "GEAR_Torch", Mesh = "cigar_cut_" + lc, Texture = "cigar_cut_" + lc + ".jpg", HeldMesh = "cigar_cut_" + lc + "_held", WeightKg = .02f, Smoke = SmokeType.Cigar });
        }
        // cedar boxes of one brand: «take a cigar» gives one uncut cigar
        foreach (var c in CigarCatalog.All)
        {
            string lc = c.Key.ToLowerInvariant();
            Add(new Def { Id = "Box" + c.Key, Kind = Kind.Box, Mesh = "box_" + lc, Texture = "box_" + lc + ".jpg", WeightKg = .3f + .02f * c.BoxCount, Units = c.BoxCount, UnitKey = "CIGAR", Next = "Cigar" + c.Key });
        }
        foreach (var c in Cigars)
        {
            string lc = c.ToLowerInvariant();
            Add(new Def { Id = "Tube" + c, Kind = Kind.Tube, Mesh = "tube_" + lc, Texture = "tube_" + lc, WeightKg = .04f, UnitKey = "TUBE", Next = "Cigar" + c });
            Add(new Def { Id = "Cigar" + c, Kind = Kind.Cigar, Mesh = "cigar_" + lc, Texture = "cigar_" + lc, WeightKg = .02f, UnitKey = "CIGAR", Next = "CigarCut" + c });
            Add(new Def { Id = "CigarCut" + c, Kind = Kind.CigarCut, Mesh = "cigar_cut_" + lc, Texture = "cigar_cut_" + lc, WeightKg = .02f, UnitKey = "CIGAR", Lit = "LitCigar" + c });
            Add(new Def { Id = "LitCigar" + c, Kind = Kind.Lit, Base = "GEAR_Torch", Mesh = "cigar_cut_" + lc, Texture = "cigar_cut_" + lc, HeldMesh = "cigar_cut_" + lc + "_held", WeightKg = .02f, Smoke = SmokeType.Cigar });
        }
        foreach (var p in Pipes)
        {
            string lp = p.ToLowerInvariant();
            Add(new Def { Id = "Pipe" + p, Kind = Kind.Pipe, Mesh = "pipe_" + lp, Texture = "pipe_" + lp, WeightKg = p == "Churchwarden" ? .06f : .05f, UnitKey = "PIPE", Lit = "LitPipe" + p });
            Add(new Def { Id = "LitPipe" + p, Kind = Kind.Lit, Base = "GEAR_Torch", Mesh = "pipe_" + lp, Texture = "pipe_" + lp, HeldMesh = "pipe_" + lp + "_held", WeightKg = .05f, Smoke = SmokeType.Pipe });
        }
        Add(new Def { Id = "LitCigarette", Kind = Kind.Lit, Base = "GEAR_Torch", Mesh = "cigarette", Texture = "cigarette", HeldMesh = "cigarette_held", WeightKg = .001f, Smoke = SmokeType.Cigarette });
        foreach (var p in PouchCatalog.All)
            Add(new Def { Id = "Pouch" + p.Key, Kind = Kind.Pouch, Mesh = "pouch_captainblock", Texture = "pouch_" + p.Key.ToLowerInvariant() + ".jpg", WeightKg = .05f, Units = 20, UnitKey = "PORTION",
                PipeStats = new[] { p.Fatigue, p.Health, p.Hunger, p.Hours }, Bonus = p.Bonus, Side = p.Side, Tobacco = new Color(p.R, p.G, p.B) });
        Add(new Def { Id = "PouchCaptainBlock", Kind = Kind.Pouch, Mesh = "pouch_captainblock", Texture = "pouch_captainblock", WeightKg = .05f, Units = 20, UnitKey = "PORTION" });
        // 11-10: 29 collectible lighters (LighterZappo first, the old one), petrol or gas, and the two refills
        foreach (var l in Lighters.All)
            Add(new Def { Id = l.Id, Kind = Kind.Lighter, Base = "GEAR_Firestriker", Mesh = l.Mesh, Texture = l.Mesh, WeightKg = l.Kg, Units = l.Charges, UnitKey = "CHARGE",
                Fuel = l.Fuel, FireBonus = l.FireBonus, Rarity = l.Rarity });
        Add(new Def { Id = "LighterFluid", Kind = Kind.Refill, Mesh = "lighter_fluid", Texture = "lighter_fluid", WeightKg = .15f, Units = Lighters.FluidUnits, UnitKey = "REFILL", Fuel = "petrol" });
        Add(new Def { Id = "ButaneCan", Kind = Kind.Refill, Mesh = "butane_can", Texture = "butane_can", WeightKg = .25f, Units = Lighters.ButaneUnits, UnitKey = "REFILL", Fuel = "gas" });
        Add(new Def { Id = "CutterZikar", Kind = Kind.Cutter, Mesh = "cutter_zikar", Texture = "cutter_zikar", WeightKg = .03f, UnitKey = "CUTTER" });
    }

    static void Add(Def d)
    {
        All.Add(d);
        byName[Prefix + d.Id] = d;
    }

    public static Def? ById(string id) => byName.TryGetValue(Prefix + id, out var d) ? d : null;

    /// <summary>Ours, by an object or prefab name: "(Clone)" and " (1)" are cut off.</summary>
    public static bool TryGet(string? name, out Def def)
    {
        def = null!;
        if (string.IsNullOrEmpty(name)) return false;
        int bracket = name.IndexOf('(');
        if (bracket >= 0) name = name.Substring(0, bracket);
        name = name.Trim();
        if (name.StartsWith("GearItemData_SeagullSmoke_", StringComparison.Ordinal)) name = Prefix + name.Substring("GearItemData_SeagullSmoke_".Length);
        return byName.TryGetValue(name, out def!);
    }

    public static bool Is(GearItem? g, out Def def)
    {
        def = null!;
        return g != null && TryGet(g.name, out def);
    }

    // fatigue %, hunger minutes, health %, counts for shortness of breath, by strength (1 = the old numbers)
    static readonly float[][] Strengths = { new[] { 3f, 20f, .5f, .7f }, new[] { 5f, 25f, 1f, 1f }, new[] { 7f, 30f, 1.5f, 1.3f }, new[] { 9f, 35f, 2f, 1.6f } };

    static readonly float[][] CigarStrengths = { new[] { 8f, 1.5f }, new[] { 10f, 2f }, new[] { 13f, 2.5f } };

    /// <summary>A smoke's numbers; a cigarette from a variety pack by its strength and flavour.</summary>
    // real smoking time, minutes, by cigar size (vitola) and cigarette format; the game time is scaled so the average stays the setting
    static readonly Dictionary<string, float> Vitola = new()
    {
        ["short"] = 30f, ["robusto"] = 45f, ["corona"] = 40f, ["torpedo"] = 60f, ["churchill"] = 80f, ["lancero"] = 55f, ["boxpress"] = 50f,
    };
    static readonly Dictionary<string, string> CigarShape = new()
    {
        ["Cohibra"] = "robusto", ["Montecrusto"] = "torpedo", ["Partagaz"] = "robusto", ["Romero"] = "churchill", ["Hoyo"] = "corona", ["Uppmann"] = "corona",
        ["Pounch"] = "short", ["Bolivarr"] = "torpedo", ["Trinidud"] = "lancero", ["Arturo"] = "churchill", ["Padrone"] = "boxpress", ["Macanuto"] = "corona",
        ["Olivva"] = "robusto", ["Rocky"] = "torpedo", ["Ashten"] = "churchill", ["Grandfather"] = "boxpress", ["Cuabba"] = "torpedo", ["VegasRobaino"] = "robusto",
        ["JuanLopes"] = "corona", ["RamonAllonzo"] = "corona", ["SanCristobaal"] = "churchill", ["PorLarranya"] = "corona", ["ReyDelMondo"] = "lancero",
        ["SanchoPansa"] = "torpedo", ["Diplomatikos"] = "robusto", ["GloriaCubanna"] = "churchill", ["Fonsecka"] = "corona", ["MyUncle"] = "boxpress",
        ["Perdomio"] = "churchill", ["LigaPrivatta"] = "robusto", ["AlecBadly"] = "robusto", ["FlorDominicanna"] = "torpedo", ["Kamacho"] = "short",
        ["JoyaNicaragwa"] = "corona", ["Tatuajje"] = "boxpress", ["CuestaRay"] = "lancero",
    };
    const float CigarMeanReal = 52.78f;   // the average of the list above
    static readonly Dictionary<string, float> CigFormat = new()
    {
        ["cig_regular"] = 6f, ["cig_long"] = 7.5f, ["cig_nofilter"] = 5f, ["cig_papirosa"] = 4f, ["cig_short"] = 5f, ["cig_superslim"] = 6.5f,
    };

    static string CigarKey(string id)
    {
        foreach (var p in new[] { "LitCigar", "CigarCut", "Cigar", "Tube", "Box" })
            if (id.StartsWith(p, StringComparison.Ordinal)) return id.Substring(p.Length);
        return id;
    }

    /// <summary>How much longer than the average this smoke burns (1 = the setting).</summary>
    public static float TimeFactor(SmokeType t, Def? from)
    {
        if (t == SmokeType.Cigar && from != null && CigarShape.TryGetValue(CigarKey(from.Id), out var shape) && Vitola.TryGetValue(shape, out var m))
            return m / CigarMeanReal;
        if (t == SmokeType.Cigarette && from?.CigMesh != null)
        {
            float f = CigFormat.TryGetValue(from.CigMesh, out var x) ? x / 6f : 1f;
            if (from.Brand != null && from.Brand.StartsWith("djarum", StringComparison.OrdinalIgnoreCase)) f *= 1.4f;   // kretek: cloves, burns long
            // the blend and how tightly it is packed differ by brand: within +-5%, the same for one brand every time
            uint h = 2166136261; foreach (char ch in from.Brand ?? from.Id) { h ^= ch; h *= 16777619; }
            return f * (.95f + (h % 1000) / 1000f * .1f);
        }
        return 1f;
    }

    public static SmokeStats Stats(SmokeType t, Def? from)
    {
        var s = Stats(t);
        float tf = TimeFactor(t, from);
        s.RealMinutes *= tf;
        s.GameMinutes *= tf;
        // 11-09 user: cigars from 3.2 (the shortest) to 7 real minutes (the churchill), by size in between
        if (t == SmokeType.Cigar && from != null && CigarShape.TryGetValue(CigarKey(from.Id), out var sh) && Vitola.TryGetValue(sh, out var vm))
            s.RealMinutes = Mathf.Lerp(Settings.CigarMinMinutes.Value, Settings.CigarMaxMinutes.Value, Mathf.InverseLerp(30f, 80f, vm));
        if (t == SmokeType.Pipe && from?.PipeStats is { } ps)
        {
            s.FatiguePct = ps[0]; s.HealthPct = ps[1]; s.HungerMinutes = ps[2];
            if (from.Bonus.Length > 0) { s.Bonus = from.Bonus; s.BonusHours = ps[3] > 0f ? ps[3] : 1f; }
            if (from.Side.Length > 0) { s.Side = from.Side; s.SideHours = 1f; }
            return s;
        }
        if (t == SmokeType.Cigar && from != null && from.Brand != null)
        {
            // cigar brands: mild / medium / full; full ones warm, mild ones calm, for an hour
            var c = CigarStrengths[Math.Clamp(from.Strength, 0, 2)];
            s.FatiguePct = c[0]; s.HealthPct = c[1];
            if (from.Bonus.Length > 0) { s.Bonus = from.Bonus; s.BonusHours = 1f; }
            if (from.Side.Length > 0) { s.Side = from.Side; s.SideHours = 1f; }
            return s;
        }
        if (t != SmokeType.Cigarette || from == null || from.CigTexture == null) return s;
        var k = Strengths[Math.Clamp(from.Strength, 0, 3)];
        s.FatiguePct = k[0]; s.HungerMinutes = k[1]; s.HealthPct = k[2]; s.Counts = k[3];
        switch (from.Bonus)
        {
            case "coffee": s.FatiguePct += 2f; break;
            case "sweet": s.HungerMinutes += 10f; break;
            case "fresh": case "warm": case "calm": case "hands": case "steady": s.Bonus = from.Bonus; s.BonusHours = .5f; break;
        }
        if (from.Side.Length > 0) { s.Side = from.Side; s.SideHours = .5f; }
        return s;
    }

    public static SmokeStats Stats(SmokeType t) => t switch
    {
        SmokeType.Cigar => new SmokeStats { RealMinutes = Settings.CigarRealMinutes.Value, GameMinutes = 15, FatiguePct = 10, HealthPct = 2, HungerMinutes = 45, Counts = 3 },
        SmokeType.Pipe => new SmokeStats { RealMinutes = Settings.PipeRealMinutes.Value, GameMinutes = 10, FatiguePct = 7, HealthPct = 1, HungerMinutes = 35, Counts = 1 },
        _ => new SmokeStats { RealMinutes = Settings.CigaretteRealMinutes.Value, GameMinutes = 5, FatiguePct = 5, HealthPct = 1, HungerMinutes = 25, Counts = 1 },
    };
}
