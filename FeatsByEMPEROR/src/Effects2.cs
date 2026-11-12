using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using UnityEngine;

namespace FeatsByEMPEROR
{
    /// <summary>
    /// Second batch of feats (Defs2): counters and bonuses. Same rules as <see cref="Effects"/>: only the local player's own actions
    /// count, bonuses work only while the feat is unlocked and chosen for this game.
    /// </summary>
    internal static class Effects2
    {
        static HarmonyLib.Harmony _h;

        static void Patch(Type t, string method, string pre = null, string post = null, Type[] args = null)
        {
            try
            {
                var m = args == null ? AccessTools.Method(t, method) : AccessTools.Method(t, method, args);
                if (m == null) { FeatsMod.Error($"нет метода / no method {t.Name}.{method}"); return; }
                _h.Patch(m, prefix: pre == null ? null : new HarmonyMethod(typeof(Effects2), pre),
                    postfix: post == null ? null : new HarmonyMethod(typeof(Effects2), post));
            }
            catch (Exception e) { FeatsMod.Error($"patch {t.Name}.{method}: {e.Message}"); }
        }

        static void PatchAll(Type t, string method, string pre = null, string post = null)
        {
            try
            {
                foreach (var m in AccessTools.GetDeclaredMethods(t))
                {
                    if (m.Name != method) continue;
                    _h.Patch(m, prefix: pre == null ? null : new HarmonyMethod(typeof(Effects2), pre),
                        postfix: post == null ? null : new HarmonyMethod(typeof(Effects2), post));
                }
            }
            catch (Exception e) { FeatsMod.Error($"patch {t.Name}.{method}*: {e.Message}"); }
        }

        internal static void Patch(HarmonyLib.Harmony h)
        {
            _h = h;
            // food: counters, calories, thirst
            Patch(typeof(PlayerManager), nameof(PlayerManager.EatingComplete_Internal), nameof(EatPre), nameof(EatPost));
            Patch(typeof(FreezingBuff), nameof(FreezingBuff.Apply), nameof(FreezeBuffPre), nameof(FreezeBuffPost));
            Patch(typeof(FatigueBuff), nameof(FatigueBuff.Apply), nameof(FatigueBuffPre), nameof(FatigueBuffPost));
            Patch(typeof(ConditionOverTimeBuff), nameof(ConditionOverTimeBuff.Apply), nameof(CondBuffPre), nameof(CondBuffPost));
            Patch(typeof(ConditionRestBuff), nameof(ConditionRestBuff.Apply), nameof(RestBuffPre), nameof(RestBuffPost));
            // cooking
            Patch(typeof(CookingPotItem), nameof(CookingPotItem.GetTotalCookMultiplier), post: nameof(CookMultiplier));
            Patch(typeof(CookingPotItem), nameof(CookingPotItem.PickUpCookedGearItem), nameof(PickUpCooked));
            // spoilage
            Patch(typeof(GearItem), nameof(GearItem.DecayOverTODHours), nameof(DecayPre));
            // tanner
            Patch(typeof(EvolveItem), nameof(EvolveItem.Update), nameof(EvolvePre), nameof(EvolvePost));
            Patch(typeof(EvolveItem), nameof(EvolveItem.DoEvolution), nameof(EvolveDone));
            // forge, ammo
            Patch(typeof(Forge), nameof(Forge.ModifyCraftingTime), post: nameof(ForgeTime));
            Patch(typeof(Panel_Crafting), nameof(Panel_Crafting.GetAdjustedCraftingTime), post: nameof(CraftTimeMorning));
            Patch(typeof(Panel_Crafting), nameof(Panel_Crafting.OnCraftingSuccess), nameof(CraftPre), nameof(CraftPost));
            // sharpen, repair
            Patch(typeof(Panel_Inventory_Examine), nameof(Panel_Inventory_Examine.GetSharpenConditionIncreaseAndDuration), post: nameof(SharpenAmount));
            Patch(typeof(Panel_Inventory_Examine), nameof(Panel_Inventory_Examine.SharpenSuccessful), post: nameof(SharpenDone));
            Patch(typeof(Panel_Inventory_Examine), nameof(Panel_Inventory_Examine.RepairSuccessful), post: nameof(RepairDone));
            Patch(typeof(GearItem), nameof(GearItem.Degrade), nameof(DegradePre), args: new[] { typeof(float) });
            // locks
            Patch(typeof(Lock), nameof(Lock.PrepareForceLock), post: nameof(ForceLockTime));
            Patch(typeof(Lock), nameof(Lock.FinishForceLock), nameof(ForceLockDone));
            // light and fire
            Patch(typeof(TorchItem), nameof(TorchItem.GetModifiedBurnLifetimeMinutes), post: nameof(TorchLife));
            Patch(typeof(KeroseneLampItem), nameof(KeroseneLampItem.GetModifiedFuelBurnLitersPerHour), post: nameof(LampRate));
            Patch(typeof(KeroseneLampItem), nameof(KeroseneLampItem.ReduceFuel), nameof(LampFuelPre), nameof(LampFuelPost));
            Patch(typeof(FuelSourceItem), nameof(FuelSourceItem.GetModifiedBurnDurationHours), post: nameof(FuelHours));
            Patch(typeof(Panel_FireStart), nameof(Panel_FireStart.HasDirectSunlight), post: nameof(Sunlight));
            Patch(typeof(Panel_FireStart), nameof(Panel_FireStart.OnDoneStartingFire), nameof(FireStartDone));
            // sleep
            PatchAll(typeof(Rest), nameof(Rest.BeginSleeping), post: nameof(SleepBegin));
            Patch(typeof(Rest), nameof(Rest.EndSleeping), nameof(SleepEnd));
            Patch(typeof(Rest), nameof(Rest.UpdateFatigue), nameof(RestFatiguePre), nameof(RestFatiguePost));
            Patch(typeof(Bed), nameof(Bed.Degrade), nameof(BedWear));
            Patch(typeof(GearItem), nameof(GearItem.GetItemWeightKG), nameof(WeightPre), nameof(WeightPost), new[] { typeof(bool) });
            Patch(typeof(GearItem), nameof(GearItem.GetItemWeightKG), nameof(WeightPre), nameof(WeightPost), new[] { typeof(float), typeof(bool) });
            // ice
            Patch(typeof(IceCrackingTrigger), nameof(IceCrackingTrigger.TryUpdateTimer), nameof(IceTimer));
            Patch(typeof(Panel_IceFishingHoleClear), nameof(Panel_IceFishingHoleClear.UseTool), nameof(HoleToolPre), nameof(HoleToolPost));
            Patch(typeof(Panel_IceFishingHoleClear), nameof(Panel_IceFishingHoleClear.OnBreakIceComplete), nameof(HoleDone));
            PatchAll(typeof(Panel_GenericProgressBar), nameof(Panel_GenericProgressBar.Launch), nameof(BarPre), nameof(BarPost));
            // body: temperature, fatigue, speed, death
            Patch(typeof(Freezing), nameof(Freezing.CalculateBodyTemperature), post: nameof(BodyTemp));
            PatchAll(typeof(Fatigue), nameof(Fatigue.AddFatigue), nameof(FatiguePre), nameof(FatiguePost));
            Patch(typeof(PlayerMovement), nameof(PlayerMovement.GetCelestialNavigatorMultiplier), post: nameof(SpeedBonus));
            Patch(typeof(Condition), nameof(Condition.PlayerDeath), nameof(DeathPre));
            // cans
            Patch(typeof(PlayerManager), nameof(PlayerManager.OpenAndUseFoodInventoryItem), nameof(CanOpenStart));
            Patch(typeof(PlayerManager), nameof(PlayerManager.UseSmashableItem), nameof(CanOpenStart2));
            Patch(typeof(PlayerManager), nameof(PlayerManager.OnFoodOpeningComplete), nameof(CanOpenedPre), nameof(CanOpenedPost));
            Patch(typeof(PlayerManager), nameof(PlayerManager.OnSmashComplete), nameof(CanOpenedPre), nameof(CanOpenedPost));
            // loot
            Patch(typeof(PlayerManager), nameof(PlayerManager.AddItemToPlayerInventory), nameof(Looted));
            Patch(typeof(Container), nameof(Container.BeginContainerOpen), nameof(ContainerOpen));
            // train, trader
            Patch(typeof(Il2CppTLD.SteamTrain.SteamTrain), nameof(Il2CppTLD.SteamTrain.SteamTrain.GetMaxVelocityIncreaseModifier), post: nameof(TrainAccel));
            Patch(typeof(Il2CppTLD.SteamTrain.SteamTrain), nameof(Il2CppTLD.SteamTrain.SteamTrain.GetOilConsumedModifier), post: nameof(TrainFuel));
            Patch(typeof(Il2CppTLD.Trader.TraderManager), nameof(Il2CppTLD.Trader.TraderManager.CompleteTrade), nameof(TradeDone));
            Patch(typeof(Il2CppTLD.Trader.TraderManager), nameof(Il2CppTLD.Trader.TraderManager.AddTrust), nameof(TrustPre));
        }

