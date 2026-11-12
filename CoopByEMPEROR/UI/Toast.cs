using UnityEngine;

namespace CoopByEMPEROR.UI
{
    // Короткая надпись вверху экрана (когда игровой HUD недоступен, например в меню).
    internal static class Toast
    {
        private static string _text;
        private static float _until;

        public static void Show(string text)
        {
            _text = text;
            _until = Time.unscaledTime + 3f;
        }

        public static void Draw()
        {
            if (_text == null || Time.unscaledTime > _until) return;
            var r = new Rect(Screen.width / 2f - 200f, 20f, 400f, 26f);
            Theme.Apply();
            Widgets.Backdrop(r);
            Widgets.Centered(r, _text);
        }
    }
}
