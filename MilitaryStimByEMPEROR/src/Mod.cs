using System;
using System.IO;
using MelonLoader;

[assembly: MelonInfo(typeof(MilitaryStimByEMPEROR.StimMod), "MilitaryStimByEMPEROR", MilitaryStimByEMPEROR.StimMod.Version, "EMPEROR")]
[assembly: MelonGame("Hinterland", "TheLongDark")]
[assembly: HarmonyDontPatchAll]

namespace MilitaryStimByEMPEROR
{
    /// <summary>
    /// WINTERMUTE (episode 5) military stim in survival mode, as in the story: +50 condition, −75 % fatigue, no collapse after.
    /// The item, its first-person hands, item data and icon come from our own bundle (Mods/MilitaryStimByEMPEROR/militarystimbyemperor.bundle),
    /// built offline from the episode 5 files; the mesh, animations, sounds and scripts are the survival emergency stim's.
    /// </summary>
    public class StimMod : MelonMod
    {
        public const string Version = "0.1.6";
        internal static StimMod Instance;
        internal static string Folder;
        bool _setUp;

        internal static MelonPreferences_Entry<float> LootChance;
        internal static MelonPreferences_Entry<float> WorldChance;
        internal static MelonPreferences_Entry<float> MedicalChance;
        internal static MelonPreferences_Entry<bool> DebugLog;

        public override void OnInitializeMelon()
        {
            // the mod was renamed from its old name: move the player's files over once
            Legacy.Move(LoggerInstance, "TLDMilitaryStim", "MilitaryStimByEMPEROR", "tldmilitarystim.bundle", "militarystimbyemperor.bundle");
            Instance = this;
            Folder = Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "MilitaryStimByEMPEROR");
            var cat = MelonPreferences.CreateCategory("TLDMilitaryStim");
            LootChance = cat.CreateEntry("ContainerChancePercent", 5f, "Шанс, что шприц первой помощи в тайнике окажется военным стимулятором, % / chance an emergency stim in a container is a military stim, %");
            WorldChance = cat.CreateEntry("WorldChancePercent", 5f, "Шанс, что шприц первой помощи, лежащий на локации, окажется военным стимулятором, % / chance an emergency stim lying in the world is a military stim, %");
            MedicalChance = cat.CreateEntry("MedicalContainerChancePercent", 3f, "Отдельный шанс найти военный стимулятор в любой аптечке или аптечном шкафчике, % / extra chance for any first-aid kit or medicine cabinet, %");
            DebugLog = cat.CreateEntry("DebugLog", true, "Подробный лог / verbose log");
        }

        public override void OnUpdate() { AssetsHint.Tick(); }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (_setUp) return;
            if (sceneName == null || !sceneName.StartsWith("MainMenu", StringComparison.OrdinalIgnoreCase)) return;
            _setUp = true;
            try
            {
                Texts.Patch(HarmonyInstance);
                ConsoleNames.Patch(HarmonyInstance);
                Loot.Patch(HarmonyInstance);
                Assets.Register();
                LoggerInstance.Msg($"MilitaryStimByEMPEROR {Version}: готово / ready");
            }
            catch (Exception e)
            {
                LoggerInstance.Error("setup failed: " + e);
            }
        }

        internal static void Log(string s) { if (DebugLog.Value) Instance.LoggerInstance.Msg(s); }
        internal static void Warn(string s) => Instance.LoggerInstance.Warning(s);
        internal static void Error(string s) => Instance.LoggerInstance.Error(s);
    }
}
