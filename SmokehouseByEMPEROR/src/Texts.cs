namespace SmokehouseByEMPEROR;

/// <summary>All in-game text, Russian and English by the game's language (anything but Russian reads English). Keys start with SMOKEHOUSE_.</summary>
public static class Texts
{
    static readonly Dictionary<string, string> ru = new(), en = new();

    static void Add(string key, string r, string e) { ru["SMOKEHOUSE_" + key] = r; en["SMOKEHOUSE_" + key] = e; }

    static Texts()
    {
        Add("Barrel_NAME", "Коптильня из бочки", "Barrel smokehouse");
        Add("Barrel_DESC", "Железная бочка на камнях с топкой, крышкой и трубой. Ставится на улице. Внутри на прутьях коптится мясо и рыба: горячим дымом за 32 часа, холодным за 52.",
            "A steel barrel on stones with a firebox, a lid and a pipe. Stands outdoors. Meat and fish smoke on the rods inside: hot in 32 hours, cold in 52.");
        foreach (var s in Defs.AllSpecies)
            foreach (bool hot in new[] { true, false })
                foreach (bool salted in new[] { false, true })
                {
                    string id = Defs.OutId(s, hot, salted);
                    string enName = (hot ? "Hot-smoked " : "Cold-smoked ") + s.En + (salted ? ", salted" : "");
                    string ruName = s.Ru + (hot ? " горячего копчения" : " холодного копчения") + (salted ? ", с солью" : "");
                    Add(id + "_NAME", ruName, enName);
                    string r = hot
                        ? "Прокопчено горячим дымом в бочке. Готово к еде без огня, паразитов нет, портится впятеро медленнее жареного."
                        : "Провялено холодным дымом за 52 часа. Лёгкое и плотное, не портится. Едят без огня.";
                    string e = hot
                        ? "Smoked hot in the barrel. Ready to eat with no fire, free of parasites, spoils five times slower than cooked."
                        : "Cured in cold smoke for 52 hours. Light and dense, it never spoils. Eaten with no fire.";
                    if (!hot && !s.Fish && (s.Id == "Wolf" || s.Id == "Bear" || s.Id == "Cougar"))
                    {
                        r += " Холодный дым паразитов не убивает.";
                        e += " Cold smoke does not kill parasites.";
                    }
                    if (salted)
                    {
                        r += s.Fish ? " Посолено: лучше держит на холоде, но сильнее хочется пить." : " Посолено: усталость копится медленнее, но сильнее хочется пить.";
                        e += s.Fish ? " Salted: it helps against the cold, but thirst grows faster." : " Salted: you tire slower, but thirst grows faster.";
                    }
                    Add(id + "_DESC", r, e);
                }
        Add("TITLE", "Коптильня", "Smokehouse");
        Add("MODE_HOT", "Горячее копчение, 32 ч", "Hot smoking, 32 h");
        Add("MODE_COLD", "Холодное копчение, 52 ч", "Cold smoking, 52 h");
        Add("LIT", "Дымит", "Smoking");
        Add("UNLIT", "Не горит", "Not lit");
        Add("FUEL", "Дров на {0}", "Wood for {0}");
        Add("NOFUEL", "Дров нет", "No wood");
        Add("H", "{0} ч", "{0} h");
        Add("M", "{0} мин", "{0} min");
        Add("HM", "{0} ч {1} мин", "{0} h {1} min");
        Add("EMPTY", "пусто", "empty");
        Add("DONE", "готово", "done");
        Add("LEFT", "ещё {0}", "{0} left");
        Add("KG", "{0} кг", "{0} kg");
        Add("SALTED", "с солью", "salted");
        Add("TAKE", "Забрать", "Take");
        Add("TAKE_RAW", "Вынуть", "Take out");
        Add("LIGHT", "Разжечь", "Light");
        Add("PUT_OUT", "Потушить", "Put out");
        Add("ADD_WOOD", "Подбросить дров:", "Add wood:");
        Add("NO_WOOD_INV", "В рюкзаке нет дров или палок", "No wood or sticks in the pack");
        Add("PUT_IN", "Положить в коптильню:", "Put in the smokehouse:");
        Add("NO_RAW", "В рюкзаке нет сырого мяса или рыбы", "No raw meat or fish in the pack");
        Add("SALT", "Солить при закладке (соли: {0})", "Salt when putting in (salt: {0})");
        Add("PICK_UP", "Забрать бочку", "Take the barrel");
        Add("CLOSE", "Закрыть", "Close");
        Add("MORE", "Дальше ({0})", "More ({0})");
        Add("WAIT_HOST", "(ждём хоста…)", "(waiting for the host…)");
        Add("MSG_OTHER", "Коптильню уже изменил другой игрок", "Another player has already changed the smokehouse");
        Add("MSG_NO_HOST", "Хост не ответил, ничего не изменилось", "No answer from the host, nothing was changed");
        Add("ACT_PLACE", "Поставить", "Place");
        Add("MSG_NO_PLACE", "Здесь бочку не поставить", "The barrel cannot be placed here");
        Add("TAKE_WOOD", "Забрать дрова", "Take the wood");
        Add("MSG_WOOD_LIT", "Сначала потушите огонь", "Put the fire out first");
        Add("MSG_WOOD_BURNT", "Целых поленьев в топке нет", "No whole logs in the firebox");
        Add("MSG_FULL", "В коптильне нет места", "The smokehouse is full");
        Add("MSG_INDOORS", "Коптильню разжигают только на улице", "A smokehouse is lit outdoors only");
        Add("MSG_NO_STARTER", "Нечем разжечь: нужны спички или огниво", "Nothing to light it with: matches or a firestriker are needed");
        Add("MSG_NO_FUEL", "Сначала подбросьте дров", "Add wood first");
        Add("MSG_FUEL_FULL", "Топка полна", "The firebox is full");
        Add("MSG_SKILL", "Холодное копчение: нужен {0} уровень готовки", "Cold smoking needs Cooking level {0}");
        Add("MSG_MODE_BUSY", "Способ меняют, пока продукты ещё не начали коптиться", "The way is changed before the food has started smoking");
        Add("MSG_BUSY", "Сначала заберите продукты и потушите огонь", "Take the food out and put the fire out first");
        Add("MSG_OUT", "Коптильня погасла: дрова кончились", "The smokehouse went out: no wood left");
        Add("MSG_READY", "Коптильня: всё готово", "Smokehouse: everything is done");
        Add("HOVER_USE", "Коптильня", "Smokehouse");
        Add("HOVER_ITEMS", "готово {0} из {1}", "{0} of {1} done");
        Add("FX_CAUSE", "Копчёное", "Smoked food");
        Add("BP_DESC", "Бочка, топка, крышка с трубой и прутья.", "A barrel, a firebox, a lid with a pipe, and rods.");
    }

    public static int Count => en.Count;

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
        if (!key.StartsWith("SMOKEHOUSE_", StringComparison.Ordinal)) key = "SMOKEHOUSE_" + key;
        var table = Russian ? ru : en;
        return table.TryGetValue(key, out var s) || en.TryGetValue(key, out s) ? s : key;
    }

    public static string Get(string key, params object[] args)
    {
        try { return string.Format(Get(key), args); }
        catch { return Get(key); }
    }

    /// <summary>«2 ч 15 мин», «40 мин».</summary>
    public static string Time(float hours)
    {
        int m = Math.Max(0, (int)Math.Round(hours * 60f));
        if (m < 60) return Get("M", m);
        return m % 60 == 0 ? Get("H", m / 60) : Get("HM", m / 60, m % 60);
    }
}

[HarmonyPatch(typeof(Localization), nameof(Localization.Get), new[] { typeof(string) })]
internal static class TextPatch
{
    static void Postfix(string key, ref string __result)
    {
        if (key == null || !key.StartsWith("SMOKEHOUSE_", StringComparison.Ordinal)) return;
        __result = Texts.Get(key);
    }
}
