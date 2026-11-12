namespace SmokehouseByEMPEROR;

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
    public Species? Species;      // smoked meat and fish only
    public bool Hot, Salted;
    /// <summary>"Barrel", "Kettle", "Table": a thing that stands in the world and opens a window. "" for food.</summary>
    public string Station = "";
    public bool IsStation => Station.Length > 0;
    /// <summary>A station that was not finished at the workbench (11-12 owner): the same model under another name, it is not placed or used.</summary>
    public bool Wip;
    /// <summary>An unfinished sausage (11-12 owner): the raw sausage's model under another name; it is not eaten or smoked, the table finishes it.</summary>
    public string Unfinished = "";
    public bool IsUnfinished => Unfinished.Length > 0;
    public bool Bulky => IsStation || Wip;
    public bool IsBarrel => Station == Defs.Barrel;
    public float Kg;              // a station's weight
    /// <summary>Half the size of what the player cannot walk through, along the item's X and Z; 0: a round thing.</summary>
    public float HalfX, HalfZ;
    public string Icon => "icons/" + IconId + ".png";
    /// <summary>What is left of the raw weight, and what the tin adds.</summary>
    public float Weight = 1f, Tare;
    /// <summary>How fast it spoils next to the source piece (0: never).</summary>
    public float Decay = 1f;
    public bool NoParasites, NoCook, NoScent;
    /// <summary>What is left in the pack when it is eaten (an empty can).</summary>
    public string Empty = "";
    /// <summary>The FoodPack effect when eaten: "meat", "fish" or none.</summary>
    public string Fx = "";
    /// <summary>A smoked sausage: its kind (the effects come from it) or null.</summary>
    public SausKind? Kind;
}

/// <summary>A part of a sausage: so much of any of these raw meats.</summary>
public record Part(string Group, float Kg);

/// <summary>A kind of sausage the table makes (11-12 owner: sausages are only smoked, many kinds, each with its effect; mixed ones of several meats).
/// Items: Saus{Id}Raw, Saus{Id}Hot, Saus{Id}Dry.</summary>
public record SausKind(string Id, Part[] Parts, string RawBase, string CookedBase, bool Parasites, (string Id, float Value)[] Fx,
    string Ru, string RuRaw, string En, string RuFx, string EnFx, float Hours = 1f)
{
    public string RawId => "Saus" + Id + "Raw";
    public string SpeciesId => "Saus" + Id;
    public string WipId => "Saus" + Id + "Wip";
    /// <summary>11-12 owner: the more kinds of meat and fish in it, the higher the Cooking level it asks (1..5 as the game shows it); «Рыбная» of all fish is the top.</summary>
    public int Level => Math.Clamp(Parts.Length, 1, 5);
    public string Name => Texts.Russian ? Ru : char.ToUpperInvariant(En[0]) + En.Substring(1);
}

/// <summary>A kind of can the kettle boils (11-12 owner: more than the first five, some of several meats): 0.30 kg of meat in all.</summary>
public record CanKind(string Id, Part[] Parts, string Base, string Fx, string Ru, string En, string RuOf, string EnOf)
{
    public string Name => Texts.Russian ? Ru : En;
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
    public const float BoilVolume = .6f;
    public const float BoilLevel = 100f;      // the audio parameter CookingState of the kettle's boiling loop (clamped by the sound engine to its own top)
    public const float KettleWarm = 6f, BarrelWarm = 3f;     // 11-12 owner: degrees of warmth near a burning kettle and smokehouse
    public const int ColdSkillLevel = 4;          // Cooking level, as the game shows it (1..5)
    public const float SaltKg = .3f, BarrelKg = 15f;
    public const float ConditionGain = .25f;
    // «Заготовки» (11-11): the owner set 24 h and the salt for the kettle
    public const float KettleHours = 24f, BoilHours = 1f;
    public const float SausageMeatKg = .50f;      // 11-12 owner: a sausage takes its portion of the meat too
    public const float CanWater = .25f, CanWeight = .85f, CanTare = .08f;
    public const float CanMeatKg = .30f;          // 11-12 owner: a can takes its portion of the meat, not the whole piece
    public const float KettleKg = 12f, TableKg = 20f;
    public const float SausageDryWeight = .6f, SausageSkin = .05f;
    // 11-12 owner: a sausage must be worth the two steps. Besides the smoked piece's «Долгая сытость» it has its kind's effect, both for this long
    public const float SausageMinutes = 45f, SausageSeconds = 6f;     // 11-12 owner: a sausage takes time to make, as crafting does
    public const float SausHotFxHours = 6f, SausDryFxHours = 12f, MixedFx = .7f, TripleFx = .55f;
}

