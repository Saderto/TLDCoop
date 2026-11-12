using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace FeatsByEMPEROR
{
    /// <summary>
    /// What the feats give (only while the feat is unlocked and chosen for this game) and the counters that unlock them.
    /// Counters only count the local player's own actions; in co-op the other player's actions arrive through CoopByEMPEROR
    /// and are skipped (CoopByEMPEROR.Game.AnimalSync._applyingHit, distance checks for pots and fishing holes).
    /// </summary>
    internal static class Effects
    {
        static HarmonyLib.Harmony _h;

        static void Pre(Type t, string method, string patch, Type[] args = null, int priority = -1)
        {
            try
            {
                var m = args == null ? AccessTools.Method(t, method) : AccessTools.Method(t, method, args);
                if (m == null) { FeatsMod.Error($"нет метода / no method {t.Name}.{method}"); return; }
                var hm = new HarmonyMethod(typeof(Effects), patch);
                if (priority >= 0) hm.priority = priority;
                _h.Patch(m, prefix: hm);
            }
            catch (Exception e) { FeatsMod.Error($"patch {t.Name}.{method}: {e.Message}"); }
        }

        static void Post(Type t, string method, string patch, Type[] args = null)
        {
            try
            {
                var m = args == null ? AccessTools.Method(t, method) : AccessTools.Method(t, method, args);
                if (m == null) { FeatsMod.Error($"нет метода / no method {t.Name}.{method}"); return; }
                _h.Patch(m, postfix: new HarmonyMethod(typeof(Effects), patch));
            }
            catch (Exception e) { FeatsMod.Error($"patch {t.Name}.{method}: {e.Message}"); }
        }

        static void PrePost(Type t, string method, string pre, string post, Type[] args = null)
        {
            try
            {
                var m = args == null ? AccessTools.Method(t, method) : AccessTools.Method(t, method, args);
                if (m == null) { FeatsMod.Error($"нет метода / no method {t.Name}.{method}"); return; }
                _h.Patch(m, prefix: new HarmonyMethod(typeof(Effects), pre), postfix: new HarmonyMethod(typeof(Effects), post));
            }
            catch (Exception e) { FeatsMod.Error($"patch {t.Name}.{method}: {e.Message}"); }
        }

        internal static void Patch(HarmonyLib.Harmony h)
        {
            _h = h;
            // Butcher
            Pre(typeof(Panel_BodyHarvest), nameof(Panel_BodyHarvest.StartHarvest), nameof(ButcherTime));
            Pre(typeof(Panel_BodyHarvest), nameof(Panel_BodyHarvest.StartQuarter), nameof(ButcherTime));
            PrePost(typeof(Panel_BodyHarvest), nameof(Panel_BodyHarvest.TransferMeatFromCarcassToInventory), nameof(MeatBefore), nameof(MeatAfter));
            // Tailor + Marksman wear
            Pre(typeof(GearItem), nameof(GearItem.Degrade), nameof(DegradePre), new[] { typeof(float) });
            Post(typeof(Panel_Inventory_Examine), nameof(Panel_Inventory_Examine.RepairSuccessful), nameof(RepairDone));
            // Wolf hunter, marksman hits
            Pre(typeof(BaseAi), nameof(BaseAi.ApplyDamage), nameof(AiDamage4), new[] { typeof(float), typeof(float), typeof(DamageSource), typeof(string) }, Priority.First);
            Pre(typeof(BaseAi), nameof(BaseAi.ApplyDamage), nameof(AiDamage3), new[] { typeof(float), typeof(DamageSource), typeof(string) }, Priority.First);
            Post(typeof(BaseAi), nameof(BaseAi.EnterDead), nameof(AiDead));
            Post(typeof(GunItem), nameof(GunItem.Fired), nameof(GunFired));
            PrePost(typeof(BaseAi), nameof(BaseAi.ChangeModeWhenTargetDetected), nameof(WolfDetectPre), nameof(WolfDetectPost));
            // Bear hide
            PrePost(typeof(Condition), nameof(Condition.AddHealth), nameof(AddHealth2), nameof(AddHealthPost), new[] { typeof(float), typeof(DamageSource) });
            PrePost(typeof(Condition), nameof(Condition.AddHealth), nameof(AddHealth3), nameof(AddHealthPost), new[] { typeof(float), typeof(DamageSource), typeof(bool) });
            PrePost(typeof(Condition), nameof(Condition.AddHealthWithNoHudNotification), nameof(AddHealth2), nameof(AddHealthPost));
            Pre(typeof(PlayerStruggle), nameof(PlayerStruggle.ApplyClothingDamagePerSecond), nameof(StruggleClothing));
            // Night eye
            PrePost(typeof(ColorGrading), nameof(ColorGrading.GetModifiedGamma), nameof(GammaPre), nameof(GammaPost));
            // Angler
            Post(typeof(IceFishingHole), nameof(IceFishingHole.RollNextCatch), nameof(FishRoll));
            Post(typeof(IceFishingHole), nameof(IceFishingHole.InstantiateFish), nameof(FishCaught));
            // Iron stomach
            PrePost(typeof(PlayerManager), nameof(PlayerManager.EatingComplete_Internal), nameof(EatPre), nameof(EatPost));
            // Woodcutter
            PrePost(typeof(Panel_BreakDown), nameof(Panel_BreakDown.UpdateDurationLabel), nameof(BreakTimePre), nameof(BreakTimePost));
            Pre(typeof(Panel_BreakDown), nameof(Panel_BreakDown.BreakDownFinished), nameof(BreakDone));
            // Climber
            Post(typeof(PlayerClimbRope), nameof(PlayerClimbRope.SetClimbSpeed), nameof(ClimbSpeed));
            PrePost(typeof(PlayerClimbRope), nameof(PlayerClimbRope.UpdateStamina), nameof(ClimbDrainPre), nameof(ClimbDrainPost));
            PrePost(typeof(PlayerClimbRope), nameof(PlayerClimbRope.UpdateFatigue), nameof(ClimbDrainPre), nameof(ClimbDrainPost));
            // Water carrier
            Post(typeof(CookingPotItem), nameof(CookingPotItem.GetTotalBoilMultiplier), nameof(BoilMultiplier));
            Pre(typeof(CookingPotItem), nameof(CookingPotItem.StartBoilingWater), nameof(BoilStart));
            // Craftsman
            Post(typeof(Panel_Crafting), nameof(Panel_Crafting.GetAdjustedCraftingTime), nameof(CraftTime));
            Pre(typeof(Panel_Crafting), nameof(Panel_Crafting.OnCraftingSuccess), nameof(CraftDone));
        }

        static bool On(string id) => FeatObjects.On(id);

        static bool NearPlayer(Component c, float meters)
        {
            try
            {
                var p = GameManager.GetPlayerTransform();
                if (p == null || c == null) return false;
                return (p.position - c.transform.position).sqrMagnitude <= meters * meters;
            }
            catch { return false; }
        }

        // ---------------- Butcher ----------------
        static void ButcherTime(ref int durationMinutes)
        {
            if (!On(Defs.Butcher)) return;
            durationMinutes = Math.Max(1, (int)Math.Round(durationMinutes * Defs.ButcherTimeScale));
        }

        static void MeatBefore(Panel_BodyHarvest __instance, out float __state)
        {
            __state = -1f;
            try { var bh = __instance.m_BodyHarvest; if (bh != null) __state = bh.m_MeatAvailableKG.ToQuantity(1f); } catch { }
        }

        static void MeatAfter(Panel_BodyHarvest __instance, float __state)
        {
            if (__state < 0f) return;
            try
            {
                var bh = __instance.m_BodyHarvest;
                float after = bh != null ? bh.m_MeatAvailableKG.ToQuantity(1f) : 0f;
                Progress.Add(Defs.Butcher, __state - after);
            }
            catch (Exception e) { FeatsMod.Error("meat: " + e.Message); }
        }

        // ---------------- Tailor / Marksman wear ----------------
        static void DegradePre(GearItem __instance, ref float hp)
        {
            if (hp <= 0f) return;
            bool tailor = On(Defs.Tailor), marks = On(Defs.Marksman);
            if (!tailor && !marks) return;
            try
            {
                if (tailor && __instance.m_ClothingItem != null) hp *= Defs.TailorWearScale;
                else if (marks && __instance.m_GunItem != null) hp *= Defs.MarksmanWearScale;
            }
            catch { }
        }

        static void RepairDone(Panel_Inventory_Examine __instance)
        {
            try
            {
                var gi = __instance.m_GearItem;
                if (gi != null && gi.m_ClothingItem != null) Progress.Add(Defs.Tailor, 1f);
            }
            catch (Exception e) { FeatsMod.Error("repair: " + e.Message); }
        }

        // ---------------- Wolf hunter / marksman hits ----------------
        static System.Reflection.FieldInfo _coopApplying;
        static bool _coopChecked;

        /// <summary>CoopByEMPEROR sets AnimalSync._applyingHit while it applies the other player's hit to an animal our game runs.</summary>
        static bool CoopRemoteHit()
        {
            if (!_coopChecked)
            {
                _coopChecked = true;
                try
                {
                    var t = AccessTools.TypeByName("CoopByEMPEROR.Game.AnimalSync");
                    if (t != null) _coopApplying = AccessTools.Field(t, "_applyingHit");
                    if (_coopApplying != null) FeatsMod.Log("CoopByEMPEROR найден: чужие попадания не считаются / co-op hits from the other player are skipped");
                }
                catch { }
            }
            try { return _coopApplying != null && (bool)_coopApplying.GetValue(null); } catch { return false; }
        }

        static readonly Dictionary<IntPtr, float> _wolvesHit = new();   // wolves the local player hurt -> real time
        static bool _inDamage;

        static void AiDamage4(BaseAi __instance, float damage, DamageSource damageSource) => OnAiDamage(__instance, damage, damageSource);
        static void AiDamage3(BaseAi __instance, float damage, DamageSource damageSource) => OnAiDamage(__instance, damage, damageSource);

        static void OnAiDamage(BaseAi ai, float damage, DamageSource src)
        {
            if (_inDamage || src != DamageSource.Player || damage <= 0f || ai == null) return;
            _inDamage = true;
            try
            {
                if (CoopRemoteHit()) return;
                if (ai.m_CurrentMode == AiMode.Dead || ai.m_CurrentHP <= 0f) return;
                float now = Time.realtimeSinceStartup;
                IntPtr key = ai.Pointer;
                if (ai.m_AiSubType == AiSubType.Wolf) _wolvesHit[key] = now;

            }
            catch (Exception e) { FeatsMod.Error("ai damage: " + e.Message); }
            finally { _inDamage = false; }
        }

        /// <summary>Marksman: every shot of our own rifle, revolver or shotgun (not the flare gun or the camera). Fired runs only in the shooter's game.</summary>
        static void GunFired(GunItem __instance)
        {
            try
            {
                if (__instance == null) return;
                var t = __instance.m_GunType;
                if (t == GunType.Rifle || t == GunType.Revolver || t == GunType.Shotgun) Progress.Add(Defs.Marksman, 1f);
            }
            catch (Exception e) { FeatsMod.Error("gun fired: " + e.Message); }
        }

        static void AiDead(BaseAi __instance)
        {
            try
            {
                if (__instance == null || __instance.m_AiSubType != AiSubType.Wolf) return;
                IntPtr key = __instance.Pointer;
                if (!_wolvesHit.TryGetValue(key, out var t)) return;
                _wolvesHit.Remove(key);
                if (Time.realtimeSinceStartup - t > 1800f) return;
                Progress.Add(Defs.WolfHunter, 1f);
            }
            catch (Exception e) { FeatsMod.Error("wolf dead: " + e.Message); }
        }

        static void WolfDetectPre(BaseAi __instance, out int __state)
        {
            __state = int.MinValue;
            try
            {
                if (__instance.m_AiSubType != AiSubType.Wolf) return;
                int bonus = CoopApi.WolfFleeBonusNow();   // in co-op: the bonus of the player this wolf is thinking about
                if (bonus <= 0) return;
                __state = __instance.m_FleeChanceWhenTargetDetected;
                __instance.m_FleeChanceWhenTargetDetected = Math.Min(100, __state + bonus);
            }
            catch { __state = int.MinValue; }
        }

        static void WolfDetectPost(BaseAi __instance, int __state)
        {
            if (__state == int.MinValue) return;
            try { __instance.m_FleeChanceWhenTargetDetected = __state; } catch { }
        }

        // ---------------- Bear hide ----------------
        static bool _inHealth;

        static bool Predator(DamageSource s) => s == DamageSource.Wolf || s == DamageSource.Bear || s == DamageSource.Cougar;

        static void AddHealth2(ref float hp, DamageSource __1, out bool __state) => __state = ScaleHit(ref hp, __1);
        static void AddHealth3(ref float hp, DamageSource cause, out bool __state) => __state = ScaleHit(ref hp, cause);
        static void AddHealthPost(bool __state) { if (__state) _inHealth = false; }

        /// <summary>The 2-argument AddHealth goes on to the 3-argument one: scale once, in the outermost call.</summary>
        static bool ScaleHit(ref float hp, DamageSource cause)
        {
            if (_inHealth || hp >= 0f || !Predator(cause) || !On(Defs.BearHide)) return false;
            _inHealth = true;
            hp *= Defs.BearHideDamageScale;
            return true;
        }

        static void StruggleClothing(ref float damagePerSecond)
        {
            if (On(Defs.BearHide)) damagePerSecond *= Defs.BearHideDamageScale;
        }

        // ---------------- Night eye ----------------
        internal static float NightFactor;   // 0 by day .. 1 at full night, set by FeatsMod.OnUpdate

        static void GammaPre(ColorGrading __instance, out float __state)
        {
            __state = float.NaN;
            if (NightFactor <= 0f || !On(Defs.NightEye)) return;
            try
            {
                __state = __instance.m_BrightnessSetting;
                __instance.m_BrightnessSetting = Math.Min(1f, __state + Defs.NightEyeBrightness * NightFactor);
            }
            catch { __state = float.NaN; }
        }

        static void GammaPost(ColorGrading __instance, float __state)
        {
            if (float.IsNaN(__state)) return;
            try { __instance.m_BrightnessSetting = __state; } catch { }
        }

        // ---------------- Angler ----------------
        static void FishRoll(IceFishingHole __instance)
        {
            if (!On(Defs.Angler)) return;
            try
            {
                var c = __instance.m_NextCatch;
                if (c != null) c.m_MinutesToNextCatch *= Defs.AnglerTimeScale;
            }
            catch { }
        }

        static void FishCaught(IceFishingHole __instance, GearItem __result)
        {
            if (__result == null) return;
            if (NearPlayer(__instance, 6f)) Progress.Add(Defs.Angler, 1f);
        }

        // ---------------- Iron stomach ----------------
        sealed class EatState { public GearItem Item; public float Cal; public float Kg; public bool Counts; }

        static void EatPre(PlayerManager __instance, bool success, out EatState __state)
        {
            __state = null;
            try
            {
                var gi = __instance.m_FoodItemEaten;
                var food = gi != null ? gi.m_FoodItem : null;
                if (food == null) return;
                if (success && On(Defs.IronStomach) && __instance.m_FoodPoisoningOnEatingComplete
                    && UnityEngine.Random.value > Defs.IronStomachKeepChance)
                {
                    __instance.m_FoodPoisoningOnEatingComplete = false;
                    FeatsMod.Log("Железный желудок: отравление отменено / Iron Stomach: food poisoning avoided");
                }
                bool counts = food.m_IsRawMeat;
                if (!counts) return;
                __state = new EatState
                {
                    Item = gi,
                    Cal = __instance.m_FoodItemEatenStartingCalories,
                    Kg = __instance.m_FoodItemEatenStartingWeight.ToQuantity(1f),
                    Counts = true,
                };
            }
            catch (Exception e) { FeatsMod.Error("eat: " + e.Message); }
        }

        static void EatPost(bool success, EatState __state)
        {
            if (__state == null || !success) return;
            try
            {
                float frac = 1f;
                var gi = __state.Item;
                bool alive = gi != null && !gi.WasCollected && gi;
                if (alive && gi.m_FoodItem != null && __state.Cal > 0f)
                    frac = Math.Clamp((__state.Cal - gi.m_FoodItem.m_CaloriesRemaining) / __state.Cal, 0f, 1f);
                Progress.Add(Defs.IronStomach, __state.Kg * frac);
            }
            catch (Exception e) { FeatsMod.Error("eat post: " + e.Message); }
        }

        // ---------------- Woodcutter ----------------
        static bool IsWood(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.Contains("Stick", StringComparison.OrdinalIgnoreCase) || name.Contains("Softwood", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Hardwood", StringComparison.OrdinalIgnoreCase) || name.Contains("ReclaimedWood", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Firelog", StringComparison.OrdinalIgnoreCase) || name.Contains("Firewood", StringComparison.OrdinalIgnoreCase);
        }

        static int WoodUnits(BreakDown bd)
        {
            int n = 0;
            try
            {
                var objs = bd.m_YieldObject; var units = bd.m_YieldObjectUnits;
                if (objs == null) return 0;
                for (int i = 0; i < objs.Length; i++)
                {
                    var go = objs[i];
                    if (go == null || !IsWood(go.name)) continue;
                    n += units != null && i < units.Length ? Math.Max(0, units[i]) : 1;
                }
            }
            catch { }
            return n;
        }

        sealed class TimeState { public BreakDown Bd; public float A, B; }

        static void BreakTimePre(Panel_BreakDown __instance, out TimeState __state)
        {
            __state = null;
            if (!On(Defs.Woodcutter)) return;
            try
            {
                var bd = __instance.m_BreakDown;
                if (bd == null || WoodUnits(bd) <= 0) return;
                __state = new TimeState { Bd = bd, A = bd.m_TimeCostHours, B = bd.m_EditModeDestroyTimeCostHours };
                bd.m_TimeCostHours = __state.A * Defs.WoodcutterTimeScale;
                bd.m_EditModeDestroyTimeCostHours = __state.B * Defs.WoodcutterTimeScale;
            }
            catch { __state = null; }
        }

        static void BreakTimePost(TimeState __state)
        {
            if (__state == null) return;
            try { __state.Bd.m_TimeCostHours = __state.A; __state.Bd.m_EditModeDestroyTimeCostHours = __state.B; } catch { }
        }

        static void BreakDone(Panel_BreakDown __instance)
        {
            try
            {
                var bd = __instance.m_BreakDown;
                if (bd != null) Progress.Add(Defs.Woodcutter, WoodUnits(bd));
            }
            catch (Exception e) { FeatsMod.Error("breakdown: " + e.Message); }
        }

        // ---------------- Climber ----------------
        static void ClimbSpeed(PlayerClimbRope __instance)
        {
            if (!On(Defs.Climber)) return;
            try { __instance.m_ClimbSpeed *= Defs.ClimberSpeedScale; } catch { }
        }

        sealed class Drain { public float[] V; }

        static void ClimbDrainPre(PlayerClimbRope __instance, out Drain __state)
        {
            __state = null;
            if (!On(Defs.Climber)) return;
            try
            {
                var p = __instance;
                __state = new Drain
                {
                    V = new[] { p.m_StaminaDrainPerSecondClimbingUp, p.m_StaminaDrainPerSecondClimbingDown, p.m_StaminaDrainPerSecondClimbingHolding,
                                p.m_FatigueDrainPerSecondClimbingUp, p.m_FatigueDrainPerSecondClimbingDown, p.m_FatigueDrainPerSecondClimbingHolding }
                };
                float k = Defs.ClimberDrainScale;
                p.m_StaminaDrainPerSecondClimbingUp *= k; p.m_StaminaDrainPerSecondClimbingDown *= k; p.m_StaminaDrainPerSecondClimbingHolding *= k;
                p.m_FatigueDrainPerSecondClimbingUp *= k; p.m_FatigueDrainPerSecondClimbingDown *= k; p.m_FatigueDrainPerSecondClimbingHolding *= k;
            }
            catch { __state = null; }
        }

        static void ClimbDrainPost(PlayerClimbRope __instance, Drain __state)
        {
            if (__state == null) return;
            try
            {
                var p = __instance; var v = __state.V;
                p.m_StaminaDrainPerSecondClimbingUp = v[0]; p.m_StaminaDrainPerSecondClimbingDown = v[1]; p.m_StaminaDrainPerSecondClimbingHolding = v[2];
                p.m_FatigueDrainPerSecondClimbingUp = v[3]; p.m_FatigueDrainPerSecondClimbingDown = v[4]; p.m_FatigueDrainPerSecondClimbingHolding = v[5];
            }
            catch { }
        }

        // ---------------- Water carrier ----------------
        static void BoilMultiplier(ref float __result)
        {
            if (On(Defs.WaterCarrier)) __result *= Defs.WaterTimeScale;
        }

        static void BoilStart(CookingPotItem __instance, Il2CppTLD.IntBackedUnit.ItemLiquidVolume amountToBoil)
        {
            try
            {
                if (!NearPlayer(__instance, 6f)) return;
                Progress.Add(Defs.WaterCarrier, amountToBoil.ToQuantity(1f));
            }
            catch (Exception e) { FeatsMod.Error("boil: " + e.Message); }
        }

        // ---------------- Craftsman ----------------
        static void CraftTime(ref int __result)
        {
            if (__result > 0 && On(Defs.Craftsman)) __result = Math.Max(1, (int)Math.Round(__result * Defs.CraftTimeScale));
        }

        static void CraftDone(Panel_Crafting __instance)
        {
            try
            {
                int q = 1;
                try { var rc = __instance.m_RequirementContainer; if (rc != null) q = Math.Max(1, rc.GetQuantity()); } catch { }
                Progress.Add(Defs.Craftsman, q);
            }
            catch (Exception e) { FeatsMod.Error("craft: " + e.Message); }
        }
    }
}
