using System;
using Il2Cpp;
using UnityEngine;

namespace TLDCrankLamp
{
    // Files made from the game's own WINTERMUTE files are not shipped with the mod: TLDAssets.exe builds them on the
    // player's PC. If they are missing, say so once in game (not only in the log), with what to do.
    internal static class AssetsHint
    {
        static bool _missing, _shown;
        static float _readyAt = -1f;

        internal static void Missing() => _missing = true;

        internal static void Tick()
        {
            if (!_missing || _shown) return;
            bool ready = false;
            try { ready = GameManager.GetPlayerObject() != null && !GameManager.IsMainMenuActive(); } catch { }
            if (!ready) { _readyAt = -1f; return; }
            if (_readyAt < 0f) { _readyAt = Time.unscaledTime + 5f; return; }
            if (Time.unscaledTime < _readyAt) return;
            _shown = true;
            string s = Texts.IsRussian()
                ? "Фонарь с ручкой: нет файлов из игры. Закройте игру и запустите Mods\\TLDAssets.exe"
                : "Crank lamp: game files missing. Close the game and run Mods\\TLDAssets.exe";
            try { HUDMessage.AddMessage(s, 10f, false, true); } catch { }
        }
    }
}
