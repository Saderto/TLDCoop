using UnityEngine;

namespace TrainerByEMPEROR
{
    // Клавиши «трейнер поверх игры» и «мини-карта», пойманные внутри игры.
    // Нужны, когда трейнер через Windows нажатий не видит (например, игра запущена от администратора).
    // Трейнер присылает коды клавиш Windows и забирает счётчики нажатий.
    internal static class OverlayKeys
    {
        private static KeyCode _overlay = KeyCode.Insert, _mini = KeyCode.Home;
        public static int OverlayPresses, MiniPresses;
        private static float _nextLaunch;

        public static void Set(int overlayVk, int miniVk)
        {
            _overlay = FromVk(overlayVk, KeyCode.Insert);
            _mini = FromVk(miniVk, KeyCode.Home);
        }

        public static void Tick()
        {
            if (!Application.isFocused) return;
            if (_overlay != KeyCode.None && Input.GetKeyDown(_overlay))
            {
                OverlayPresses++;
                // Окно трейнера не запущено — запускаем его, и оно сразу откроется поверх игры.
                if (UnityEngine.Time.unscaledTime > _nextLaunch && !TrainerMod.AppRunning())
                {
                    _nextLaunch = UnityEngine.Time.unscaledTime + 10f;
                    TrainerMod.StartApp(open: true);
                }
            }
            if (_mini != KeyCode.None && Input.GetKeyDown(_mini)) MiniPresses++;
        }

        private static KeyCode FromVk(int vk, KeyCode fallback)
        {
            if (vk >= 65 && vk <= 90) return KeyCode.A + (vk - 65);
            if (vk >= 48 && vk <= 57) return KeyCode.Alpha0 + (vk - 48);
            if (vk >= 96 && vk <= 105) return KeyCode.Keypad0 + (vk - 96);
            if (vk >= 112 && vk <= 126) return KeyCode.F1 + (vk - 112);
            return vk switch
            {
                45 => KeyCode.Insert,
                46 => KeyCode.Delete,
                36 => KeyCode.Home,
                35 => KeyCode.End,
                33 => KeyCode.PageUp,
                34 => KeyCode.PageDown,
                19 => KeyCode.Pause,
                145 => KeyCode.ScrollLock,
                9 => KeyCode.Tab,
                192 => KeyCode.BackQuote,
                106 => KeyCode.KeypadMultiply,
                107 => KeyCode.KeypadPlus,
                109 => KeyCode.KeypadMinus,
                110 => KeyCode.KeypadPeriod,
                111 => KeyCode.KeypadDivide,
                _ => fallback,
            };
        }
    }
}
