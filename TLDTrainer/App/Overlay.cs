using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace TLDTrainer.App
{
    // Трейнер поверх игры: по клавише (Insert) окно появляется над игрой и прячется обратно.
    // Игра при этом не сворачивается и продолжает идти.
    internal sealed class Overlay
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int cmd);

        private const int SW_SHOWNOACTIVATE = 4;

        private readonly Window _window;
        private readonly Ctx _ctx;
        private readonly MiniMap _mini;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
        private bool _overlayDown, _miniDown, _wasPlaying;
        private Rect _normal;
        private DateTime _nextGoneCheck;

        public bool Active { get; private set; }
        public event Action ModeChanged;

        public Overlay(Window window, Ctx ctx)
        {
            _window = window;
            _ctx = ctx;
            _mini = new MiniMap(ctx);
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
            _mini.Start();
        }

        public static Key ParseKey(string name, Key fallback) =>
            !string.IsNullOrEmpty(name) && Enum.TryParse<Key>(name, out var k) ? k : fallback;

        public Key OverlayKey => ParseKey(_ctx.Settings.OverlayKey, Key.Insert);
        public Key MiniMapKey => ParseKey(_ctx.Settings.MiniMapKey, Key.Home);
        public bool MiniMapOn => _mini.Enabled;
        public void SetMiniMap(bool on) => _mini.Enabled = on;

        private void Tick()
        {
            bool gameFocused = Native.GameFocused();
            bool trainerFocused = Native.TrainerFocused();
            bool gameUp = Native.GameWindow() != IntPtr.Zero;

            // Клавиши ловим только когда активна игра или сам трейнер, чтобы не мешать другим программам.
            bool overlayDown = Native.KeyDown(KeyInterop.VirtualKeyFromKey(OverlayKey));
            if (overlayDown && !_overlayDown && (gameFocused || trainerFocused) && gameUp)
                ToggleOverlay(gameFocused);
            _overlayDown = overlayDown;

            bool miniDown = Native.KeyDown(KeyInterop.VirtualKeyFromKey(MiniMapKey));
            if (miniDown && !_miniDown && gameFocused)
                ToggleMini(true);
            _miniDown = miniDown;

            // Те же клавиши ловит и мод внутри игры: Windows не показывает трейнеру нажатия,
            // если игра запущена с правами администратора, а трейнер — без них.
            if (gameUp && _ctx.Connected) PollGameKeys();

            // Игра сворачивается, когда теряет фокус: пока трейнер над ней, разворачиваем её обратно, не отдавая ей фокус.
            var game = Native.GameWindow();
            if (gameUp && trainerFocused && Native.IsIconic(game))
            {
                ShowWindow(game, SW_SHOWNOACTIVATE);
                if (_window.IsVisible)
                {
                    _window.Topmost = false;
                    _window.Topmost = true;
                }
            }

            // Игру запустили: окно трейнера прячется, открывается клавишей поверх игры.
            if (gameUp && gameFocused && !_wasPlaying && _window.IsVisible && !Active && (_ctx.Settings.HideWhenPlaying ?? true))
            {
                _normal = NormalBounds();
                Active = true;
                _window.Hide();
                ModeChanged?.Invoke();
            }
            if (gameUp && gameFocused) _wasPlaying = true;

            // Игру закрыли: возвращаем обычное окно. Проверяем раз в секунду и без условия «играли»,
            // чтобы спрятанный трейнер никогда не оставался висеть в фоне без окна.
            if (!gameUp && Active && !_window.IsVisible && DateTime.UtcNow >= _nextGoneCheck)
            {
                _nextGoneCheck = DateTime.UtcNow.AddSeconds(1);
                if (!GameInstaller.IsGameRunning())
                {
                    _wasPlaying = false;
                    Restore();
                }
            }
            else if (!gameUp && _wasPlaying && Active && _window.IsVisible && !GameInstaller.IsGameRunning())
            {
                _wasPlaying = false;
                Restore();
            }
            else if (!gameUp && _wasPlaying && !Active) _wasPlaying = false;
        }

        private DateTime _nextPoll;
        private bool _polling;
        private int _gameOverlay = -1, _gameMini = -1, _overlayEcho, _miniEcho;

        // Нажатие, замеченное через Windows. Если игра была активна, мод в игре увидит то же нажатие —
        // это эхо, его не считаем второй раз.
        private void ToggleOverlay(bool gameSawIt)
        {
            if (gameSawIt) _overlayEcho++;
            DoToggleOverlay();
        }

        private void ToggleMini(bool gameSawIt)
        {
            if (gameSawIt) _miniEcho++;
            _mini.Enabled = !_mini.Enabled;
        }

        private void DoToggleOverlay()
        {
            if (Active && _window.IsVisible) Close();
            else Open();
        }

        private async void PollGameKeys()
        {
            if (_polling || DateTime.UtcNow < _nextPoll) return;
            _polling = true;
            _nextPoll = DateTime.UtcNow.AddMilliseconds(100);
            try
            {
                int ovk = KeyInterop.VirtualKeyFromKey(OverlayKey), mvk = KeyInterop.VirtualKeyFromKey(MiniMapKey);
                var r = await System.Threading.Tasks.Task.Run(() => _ctx.Client.Send(new { cmd = "overlaykeys", overlay = ovk, mini = mvk }));
                if (!r.TryGetProperty("overlay", out var o) || !r.TryGetProperty("mini", out var m)) return;
                int ov = o.GetInt32(), mi = m.GetInt32();
                bool first = _gameOverlay < 0;
                if (first)
                {
                    _overlayEcho = _miniEcho = 0;
                }
                else
                {
                    for (int i = _gameOverlay; i < ov; i++)
                    {
                        if (_overlayEcho > 0) _overlayEcho--;
                        else if (Native.GameWindow() != IntPtr.Zero) DoToggleOverlay();
                    }
                    for (int i = _gameMini; i < mi; i++)
                    {
                        if (_miniEcho > 0) _miniEcho--;
                        else _mini.Enabled = !_mini.Enabled;
                    }
                }
                _gameOverlay = ov;
                _gameMini = mi;
            }
            catch
            {
                // мод не ответил — остаётся обычная проверка клавиш
            }
            finally
            {
                _polling = false;
            }
        }

        public void Open()
        {
            if (!Native.GameRect(out var r)) return;
            if (!Active) _normal = NormalBounds();
            Active = true;
            _window.WindowStyle = WindowStyle.None;
            // Размер можно менять, потянув за край или уголок справа внизу.
            _window.ResizeMode = ResizeMode.CanResizeWithGrip;
            _window.Topmost = true;
            _window.ShowInTaskbar = true;
            _window.Show();
            var hwnd = new WindowInteropHelper(_window).Handle;
            int gw = r.Right - r.Left, gh = r.Bottom - r.Top;
            double dpi = System.Windows.Media.VisualTreeHelper.GetDpi(_window).DpiScaleX;
            int w = Math.Min((int)(1180 * dpi), (int)(gw * 0.9)), h = Math.Min((int)(820 * dpi), (int)(gh * 0.9));
            if (_ctx.Settings.OverlayW is int sw && _ctx.Settings.OverlayH is int sh && sw >= 400 && sh >= 300)
            {
                w = sw;
                h = sh;
            }
            int x = r.Left + (gw - w) / 2, y = r.Top + (gh - h) / 2;
            if (_ctx.Settings.OverlayX is int sx && _ctx.Settings.OverlayY is int sy && OnScreen(sx, sy, w, h))
            {
                x = sx;
                y = sy;
            }
            _opening = true;
            Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, w, h, Native.SWP_SHOWWINDOW);
            _opening = false;
            Native.ForceForeground(hwnd);
            _window.Activate();
            _mini.Editing = true;
            ModeChanged?.Invoke();
        }

        // Окно перетащили: запоминаем место (в том числе на другом мониторе).
        public void RememberPosition()
        {
            var hwnd = new WindowInteropHelper(_window).Handle;
            if (!Native.GetWindowRect(hwnd, out var wr)) return;
            _ctx.Settings.OverlayX = wr.Left;
            _ctx.Settings.OverlayY = wr.Top;
            _ctx.Settings.Save();
        }

        // Окно растянули или сжали: запоминаем размер (с задержкой, чтобы не писать файл на каждом шаге).
        private DispatcherTimer _sizeSave;

        public void SizeChanged()
        {
            if (!Active || !_window.IsVisible || _opening) return;
            _sizeSave ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _sizeSave.Stop();
            _sizeSave.Tick -= SaveSize;
            _sizeSave.Tick += SaveSize;
            _sizeSave.Start();
        }

        private void SaveSize(object sender, EventArgs e)
        {
            _sizeSave.Stop();
            var hwnd = new WindowInteropHelper(_window).Handle;
            if (!Native.GetWindowRect(hwnd, out var wr)) return;
            _ctx.Settings.OverlayW = wr.Right - wr.Left;
            _ctx.Settings.OverlayH = wr.Bottom - wr.Top;
            _ctx.Settings.OverlayX = wr.Left;
            _ctx.Settings.OverlayY = wr.Top;
            _ctx.Settings.Save();
        }

        private bool _opening;

        // Двойной щелчок по верхней полосе: снова по центру игры и обычного размера.
        public void ResetPosition()
        {
            _ctx.Settings.OverlayX = _ctx.Settings.OverlayY = null;
            _ctx.Settings.OverlayW = _ctx.Settings.OverlayH = null;
            _ctx.Settings.Save();
            if (Active && _window.IsVisible) Open();
        }

        // Сохранённое место ещё на экране (монитор могли отключить)?
        public static bool OnScreen(int x, int y, int w, int h)
        {
            int vx = Native.GetSystemMetrics(76), vy = Native.GetSystemMetrics(77);
            int vw = Native.GetSystemMetrics(78), vh = Native.GetSystemMetrics(79);
            return x + w / 2 > vx && x + w / 2 < vx + vw && y >= vy - 10 && y + 40 < vy + vh;
        }

        // Спрятать трейнер и вернуть фокус игре.
        public void Close()
        {
            _mini.Editing = false;
            _window.Hide();
            _window.Topmost = false;
            var game = Native.GameWindow();
            if (game != IntPtr.Zero) Native.SetForegroundWindow(game);
            ModeChanged?.Invoke();
        }

        // Размер и место обычного окна; у свёрнутого окна Left/Top = -32000, берём RestoreBounds.
        private Rect NormalBounds()
        {
            var b = _window.WindowState == WindowState.Normal
                ? new Rect(_window.Left, _window.Top, _window.Width, _window.Height)
                : _window.RestoreBounds;
            return b.IsEmpty || double.IsNaN(b.Left) ? Rect.Empty : b;
        }

        // Обычное окно, как без игры.
        public void Restore()
        {
            Active = false;
            _mini.Editing = false;
            _window.Topmost = false;
            _window.WindowStyle = WindowStyle.SingleBorderWindow;
            _window.ResizeMode = ResizeMode.CanResize;
            _window.ShowInTaskbar = true;
            _window.WindowState = WindowState.Normal;
            var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            if (!_normal.IsEmpty && _normal.Width > 200 && _normal.Height > 200 && screen.IntersectsWith(_normal)
                && _normal.Left > screen.Left - 100 && _normal.Top > screen.Top - 50)
            {
                _window.Left = _normal.Left;
                _window.Top = _normal.Top;
                _window.Width = _normal.Width;
                _window.Height = _normal.Height;
            }
            else
            {
                _window.Width = Math.Min(1180, SystemParameters.WorkArea.Width);
                _window.Height = Math.Min(820, SystemParameters.WorkArea.Height);
                _window.Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - _window.Width) / 2;
                _window.Top = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height - _window.Height) / 2;
            }
            _window.Show();
            _window.Activate();
            ModeChanged?.Invoke();
        }
    }
}
