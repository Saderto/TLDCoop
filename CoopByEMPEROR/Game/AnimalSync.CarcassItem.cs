using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Туша-вещь: заяц, которого подняли и положили (GEAR_RabbitCarcass), четверти туши и другие вещи с BodyHarvest.
    // Это не зверь, и рассылка состояния туш (HarvestTick, AnimalHarvest) её не видит: хост снял шкуру с зайца,
    // лежащего на земле, а у друга на этом зайце шкура осталась. Теперь после каждой удачной разделки такой вещи
    // заменяем её у остальных нашей (ItemSync.Resend, под тем же номером), а когда с неё сняли всё и игра её
    // уничтожает (MaybeDestroyDepletedGearItem при закрытии окна разделки) — убираем у всех.
    public static partial class AnimalSync
    {
        private static int _carcassItemsSent, _carcassItemsGone, _carcassItemLogs;

        private static GearItem CarcassItemOf(BodyHarvest bh)
        {
            if (bh == null) return null;
            var gi = bh.GetComponent<GearItem>();
            // Активность не проверяем: пока открыто окно разделки, игра может прятать вещь (в руках её модель).
            if (gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer) return null;
            return gi;
        }

        // Разделали: шлём, когда окно разделки закроется. Пока оно открыто, игра держит вещь у рук (поднятый заяц
        // у хоста был на 1,5 м выше, чем у друга) — такое место ушло бы остальным.
        private static GearItem _carcassItemDirty;
        private static float _carcassItemUntil;

        internal static void OnCarcassItemHarvested(Panel_BodyHarvest panel)
        {
            if (Session.Mode == Mode.Off || panel == null) return;
            try { var gi = CarcassItemOf(panel.m_BodyHarvest); if (gi != null) { _carcassItemDirty = gi; _carcassItemUntil = -1f; } }
            catch (Exception e) { if (_carcassItemLogs++ < 20) MelonLogger.Warning("[звери] туша-вещь: " + e.Message); }
        }

        // Из HarvestTick (раз в секунду): окно разделки закрыто — рассылаем разделанную вещь.
        private static void SendCarcassItem(float now)
        {
            var gi = _carcassItemDirty;
            if (gi == null) { _carcassItemDirty = null; return; }
            if (now - _harvestPanelAt < 0.5f) return;   // окно ещё открыто
            try
            {
                if (_carcassItemUntil < 0f) _carcassItemUntil = now + 5f;
                if (gi.m_InPlayerInventory || gi.m_InsideContainer) { _carcassItemDirty = null; return; }
                if (!gi.gameObject.activeInHierarchy)
                {
                    // Игра ещё не вернула вещь на место после окна: подождём немного.
                    if (now > _carcassItemUntil) _carcassItemDirty = null;
                    return;
                }
                _carcassItemDirty = null;
                ItemSync.Resend(gi, gi.transform.position);
                _carcassItemsSent++;
                if (_carcassItemLogs++ < 20) MelonLogger.Msg($"[звери] туша-вещь {ItemSync.PrefabName(gi)} разделана, разослал");
            }
            catch (Exception e) { _carcassItemDirty = null; if (_carcassItemLogs++ < 20) MelonLogger.Warning("[звери] туша-вещь: " + e.Message); }
        }

        // Перед уничтожением: игра уничтожит вещь при тех же условиях (ресурсов нет или состояние 0 %).
        internal static void OnCarcassItemMaybeGone(Panel_BodyHarvest panel)
        {
            if (Session.Mode == Mode.Off || panel == null) return;
            try
            {
                var bh = panel.m_BodyHarvest;
                var gi = CarcassItemOf(bh);
                if (gi == null || !(bh.NoMoreResources() || bh.ConditionReachedZero())) return;
                string prefab = ItemSync.PrefabName(gi);
                if (_carcassItemDirty != null && _carcassItemDirty.Pointer == gi.Pointer) _carcassItemDirty = null;
                ItemSync.OnLocalPickup(gi, gi.transform.position);
                _carcassItemsGone++;
                if (_carcassItemLogs++ < 20) MelonLogger.Msg($"[звери] туша-вещь {prefab} разделана до конца, убираю у всех");
            }
            catch (Exception e) { if (_carcassItemLogs++ < 20) MelonLogger.Warning("[звери] туша-вещь: " + e.Message); }
        }

        private static string CarcassItemStats => $"туш-вещей разослал после разделки {_carcassItemsSent}, убрал разделанных до конца {_carcassItemsGone}";
    }

    [HarmonyPatch(typeof(Panel_BodyHarvest), nameof(Panel_BodyHarvest.HarvestSuccessful))]
    internal static class Patch_CarcassItemHarvested
    {
        private static void Postfix(Panel_BodyHarvest __instance)
        {
            AnimalSync.OnCarcassItemHarvested(__instance);
            AnimalSync.OnMapCarcassHarvested(__instance);
        }
    }

    [HarmonyPatch(typeof(Panel_BodyHarvest), nameof(Panel_BodyHarvest.MaybeDestroyDepletedGearItem))]
    internal static class Patch_CarcassItemGone
    {
        private static void Prefix(Panel_BodyHarvest __instance) => AnimalSync.OnCarcassItemMaybeGone(__instance);
    }
}
