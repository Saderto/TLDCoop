using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Обычный экран загрузки (кадр из игры и подсказка) держится чуть дольше, пока новая локация не подстроится
    // под остальных: отложенные события применены (ItemSync.Settled), а у друга ещё и пришёл ответ хоста о
    // раскладке вещей (SceneSnapshot) — чтобы не было видно, как свои вещи пропадают и появляются чужие.
    // Не дольше 3 секунд сверх того, сколько экран держала бы сама игра. Только когда в мире есть кто-то ещё.
    // Пока держим, локация уже загружена и для мода считается «в игре» (GameApi.IsLoading → false).
    public static class LoadVeil
    {
        private const float MaxHold = 3f;
        private static string _doneKey = "", _key = "";
        private static float _since = -1f;

        // Игра уже закончила бы загрузку, а мы ещё держим экран.
        public static bool Holding => _since >= 0f;

        // Из Panel_Loading.Update: игра готова убрать экран загрузки — убирать ли.
        internal static bool KeepShowing()
        {
            try
            {
                if (Session.Mode == Mode.Off || !Session.InSharedWorld || Session.Players.Count <= 1 || GameManager.IsMainMenuActive())
                    return Stop(null);
                string key = ItemSync.SceneKey();
                if (string.IsNullOrEmpty(key) || key == _doneKey) return Stop(null);
                float now = Time.unscaledTime;
                if (key != _key || _since < 0f)
                {
                    _key = key;
                    _since = now;
                    SceneSnapshot.AnsweredScene = "";   // ответ о прошлом заходе сюда не считается
                }
                if (Ready(key)) return Stop(key, null);
                if (now - _since > MaxHold) return Stop(key, "не дождался");
                return true;
            }
            catch { return Stop(null); }
        }

        private static bool Ready(string key)
        {
            if (!ItemSync.Settled) return false;
            if (Session.Mode == Mode.Client) return SceneSnapshot.AnsweredScene == key;
            return true;   // хост выравнивается под раскладку друга в тот же момент, как устоялся
        }

        private static bool Stop(string why) { _since = -1f; return false; }

        private static bool Stop(string key, string why)
        {
            float held = _since < 0f ? 0f : Time.unscaledTime - _since;
            _doneKey = key;
            _since = -1f;
            MelonLogger.Msg($"[локация] {key}: экран загрузки держал ещё {held:0.0} с" + (why != null ? $" ({why})" : ""));
            return false;
        }

        // Вышли из локации (или из мира): в следующий раз туда — снова ждём.
        public static void Tick()
        {
            // Экран убрали не мы (выход в меню и т.п.) — не оставляем «держим» навсегда.
            if (Holding) { if (Time.unscaledTime - _since > MaxHold + 2f) _since = -1f; return; }
            try
            {
                if (_doneKey.Length > 0 && GameApi.InGame() && ItemSync.SceneKey() != _doneKey) _doneKey = "";
                if (!GameApi.InGame() && !GameApi.IsLoading()) _doneKey = "";
            }
            catch { }
        }
    }

    // В 2.59 HasFinishedHolding встроен в Panel_Loading.Update (realtimeSinceStartup - m_StartLoadingTimeSeconds >
    // m_MinHoldTime), патч на нём не вызывался. Держим из Update: когда игра уже убрала бы экран, сдвигаем начало загрузки.
    [HarmonyPatch(typeof(Panel_Loading), nameof(Panel_Loading.Update))]
    internal static class Patch_LoadingHold
    {
        private static void Prefix(Panel_Loading __instance)
        {
            if (Session.Mode == Mode.Off || !Session.InSharedWorld || Session.Players.Count <= 1) return;
            try
            {
                // Ждёт нажатия клавиши — держит сама; в главное меню не держим.
                if (__instance == null || __instance.m_HoldScreenAfterLoad || __instance.m_LoadingMainMenu) return;
                float now = Time.realtimeSinceStartup, min = __instance.m_MinHoldTime;
                if (now - __instance.m_StartLoadingTimeSeconds <= min) return;   // игра и так ещё держит
                if (!__instance.HasFinishedLoading()) return;
                if (LoadVeil.KeepShowing()) __instance.m_StartLoadingTimeSeconds = now - min + 0.1f;
            }
            catch { }
        }
    }
}
