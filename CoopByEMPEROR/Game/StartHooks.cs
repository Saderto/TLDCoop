using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;

namespace CoopByEMPEROR.Game
{
    // Хост начинает игру через обычное меню («Режим выживания → Новая игра»).
    // Как только игра запускает новую кампанию, мод сообщает друзьям сложность и регион,
    // и они начинают загружаться одновременно с хостом. Сохранение хост тоже грузит через меню,
    // тогда друзья загружаются, когда хост окажется в мире (регион до загрузки неизвестен).
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.LaunchSandbox))]
    internal static class Patch_LaunchSandbox
    {
        private static void Prefix()
        {
            // Новая кампания: память мода о прошлом мире (только данные мода, игре не мешает и вне коопа).
            try { SlotStore.ForgetWorld(); MapShare.Reset(); }
            catch (Exception e) { MelonLogger.Warning("[новая игра] " + e.Message); }
            if (Session.Mode != Mode.Host) return;
            try
            {
                int mode = (int)ExperienceModeManager.GetCurrentExperienceModeType();
                var region = GameManager.m_StartRegion;
                Session.AnnounceNewGame(region != null ? mode : -1, region != null ? region.name : "");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[новая игра] " + e.Message);
            }
        }
    }
}

namespace CoopByEMPEROR.Game
{
    // В кооп-режиме мир не останавливается: ни меню паузы, ни сворачивание игры (Alt+Tab).
    [HarmonyLib.HarmonyPatch(typeof(Il2Cpp.InputManager), nameof(Il2Cpp.InputManager.PauseGame))]
    internal static class Patch_NoPauseInCoop
    {
        private static void Postfix()
        {
            if (Net.Session.Mode != Net.Mode.Off) Il2Cpp.GameManager.m_IsPaused = false;
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Il2Cpp.GameManager), nameof(Il2Cpp.GameManager.PauseWhenFocusLost))]
    internal static class Patch_NoFocusPauseInCoop
    {
        private static bool Prefix(bool focusLost) => !(focusLost && Net.Session.Mode != Net.Mode.Off);
    }

    // Игра вызывает паузу из OnApplicationFocus напрямую, поэтому глушим и сами обработчики.
    [HarmonyLib.HarmonyPatch(typeof(Il2Cpp.GameManager), nameof(Il2Cpp.GameManager.OnApplicationFocus))]
    internal static class Patch_NoFocusPauseInCoop2
    {
        private static bool Prefix(bool focusStatus) => focusStatus || Net.Session.Mode == Net.Mode.Off;
    }

    [HarmonyLib.HarmonyPatch(typeof(Il2Cpp.InputManager), nameof(Il2Cpp.InputManager.OnApplicationFocus))]
    internal static class Patch_NoFocusPauseInCoop3
    {
        private static bool Prefix(bool focusStatus) => focusStatus || Net.Session.Mode == Net.Mode.Off;
    }

    internal static class NoPause
    {
        // Подстраховка каждый кадр: если пауза всё же включилась от потери фокуса — снять.
        public static void Tick()
        {
            if (Net.Session.Mode == Net.Mode.Off) return;
            if (Il2Cpp.GameManager.m_PausedWhenFocusLost || Il2Cpp.GameManager.s_PausedWhenFocusLost)
            {
                Il2Cpp.GameManager.m_PausedWhenFocusLost = false;
                Il2Cpp.GameManager.s_PausedWhenFocusLost = false;
                Il2Cpp.GameManager.m_IsPaused = false;
            }
        }
    }
}
