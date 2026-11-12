using System;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(FeatsByEMPEROR.FeatsMod), "FeatsByEMPEROR", FeatsByEMPEROR.FeatsMod.Version, "EMPEROR")]
[assembly: MelonGame("Hinterland", "TheLongDark")]
[assembly: HarmonyDontPatchAll]

namespace FeatsByEMPEROR
{
    /// <summary>
    /// 49 new feats that work like the game's own: progress builds up over all games of the profile, an unlocked feat is chosen
    /// when starting a new survival game (same window, same limit per difficulty) and then gives its bonus for the whole game.
    /// </summary>
    public class FeatsMod : MelonMod
    {
        public const string Version = "0.2.2";
        internal static FeatsMod Instance;
        internal static string Folder;
        bool _setUp;

        internal static MelonPreferences_Entry<bool> DebugLog;
        internal static MelonPreferences_Entry<bool> CountInCustom;
        internal static MelonPreferences_Entry<bool> IgnoreUnlock;

        public override void OnInitializeMelon()
        {
            // the mod was renamed from its old name: move the player's files over once
            Legacy.Move(LoggerInstance, "FeatsBySeagull", "FeatsByEMPEROR", "featsbyseagull.bundle", "");
            Instance = this;
            Folder = Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "FeatsByEMPEROR");
            var cat = MelonPreferences.CreateCategory("FeatsBySeagull");
            DebugLog = cat.CreateEntry("DebugLog", true, "Подробный лог / verbose log");
            CountInCustom = cat.CreateEntry("CountInCustomGames", false,
                "Считать прогресс и в пользовательских играх (в игре подвиги там не растут) / count progress in custom games too (the game does not)");
            IgnoreUnlock = cat.CreateEntry("TestAllFeatsWork", false,
                "Для проверки: выбранные подвиги работают, даже если не открыты / testing: chosen feats work even when locked");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName) => Diag.Scene("сцена загружена " + sceneName);

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            Diag.Scene("сцена готова " + sceneName);
            FeatObjects.Invalidate();
            Tracker.Reset();
            Tracker2.Reset();
            if (_setUp) return;
            if (sceneName == null || !sceneName.StartsWith("MainMenu", StringComparison.OrdinalIgnoreCase)) return;
            _setUp = true;
            try
            {
                Progress.Load();
                FeatObjects.RegisterType();
                Badges.Register();
                HarmonyInstance.Patch(AccessTools.Method(typeof(FeatsManager), nameof(FeatsManager.Awake)),
                    postfix: new HarmonyMethod(typeof(FeatsMod), nameof(FeatsAwakePost)));
                HarmonyInstance.Patch(AccessTools.Method(typeof(FeatsManager), nameof(FeatsManager.GetFeat)),
                    postfix: new HarmonyMethod(typeof(FeatObjects), nameof(FeatObjects.GetFeatPost)));
                Effects.Patch(HarmonyInstance);
                Effects2.Patch(HarmonyInstance);
                Diag.Patch(HarmonyInstance);
                FeatObjects.Ensure();
                Msg($"FeatsByEMPEROR {Version}: готово / ready");
            }
            catch (Exception e) { Error("setup failed: " + e); }
        }

        static void FeatsAwakePost() => FeatObjects.Ensure();

        public override void OnUpdate()
        {
            Diag.Frame();
            if (!_setUp) return;
            float now = Time.realtimeSinceStartup;
            bool inGame = false;
            try { inGame = !GameManager.IsMainMenuActive() && GameManager.GetPlayerTransform() != null; } catch { }
            FeatObjects.Tick(now, inGame);
            Progress.Tick(now);
            Texts.Tick(now);
            if (inGame) { Tracker.Update(); Tracker2.Update(); }
            else Effects.NightFactor = 0f;
        }

        public override void OnApplicationQuit()
        {
            if (_setUp) Progress.Save();
        }

        internal static void Log(string s) { if (DebugLog != null && DebugLog.Value) Instance.LoggerInstance.Msg(s); }
        internal static void Msg(string s) => Instance.LoggerInstance.Msg(s);
        internal static void Warn(string s) => Instance.LoggerInstance.Warning(s);
        internal static void Error(string s) => Instance.LoggerInstance.Error(s);
    }

    /// <summary>Per-frame counters: distance walked at night, struggles survived, meters climbed; night amount for Night Eye.</summary>
    internal static class Tracker
    {
        static Vector3 _last;
        static bool _haveLast;
        static float _nightMeters, _climbMeters;
        static float _lastRopeY = float.NaN;
        static bool _wasStruggle;
        static float _struggleCheckAt = -1f;

        public static void Reset() { _haveLast = false; _lastRopeY = float.NaN; _wasStruggle = false; _struggleCheckAt = -1f; }

        static float Night(float h)
        {
            // full night 21:00..05:00, fading in 19:30..21:00 and out 05:00..06:30
            if (h >= 21f || h < 5f) return 1f;
            if (h >= 19.5f) return (h - 19.5f) / 1.5f;
            if (h < 6.5f) return 1f - (h - 5f) / 1.5f;
            return 0f;
        }

        public static void Update()
        {
            try
            {
                var tod = GameManager.GetTimeOfDayComponent();
                float night = tod != null ? Night(tod.GetNormalizedTime() * 24f) : 0f;
                Effects.NightFactor = night;

                var pt = GameManager.GetPlayerTransform();
                var pm = GameManager.GetPlayerManagerComponent();
                if (pt == null || pm == null) { _haveLast = false; return; }
                Vector3 pos = pt.position;
                var mode = pm.GetControlMode();

                // Night Eye: on foot at night (no car/train/rope, no teleports or loads: <3 m per frame)
                if (_haveLast && night >= 0.99f && mode == PlayerControlMode.Normal)
                {
                    float d = Vector3.Distance(pos, _last);
                    if (d > 0.001f && d < 3f)
                    {
                        _nightMeters += d;
                        if (_nightMeters >= 25f) { Progress.Add(Defs.NightEye, _nightMeters / 1000f); _nightMeters = 0f; }
                    }
                }
                _last = pos; _haveLast = true;

                // Climber: height gained on a rope while climbing up
                var pcr = GameManager.GetPlayerClimbRopeComponent();
                if (mode == PlayerControlMode.Climbing && pcr != null && pcr.m_ClimbingState == ClimbingState.Up)
                {
                    if (!float.IsNaN(_lastRopeY))
                    {
                        float dy = pos.y - _lastRopeY;
                        if (dy > 0f && dy < 2f) _climbMeters += dy;
                        if (_climbMeters >= 5f) { Progress.Add(Defs.Climber, _climbMeters); _climbMeters = 0f; }
                    }
                    _lastRopeY = pos.y;
                }
                else _lastRopeY = float.NaN;

                // Bear hide: a struggle ended and 3 s later we are still alive
                var ps = GameManager.GetPlayerStruggleComponent();
                bool inStruggle = ps != null && ps.m_Active;
                if (_wasStruggle && !inStruggle) _struggleCheckAt = Time.realtimeSinceStartup + 3f;
                _wasStruggle = inStruggle;
                if (_struggleCheckAt > 0f && Time.realtimeSinceStartup >= _struggleCheckAt)
                {
                    _struggleCheckAt = -1f;
                    var c = GameManager.GetConditionComponent();
                    if (c != null && c.m_CurrentHP > 0f && pm.GetControlMode() != PlayerControlMode.Dead) Progress.Add(Defs.BearHide, 1f);
                }
            }
            catch (Exception e) { FeatsMod.Error("tracker: " + e.Message); }
        }
    }
}