        // ================= common helpers =================
        internal static bool On(string id) => FeatObjects.On(id);

        internal static double Hours()
        {
            try { var t = GameManager.GetTimeOfDayComponent(); return t != null ? t.GetHoursPlayedNotPaused() : -1; } catch { return -1; }
        }

        internal static float HourOfDay()
        {
            try { var t = GameManager.GetTimeOfDayComponent(); return t != null ? t.GetNormalizedTime() * 24f : 12f; } catch { return 12f; }
        }

        static string _worldKey;
        static float _worldKeyAt = -100f;

        internal static void InvalidateWorld() => _worldKeyAt = -100f;

        internal static string WorldKey()
        {
            float now = Time.realtimeSinceStartup;
            if (now - _worldKeyAt < 5f) return _worldKey;
            _worldKeyAt = now;
            try
            {
                string slot = SaveGameSystem.GetCurrentSaveName();
                int seed = GameManager.m_SceneTransitionData != null ? GameManager.m_SceneTransitionData.m_GameRandomSeed : 0;
                _worldKey = string.IsNullOrEmpty(slot) ? null : slot + "|" + seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { _worldKey = null; }
            return _worldKey;
        }

        /// <summary>This game's state; StartH is set the first time we see the world.</summary>
        internal static World W()
        {
            var w = Progress.GetWorld(WorldKey());
            if (w != null && w.StartH < 0) { double h = Hours(); if (h >= 0) { w.StartH = h; Progress.MarkDirty(); } }
            return w;
        }

        internal static string BaseName(GameObject go)
        {
            if (go == null) return "";
            string n = go.name ?? "";
            int i = n.IndexOf("(Clone)", StringComparison.Ordinal);
            return (i >= 0 ? n.Substring(0, i) : n).Trim();
        }

        static bool Has(string s, string part) => s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

        internal static string Scene() { try { return GameManager.m_ActiveScene ?? ""; } catch { return ""; } }

        internal static bool OutdoorScene() { try { return GameManager.IsOutDoorsScene(GameManager.m_ActiveScene); } catch { return true; } }

        internal static bool IndoorEnv() { try { var w = GameManager.GetWeatherComponent(); return w != null && w.IsIndoorEnvironment(); } catch { return false; } }

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

        internal static void Hud(string ru, string en)
        {
            try { HUDMessage.AddMessage(Texts.Ru() ? ru : en, false, true); } catch { }
        }

        static void Give(string gear, int units)
        {
            if (units <= 0) return;
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                if (pm != null) pm.InstantiateItemInPlayerInventory(gear, units, 1f, PlayerManager.InventoryInstantiateFlags.None);
            }
            catch (Exception e) { FeatsMod.Error("give " + gear + ": " + e.Message); }
        }

