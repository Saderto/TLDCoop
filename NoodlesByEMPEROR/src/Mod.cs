[assembly: MelonInfo(typeof(NoodlesByEMPEROR.Mod), "NoodlesByEMPEROR", NoodlesByEMPEROR.Mod.VersionText, "EMPEROR")]
[assembly: MelonAdditionalDependencies("AfflictionComponent")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace NoodlesByEMPEROR;

/// <summary>Instant noodles: blocks, cups, a tray and udon, brewed in a pot with drinking water like tea, eaten hot for warmth.
/// Split out of SnacksByEMPEROR 0.1.0 (own prefix GEAR_NDL_). They turn up in kitchen cupboards, fridges and other containers, and the Trader sells two each aurora.</summary>
public class Mod : MelonMod
{
    public const string VersionText = "0.6.9";

    public static string Root => Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "NoodlesByEMPEROR");

    static MelonPreferences_Entry<bool> debugLog = null!, testKeys = null!;
    static MelonPreferences_Entry<float> lootChance = null!, kitchenChance = null!;
    public static bool DebugLog => debugLog.Value;
    public static float LootChance => lootChance.Value;
    public static float KitchenChance => kitchenChance.Value;

    public override void OnInitializeMelon()
    {
        var cat = MelonPreferences.CreateCategory("NoodlesByEMPEROR");
        debugLog = cat.CreateEntry("DebugLog", false, "Подробный лог / Detailed log");
        testKeys = cat.CreateEntry("TestKeys", false, "Ctrl+Shift+F8: по одной пачке каждого вида, котелок и вода в рюкзак / one of every kind, a pot and water into the pack");
        lootChance = cat.CreateEntry("LootChance", 1.5f, "% обычных контейнеров с находкой / % of ordinary containers with a find");
        kitchenChance = cat.CreateEntry("KitchenChance", 5f, "% кухонных шкафов, холодильников, ящиков с едой / % of kitchen cupboards, fridges, food boxes");
        ContainerOpenPatch.Apply(HarmonyInstance);
        LoggerInstance.Msg($"NoodlesByEMPEROR {VersionText}: {Defs.All.Length} items, {Texts.Count} text keys.");
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName) => ConsoleNames.Fill();

    public override void OnUpdate()
    {
        try { Items.Tick(); } catch (Exception e) { Safe.Log("Items tick", e); }
        try { Effects.Tick(); } catch (Exception e) { Safe.Log("Effects tick", e); }
        EffectIcons.Hover();
        if (testKeys.Value && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F8))
        {
            var pm = GameManager.GetPlayerManagerComponent();
            foreach (var d in Defs.Sellable)
            {
                try { pm.InstantiateItemInPlayerInventory(d.Gear, 1, 1f, PlayerManager.InventoryInstantiateFlags.None); }
                catch (Exception e) { Safe.Log("Test give " + d.Id, e); }
            }
            foreach (var n in new[] { "GEAR_CookingPot", "GEAR_WaterSupplyPotable", "GEAR_WaterSupplyPotable", "GEAR_WaterSupplyPotable" })
            {
                try { pm.InstantiateItemInPlayerInventory(n, 1, 1f, PlayerManager.InventoryInstantiateFlags.None); }
                catch (Exception e) { Safe.Log("Test give " + n, e); }
            }
            Safe.Info("Test items given");
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
                string full = def.Gear;
                foreach (var k in new[] { full.ToLowerInvariant(), full.Substring(5).ToLowerInvariant() })
                    if (!d.ContainsKey(k)) d[k] = full;
            }
        }
        catch (Exception e) { Safe.Log("Console names", e); }
    }
}