public static class Defs
{
    public const string Prefix = "GEAR_SH_";
    public const string RefPrefix = "SEAGULLSMOKEHOUSE:";
    public const string Barrel = "Barrel", Kettle = "Kettle", Table = "Table";
    public const string Can = "GEAR_RecycledCan", SausRaw = "SausRaw", SausBoiled = "SausBoiled", SausDry = "SausDry", SausHot = "SausHot";
    /// <summary>The raw sausage as a thing to boil or smoke: it goes where a raw piece goes.</summary>
    public static readonly Species Sausage = new("Sausage", Prefix + SausRaw, "GEAR_CookedMeatDeer", false, "Колбаса", "sausage");

    /// <summary>Which raw meats a recipe's word stands for.</summary>
    public static readonly Dictionary<string, string[]> Groups = new()
    {
        ["Deer"] = new[] { "Deer" }, ["Moose"] = new[] { "Moose" }, ["Bear"] = new[] { "Bear" }, ["Predator"] = new[] { "Wolf", "Cougar" },
        ["Small"] = new[] { "Rabbit", "Ptarmigan" }, ["Wolf"] = new[] { "Wolf" }, ["Cougar"] = new[] { "Cougar" }, ["Rabbit"] = new[] { "Rabbit" }, ["Ptarmigan"] = new[] { "Ptarmigan" },
        ["Fish"] = new[] { "Coho", "WhiteFish", "Trout", "Bass", "Burbot", "Goldeye", "Rockfish", "IrishLord" },
        ["Salmon"] = new[] { "Coho", "Trout" }, ["Sea"] = new[] { "Rockfish", "IrishLord" }, ["Lake"] = new[] { "WhiteFish", "Bass", "Burbot", "Goldeye" },
    };
    public static readonly Dictionary<string, (string Ru, string En)> GroupNames = new()
    {
        ["Deer"] = ("оленина", "venison"), ["Moose"] = ("лосятина", "moose"), ["Bear"] = ("медвежатина", "bear"), ["Predator"] = ("волк или пума", "wolf or cougar"),
        ["Small"] = ("заяц или куропатка", "rabbit or ptarmigan"), ["Fish"] = ("любая рыба", "any fish"),
        ["Salmon"] = ("кижуч или форель", "coho or trout"), ["Sea"] = ("морской окунь или ирландский лорд", "rockfish or Irish lord"), ["Lake"] = ("сиг, окунь, налим или золотоглазка", "whitefish, bass, burbot or goldeye"),
        ["Wolf"] = ("волчатина", "wolf"), ["Cougar"] = ("мясо пумы", "cougar"), ["Rabbit"] = ("крольчатина", "rabbit"), ["Ptarmigan"] = ("куропатка", "ptarmigan"),
        ["Coho"] = ("кижуч", "coho salmon"), ["WhiteFish"] = ("сиг", "lake whitefish"), ["Trout"] = ("радужная форель", "rainbow trout"), ["Bass"] = ("малоротый окунь", "smallmouth bass"),
        ["Burbot"] = ("налим", "burbot"), ["Goldeye"] = ("золотоглазка", "goldeye"), ["Rockfish"] = ("морской окунь", "rockfish"), ["IrishLord"] = ("красный ирландский лорд", "red Irish lord"),
    };

