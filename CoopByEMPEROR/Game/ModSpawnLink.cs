using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Il2Cpp;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Связь с ModComponent (и встроенным в него GearSpawner) без жёсткой ссылки на его dll (мода может не быть).
    // Его вещи на земле при первом заходе в локацию бросаются своим генератором (new System.Random, криптогенератор),
    // и у каждого игрока выпадало своё: есть ли банка на месте и какая именно (заглушка GEAR_Random… заменяется
    // случайной вещью из списка). Раскладка первого посетителя выравнивает это только рядом с игроками (40 м на
    // большой карте), поэтому в общем мире решаем это от зерна мира + локации + места: у всех выпадет одно и то же.
    // Так же и содержимое этих вещей, которое моды бросают в Awake (штуки в коробке, порошок, вода).
    // В одиночной игре и в своём сохранении друга всё как в моде.
    internal static class ModSpawnLink
    {
        private static MethodInfo _getProb;
        private static PropertyInfo _pos, _prefab;
        private static FieldInfo _names, _wNames, _wWeights, _stackChance, _powderChance;
        private static int _spawnFrame = -1;   // кадр, в котором GearSpawner раскладывает вещи локации (в общем мире)
        private static bool _warned;

        public static void Patch(HarmonyLib.Harmony h)
        {
            Type prob = null, rnd = null, wrnd = null, stack = null, powder = null, spawnMgr = null;
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    prob ??= a.GetType("GearSpawner.ProbabilisticGearSpawnHandler", false);
                    rnd ??= a.GetType("ModComponent.API.Components.ModRandomItemComponent", false);
                    wrnd ??= a.GetType("ModComponent.API.Components.ModRandomWeightedItemComponent", false);
                    stack ??= a.GetType("ModComponent.API.Behaviours.ModStackableBehaviour", false);
                    powder ??= a.GetType("ModComponent.API.Components.ModPowderComponent", false);
                    spawnMgr ??= a.GetType("GearSpawner.SpawnManager", false);
                }
            }
            catch (Exception e) { MelonLogger.Warning("[моды] поиск ModComponent: " + e.Message); return; }

            // GearSpawner: появится ли вещь (ShouldSpawn — sealed override, вызывается виртуально, не встраивается).
            if (prob != null)
            {
                try
                {
                    var m = AccessTools.Method(prob, "ShouldSpawn");
                    _getProb = AccessTools.Method(prob, "GetProbability");
                    var pars = m?.GetParameters();
                    if (m != null && _getProb != null && pars.Length == 3 && m.ReturnType == typeof(bool) && _getProb.ReturnType == typeof(float))
                    {
                        _pos = pars[2].ParameterType.GetProperty("Position");
                        _prefab = pars[2].ParameterType.GetProperty("PrefabName");
                        if (_pos?.PropertyType == typeof(Vector3) && _prefab?.PropertyType == typeof(string))
                        {
                            h.Patch(m, prefix: new HarmonyMethod(typeof(ModSpawnLink), nameof(ShouldSpawnPrefix)));
                            MelonLogger.Msg("[моды] GearSpawner: вещи модов на земле в общем мире решает зерно мира");
                        }
                    }
                }
                catch (Exception e) { MelonLogger.Warning("[моды] патч GearSpawner: " + e.Message); }
            }

            // Заглушки «случайная вещь из списка»: в общем мире оставляем в списке одну вещь, выбранную от зерна,
            // и дальше мод делает всё сам (при одном варианте его генератор не нужен).
            if (rnd != null)
            {
                try
                {
                    var m = AccessTools.Method(rnd, "Update");
                    _names = AccessTools.Field(rnd, "ItemNames");
                    if (m != null && _names?.FieldType == typeof(string[]))
                    {
                        h.Patch(m, prefix: new HarmonyMethod(typeof(ModSpawnLink), nameof(RandomItemPrefix)));
                        MelonLogger.Msg("[моды] ModRandomItemComponent: выбор вещи в общем мире решает зерно мира");
                    }
                }
                catch (Exception e) { MelonLogger.Warning("[моды] патч ModRandomItemComponent: " + e.Message); }
            }
            if (wrnd != null)
            {
                try
                {
                    var m = AccessTools.Method(wrnd, "Update");
                    _wNames = AccessTools.Field(wrnd, "ItemNames");
                    _wWeights = AccessTools.Field(wrnd, "ItemWeights");
                    if (m != null && _wNames?.FieldType == typeof(string[]) && _wWeights?.FieldType == typeof(int[]))
                    {
                        h.Patch(m, prefix: new HarmonyMethod(typeof(ModSpawnLink), nameof(WeightedItemPrefix)));
                        MelonLogger.Msg("[моды] ModRandomWeightedItemComponent: выбор вещи в общем мире решает зерно мира");
                    }
                }
                catch (Exception e) { MelonLogger.Warning("[моды] патч ModRandomWeightedItemComponent: " + e.Message); }
            }

            // Содержимое вещей GearSpawner: мод бросает его в Awake криптогенератором. Окно — от OnStartSpawning
            // до OnFinishSpawning (тот же кадр); вне него (контейнеры, крафт, чужие вещи) всё как в моде.
            if (spawnMgr != null)
            {
                try
                {
                    var start = spawnMgr.GetEvent("OnStartSpawning");
                    var finish = spawnMgr.GetEvent("OnFinishSpawning");
                    if (start != null && finish != null)
                    {
                        const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Static;
                        start.AddEventHandler(null, Delegate.CreateDelegate(start.EventHandlerType, typeof(ModSpawnLink).GetMethod(nameof(StartSpawning), F)));
                        finish.AddEventHandler(null, Delegate.CreateDelegate(finish.EventHandlerType, typeof(ModSpawnLink).GetMethod(nameof(FinishSpawning), F)));
                        if (stack != null)
                        {
                            var m = AccessTools.Method(stack, "Awake");
                            _stackChance = AccessTools.Field(stack, "ChanceFull");
                            if (m != null && _stackChance?.FieldType == typeof(float))
                                h.Patch(m, postfix: new HarmonyMethod(typeof(ModSpawnLink), nameof(StackAwakePostfix)));
                        }
                        if (powder != null)
                        {
                            var m = AccessTools.Method(powder, "Awake");
                            _powderChance = AccessTools.Field(powder, "ChanceFull");
                            if (m != null && _powderChance?.FieldType == typeof(float))
                                h.Patch(m, postfix: new HarmonyMethod(typeof(ModSpawnLink), nameof(PowderAwakePostfix)));
                        }
                        MelonLogger.Msg("[моды] GearSpawner: содержимое вещей модов на земле в общем мире решает зерно мира");
                    }
                }
                catch (Exception e) { MelonLogger.Warning("[моды] содержимое вещей GearSpawner: " + e.Message); }
            }
        }

        private static void StartSpawning(string scene)
        {
            _spawnFrame = WorldSeed() != 0 ? Time.frameCount : -1;
        }

        private static bool Spawning(out int world)
        {
            world = 0;
            if (_spawnFrame != Time.frameCount) return false;
            world = WorldSeed();
            return world != 0;
        }

        // Как RollChance мода: процент 0..100.
        private static bool Full(uint h, float chance) => chance >= 100f || chance > 0f && h % 10000 < chance * 100f;

        private static float Frac(uint h) => ((h >> 8) % 10000) / 10000f;

        // Как ModStackableBehaviour.Awake: полная коробка с шансом ChanceFull, иначе Range(1, m_DefaultUnitsInItem).
        private static void StackAwakePostfix(object __instance)
        {
            if (!Spawning(out int world)) return;
            try
            {
                if (!(__instance is Component c) || c == null) return;
                var gi = c.GetComponent<GearItem>();
                var st = c.GetComponent<StackableItem>();
                if (gi == null || st == null || gi.m_BeenInspected || st.m_DefaultUnitsInItem <= 1) return;
                float chance = (float)_stackChance.GetValue(__instance);
                uint h = Hash(world, "units|" + ItemSync.PrefabName(gi), c.transform.position);
                st.m_Units = Full(h, chance) ? st.m_DefaultUnitsInItem : 1 + (int)((h >> 8) % (uint)(st.m_DefaultUnitsInItem - 1));
            }
            catch (Exception e) { Warn("ModStackableBehaviour", e); }
        }

        // Как ModPowderComponent.Awake: при ChanceFull < 100 неполная — m_WeightLimit * Range(0.125, 1);
        // полная — m_WeightLimit (столько ставит в префаб сам мод).
        private static void PowderAwakePostfix(object __instance)
        {
            if (!Spawning(out int world)) return;
            try
            {
                if (!(__instance is Component c) || c == null) return;
                float chance = (float)_powderChance.GetValue(__instance);
                if (chance == 100f) return;
                var gi = c.GetComponent<GearItem>();
                var pw = c.GetComponent<PowderItem>();
                if (gi == null || pw == null || gi.m_BeenInspected) return;
                uint h = Hash(world, "powder|" + ItemSync.PrefabName(gi), c.transform.position);
                pw.m_Weight = Full(h, chance) ? pw.m_WeightLimit : new ItemWeight((long)(pw.m_WeightLimit.m_Units * (0.125f + 0.875f * Frac(h))));
            }
            catch (Exception e) { Warn("ModPowderComponent", e); }
        }

        // Как постфикс мода на LiquidItem.Awake (питьевая вода без минимума — случайно от 1/8 до полной). Условие по
        // имени (бутылки GEAR_Water500ml/1000ml) у вещей GearSpawner всегда проходит: в Awake их имя ещё «…(Clone)».
        private static void FinishSpawning(IReadOnlyList<GearItem> items)
        {
            try
            {
                if (!Spawning(out int world) || items == null) return;
                var potable = LiquidType.GetPotableWater();
                foreach (var gi in items)
                {
                    try
                    {
                        var li = gi != null ? gi.GetComponent<LiquidItem>() : null;
                        if (li == null || potable == null || li.m_Minimum.m_Units != 0 || li.m_LiquidType == null || li.m_LiquidType.Pointer != potable.Pointer) continue;
                        float cap = li.GetCapacityLitres().m_Units;
                        uint h = Hash(world, "liquid|" + ItemSync.PrefabName(gi), gi.transform.position);
                        li.m_Liquid = ItemLiquidVolume.FromLiters(cap / 8f + (cap - cap / 8f) * Frac(h));
                    }
                    catch (Exception e) { Warn("LiquidItem", e); }
                }
            }
            catch (Exception e) { Warn("GearSpawner", e); }
            finally { _spawnFrame = -1; }
        }

        // Зерно мира (одно у хоста и у друзей); 0 — не в общем мире, всё как в моде.
        private static int WorldSeed()
        {
            if (Session.Mode == Mode.Off || !Session.InSharedWorld) return 0;
            try { return GameManager.m_SceneTransitionData?.m_GameRandomSeed ?? 0; } catch { return 0; }
        }

        private static uint Mix(uint h, int v)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++) { h ^= (byte)(v >> (i * 8)); h *= 16777619; }
                return h;
            }
        }

        // Локация (ключ сохранения: у одинаковых домиков он разный), что бросаем и место с точностью до 10 см.
        private static uint Hash(int world, string what, Vector3 p)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in ItemSync.SceneKey().ToLowerInvariant() + "|" + what) { h ^= ch; h *= 16777619; }
                h = Mix(h, Mathf.RoundToInt(p.x * 10f));
                h = Mix(h, Mathf.RoundToInt(p.y * 10f));
                h = Mix(h, Mathf.RoundToInt(p.z * 10f));
                h = Mix(h, world);
                h ^= h >> 16; h *= 0x85ebca6b; h ^= h >> 13; h *= 0xc2b2ae35; h ^= h >> 16;
                return h;
            }
        }

        private static void Warn(string what, Exception e)
        {
            if (_warned) return;
            _warned = true;
            MelonLogger.Warning($"[моды] {what}: {e.Message} — дальше как в моде");
        }

        // Как GearSpawnHandler.RollChance: целое 0..99 меньше процента.
        private static bool ShouldSpawnPrefix(object __instance, object[] __args, ref bool __result)
        {
            int world = WorldSeed();
            if (world == 0) return true;
            try
            {
                var info = __args[2];
                var p = (Vector3)_pos.GetValue(info);
                string prefab = _prefab.GetValue(info) as string ?? "";
                float percent = (float)_getProb.Invoke(__instance, __args);
                if (percent <= 0f) __result = false;
                else if (percent >= 100f) __result = true;
                else __result = Hash(world, "spawn|" + prefab, p) % 100 < percent;
                return false;
            }
            catch (Exception e) { Warn("GearSpawner", e); return true; }
        }

        private static void RandomItemPrefix(object __instance)
        {
            int world = WorldSeed();
            if (world == 0) return;
            try
            {
                var names = _names.GetValue(__instance) as string[];
                if (names == null || names.Length < 2 || !(__instance is Component c) || c == null) return;
                uint h = Hash(world, "item|" + names.Length + "|" + names[0], c.transform.position);
                _names.SetValue(__instance, new[] { names[h % (uint)names.Length] });
            }
            catch (Exception e) { Warn("ModRandomItemComponent", e); }
        }

        // Как ModRandomWeightedItemComponent.GetIndex: накопленные веса, первый, чья сумма больше броска.
        private static void WeightedItemPrefix(object __instance)
        {
            int world = WorldSeed();
            if (world == 0) return;
            try
            {
                var names = _wNames.GetValue(__instance) as string[];
                var weights = _wWeights.GetValue(__instance) as int[];
                if (names == null || names.Length < 2 || weights == null || weights.Length != names.Length || !(__instance is Component c) || c == null) return;
                int total = 0;
                foreach (int w in weights) total += w;
                if (total <= 0) return;
                uint h = Hash(world, "item|" + names.Length + "|" + names[0], c.transform.position);
                int roll = (int)(h % (uint)total), sum = 0, index = names.Length - 1;
                for (int i = 0; i < weights.Length; i++)
                {
                    sum += weights[i];
                    if (sum > roll) { index = i; break; }
                }
                _wNames.SetValue(__instance, new[] { names[index] });
                _wWeights.SetValue(__instance, new[] { weights[index] });
            }
            catch (Exception e) { Warn("ModRandomWeightedItemComponent", e); }
        }
    }
}
