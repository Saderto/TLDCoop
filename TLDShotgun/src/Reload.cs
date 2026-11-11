using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace TLDShotgun
{
    /// <summary>
    /// The shotgun prop has its own animator (open barrel, load shell, close barrel). In episode 5 it follows the arms;
    /// survival arms play the same shotgun clips but nobody tells the prop. Log 11-08 showed the pairs have equal length:
    ///   arms FPH_Shotgun_Reload_In (1.17 s)          ↔ prop Open_Barrel_Loaded / Open_Barrel_Empty
    ///   arms FPH_Shotgun_Reload_Loop (1.75 s)        ↔ prop Load_Shell
    ///   arms FPH_Shotgun_Reload_Out (1.25 s)         ↔ prop Close_Barrel
    ///   arms FPH_Shotgun_Reload_LastShell_Empty (1.46 s) ↔ prop Close_Barrel_Empty
    /// So while reloading we copy the arm clip and its exact progress onto the prop every frame.
    /// The empty close has no click in survival: we play the close sound when the barrel bones are back in place.
    /// </summary>
    internal static class Reload
    {
        const string CloseSound = "PLAY_FPH_SHOTGUN_RELOAD_CLOSE";
        static int _seq;
        static float _completedAt = -10f;
        static float _gameCompletedAt = -10f;
        const float BlendSeconds = 0.6f;

        internal static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(PlayerAnimation), nameof(PlayerAnimation.Trigger_Generic_Reload)),
                prefix: new HarmonyMethod(typeof(Reload), nameof(StartPrefix)));
            h.Patch(AccessTools.Method(typeof(PlayerAnimation), nameof(PlayerAnimation.OnAnimationEvent_Generic_Reload_Complete)),
                prefix: new HarmonyMethod(typeof(Reload), nameof(CompletePrefix)));
        }

        static FirstPersonWeapon Prop(PlayerAnimation pa)
        {
            try
            {
                var w = pa.m_EquippedFirstPersonWeaponShoulder;
                if (w == null || w.name.IndexOf("Shotgun", StringComparison.OrdinalIgnoreCase) < 0) return null;
                return w;
            }
            catch { return null; }
        }

        static void StartPrefix(PlayerAnimation __instance, int __0, int __2)
        {
            var w = Prop(__instance);
            if (w == null) return;
            var a = w.m_Animator != null ? w.m_Animator : w.GetComponent<Animator>();
            if (a == null) return;
            ShotgunMod.Log($"перезарядка: начало, вставить {__0}, стреляных {__2} / reload start");
            MelonCoroutines.Start(Follow(__instance, w, a, __2 > 0, ++_seq));
        }

        // we finish the reload ourselves a moment early (see Follow); the clip's own end event then must not finish it twice
        static bool CompletePrefix()
        {
            if (Time.time - _completedAt < 1.5f) { ShotgunMod.Log("перезарядка: повторное завершение пропущено / duplicate complete skipped"); _completedAt = -10f; return false; }
            _gameCompletedAt = Time.time;
            return true;
        }

        static string PropStateFor(string armClip, bool spent)
        {
            if (armClip.Contains("Reload_In")) return spent ? "Open_Barrel_Loaded" : "Open_Barrel_Empty"; // "Loaded" = a spent casing to throw out
            if (armClip.Contains("Reload_Loop")) return "Load_Shell";
            if (armClip.Contains("LastShell_Empty")) return "Close_Barrel_Empty";
            if (armClip.Contains("Reload_Out")) return "Close_Barrel";
            return null;
        }

        // the closing clip normally fires Reload_Complete itself; if our cross-fade swallowed it, fire it once
        static IEnumerator EnsureComplete(PlayerAnimation pa, float reloadStart)
        {
            float t0 = Time.time;
            while (Time.time - t0 < 1.0f) yield return null;
            if (_gameCompletedAt >= reloadStart) yield break;
            try
            {
                _completedAt = -10f;
                pa.OnAnimationEvent_Generic_Reload_Complete();
                _completedAt = Time.time;
                ShotgunMod.Log("перезарядка: завершение вызвано модом / complete fired by mod");
            }
            catch (Exception e) { ShotgunMod.Warn("complete: " + e.Message); }
        }

        static IEnumerator Follow(PlayerAnimation pa, FirstPersonWeapon w, Animator prop, bool spent, int seq)
        {
            var arms = pa.m_Animator;
            if (arms == null) yield break;

            // closed pose of the barrel parts, to hear the empty close at the right moment
            var bones = new List<(Transform t, Quaternion closed)>();
            try
            {
                foreach (var t in w.GetComponentsInChildren<Transform>(true))
                {
                    var nm = t.name;
                    if (nm == "body" || nm == "origin" || nm == "hingePin" || nm == "topLever") bones.Add((t, t.localRotation));
                }
            }
            catch { }

            bool blended = false;

            float start = Time.time;
            string current = null;
            bool sawReload = false, emptyClose = false, clicked = false, wasOpen = false;
            while (seq == _seq && Time.time - start < 8f)
            {
                string clip = null; float n = 0f;
                try
                {
                    // during a cross-fade follow the clip we are going to
                    bool trans = arms.IsInTransition(0);
                    var infos = trans ? arms.GetNextAnimatorClipInfo(0) : arms.GetCurrentAnimatorClipInfo(0);
                    var st = trans ? arms.GetNextAnimatorStateInfo(0) : arms.GetCurrentAnimatorStateInfo(0);
                    if (infos != null && infos.Length > 0 && infos[0].clip != null) clip = infos[0].clip.name;
                    n = st.normalizedTime;
                }
                catch { }

                string target = clip != null ? PropStateFor(clip, spent) : null;
                // the game itself starts a 0.04 s snap from the closing clip to its stance: re-do that same move, slowly.
                // (0.1.13/0.1.14 used the state seen at reload start, which was the draw animation: wrong.)
                bool armsLeaving = target == null && sawReload;
                if (!blended && armsLeaving && (current == "Close_Barrel" || current == "Close_Barrel_Empty"))
                {
                    blended = true;
                    try
                    {
                        if (emptyClose && !clicked) { clicked = true; GameAudioManager.PlaySound(CloseSound, GameManager.GetPlayerObject()); }
                        int nextHash = arms.GetNextAnimatorStateInfo(0).fullPathHash;
                        arms.CrossFadeInFixedTime(nextHash, BlendSeconds, 0);
                        ShotgunMod.Log($"перезарядка: плавный выход в стойку игры ({clip}, {nextHash}) / smooth exit");
                    }
                    catch (Exception e) { ShotgunMod.Warn("smooth exit: " + e.Message); }
                    MelonCoroutines.Start(EnsureComplete(pa, start));
                }

                if (target != null)
                {
                    sawReload = true;
                    float t = Mathf.Clamp01(n);
                    try
                    {
                        var ps = prop.GetCurrentAnimatorStateInfo(0);
                        if (target != current)
                        {
                            prop.Play(target, 0, t);
                            ShotgunMod.Log($"перезарядка: руки {clip} → ружьё {target} ({t:0.00}) / reload sync");
                            current = target;
                            if (target == "Close_Barrel_Empty") emptyClose = true;
                            if (target.StartsWith("Open")) wasOpen = true;
                        }
                        else if (Mathf.Abs(Mathf.Repeat(ps.normalizedTime, 1f) - t) > 0.03f && ps.normalizedTime < 1f)
                            prop.Play(target, 0, t);
                    }
                    catch { }
                }
                else if (sawReload)
                {
                    break; // arms left the reload clips
                }

                if (emptyClose && !clicked && wasOpen && current == "Close_Barrel_Empty")
                {
                    try
                    {
                        float worst = 0f;
                        foreach (var (bt, closed) in bones)
                            if (bt != null) worst = Mathf.Max(worst, Quaternion.Angle(bt.localRotation, closed));
                        float t = Mathf.Clamp01(n);
                        if ((worst < 1.5f && t > 0.15f) || t > 0.7f)
                        {
                            clicked = true;
                            GameAudioManager.PlaySound(CloseSound, GameManager.GetPlayerObject());
                            ShotgunMod.Log($"перезарядка: щелчок пустого ружья ({t:0.00}) / empty close sound");
                        }
                    }
                    catch (Exception e) { clicked = true; ShotgunMod.Warn("close sound: " + e.Message); }
                }
                yield return null;
            }

            try
            {
                if (seq == _seq && current != null) { prop.Play("Idle", 0, 0f); ShotgunMod.Log("перезарядка: конец / reload end"); }
            }
            catch { }
        }
    }
}
