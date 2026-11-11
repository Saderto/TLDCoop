// Общее описание функций трейнера. Этот файл компилируется и в модуль игры, и в окно трейнера,
// поэтому названия и диапазоны задаются в одном месте.
namespace TLDTrainer.Shared
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
        public string Name;
        public string Category;
        public FeatureKind Kind;
        public string Hotkey = "";
        public string Hint = "";
        public float Min;
        public float Max = 1f;
        public float Default;
        public float Step = 0.1f;
        public bool Persist = true;
    }

    public sealed class WeatherDef
    {
        public int Stage;
        public string Name;
    }

    public static class Features
    {
        public const int Port = 47811;
        public const string Version = "2.5.23";

        public const string CatPlayer = "Игрок";
        public const string CatInventory = "Инвентарь";
        public const string CatStats = "Навыки";
        public const string CatWeapons = "Оружие";
        public const string CatEnemies = "Животные";
        public const string CatGame = "Игра";
        public const string CatPhysics = "Физика";

        public static readonly string[] Categories =
        {
            CatPlayer, CatInventory, CatStats, CatWeapons, CatEnemies, CatGame, CatPhysics,
        };

        public static readonly FeatureDef[] All =
        {
            T("god", "Бесконечное здоровье", CatPlayer, "Keypad1", "Здоровье всегда полное, смерть отключена."),
            T("nofreeze", "Без холода", CatPlayer, "Keypad5", "Тепло держится на уровне ползунка ниже."),
            S("freezelvl", "Уровень тепла", CatPlayer, 0f, 100f, 100f, 1f, "Работает, пока включено «Без холода». 100 % = полностью согрет."),
            T("nofatigue", "Без усталости", CatPlayer, "Keypad4", "Бодрость держится на уровне ползунка ниже."),
            S("fatiguelvl", "Уровень бодрости", CatPlayer, 0f, 100f, 100f, 1f, "Работает, пока включено «Без усталости». 100 % = полностью отдохнул."),
            T("nothirst", "Без жажды", CatPlayer, "Keypad3", "Вода держится на уровне ползунка ниже."),
            S("thirstlvl", "Уровень воды", CatPlayer, 0f, 100f, 100f, 1f, "Работает, пока включено «Без жажды». 100 % = жажды нет."),
            T("nohunger", "Без голода", CatPlayer, "Keypad2", "Сытость держится на уровне ползунка ниже."),
            S("hungerlvl", "Уровень сытости", CatPlayer, 0f, 100f, 100f, 1f, "Работает, пока включено «Без голода». 100 % = полностью сыт."),
            T("noinfection", "Без заражений", CatPlayer, "", "Не появляются риск заражения и заражение."),
            S("drain", "Множитель расхода показателей", CatPlayer, 0f, 5f, 1f, 0.1f, "Насколько быстро падают сытость, вода, бодрость и тепло. 1 = как в игре."),
            T("stamina", "Бесконечная выносливость", CatPlayer, "Keypad6"),
            S("staminamult", "Множитель расхода выносливости", CatPlayer, 0f, 5f, 1f, 0.1f, "1 = как в игре."),
            T("nowrist", "Без растяжения запястья", CatPlayer),
            T("noankle", "Без растяжения лодыжки", CatPlayer),
            T("nopain", "Без боли", CatPlayer),
            A("cure", "Вылечить все болезни и травмы", CatPlayer, "KeypadMinus"),
            A("heal", "Восстановить всё (здоровье, еда, вода, сон, тепло)", CatPlayer, "KeypadPlus"),
            S("dmgmult", "Множитель получаемого урона", CatPlayer, 0f, 5f, 1f, 0.1f, "1 = как в игре, 0 = без урона."),

            T("weight", "Бесконечный переносимый вес", CatInventory, "Keypad7"),
            S("carry", "Максимальный переносимый вес, кг", CatInventory, 0f, 5000f, 0f, 10f, "0 = как в игре. Бегать можно, пока вес не больше этого предела."),
            T("uses", "Бесконечные расходники", CatInventory, "", "Спички, бинты и другие штучные предметы не тратятся."),
            T("durability", "Бесконечная прочность", CatInventory, "Keypad8", "Предметы в инвентаре не изнашиваются и не портятся."),
            T("light", "Бесконечное топливо для света", CatInventory, "", "Фонарь, факелы и фальшфейеры не прогорают."),
            T("easycraft", "Лёгкий крафт", CatInventory, "", "Крафт без нужных материалов и инструментов, материалы не тратятся."),
            T("instantcraft", "Мгновенный крафт", CatInventory),
            A("repair", "Починить весь инвентарь", CatInventory),

            A("maxskills", "Все навыки на максимум", CatStats),

            A("addweapons", "Выдать всё оружие и патроны", CatWeapons),
            T("ammo", "Бесконечные патроны", CatWeapons, "Keypad9"),
            T("onehit", "Убийство с одного попадания", CatWeapons),
            T("nojam", "Без осечек и заклинивания", CatWeapons),

            T("stealth", "Невидимость для животных", CatEnemies, "Keypad0"),

            S("gamespeed", "Скорость игры", CatGame, 0.1f, 5f, 1f, 0.05f, "1 = обычная скорость."),
            new FeatureDef { Id = "settime", Name = "Установить время суток", Category = CatGame, Kind = FeatureKind.Time, Min = 0, Max = 23, Default = 12, Step = 1, Persist = false },
            T("freezetime", "Остановить время", CatGame, "", "Время суток не идёт.", persist: false),
            T("noblizzard", "Без метелей", CatGame),
            new FeatureDef { Id = "weather", Name = "Установить погоду", Category = CatGame, Kind = FeatureKind.Weather, Persist = false },
            A("quicksave", "Быстрое сохранение", CatGame),
            T("keepsave", "Не удалять сохранение при смерти", CatGame),

            T("superspeed", "Суперскорость", CatPhysics),
            S("speedmult", "Множитель скорости", CatPhysics, 1f, 10f, 3f, 0.5f),
            T("fly", "Режим полёта", CatPhysics, "KeypadPeriod", "WASD, Space вверх, Ctrl вниз, Shift быстрее. Сквозь стены.", persist: false),
            S("flyspeed", "Скорость полёта", CatPhysics, 2f, 50f, 10f, 1f),
        };

        public static readonly WeatherDef[] Weathers =
        {
            new WeatherDef { Stage = 4, Name = "Ясно" },
            new WeatherDef { Stage = 3, Name = "Переменная облачность" },
            new WeatherDef { Stage = 5, Name = "Облачно" },
            new WeatherDef { Stage = 6, Name = "Лёгкий туман" },
            new WeatherDef { Stage = 0, Name = "Густой туман" },
            new WeatherDef { Stage = 1, Name = "Лёгкий снег" },
            new WeatherDef { Stage = 2, Name = "Сильный снег" },
            new WeatherDef { Stage = 7, Name = "Метель" },
            new WeatherDef { Stage = 8, Name = "Ясно, северное сияние" },
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

        private static FeatureDef T(string id, string name, string cat, string hotkey = "", string hint = "", bool persist = true) =>
            new FeatureDef { Id = id, Name = name, Category = cat, Kind = FeatureKind.Toggle, Hotkey = hotkey, Hint = hint, Persist = persist };

        private static FeatureDef S(string id, string name, string cat, float min, float max, float def, float step, string hint = "") =>
            new FeatureDef { Id = id, Name = name, Category = cat, Kind = FeatureKind.Slider, Min = min, Max = max, Default = def, Step = step, Hint = hint };

        private static FeatureDef A(string id, string name, string cat, string hotkey = "") =>
            new FeatureDef { Id = id, Name = name, Category = cat, Kind = FeatureKind.Action, Hotkey = hotkey, Persist = false };
    }
}
