using System.Text.Json;

namespace SmokesByEMPEROR;

/// <summary>All in-game text, Russian and English by the game's language (anything but Russian reads English). Keys start with SMOKES_.</summary>
public static class Texts
{
    static Dictionary<string, string> ru = new(), en = new();

    public static int Count => en.Count;

    public static void Load(string root)
    {
        ru = Read(Path.Combine(root, "data", "text_ru.json"));
        en = Read(Path.Combine(root, "data", "text_en.json"));
        foreach (var kv in Read(Path.Combine(root, "data", "text_var_ru.json"))) ru[kv.Key] = kv.Value;
        foreach (var kv in Read(Path.Combine(root, "data", "text_var_en.json"))) en[kv.Key] = kv.Value;
        foreach (var kv in Read(Path.Combine(root, "data", "text_cgl_ru.json"))) ru[kv.Key] = kv.Value;
        foreach (var kv in Read(Path.Combine(root, "data", "text_cgl_en.json"))) en[kv.Key] = kv.Value;
        foreach (var kv in Read(Path.Combine(root, "data", "text_cigar_ru.json"))) ru[kv.Key] = kv.Value;
        foreach (var kv in Read(Path.Combine(root, "data", "text_cigar_en.json"))) en[kv.Key] = kv.Value;
        foreach (var kv in Read(Path.Combine(root, "data", "text_pouch_ru.json"))) ru[kv.Key] = kv.Value;
        foreach (var kv in Read(Path.Combine(root, "data", "text_pouch_en.json"))) en[kv.Key] = kv.Value;
        foreach (var kv in Read(Path.Combine(root, "data", "text_lighter_ru.json"))) ru[kv.Key] = kv.Value;
        foreach (var kv in Read(Path.Combine(root, "data", "text_lighter_en.json"))) en[kv.Key] = kv.Value;
    }

    static Dictionary<string, string> Read(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new() : new();
        }
        catch (Exception e)
        {
            MelonLogger.Error("Text file unreadable " + path + ": " + e.Message);
            return new();
        }
    }

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
        var table = Russian ? ru : en;
        if (table.TryGetValue(key, out var s) || en.TryGetValue(key, out s)) return s;
        // a variety's lit cigarette: named after its pack, described as the plain one
        if (key.StartsWith("SMOKES_LitCig_", StringComparison.Ordinal))
        {
            if (key.EndsWith("_NAME", StringComparison.Ordinal))
            {
                string pack = Get("SMOKES_" + key.Substring("SMOKES_LitCig_".Length));
                if (Russian) return (pack.StartsWith("Сигареты ") ? "Сигарета " + pack.Substring(9) : pack.StartsWith("Сигариллы ") ? "Сигарилла " + pack.Substring(10) : pack) + " (курится)";
                return (pack.EndsWith(" cigarettes") ? pack.Substring(0, pack.Length - 11) + " cigarette" : pack.EndsWith(" cigarillos") ? pack.Substring(0, pack.Length - 1) : pack) + " (lit)";
            }
            return Get("SMOKES_LitCigarette_DESC");
        }
        return key;
    }

    public static string Get(string key, params object[] args)
    {
        try { return string.Format(Get(key), args); }
        catch { return Get(key); }
    }
}

[HarmonyPatch(typeof(Localization), nameof(Localization.Get), new[] { typeof(string) })]
internal static class TextPatch
{
    static void Postfix(string key, ref string __result)
    {
        if (key == null || !key.StartsWith("SMOKES_", StringComparison.Ordinal)) return;
        __result = Texts.Get(key);
    }
}
