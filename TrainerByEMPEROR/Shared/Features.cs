// Общее описание функций трейнера. Этот файл компилируется и в модуль игры, и в окно трейнера,
// поэтому названия и диапазоны задаются в одном месте.
namespace TrainerByEMPEROR.Shared
{
    public enum FeatureKind
    {
        Toggle,
        Slider,
        Action,
        Time,
        Weather,
    }

    public sealed class FeatureDef
    {
        public string Id;
        public string NameRu, NameEn;
        public string Name => L.T(NameRu, NameEn ?? NameRu);
        public string Category;
        public FeatureKind Kind;
        public string Hotkey = "";
        public string HintRu = "", HintEn = "";
        public string Hint => L.T(HintRu, HintEn ?? HintRu);
        public float Min;
        public float Max = 1f;
        public float Default;
        public float Step = 0.1f;
        public bool Persist = true;
    }

    public sealed class WeatherDef
    {
        public int Stage;
        public string NameRu, NameEn;
        public string Name => L.T(NameRu, NameEn);
    }

    public static class Features
    {
        public const int Port = 47811;
        public const string Version = "2.5.27";

        public const string CatPlayer = "Игрок";
        public const string CatInventory = "Инвентарь";
        public const string CatStats = "Навыки";
        public const string CatWeapons = "Оружие";
        public const string CatEnemies = "Животные";
        public const string CatGame = "Игра";
        public const string CatPhysics = "Физика";

        // Категории — это ключи; на экран выводится CatName(cat).
        public static string CatName(string cat) => !L.En ? cat : cat switch
        {
            CatPlayer => "Player",
            CatInventory => "Inventory",
            CatStats => "Skills",
            CatWeapons => "Weapons",
            CatEnemies => "Animals",
            CatGame => "Game",
            CatPhysics => "Physics",
            _ => cat,
        };

        public static readonly string[] Categories =
        {
            CatPlayer, CatInventory, CatStats, CatWeapons, CatEnemies, CatGame, CatPhysics,
        };

