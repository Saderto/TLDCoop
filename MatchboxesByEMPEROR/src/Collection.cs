using System.IO;
using System.Text.Json;

namespace MatchboxesByEMPEROR;

public class CollectionState
{
    public List<string> Labels { get; set; } = new();
}

/// <summary>The labels found in this world. Only grows: the first time a label comes into the pack, «New label: N of 120».
/// Kept per world (save slot + world seed, the owner's rule) in Mods/MatchboxesByEMPEROR/saves.</summary>
public static class Collection
{
    public static CollectionState State = new();
    public static bool Quiet;
    static string? loadedKey;

    public static int Found => State.Labels.Count(id => Defs.ById(id) != null);
    public static bool Has(string id) => State.Labels.Contains(id);
    public static int FoundIn(SeriesDef s) => Defs.All.Count(d => d.Series.Id == s.Id && Has(d.Id));

    public static void Say(string key, params object[] args)
    {
        try { HUDMessage.AddMessage(Texts.Get(key, args), false, true); } catch (Exception e) { Safe.Log("HUD message", e); }
    }

    public static void Collected(Def def, bool quiet)
    {
        if (Has(def.Id)) return;
        State.Labels.Add(def.Id);
        Safe.Info($"Matchboxes: {def.Id} collected ({Found} of {Defs.All.Count}){(quiet ? " quietly" : "")}");
        if (quiet) return;
        Say("MSG_NEW", Texts.Get("LABEL_" + def.Id), Found, Defs.All.Count);
        if (FoundIn(def.Series) == def.Series.Count) Say("MSG_SERIES", Texts.Get("SERIES_" + def.Series.Id));
    }

    /// <summary>Boxes already in the pack count, quietly.</summary>
    public static void SyncFromPack()
    {
        try
        {
            var inv = GameManager.GetInventoryComponent();
            if (inv == null) return;
            foreach (var o in inv.m_Items)
                if (o?.m_GearItem is { } gi && Defs.Is(gi, out var d)) Collected(d, true);
        }
        catch (Exception e) { Safe.Log("Labels from the pack", e); }
    }

    public static System.Collections.IEnumerator SyncLater()
    {
        float until = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < until) yield return null;
        SyncFromPack();
    }

    static string Folder => Path.Combine(Mod.Root, "saves");

    /// <summary>Slot + world seed: a new game in the same slot is another world (the game id is 0 until the first save, so it is left out).</summary>
    public static string WorldKey()
    {
        string slot = SaveGameSystem.GetCurrentSaveName() ?? "";
        return slot + "|" + GameManager.m_SceneTransitionData.m_GameRandomSeed.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    static string FileOf(string key) => Path.Combine(Folder, new string(key.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_').ToArray()) + ".json");

    public static void Load()
    {
        State = new CollectionState();
        try
        {
            loadedKey = WorldKey();
            var path = FileOf(loadedKey);
            if (File.Exists(path)) State = JsonSerializer.Deserialize<CollectionState>(File.ReadAllText(path)) ?? new CollectionState();
            Safe.Info($"Matchboxes: collection of {loadedKey}: {Found} labels");
        }
        catch (Exception e) { MelonLogger.Error("Matchbox collection unreadable, starting clean: " + e.Message); }
    }

    public static void Save()
    {
        try
        {
            string key = WorldKey();
            if (string.IsNullOrEmpty(key) || key.StartsWith("|", StringComparison.Ordinal)) return;
            loadedKey = key;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FileOf(key), JsonSerializer.Serialize(State));
        }
        catch (Exception e) { Safe.Log("Saving the matchbox collection", e); }
    }
}

[HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.SaveGlobalData))]
internal static class SavePatch { static void Postfix() => Collection.Save(); }

[HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.RestoreGlobalData))]
internal static class LoadPatch
{
    static void Postfix()
    {
        Collection.Load();
        MelonCoroutines.Start(Collection.SyncLater());
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.LaunchSandbox))]
internal static class NewGamePatch { static void Prefix() => Collection.State = new CollectionState(); }

/// <summary>A box of ours coming into the pack counts for the collection; quiet while a save loads.</summary>
[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddGear))]
internal static class CollectPatch
{
    static void Postfix(GearItem gi)
    {
        try
        {
            if (!Defs.Is(gi, out var def)) return;
            bool quiet = Collection.Quiet || GameManager.IsMainMenuActive() || SaveGameSystem.IsRestoreInProgress() || Time.timeSinceLevelLoad < 8f;
            Collection.Collected(def, quiet);
        }
        catch (Exception e) { Safe.Log("Label collection", e); }
    }
}
