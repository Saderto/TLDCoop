using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace ShotgunByEMPEROR
{
    /// <summary>
    /// Shot instead of the episode 5 slug: N pellets with spread; damage of one pellet =
    /// gun damage × DamageMultiplier / N, falling off with distance; one skill point per shot.
    /// </summary>
    internal static class Pellets_
    {
        // set while a shotgun pellet resolves its hit (vp_Bullet.Start)
        [ThreadStatic] static bool _inPellet;
        static float _factor = 1f;
        static bool _scaling;
        static int _skillFrame = -1;

        internal static void Patch(HarmonyLib.Harmony h)
        {
            h.Patch(AccessTools.Method(typeof(vp_FPSShooter), nameof(vp_FPSShooter.Fire)),
                prefix: new HarmonyMethod(typeof(Pellets_), nameof(FirePrefix)));
            h.Patch(AccessTools.Method(typeof(vp_Bullet), nameof(vp_Bullet.Start)),
                prefix: new HarmonyMethod(typeof(Pellets_), nameof(BulletPrefix)),
                finalizer: new HarmonyMethod(typeof(Pellets_), nameof(BulletFinalizer)));
            h.Patch(AccessTools.Method(typeof(BaseAi), nameof(BaseAi.ApplyDamage), new[] { typeof(float), typeof(DamageSource), typeof(string) }),
                prefix: new HarmonyMethod(typeof(Pellets_), nameof(Damage3Prefix)) { priority = Priority.High },
                finalizer: new HarmonyMethod(typeof(Pellets_), nameof(DamageFinalizer)));
            h.Patch(AccessTools.Method(typeof(BaseAi), nameof(BaseAi.ApplyDamage), new[] { typeof(float), typeof(float), typeof(DamageSource), typeof(string) }),
                prefix: new HarmonyMethod(typeof(Pellets_), nameof(Damage4Prefix)) { priority = Priority.High },
                finalizer: new HarmonyMethod(typeof(Pellets_), nameof(DamageFinalizer)));
            h.Patch(AccessTools.Method(typeof(LocalizedDamage), nameof(LocalizedDamage.RollChanceToKill)),
                postfix: new HarmonyMethod(typeof(Pellets_), nameof(KillPostfix)));
            h.Patch(AccessTools.Method(typeof(SkillsManager), nameof(SkillsManager.IncrementPointsAndNotify)),
                prefix: new HarmonyMethod(typeof(Pellets_), nameof(SkillPrefix)));
        }

        static bool IsShotgunShooter(vp_FPSShooter s)
        {
            try { return s != null && s.gameObject.name.IndexOf("Shotgun", StringComparison.OrdinalIgnoreCase) >= 0; }
            catch { return false; }
        }

        static void FirePrefix(vp_FPSShooter __instance)
        {
            if (!IsShotgunShooter(__instance)) return;
            int n = Math.Max(1, ShotgunMod.Pellets.Value);
            if (__instance.ProjectileCount != n || Math.Abs(__instance.ProjectileSpread - ShotgunMod.SpreadDeg.Value) > 0.001f)
            {
                __instance.ProjectileCount = n;
                __instance.ProjectileSpread = ShotgunMod.SpreadDeg.Value;
                ShotgunMod.Log($"дробь: {n} дробин, разлёт {ShotgunMod.SpreadDeg.Value}° / pellets set");
            }
        }

        static void BulletPrefix(vp_Bullet __instance)
        {
            try
            {
                if (__instance.m_GunType != GunType.Shotgun) return;
                float dist = __instance.Range;
                var t = __instance.transform;
                if (Physics.Raycast(t.position, t.forward, out RaycastHit hit, __instance.Range))
                    dist = hit.distance;
                float near = ShotgunMod.FullDamageMeters.Value, far = Math.Max(near + 0.1f, ShotgunMod.MinDamageMeters.Value);
                float min = Mathf.Clamp01(ShotgunMod.MinDamageFactor.Value);
                float k = dist <= near ? 1f : dist >= far ? min : 1f - (dist - near) / (far - near) * (1f - min);
                _factor = k * ShotgunMod.DamageMultiplier.Value / Math.Max(1, ShotgunMod.Pellets.Value);
                _inPellet = true;
            }
            catch (Exception e) { ShotgunMod.Warn("pellet: " + e.Message); }
        }

        static Exception BulletFinalizer(Exception __exception)
        {
            _inPellet = false;
            return __exception;
        }

        static void Damage3Prefix(ref float damage)
        {
            if (!_inPellet || _scaling) return;
            AwardHit();
            float before = damage;
            damage *= _factor;
            _scaling = true;
            ShotgunMod.Log($"дробина: урон {before:0.#} → {damage:0.#}");
        }

        static void Damage4Prefix(ref float damage)
        {
            if (!_inPellet || _scaling) return;
            AwardHit();
            float before = damage;
            damage *= _factor;
            _scaling = true;
            ShotgunMod.Log($"дробина: урон {before:0.#} → {damage:0.#}");
        }

        // vp_Bullet.Start gives a skill point for a hit only to the rifle (GunType Rifle→SkillType.Rifle) and the
        // revolver; for GunType.Shotgun it gives nothing (checked in GameAssembly 2.59). One point per shot that hit an animal.
        static int _hitFrame = -1;
        static void AwardHit()
        {
            if (_hitFrame == Time.frameCount) return;
            _hitFrame = Time.frameCount;
            try
            {
                ShotgunSkill.Create(GameManager.GetSkillsManager());
                GameManager.GetSkillsManager()?.IncrementPointsAndNotify(SkillType.Shotgun, 1, (SkillsManager.PointAssignmentMode)0);
                ShotgunMod.Log("навык «Дробовик» +1 за попадание / shotgun skill +1");
            }
            catch (Exception e) { ShotgunMod.Warn("skill point: " + e.Message); }
        }

        static Exception DamageFinalizer(Exception __exception)
        {
            _scaling = false;
            return __exception;
        }

        static void KillPostfix(ref bool __result)
        {
            if (!_inPellet || !__result) return;
            if (UnityEngine.Random.Range(0, 100) >= ShotgunMod.KillChancePercent.Value) __result = false;
        }

        static bool SkillPrefix()
        {
            if (!_inPellet) return true;
            if (_skillFrame == Time.frameCount) return false; // one point per shot, not per pellet
            _skillFrame = Time.frameCount;
            return true;
        }
    }
}