        public static readonly FeatureDef[] All =
        {
            T("god", "Бесконечное здоровье", "Infinite health", CatPlayer, "Keypad1", "Здоровье всегда полное, смерть отключена.", "Health always full, death disabled."),
            T("nofreeze", "Без холода", "No cold", CatPlayer, "Keypad5", "Тепло держится на уровне ползунка ниже.", "Warmth stays at the slider level below."),
            S("freezelvl", "Уровень тепла", "Warmth level", CatPlayer, 0f, 100f, 100f, 1f, "Работает, пока включено «Без холода». 100 % = полностью согрет.", "Works while “No cold” is on. 100 % = fully warm."),
            T("nofatigue", "Без усталости", "No fatigue", CatPlayer, "Keypad4", "Бодрость держится на уровне ползунка ниже.", "Rest stays at the slider level below."),
            S("fatiguelvl", "Уровень бодрости", "Rest level", CatPlayer, 0f, 100f, 100f, 1f, "Работает, пока включено «Без усталости». 100 % = полностью отдохнул.", "Works while “No fatigue” is on. 100 % = fully rested."),
            T("nothirst", "Без жажды", "No thirst", CatPlayer, "Keypad3", "Вода держится на уровне ползунка ниже.", "Water stays at the slider level below."),
            S("thirstlvl", "Уровень воды", "Water level", CatPlayer, 0f, 100f, 100f, 1f, "Работает, пока включено «Без жажды». 100 % = жажды нет.", "Works while “No thirst” is on. 100 % = not thirsty."),
            T("nohunger", "Без голода", "No hunger", CatPlayer, "Keypad2", "Сытость держится на уровне ползунка ниже.", "Calories stay at the slider level below."),
            S("hungerlvl", "Уровень сытости", "Calorie level", CatPlayer, 0f, 100f, 100f, 1f, "Работает, пока включено «Без голода». 100 % = полностью сыт.", "Works while “No hunger” is on. 100 % = fully fed."),
            T("noinfection", "Без заражений", "No infections", CatPlayer, "", "Не появляются риск заражения и заражение.", "No infection risk and no infection."),
            S("drain", "Множитель расхода показателей", "Needs drain multiplier", CatPlayer, 0f, 5f, 1f, 0.1f, "Насколько быстро падают сытость, вода, бодрость и тепло. 1 = как в игре.", "How fast calories, water, rest and warmth drop. 1 = as in the game."),
            T("stamina", "Бесконечная выносливость", "Infinite stamina", CatPlayer, "Keypad6"),
            S("staminamult", "Множитель расхода выносливости", "Stamina drain multiplier", CatPlayer, 0f, 5f, 1f, 0.1f, "1 = как в игре.", "1 = as in the game."),
            T("nowrist", "Без растяжения запястья", "No sprained wrist", CatPlayer),
            T("noankle", "Без растяжения лодыжки", "No sprained ankle", CatPlayer),
            T("nopain", "Без боли", "No pain", CatPlayer),
            A("cure", "Вылечить все болезни и травмы", "Cure all afflictions", CatPlayer, "KeypadMinus"),
            A("heal", "Восстановить всё (здоровье, еда, вода, сон, тепло)", "Restore everything (health, food, water, rest, warmth)", CatPlayer, "KeypadPlus"),
            S("dmgmult", "Множитель получаемого урона", "Damage taken multiplier", CatPlayer, 0f, 5f, 1f, 0.1f, "1 = как в игре, 0 = без урона.", "1 = as in the game, 0 = no damage."),

            T("weight", "Бесконечный переносимый вес", "Infinite carry weight", CatInventory, "Keypad7"),
            S("carry", "Максимальный переносимый вес, кг", "Max carry weight, kg", CatInventory, 0f, 5000f, 0f, 10f, "0 = как в игре. Бегать можно, пока вес не больше этого предела.", "0 = as in the game. You can run while the weight is under this limit."),
            T("uses", "Бесконечные расходники", "Infinite consumables", CatInventory, "", "Спички, бинты и другие штучные предметы не тратятся.", "Matches, bandages and other stackable items are not used up."),
            T("durability", "Бесконечная прочность", "Infinite durability", CatInventory, "Keypad8", "Предметы в инвентаре не изнашиваются и не портятся.", "Items in the inventory do not wear out or spoil."),
            T("light", "Бесконечное топливо для света", "Infinite light fuel", CatInventory, "", "Фонарь, факелы и фальшфейеры не прогорают.", "Lantern, torches and flares do not burn out."),
            T("easycraft", "Лёгкий крафт", "Easy crafting", CatInventory, "", "Крафт без нужных материалов и инструментов, материалы не тратятся.", "Craft without the required materials and tools; materials are not used up."),
            T("instantcraft", "Мгновенный крафт", "Instant crafting", CatInventory),
            A("repair", "Починить весь инвентарь", "Repair the whole inventory", CatInventory),

            A("maxskills", "Все навыки на максимум", "Max all skills", CatStats),

            A("addweapons", "Выдать всё оружие и патроны", "Give all weapons and ammo", CatWeapons),
            T("ammo", "Бесконечные патроны", "Infinite ammo", CatWeapons, "Keypad9"),
            T("onehit", "Убийство с одного попадания", "One-hit kills", CatWeapons),
            T("nojam", "Без осечек и заклинивания", "No misfires or jams", CatWeapons),

            T("stealth", "Невидимость для животных", "Invisible to animals", CatEnemies, "Keypad0"),

            S("gamespeed", "Скорость игры", "Game speed", CatGame, 0.1f, 5f, 1f, 0.05f, "1 = обычная скорость.", "1 = normal speed."),
            new FeatureDef { Id = "settime", NameRu = "Установить время суток", NameEn = "Set time of day", Category = CatGame, Kind = FeatureKind.Time, Min = 0, Max = 23, Default = 12, Step = 1, Persist = false },
            T("freezetime", "Остановить время", "Freeze time", CatGame, "", "Время суток не идёт.", "Time of day does not move.", persist: false),
            T("noblizzard", "Без метелей", "No blizzards", CatGame),
            new FeatureDef { Id = "weather", NameRu = "Установить погоду", NameEn = "Set weather", Category = CatGame, Kind = FeatureKind.Weather, Persist = false },
            A("quicksave", "Быстрое сохранение", "Quick save", CatGame),
            T("keepsave", "Не удалять сохранение при смерти", "Keep the save on death", CatGame),

            T("superspeed", "Суперскорость", "Super speed", CatPhysics),
            S("speedmult", "Множитель скорости", "Speed multiplier", CatPhysics, 1f, 10f, 3f, 0.5f),
            T("fly", "Режим полёта", "Fly mode", CatPhysics, "KeypadPeriod", "WASD, Space вверх, Ctrl вниз, Shift быстрее. Сквозь стены.", "WASD, Space up, Ctrl down, Shift faster. Through walls.", persist: false),
            S("flyspeed", "Скорость полёта", "Fly speed", CatPhysics, 2f, 50f, 10f, 1f),
        };

