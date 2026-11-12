[assembly: MelonInfo(typeof(JellyfishByEMPEROR.Mod), "JellyfishByEMPEROR", JellyfishByEMPEROR.Mod.VersionText, "EMPEROR")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace JellyfishByEMPEROR;

/// <summary>Jellyfish of the northern sea (11-12, asked by a player): one may rise in a hole just cut in the sea ice and stay 3 to 7 seconds.
/// Scooped up with a glass jar it lives in it as decoration that swims and glows; below −5° it dies in 90 minutes. Up to three are poured into
/// an aquarium to swim free, and caught back out with a jar. Decoration only (the owner, 11-12). Six species, 20 colours. Coop: CoopApi.</summary>
public class Mod : MelonMod
{
    public const string VersionText = "0.2.9";

    public static string Root => Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "JellyfishByEMPEROR");

    static MelonPreferences_Entry<bool> debugLog = null!, testKeys = null!;
    public static bool DebugLog => debugLog.Value;

    public override void OnInitializeMelon()
    {
        var cat = MelonPreferences.CreateCategory("JellyfishByEMPEROR");
        debugLog = cat.CreateEntry("DebugLog", true, "Подробный лог / Detailed log");
        testKeys = cat.CreateEntry("TestKeys", false, "Ctrl+Shift+F8: банки и все медузы в рюкзак, Ctrl+Shift+F9: медуза перед тобой / test items, a jellyfish in front of you");
        Catch.Prefs(cat);
        LoggerInstance.Msg($"JellyfishByEMPEROR {VersionText}: {Defs.All.Count} items, {Texts.Count} text keys.");
    }

    public override void OnGUI()
    {
        try { Catch.Draw(); } catch (Exception e) { Safe.Log("Catch window", e); }
        try { TankWindow.Draw(); } catch (Exception e) { Safe.Log("Aquarium window", e); }
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName) => ConsoleNames.Fill();

    public override void OnUpdate()
    {
        Ui.Capture();
        try { Remote.Tick(); } catch (Exception e) { Safe.Log("Coop requests", e); }
        try { Catch.Update(); } catch (Exception e) { Safe.Log("Catch", e); }
        try { TankWindow.Update(); } catch (Exception e) { Safe.Log("Aquarium window", e); }
        try { Place.Tick(); } catch (Exception e) { Safe.Log("Place tick", e); }
        try { Items.Tick(); } catch (Exception e) { Safe.Log("Items tick", e); }
        try { Visual.Frame(); } catch (Exception e) { Safe.Log("Visual", e); }
        try { Life.Tick(); } catch (Exception e) { Safe.Log("Life tick", e); }
        if (!testKeys.Value || !Input.GetKey(KeyCode.LeftControl) || !Input.GetKey(KeyCode.LeftShift)) return;
        if (Input.GetKeyDown(KeyCode.F8))
        {
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                foreach (var d in Defs.All)
                    pm.InstantiateItemInPlayerInventory(Defs.Prefix + d.Id, d.Kind == Kind.Jar ? 3 : 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
                Safe.Info("Test items given");
            }
            catch (Exception e) { Safe.Log("Test give", e); }
        }
        if (Input.GetKeyDown(KeyCode.F9))
        {
            try
            {
                var t = GameManager.GetPlayerTransform();
                Catch.Start(t.position + t.forward * 1.2f);
            }
            catch (Exception e) { Safe.Log("Test jellyfish", e); }
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
