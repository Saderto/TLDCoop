using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Что видят друзья в списке Steam под ником, пока идёт кооп: «Кооп: ведёт игру, игроков 2» или
    // «Кооп: играет у <имя хоста>». Игра сама время от времени ставит туда свой текст (ChangePresence) —
    // пока кооп включён, её текст не ставим, а свой обновляем раз в 15 секунд, если он поменялся.
    public static class SteamStatus
    {
        private static string _shown = "";
        private static float _next;
        private static bool _ours;
        private static string _displayKey, _token, _variable;

        private static void Keys()
        {
            if (_displayKey != null) return;
            _displayKey = "steam_display"; _token = "#Status"; _variable = "status_value";
            try
            {
                string a = SteamRichPresenceProvider.SteamDisplayKeyName, b = SteamRichPresenceProvider.StatusKeyName, c = SteamRichPresenceProvider.StatusVariableName;
                if (!string.IsNullOrEmpty(a)) _displayKey = a;
                if (!string.IsNullOrEmpty(b)) _token = b.StartsWith("#") ? b : "#" + b;
                if (!string.IsNullOrEmpty(c)) _variable = c;
            }
            catch { }
            MelonLogger.Msg($"[steam] строка в списке друзей: {_displayKey}={_token}, текст в {_variable}");
        }

        public static bool CoopActive => Session.Mode != Mode.Off && Steam.Ready;

        private static string Text()
        {
            int n = 0;
            foreach (var p in Session.Players.Values) if (Session.Mode == Mode.Host || p.LastState > Time.unscaledTime - 10f || p.Id == Session.LocalId) n++;
            n = Math.Max(1, n);
            string where = GameApi.InGame() ? "" : " (в меню)";
            if (Session.Mode == Mode.Host) return n > 1 ? $"Кооп: ведёт игру, игроков {n}{where}" : $"Кооп: ждёт друзей{where}";
            string host = Session.Players.TryGetValue(0, out var h) && !string.IsNullOrEmpty(h.Name) ? h.Name : "друга";
            return $"Кооп: играет у {host}{where}";
        }

        public static void Tick(float now)
        {
            if (now < _next) return;
            _next = now + 15f;
            try
            {
                if (!CoopActive) { Clear(); return; }
                Keys();
                string t = Text();
                if (t == _shown && _ours) return;
                _shown = t;
                _ours = true;
                Steam.SetDisplay(t, _displayKey, _token, _variable);
            }
            catch (Exception e) { MelonLogger.Warning("[steam] статус: " + e.Message); _next = now + 120f; }
        }

        // Кооп выключили: вернуть строку игре.
        public static void Clear()
        {
            if (!_ours) return;
            _ours = false;
            _shown = "";
            try
            {
                Keys();
                Steam.SetDisplay("", _displayKey, _token, _variable);
                _clearing = true;
                try { Provider?.ChangePresence(); }   // игра ставит свой текст обратно
                finally { _clearing = false; }
            }
            catch { }
        }

        public static void Refresh() => _next = 0f;
        internal static SteamRichPresenceProvider Provider;
        private static bool _clearing;
        public static bool GameMaySet => _clearing || !CoopActive;
    }

    [HarmonyPatch(typeof(SteamRichPresenceProvider), nameof(SteamRichPresenceProvider.ChangePresence))]
    internal static class Patch_SteamPresence
    {
        private static bool Prefix(SteamRichPresenceProvider __instance)
        {
            SteamStatus.Provider = __instance;
            if (SteamStatus.GameMaySet) return true;
            SteamStatus.Refresh();
            return false;
        }
    }
}
