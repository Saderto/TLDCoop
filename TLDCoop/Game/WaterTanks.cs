using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using Il2CppTLD.IntBackedUnit;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Вода в бачках унитазов и других «источниках воды» мира (WaterSource). Раньше у каждого был свой бачок:
    // один выпил — у другого там всё ещё полно. Теперь, когда кто-то набрал или выпил, остальным уходит
    // новое состояние бачка (сколько литров осталось и чистая ли вода) — его же игра пишет в сохранение.
    public static class WaterTanks
    {
        internal static void OnTaken(WaterSource ws)
        {
            if (ws == null || WorldObjects.Applying) return;
            string st;
            try { st = ws.Serialize(); } catch { return; }
            if (string.IsNullOrEmpty(st)) return;
            WorldObjects.Send(WorldObjects.Kind.Water, ws, st);
            try { WorldStates.Note("W", ws, WorldStates.WaterSig(ws)); } catch { }
        }

        internal static Component Apply(string id, string state)
        {
            var ws = WorldObjects.Find<WaterSource>(id);
            if (ws == null || string.IsNullOrEmpty(state)) return ws;
            try
            {
                ws.Deserialize(state);
                WorldStates.Note("W", ws, WorldStates.WaterSig(ws));   // пришедшее не рассылаем обратно
                MelonLogger.Msg($"[объекты] вода {id}: теперь {ws.m_CurrentLiters.m_Units / (float)ItemLiquidVolume.Liter.m_Units:0.##} л");
            }
            catch (Exception e) { MelonLogger.Warning("[объекты] вода: " + e.Message); }
            return ws;
        }
    }

    [HarmonyPatch(typeof(WaterSource), nameof(WaterSource.RemoveLiters))]
    internal static class Patch_WaterTaken
    {
        private static void Postfix(WaterSource __instance)
        {
            try { WaterTanks.OnTaken(__instance); } catch { }
        }
    }

    // В 2.59 RemoveLiters встроен сюда: игра вычитает воду из бачка прямо здесь, а в конце обнуляет m_WaterSource.
    [HarmonyPatch(typeof(Panel_PickWater), nameof(Panel_PickWater.OnTakeWaterComplete))]
    internal static class Patch_WaterTakeComplete
    {
        private static void Prefix(Panel_PickWater __instance, out WaterSource __state)
        {
            __state = null;
            if (Session.Mode == Mode.Off) return;
            try { __state = __instance.m_WaterSource; } catch { }
        }

        private static void Postfix(WaterSource __state)
        {
            if (__state == null) return;
            try { WaterTanks.OnTaken(__state); } catch { }
        }
    }
}
