using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TLDTrainer.App
{
    // Мини-карта в углу экрана поверх игры. Прозрачна для мыши и не забирает фокус у игры.
    internal sealed class MiniMap : Window
    {
        private const double D = 260;
        public const int MinRadius = 40, MaxRadius = 600, DefaultRadius = 160;
        private double RadiusMeters => Math.Clamp(_ctx.Settings.MiniMapRadius ?? DefaultRadius, MinRadius, MaxRadius);

        // Изменить масштаб (колесо мыши над мини-картой или ползунок в настройках).
        public void Zoom(int steps)
        {
            int r = (int)RadiusMeters;
            for (int i = 0; i < Math.Abs(steps); i++)
                r = steps > 0 ? (int)Math.Round(r / 1.25) : (int)Math.Round(r * 1.25);
            _ctx.Settings.MiniMapRadius = Math.Clamp(r, MinRadius, MaxRadius);
            _ctx.Settings.Save();
            RadiusChanged?.Invoke();
            ShowScale();
        }

        public static event Action RadiusChanged;

        // На пару секунд показываем масштаб внизу круга.
        private DateTime _scaleUntil;

        public void ShowScale()
        {
            _scaleText.Text = $"{(int)RadiusMeters} м";
            _scaleText.Visibility = Visibility.Visible;
            _scaleUntil = DateTime.UtcNow.AddSeconds(2);
        }

        private readonly Ctx _ctx;
        private readonly Canvas _board = new();
        private readonly Canvas _marks = new() { Width = D, Height = D };
        private readonly TranslateTransform _toOrigin = new();
        private readonly ScaleTransform _scale = new();
        private readonly RotateTransform _arrowRotate = new();
        private readonly TextBlock _note = new() { Foreground = Brushes.White, FontSize = 12, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Width = D - 60 };
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };

        private double _a, _b, _c, _d, _e, _f;
        private bool _hasMap, _loading, _posBusy;
        private string _scene;
        private DateTime _nextMarkers;
        private List<(string Type, string Kind, double X, double Z)> _markers = new();
        private double _px, _pz;
        private Point _drawnP;
        private double _drawnK = -1;
        private readonly Ellipse _editFrame;
        private readonly TextBlock _scaleText = new()
        {
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(6, 1, 6, 1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 8),
            Visibility = Visibility.Collapsed,
        };
        private readonly TextBlock _editHint;
        private bool _editing;

        // Режим перемещения: окно ловит мышь (иначе щелчки проходят сквозь него в игру).
        public bool Editing
        {
            get => _editing;
            set
            {
                if (_editing == value) return;
                _editing = value;
                _editFrame.Visibility = _editHint.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                if (value) ShowScale();
                var h = new WindowInteropHelper(this).Handle;
                if (h != IntPtr.Zero)
                {
                    long ex = Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE).ToInt64();
                    if (value) ex &= ~Native.WS_EX_TRANSPARENT;
                    else ex |= Native.WS_EX_TRANSPARENT;
                    Native.SetWindowLongPtr(h, Native.GWL_EXSTYLE, new IntPtr(ex));
                }
                if (value && Enabled) _ = Tick();
            }
        }

        // Своё место (если мини-карту перетаскивали и тот экран на месте) или правый верхний угол игры.
        private void Place(bool force = false)
        {
            if (_editing && !force && _placedInEdit) return;
            if (!Native.GameRect(out var r)) return;
            var hwnd = new WindowInteropHelper(this).Handle;
            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            int px = (int)Math.Round(D * dpi), margin = (int)Math.Round(24 * dpi);
            int x = r.Right - px - margin, y = r.Top + margin;
            if (_ctx.Settings.MiniMapX is int sx && _ctx.Settings.MiniMapY is int sy && Overlay.OnScreen(sx, sy, px, px))
            {
                x = sx;
                y = sy;
            }
            Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, x, y, px, px, Native.SWP_NOACTIVATE);
            _placedInEdit = _editing;
        }

        private bool _placedInEdit;

        public MiniMap(Ctx ctx)
        {
            _ctx = ctx;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            Width = Height = D;
            Title = "TLD Trainer — мини-карта";

            var clip = new EllipseGeometry(new Point(D / 2, D / 2), D / 2 - 3, D / 2 - 3);
            var view = new Grid { Width = D, Height = D, Clip = clip };
            view.Children.Add(new Ellipse { Fill = new SolidColorBrush(Color.FromArgb(215, 18, 20, 26)) });
            var group = new TransformGroup();
            group.Children.Add(_toOrigin);
            group.Children.Add(_scale);
            group.Children.Add(new TranslateTransform(D / 2, D / 2));
            _board.RenderTransform = group;
            _board.HorizontalAlignment = HorizontalAlignment.Left;
            _board.VerticalAlignment = VerticalAlignment.Top;
            var boardHost = new Canvas { Width = D, Height = D };
            boardHost.Children.Add(_board);
            view.Children.Add(boardHost);
            view.Children.Add(_marks);

            var arrow = new Polygon
            {
                Points = new PointCollection { new(0, -11), new(7, 8), new(0, 4), new(-7, 8) },
                Fill = new SolidColorBrush(Color.FromRgb(80, 200, 255)),
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                RenderTransform = _arrowRotate,
            };
            var arrowHost = new Canvas { Width = 0, Height = 0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            arrowHost.Children.Add(arrow);
            view.Children.Add(arrowHost);

            _note.HorizontalAlignment = HorizontalAlignment.Center;
            _note.VerticalAlignment = VerticalAlignment.Center;
            view.Children.Add(_note);

            var root = new Grid();
            root.Children.Add(view);
            root.Children.Add(new Ellipse { Stroke = new SolidColorBrush(Color.FromArgb(220, 230, 232, 238)), StrokeThickness = 3 });
            var north = new TextBlock
            {
                Text = "С",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 5, 0, 0),
            };
            root.Children.Add(north);
            _editFrame = new Ellipse
            {
                Stroke = new SolidColorBrush(Color.FromRgb(255, 200, 80)),
                StrokeThickness = 3,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                Visibility = Visibility.Collapsed,
            };
            root.Children.Add(_editFrame);
            _editHint = new TextBlock
            {
                Text = "Тяните мышью, колесо — масштаб\nДвойной щелчок — на место",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 28),
                Visibility = Visibility.Collapsed,
            };
            root.Children.Add(_editHint);
            root.Children.Add(_scaleText);
            root.Background = Brushes.Transparent;
            Content = root;

            MouseWheel += (_, e) =>
            {
                if (_editing) Zoom(e.Delta > 0 ? 1 : -1);
            };

            // Пока открыт трейнер поверх игры, мини-карту можно перетащить (например, на второй экран).
            MouseLeftButtonDown += (_, e) =>
            {
                if (!_editing) return;
                if (e.ClickCount == 2)
                {
                    _ctx.Settings.MiniMapX = _ctx.Settings.MiniMapY = null;
                    _ctx.Settings.Save();
                    Place(force: true);
                    return;
                }
                try
                {
                    DragMove();
                }
                catch (InvalidOperationException)
                {
                    return;
                }
                if (Native.GetWindowRect(new WindowInteropHelper(this).Handle, out var wr))
                {
                    _ctx.Settings.MiniMapX = wr.Left;
                    _ctx.Settings.MiniMapY = wr.Top;
                    _ctx.Settings.Save();
                }
            };

            SourceInitialized += (_, _) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                long ex = Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE).ToInt64();
                ex |= Native.WS_EX_LAYERED | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW;
                if (!_editing) ex |= Native.WS_EX_TRANSPARENT;
                Native.SetWindowLongPtr(h, Native.GWL_EXSTYLE, new IntPtr(ex));
            };
            _timer.Tick += async (_, _) => await Tick();
        }

        public bool Enabled
        {
            get => _ctx.Settings.MiniMapOn ?? false;
            set
            {
                _ctx.Settings.MiniMapOn = value;
                _ctx.Settings.Save();
                if (value) _timer.Start();
                else
                {
                    _timer.Stop();
                    Hide();
                }
            }
        }

        public void Start()
        {
            if (Enabled) _timer.Start();
        }

        private async Task Tick()
        {
            // Видна, только пока активна игра (не трейнер и не другие окна) и загружено сохранение.
            // Пока открыт трейнер поверх игры, мини-карта тоже видна — чтобы её можно было передвинуть.
            bool show = _ctx.InGame && (Native.GameFocused() || _editing) && Native.GameRect(out _);
            if (!show)
            {
                if (IsVisible) Hide();
                _placedInEdit = false;
                return;
            }
            if (!IsVisible) Show();
            if (!_editing) _placedInEdit = false;
            if (_scaleText.Visibility == Visibility.Visible && !_editing && DateTime.UtcNow > _scaleUntil) _scaleText.Visibility = Visibility.Collapsed;
            Place();
            await UpdatePosition();
        }

        private async Task UpdatePosition()
        {
            if (_posBusy) return;
            _posBusy = true;
            try
            {
                var r = await Task.Run(() => _ctx.Client.Send(new { cmd = "mappos" }));
                string scene = r.GetProperty("scene").GetString();
                _px = r.GetProperty("x").GetDouble();
                _pz = r.GetProperty("z").GetDouble();
                if (scene != _scene && !_loading)
                {
                    _scene = scene;
                    _hasMap = false;
                    _ = LoadMap(scene);
                }
                if (!_hasMap) return;
                double yaw = r.GetProperty("yaw").GetDouble();
                var p = ToBoard(_px, _pz);
                _toOrigin.X = -p.X;
                _toOrigin.Y = -p.Y;
                double k = Scale();
                _scale.ScaleX = _scale.ScaleY = k;
                double rad = yaw * Math.PI / 180;
                double bx = _a * Math.Sin(rad) + _b * Math.Cos(rad), by = _d * Math.Sin(rad) + _e * Math.Cos(rad);
                _arrowRotate.Angle = Math.Atan2(bx, -by) * 180 / Math.PI;
                if (DateTime.UtcNow >= _nextMarkers)
                {
                    await LoadMarkers();
                    _drawnK = -1;
                }
                // Перерисовываем значки, только если игрок сдвинулся, сменился масштаб или пришли новые метки.
                if (k != _drawnK || Math.Abs(p.X - _drawnP.X) * k > 0.5 || Math.Abs(p.Y - _drawnP.Y) * k > 0.5)
                {
                    DrawMarkers(p, k);
                    _drawnP = p;
                    _drawnK = k;
                }
            }
            catch
            {
                // нет связи — следующий опрос попробует снова
            }
            finally
            {
                _posBusy = false;
            }
        }

        // Пикселей мини-карты на единицу картинки: в радиус круга помещается RadiusMeters метров.
        private double Scale()
        {
            double perMeter = Math.Sqrt(Math.Abs(_a * _e - _b * _d));
            return perMeter <= 0 ? 1 : D / 2 / (RadiusMeters * perMeter);
        }

        private Point ToBoard(double x, double z) => new(_a * x + _b * z + _c, _d * x + _e * z + _f);

        private async Task LoadMap(string scene)
        {
            _loading = true;
            _note.Text = "Карта готовится…";
            try
            {
                JsonElement? map = null;
                if (_ctx.Settings.MapGameBackground ?? true)
                {
                    map = await Fetch("game");
                    if (map != null && map.Value.TryGetProperty("unavailable", out var un) && un.ValueKind == JsonValueKind.True) map = null;
                }
                map ??= await Fetch("relief");
                if (map == null || scene != _scene) return;
                _board.Children.Clear();
                var (w, h, m) = MapImage.Fill(_board, map.Value);
                _board.Width = w;
                _board.Height = h;
                (_a, _b, _c, _d, _e, _f) = (m[0], m[1], m[2], m[3], m[4], m[5]);
                _hasMap = true;
                _note.Text = "";
                _nextMarkers = DateTime.MinValue;
            }
            catch
            {
                _scene = null;
            }
            finally
            {
                _loading = false;
            }
        }

        private async Task<JsonElement?> Fetch(string source)
        {
            for (int i = 0; i < 120 && Enabled; i++)
            {
                var r = await Task.Run(() => _ctx.Client.Send(new { cmd = "maprender", force = false, source }));
                if (r.TryGetProperty("ready", out var ready) && ready.ValueKind == JsonValueKind.True) return r;
                if (r.TryGetProperty("progress", out var pr)) _note.Text = $"Карта готовится: {pr.GetInt32()}%";
                await Task.Delay(1000);
            }
            return null;
        }

        private static readonly HashSet<string> Shown = new() { "building", "cave", "cavescene", "exit", "landmark", "animal", "fire", "bed", "foodpack" };

        private async Task LoadMarkers()
        {
            _nextMarkers = DateTime.UtcNow.AddSeconds(5);
            var r = await Task.Run(() => _ctx.Client.Send(new { cmd = "mapmarkers" }));
            _markers = r.GetProperty("markers").EnumerateArray()
                .Select(m => (m.GetProperty("Type").GetString(),
                    m.TryGetProperty("Kind", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null,
                    m.GetProperty("X").GetDouble(), m.GetProperty("Z").GetDouble()))
                .Where(m => Shown.Contains(m.Item1))
                .ToList();
        }

        // Зверь — морда в кружке цвета вида, чтобы с первого взгляда было ясно, кто это.
        private static readonly Dictionary<string, (string Face, Color Rim, string Tag)> Animals = new()
        {
            ["wolf"] = ("🐺", Color.FromRgb(85, 95, 110), null),
            ["bear"] = ("🐻", Color.FromRgb(150, 95, 55), null),
            ["deer"] = ("🦌", Color.FromRgb(215, 170, 110), null),
            ["moose"] = (HasEmoji(0x1FACE) ? "🫎" : "🦌", Color.FromRgb(95, 70, 45), HasEmoji(0x1FACE) ? null : "Л"),
            ["rabbit"] = ("🐰", Color.FromRgb(150, 150, 150), null),
            ["cougar"] = ("🐆", Color.FromRgb(200, 150, 90), null),
            ["bird"] = ("🐔", Color.FromRgb(100, 160, 210), null),
        };

        // Есть ли значок в шрифте эмодзи (лось 🫎 есть только в новых Windows 11).
        private static bool HasEmoji(int codepoint)
        {
            try
            {
                foreach (var tf in new FontFamily("Segoe UI Emoji").GetTypefaces())
                    if (tf.TryGetGlyphTypeface(out var g) && g.CharacterToGlyphMap.ContainsKey(codepoint)) return true;
            }
            catch
            {
                // шрифта нет
            }
            return false;
        }

        private static FrameworkElement AnimalBadge(string kind)
        {
            if (!Animals.TryGetValue(kind ?? "", out var a)) a = ("🐾", Color.FromRgb(255, 138, 61), null);
            // Светлый кружок с тёмной обводкой: на тёмном фоне морды зверей сливались.
            var g = new Grid { Width = 25, Height = 25 };
            g.Children.Add(new Ellipse { Fill = Brushes.Black });
            g.Children.Add(new Ellipse
            {
                Margin = new Thickness(1),
                Fill = new SolidColorBrush(Color.FromRgb(250, 246, 236)),
                Stroke = new SolidColorBrush(a.Rim),
                StrokeThickness = 2.5,
            });
            if (kind == "bird")
            {
                g.Children.Add(Ptarmigan());
                return g;
            }
            g.Children.Add(new TextBlock
            {
                Text = a.Face,
                FontSize = 15,
                FontFamily = new FontFamily("Segoe UI Emoji"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            if (a.Tag != null)
                g.Children.Add(new TextBlock
                {
                    Text = a.Tag,
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, -2, -2),
                });
            return g;
        }

        // Белая куропатка (в эмодзи такой птицы нет): толстое белое тельце, чёрный хвост, красная бровь.
        private static Canvas Ptarmigan()
        {
            var c = new Canvas { Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var white = new SolidColorBrush(Color.FromRgb(245, 245, 245));
            var edge = new SolidColorBrush(Color.FromRgb(150, 150, 155));
            void E(double cx, double cy, double rx, double ry, Brush fill, Brush stroke = null)
            {
                var e = new Ellipse { Width = rx * 2, Height = ry * 2, Fill = fill, Stroke = stroke, StrokeThickness = stroke == null ? 0 : 0.6 };
                Canvas.SetLeft(e, cx - rx);
                Canvas.SetTop(e, cy - ry);
                c.Children.Add(e);
            }
            void P(Brush fill, params double[] xy)
            {
                var pts = new PointCollection();
                for (int i = 0; i < xy.Length; i += 2) pts.Add(new Point(xy[i], xy[i + 1]));
                c.Children.Add(new Polygon { Points = pts, Fill = fill });
            }
            P(new SolidColorBrush(Color.FromRgb(25, 25, 25)), 13.2, 8.2, 15.8, 7.0, 15.6, 11.0, 13.4, 11.2);
            E(8.6, 10.2, 6.0, 4.4, white, edge);
            E(4.6, 5.6, 3.0, 3.0, white, edge);
            P(new SolidColorBrush(Color.FromRgb(40, 40, 40)), 1.8, 5.4, 0.4, 6.2, 1.9, 6.8);
            E(4.3, 4.1, 1.3, 0.55, new SolidColorBrush(Color.FromRgb(220, 40, 40)));
            E(4.0, 5.4, 0.65, 0.65, Brushes.Black);
            return c;
        }

        private void DrawMarkers(Point player, double k)
        {
            _marks.Children.Clear();
            double r2 = (D / 2 - 8) * (D / 2 - 8);
            // Звери поверх значков построек.
            foreach (var m in _markers.OrderBy(m => m.Type == "animal" ? 1 : 0))
            {
                var b = ToBoard(m.X, m.Z);
                double x = (b.X - player.X) * k, y = (b.Y - player.Y) * k;
                if (x * x + y * y > r2) continue;
                FrameworkElement el;
                if (m.Type == "animal")
                    el = AnimalBadge(m.Kind);
                else
                {
                    string icon = MapPage.Icons.TryGetValue(m.Kind ?? m.Type, out var ic) ? ic : (MapPage.Icons.TryGetValue(m.Type, out var ic2) ? ic2 : "•");
                    el = new TextBlock { Text = icon, FontSize = 13, FontFamily = new FontFamily("Segoe UI Emoji") };
                }
                el.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(el, D / 2 + x - el.DesiredSize.Width / 2);
                Canvas.SetTop(el, D / 2 + y - el.DesiredSize.Height / 2);
                _marks.Children.Add(el);
            }
        }
    }
}
