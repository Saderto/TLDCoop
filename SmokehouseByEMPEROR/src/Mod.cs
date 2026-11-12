[assembly: MelonInfo(typeof(SmokehouseByEMPEROR.Mod), "SmokehouseByEMPEROR", SmokehouseByEMPEROR.Mod.VersionText, "EMPEROR")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace SmokehouseByEMPEROR;

/// <summary>A barrel smokehouse made at the workbench: hot and cold smoked meat and fish that keep long and are eaten with no fire.</summary>
public class Mod : MelonMod
{
    public const string VersionText = "0.2.7";

    public static string Root => Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "SmokehouseByEMPEROR");

    static MelonPreferences_Entry<bool> debugLog = null!, testKeys = null!;
    public static bool DebugLog => debugLog.Value;

    public override void OnInitializeMelon()
    {
        // the mod was renamed from its old name: move the player's files over once
        Legacy.Move(LoggerInstance, "SmokehouseBySeagull", "SmokehouseByEMPEROR");
        var cat = MelonPreferences.CreateCategory("SmokehouseBySeagull");
        debugLog = cat.CreateEntry("DebugLog", true, "Подробный лог / Detailed log");
        testKeys = cat.CreateEntry("TestKeys", false, "Ctrl+Shift+F8: бочка, дрова, сырое мясо и рыба в рюкзак / test items into the pack");
        LoggerInstance.Msg($"SmokehouseByEMPEROR {VersionText}: {Defs.All.Count} items, {Texts.Count} text keys.");
    }

    public override void OnGUI()
    {
        try { Window.Draw(); } catch (Exception e) { Safe.Log("Window", e); }
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        ConsoleNames.Fill();
        try { Fx.Clear(); } catch (Exception e) { Safe.Log("Scene", e); }
    }

    public override void OnUpdate()
    {
        try { Window.Update(); } catch (Exception e) { Safe.Log("Window update", e); }
        try { Place.Tick(); } catch (Exception e) { Safe.Log("Place tick", e); }
        try { Items.Tick(); } catch (Exception e) { Safe.Log("Items tick", e); }
        try { Barrels.Tick(); } catch (Exception e) { Safe.Log("Barrels tick", e); }
        try { Fx.Frame(); } catch (Exception e) { Safe.Log("Smoke", e); }
        if (testKeys.Value && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F8))
        {
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                pm.InstantiateItemInPlayerInventory(Defs.Prefix + Defs.Barrel, 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
                foreach (var n in new[] { "GEAR_Softwood", "GEAR_Softwood", "GEAR_Softwood", "GEAR_Hardwood", "GEAR_Hardwood", "GEAR_RawMeatDeer", "GEAR_RawMeatWolf", "GEAR_RawCohoSalmon", "GEAR_RawRainbowTrout", "GEAR_Salt", "GEAR_PackMatches" })
                    pm.InstantiateItemInPlayerInventory(n, 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
                Safe.Info("Test items given");
            }
            catch (Exception e) { Safe.Log("Test give", e); }
        }
    }
}

/// <summary>Errors go to the log once per place: a failing hook mustn't flood the log every frame.</summary>
internal static class Safe
{
    static readonly HashSet<string> seen = new();

    public static void Log(string where, Exception e)
    {
        if (seen.Add(where)) MelonLogger.Error($"{where}: {e}");
    }

    public static void Info(string msg) => MelonLogger.Msg(msg);

    public static void Debug(string msg)
    {
        if (Mod.DebugLog) MelonLogger.Msg(msg);
    }
}

/// <summary>The dev console's «add» looks gear up in a list the game builds once from its own catalogue: ours are added to it.</summary>
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
        catch (Exception e)
        {
            Safe.Log("Console names", e);
        }
    }
}
