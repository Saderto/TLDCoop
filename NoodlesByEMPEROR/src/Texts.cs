namespace NoodlesByEMPEROR;

/// <summary>All in-game text, Russian and English by the game's language (anything but Russian reads English). Keys start with NOODLES_.</summary>
public static class Texts
{
    static readonly Dictionary<string, string> ru = new(), en = new();

    static void Add(string key, string r, string e) { ru["NOODLES_" + key] = r; en["NOODLES_" + key] = e; }

    static string Thirst(float t, bool rus) => t >= 15 ? (rus ? "Бульон утоляет жажду." : "The broth quenches thirst.")
        : t > 0 ? (rus ? "Немного утоляет жажду." : "Quenches thirst a little.")
        : t <= -10 ? (rus ? "Без бульона и жгучая: после неё сильно хочется пить." : "No broth and fiery: it makes you really thirsty.")
        : (rus ? "Без бульона: после неё хочется пить." : "No broth: it makes you thirsty.");

    static string Spicy(int level, bool rus) => level switch
    {
        1 => rus ? " Слегка острая: греет чуть сильнее." : " Mildly spicy: warms you a little more.",
        2 => rus ? " Острая: греет сильнее и дольше." : " Spicy: warms you more and longer.",
        3 => rus ? " Очень острая: греет намного сильнее и дольше." : " Very spicy: warms you far more and longer.",
        4 => rus ? " Адски острая: греет сильнее и дольше всех." : " Hellishly spicy: warms you the most and the longest.",
        _ => "",
    };

    static string Sentence(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1) + ".";

    static Texts()
    {
        Add("FX_CAUSE", "Лапша быстрого приготовления", "Instant noodles");
        Add("FX_SELF", "Пройдёт само", "It will pass by itself");
        foreach (var f in Effects.All)
        {
            Add("FX_" + f.Key + "_NAME", f.Ru, f.En);
            Add("FX_" + f.Key + "_DESC", Sentence(f.RuD), Sentence(f.EnD));
        }
        foreach (var d in Defs.All)
        {
            switch (d.Kind)
            {
                case Kind.Noodle:
                {
                    Add(d.Id + "_NAME", d.Ru, d.En);
                    // user 11-15: a meme description and nothing else but the note on the heat
                    Add(d.Id + "_DESC", d.RuM + Spicy(d.Spicy, true), d.EnM + Spicy(d.Spicy, false));
                    break;
                }
                case Kind.Brewed:
                    Add(d.Id + "_NAME", (d.Ru.StartsWith("Удон") ? "Готовый " : "Готовая ") + LowerFirst(d.Ru), "Brewed " + d.En);
                    Add(d.Id + "_DESC", d.RuM + Spicy(d.Spicy, true), d.EnM + Spicy(d.Spicy, false));
                    break;
            }
        }
    }

    static string LowerFirst(string s)
    {
        // «Лапша «Роллтан»» -> «лапша «Роллтан»»; names that begin with a brand keep it
        if (s.Length > 1 && char.IsUpper(s[0]) && char.IsLower(s[1])) return char.ToLowerInvariant(s[0]) + s.Substring(1);
        return s;
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
        if (!key.StartsWith("NOODLES_", StringComparison.Ordinal)) key = "NOODLES_" + key;
        var table = Russian ? ru : en;
        return table.TryGetValue(key, out var s) || en.TryGetValue(key, out s) ? s : key;
    }
}

[HarmonyPatch(typeof(Localization), nameof(Localization.Get), new[] { typeof(string) })]
internal static class TextPatch
{
    static void Postfix(string key, ref string __result)
    {
        if (key == null || !key.StartsWith("NOODLES_", StringComparison.Ordinal)) return;
        __result = Texts.Get(key);
    }
}
