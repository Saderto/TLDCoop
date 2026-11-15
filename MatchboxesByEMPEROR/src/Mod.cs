using System.IO;
[assembly: MelonInfo(typeof(MatchboxesByEMPEROR.Mod), "MatchboxesByEMPEROR", MatchboxesByEMPEROR.Mod.VersionText, "EMPEROR")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace MatchboxesByEMPEROR;

/// <summary>Collectible matchboxes (11-14): 120 labels in 12 series on five kinds of box. Each box is real matches (fire, torches,
/// SmokesByEMPEROR lights cigarettes with them); every new label goes into the journal's Collections, per world.</summary>
public class Mod : MelonMod
{
    public const string VersionText = "0.3.5";

    public static string Root => Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "MatchboxesByEMPEROR");

    public override void OnInitializeMelon()
    {
        Settings.Create();
        try { ContainerOpenPatch.Apply(HarmonyInstance); } catch (Exception e) { Safe.Log("Loot patches", e); }
        LoggerInstance.Msg($"MatchboxesByEMPEROR {VersionText}: {Defs.All.Count} matchboxes in {Catalog.Series.Length} series, {Texts.Count} text keys.");
    }

    public override void OnGUI() => Journal.Draw();

    public override void OnSceneWasInitialized(int buildIndex, string sceneName) => ConsoleNames.Fill();

    public override void OnLateUpdate()
    {
        try { Hand.LateTick(); } catch (Exception e) { Safe.Log("Hand", e); }
        NextMatch.Tick();
    }

    public override void OnUpdate()
    {
        try { Journal.Tick(); } catch (Exception e) { Safe.Log("Journal tick", e); }
        if (!Settings.TestKeys.Value) return;
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F8))
        {
            // test key (off by default): one box of every label into the pack
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                Collection.Quiet = true;
                foreach (var d in Defs.All) pm?.InstantiateItemInPlayerInventory(Defs.Prefix + d.Id, 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
            }
            catch (Exception e) { Safe.Log("Test give", e); }
            finally { Collection.Quiet = false; }
            Collection.Say("MSG_COUNT", Collection.Found, Defs.All.Count);
        }
    }
}

/// <summary>Errors go to the log once per place.</summary>
internal static class Safe
{
    static readonly HashSet<string> seen = new();
    public static void Log(string where, Exception e) { if (seen.Add(where)) MelonLogger.Error($"{where}: {e}"); }
    public static void Info(string msg) => MelonLogger.Msg(msg);
    public static void Debug(string msg) { if (Settings.DebugLog.Value) MelonLogger.Msg(msg); }
}

public static class Settings
{
    static MelonPreferences_Category? cat;
    public static MelonPreferences_Entry<bool> TestKeys = null!, DebugLog = null!;
    public static MelonPreferences_Entry<float> FlameRaise = null!;
    public static MelonPreferences_Entry<float> LootChance = null!, ReplaceVanilla = null!;

    public static void Create()
    {
        cat = MelonPreferences.CreateCategory("MatchboxesByEMPEROR");
        TestKeys = cat.CreateEntry("TestKeys", false, description: "Ctrl+Shift+F8 gives one box of every label (testing only)");
        DebugLog = cat.CreateEntry("DebugLog", false);
        FlameRaise = cat.CreateEntry("FlameRaise", 2f, description: "mm the flame is lifted above the burning line of the stick (negative: lower)");
        LootChance = cat.CreateEntry("LootChance", 3f, description: "% of containers searched for the first time that get a matchbox");
        ReplaceVanilla = cat.CreateEntry("ReplaceVanilla", 40f, description: "% of the game's own matches in containers that become a labelled box");
    }
}

/// <summary>The dev console's «add» knows our names too.</summary>
[HarmonyPatch(typeof(ConsoleManager), "StoreAutoCompleteGearNames")]
internal static class ConsoleNames
{
    static void Postfix() => Fill();

    public static void Fill()
    {
        try
        {
            var d = ConsoleManager.m_SearchStringToGearNames;
            if (d == null) return;
            foreach (var def in Defs.All)
            {
                string full = Defs.Prefix + def.Id;
                foreach (var k in new[] { full.ToLowerInvariant(), full.Substring(5).ToLowerInvariant() })
                    if (!d.ContainsKey(k)) d[k] = full;
            }
        }
        catch (Exception e) { Safe.Log("Console names", e); }
    }
}