        // item kinds by prefab name
        static bool IsCoffee(string n) => Has(n, "Coffee") && !Has(n, "CoffeeTin");
        static bool IsTea(string n) => Has(n, "Tea") && !Has(n, "TeaPackage") && !Has(n, "Steak");
        static bool IsSoda(string n) => n.StartsWith("GEAR_Soda", StringComparison.OrdinalIgnoreCase) || Has(n, "_Soda");
        static bool IsStew(string n) => Has(n, "Stew");
        static readonly string[] SweetParts = { "Candy", "Chocolate", "Granola", "EnergyBar", "Cookie", "Cake", "Honey", "Jam", "Syrup", "Marshmallow", "Sweet", "Toffee", "Fudge" };
        static bool IsSweet(string n) { foreach (var p in SweetParts) if (Has(n, p)) return true; return false; }

        // ================= food =================
        sealed class Eat { public GearItem Item; public string Name; public float Cal, Kg; public bool Hot, Meat, Drink; public float Thirst; }

        static void EatPre(PlayerManager __instance, bool success, out Eat __state)
        {
            __state = null;
            if (!success) return;
            try
            {
                var gi = __instance.m_FoodItemEaten;
                var food = gi != null ? gi.m_FoodItem : null;
                if (food == null) return;
                __state = new Eat
                {
                    Item = gi,
                    Name = BaseName(gi.gameObject),
                    Cal = __instance.m_FoodItemEatenStartingCalories,
                    Kg = __instance.m_FoodItemEatenStartingWeight.ToQuantity(1f),
                    Hot = food.IsHot(),
                    Meat = food.m_IsMeat || food.m_IsFish,
                    Drink = food.m_IsDrink,
                    Thirst = food.m_ReduceThirst,
                };
            }
            catch (Exception e) { FeatsMod.Error("eat2: " + e.Message); }
        }

        static void EatPost(Eat __state)
        {
            if (__state == null) return;
            try
            {
                float frac = 1f;
                var gi = __state.Item;
                bool alive = gi != null && !gi.WasCollected && gi;
                if (alive && gi.m_FoodItem != null && __state.Cal > 0f)
                    frac = Math.Clamp((__state.Cal - gi.m_FoodItem.m_CaloriesRemaining) / __state.Cal, 0f, 1f);
                if (frac <= 0.01f) return;
                string n = __state.Name;
                bool whole = frac >= 0.5f;   // a cup/can counts once it is mostly finished

                Progress.Add(Defs2.Hoarder, __state.Kg * frac);
                if (__state.Hot && whole) Progress.Add(Defs2.HotMeal, 1f);
                bool coffee = IsCoffee(n), tea = IsTea(n), soda = IsSoda(n), sweet = IsSweet(n), stew = IsStew(n);
                if (coffee && whole) Progress.Add(Defs2.Coffee, 1f);
                if (tea && whole) Progress.Add(Defs2.Tea, 1f);
                if (soda && whole) Progress.Add(Defs2.Soda, 1f);
                if (sweet && whole) Progress.Add(Defs2.Sweet, 1f);

                var w = W();
                double h = Hours();
                if (w != null && h >= 0)
                {
                    if (__state.Meat) { w.LastMeatH = h; Progress.MarkDirty(); }
                    if ((coffee || tea) && h - w.LastWakeH <= 1.0 && !w.RitualCounted)
                    {
                        w.RitualCounted = true; Progress.MarkDirty();
                        Progress.Add(Defs2.Ritual, 1f);
                        if (On(Defs2.Ritual))
                        {
                            NoFatigueUntil = Math.Max(NoFatigueUntil, h + 0.5);
                            Hud("Утренний ритуал: 30 минут без усталости", "Morning Ritual: no fatigue for 30 minutes");
                        }
                    }
                }

                // extra calories
                float pct = 0f;
                if (sweet && On(Defs2.Sweet)) pct += 0.10f;
                if (!__state.Meat && On(Defs2.Vegetarian)) pct += 0.25f;
                if (stew && On(Defs2.Stew)) pct += 0.25f;
                if (soda && On(Defs2.Soda)) pct += 0.50f;
                if (FortuneNow(2)) pct += 0.10f;
                float extra = __state.Cal * frac * pct;
                if (extra > 0.5f)
                {
                    var hunger = GameManager.GetHungerComponent();
                    if (hunger != null) hunger.AddReserveCalories(extra);
                    FeatsMod.Log($"калории +{extra:0} / calories +{extra:0} ({n})");
                }
                if (soda && On(Defs2.Soda) && Math.Abs(__state.Thirst) > 0f)
                {
                    var th = GameManager.GetThirstComponent();
                    if (th != null) th.AddThirst(-Math.Abs(__state.Thirst) * frac);
                }
            }
            catch (Exception e) { FeatsMod.Error("eat2 post: " + e.Message); }
        }

        // buffs from food and drinks: scale while the game applies them
        static GearItem ItemOf(Component c) { try { return c != null ? c.GetComponent<GearItem>() : null; } catch { return null; } }

