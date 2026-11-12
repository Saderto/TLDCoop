namespace JellyfishByEMPEROR;

/// <summary>All in-game text, Russian and English by the game's language (anything but Russian reads English). Keys start with JELLYFISH_.</summary>
public static class Texts
{
    static readonly Dictionary<string, string> ru = new(), en = new();

    static void Add(string key, string r, string e) { ru["JELLYFISH_" + key] = r; en["JELLYFISH_" + key] = e; }

    static readonly Dictionary<string, (string Ru, string En)> facts = new()
    {
        ["LionsMane"] = ("Самая большая медуза на свете: в открытом море её щупальца вырастают на десятки метров. Эта ещё молодая. Жжётся сильно.",
                         "The largest jellyfish in the world: out at sea its tentacles grow tens of metres long. This one is still young. Its sting is bad."),
        ["Aurelia"] = ("Самая обычная медуза северных морей: плоский прозрачный купол и четыре кольца внутри. Жжётся еле-еле.",
                       "The most common jellyfish of northern seas: a flat clear bell with four rings inside. Its sting is barely felt."),
        ["Crystal"] = ("Почти невидимая, но по краю купола светится сама: из неё учёные когда-то добыли светящийся белок. Жжётся слабо.",
                       "Almost invisible, yet the rim of its bell lights up by itself: scientists once took a glowing protein from it. A weak sting."),
        ["SeaNettle"] = ("Купол в полоску и длинные ленты щупалец. Жжётся сильнее всех здешних медуз: без перчаток лучше не трогать.",
                         "A striped bell and long ribbon arms. The worst sting of all jellyfish here: don't touch it with bare hands."),
        ["Periphylla"] = ("Глубоководная медуза-шлем. Поднимается к поверхности только ночью и вспыхивает голубым светом. Редкая.",
                          "A deep-sea helmet jellyfish. It rises to the surface only at night and flashes blue. Rare."),
        ["CombJelly"] = ("Строго говоря, не медуза, а гребневик: стеклянный шарик, по рядам ресничек которого бегут радужные полосы. Не жжётся.",
                         "Not a true jellyfish but a comb jelly: a glass bead with rainbows running along its rows of combs. It doesn't sting."),
    };

