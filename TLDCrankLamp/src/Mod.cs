using System;
using System.IO;
using MelonLoader;

[assembly: MelonInfo(typeof(TLDCrankLamp.LampMod), "TLDCrankLamp", TLDCrankLamp.LampMod.Version, "Seagull")]
[assembly: MelonGame("Hinterland", "TheLongDark")]
[assembly: HarmonyDontPatchAll]

namespace TLDCrankLamp
{
    /// <summary>
    /// WINTERMUTE (episode 5) hand-crank lamp in survival mode. The model, first-person hands, item data and icon come
    /// from our bundle (Mods/TLDCrankLamp/tldcranklamp.bundle) built from the episode 5 files. Episode 5's crank code
    /// does not exist in survival, so the lamp runs on the survival flashlight and the crank is done here (Crank.cs).
    /// </summary>
    public class LampMod : MelonMod
    {
        public const string Version = "0.4.1";
        internal static LampMod Instance;
        internal static string Folder;
        bool _setUp;

        internal static MelonPreferences_Entry<float> CrankSecondsToFull;
        internal static MelonPreferences_Entry<float> FullChargeGameHours;
        internal static MelonPreferences_Entry<float> WearPerLightHour, WearPerCrankMinute;
        internal static MelonPreferences_Entry<float> LightIntensity;
        internal static MelonPreferences_Entry<float> HandleOffsetDegrees;
        internal static MelonPreferences_Entry<bool> DebugLog;

        public override void OnInitializeMelon()
        {
            Instance = this;
            Folder = Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "TLDCrankLamp");
            var cat = MelonPreferences.CreateCategory("TLDCrankLamp");
            CrankSecondsToFull = cat.CreateEntry("CrankSecondsToFull2", 45f, "Сколько секунд крутить ручку с нуля до полного заряда / seconds of cranking from empty to full");
            HandleOffsetDegrees = cat.CreateEntry("HandleOffsetDegrees", 0f, "Поворот ручки относительно руки, градусы (если ручку крутит код) / handle angle offset against the hand, degrees");
            LightIntensity = cat.CreateEntry("LightIntensity", 3f, "Яркость света фонаря (в WINTERMUTE 1) / light intensity (1 in WINTERMUTE)");
            FullChargeGameHours = cat.CreateEntry("FullChargeGameHours", 1f, "Сколько игровых часов светит полный заряд / game hours of light on a full charge");
            WearPerLightHour = cat.CreateEntry("WearPerLightHour", 1f, "Сколько процентов состояния теряет фонарь за игровой час горения / condition percent lost per game hour of light");
            WearPerCrankMinute = cat.CreateEntry("WearPerCrankMinute", 2f, "Сколько процентов состояния теряет фонарь за минуту кручения ручки / condition percent lost per minute of cranking");
            DebugLog = cat.CreateEntry("DebugLog", true, "Подробный лог / verbose log");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (_setUp) return;
            if (sceneName == null || !sceneName.StartsWith("MainMenu", StringComparison.OrdinalIgnoreCase)) return;
            _setUp = true;
            try
            {
                Texts.Patch(HarmonyInstance);
                ConsoleNames.Patch(HarmonyInstance);
                Crank.Patch(HarmonyInstance);
                Assets.Register();
                Sound.Load();
                LoggerInstance.Msg($"TLDCrankLamp {Version}: готово / ready");
            }
            catch (Exception e)
            {
                LoggerInstance.Error("setup failed: " + e);
            }
        }

        public override void OnUpdate() { Crank.Tick(); Sound.Tick(); Place.Update(); AssetsHint.Tick(); }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (!_setUp || sceneName == null || sceneName.StartsWith("MainMenu", StringComparison.OrdinalIgnoreCase)) return;
            try { Sound.Load(); } catch { }
            try { Place.OnScene(sceneName); } catch (Exception e) { Warn("place: " + e.Message); }
        }
        public override void OnLateUpdate() { Crank.LateTick(); CoopApi.LateTick(); }

        internal static void Log(string s) { if (DebugLog.Value) Instance.LoggerInstance.Msg(s); }
        internal static void Warn(string s) => Instance.LoggerInstance.Warning(s);
        internal static void Error(string s) => Instance.LoggerInstance.Error(s);
    }
}