        static void FreezeBuffPre(FreezingBuff __instance, out float __state)
        {
            __state = float.NaN;
            try
            {
                var gi = ItemOf(__instance);
                if (gi == null) return;
                string n = BaseName(gi.gameObject);
                float k = 1f;
                if (On(Defs2.HotMeal) && gi.m_FoodItem != null && gi.m_FoodItem.IsHot()) k *= 2f;
                if (On(Defs2.Tea) && IsTea(n)) k *= 2f;
                if (On(Defs2.Stew) && IsStew(n)) k *= 2f;
                if (k == 1f) return;
                k = Math.Min(k, 3f);
                __state = __instance.m_DurationHours;
                __instance.m_DurationHours = __state * k;
            }
            catch { __state = float.NaN; }
        }

        static void FreezeBuffPost(FreezingBuff __instance, float __state)
        {
            if (float.IsNaN(__state)) return;
            try { __instance.m_DurationHours = __state; } catch { }
        }

        static void FatigueBuffPre(FatigueBuff __instance, out float[] __state)
        {
            __state = null;
            try
            {
                var gi = ItemOf(__instance);
                if (gi == null) return;
                string n = BaseName(gi.gameObject);
                bool coffee = On(Defs2.Coffee) && IsCoffee(n), tea = On(Defs2.Tea) && IsTea(n);
                if (!coffee && !tea) return;
                __state = new[] { __instance.m_InitialPercentDecrease, __instance.m_DurationHours };
                if (coffee) __instance.m_InitialPercentDecrease = __state[0] * 1.5f;
                if (tea) __instance.m_DurationHours = __state[1] * 2f;
            }
            catch { __state = null; }
        }

        static void FatigueBuffPost(FatigueBuff __instance, float[] __state)
        {
            if (__state == null) return;
            try { __instance.m_InitialPercentDecrease = __state[0]; __instance.m_DurationHours = __state[1]; } catch { }
        }

        static void CondBuffPre(ConditionOverTimeBuff __instance, out float __state)
        {
            __state = float.NaN;
            try
            {
                var gi = ItemOf(__instance);
                if (gi == null || !On(Defs2.Tea) || !IsTea(BaseName(gi.gameObject))) return;
                __state = __instance.m_NumHours;
                __instance.m_NumHours = __state * 2f;
            }
            catch { __state = float.NaN; }
        }

        static void CondBuffPost(ConditionOverTimeBuff __instance, float __state)
        {
            if (float.IsNaN(__state)) return;
            try { __instance.m_NumHours = __state; } catch { }
        }

        static void RestBuffPre(ConditionRestBuff __instance, out float __state)
        {
            __state = float.NaN;
            try
            {
                var gi = ItemOf(__instance);
                if (gi == null || !On(Defs2.Tea) || !IsTea(BaseName(gi.gameObject))) return;
                __state = __instance.m_NumHoursRestAffected;
                __instance.m_NumHoursRestAffected = __state * 2f;
            }
            catch { __state = float.NaN; }
        }

        static void RestBuffPost(ConditionRestBuff __instance, float __state)
        {
            if (float.IsNaN(__state)) return;
            try { __instance.m_NumHoursRestAffected = __state; } catch { }
        }

        // ================= cooking =================
        static void CookMultiplier(ref float __result)
        {
            if (On(Defs2.Cook)) __result *= 0.75f;
        }

        /// <summary>The player takes a finished dish out of a pot (local action).</summary>
        static void PickUpCooked(CookingPotItem __instance)
        {
            try
            {
                if (__instance.m_CookingState != CookingPotItem.CookingState.Ready) return;
                var gi = __instance.m_GearItemBeingCooked;
                if (gi == null || gi.m_FoodItem == null) return;
                if (!NearPlayer(__instance, 8f)) return;
                Progress.Add(Defs2.Cook, 1f);
                if (IsStew(BaseName(gi.gameObject))) Progress.Add(Defs2.Stew, 1f);
            }
            catch (Exception e) { FeatsMod.Error("cooked: " + e.Message); }
        }

        // ================= spoilage =================
        static void DecayPre(GearItem __instance, ref float scale)
        {
            try
            {
                if (__instance.m_FoodItem == null) return;
                if (__instance.m_InPlayerInventory) { if (On(Defs2.Hoarder)) scale *= 0.8f; }
                else if (On(Defs2.Home) && IsHomeScene()) scale *= 0.5f;
            }
            catch { }
        }

        // ================= tanner =================
        static bool IsHide(string n) => Has(n, "Pelt") || Has(n, "Hide");
        static bool IsGut(string n) => Has(n, "Gut");

        static readonly Dictionary<IntPtr, bool> _evolveKind = new();

        static void EvolvePre(EvolveItem __instance, out float __state)
        {
            __state = float.NaN;
            if (!On(Defs2.Tanner)) return;
            try
            {
                if (!_evolveKind.TryGetValue(__instance.Pointer, out var hide))
                {
                    string n = BaseName(__instance.gameObject);
                    hide = !Has(n, "Cured") && !Has(n, "Dried") && (IsHide(n) || IsGut(n));
                    _evolveKind[__instance.Pointer] = hide;
                }
                if (!hide) return;
                __state = __instance.m_TimeToEvolveGameDays;
                __instance.m_TimeToEvolveGameDays = __state * 0.7f;
            }
            catch { __state = float.NaN; }
        }

        static void EvolvePost(EvolveItem __instance, float __state)
        {
            if (float.IsNaN(__state)) return;
            try { __instance.m_TimeToEvolveGameDays = __state; } catch { }
        }

        static void EvolveDone(EvolveItem __instance)
        {
            try
            {
                string n = BaseName(__instance.gameObject);
                if (Has(n, "Cured") || !IsHide(n)) return;
                var gi = __instance.GetComponent<GearItem>();
                if ((gi != null && gi.m_InPlayerInventory) || NearPlayer(__instance, 40f)) Progress.Add(Defs2.Tanner, 1f);
            }
            catch (Exception e) { FeatsMod.Error("evolve: " + e.Message); }
        }

