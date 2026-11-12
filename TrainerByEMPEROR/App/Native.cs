using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TrainerByEMPEROR.App
{
    // Окно игры и клавиши: всё через WinAPI, чтобы показывать трейнер и мини-карту поверх игры.
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X, Y;
        }

        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);

        public static readonly IntPtr HWND_TOPMOST = new(-1);
        public const uint SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040, SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002;
        public const int GWL_EXSTYLE = -20;
        public const long WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;

        private static int _gamePid;
        private static IntPtr _gameWnd;
        private static DateTime _nextLookup;

        // Главное окно игры (tld.exe). Ищем не чаще раза в секунду.
        public static IntPtr GameWindow()
        {
            if (_gameWnd != IntPtr.Zero && IsWindowVisible(_gameWnd)) return _gameWnd;
            if (DateTime.UtcNow < _nextLookup) return IntPtr.Zero;
            _nextLookup = DateTime.UtcNow.AddSeconds(1);
            _gameWnd = IntPtr.Zero;
            foreach (var p in Process.GetProcessesByName("tld"))
            {
                try
                {
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        _gamePid = p.Id;
                        _gameWnd = p.MainWindowHandle;
                        break;
                    }
                }
                catch
                {
                    // процесс уже завершился
                }
                finally
                {
                    p.Dispose();
                }
            }
            return _gameWnd;
        }

        // Какое окно сейчас активно: игра, сам трейнер или что-то другое.
        public static bool GameFocused()
        {
            var game = GameWindow();
            if (game == IntPtr.Zero) return false;
            GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
            return pid == _gamePid;
        }

        public static bool TrainerFocused()
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
            return pid == (uint)Environment.ProcessId;
        }

        // Клиентская часть окна игры в пикселях экрана.
        public static bool GameRect(out RECT r)
        {
            r = default;
            var w = GameWindow();
            if (w == IntPtr.Zero || IsIconic(w) || !GetClientRect(w, out var c)) return false;
            var p = new POINT();
            if (!ClientToScreen(w, ref p)) return false;
            r = new RECT { Left = p.X, Top = p.Y, Right = p.X + c.Right, Bottom = p.Y + c.Bottom };
            return c.Right > 0 && c.Bottom > 0;
        }

        // Windows не даёт фоновой программе забрать фокус; обходим, привязав ввод к активному окну.
        public static void ForceForeground(IntPtr hwnd)
        {
            var fg = GetForegroundWindow();
            uint fgThread = GetWindowThreadProcessId(fg, out _);
            uint me = GetCurrentThreadId();
            bool attached = fgThread != me && AttachThreadInput(me, fgThread, true);
            keybd_event(0x12, 0, 0, UIntPtr.Zero);
            keybd_event(0x12, 0, 2, UIntPtr.Zero);
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            if (attached) AttachThreadInput(me, fgThread, false);
        }

        public static bool KeyDown(int vk) => vk != 0 && (GetAsyncKeyState(vk) & 0x8000) != 0;
    }
}