        public static readonly WeatherDef[] Weathers =
        {
            new WeatherDef { Stage = 4, NameRu = "Ясно", NameEn = "Clear" },
            new WeatherDef { Stage = 3, NameRu = "Переменная облачность", NameEn = "Partly cloudy" },
            new WeatherDef { Stage = 5, NameRu = "Облачно", NameEn = "Cloudy" },
            new WeatherDef { Stage = 6, NameRu = "Лёгкий туман", NameEn = "Light fog" },
            new WeatherDef { Stage = 0, NameRu = "Густой туман", NameEn = "Heavy fog" },
            new WeatherDef { Stage = 1, NameRu = "Лёгкий снег", NameEn = "Light snow" },
            new WeatherDef { Stage = 2, NameRu = "Сильный снег", NameEn = "Heavy snow" },
            new WeatherDef { Stage = 7, NameRu = "Метель", NameEn = "Blizzard" },
            new WeatherDef { Stage = 8, NameRu = "Ясно, северное сияние", NameEn = "Clear, aurora" },
        };

        // Можно ли назначить функции горячую клавишу.
        public static bool Bindable(FeatureDef f) => f.Kind == FeatureKind.Toggle || f.Kind == FeatureKind.Action;

        // Клавиши хранятся в виде "Ctrl+Shift+Keypad1" (имена клавиш как в Unity KeyCode).
        public static string DisplayCombo(string combo)
        {
            if (string.IsNullOrEmpty(combo)) return "—";
            var parts = combo.Split('+');
            for (int i = 0; i < parts.Length; i++) parts[i] = DisplayKey(parts[i]);
            return string.Join(" + ", parts);
        }

        private static string DisplayKey(string key)
        {
            switch (key)
            {
                case "KeypadPlus": return "Num+";
                case "KeypadMinus": return "Num−";
                case "KeypadMultiply": return "Num*";
                case "KeypadDivide": return "Num/";
                case "KeypadPeriod": return "Num.";
                case "KeypadEnter": return "NumEnter";
                case "UpArrow": return "↑";
                case "DownArrow": return "↓";
                case "LeftArrow": return "←";
                case "RightArrow": return "→";
                case "Return": return "Enter";
                case "BackQuote": return "`";
            }
            if (key.StartsWith("Keypad")) return "Num" + key.Substring(6);
            if (key.StartsWith("Alpha")) return key.Substring(5);
            return key;
        }

        public static FeatureDef Find(string id)
        {
            foreach (var f in All)
                if (f.Id == id) return f;
            return null;
        }

        private static FeatureDef T(string id, string ru, string en, string cat, string hotkey = "", string hintRu = "", string hintEn = "", bool persist = true) =>
            new FeatureDef { Id = id, NameRu = ru, NameEn = en, Category = cat, Kind = FeatureKind.Toggle, Hotkey = hotkey, HintRu = hintRu, HintEn = hintEn, Persist = persist };

        private static FeatureDef S(string id, string ru, string en, string cat, float min, float max, float def, float step, string hintRu = "", string hintEn = "") =>
            new FeatureDef { Id = id, NameRu = ru, NameEn = en, Category = cat, Kind = FeatureKind.Slider, Min = min, Max = max, Default = def, Step = step, HintRu = hintRu, HintEn = hintEn };

        private static FeatureDef A(string id, string ru, string en, string cat, string hotkey = "") =>
            new FeatureDef { Id = id, NameRu = ru, NameEn = en, Category = cat, Kind = FeatureKind.Action, Hotkey = hotkey, Persist = false };
    }
}
