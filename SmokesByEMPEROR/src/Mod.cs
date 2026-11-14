using System.IO;
[assembly: MelonInfo(typeof(SmokesByEMPEROR.Mod), "SmokesByEMPEROR", SmokesByEMPEROR.Mod.VersionText, "EMPEROR")]
[assembly: MelonGame("Hinterland", "TheLongDark")]
[assembly: MelonAdditionalDependencies("AfflictionComponent")]

namespace SmokesByEMPEROR;

/// <summary>Cigarettes, cigars and pipes: smoked on the move, held like a torch, with drags, exhaled smoke and the effects the owner chose.</summary>
public class Mod : MelonMod
{
    public const string VersionText = "0.1.148";

    public static string Root => Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "SmokesByEMPEROR");

    public override void OnInitializeMelon()
    {
        // the mod was renamed from its old name: move the player's files over once
        Legacy.Move(LoggerInstance, "SmokesBySeagull", "SmokesByEMPEROR");
        if (File.Exists(Path.Combine(MelonLoader.Utils.MelonEnvironment.PluginsDirectory, "SmokesAudioOnByEMPEROR.dll"))) Legacy.PutAside(LoggerInstance, Path.Combine(MelonLoader.Utils.MelonEnvironment.PluginsDirectory, "SmokesAudioOn.dll"));
        Settings.Create();
        Texts.Load(Root);
        ContainerOpenPatch.Apply(HarmonyInstance);
        // Synth.Warm(): cut sounds are off (0.1.62), so no helper process is started with the game
        LoggerInstance.Msg($"SmokesByEMPEROR {VersionText}: {Defs.All.Count} items, {Texts.Count} text keys.");
    }

    public override void OnGUI()
    {
        Journal.Draw();
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        ConsoleNames.Fill();
        try { PipeSpots.OnScene(sceneName); } catch (Exception e) { Safe.Log("Pipe spots", e); }
    }

    public override void OnUpdate()
    {
        try { Journal.Tick(); } catch (Exception e) { Safe.Log("Journal tick", e); }
        try { InspectIcon.Tick(); } catch (Exception e) { Safe.Log("Inspect icon", e); }
        try { Smoking.HealHand(); } catch (Exception e) { Safe.Log("Broken hand", e); }
        try
        {
            Smoking.Tick();
            Visual.TickPuffs();
        }
        catch (Exception e)
        {
            Safe.Log("Smoking tick", e);
        }
        try
        {
            Habit.Tick();
            Boxes.Tick();
        }
        catch (Exception e)
        {
            Safe.Log("Habit tick", e);
        }
        if (Settings.GiveLightersKey.Value && Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.L))
        {
            // 11-10 user: a key to give himself the new items (lighters and their refills)
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                int n = 0;
                Lighters.QuietGive = true;
                if (pm != null)
                    foreach (var d in Defs.All)
                        if ((d.Kind == Kind.Lighter || d.Kind == Kind.Refill) && pm.InstantiateItemInPlayerInventory(Defs.Prefix + d.Id, 1, 1f, PlayerManager.InventoryInstantiateFlags.None) != null) n++;
                Lighters.QuietGive = false;
                Safe.Info($"Give lighters key: {n} items");
                Use.Say("SMOKES_MSG_NEW_LIGHTER", Lighters.Found, Lighters.Total);
            }
            catch (Exception e) { Lighters.QuietGive = false; Safe.Log("Give lighters", e); }
        }
        if (!Settings.TestKeys.Value) return;
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F10))
        {
            try { Sounds.NextCandidate(); } catch (Exception e) { Safe.Log("Cut sound test", e); }
        }
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F9))
        {
            try
            {
                foreach (var d in Defs.All)
                    if (d.Kind != Kind.Lit) GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory(Defs.Prefix + d.Id, 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
            }
            catch (Exception e)
            {
                Safe.Log("Test give", e);
            }
        }
    }

    public override void OnLateUpdate()
    {
        try
        {
            Smoking.LateTick();
        }
        catch (Exception e)
        {
            Safe.Log("Smoking late tick", e);
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
        if (Settings.DebugLog.Value) MelonLogger.Msg(msg);
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
                if (def.Kind == Kind.Lit) continue;
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