        // ================= forge, ammo, morning crafting =================
        static void ForgeTime(ref int __result)
        {
            if (__result > 0 && On(Defs2.Smith)) __result = Math.Max(1, (int)Math.Round(__result * 0.7f));
        }

        static bool Morning() { float h = HourOfDay(); return h >= 5f && h < 9f; }

        static void CraftTimeMorning(ref int __result)
        {
            if (__result > 0 && On(Defs2.EarlyBird) && Morning()) __result = Math.Max(1, (int)Math.Round(__result * 0.9f));
        }

        sealed class Craft { public string Result; public int Count; public bool Forge; public int Scrap; }

        static void CraftPre(Panel_Crafting __instance, out Craft __state)
        {
            __state = null;
            try
            {
                var bp = __instance.SelectedBPI;
                if (bp == null) return;
                int q = 1;
                try { var rc = __instance.m_RequirementContainer; if (rc != null) q = Math.Max(1, rc.GetQuantity()); } catch { }
                var c = new Craft { Result = bp.m_CraftedResultGear != null ? BaseName(bp.m_CraftedResultGear.gameObject) : "" };
                c.Count = q * Math.Max(1, bp.m_CraftedResultCount);
                try { c.Forge = __instance.m_CurrentLocation != null && __instance.m_CurrentLocation.TryCast<Forge>() != null; } catch { }
                if (c.Forge)
                {
                    var req = bp.m_RequiredGear;
                    if (req != null)
                        for (int i = 0; i < req.Length; i++)
                        {
                            var r = req[i];
                            if (r != null && r.m_Item != null && Has(BaseName(r.m_Item.gameObject), "ScrapMetal")) c.Scrap += r.m_Count * q;
                        }
                }
                __state = c;
            }
            catch (Exception e) { FeatsMod.Error("craft2: " + e.Message); }
        }

        static void CraftPost(Craft __state)
        {
            if (__state == null) return;
            try
            {
                bool ammo = Has(__state.Result, "Ammo") || Has(__state.Result, "Cartridge") || Has(__state.Result, "Shell");
                if (ammo)
                {
                    Progress.Add(Defs2.Gunsmith, __state.Count);
                    if (On(Defs2.Gunsmith))
                    {
                        int extra = 0;
                        for (int i = 0; i < __state.Count; i++) if (UnityEngine.Random.value < 0.25f) extra++;
                        if (extra > 0)
                        {
                            Give(__state.Result, extra);
                            Hud($"Оружейник: +{extra} патр.", $"Gunsmith: +{extra} extra");
                        }
                    }
                }
                if (__state.Forge)
                {
                    Progress.Add(Defs2.Smith, Math.Max(1, __state.Count));
                    if (On(Defs2.Smith) && __state.Scrap > 0)
                    {
                        float back = __state.Scrap * 0.2f;
                        int n = (int)Math.Floor(back);
                        if (UnityEngine.Random.value < back - n) n++;
                        if (n > 0) { Give("GEAR_ScrapMetal", n); Hud($"Кузнец: сэкономлено металлолома {n}", $"Blacksmith: {n} scrap metal saved"); }
                    }
                }
            }
            catch (Exception e) { FeatsMod.Error("craft2 post: " + e.Message); }
        }

        // ================= sharpen, repair, wear =================
        static void SharpenAmount(ref float conditionIncrease)
        {
            if (On(Defs2.Sharpener)) conditionIncrease *= 1.5f;
        }

        static void SharpenDone(Panel_Inventory_Examine __instance)
        {
            Progress.Add(Defs2.Sharpener, 1f);
            MaybePerfect(__instance);
        }

        static void RepairDone(Panel_Inventory_Examine __instance) => MaybePerfect(__instance, true);

        static void MaybePerfect(Panel_Inventory_Examine p, bool count = false)
        {
            try
            {
                var gi = p.m_GearItem;
                if (gi == null || gi.GetNormalizedCondition() < 0.995f) return;
                if (count) Progress.Add(Defs2.Perfect, 1f);
                var w = W();
                string guid = Guid(gi.gameObject);
                if (w != null && !string.IsNullOrEmpty(guid)) { w.Perfect[guid] = Hours(); Progress.MarkDirty(); }
            }
            catch (Exception e) { FeatsMod.Error("perfect: " + e.Message); }
        }

        static System.Reflection.MethodInfo _guidFn;
        static bool _guidChecked;

        /// <summary>ObjectGuid.MaybeGetGuidFromGameObject by reflection (its base class lives in an assembly we don't reference).</summary>
        static readonly Dictionary<IntPtr, string> _guids = new();

        static string Guid(GameObject go)
        {
            if (go == null) return null;
            if (_guids.TryGetValue(go.Pointer, out var g)) return g;
            g = GuidRaw(go);
            if (_guids.Count > 4000) _guids.Clear();
            _guids[go.Pointer] = g;
            return g;
        }

        static string GuidRaw(GameObject go)
        {
            if (!_guidChecked)
            {
                _guidChecked = true;
                try { _guidFn = AccessTools.Method(AccessTools.TypeByName("Il2Cpp.ObjectGuid"), "MaybeGetGuidFromGameObject"); } catch { }
                if (_guidFn == null) FeatsMod.Error("нет ObjectGuid / no ObjectGuid");
            }
            try { return _guidFn?.Invoke(null, new object[] { go }) as string; } catch { return null; }
        }

        // can opening: tool wear is cancelled while a can is being opened
        static float _canWindowUntil = -1f;

