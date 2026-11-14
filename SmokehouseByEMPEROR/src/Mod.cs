[assembly: MelonInfo(typeof(SmokehouseByEMPEROR.Mod), "SmokehouseByEMPEROR", SmokehouseByEMPEROR.Mod.VersionText, "EMPEROR")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace SmokehouseByEMPEROR;

/// <summary>A barrel smokehouse made at the workbench: hot and cold smoked meat and fish that keep long and are eaten with no fire.
/// Since 0.3.0 also a canning kettle (cans, boiled sausage) and a sausage table (raw sausage, dry-cured in the barrel).</summary>
public class Mod : MelonMod
{
    public const string VersionText = "0.4.27";

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
        Crafting.Tick();
        try { Barrels.Tick(); } catch (Exception e) { Safe.Log("Barrels tick", e); }
        try { Fx.Frame(); } catch (Exception e) { Safe.Log("Smoke", e); }
        if (testKeys.Value && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F9)) { try { Fx.NextBoilLevel(); } catch (Exception e) { Safe.Log("Boil test", e); } }
        if (testKeys.Value && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F8))
        {
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                foreach (var st in new[] { Defs.Barrel, Defs.Kettle, Defs.Table })
                    pm.InstantiateItemInPlayerInventory(Defs.Prefix + st, 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
                // 11-12 owner: one press gives everything there is to check
                foreach (var own in new[] { "CanDeer", "CanMoose", "CanBear", "CanFish", "CanPate", "CanWolf", "CanCougar", "CanSalmon", "CanSea", "CanHunter", "CanStew", "CanBeast", "CanAngler", "CanGreat", "CanFishMix", "SausHuntRaw", "SausTaigaRaw", "SausFishRaw", "SausGreatBearRaw", "SausHuntHot", "SausHuntDry", "SausCohoHot", "HotDeer", "ColdDeer_S", "HotCoho", "ColdBass" })
                {
                    try { pm.InstantiateItemInPlayerInventory(Defs.Prefix + own, 1, 1f, PlayerManager.InventoryInstantiateFlags.None); } catch (Exception e) { Safe.Log("Test give " + own, e); }
                }
                foreach (var (n, count) in new[] { ("GEAR_RecycledCan", 6), ("GEAR_Gut", 6), ("GEAR_Salt", 6), ("GEAR_Softwood", 4), ("GEAR_Hardwood", 10), ("GEAR_PackMatches", 1), ("GEAR_Hacksaw", 1),
                    ("GEAR_RawMeatDeer", 3), ("GEAR_RawMeatMoose", 2), ("GEAR_RawMeatBear", 2), ("GEAR_RawMeatWolf", 2), ("GEAR_RawMeatRabbit", 2), ("GEAR_RawMeatPtarmigan", 2),
                    ("GEAR_RawCohoSalmon", 2), ("GEAR_RawRainbowTrout", 2), ("GEAR_RawSmallMouthBass", 1), ("GEAR_WaterSupplyPotable", 1) })
                    for (int i = 0; i < count; i++)
                    {
                        try { pm.InstantiateItemInPlayerInventory(n, 1, 1f, PlayerManager.InventoryInstantiateFlags.None); } catch (Exception e) { Safe.Log("Test give " + n, e); }
                    }
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
