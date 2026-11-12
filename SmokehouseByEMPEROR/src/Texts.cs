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
        // ---- «Заготовки к коптильне» (0.3.0)
        Add("Kettle_NAME", "Котёл для консервов", "Canning kettle");
        Add("Kettle_DESC", "Чугунный котёл на четырёх камнях, огонь под ним. Огонь под ним разжигают только на улице. В нём 24 часа варятся консервы: на банку идут 0,30 кг мяса или рыбы, пустая банка, четверть литра воды и соль.",
            "A cast-iron kettle on four stones with a fire under it. Its fire is lit outdoors only. Cans boil in it for 24 hours: a can takes 0.30 kg of meat or fish, an empty tin, a quarter litre of water and salt.");
        Add("BarrelWip_NAME", "Недостроенная коптильня", "Unfinished smokehouse");
        Add("KettleWip_NAME", "Недостроенный котёл для консервов", "Unfinished canning kettle");
        Add("TableWip_NAME", "Недостроенный колбасный стол", "Unfinished sausage table");
        foreach (var s in new[] { "Barrel", "Kettle", "Table" })
            Add(s + "Wip_DESC", "Работа начата, материалы уже в деле. Поставить и пользоваться нельзя. Достраивается у любого верстака тем же рецептом: он попросит только эту заготовку и оставшееся время.",
                "The work is begun, the materials are in it. It cannot be placed or used. Finish it at any workbench with the same recipe: it asks only for this piece and the time that is left.");
        Add("Table_NAME", "Колбасный стол", "Sausage table");
        Add("Table_DESC", "Стол с жердью для колбас. Ставится где угодно, огня не нужно. Полкило мяса, кишка и соль дают сырую колбасу одного из двух десятков видов. Готовят её только дымом в коптильне.",
            "A table with a pole for sausages. Stands anywhere and needs no fire. Half a kilo of meat, a gut and salt make a raw sausage of one of two dozen kinds. Only the smokehouse's smoke makes it food.");
        foreach (var c in Defs.Cans)
        {
            Add(c.Id + "_NAME", c.Ru, c.En);
            Add(c.Id + "_DESC", c.RuOf + ": сварено в банке за 24 часа в котле. Не портится, не пахнет, паразитов нет. Открывается без инструмента, пустая банка остаётся.",
                c.EnOf + ", boiled in a tin for 24 hours in the kettle. Never spoils, has no smell and no parasites. Opens with no tool; the empty tin is left.");
        }
        Add("SausRaw_NAME", "Сырая колбаса", "Raw sausage");
        Add("SausRaw_DESC", "Рубленое мясо с солью в кишке. Сырую есть опасно, портится быстро. Закоптите её в коптильне.",
            "Minced meat with salt in a gut. Unsafe to eat raw and quick to spoil. Smoke it in the smokehouse.");
        foreach (var k in Defs.Sausages)
        {
            bool all = k.Parts.Length > 4;
            string id = "Saus" + k.Id, of = all ? "все восемь видов рыбы" : string.Join(", ", k.Parts.Select(p => Defs.GroupNames[p.Group].Ru)), ofEn = all ? "all eight kinds of fish" : string.Join(", ", k.Parts.Select(p => Defs.GroupNames[p.Group].En));
            string h6 = Texts.Hours(Tune.SausHotFxHours * k.Hours), h12 = Texts.Hours(Tune.SausDryFxHours * k.Hours);
            Add(id + "Raw_NAME", k.RuRaw, "Raw " + k.En);
            Add(id + "Wip_NAME", k.RuRaw.Replace("Сырая", "Недоделанная").Replace("Сырые", "Недоделанные"), "Unfinished " + k.En);
            Add(id + "Wip_DESC", "Мясо (" + of + "), кишка и соль уже в деле, но набивка не закончена. Есть и коптить её нельзя. Доделывается за любым колбасным столом кнопкой «Продолжить».",
                "The meat (" + ofEn + "), the gut and the salt are already in it, but the stuffing is not finished. It cannot be eaten or smoked. Finish it at any sausage table with «Continue».");
            Add(id + "Raw_DESC", "Рубленое мясо (" + of + ") с солью в кишке. На огне её не готовят: закоптите в коптильне горячим дымом за 32 часа или холодным за 52. Сырую есть опасно, портится быстро.",
                "Minced meat (" + ofEn + ") with salt in a gut. It is not cooked on a fire: smoke it in the smokehouse, 32 hours hot or 52 hours cold. Unsafe to eat raw and quick to spoil.");
            Add(id + "Hot_NAME", k.Ru + " горячего копчения", "Hot-smoked " + k.En);
            Add(id + "Hot_DESC", "Прокопчена горячим дымом за 32 часа. Паразитов нет, портится впятеро медленнее жареного мяса. На " + h6 + " ч: калории расходуются медленнее, " + k.RuFx + ".",
                "Smoked hot for 32 hours. Free of parasites, spoils five times slower than cooked meat. For " + h6 + " hours: calories burn slower, " + k.EnFx + ".");
            Add(id + "Dry_NAME", k.Ru + " холодного копчения", "Cold-smoked " + k.En);
            Add(id + "Dry_DESC", "Провисела в холодном дыму 52 часа. Лёгкая, плотная и не портится. На " + h12 + " ч: калории расходуются медленнее, " + k.RuFx + "." + (k.Parasites ? " Холодный дым паразитов не убивает." : ""),
                "Hung in cold smoke for 52 hours. Light, dense, and it never spoils. For " + h12 + " hours: calories burn slower, " + k.EnFx + "." + (k.Parasites ? " Cold smoke does not kill parasites." : ""));
        }
        Add("SausBoiled_NAME", "Домашняя колбаса", "Home-made sausage");
        Add("SausBoiled_DESC", "Приготовлена на огне. Готова к еде, но портится так же быстро, как жареное мясо.", "Boiled or fried on a fire. Ready to eat, but spoils as fast as cooked meat.");
        Add("SausDry_NAME", "Сыровяленая колбаса", "Dry-cured sausage");
        Add("SausDry_DESC", "Провисела в холодном дыму коптильни 52 часа. Лёгкая, плотная и не портится.", "Hung in the smokehouse's cold smoke for 52 hours. Light, dense, and it never spoils.");
        Add("K_TITLE", "Котёл для консервов", "Canning kettle");
        Add("K_LIT", "Кипит", "Boiling");
        Add("K_NEED", "На банку:", "A can takes:");
        Add("K_CANS", "пустая банка (есть {0})", "an empty tin ({0} in the pack)");
        Add("K_WATER", "вода 0,25 л (есть {0} л)", "0.25 l of water ({0} l in the pack)");
        Add("K_SALT", "соль 300 г (есть {0} кг)", "salt 300 g ({0} kg in the pack)");
        Add("K_M_DEER", "оленина", "venison"); Add("K_M_MOOSE", "лосятина", "moose meat"); Add("K_M_BEAR", "медвежатина", "bear meat");
        Add("K_M_FISH", "любая рыба", "any fish"); Add("K_M_PATE", "заяц или куропатка", "rabbit or ptarmigan");
        Add("K_DIFF_FISH", "разные виды рыбы: {0}, каждой по {1} (у вас подходящих {2})", "{0} different kinds of fish, {1} of each (you have {2} that fit)");
        Add("K_NEED_PART", "Не хватает: {0}. Нужен один вид мяса на всю порцию", "Not enough: {0}. One kind of meat must cover the portion");
        Add("K_HAVE", "В рюкзаке {0} кг.", "{0} kg in the pack.");
        Add("K_ROW", "Варится 24 ч, не портится", "Boils for 24 h, never spoils");
        Add("K_DO", "В котёл", "Into the kettle");
        Add("K_PUT_IN", "Рецепты:", "Recipes:");
        Add("K_PORTION", "в банку {0} кг (в рюкзаке {1} кг)", "{0} kg per can ({1} kg in the pack)");
        Add("MSG_NEED_MEAT", "На банку нужно {0} кг", "A can takes {0} kg");
        Add("K_NO_RAW", "В рюкзаке нет подходящего мяса или рыбы", "No fitting meat or fish in the pack");
        Add("K_PICK_UP", "Забрать котёл", "Take the kettle");
        Add("K_MSG_BUSY", "Сначала заберите всё из котла и потушите огонь", "Take everything out and put the fire out first");
        Add("K_MSG_OUT", "Котёл погас: дрова кончились", "The kettle went out: no wood left");
        Add("K_MSG_READY", "Котёл: всё готово", "Kettle: everything is done");
        Add("MSG_NEED_CAN", "Нужна пустая консервная банка", "An empty tin is needed");
        Add("MSG_NEED_WATER", "Нужно 0,25 л питьевой воды", "0.25 l of drinking water is needed");
        Add("MSG_NEED_SALT", "Нужна соль", "Salt is needed");
        Add("T_TITLE", "Колбасный стол", "Sausage table");
        Add("T_NEED", "На любую колбасу, кроме мяса:", "Any sausage takes, besides the meat:");
        Add("T_DO", "Сделать", "Make");
        Add("T_CONTINUE", "Продолжить ({0}%)", "Continue ({0}%)");
        Add("T_WORK", "{0}: набивка", "{0}: stuffing");
        Add("B_PAUSED", "Постройка не закончена, готово {0}%. Заготовка в рюкзаке, достроить можно у любого верстака", "The build is not finished, {0}% done. The unfinished piece is in your pack, finish it at any workbench");
        Add("T_LEVEL", "Кулинария {0}", "Cooking {0}");
        Add("T_NEED_LEVEL", "Для этой колбасы нужен {0} уровень кулинарии", "This sausage needs Cooking level {0}");
        Add("T_BROKEN", "Работа прервана. Мясо, кишка и соль остались у вас", "The work was broken off. The meat, the gut and the salt are still yours");
        Add("T_PAUSED", "Колбаса не закончена, готово {0}%. Заготовка в рюкзаке, доделать можно за любым колбасным столом", "The sausage is not finished, {0}% done. It is in your pack, finish it at any sausage table");
        Add("T_ALLFISH", "все восемь видов рыбы по {0} кг", "all eight kinds of fish, {0} kg each");
        Add("T_GUTS", "кишка (есть {0})", "a gut ({0} in the pack)");
        Add("T_HINT", "Годится оленина, лосятина, заяц и куропатка.", "Venison, moose, rabbit and ptarmigan will do.");
        Add("T_MAKE", "Сделать колбасу из:", "Make a sausage of:");
        Add("T_NO_MEAT", "В рюкзаке нет подходящего сырого мяса", "No fitting raw meat in the pack");
        Add("T_NEXT", "Набивка занимает 45 минут. Сырую колбасу только коптят в коптильне: горячим дымом 32 ч, холодным 52 ч.", "Stuffing takes 45 minutes. A raw sausage is only smoked in the smokehouse: 32 h hot, 52 h cold.");
        Add("T_NEED_GUT", "Нужна кишка", "A gut is needed");
        Add("T_PORTION", "на колбасу {0} кг (в рюкзаке {1} кг)", "{0} kg per sausage ({1} kg in the pack)");
        Add("T_NEED_MEAT", "На эту колбасу не хватает мяса", "Not enough meat for this sausage");
        Add("SausHot_NAME", "Копчёная колбаса", "Smoked sausage");
        Add("SausHot_DESC", "Прокопчена горячим дымом в бочке за 32 часа. Готова к еде без огня, портится впятеро медленнее жареного мяса.", "Smoked hot in the barrel for 32 hours. Ready to eat with no fire, spoils five times slower than cooked meat.");
        Add("T_PICK_UP", "Забрать стол", "Take the table");
        Add("TITLE", "Коптильня", "Smokehouse");
        Add("R_TITLE", "Рецепты", "Recipes");
        Add("R_MEAT", "Копчёное мясо", "Smoked meat");
        Add("R_MEAT_NEED", "Сырое мясо любого зверя, один кусок на место", "Raw meat of any animal, one piece per place");
        Add("R_FISH", "Копчёная рыба", "Smoked fish");
        Add("R_FISH_NEED", "Любая сырая рыба, одна рыбина на место", "Any raw fish, one fish per place");
        Add("R_SAUS", "Копчёная колбаса", "Smoked sausage");
        Add("R_SAUS_NEED", "Сырая колбаса с колбасного стола", "A raw sausage from the sausage table");
        Add("R_HAVE_N", "В рюкзаке {0} шт.", "{0} in the pack");
        Add("R_ALSO", "Что ещё нужно", "What else it takes");
        Add("R_WOOD", "Дрова на {0} ч тления. Сейчас в топке: {1}", "Wood for {0} h of smouldering. In the firebox now: {1}");
        Add("R_SALT", "Соль по желанию, 300 г на кусок мяса или рыбы (есть {0} кг)", "Salt if you like, 300 g per piece of meat or fish ({0} kg in the pack)");
        Add("R_SLOTS", "Мест: {0}. Разжигается на улице", "Places: {0}. Lit outdoors");
        Add("R_WAYS", "Способы", "Ways");
        Add("R_HOT", "Горячее, 32 ч: остаётся 70% веса,", "Hot, 32 h: 70% of the weight is left,");
        Add("R_HOT2", "портится впятеро медленнее жареного", "spoils five times slower than cooked");
        Add("R_COLD", "Холодное, 52 ч: остаётся 55% веса,", "Cold, 52 h: 55% of the weight is left,");
        Add("R_COLD2", "не портится. Нужна готовка 4 уровня", "never spoils. Takes Cooking level 4");
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
        Add("NO_RAW", "В рюкзаке нет сырого мяса, рыбы или колбасы", "No raw meat, fish or sausage in the pack");
        Add("SALT", "Солить при закладке, 300 г на кусок (соли: {0} кг)", "Salt when putting in, 300 g a piece (salt: {0} kg)");
        Add("PICK_UP", "Забрать бочку", "Take the barrel");
        Add("CLOSE", "Закрыть", "Close");
        Add("MORE", "Дальше ({0})", "More ({0})");
        Add("WAIT_HOST", "(ждём хоста…)", "(waiting for the host…)");
        Add("MSG_OTHER", "Коптильню уже изменил другой игрок", "Another player has already changed the smokehouse");
        Add("MSG_NO_HOST", "Хост не ответил, ничего не изменилось", "No answer from the host, nothing was changed");
        Add("ACT_PLACE", "Поставить", "Place");
        Add("MSG_NO_PLACE", "Здесь это не поставить", "It cannot be placed here");
        Add("TAKE_WOOD", "Забрать дрова", "Take the wood");
        Add("MSG_WOOD_LIT", "Сначала потушите огонь", "Put the fire out first");
        Add("MSG_WOOD_BURNT", "Целых поленьев в топке нет", "No whole logs in the firebox");
        Add("MSG_FULL", "В коптильне нет места", "The smokehouse is full");
        Add("K_MSG_INDOORS", "Котёл разжигают только на улице", "The kettle is lit outdoors only");
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
        Add("HOVER_WARM", "рядом теплее на {0} °C", "{0} °C warmer beside it");
        Add("HOVER_ITEMS", "готово {0} из {1}", "{0} of {1} done");
        Add("FX_CAUSE", "Копчёное", "Smoked food");
        Add("BP_DESC", "Бочка, топка, крышка с трубой и прутья.", "A barrel, a firebox, a lid with a pipe, and rods.");
    }

    static string Hours(float h) => ((int)Math.Round(h)).ToString();

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
