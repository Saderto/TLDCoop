using System;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Быстрый старт: без окна мода.
    //  - хост: загрузил свой мир — он сразу открыт друзьям (как кнопка «Перейти в кооп-режим»);
    //  - друг: игра запустилась — сам подключается к тому, у кого играл в прошлый раз, как только тот откроет мир.
    // Включается в окне мода (\), в самом низу, или в UserData/MelonPreferences.cfg.
    public static class QuickStart
    {
        private static bool _hostedThisLoad, _wasInGame, _joinTried;
        private static float _nextTry, _bootAt = -1f;

        public static void Tick()
        {
            float now = Time.unscaledTime;
            if (_bootAt < 0f) _bootAt = now;
            bool inGame = GameApi.InGame() && !GameApi.IsLoading();
            if (!inGame) { _wasInGame = false; _hostedThisLoad = false; }

            // Хост: только что загрузились в свой мир, кооп выключен — открываем.
            if (Config.QuickHost && inGame && !_wasInGame && Session.Mode == Mode.Off && !_hostedThisLoad)
            {
                _hostedThisLoad = true;
                MelonLogger.Msg("[быстрый старт] загрузил мир — открываю его друзьям");
                Session.HostSteam();
                GameApi.HudMessage("Кооп: мир открыт друзьям (быстрый старт)");
            }
            if (inGame) _wasInGame = true;

            // Друг: в главном меню ждём, пока прошлый хост откроет мир, и подключаемся (первые 10 минут после запуска игры).
            if (!Config.QuickJoin || _joinTried || inGame || Session.Mode != Mode.Off || now < _nextTry) return;
            if (now - _bootAt > 600f) { _joinTried = true; return; }
            _nextTry = now + 5f;
            if (!ulong.TryParse(Config.LastHostId, out ulong host) || host == 0) { _joinTried = true; return; }
            if (!Steam.Init()) return;
            foreach (var f in Steam.FriendsInGame(now))
            {
                if (f.Id != host || !f.Presence.StartsWith("host")) continue;
                _joinTried = true;
                MelonLogger.Msg($"[быстрый старт] {f.Name} открыл мир — подключаюсь");
                Session.JoinSteam(f.Id, f.Name);
                return;
            }
        }

        // Подключились к хосту через Steam: запоминаем его для быстрого старта.
        public static void RememberHost(ulong id, string name)
        {
            if (id == 0) return;
            Config.LastHostId = id.ToString();
            Config.LastHostName = name ?? "";
        }
    }
}
