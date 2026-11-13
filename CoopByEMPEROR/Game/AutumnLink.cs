using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace CoopByEMPEROR.Game
{
    // Связь с AutumnComforts без жёсткой ссылки на его dll (мода может не быть).
    // Мод в GearItem.Awake тыквенной еды берёт материал котелка и настройки головной боли с настоящей вещи:
    // GearItem.InstantiateGearItem("GEAR_UncookedBannock" / "GEAR_CoffeeCup" / "GEAR_CookedPieRoseHip") — и не удаляет её.
    // Эти лепёшки, чашки и пироги оставались лежать в мире, у каждого игрока своё число (Awake идёт только на одной
    // машине: готовка, чужая вещь у нас, показ вещи в руках друга), и раскладки/проверка локации раздавали или
    // удаляли их. В кооп-сессии подменяем этот вызов: мод читает те же поля с префаба, вещь в мире не создаётся.
    // В одиночной игре всё как в моде.
    internal static class AutumnLink
    {
        private static readonly string[] UtilMethods =
        {
            "BurntMaterial", "UncookedPumpkinPieMaterial", "CookedPumpkinPieMaterial", "CookedPumpkinSeedsMaterial",
            "SoupCookedLiquidMaterial", "SoupRawLiquidMaterial", "CoffeeLiquidMaterial",
        };

        private static MethodInfo _helper;
        private static int _swapped;
        private static readonly Dictionary<string, GearItem> Prefabs = new();
        // Запущенные загрузки префабов: ждать их в кадре (WaitForCompletion) нельзя — при загрузке локации
        // такое ожидание может не кончиться никогда. Проверяем готовность при следующих вызовах.
        private static readonly Dictionary<string, UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject>> Loads = new();

        public static void Patch(HarmonyLib.Harmony h)
        {
            Type util;
            try { util = AccessTools.TypeByName("AutumnComforts.Utilities"); }
            catch (Exception e) { MelonLogger.Warning("[моды] поиск AutumnComforts: " + e.Message); return; }
            if (util == null) return;
            _helper = AccessTools.Method(typeof(AutumnLink), nameof(PrefabGear));
            var targets = new List<MethodBase>();
            foreach (var n in UtilMethods)
            {
                var m = AccessTools.Method(util, n, Type.EmptyTypes);
                if (m != null) targets.Add(m);
            }
            try
            {
                var buffs = AccessTools.TypeByName("AutumnComforts.Patches+FoodBuffs");
                var m = buffs != null ? AccessTools.Method(buffs, "Postfix") : null;
                if (m != null) targets.Add(m);
            }
            catch { }
            int ok = 0;
            var tr = new HarmonyMethod(typeof(AutumnLink), nameof(Swap));
            foreach (var m in targets)
            {
                try { h.Patch(m, transpiler: tr); ok++; }
                catch (Exception e) { MelonLogger.Warning($"[моды] AutumnComforts {m.Name}: {e.Message}"); }
            }
            MelonLogger.Msg($"[моды] AutumnComforts: временные вещи для материалов не создаются в кооп-сессии ({ok} методов, вызовов {_swapped})");
            PatchHarvest(h);
        }

        // AutumnComforts 1.0.2: разделка и вырезание тыквы создают вещь в мире (GearItem.InstantiateGearItem) и отдают её
        // в PlayerManager.InstantiateItemInPlayerInventory(GearItem), который кладёт в рюкзак копию. Созданная вещь
        // оставалась лежать в мире (в 1.0.0 мод брал префаб и лишнего не было). Запоминаем вещи, созданные за время
        // OnActionFinished, и после него удаляем те, что не попали в рюкзак. Так и в одиночной игре, и в коопе.
        private static readonly List<GearItem> HarvestTemps = new();
        private static int _harvestDepth, _harvestRemoved;

        private static void PatchHarvest(HarmonyLib.Harmony h)
        {
            MethodInfo m = null;
            try
            {
                var t = AccessTools.TypeByName("AutumnComforts.HarvestButton");
                m = t != null ? AccessTools.Method(t, "OnActionFinished") : null;
            }
            catch { }
            if (m == null) return;
            try
            {
                h.Patch(m, prefix: new HarmonyMethod(typeof(AutumnLink), nameof(HarvestPrefix)),
                    finalizer: new HarmonyMethod(typeof(AutumnLink), nameof(HarvestFinalizer)));
                h.Patch(AccessTools.Method(typeof(GearItem), nameof(GearItem.InstantiateGearItem), new[] { typeof(string) }),
                    postfix: new HarmonyMethod(typeof(AutumnLink), nameof(HarvestCreated)));
                MelonLogger.Msg("[моды] AutumnComforts: лишние вещи после разделки тыквы будут удаляться");
            }
            catch (Exception e) { MelonLogger.Warning("[моды] AutumnComforts, разделка тыквы: " + e.Message); }
        }

        private static void HarvestPrefix() => _harvestDepth++;

        private static void HarvestCreated(GearItem __result)
        {
            if (_harvestDepth > 0 && __result != null) HarvestTemps.Add(__result);
        }

        private static Exception HarvestFinalizer(Exception __exception)
        {
            if (_harvestDepth > 0) _harvestDepth--;
            if (_harvestDepth == 0 && HarvestTemps.Count > 0)
            {
                bool q = ItemSync.Quiet;
                ItemSync.Quiet = true;
                try
                {
                    foreach (var gi in HarvestTemps)
                    {
                        try
                        {
                            if (gi == null || gi.gameObject == null || gi.m_InPlayerInventory || gi.m_InsideContainer) continue;
                            gi.gameObject.SetActive(false);
                            UnityEngine.Object.Destroy(gi.gameObject);
                            if (_harvestRemoved++ < 5) MelonLogger.Msg($"[моды] AutumnComforts: удалил лишнюю вещь после разделки тыквы ({gi.name})");
                        }
                        catch { }
                    }
                }
                finally { ItemSync.Quiet = q; HarvestTemps.Clear(); }
            }
            return __exception;
        }

        private static IEnumerable<CodeInstruction> Swap(IEnumerable<CodeInstruction> code)
        {
            foreach (var ci in code)
            {
                if ((ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt) && ci.operand is MethodInfo m &&
                    m.DeclaringType == typeof(GearItem) && m.Name == nameof(GearItem.InstantiateGearItem))
                {
                    var p = m.GetParameters();
                    if (p.Length == 1 && p[0].ParameterType == typeof(string))
                    {
                        ci.opcode = OpCodes.Call;
                        ci.operand = _helper;
                        _swapped++;
                    }
                }
                yield return ci;
            }
        }

        // Вместо новой вещи в мире — сама вещь-префаб (мод только читает с неё поля и копирует материал).
        // Префаб грузится без ожидания: пока он не готов, идём запасным путём (вещь создаётся и сразу удаляется).
        public static GearItem PrefabGear(string name)
        {
            if (Session.Mode == Mode.Off || string.IsNullOrEmpty(name)) return GearItem.InstantiateGearItem(name);
            try
            {
                if (Prefabs.TryGetValue(name, out var cached) && cached != null) return cached;
                var gi = PollPrefab(name);
                if (gi != null) { Prefabs[name] = gi; return gi; }
            }
            catch (Exception e) { MelonLogger.Warning($"[моды] AutumnComforts, префаб {name}: {e.Message}"); }
            // Запасной путь: настоящая вещь, но сразу выключена и удалена, как в ItemSync.Creatable.
            bool q = ItemSync.Quiet;
            ItemSync.Quiet = true;
            try
            {
                var t = GearItem.InstantiateGearItem(name);
                if (t != null) { t.gameObject.SetActive(false); UnityEngine.Object.Destroy(t.gameObject); }
                return t;
            }
            finally { ItemSync.Quiet = q; }
        }

        // Запускает загрузку префаба или забирает готовый. Ничего не ждёт: не готов — null.
        private static GearItem PollPrefab(string name)
        {
            if (!Loads.TryGetValue(name, out var h))
            {
                var key = new Il2CppSystem.Object(IL2CPP.ManagedStringToIl2Cpp(name));
                h = Addressables.LoadAssetAsync<GameObject>(key);
                Loads[name] = h;
            }
            bool done;
            try { done = h.IsDone; } catch { done = true; }
            if (!done) return null;
            Loads.Remove(name);   // не вышло — при следующем вызове запустим заново, как раньше грузили каждый раз
            GameObject src = null;
            try
            {
                if (h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded) src = h.Result;
            }
            catch { }
            return src != null ? src.GetComponent<GearItem>() : null;
        }
    }
}
