namespace MatchboxesByEMPEROR;

/// <summary>All in-game text, Russian and English by the game's language (anything but Russian reads English). Keys start with MATCHBOX_.</summary>
public static class Texts
{
    static readonly Dictionary<string, string> ru = new(), en = new();

    static void Add(string key, string r, string e) { ru["MATCHBOX_" + key] = r; en["MATCHBOX_" + key] = e; }

    static readonly Dictionary<string, (string Ru, string En)> kindText = new()
    {
        ["Std"] = ("Обычные деревянные спички, 40 штук. Ими разводят огонь, зажигают факел и прикуривают.",
                   "Ordinary wooden matches, 40 of them. Light fires and torches with them, or a cigarette."),
        ["Storm"] = ("Охотничьи штормовые спички, 20 штук: толстая головка не гаснет на сильном ветру, горит дольше и разжигает огонь легче (+10%).",
                     "Hunter's storm matches, 20 of them: the thick head stays lit in a strong wind, burns longer and starts a fire more easily (+10%)."),
        ["Long"] = ("Длинные каминные спички, 25 штук: горят в два с половиной раза дольше обычных, огонь разводить легче (+5%).",
                    "Long fireplace matches, 25 of them: they burn two and a half times as long as ordinary ones and start a fire more easily (+5%)."),
        ["Book"] = ("Картонная книжечка на 20 спичек из бара или закусочной. Лёгкая, но картонные спички хуже деревянных.",
                    "A cardboard matchbook of 20 from a bar or a diner. Light, but paper matches are worse than wooden ones."),
        ["Souvenir"] = ("Большой сувенирный коробок на 100 деревянных спичек.",
                        "A big souvenir box of 100 wooden matches."),
    };

    static Texts()
    {
        foreach (var d in Defs.All)
        {
            var l = d.Label; var s = d.Series; var k = d.Kind;
            Add(d.Id + "_NAME", $"{k.Ru} «{l.Ru}»", $"{k.En} “{l.En}”");
            // 0.3.4 (the owner): one joke line for every label; the two kinds that change the game keep a few words about it
            var joke = Catalog.Memes.TryGetValue(d.Id, out var j) ? j : (kindText[k.Id].Ru, kindText[k.Id].En);
            string extraRu = k.Id == "Storm" ? " Не гаснут на ветру, розжиг +10%." : k.Id == "Long" ? " Горят в 2,5 раза дольше, розжиг +5%." : "";
            string extraEn = k.Id == "Storm" ? " Stay lit in the wind, fire starting +10%." : k.Id == "Long" ? " Burn 2.5 times longer, fire starting +5%." : "";
            Add(d.Id + "_DESC", joke.Item1 + extraRu, joke.Item2 + extraEn);
            Add("LABEL_" + d.Id, l.Ru, l.En);
        }
        foreach (var s in Catalog.Series) Add("SERIES_" + s.Id, s.Ru, s.En);
        foreach (var k in Catalog.Kinds) Add("KIND_" + k.Id, k.Ru, k.En);
        Add("COLL", "Спичечные этикетки", "Matchbox labels");
        Add("COLL_DESC", "Этикетки со спичечных коробков, найденных в этом мире: 12 серий по 10 штук. Наведи на этикетку, чтобы рассмотреть её.",
            "Labels from the matchboxes found in this world: 12 series of 10. Point at a label to look at it closely.");
        Add("COLL_COUNT", "Собрано {0} из {1}", "Collected {0} of {1}");
        Add("COLL_SERIES", "Серия «{0}»: {1} из {2}", "Series “{0}”: {1} of {2}");
        Add("COLL_UNKNOWN", "???", "???");
        Add("COLL_UNKNOWN_DESC", "Эту этикетку ты ещё не находил. Ищи спички в шкафах, ящиках и карманах.",
            "You haven't found this label yet. Look for matches in cupboards, drawers and pockets.");
        Add("COLL_WHERE", "{0}  ·  № {1} из {2}", "{0}  ·  No. {1} of {2}");
        Add("MSG_NEW", "Новая этикетка: «{0}» ({1} из {2})", "New label: “{0}” ({1} of {2})");
        Add("MSG_SERIES", "Серия «{0}» собрана полностью!", "The “{0}” series is complete!");
        Add("MSG_COUNT", "Этикеток в коллекции: {0} из {1}", "Labels in the collection: {0} of {1}");
    }

    public static int Count => ru.Count;

    public static bool Russian
    {
        get { try { return Localization.Language == "Russian"; } catch { return false; } }
    }

    public static string Get(string key)
    {
        if (!key.StartsWith("MATCHBOX_", StringComparison.Ordinal)) key = "MATCHBOX_" + key;
        var table = Russian ? ru : en;
        return table.TryGetValue(key, out var s) || en.TryGetValue(key, out s) ? s : key;
    }

    public static string Get(string key, params object[] args)
    {
        try { return string.Format(Get(key), args); } catch { return Get(key); }
    }
}

[HarmonyPatch(typeof(Localization), nameof(Localization.Get), new[] { typeof(string) })]
internal static class TextPatch
{
    static void Postfix(string key, ref string __result)
    {
        if (key == null || !key.StartsWith("MATCHBOX_", StringComparison.Ordinal)) return;
        __result = Texts.Get(key);
    }
}
