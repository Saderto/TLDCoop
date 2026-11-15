namespace SnacksByEMPEROR;

/// <summary>All in-game text, Russian and English by the game's language (anything but Russian reads English). Keys start with SNACKS_.</summary>
public static class Texts
{
    static readonly Dictionary<string, string> ru = new(), en = new();

    static void Add(string key, string r, string e) { ru["SNACKS_" + key] = r; en["SNACKS_" + key] = e; }

    static string Thirst(float t, bool rus) => t >= 15 ? (rus ? "Бульон утоляет жажду." : "The broth quenches thirst.")
        : t > 0 ? (rus ? "Немного утоляет жажду." : "Quenches thirst a little.")
        : t <= -8 ? (rus ? "Очень солёное: после него сильно хочется пить." : "Very salty: it makes you really thirsty.")
        : (rus ? "Солёное: после него хочется пить." : "Salty: it makes you thirsty.");

    /// <summary>What the snack does, first thing in its card: one line per effect.</summary>
    static string FxLine(Def d, bool rus)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var f in d.Fx.Select(Effects.Get))
        {
            if (f == null) continue;
            sb.Append(f.Good ? "+ " : "\u2212 ").Append(rus ? f.Ru : f.En).Append(": ").Append(rus ? f.RuS : f.EnS)
              .Append(" (").Append(f.Hours.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)).Append(rus ? " ч)\n" : " h)\n");
        }
        return sb.ToString();
    }

    static Texts()
    {
        Add("FX_CAUSE", "Снеки", "Snacks");
        Add("FX_SELF", "Пройдёт само", "It will pass by itself");
        Add("FX_DRINK", "Попейте воды", "Have a drink of water");
        foreach (var f in Effects.All)
        {
            Add("FX_" + f.Id + "_NAME", f.Ru, f.En);
            Add("FX_" + f.Id + "_DESC", f.RuD, f.EnD);
        }
        foreach (var d in Defs.All)
        {
            switch (d.Kind)
            {
                case Kind.Snack:
                    Add(d.Id + "_NAME", d.Ru + ", " + d.RuF, d.En + ", " + d.EnF);
                    Add(d.Id + "_DESC", d.RuJ, d.EnJ);   // effects are icons on the item panel (EffectIcons)
                    break;
                case Kind.Noodle:
                    Add(d.Id + "_NAME", d.Ru, d.En);
                    string what = d.Shape == "brick" ? "Брикет лапши быстрого приготовления" : d.Shape == "tray" ? "Лапша в лотке" : "Лапша в стакане";
                    string whatEn = d.Shape == "brick" ? "A block of instant noodles" : d.Shape == "tray" ? "Noodles in a tray" : "Noodles in a cup";
                    string spicyRu = d.Spicy == 2 ? " Обжигающе острая: согревает намного сильнее и дольше." : d.Spicy == 1 ? " Острая: согревает сильнее и дольше." : "";
                    string spicyEn = d.Spicy == 2 ? " Scorching hot: warms you far more and longer." : d.Spicy == 1 ? " Spicy: warms you more and longer." : "";
                    Add(d.Id + "_DESC", $"{what}, {d.RuF}. Заваривается в котелке на огне: нужно {d.Water:0.##} л питьевой воды. Горячая даёт {d.Kcal:0} ккал и согревает.{spicyRu}",
                        $"{whatEn}, {d.EnF}. Brew it in a pot over a fire with {d.Water:0.##} L of drinking water. Hot, it gives {d.Kcal:0} kcal and warms you up.{spicyEn}");
                    break;
                case Kind.Brewed:
                    Add(d.Id + "_NAME", (d.Ru.StartsWith("Удон") ? "Заваренный " : "Заваренная ") + LowerFirst(d.Ru), "Brewed " + d.En);
                    Add(d.Id + "_DESC", $"Горячая лапша, {d.RuF}. {d.Kcal:0} ккал, согревает, пока не остынет. {Thirst(d.Thirst, true)} Съешьте в ближайшие дни.",
                        $"Hot noodles, {d.EnF}. {d.Kcal:0} kcal, warms you while it is hot. {Thirst(d.Thirst, false)} Eat it within a few days.");
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
        if (!key.StartsWith("SNACKS_", StringComparison.Ordinal)) key = "SNACKS_" + key;
        var table = Russian ? ru : en;
        return table.TryGetValue(key, out var s) || en.TryGetValue(key, out s) ? s : key;
    }
}

[HarmonyPatch(typeof(Localization), nameof(Localization.Get), new[] { typeof(string) })]
internal static class TextPatch
{
    static void Postfix(string key, ref string __result)
    {
        if (key == null || !key.StartsWith("SNACKS_", StringComparison.Ordinal)) return;
        __result = Texts.Get(key);
    }
}