    static (string, float)[] Fx(float k, params (string Id, float V)[] a) => a.Select(x => (x.Id, x.V * k)).ToArray();
    static readonly Dictionary<string, (float V, string Ru, string En)> FxInfo = new()
    {
        ["SureStep"] = (.30f, "на склонах меньше шанс растяжения", "less chance of a sprain on slopes"),
        ["StrongBack"] = (.20f, "при перегрузе выносливость тратится медленнее", "stamina drains slower when overloaded"),
        ["Hardening"] = (.25f, "риск гипотермии набирается медленнее", "hypothermia risk builds slower"),
        ["ThickBlood"] = (.30f, "кровопотеря отнимает здоровье медленнее", "blood loss takes health slower"),
        ["Stealth"] = (.15f, "звери замечают вас с меньшего расстояния", "animals notice you from a shorter distance"),
        ["Diligence"] = (.15f, "крафт, ремонт и шитьё идут быстрее", "crafting, repairs and sewing go faster"),
        ["SteadyHand"] = (.20f, "прицел качается медленнее", "your aim sways slower"),
        ["FrostResist"] = (.25f, "обморожение набирается медленнее", "frostbite builds slower"),
        ["WarmSleep"] = (.20f, "во сне тело остывает медленнее", "you cool slower in your sleep"),
        ["ClearHead"] = (.20f, "книги навыков читаются быстрее", "skill books read faster"),
        ["DeepRest"] = (.15f, "во сне усталость уходит быстрее", "sleep takes tiredness away faster"),
        ["GripHands"] = (.25f, "на верёвке выносливость тратится медленнее", "stamina drains slower on a rope"),
        ["Stillness"] = (.20f, "стоя на холоде, тело остывает медленнее", "you cool slower while standing still in the cold"),
        ["Focus"] = (.10f, "готовка, шитьё и ремонт дают больше опыта", "cooking, sewing and repairs give more skill"),
        ["StrongBones"] = (.25f, "падения причиняют меньше урона", "falls hurt less"),
        ["StrongStomach"] = (.50f, "несвежая еда и сырая вода реже вызывают отравление", "stale food and raw water poison you less often"),
        ["WindProof"] = (.25f, "ветер охлаждает слабее", "the wind chills less"),
        ["Tireless"] = (.25f, "усталость растёт медленнее", "tiredness grows slower"),
        ["Immunity"] = (.30f, "раны реже заражаются", "wounds get infected less often"),
        ["FastHealing"] = (.25f, "растяжения проходят быстрее", "sprains heal faster"),
        ["PolarHardening"] = (.25f, "тело остывает медленнее", "your body cools slower"),
    };

    static SausKind Kind(string id, string ru, string ruRaw, string en, string parts, string fx, float k = 1f, float hours = 1f, bool parasites = false)
    {
        var ps = parts.Split(' ').Select(x => x.Split(':')).Select(x => new Part(x[0], float.Parse(x[1], System.Globalization.CultureInfo.InvariantCulture))).ToArray();
        var fs = fx.Split(' ');
        string first = ps[0].Group, sp = Groups.TryGetValue(first, out var g) ? g[0] : first;
        var s = AllSpecies.First(x => x.Id == sp);
        // fish has no cooked piece with meat's sounds and smell that suits a sausage: a fish sausage is built on rabbit
        string rawBase = s.Fish ? "GEAR_RawMeatRabbit" : s.Raw, cookedBase = s.Fish ? "GEAR_CookedMeatRabbit" : s.Cooked;
        if (parasites) { rawBase = ps.Any(x => x.Group == "Bear") ? "GEAR_RawMeatBear" : "GEAR_RawMeatWolf"; cookedBase = rawBase.Replace("Raw", "Cooked"); }
        return new SausKind(id, ps, rawBase, cookedBase, parasites, fs.Select(f => (f, FxInfo[f].V * k)).ToArray(), ru, ruRaw, en,
            string.Join(", ", fs.Select(f => FxInfo[f].Ru)), string.Join(", ", fs.Select(f => FxInfo[f].En)), hours);
    }

    static CanKind Can_(string id, string parts, string cooked, string fx, string ru, string en, string ruOf, string enOf) =>
        new(id, parts.Split(' ').Select(x => x.Split(':')).Select(x => new Part(x[0], float.Parse(x[1], System.Globalization.CultureInfo.InvariantCulture))).ToArray(), "GEAR_Cooked" + cooked, fx, ru, en, ruOf, enOf);