        static void DegradePre(GearItem __instance, ref float hp)
        {
            if (hp <= 0f) return;
            try
            {
                if (_canWindowUntil > 0f && Time.realtimeSinceStartup < _canWindowUntil && On(Defs2.Canner)
                    && __instance.m_FoodItem == null) { hp = 0f; return; }
                if (On(Defs2.Burglar) && Has(BaseName(__instance.gameObject), "Prybar")) hp *= 0.5f;
                if (On(Defs2.Perfect))
                {
                    var w = W();
                    if (w != null && w.Perfect.Count > 0)
                    {
                        string guid = Guid(__instance.gameObject);
                        if (!string.IsNullOrEmpty(guid) && w.Perfect.TryGetValue(guid, out var t))
                        {
                            if (Hours() - t < 24.0) hp = 0f;
                            else { w.Perfect.Remove(guid); Progress.MarkDirty(); }
                        }
                    }
                }
            }
            catch { }
        }

        // ================= locks =================
        static void ForceLockTime(Lock __instance)
        {
            if (!On(Defs2.Burglar)) return;
            try { __instance.ForceTime = __instance.ForceTime * 0.6f; } catch { }
        }

        static void ForceLockDone(Lock __instance, bool complete)
        {
            if (!complete) return;
            if (NearPlayer(__instance, 6f)) Progress.Add(Defs2.Burglar, 1f);
        }

        // ================= light and fire =================
        static void TorchLife(ref float __result) { if (On(Defs2.Torch)) __result *= 1.3f; }

        static void LampRate(ref ItemLiquidVolume __result)
        {
            if (On(Defs2.Lamp)) __result = __result * 0.75f;
        }

        static void LampFuelPre(KeroseneLampItem __instance, out float __state)
        {
            __state = -1f;
            try { __state = __instance.m_CurrentFuelLiters.ToQuantity(1f); } catch { }
        }

        static void LampFuelPost(KeroseneLampItem __instance, float __state)
        {
            if (__state < 0f) return;
            try
            {
                float used = __state - __instance.m_CurrentFuelLiters.ToQuantity(1f);
                if (used <= 0f || used > 5f) return;
                var gi = __instance.m_GearItem;
                if ((gi != null && gi.m_InPlayerInventory) || NearPlayer(__instance, 25f)) Progress.Add(Defs2.Lamp, used);
            }
            catch { }
        }

        static void FuelHours(ref float __result) { if (On(Defs2.Hearth)) __result *= 1.15f; }

        /// <summary>Prometheus: the lens works in any daylight outdoors, not only in clear/partly cloudy sky.</summary>
        static void Sunlight(ref bool __result)
        {
            if (__result || !On(Defs2.Prometheus)) return;
            try
            {
                float h = HourOfDay();
                if (h >= 6f && h < 18f && !IndoorEnv()) __result = true;
            }
            catch { }
        }

        static void FireStartDone(Panel_FireStart __instance)
        {
            try
            {
                var st = __instance.GetSelectedFireStarter();
                if (st == null) return;
                string n = BaseName(st.gameObject);
                if (!(Has(n, "Match") || Has(n, "Lighter"))) return;
                var w = W();
                double h = Hours();
                if (w != null && h >= 0) { w.LastMatchH = h; Progress.MarkDirty(); }
            }
            catch (Exception e) { FeatsMod.Error("fire start: " + e.Message); }
        }

        // ================= sleep =================
        static double _sleepStartH = -1;
        static bool _sleepOutdoorBedroll;
        static string _sleepPlace;

        static void SleepBegin(Rest __instance)
        {
            try
            {
                _sleepStartH = Hours();
                var bed = __instance.m_Bed;
                if (bed == null) { _sleepStartH = -1; return; }   // passing time, not sleeping
                bool bedroll = bed != null && bed.GetComponent<GearItem>() != null;
                _sleepOutdoorBedroll = bedroll && OutdoorScene() && !IndoorEnv();
                _sleepPlace = PlaceKey();
            }
            catch { }
        }

        /// <summary>Place for Nomad/Homeowner: the indoor scene, or a cave/shelter spot inside a region (40 m grid).</summary>
        static string PlaceKey()
        {
            string sc = Scene();
            if (!OutdoorScene()) return sc;
            if (!IndoorEnv()) return null;
            var p = GameManager.GetPlayerTransform();
            if (p == null) return null;
            return sc + "@" + Mathf.RoundToInt(p.position.x / 40f) + "," + Mathf.RoundToInt(p.position.z / 40f);
        }

        static void SleepEnd(Rest __instance)
        {
            try
            {
                double h = Hours();
                if (_sleepStartH < 0 || h < 0) return;
                double slept = h - _sleepStartH;
                _sleepStartH = -1;
                if (slept <= 0.05) return;
                var w = W();
                if (w != null) { w.LastWakeH = h; w.RitualCounted = false; Progress.MarkDirty(); }
                if (_sleepOutdoorBedroll) Progress.Add(Defs2.LightSleep, (float)slept);
                float hod = HourOfDay();
                if (hod < 6f && slept >= 3.0) Progress.Add(Defs2.EarlyBird, 1f);
                if (slept >= 3.0 && !string.IsNullOrEmpty(_sleepPlace))
                {
                    int n = Progress.AddToSet("nomad", _sleepPlace);
                    if (n > 0) Progress.Max(Defs2.Nomad, n);
                    if (!_sleepPlace.Contains("@"))   // a real house, not a cave spot
                    {
                        float nights = Progress.AddTally("home:" + _sleepPlace, 1f);
                        Progress.Max(Defs2.Home, nights);
                        if (w != null) { w.HomeNights.TryGetValue(_sleepPlace, out var k); w.HomeNights[_sleepPlace] = k + 1; _homeCache = null; }
                    }
                }
                Tracker2.OnWake(hod);
            }
            catch (Exception e) { FeatsMod.Error("sleep end: " + e.Message); }
        }

