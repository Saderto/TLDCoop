using System.Collections.Generic;

namespace FeatsByEMPEROR
{
    /// <summary>One new feat: what unlocks it, what it gives and its texts.</summary>
    internal sealed class Def
    {
        public string Id;          // file/console name
        public int Type;           // FeatType value (vanilla uses 0..12; ours start at 1000)
        public float Target;       // amount needed to unlock
        public string Guid;        // Addressables key of the badge texture in our bundle
        public string NameRu, NameEn;
        public string TaskRu, TaskEn;      // "Разделайте 500 кг мяса."
        public string BonusRu, BonusEn;    // what the feat gives
        public string UnitRu, UnitEn;      // shown in progress ("кг", "kg")
        public int Decimals;               // progress shown with this many decimals

        public string KeyName => "SGL_FEAT_" + Id + "_NAME";
        public string KeyDesc => "SGL_FEAT_" + Id + "_DESC";
        public string KeyBonus => "SGL_FEAT_" + Id + "_BONUS";
        public string Texture => "badge_feat_sgl_" + Id;
    }

    internal static class Defs
    {
        // Feat ids
        public const string Butcher = "Butcher", Tailor = "Tailor", WolfHunter = "WolfHunter", BearHide = "BearHide",
            NightEye = "NightEye", Angler = "Angler", IronStomach = "IronStomach", Woodcutter = "Woodcutter",
            Climber = "Climber", WaterCarrier = "WaterCarrier", Craftsman = "Craftsman", Marksman = "Marksman";

        // Effect sizes
        public const float ButcherTimeScale = 0.75f;
        public const float TailorWearScale = 0.8f;
        public const int WolfFleeBonus = 15;          // percentage points added to the wolf's flee roll on noticing you
        public const float BearHideDamageScale = 0.8f;
        public const float NightEyeBrightness = 0.3f; // added to the brightness setting (0..1) at full night
        public const float AnglerTimeScale = 0.8f;
        public const float IronStomachKeepChance = 0.5f; // chance that a rolled food poisoning is still applied
        public const float WoodcutterTimeScale = 0.7f;
        public const float ClimberSpeedScale = 1.25f, ClimberDrainScale = 0.7f;
        public const float WaterTimeScale = 0.75f;
        public const float CraftTimeScale = 0.75f;
        public const float MarksmanWearScale = 0.5f;