    /// <summary>The first five are the cans of 0.3; the rest came 11-12 («сделай для котла больше комбинаций»). Each takes 0.30 kg of meat in all.</summary>
    public static readonly CanKind[] Cans =
    {
        Can_("CanDeer", "Deer:.3", "MeatDeer", "meat", "Тушёнка из оленины", "Canned venison", "Оленина", "Venison"),
        Can_("CanMoose", "Moose:.3", "MeatMoose", "meat", "Тушёнка из лосятины", "Canned moose", "Лосятина", "Moose meat"),
        Can_("CanBear", "Bear:.3", "MeatBear", "meat", "Тушёнка из медвежатины", "Canned bear meat", "Медвежатина", "Bear meat"),
        Can_("CanWolf", "Wolf:.3", "MeatWolf", "meat", "Тушёнка из волчатины", "Canned wolf meat", "Волчатина", "Wolf meat"),
        Can_("CanCougar", "Cougar:.3", "MeatCougar", "meat", "Тушёнка из пумы", "Canned cougar meat", "Мясо пумы", "Cougar meat"),
        Can_("CanPate", "Small:.3", "MeatRabbit", "meat", "Паштет из дичи", "Game pate", "Заяц или куропатка", "Rabbit or ptarmigan"),
        Can_("CanFish", "Fish:.3", "CohoSalmon", "fish", "Рыба в собственном соку", "Fish in its own juice", "Рыба", "Fish"),
        Can_("CanSalmon", "Salmon:.3", "CohoSalmon", "fish", "Лосось в собственном соку", "Salmon in its own juice", "Кижуч или форель", "Coho or trout"),
        Can_("CanSea", "Sea:.3", "Rockfish", "fish", "Морская рыба в собственном соку", "Sea fish in its own juice", "Морская рыба", "Sea fish"),
        Can_("CanHunter", "Deer:.15 Moose:.15", "MeatDeer", "meat", "Охотничья тушёнка", "Hunter's canned meat", "Оленина с лосятиной", "Venison and moose"),
        Can_("CanStew", "Deer:.15 Small:.15", "MeatDeer", "meat", "Рагу из дичи", "Game stew", "Оленина с зайцем или куропаткой", "Venison with rabbit or ptarmigan"),
        Can_("CanBeast", "Bear:.15 Predator:.15", "MeatBear", "meat", "Тушёнка зверобоя", "Beastslayer's canned meat", "Медвежатина с мясом волка или пумы", "Bear with wolf or cougar meat"),
        Can_("CanAngler", "Fish:.15 Small:.15", "CohoSalmon", "fish", "Рыбацкая похлёбка", "Angler's chowder", "Рыба с зайцем или куропаткой", "Fish with rabbit or ptarmigan"),
        Can_("CanGreat", "Deer:.1 Moose:.1 Bear:.1", "MeatMoose", "meat", "Тушёнка «Великий Медведь»", "Great Bear canned meat", "Оленина, лосятина и медвежатина", "Venison, moose and bear"),
        // 11-12 owner did not follow «лосось, морская и озёрная»: simply three different fish
        Can_("CanFishMix", "Fish:.1 Fish:.1 Fish:.1", "CohoSalmon", "fish", "Рыбное ассорти", "Fish medley", "Три разные рыбы", "Three different fish"),
    };
    public static CanKind? CanById(string id) => Cans.FirstOrDefault(c => c.Id == id);