        static string _homeCache;
        static float _homeAt = -100f;

        static string HomeScene()
        {
            if (_homeCache != null && Time.realtimeSinceStartup - _homeAt < 10f) return _homeCache;
            _homeAt = Time.realtimeSinceStartup;
            string best = ""; int bn = 0;
            var w = W();
            if (w != null) foreach (var kv in w.HomeNights) if (kv.Value > bn) { bn = kv.Value; best = kv.Key; }
            return _homeCache = best;
        }

        static bool IsHomeScene()
        {
            string home = HomeScene();
            return home.Length > 0 && string.Equals(home, Scene(), StringComparison.OrdinalIgnoreCase);
        }

        static void RestFatiguePre(Rest __instance, out float __state)
        {
            __state = float.NaN;
            if (!On(Defs2.Home) || !IsHomeScene()) return;
            try { __state = __instance.m_ReduceFatiguePerHourRest; __instance.m_ReduceFatiguePerHourRest = __state * 1.2f; } catch { __state = float.NaN; }
        }

        static void RestFatiguePost(Rest __instance, float __state)
        {
            if (float.IsNaN(__state)) return;
            try { __instance.m_ReduceFatiguePerHourRest = __state; } catch { }
        }

        static void BedWear(ref float hoursUsed) { if (On(Defs2.Nomad)) hoursUsed *= 0.5f; }

        static int _weightDepth;

        static void WeightPre() => _weightDepth++;

        static void WeightPost(GearItem __instance, ref ItemWeight __result)
        {
            _weightDepth--;
            if (_weightDepth != 0 || !On(Defs2.Nomad)) return;
            try { if (__instance.m_Bed != null) __result = __result * 0.5f; } catch { }
        }

        // ================= ice =================
        static readonly HashSet<IntPtr> _iceScaled = new();

        internal static void SceneReset() { _iceScaled.Clear(); _guids.Clear(); _evolveKind.Clear(); _homeCache = null; InvalidateWorld(); }

        static void IceTimer(IceCrackingTrigger __instance)
        {
            if (!On(Defs2.IceWalker)) return;
            try { if (_iceScaled.Add(__instance.Pointer)) __instance.m_SecondsBeforeIceCracks *= 2f; } catch { }
        }

        static bool _holeTool;

        static void HoleToolPre(Panel_IceFishingHoleClear __instance) { if (On(Defs2.IceCutter)) _holeTool = true; }
        static void HoleToolPost() => _holeTool = false;

        static void HoleDone(bool success, bool playerCancel)
        {
            if (success && !playerCancel) Progress.Add(Defs2.IceCutter, 1f);
        }

        static int _barDepth;

        static void BarPre(ref float seconds, ref float minutes, out bool __state)
        {
            __state = _barDepth == 0;
            _barDepth++;
            if (!__state) return;
            float k = 1f;
            if (_holeTool) k *= 0.5f;
            if (On(Defs2.EarlyBird) && Morning()) k *= 0.9f;
            if (k >= 1f) return;
            seconds *= k; minutes *= k;
        }

        static void BarPost() { if (_barDepth > 0) _barDepth--; }

        // ================= body =================
        static void BodyTemp(ref float __result)
        {
            try
            {
                float add = 0f;
                bool outdoorScene = OutdoorScene();
                bool outside = outdoorScene && !IndoorEnv();
                if (On(Defs2.Forest)) { if (outside) add += 3f; else if (!outdoorScene) add -= 4f; }
                if (On(Defs2.Sun) && outside && SunnyNow()) add += 3f;
                if (FortuneNow(0)) add += 2f;
                __result += add;
            }
            catch { }
        }

        internal static bool SunnyNow()
        {
            try
            {
                float h = HourOfDay();
                if (h < 7f || h >= 18f) return false;
                var w = GameManager.GetWeatherComponent();
                return w != null && w.IsClear();
            }
            catch { return false; }
        }

        internal static double NoFatigueUntil = -1;
        internal static bool NearFriend;
        static int _fatDepth;

        static void FatiguePre(ref float fatigueValue, out bool __state)
        {
            __state = _fatDepth == 0;
            _fatDepth++;
            if (!__state || fatigueValue <= 0f) return;
            try
            {
                if (NoFatigueUntil > 0 && Hours() < NoFatigueUntil) { fatigueValue = 0f; return; }
                float k = 1f;
                if (NearFriend && On(Defs2.Together)) k *= 0.85f;
                if (FortuneNow(1)) k *= 0.9f;
                fatigueValue *= k;
            }
            catch { }
        }

        static void FatiguePost() { if (_fatDepth > 0) _fatDepth--; }

        internal static bool LightLoad;

        static void SpeedBonus(ref float __result)
        {
            float k = 1f;
            if (LightLoad && On(Defs2.Minimalist)) k *= 1.1f;
            if (FortuneNow(3)) k *= 1.05f;
            __result *= k;
        }

        /// <summary>Phoenix: the first death of this game is cancelled. Also counts deaths after 20 days.</summary>
        static bool DeathPre(Condition __instance, bool __runOriginal)
        {
            if (!__runOriginal) return false;
            try
            {
                var w = W();
                if (On(Defs2.Phoenix) && w != null && !w.PhoenixUsed)
                {
                    w.PhoenixUsed = true;
                    Progress.MarkDirty();
                    Progress.Save();
                    __instance.m_CurrentHP = Math.Max(1f, __instance.m_MaxHP * 0.1f);
                    Hud("Феникс: вы восстали из пепла", "Phoenix: you rise from the ashes");
                    FeatsMod.Msg("Феникс: смерть отменена / Phoenix: death cancelled");
                    return false;
                }
                if (w != null && w.StartH >= 0 && Hours() >= 20 * 24) Progress.Add(Defs2.Phoenix, 1f);
            }
            catch (Exception e) { FeatsMod.Error("phoenix: " + e.Message); }
            return true;
        }

