[assembly: MelonInfo(typeof(TeaCoffeeByEMPEROR.Mod), "TeaCoffeeByEMPEROR", TeaCoffeeByEMPEROR.Mod.VersionText, "EMPEROR")]
[assembly: MelonGame("Hinterland", "TheLongDark")]
[assembly: MelonAdditionalDependencies("AfflictionComponent")]

namespace TeaCoffeeByEMPEROR;

/// <summary>Tea and coffee (11-14, asked by the owner): about 250 products (real kinds, made-up makers; pressed teas a line of their own) with their own packs, found in houses and sold by the trader, brewed like
/// the game's own tea (the pack goes into a pot of water over a fire) into a glass of tea in a holder, a tea bowl or a mug with its own effects.
/// Effects are the game's own buffs (warmth, alertness, rest, condition over time), scaled per product. Coop: ordinary items, nothing shared.</summary>
public class Mod : MelonMod
{
    public const string VersionText = "0.5.9";

    public static string Root => Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "TeaCoffeeByEMPEROR");

    static MelonPreferences_Entry<bool> debugLog = null!, testKeys = null!;
    public static bool DebugLog => debugLog.Value;

    public override void OnInitializeMelon()
    {
        Unpack();
        ContainerOpenPatch.Apply(HarmonyInstance);
        var cat = MelonPreferences.CreateCategory("TeaCoffeeByEMPEROR");
        debugLog = cat.CreateEntry("DebugLog", true, "Подробный лог / Detailed log");
        testKeys = cat.CreateEntry("TestKeys", false, "Ctrl+Shift+F8: все пачки и банки в рюкзак, Ctrl+Shift+F9: все готовые напитки / every pack and can, F9: every brewed cup");
        LoggerInstance.Msg($"TeaCoffeeByEMPEROR {VersionText}: {Catalog.All.Length} products, {Defs.All.Count} items, {Texts.Count} text keys.");
    }

    /// <summary>The models, textures and icons come packed, Mods/TeaCoffeeByEMPEROR*.pak (zips): unpacked once into the mod's folder, then removed.</summary>
    void Unpack()
    {
        foreach (var pak in Directory.GetFiles(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "TeaCoffeeByEMPEROR*.pak"))
        {
            try
            {
                Directory.CreateDirectory(Root);
                System.IO.Compression.ZipFile.ExtractToDirectory(pak, Root, true);
                File.Delete(pak);
                LoggerInstance.Msg(Path.GetFileName(pak) + " unpacked into " + Root);
            }
            catch (Exception e) { LoggerInstance.Error("Unpacking " + pak + ": " + e); }
        }
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName) => ConsoleNames.Fill();

    public override void OnUpdate()
    {
        try { Effects.Tick(); } catch (Exception e) { Safe.Log("Effects tick", e); }
        EffectRow.Hover();
        if (!testKeys.Value || !Input.GetKey(KeyCode.LeftControl) || !Input.GetKey(KeyCode.LeftShift)) return;
        if (Input.GetKeyDown(KeyCode.F8))
        {
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                foreach (var d in Defs.All)
                    if (d.Kind != Kind.Cup) pm.InstantiateItemInPlayerInventory(Defs.Prefix + d.Id, 0, 1f, PlayerManager.InventoryInstantiateFlags.None);
                Safe.Info("Test items given");
            }
            catch (Exception e) { Safe.Log("Test give", e); }
        }
        if (Input.GetKeyDown(KeyCode.F9))
        {
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                foreach (var d in Defs.All)
                    if (d.Kind == Kind.Cup) pm.InstantiateItemInPlayerInventory(Defs.Prefix + d.Id, 0, 1f, PlayerManager.InventoryInstantiateFlags.None);
                Safe.Info("Test cups given");
            }
            catch (Exception e) { Safe.Log("Test cups", e); }
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

    public static void Once(string key, string msg)
    {
        if (seen.Add("msg:" + key)) MelonLogger.Msg(msg);
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
