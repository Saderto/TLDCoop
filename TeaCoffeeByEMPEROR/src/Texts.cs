namespace TeaCoffeeByEMPEROR;

/// <summary>All in-game text, Russian and English by the game's language (anything but Russian reads English). Keys start with TEACOFFEE_.</summary>
public static class Texts
{
    const string K = "TEACOFFEE_";
    static readonly Dictionary<string, string> ru = new(), en = new();

    static Texts()
    {
        foreach (var (key, r, e) in Catalog.Texts) { ru[K + key] = r; en[K + key] = e; }
        ru[K + "FX_CAUSE"] = "Чай и кофе"; en[K + "FX_CAUSE"] = "Tea and coffee";
        foreach (var f in Catalog.Fx)
        {
            ru[K + "FX_" + f.Key + "_NAME"] = f.Ru; en[K + "FX_" + f.Key + "_NAME"] = f.En;
            ru[K + "FXTIP_" + f.Key] = f.Ru; en[K + "FXTIP_" + f.Key] = f.En;
            ru[K + "FX_" + f.Key + "_DESC"] = char.ToUpper(f.RuD[0]) + f.RuD.Substring(1) + "."; en[K + "FX_" + f.Key + "_DESC"] = char.ToUpper(f.EnD[0]) + f.EnD.Substring(1) + ".";
        }
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
        if (!key.StartsWith(K, StringComparison.Ordinal)) key = K + key;
        var table = Russian ? ru : en;
        return table.TryGetValue(key, out var s) || en.TryGetValue(key, out s) ? s : key;
    }
}

[HarmonyPatch(typeof(Localization), nameof(Localization.Get), new[] { typeof(string) })]
internal static class TextPatch
{
    static void Postfix(string key, ref string __result)
    {
        if (key == null || !key.StartsWith("TEACOFFEE_", StringComparison.Ordinal)) return;
        __result = Texts.Get(key);
    }
}