    static Texts()
    {
        Add("Jar_NAME", "Стеклянная банка", "Glass jar");
        Add("Jar_DESC", "Пустая банка с закручивающейся крышкой. Если прорубить лунку на морском льду, в ней можно унести живую медузу.",
            "An empty jar with a screw lid. Cut a hole in the sea ice and you can carry a live jellyfish home in it.");
        foreach (var s in Defs.AllSpecies)
            foreach (var c in s.Colours)
            {
                string id = Defs.LiveId(s, c);
                Add(id + "_NAME", $"{s.Ru} в банке, {c.Ru} окрас", $"{s.En} in a jar, {c.En}");
                var f = facts[s.Id];
                Add(id + "_DESC", f.Ru + " Поставь банку: медуза плавает и ночью светится. Держи её в тепле: ниже −5° вода замерзает, и за полтора часа медуза погибнет.",
                    f.En + " Set the jar down: it swims and glows at night. Keep it warm: below −5° the water freezes and in an hour and a half the jellyfish dies.");
                Add("SP_" + s.Id, s.Ru, s.En);
                Add("COL_" + s.Id + "_" + c.Id, c.Ru, c.En);
            }
        Add("Aquarium_NAME", "Аквариум", "Aquarium");
        Add("Aquarium_DESC", "Стеклянный аквариум с песком и водорослями. Перелей в него медуз из банок, до трёх, и они будут плавать на свободе. Вода в нём тоже замерзает на холоде.",
            "A glass tank with sand and kelp. Pour in jellyfish from their jars, up to three, and they swim free. Its water freezes in the cold too.");

        Add("CATCH_TITLE", "В лунке медуза!", "A jellyfish in the hole!");
        Add("CATCH_WHAT", "{0}, {1} окрас", "{0}, {1}");
        Add("CATCH_JAR", "Зачерпнуть банкой ({0})", "Scoop up with a jar ({0})");
        Add("CATCH_NOJAR", "Нет пустой банки", "No empty jar");
        Add("CATCH_LET", "Отпустить", "Let it go");
        Add("CATCH_KEYS", "1: банкой   2: отпустить", "1: jar   2: let go");
        Add("MSG_SANK", "Медуза ушла под воду", "The jellyfish sank out of sight");
        Add("MSG_JAR", "Медуза в банке: {0}", "Jellyfish in the jar: {0}");
        Add("MSG_GONE", "Медузу уже забрали", "Someone already took the jellyfish");
        Add("MSG_DIED", "Медуза погибла: {0}", "The jellyfish died: {0}");
        Add("MSG_DIED_TANK", "Медуза в аквариуме погибла: {0}", "A jellyfish in the aquarium died: {0}");
        Add("MSG_NO_HOST", "Хост не ответил, попробуй ещё раз", "The host did not answer, try again");
        Add("MSG_OTHER", "Не вышло: аквариум уже изменился", "Didn't work: the aquarium has changed");
        Add("MSG_FULL", "В аквариуме уже три медузы", "The aquarium already holds three");
        Add("MSG_POURED", "Медуза в аквариуме: {0}", "Into the aquarium: {0}");
        Add("MSG_BUSY", "Сначала забери медуз из аквариума", "Take the jellyfish out of the aquarium first");
        Add("DEAD", "погибла", "dead");
        Add("TANK_TITLE", "Аквариум", "Aquarium");
        Add("TANK_COUNT", "Медуз: {0} из {1}", "Jellyfish: {0} of {1}");
        Add("TANK_EMPTY", "Аквариум пуст. Перелей сюда медузу из банки.", "The tank is empty. Pour in a jellyfish from a jar.");
        Add("TANK_TAKE", "В банку", "Into a jar");
        Add("TANK_NOJAR", "Нет пустой банки", "No empty jar");
        Add("TANK_REMOVE", "Убрать", "Remove");
        Add("TANK_POUR", "Перелить из банки", "Pour in from a jar");
        Add("TANK_POUR_BTN", "Перелить", "Pour in");
        Add("TANK_NONE", "В рюкзаке нет банок с живой медузой", "No jar with a live jellyfish in your pack");
        Add("TANK_LIFT", "Забрать аквариум", "Take the aquarium");
        Add("TANK_CLOSE", "Закрыть", "Close");
        Add("TANK_WAIT", "Жду ответа хоста…", "Waiting for the host…");
        Add("HOVER_TANK", "Медуз: {0} из {1}", "Jellyfish: {0} of {1}");
        Add("ACT_PLACE", "Поставить", "Place");
        Add("HOVER_COLD", "Вода стынет!", "The water is freezing!");
        Add("HOVER_COND", "Состояние {0}%", "Condition {0}%");
    }

    public static int Count => ru.Count;

    public static bool Russian
    {
        get
        {
            try { return Localization.Language == "Russian"; }
            catch { return false; }
        }
    }

    public static string Get(string key)
    {
        if (!key.StartsWith("JELLYFISH_", StringComparison.Ordinal)) key = "JELLYFISH_" + key;
        var table = Russian ? ru : en;
        return table.TryGetValue(key, out var s) || en.TryGetValue(key, out s) ? s : key;
    }

    public static string Get(string key, params object[] args)
    {
        try { return string.Format(Get(key), args); }
        catch { return Get(key); }
    }

    public static string Name(Species s, Colour c) => Get("CATCH_WHAT", Get("SP_" + s.Id), Get("COL_" + s.Id + "_" + c.Id));
}

[HarmonyPatch(typeof(Localization), nameof(Localization.Get), new[] { typeof(string) })]
internal static class TextPatch
{
    static void Postfix(string key, ref string __result)
    {
        if (key == null || !key.StartsWith("JELLYFISH_", StringComparison.Ordinal)) return;
        __result = Texts.Get(key);
    }
}
