using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Il2Cpp;

namespace FeatsBySeagull
{
    /// <summary>
    /// Diagnostics for the freeze in the feat selection window: logs every step of the window and of our feats,
    /// and a watchdog thread reports the last step when the game stops drawing frames.
    /// </summary>
    internal static class Diag
    {
        static readonly string[] PanelMethods =
        {
            "FeatSelectWindowSelected", "ShouldShowFeatSelection", "GetNumUnlockedFeats", "SetupFeatScrollList", "CleanupFeatScrollList",
            "AddFeatToList", "UpdateSelectedFeats", "CleanupSelectedFeats", "LoadFeatureIcon", "UpdateSelectFeatVisuals",
            "UpdateFeatProgressBar", "UpdateFeatActionButton", "UpdateFeatMarkerSprites", "UpdateFeatLabels",
            "UpdateFeatButtonLegend", "FeatSelectionIndexSelected", "FeatSelectionNext", "FeatSelectionPrev", "UpdateFeatControls", "UpdateSelectFeatWindow", "OnToggleFeatActive", "OnSelectFeatsContinue", "OnSelectFeatsBack",
        };

        internal static volatile string Last = "";
        static long _lastFrameTicks = DateTime.UtcNow.Ticks;
        static int _depth;
        internal static float ActiveUntil = -1f;
        internal static bool Active => UnityEngine.Time.realtimeSinceStartup < ActiveUntil;

        internal static void Patch(HarmonyLib.Harmony h)
        {
            var pre = new HarmonyMethod(typeof(Diag), nameof(Enter));
            var post = new HarmonyMethod(typeof(Diag), nameof(Leave));
            foreach (var n in PanelMethods)
            {
                try
                {
                    var m = AccessTools.Method(typeof(Panel_MainMenu), n);
                    if (m == null) { FeatsMod.Warn("diag: нет метода " + n); continue; }
                    h.Patch(m, prefix: pre, postfix: post);
                }
                catch (Exception e) { FeatsMod.Warn("diag " + n + ": " + e.Message); }
            }
            try { h.Patch(AccessTools.Method(typeof(Feat), nameof(Feat.CreateBadgeInfo)), prefix: new HarmonyMethod(typeof(Diag), nameof(BadgeEnter)), postfix: post); }
            catch (Exception e) { FeatsMod.Warn("diag CreateBadgeInfo: " + e.Message); }

            var t = new Thread(Watch) { IsBackground = true, Name = "FeatsBySeagull watchdog" };
            t.Start();
            FeatsMod.Msg("диагностика окна подвигов включена / feat window diagnostics on");
        }

        internal static void Frame() { _framesStarted = true; Interlocked.Exchange(ref _lastFrameTicks, DateTime.UtcNow.Ticks); }

        internal static void Step(string s) => Mark(s);
        static bool _framesStarted;

        // The window calls its Update* methods every frame: each method is written to the log once per opening of the window,
        // the watchdog still knows the current step.
        static readonly System.Collections.Generic.HashSet<string> _logged = new();

        static readonly object _lock = new();
        static System.IO.StreamWriter _file;

        /// <summary>Own file, not the console: a console window that is clicked into stops every write to it.</summary>
        internal static void Write(string s)
        {
            try
            {
                lock (_lock)
                {
                    if (_file == null)
                    {
                        string path = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "FeatsBySeagull_diag.log");
                        _file = new System.IO.StreamWriter(path, false) { AutoFlush = true };
                    }
                    _file.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + s);
                }
            }
            catch { }
        }

        static void Mark(string s)
        {
            Last = s;
            if (_logged.Add(s.Trim())) Write(s);
        }

        internal static void Scene(string what)
        {
            _logged.Clear();
            _depth = 0;
            Write("=== " + what);
        }

        static void Enter(MethodBase __originalMethod)
        {
            string n = __originalMethod.Name;
            if (n == "FeatSelectWindowSelected" || n == "CleanupFeatScrollList" || n == "FeatSelectionNext" || n == "FeatSelectionPrev" || n == "FeatSelectionIndexSelected") { _logged.Clear(); _depth = 0; Write("--- " + n + " " + Selected()); }
            ActiveUntil = UnityEngine.Time.realtimeSinceStartup + 30f;
            _depth++;
            Mark(new string(' ', Math.Min(_depth, 10) * 2) + "> " + n);
        }

        static string Selected()
        {
            try
            {
                var p = InterfaceManager.GetPanel<Panel_MainMenu>();
                if (p == null || p.m_FeatSelectionScrollList == null) return "";
                return "index " + p.m_FeatSelectionScrollList.m_SelectedIndex;
            }
            catch (Exception e) { return e.GetType().Name; }
        }

        static void Leave(MethodBase __originalMethod)
        {
            Mark(new string(' ', Math.Min(_depth, 10) * 2) + "< " + __originalMethod.Name);
            if (_depth > 0) _depth--;
        }

        static void BadgeEnter(Feat __instance, MethodBase __originalMethod)
        {
            _depth++;
            string t = "?";
            try { t = ((int)__instance.m_FeatType).ToString(); } catch { }
            Step(new string(' ', Math.Min(_depth, 10) * 2) + "> CreateBadgeInfo " + t);
        }

        static void Watch()
        {
            bool reported = false;
            while (true)
            {
                Thread.Sleep(1000);
                double idle = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastFrameTicks)).TotalSeconds;
                if (!_framesStarted) continue;
                if (idle >= 5)
                {
                    if (!reported)
                    {
                        reported = true;
                        Write($"!!! игра не отвечает {idle:0} с, последний шаг: {Last.Trim()}");
                    }
                }
                else if (reported)
                {
                    reported = false;
                    Write("игра снова отвечает");
                }
            }
        }
    }
}
