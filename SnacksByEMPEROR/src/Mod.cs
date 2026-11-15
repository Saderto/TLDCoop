[assembly: MelonInfo(typeof(SnacksByEMPEROR.Mod), "SnacksByEMPEROR", SnacksByEMPEROR.Mod.VersionText, "EMPEROR")]
[assembly: MelonGame("Hinterland", "TheLongDark")]
[assembly: MelonAdditionalDependencies("AfflictionComponent")]

namespace SnacksByEMPEROR;

/// <summary>Snacks: crisps, croutons, seeds, nuts, sea snacks, popcorn, corn sticks. World brands and local ones by country, several flavours each.
/// They turn up in kitchen cupboards, fridges and other containers, and the Trader sells a couple each aurora.</summary>
public class Mod : MelonMod
{
    public const string VersionText = "0.7.5";

    public static string Root => Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "SnacksByEMPEROR");

    static MelonPreferences_Entry<bool> debugLog = null!, testKeys = null!;
    static MelonPreferences_Entry<float> lootChance = null!, kitchenChance = null!;
    public static bool DebugLog => debugLog.Value;
    public static float LootChance => lootChance.Value;
    public static float KitchenChance => kitchenChance.Value;

    public override void OnInitializeMelon()
    {
        var cat = MelonPreferences.CreateCategory("SnacksByEMPEROR");
        debugLog = cat.CreateEntry("DebugLog", false, "Подробный лог / Detailed log");
        testKeys = cat.CreateEntry("TestKeys", false, "Ctrl+Shift+F8: все снеки в рюкзак / every item into the pack");
        lootChance = cat.CreateEntry("LootChance", 2f, "% обычных контейнеров с находкой / % of ordinary containers with a find");
        kitchenChance = cat.CreateEntry("KitchenChance", 8f, "% кухонных шкафов, холодильников, ящиков с едой / % of kitchen cupboards, fridges, food boxes");
        Effects.Prepare();
        ContainerOpenPatch.Apply(HarmonyInstance);
        LoggerInstance.Msg($"SnacksByEMPEROR {VersionText}: {Defs.All.Length} items, {Texts.Count} text keys.");
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName) => ConsoleNames.Fill();

    public override void OnUpdate()
    {
        try { Effects.Tick(); } catch (Exception e) { Safe.Log("Effects tick", e); }
        EffectIcons.Hover();
        if (testKeys.Value && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F8))
        {
            var pm = GameManager.GetPlayerManagerComponent();
            foreach (var d in Defs.All)
            {
                try { pm.InstantiateItemInPlayerInventory(d.Gear, 1, 1f, PlayerManager.InventoryInstantiateFlags.None); }
                catch (Exception e) { Safe.Log("Test give " + d.Id, e); }
            }
            foreach (var n in Array.Empty<string>())
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
