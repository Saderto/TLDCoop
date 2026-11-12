using System.Windows.Input;

namespace TrainerByEMPEROR.App
{
    // Перевод клавиш WPF в имена клавиш Unity (KeyCode), которые понимает модуль в игре.
    internal static class KeyNames
    {
        public static string ToUnity(Key k)
        {
            if (k >= Key.A && k <= Key.Z) return k.ToString();
            if (k >= Key.D0 && k <= Key.D9) return "Alpha" + (k - Key.D0);
            if (k >= Key.NumPad0 && k <= Key.NumPad9) return "Keypad" + (k - Key.NumPad0);
            if (k >= Key.F1 && k <= Key.F15) return k.ToString();
            return k switch
            {
                Key.Add => "KeypadPlus",
                Key.Subtract => "KeypadMinus",
                Key.Multiply => "KeypadMultiply",
                Key.Divide => "KeypadDivide",
                Key.Decimal => "KeypadPeriod",
                Key.Insert => "Insert",
                Key.Delete => "Delete",
                Key.Home => "Home",
                Key.End => "End",
                Key.PageUp => "PageUp",
                Key.PageDown => "PageDown",
                Key.Up => "UpArrow",
                Key.Down => "DownArrow",
                Key.Left => "LeftArrow",
                Key.Right => "RightArrow",
                Key.Space => "Space",
                Key.Tab => "Tab",
                Key.Return => "Return",
                Key.OemMinus => "Minus",
                Key.OemPlus => "Equals",
                Key.OemComma => "Comma",
                Key.OemPeriod => "Period",
                Key.OemQuestion => "Slash",
                Key.OemSemicolon => "Semicolon",
                Key.OemQuotes => "Quote",
                Key.OemOpenBrackets => "LeftBracket",
                Key.OemCloseBrackets => "RightBracket",
                Key.OemPipe => "Backslash",
                Key.OemTilde => "BackQuote",
                _ => null,
            };
        }

        public static bool IsModifier(Key k) =>
            k is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;
    }
}