        public static readonly List<Def> All = new()
        {
            new Def { Id = Butcher, Type = 1000, Target = 500f, Guid = "5a0c7e21d4b94f3c8e6a1b2f9d3c4e01",
                NameRu = "Мясник", NameEn = "Butcher",
                TaskRu = "Разделайте 500 кг мяса.", TaskEn = "Harvest 500 kg of meat.",
                BonusRu = "Разделка и четвертование туш на 25% быстрее.", BonusEn = "Harvesting and quartering carcasses is 25% faster.",
                UnitRu = "кг", UnitEn = "kg", Decimals = 1 },
            new Def { Id = Tailor, Type = 1001, Target = 200f, Guid = "5a0c7e21d4b94f3c8e6a1b2f9d3c4e02",
                NameRu = "Портной", NameEn = "Tailor",
                TaskRu = "Успешно почините одежду 200 раз.", TaskEn = "Successfully repair clothing 200 times.",
                BonusRu = "Одежда изнашивается на 20% медленнее.", BonusEn = "Clothing wears out 20% slower.",
                UnitRu = "", UnitEn = "" },
            new Def { Id = WolfHunter, Type = 1002, Target = 100f, Guid = "5a0c7e21d4b94f3c8e6a1b2f9d3c4e03",
                NameRu = "Охотник на волков", NameEn = "Wolf Hunter",
                TaskRu = "Убейте 100 волков.", TaskEn = "Kill 100 wolves.",
                BonusRu = "Волки на 15% чаще убегают, заметив вас, а не нападают.", BonusEn = "Wolves are 15% more likely to flee instead of attacking when they notice you.",
                UnitRu = "", UnitEn = "" },
            new Def { Id = BearHide, Type = 1003, Target = 25f, Guid = "5a0c7e21d4b94f3c8e6a1b2f9d3c4e04",
                NameRu = "Медвежья шкура", NameEn = "Thick Hide",
                TaskRu = "Переживите 25 схваток с хищниками.", TaskEn = "Survive 25 struggles with predators.",
                BonusRu = "В схватках вы и ваша одежда получаете на 20% меньше урона.", BonusEn = "You and your clothing take 20% less damage in struggles.",
                UnitRu = "", UnitEn = "" },
            new Def { Id = NightEye, Type = 1004, Target = 150f, Guid = "5a0c7e21d4b94f3c8e6a1b2f9d3c4e05",
                NameRu = "Ночной глаз", NameEn = "Night Eye",
                TaskRu = "Пройдите 150 км ночью.", TaskEn = "Travel 150 km at night.",
                BonusRu = "Ночью вы видите лучше: темнота становится светлее.", BonusEn = "You see better at night: darkness is brighter.",
                UnitRu = "км", UnitEn = "km", Decimals = 1 },
            new Def { Id = Angler, Type = 1005, Target = 300f, Guid = "5a0c7e21d4b94f3c8e6a1b2f9d3c4e06",
                NameRu = "Рыбак", NameEn = "Angler",
                TaskRu = "Поймайте 300 рыб.", TaskEn = "Catch 300 fish.",
                BonusRu = "Рыба клюёт на 20% быстрее.", BonusEn = "Fish bite 20% faster.",
                UnitRu = "", UnitEn = "" },
            new Def { Id = IronStomach, Type = 1006, Target = 100f, Guid = "5a0c7e21d4b94f3c8e6a1b2f9d3c4e07",
                NameRu = "Железный желудок", NameEn = "Iron Stomach",
                TaskRu = "Съешьте 100 кг сырого мяса и рыбы.",
                TaskEn = "Eat 100 kg of raw meat and fish.",
                BonusRu = "Шанс пищевого отравления вдвое ниже.", BonusEn = "Half the chance of food poisoning.",
                UnitRu = "кг", UnitEn = "kg", Decimals = 1 },
            new Def { Id = Woodcutter, Type = 1007, Target = 1000f, Guid = "5a0c7e21d4b94f3c8e6a1b2f9d3c4e08",
                NameRu = "Дровосек", NameEn = "Woodcutter",
                TaskRu = "Добудьте 1000 палок и дров, разбирая ветви, брёвна и мебель.",
                TaskEn = "Get 1000 sticks and pieces of firewood by breaking down limbs, logs and furniture.",
                BonusRu = "Разбор на палки и дрова на 30% быстрее.", BonusEn = "Breaking things down into sticks and firewood is 30% faster.",
                UnitRu = "", UnitEn = "" },
            new Def { Id = Climber, Type = 1008, Target = 1000f, Guid = "5a0c7e21d4b94f3c8e6a1b2f9d3c4e09",
                NameRu = "Альпинист", NameEn = "Climber",
                TaskRu = "Поднимитесь по верёвке на 1000 метров.", TaskEn = "Climb 1000 meters up ropes.",
                BonusRu = "Подъём по верёвке на 25% быстрее, выносливость и силы тратятся на 30% меньше.",
                BonusEn = "Rope climbing is 25% faster and uses 30% less stamina and fatigue.",
                UnitRu = "м", UnitEn = "m" },
            new Def { Id = WaterCarrier, Type = 1009, Target = 500f, Guid = "5a0c7e21d4b94f3c8e6a1b2f9d3c4e0a",
                NameRu = "Водовоз", NameEn = "Water Carrier",
                TaskRu = "Растопите снег или вскипятите 500 литров воды.", TaskEn = "Melt snow or boil 500 liters of water.",
                BonusRu = "Снег тает и вода закипает на 25% быстрее.", BonusEn = "Snow melts and water boils 25% faster.",
                UnitRu = "л", UnitEn = "L", Decimals = 1 },
            new Def { Id = Craftsman, Type = 1010, Target = 1000f, Guid = "5a0c7e21d4b94f3c8e6a1b2f9d3c4e0b",
                NameRu = "Ремесленник", NameEn = "Craftsman",
                TaskRu = "Изготовьте 1000 предметов.", TaskEn = "Craft 1000 items.",
                BonusRu = "Изготовление предметов на 25% быстрее.", BonusEn = "Crafting is 25% faster.",
                UnitRu = "", UnitEn = "" },
            new Def { Id = Marksman, Type = 1011, Target = 2000f, Guid = "5a0c7e21d4b94f3c8e6a1b2f9d3c4e0c",
                NameRu = "Стрелок", NameEn = "Marksman",
                TaskRu = "Сделайте 2000 выстрелов из огнестрельного оружия.", TaskEn = "Fire 2000 shots from firearms.",
                BonusRu = "Огнестрельное оружие изнашивается вдвое медленнее.", BonusEn = "Firearms wear out half as fast.",
                UnitRu = "выстр.", UnitEn = "shots" },
        };

        static Defs() { All.AddRange(Defs2.New); }

        static Dictionary<int, Def> _byType;
        static Dictionary<string, Def> _byId;

        public static Def ByType(int t)
        {
            if (_byType == null) { _byType = new(); foreach (var d in All) _byType[d.Type] = d; }
            return _byType.TryGetValue(t, out var r) ? r : null;
        }

        public static Def ById(string id)
        {
            if (_byId == null) { _byId = new(System.StringComparer.OrdinalIgnoreCase); foreach (var d in All) _byId[d.Id] = d; }
            return _byId.TryGetValue(id, out var r) ? r : null;
        }
    }
}
