using System;
using HarmonyLib;
using Il2Cpp;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Разделка вдвоём и больше: если рядом с тушей (до 3 м) стоят друзья, которые ничем другим не заняты
    // или тоже разделывают, разделка и четвертование идут быстрее: с одним помощником 60 % времени,
    // с двумя и больше 45 %. Время в окне разделки сразу показывается уже уменьшенным.
    public static class HarvestTogether
    {
        private const float Reach = 3f;
        private static int _lastHelpers = -1;

        public static int Helpers(BodyHarvest bh)
        {
            if (bh == null || Session.Mode == Mode.Off || !Session.InSharedWorld) return 0;
            var at = bh.transform.position;
            string scene = GameApi.Scene();
            float now = Time.unscaledTime;
            int n = 0;
            foreach (var pl in Session.Players.Values)
            {
                if (pl.Id == Session.LocalId || !Session.With(pl, scene) || now - pl.LastState > 5f) continue;
                if (pl.Action != 0 && pl.Action != 3) continue;   // спит, ранен, в рюкзаке… — не помогает
                if ((new Vector3(pl.X, pl.Y, pl.Z) - at).sqrMagnitude <= Reach * Reach) n++;
            }
            return n;
        }

        public static float Factor(int helpers) => helpers <= 0 ? 1f : helpers == 1 ? 0.6f : 0.45f;

        internal static void Apply(Panel_BodyHarvest panel, ref float minutes)
        {
            try
            {
                int h = Helpers(panel?.m_BodyHarvest);
                if (h != _lastHelpers && h > 0) GameApi.HudMessage(h == 1 ? "Друг помогает разделывать — быстрее" : $"Помогают разделывать: {h} — быстрее");
                _lastHelpers = h;
                minutes *= Factor(h);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Panel_BodyHarvest), nameof(Panel_BodyHarvest.GetHarvestDurationMinutes))]
    internal static class Patch_HarvestTogether
    {
        private static void Postfix(Panel_BodyHarvest __instance, ref float __result) => HarvestTogether.Apply(__instance, ref __result);
    }

    // В 2.59 GetQuarterDurationMinutes встроен в Refresh (m_HarvestTimeMinutes = время четвертования туши),
    // а OnQuarter берёт время оттуда. Правим его после Refresh и обновляем подпись с временем.
    [HarmonyPatch(typeof(Panel_BodyHarvest), nameof(Panel_BodyHarvest.Refresh))]
    internal static class Patch_QuarterTogether
    {
        private static void Postfix(Panel_BodyHarvest __instance)
        {
            if (Session.Mode == Mode.Off || !Session.InSharedWorld) return;
            try
            {
                if (__instance == null || __instance.m_BodyHarvest == null || !__instance.IsTabQuarterSelected()) return;
                float m = __instance.GetQuarterDurationMinutes();
                HarvestTogether.Apply(__instance, ref m);
                if (m == __instance.m_HarvestTimeMinutes) return;
                __instance.m_HarvestTimeMinutes = m;
                __instance.RefreshEstimatedTimeLabel();
            }
            catch { }
        }
    }
}