        // ================= cans =================
        static void CanOpenStart() { if (On(Defs2.Canner)) _canWindowUntil = Time.realtimeSinceStartup + 600f; }
        static void CanOpenStart2() => CanOpenStart();

        sealed class CanState { public FoodItem Food; public float Cal, Hp; }

        static void CanOpenedPre(PlayerManager __instance, out CanState __state)
        {
            __state = null;
            try
            {
                var gi = __instance.m_FoodItemOpened;
                if (gi == null || gi.m_FoodItem == null) return;
                __state = new CanState { Food = gi.m_FoodItem, Cal = gi.m_FoodItem.m_CaloriesRemaining, Hp = gi.m_CurrentHP };
            }
            catch { }
        }

        static void CanOpenedPost(bool success, bool playerCancel, CanState __state)
        {
            try
            {
                if (_canWindowUntil > 0f) _canWindowUntil = Time.realtimeSinceStartup + 1f;
                if (!success || playerCancel || __state == null) return;
                Progress.Add(Defs2.Canner, 1f);
                if (!On(Defs2.Canner)) return;
                var f = __state.Food;
                if (f == null || f.WasCollected || !f) return;
                if (f.m_CaloriesRemaining < __state.Cal) f.m_CaloriesRemaining = __state.Cal;
                var gi = f.m_GearItem;
                if (gi != null && gi.m_CurrentHP < __state.Hp) gi.m_CurrentHP = __state.Hp;
            }
            catch (Exception e) { FeatsMod.Error("can: " + e.Message); }
        }

        // ================= loot =================
        static readonly HashSet<string> Rare = new(StringComparer.OrdinalIgnoreCase)
        {
            "GEAR_Rifle", "GEAR_HuntingRifle", "GEAR_Revolver", "GEAR_Revolver44Mag", "GEAR_RevolverFlat", "GEAR_FlareGun",
            "GEAR_Crampons", "GEAR_Flashlight", "GEAR_Hacksaw", "GEAR_EmergencyStim", "GEAR_HeatPad",
            "GEAR_RifleCleaningKit", "GEAR_MagnifyingLens", "GEAR_Firestriker", "GEAR_Shotgun",
        };

        static void Looted(GearItem gi)
        {
            try
            {
                if (gi == null || gi.m_HasBeenOwnedByPlayer) return;
                if (Rare.Contains(BaseName(gi.gameObject))) Progress.Add(Defs2.Lucky, 1f);
            }
            catch { }
        }

        static readonly string[] Bonus =
        {
            "GEAR_PackMatches", "GEAR_WoodMatches", "GEAR_CannedBeans", "GEAR_GranolaBar", "GEAR_BeefJerky", "GEAR_CandyBar",
            "GEAR_Cloth", "GEAR_ScrapMetal", "GEAR_HeavyBandage", "GEAR_BottlePainKillers", "GEAR_NewsprintRoll",
            "GEAR_PeanutButter", "GEAR_CoffeeTin", "GEAR_GreenTeaPackage", "GEAR_Soda", "GEAR_EnergyBar", "GEAR_CannedSardines",
        };

        static void ContainerOpen(Container __instance, bool __runOriginal)
        {
            try
            {
                if (!__runOriginal || !On(Defs2.Lucky) || __instance.IsInspected() || !NearPlayer(__instance, 5f)) return;
                if (UnityEngine.Random.value >= 0.05f) return;
                string g = Bonus[UnityEngine.Random.Range(0, Bonus.Length)];
                Give(g, 1);
                Hud("Счастливая рука: нашлось кое-что ещё", "Lucky Hand: you found something extra");
            }
            catch (Exception e) { FeatsMod.Error("lucky: " + e.Message); }
        }

        // ================= train, trader =================
        static void TrainAccel(ref float __result) { if (On(Defs2.Train)) __result *= 1.3f; }
        static void TrainFuel(ref float __result) { if (On(Defs2.Train)) __result *= 0.8f; }

        static void TradeDone() => Progress.Add(Defs2.Merchant, 1f);

        static void TrustPre(ref int trustChange) { if (trustChange > 0 && On(Defs2.Merchant)) trustChange *= 2; }

        // ================= Fortune =================
        internal static bool FortuneNow(int kind)
        {
            if (!On(Defs2.Fortune)) return false;
            var w = W();
            return w != null && w.FortuneKind == kind && Hours() < w.FortuneUntilH;
        }

        internal static void RollFortune()
        {
            if (!On(Defs2.Fortune)) return;
            var w = W();
            double h = Hours();
            if (w == null || h < 0 || h < w.FortuneUntilH) return;
            float hod = HourOfDay();
            double toSix = (6f - hod + 24f) % 24f;
            if (hod < 6f) toSix += 24.0;
            w.FortuneKind = UnityEngine.Random.Range(0, 4);
            w.FortuneUntilH = h + Math.Max(1.0, toSix);
            Progress.MarkDirty();
            switch (w.FortuneKind)
            {
                case 0: Hud("Фортуна: сегодня вам на 2 °C теплее", "Fortune: today you feel 2 °C warmer"); break;
                case 1: Hud("Фортуна: сегодня усталость растёт на 10% медленнее", "Fortune: today fatigue grows 10% slower"); break;
                case 2: Hud("Фортуна: сегодня еда даёт на 10% больше калорий", "Fortune: today food gives 10% more calories"); break;
                default: Hud("Фортуна: сегодня вы ходите на 5% быстрее", "Fortune: today you walk 5% faster"); break;
            }
        }
    }
}
