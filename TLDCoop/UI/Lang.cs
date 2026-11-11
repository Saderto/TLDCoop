using System;
using UnityEngine;

namespace TLDCoop.UI
{
    // Два языка у окон мода: русский, если игра на русском, иначе английский (0.19.24).
    // Язык берём из настройки игры (Localization.Language) и перечитываем раз в секунду: его можно сменить в меню.
    internal static class L
    {
        public static bool En { get; private set; }
        private static float _next = -1f;

        public static void Refresh()
        {
            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + 1f;
            try
            {
                string lang = Il2Cpp.Localization.Language ?? "";
                En = lang.Length > 0 && !lang.StartsWith("Russian", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
        }

        public static string T(string ru, string en) => En ? en : ru;
    }
}