    static SausKind[]? sausages;
    /// <summary>One meat: its own effect. Two: both at 0.7. Three: all three at 0.55. Four have effects of their own.
    /// «Рыбная» takes every fish of the island and is by far the strongest (11-12 owner), twice as long too.</summary>
    public static SausKind[] Sausages => sausages ??= new[]
    {
        Kind("Hunt", "Охотничьи колбаски", "Сырые охотничьи колбаски", "hunter's sausages", "Deer:.5", "SureStep"),
        Kind("Taiga", "Таёжная колбаса", "Сырая таёжная колбаса", "taiga sausage", "Moose:.5", "StrongBack"),
        Kind("Bear", "Медвежья колбаса", "Сырая медвежья колбаса", "bear sausage", "Bear:.5", "Hardening", parasites: true),
        Kind("Wolf", "Волчья колбаса", "Сырая волчья колбаса", "wolf sausage", "Wolf:.5", "ThickBlood", parasites: true),
        Kind("Cougar", "Горная колбаса", "Сырая горная колбаса", "mountain sausage", "Cougar:.5", "Stealth", parasites: true),
        Kind("Game", "Заячьи колбаски", "Сырые заячьи колбаски", "rabbit sausages", "Rabbit:.5", "Diligence"),
        Kind("Bird", "Колбаски из куропатки", "Сырые колбаски из куропатки", "ptarmigan sausages", "Ptarmigan:.5", "SteadyHand"),
        Kind("Coho", "Колбаса из кижуча", "Сырая колбаса из кижуча", "coho sausage", "Coho:.5", "WarmSleep"),
        Kind("Trout", "Колбаса из форели", "Сырая колбаса из форели", "trout sausage", "Trout:.5", "ClearHead"),
        Kind("WhiteFish", "Колбаса из сига", "Сырая колбаса из сига", "whitefish sausage", "WhiteFish:.5", "DeepRest"),
        Kind("Bass", "Колбаса из окуня", "Сырая колбаса из окуня", "bass sausage", "Bass:.5", "GripHands"),
        Kind("Burbot", "Колбаса из налима", "Сырая колбаса из налима", "burbot sausage", "Burbot:.5", "Stillness"),
        Kind("Goldeye", "Колбаски из золотоглазки", "Сырые колбаски из золотоглазки", "goldeye sausages", "Goldeye:.5", "Focus"),
        Kind("Rockfish", "Колбаса из морского окуня", "Сырая колбаса из морского окуня", "rockfish sausage", "Rockfish:.5", "StrongBones"),
        Kind("IrishLord", "Колбаса из ирландского лорда", "Сырая колбаса из ирландского лорда", "Irish lord sausage", "IrishLord:.5", "StrongStomach"),
        Kind("Mix", "Сборная колбаса", "Сырая сборная колбаса", "mixed sausage", "Deer:.25 Moose:.25", "SureStep StrongBack", Tune.MixedFx),
        Kind("Forest", "Лесные колбаски", "Сырые лесные колбаски", "forest sausages", "Small:.25 Deer:.25", "Diligence SureStep", Tune.MixedFx),
        Kind("Angler", "Рыбацкая колбаса", "Сырая рыбацкая колбаса", "angler's sausage", "Fish:.25 Small:.25", "FrostResist Diligence", Tune.MixedFx),
        Kind("Beast", "Колбаса «Зверобой»", "Сырая колбаса «Зверобой»", "beastslayer sausage", "Bear:.25 Predator:.25", "Hardening ThickBlood", Tune.MixedFx, parasites: true),
        Kind("Trapper", "Трапперская колбаса", "Сырая трапперская колбаса", "trapper's sausage", "Deer:.2 Small:.15 Fish:.15", "SureStep Diligence FrostResist", Tune.TripleFx),
        Kind("Trophy", "Трофейная колбаса", "Сырая трофейная колбаса", "trophy sausage", "Moose:.2 Bear:.15 Predator:.15", "StrongBack Hardening ThickBlood", Tune.TripleFx, parasites: true),
        Kind("GreatBear", "Колбаса «Великий Медведь»", "Сырая колбаса «Великий Медведь»", "Great Bear sausage", "Deer:.125 Moose:.125 Bear:.125 Fish:.125", "WindProof Tireless Immunity", parasites: true),
        Kind("Tramp", "Бродяжья колбаса", "Сырая бродяжья колбаса", "drifter's sausage", "Deer:.125 Small:.125 Predator:.125 Fish:.125", "FastHealing StrongBones Stealth", parasites: true),
        Kind("Fish", "Рыбная колбаса", "Сырая рыбная колбаса", "fish sausage", string.Join(" ", Groups["Fish"].Select(f => f + ":.5")), "PolarHardening Tireless WarmSleep DeepRest FrostResist", 1.6f, 2f),
    };
    static readonly Dictionary<string, Species> sausSpecies = new();

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
        All.Add(new Def { Id = Barrel, Base = "GEAR_ScrapMetal", Mesh = "Barrel", Texture = "Barrel", IconId = "Barrel", Station = Barrel, Kg = Tune.BarrelKg });
        All.Add(new Def { Id = Kettle, Base = "GEAR_ScrapMetal", Mesh = "Kettle", Texture = "Kettle", IconId = "Kettle", Station = Kettle, Kg = Tune.KettleKg });
        All.Add(new Def { Id = Table, Base = "GEAR_ScrapMetal", Mesh = "Table", Texture = "Table", IconId = "Table", Station = Table, Kg = Tune.TableKg, HalfX = .68f, HalfZ = .36f });
        foreach (var s in new[] { Barrel, Kettle, Table })
        {
            var d = All.First(x => x.Id == s);
            All.Add(new Def { Id = s + "Wip", Base = d.Base, Mesh = d.Mesh, Texture = d.Texture, IconId = d.IconId, Wip = true, Kg = d.Kg });
        }
        // canned in the kettle: never spoils, no smell, the empty can is left
        foreach (var (id, src, fx) in Cans.Select(c => (c.Id, c.Base, c.Fx)))
            All.Add(new Def { Id = id, Base = src, Mesh = id, Texture = id, IconId = id, Weight = Tune.CanWeight, Tare = Tune.CanTare, Decay = 0f, NoParasites = true, NoScent = true, Empty = Can, Fx = fx });
        // the one sausage of 0.3.x: no longer made, kept so that a save holding it loads; the raw one can still be smoked
        All.Add(new Def { Id = SausRaw, Base = "GEAR_RawMeatDeer", Mesh = SausRaw, Texture = SausRaw, IconId = SausRaw, NoCook = true });
        All.Add(new Def { Id = SausHot, Base = "GEAR_CookedMeatDeer", Mesh = SausHot, Texture = SausHot, IconId = SausHot, Weight = Tune.HotWeight, Decay = 1f / Tune.HotKeeps, NoParasites = true, Fx = "meat" });
        All.Add(new Def { Id = SausBoiled, Base = "GEAR_CookedMeatDeer", Mesh = SausBoiled, Texture = SausBoiled, IconId = SausBoiled, NoParasites = true, Fx = "meat" });
        All.Add(new Def { Id = SausDry, Base = "GEAR_CookedMeatDeer", Mesh = SausDry, Texture = SausDry, IconId = SausDry, Weight = Tune.SausageDryWeight, Decay = 0f, NoParasites = true, Fx = "meat" });
        byRaw[Sausage.Raw] = Sausage;
        // the sausages of the table: raw (smoked only, never fried), hot-smoked, cold-smoked. Hot smoke kills parasites, cold does not.
        foreach (var k in Sausages)
        {
            string id = "Saus" + k.Id;
            All.Add(new Def { Id = id + "Raw", Base = k.RawBase, Mesh = id + "Raw", Texture = id + "Raw", IconId = id + "Raw", NoCook = true, NoParasites = !k.Parasites });
            All.Add(new Def { Id = id + "Hot", Base = k.CookedBase, Mesh = id + "Hot", Texture = id + "Hot", IconId = id + "Hot", Hot = true, Weight = Tune.HotWeight, Decay = 1f / Tune.HotKeeps, NoParasites = true, Kind = k });
            All.Add(new Def { Id = id + "Dry", Base = k.CookedBase, Mesh = id + "Dry", Texture = id + "Dry", IconId = id + "Dry", Weight = Tune.SausageDryWeight, Decay = 0f, NoParasites = !k.Parasites, Kind = k });
            // a dried gut underneath: a plain thing that is not food, not fuel and does not spoil
            All.Add(new Def { Id = id + "Wip", Base = "GEAR_GutDried", Mesh = id + "Raw", Texture = id + "Raw", IconId = id + "Raw", Unfinished = id + "Raw", Kg = Tune.SausageMeatKg + Tune.SausageSkin });
            var sp = new Species(k.SpeciesId, Prefix + k.RawId, k.CookedBase, false, k.Ru, k.En);
            sausSpecies[sp.Id] = sp;
            byRaw[sp.Raw] = sp;
        }
        foreach (var s in AllSpecies)
        {
            byRaw[s.Raw] = s;
            foreach (bool hot in new[] { true, false })
                foreach (bool salted in new[] { false, true })
                {
                    string look = (hot ? "Hot" : "Cold") + s.Id;
                    All.Add(new Def { Id = look + (salted ? "_S" : ""), Base = s.Cooked, Mesh = look, Texture = look, IconId = look, Species = s, Hot = hot, Salted = salted,
                        Weight = hot ? Tune.HotWeight : Tune.ColdWeight, Decay = hot ? 1f / Tune.HotKeeps : 0f, NoParasites = hot });
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

    public static bool IsStation(GearItem? g) => Is(g, out var d) && d.IsStation;

    public static bool IsStation(GearItem? g, out Def def) => Is(g, out def) && def.IsStation;

    /// <summary>The name a station's state goes by: "" the barrel (as before 0.3), "Kettle"; the table has no state.</summary>
    public static string KindOf(Def d) => d.Station == Barrel ? "" : d.Station;

    public static string? CanOf(Species s) => s.Fish ? "CanFish" : s.Id switch { "Deer" => "CanDeer", "Moose" => "CanMoose", "Bear" => "CanBear", "Rabbit" or "Ptarmigan" => "CanPate", _ => null };

    /// <summary>Meat a sausage is made of: no predators (their parasites would be lost on the way).</summary>
    public static bool ForSausage(Species s) => s != Sausage && !IsSausage(s);

    /// <summary>A raw sausage of any kind (the old single one too): the barrel smokes it, nothing else takes it.</summary>
    public static bool IsSausage(Species s) => s == Sausage || sausSpecies.ContainsKey(s.Id);

    /// <summary>The raw gear names a recipe's part takes.</summary>
    public static IEnumerable<string> RawsOf(Part p) => (Groups.TryGetValue(p.Group, out var g) ? g : new[] { p.Group }).Select(id => AllSpecies.First(s => s.Id == id).Raw);

    /// <summary>Does this station take this raw thing at all.</summary>
    public static bool Takes(string kind, Species s) => IsSausage(s) ? kind != Kettle : true;      // 0.4.22: the kettle has a can for every meat

    /// <summary>What a finished slot gives: a can or a boiled sausage out of the kettle, smoked food or a dry sausage out of the barrel.</summary>
    public static Def? Result(string kind, Species s, bool cold, bool salted)
    {
        if (s == Sausage) return kind == Kettle ? null : ById(cold ? SausDry : SausHot);
        if (IsSausage(s)) return kind == Kettle ? null : ById(s.Id + (cold ? "Dry" : "Hot"));
        if (kind == Kettle) return CanOf(s) is { } c ? ById(c) : null;
        return ById(OutId(s, !cold, salted));
    }

    /// <summary>What a slot gives when done: a kettle's slot knows its can by Recipe (0.4.22), an older one by its meat.</summary>
    public static Def? ResultOf(string kind, Slot s, bool cold)
    {
        if (kind == Kettle && s.Recipe.Length > 0 && ById(s.Recipe) is { } can) return can;
        return SpeciesById(s.Species) is { } sp ? Result(kind, sp, cold, s.Salted) : null;
    }

    public static Def? ById(string id) => byName.TryGetValue(Prefix + id, out var d) ? d : null;

    public static Species? ByRaw(string? gearName) => gearName != null && byRaw.TryGetValue(Clean(gearName), out var s) ? s : null;

    public static Species? SpeciesById(string id) => id == Sausage.Id ? Sausage : sausSpecies.TryGetValue(id, out var k) ? k : AllSpecies.FirstOrDefault(s => s.Id == id);

    public static string OutId(Species s, bool hot, bool salted) => (hot ? "Hot" : "Cold") + s.Id + (salted ? "_S" : "");
}
