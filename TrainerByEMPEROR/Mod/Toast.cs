using UnityEngine;

namespace TrainerByEMPEROR
{
    // Короткая надпись вверху экрана, когда функция переключена горячей клавишей.
    internal static class Toast
    {
        private static string _text;
        private static float _until;

        public static void Show(string text)
        {
            _text = text;
            _until = Time.unscaledTime + 2f;
        }

        public static void Draw()
        {
            if (_text == null || Time.unscaledTime > _until) return;
            GUI.Box(new Rect(Screen.width / 2f - 180f, 20f, 360f, 26f), _text);
        }
    }
}
