using System;
using System.IO;
using MelonLoader;

[assembly: MelonInfo(typeof(ShotgunByEMPEROR.ShotgunMod), "ShotgunByEMPEROR", ShotgunByEMPEROR.ShotgunMod.Version, "EMPEROR")]
[assembly: MelonGame("Hinterland", "TheLongDark")]
[assembly: HarmonyDontPatchAll]

namespace ShotgunByEMPEROR
{
    /// <summary>
    /// WINTERMUTE (episode 5) shotgun in survival mode.
    /// The gun, its hands, ammo box, shell and casing come from our own bundle (Mods/ShotgunByEMPEROR/shotgunbyemperor.bundle),
    /// built offline from the episode 5 files. Everything else it needs is already in the survival bundles.
    /// </summary>
    public class ShotgunMod : MelonMod
    {
        public const string Version = "0.1.35";
        internal static ShotgunMod Instance;
        internal static string Folder;
        bool _setUp;

        // settings (UserData/MelonPreferences.cfg, section ShotgunByEMPEROR)
        internal static MelonPreferences_Entry<int> Pellets;
        internal static MelonPreferences_Entry<float> SpreadDeg;
        internal static MelonPreferences_Entry<float> DamageMultiplier;
        internal static MelonPreferences_Entry<float> FullDamageMeters;
        internal static MelonPreferences_Entry<float> MinDamageMeters;
        internal static MelonPreferences_Entry<float> MinDamageFactor;
        internal static MelonPreferences_Entry<int> KillChancePercent;
        internal static MelonPreferences_Entry<bool> DebugLog;
        internal static MelonPreferences_Entry<float> ShellLootChance;
        internal static MelonPreferences_Entry<float> WearPercent;

        public override void OnInitializeMelon()
        {
            // the mod was renamed from its old name: move the player's files over once
            Legacy.Move(LoggerInstance, "TLDShotgun", "ShotgunByEMPEROR", "tldshotgun.bundle", "shotgunbyemperor.bundle", "places.txt", "", "spots.txt", "");
            Instance = this;
            Folder = Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "ShotgunByEMPEROR");
            var cat = MelonPreferences.CreateCategory("TLDShotgun");
            Pellets = cat.CreateEntry("Pellets", 8, "Дробин за выстрел / pellets per shot");
            SpreadDeg = cat.CreateEntry("SpreadDegrees", 2.5f, "Разлёт дроби, градусы / pellet spread, degrees");
            DamageMultiplier = cat.CreateEntry("DamageMultiplier", 1.25f, "Сумма урона всех дробин вблизи = урон ружья × это / total close-range damage = gun damage × this");
            FullDamageMeters = cat.CreateEntry("FullDamageMeters", 10f, "До этой дистанции полный урон / full damage up to this distance");
            MinDamageMeters = cat.CreateEntry("MinDamageMeters", 35f, "С этой дистанции минимальный урон / minimal damage from this distance");
            MinDamageFactor = cat.CreateEntry("MinDamageFactor", 0.15f, "Минимальная доля урона / minimal damage share");
            KillChancePercent = cat.CreateEntry("PelletInstantKillChancePercent", 20, "Шанс, что попадание дробины в убойное место убьёт сразу (от игрового шанса), % / share of the game's instant-kill roll kept per pellet, %");
            ShellLootChance = cat.CreateEntry("ShellLootChancePercent", 35f, "Шанс, что патроны для винтовки в тайнике окажутся патронами для дробовика, % / chance rifle ammo loot becomes shotgun shells, %");
            WearPercent = cat.CreateEntry("WearPercentPerShot", 1.5f, "Износ дробовика за выстрел, % от прочности / wear per shot, % of condition");
            DebugLog = cat.CreateEntry("DebugLog", false, "Подробный лог / verbose log");
        }

        static string _lastScene;

        public override void OnUpdate() { AssetsHint.Tick(); }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (_setUp)
            {
                try { if (_lastScene != null && (Loot.AmmoRolls + Loot.LooseAmmo) > 0) Loot.Report(_lastScene); } catch { }
                _lastScene = sceneName;
                try { FixedSpawns.OnScene(sceneName); } catch { }
                return;
            }
            if (sceneName == null || !sceneName.StartsWith("MainMenu", StringComparison.OrdinalIgnoreCase)) return;
            _setUp = true;
            try
            {
                Texts.Patch(HarmonyInstance);
                Pellets_.Patch(HarmonyInstance);
                ConsoleNames.Patch(HarmonyInstance);
                KeepEnabled.Patch(HarmonyInstance);
                ShotgunSkill.Patch(HarmonyInstance);
                Crafting.Patch(HarmonyInstance);
                SkillImage.Patch(HarmonyInstance);
                Loot.Patch(HarmonyInstance);
                Wear.Patch(HarmonyInstance);
                Reload.Patch(HarmonyInstance);
                Assets.Register();
                Sound.LoadBank();
                LoggerInstance.Msg($"ShotgunByEMPEROR {Version}: готово / ready");
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
