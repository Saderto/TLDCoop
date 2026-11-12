using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TrainerByEMPEROR.App
{
    // Раздел «Карта»: снимок локации сверху, игрок со стрелкой взгляда, метки и телепорт по щелчку.
    internal sealed class MapPage : Grid
    {
        private sealed record Marker(string Type, string Kind, string Name, float X, float Z, bool Done, string Sprite, bool Game, bool Hidden);

        // Значки угольной карты игры (вырезаны из атласа игры) и цвет, которым игра их рисует.
        private static readonly Dictionary<string, BitmapSource> Sprites = new();
        private static readonly HashSet<string> SpritesAsked = new();
        private static Color _charcoal = Color.FromRgb(43, 39, 36);
        private bool _gameMap;

        private readonly Ctx _ctx;
        private readonly ScrollViewer _view;
        private readonly Grid _board = new();
        private readonly Canvas _background = new();
        private readonly Canvas _markers = new();
        private readonly GameLayer _gameLayer;
        private readonly Canvas _playerLayer = new();
        private readonly ScaleTransform _zoom = new(1, 1);
        private readonly ScaleTransform _inverse = new(1, 1);
        private readonly Polygon _arrow;
        private readonly RotateTransform _arrowRotate = new();
        private readonly TextBlock _status;
        private readonly TextBlock _coords;
        private readonly Dictionary<string, ToggleButton> _filters = new();
        private readonly DispatcherTimer _posTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
        private readonly DispatcherTimer _markerTimer = new() { Interval = TimeSpan.FromSeconds(6) };

        private List<Marker> _markerData = new();
        private double _size, _w, _h;
        // Мир (x, z) → точка на доске: bx = a·x + b·z + c, by = d·x + e·z + f; и обратное.
        private double _a, _b, _c, _d, _e, _f;
        private string _scene;
        private bool _busy, _posBusy, _follow = true;
        private Point _panStart;
        private Vector _panOffset;
        private bool _panning;
        private bool _leftDown;
        private const double DragThreshold = 4;
        private readonly Border _hover = new()
        {
            Background = new SolidColorBrush(Color.FromArgb(235, 20, 22, 28)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 90, 95, 107)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(7, 3, 7, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            Child = new TextBlock { FontSize = 12, Foreground = Brushes.White },
        };
        private Point _lastPlayer;

        private static readonly (string Key, string Label, Brush Color, bool Default)[] Layers =
        {
            ("building", "Здания", Theme.Hex("#E0A43A"), true),
            ("cave", "Пещеры на этой локации", Theme.Hex("#B07CFF"), true),
            ("cavescene", "Входы в пещеры-локации", Theme.Hex("#7C5CFF"), true),
            ("exit", "Переходы в другие локации", Theme.Hex("#FF5C7A"), true),
            ("animal", "Животные", Theme.Hex("#FF8A3D"), true),
            ("carcass", "Туши", Theme.Hex("#D9534F"), true),
            ("container", "Контейнеры", Theme.Hex("#4FA3FF"), true),
            ("loot", "Лут (предметы)", Theme.Hex("#3CC46A"), false),
            ("plant", "Растения", Theme.Hex("#7BD389"), true),
            ("breakdown", "Можно разобрать", Theme.Hex("#A0785A"), false),
            ("fire", "Костры и печи", Theme.Hex("#FF6A3D"), true),
            ("bed", "Кровати и укрытия", Theme.Hex("#9AA5B1"), true),
            ("workbench", "Верстаки и кузницы", Theme.Hex("#C9B458"), true),
            ("fishing", "Лунки для рыбалки", Theme.Hex("#5BC0DE"), true),
            ("rope", "Верёвки", Theme.Hex("#E6E8EE"), true),
            ("cairn", "Каирны", Theme.Hex("#8A8F9C"), true),
            ("landmark", "Ориентиры с карты игры", Theme.Hex("#D6C7A1"), true),
            ("label", "Названия мест с карты игры", Theme.Hex("#C9B79C"), true),
            ("foodpack", "FoodPack: саке, виски и карточки", Theme.Hex("#F27BB0"), true),
        };

        // Фильтры — несколько крупных групп вместо двадцати слоёв.
        // Фильтры. По умолчанию карта как в игре: только её значки и надписи. Остальное — наши дополнительные слои.
        private static readonly (string Key, string Label, Brush Color, bool Default)[] Groups =
        {
            ("gamebuild", "Карта игры: здания, пещеры, помещения", Theme.Hex("#E0A43A"), true),
            ("game", "Карта игры: надписи и ориентиры", Theme.Hex("#C9B79C"), true),
            ("places", "Ещё здания и пещеры (нашёл трейнер)", Theme.Hex("#E0A43A"), false),
            ("exit", "Переходы в другие локации", Theme.Hex("#FF5C7A"), false),
            ("animal", "Животные", Theme.Hex("#FF8A3D"), false),
            ("camp", "Костры, кровати, верстаки, верёвки", Theme.Hex("#FF6A3D"), false),
            ("foodpack", "Саке, виски и карточки FoodPack", Theme.Hex("#F27BB0"), false),
            ("loot", "Добыча: контейнеры, туши, растения, лут", Theme.Hex("#4FA3FF"), false),
        };

        private static string GroupOf(Marker m) => m.Game ? (m.Hidden ? "gamehidden" : m.Type is "building" or "cave" or "cavescene" ? "gamebuild" : "game") : m.Type switch
        {
            "building" or "cave" or "cavescene" or "landmark" or "label" => "places",
            "exit" => "exit",
            "animal" => "animal",
            "fire" or "bed" or "workbench" or "fishing" or "rope" or "cairn" => "camp",
            "foodpack" => "foodpack",
            _ => "loot",
        };

        private bool Shown(Marker m) => _filters.TryGetValue(GroupOf(m), out var f) && f.IsChecked == true;

        // Слои, которые были до 2.4.2: у старых настроек новые слои включаются по умолчанию.
        private static readonly string[] OldLayers =
        {
            "building", "cave", "exit", "animal", "carcass", "container", "loot", "plant", "breakdown", "fire", "bed", "workbench", "fishing", "rope", "cairn",
        };

        internal static readonly Dictionary<string, string> Icons = new()
        {
            ["house"] = "🏠", ["cabin"] = "🏠", ["lodge"] = "🏡", ["church"] = "⛪", ["barn"] = "🏚",
            ["lighthouse"] = "🗼", ["trailer"] = "🚐", ["store"] = "🏪", ["office"] = "🏢", ["garage"] = "🔧",
            ["factory"] = "🏭", ["cave"] = "🕳", ["mine"] = "⛏", ["cavescene"] = "🚪", ["landmark"] = "📍", ["sake"] = "🍶", ["card"] = "📜", ["whiskey"] = "🥃", ["cream"] = "🧁", ["exit"] = "➜", ["carcass"] = "🦌",
            ["wolf"] = "🐺", ["bear"] = "🐻", ["deer"] = "🦌", ["rabbit"] = "🐇", ["moose"] = "🦌", ["cougar"] = "🐆", ["bird"] = "🐦",
            ["plant"] = "🌿", ["breakdown"] = "🪓", ["fire"] = "🔥", ["bed"] = "🛏", ["shelter"] = "⛺",
            ["workbench"] = "🔨", ["forge"] = "⚒", ["fishing"] = "🎣", ["rope"] = "🧗", ["cairn"] = "⛰",
        };

        public MapPage(Ctx ctx)
        {
            _ctx = ctx;
            _ctx.EnteredGame += async () => { await System.Threading.Tasks.Task.Delay(3000); if (IsVisible && _ctx.InGame) await RenderMap(); };
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition());
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Панель управления
            var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            var render = Theme.Button("Обновить карту", Theme.Accent);
            render.Foreground = Brushes.White;
            render.Click += async (_, _) => await RenderMap(force: true);
            bar.Children.Add(render);
            var refresh = Theme.Button("Обновить метки");
            refresh.Margin = new Thickness(8, 0, 0, 0);
            refresh.Click += async (_, _) => await LoadMarkers(refresh: true);
            bar.Children.Add(refresh);
            var center = Theme.Button("К себе");
            center.Margin = new Thickness(8, 0, 16, 0);
            center.Click += (_, _) =>
            {
                _follow = true;
                CenterOnPlayer();
            };
            bar.Children.Add(center);
            var tpLabel = Theme.Label("Телепорт по щелчку", 13);
            tpLabel.VerticalAlignment = VerticalAlignment.Center;
            tpLabel.Margin = new Thickness(0, 0, 8, 0);
            bar.Children.Add(tpLabel);
            var tpSwitch = new OffOnSwitch { Value = ClickTeleport, VerticalAlignment = VerticalAlignment.Center };
            tpSwitch.Changed += v =>
            {
                _ctx.Settings.MapClickTeleport = v;
                _ctx.Settings.Save();
                _view.Cursor = v ? Cursors.Cross : Cursors.Arrow;
            };
            bar.Children.Add(tpSwitch);
            var bgLabel = Theme.Label("Карта из игры", 13);
            bgLabel.VerticalAlignment = VerticalAlignment.Center;
            bgLabel.Margin = new Thickness(16, 0, 8, 0);
            bar.Children.Add(bgLabel);
            var bgSwitch = new OffOnSwitch { Value = GameBackground, VerticalAlignment = VerticalAlignment.Center };
            bgSwitch.Changed += async v =>
            {
                _ctx.Settings.MapGameBackground = v;
                _ctx.Settings.Save();
                _scene = null;
                await RenderMap();
            };
            bar.Children.Add(bgSwitch);
            Children.Add(bar);

            // Карта
            _board.Children.Add(_background);
            _gameLayer = new GameLayer(this) { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            _board.Children.Add(_gameLayer);
            _board.Children.Add(_markers);
            _board.Children.Add(_playerLayer);
            _board.LayoutTransform = _zoom;
            _board.Background = Brushes.Transparent;

            _arrow = new Polygon
            {
                Points = new PointCollection { new(0, -13), new(9, 10), new(0, 5), new(-9, 10) },
                Fill = Brushes.White,
                Stroke = Theme.Hex("#D9534F"),
                StrokeThickness = 2.5,
                RenderTransform = new TransformGroup { Children = { _arrowRotate, _inverse } },
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };
            _playerLayer.Children.Add(_arrow);
            _playerLayer.IsHitTestVisible = false;

            _view = new ScrollViewer
            {
                Content = _board,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                Background = Theme.Card,
                Cursor = ClickTeleport ? Cursors.Cross : Cursors.Arrow,
            };
            _view.PreviewMouseWheel += OnWheel;
            _view.PreviewMouseRightButtonDown += (_, e) =>
            {
                StartPan(e.GetPosition(_view));
                _panning = true;
                _follow = false;
                _view.CaptureMouse();
                e.Handled = true;
            };
            // Левая кнопка: потянули — карта двигается, просто щёлкнули — телепорт.
            _view.PreviewMouseLeftButtonDown += (_, e) =>
            {
                _leftDown = true;
                StartPan(e.GetPosition(_view));
            };
            _view.PreviewMouseMove += (_, e) =>
            {
                var p = e.GetPosition(_board);
                if (_size > 0)
                {
                    var (wx, wz) = ToWorld(p);
                    _coords.Text = $"X {wx:0}  Z {wz:0}";
                }
                GameHover(e);
                MoveHover(e);
                var d = e.GetPosition(_view) - _panStart;
                if (_leftDown && !_panning && d.Length > DragThreshold)
                {
                    _panning = true;
                    _follow = false;
                    _view.CaptureMouse();
                }
                if (!_panning) return;
                _view.ScrollToHorizontalOffset(_panOffset.X - d.X);
                _view.ScrollToVerticalOffset(_panOffset.Y - d.Y);
            };
            _view.PreviewMouseRightButtonUp += (_, e) =>
            {
                _panning = false;
                _view.ReleaseMouseCapture();
                e.Handled = true;
            };
            _view.PreviewMouseLeftButtonUp += async (_, e) =>
            {
                bool dragged = _panning;
                _leftDown = false;
                if (dragged)
                {
                    _panning = false;
                    _view.ReleaseMouseCapture();
                    e.Handled = true;
                    return;
                }
                if (e.OriginalSource is DependencyObject src && IsInside(src, _board)) await TeleportTo(e.GetPosition(_board));
            };
            _view.MouseLeave += (_, _) => _hover.Visibility = Visibility.Collapsed;
            var mapArea = new Grid();
            mapArea.Children.Add(_view);
            mapArea.Children.Add(_hover);
            mapArea.Children.Add(FilterPanel());
            _mapArea = mapArea;
            SetRow(mapArea, 1);
            Children.Add(mapArea);

            // Строка состояния
            var foot = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            _coords = Theme.Label("", 12, Theme.Muted);
            DockPanel.SetDock(_coords, Dock.Right);
            foot.Children.Add(_coords);
            _status = Theme.Label("Щелчок — телепорт в точку. Колесо мыши — масштаб. Зажмите кнопку мыши и тяните, чтобы двигать карту.", 12, Theme.Muted);
            foot.Children.Add(_status);
            SetRow(foot, 2);
            Children.Add(foot);

            _posTimer.Tick += async (_, _) => await UpdatePlayer();
            _markerTimer.Tick += async (_, _) => await LoadMarkers();
            IsVisibleChanged += (_, _) =>
            {
                if (IsVisible)
                {
                    _posTimer.Start();
                    _markerTimer.Start();
                }
                else
                {
                    _posTimer.Stop();
                    _markerTimer.Stop();
                }
            };
        }

        public async void Show()
        {
            if (_size <= 0 || _scene == null) await RenderMap();
        }

        private bool GameBackground => _ctx.Settings.MapGameBackground ?? true;

        private async Task RenderMap(bool force = false)
        {
            if (_busy) return;
            if (!_ctx.InGame)
            {
                _status.Text = "Загрузите сохранение в игре, чтобы увидеть карту.";
                return;
            }
            _busy = true;
            _status.Text = "Карта готовится в игре…";
            try
            {
                string note = null;
                JsonElement? r = null;
                if (GameBackground)
                {
                    r = await Fetch("game", force);
                    if (r == null) return;
                    if (r.Value.TryGetProperty("unavailable", out var un) && un.ValueKind == JsonValueKind.True)
                    {
                        note = "Карта из игры недоступна (" + r.Value.GetProperty("reason").GetString() + "), показана схема рельефа.";
                        r = null;
                    }
                }
                r ??= await Fetch("relief", force);
                if (r == null) return;

                var map = r.Value;
                _scene = map.GetProperty("scene").GetString();
                _background.Children.Clear();
                var (w, h, m) = MapImage.Fill(_background, map);
                _gameMap = map.TryGetProperty("kind", out var mk) && mk.GetString() == "game";
                _w = w;
                _h = h;
                SetAffine(m[0], m[1], m[2], m[3], m[4], m[5]);
                _size = Math.Max(_w, _h);
                _background.Width = _markers.Width = _playerLayer.Width = _w;
                _background.Height = _markers.Height = _playerLayer.Height = _h;

                FitToView();
                _status.Text = note ?? "Щелчок — телепорт в точку. Колесо мыши — масштаб. Зажмите кнопку мыши и тяните, чтобы двигать карту.";
                await LoadMarkers(refresh: force);
                _follow = true;
                await UpdatePlayer();
            }
            finally
            {
                _busy = false;
            }
        }

        // Игра готовит карту понемногу в каждом кадре; ждём, пока будет готова.
        private async Task<JsonElement?> Fetch(string source, bool force)
        {
            while (true)
            {
                JsonElement r;
                try
                {
                    bool f = force;
                    r = await Task.Run(() => _ctx.Client.Send(new { cmd = "maprender", force = f, source }));
                }
                catch (Exception e)
                {
                    _status.Text = Ctx.Friendly(e);
                    return null;
                }
                force = false;
                if (r.TryGetProperty("ready", out var ready) && ready.ValueKind == JsonValueKind.True) return r;
                int progress = r.TryGetProperty("progress", out var pr) ? pr.GetInt32() : 0;
                _status.Text = $"Карта готовится в игре: {progress}%. Играть можно, игра не замирает.";
                await Task.Delay(1000);
                if (!IsVisible || !_ctx.InGame)
                {
                    _status.Text = "Карта достраивается в игре и откроется, когда вы вернётесь в этот раздел.";
                    _scene = null;
                    return null;
                }
            }
        }

        private async Task LoadMarkers(bool refresh = false)
        {
            if (!_ctx.InGame || _size <= 0) return;
            try
            {
                var r = await Task.Run(() => _ctx.Client.Send(new { cmd = "mapmarkers", refresh }));
                var fresh = r.GetProperty("markers").EnumerateArray().Select(m => new Marker(
                    m.GetProperty("Type").GetString(),
                    m.TryGetProperty("Kind", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null,
                    m.GetProperty("Name").GetString(),
                    (float)m.GetProperty("X").GetDouble(),
                    (float)m.GetProperty("Z").GetDouble(),
                    m.GetProperty("Done").GetBoolean(),
                    m.TryGetProperty("Sprite", out var sp) && sp.ValueKind == JsonValueKind.String ? sp.GetString() : null,
                    m.TryGetProperty("Game", out var gm) && gm.ValueKind == JsonValueKind.True,
                    m.TryGetProperty("Hidden", out var hd) && hd.ValueKind == JsonValueKind.True)).ToList();
                if (_markerData != null && _markerData.SequenceEqual(fresh)) return;
                // Чаще всего двигаются только звери: тогда перерисовываем только их, а не все метки карты.
                bool onlyMoving = _markerData != null && _markerData.Count > 0
                    && _markerData.Where(x => !Moving(x.Type)).SequenceEqual(fresh.Where(x => !Moving(x.Type)));
                _markerData = fresh;
                await LoadSprites(fresh);
                if (onlyMoving) DrawMoving();
                else DrawMarkers();
                UpdateCounts();
            }
            catch (Exception e)
            {
                _status.Text = Ctx.Friendly(e);
            }
        }

        // Догружаем из игры значки, которых ещё нет (один раз за сеанс трейнера).
        private async Task LoadSprites(List<Marker> markers)
        {
            var want = markers.Where(m => m.Sprite != null && !SpritesAsked.Contains(m.Sprite)).Select(m => m.Sprite).Distinct().ToArray();
            if (want.Length == 0) return;
            foreach (var n in want) SpritesAsked.Add(n);
            try
            {
                var r = await Task.Run(() => _ctx.Client.Send(new { cmd = "mapsprites", names = want }));
                foreach (var p in r.GetProperty("sprites").EnumerateObject())
                    Sprites[p.Name] = MapImage.Decode(p.Value.GetString());
                if (r.TryGetProperty("color", out var c) && c.GetArrayLength() >= 3)
                {
                    byte B(int i) => (byte)Math.Clamp(c[i].GetDouble() * 255, 0, 255);
                    _charcoal = Color.FromRgb(B(0), B(1), B(2));
                }
            }
            catch
            {
                // без значков игры — останутся обычные
            }
        }

        private static readonly FontFamily Hand = new("Segoe Print, Segoe UI");

        // Подпись как на угольной карте: тёмный текст со светлым ореолом, чтобы читался на бумаге.
        // Ореол — четыре светлые копии текста со сдвигом, без DropShadowEffect: эффекты в WPF
        // перерисовываются при каждом сдвиге и масштабе карты и сильно тормозят.
        private static readonly SolidColorBrush Halo = Freeze(new SolidColorBrush(Color.FromArgb(220, 236, 226, 205)));

        private static T Freeze<T>(T f) where T : Freezable
        {
            f.Freeze();
            return f;
        }

        private static FrameworkElement CharcoalText(string text, double size, Color color)
        {
            var g = new Grid { HorizontalAlignment = HorizontalAlignment.Center, IsHitTestVisible = true, Background = Brushes.Transparent };
            TextBlock T(Brush b, double dx, double dy) => new()
            {
                Text = text,
                FontSize = size,
                FontFamily = Hand,
                FontWeight = FontWeights.SemiBold,
                Foreground = b,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                RenderTransform = dx == 0 && dy == 0 ? null : new TranslateTransform(dx, dy),
            };
            g.Children.Add(T(Halo, -1, 0));
            g.Children.Add(T(Halo, 1, 0));
            g.Children.Add(T(Halo, 0, -1));
            g.Children.Add(T(Halo, 0, 1));
            g.Children.Add(T(new SolidColorBrush(color), 0, 0));
            return g;
        }

        // Значок игры, уже закрашенный углём (одна готовая картинка вместо маски прозрачности на каждой метке).
        private static readonly Dictionary<string, BitmapSource> Tinted = new();

        private static BitmapSource Tint(string name, BitmapSource src, Color c)
        {
            string key = name + "|" + c;
            if (Tinted.TryGetValue(key, out var done)) return done;
            var conv = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
            int w = conv.PixelWidth, h = conv.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            conv.CopyPixels(px, stride, 0);
            for (int i = 0; i < px.Length; i += 4)
            {
                byte a = px[i + 3];
                // premultiplied не нужен: формат Bgra32 — обычная альфа
                px[i] = c.B;
                px[i + 1] = c.G;
                px[i + 2] = c.R;
                px[i + 3] = a;
            }
            var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, stride);
            bmp.Freeze();
            Tinted[key] = bmp;
            return bmp;
        }

        private static bool Moving(string type) => type == "animal";
        private readonly List<FrameworkElement> _movingEls = new();

        private bool _gameHover;

        // Подсказка с названием над меткой карты игры (сами метки нарисованы одной картинкой).
        private void GameHover(MouseEventArgs e)
        {
            if (_panning || _gameLayer.Count == 0)
            {
                if (_gameHover) ShowHover(null);
                _gameHover = false;
                return;
            }
            var p = e.GetPosition(_board);
            var m = _gameLayer.Nearest(p, 16 / _zoom.ScaleX);
            if (m != null && !string.IsNullOrWhiteSpace(m.Name))
            {
                ShowHover(m.Name);
                _gameHover = true;
            }
            else if (_gameHover)
            {
                ShowHover(null);
                _gameHover = false;
            }
        }

        private sealed class GameLayer : FrameworkElement
        {
            private readonly MapPage _page;
            private readonly List<(Marker M, Point P, BitmapSource Icon, Geometry Text, string Emoji)> _items = new();
            private static readonly Pen HaloPen = MakeHalo();

            public GameLayer(MapPage page) => _page = page;
            public int Count => _items.Count;

            private static Pen MakeHalo()
            {
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(230, 236, 226, 205)), 3) { LineJoin = PenLineJoin.Round };
                pen.Freeze();
                return pen;
            }

            public void Set(List<Marker> markers)
            {
                _items.Clear();
                Width = _page._w;
                Height = _page._h;
                double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                var face = new Typeface(Hand, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
                foreach (var m in markers)
                {
                    BitmapSource icon = null;
                    string emoji = null;
                    bool label = m.Type == "label";
                    if (!label)
                    {
                        if (m.Sprite != null && Sprites.TryGetValue(m.Sprite, out var spr)) icon = Tint(m.Sprite, spr, _charcoal);
                        else emoji = Icons.TryGetValue(m.Kind ?? m.Type, out var ic) ? ic : (Icons.TryGetValue(m.Type, out var ic2) ? ic2 : "•");
                    }
                    Geometry text = null;
                    if (!string.IsNullOrWhiteSpace(m.Name))
                    {
                        var ft = new FormattedText(m.Name, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, label ? 14 : 11, Brushes.Black, dip)
                        {
                            TextAlignment = TextAlignment.Center,
                        };
                        text = ft.BuildGeometry(new Point(0, 0));
                        text.Freeze();
                    }
                    _items.Add((m, _page.ToBoard(m.X, m.Z), icon, text, emoji));
                }
                InvalidateVisual();
            }

            public Marker Nearest(Point p, double radius)
            {
                Marker best = null;
                double bestD = radius * radius;
                foreach (var it in _items)
                {
                    double dx = it.P.X - p.X, dy = it.P.Y - p.Y, d = dx * dx + dy * dy;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = it.M;
                    }
                }
                return best;
            }

            protected override void OnRender(DrawingContext dc)
            {
                if (_items.Count == 0) return;
                double inv = 1 / Math.Max(0.01, _page._zoom.ScaleX);
                var fill = new SolidColorBrush(_charcoal);
                fill.Freeze();
                double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                foreach (var it in _items)
                {
                    dc.PushTransform(new TranslateTransform(it.P.X, it.P.Y));
                    dc.PushTransform(new ScaleTransform(inv, inv));
                    if (it.M.Done) dc.PushOpacity(0.6);
                    double textTop;
                    if (it.Icon != null)
                    {
                        double h = 26, w = h * it.Icon.PixelWidth / Math.Max(1, it.Icon.PixelHeight);
                        dc.DrawImage(it.Icon, new Rect(-w / 2, -h / 2, w, h));
                        textTop = h / 2 + 1;
                    }
                    else if (it.Emoji != null)
                    {
                        var ft = new FormattedText(it.Emoji, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                            new Typeface("Segoe UI Emoji"), 15, Brushes.Black, dip);
                        dc.DrawText(ft, new Point(-ft.Width / 2, -ft.Height / 2));
                        textTop = ft.Height / 2;
                    }
                    else textTop = double.NaN;
                    if (it.Text != null)
                    {
                        var b = it.Text.Bounds;
                        double ty = double.IsNaN(textTop) ? -b.Height / 2 - b.Y : textTop - b.Y;
                        dc.PushTransform(new TranslateTransform(-b.X - b.Width / 2, ty));
                        dc.DrawGeometry(null, HaloPen, it.Text);
                        dc.DrawGeometry(fill, null, it.Text);
                        dc.Pop();
                    }
                    if (it.M.Done) dc.Pop();
                    dc.Pop();
                    dc.Pop();
                }
            }
        }

        private void DrawMoving()
        {
            foreach (var el in _movingEls) _markers.Children.Remove(el);
            _movingEls.Clear();
            if (_size <= 0) return;
            foreach (var m in _markerData)
            {
                if (!Moving(m.Type) || !Shown(m)) continue;
                var el = MarkerElement(m);
                var p = ToBoard(m.X, m.Z);
                Canvas.SetLeft(el, p.X);
                Canvas.SetTop(el, p.Y);
                _markers.Children.Add(el);
                _movingEls.Add(el);
            }
        }

        private void DrawMarkers()
        {
            _markers.Children.Clear();
            _movingEls.Clear();
            _hover.Visibility = Visibility.Collapsed;
            _gameHover = false;
            if (_size <= 0)
            {
                _gameLayer.Set(new List<Marker>());
                return;
            }
            // Метки карты игры на её подложке рисуем одним слоем: сотни отдельных элементов WPF пересчитывает
            // при каждом сдвиге и масштабе, и это сильно грузит компьютер.
            var drawn = _gameMap ? _markerData.Where(m => m.Game && Shown(m)).ToList() : new List<Marker>();
            _gameLayer.Set(drawn);
            // Сначала мелкие метки, крупные значки зданий поверх.
            foreach (var m in _markerData.Where(m => !(_gameMap && m.Game)).OrderBy(m => m.Type switch { "loot" => 0, "breakdown" => 1, "container" => 2, "building" or "cave" or "cavescene" or "exit" or "landmark" or "foodpack" => 4, _ => 3 }))
            {
                if (!Shown(m)) continue;
                var el = MarkerElement(m);
                var p = ToBoard(m.X, m.Z);
                Canvas.SetLeft(el, p.X);
                Canvas.SetTop(el, p.Y);
                _markers.Children.Add(el);
                if (Moving(m.Type)) _movingEls.Add(el);
            }
        }

        private FrameworkElement MarkerElement(Marker m)
        {
            var color = Layers.First(l => l.Key == m.Type).Color;
            string icon = Icons.TryGetValue(m.Kind ?? m.Type, out var ic) ? ic : (Icons.TryGetValue(m.Type, out var ic2) ? ic2 : "•");
            FrameworkElement body;
            if (_gameMap && m.Game && m.Type == "label")
            {
                // Надпись с карты игры
                body = CharcoalText(m.Name, 14, _charcoal);
            }
            else if (_gameMap && m.Game && m.Type is "building" or "cave" or "cavescene" or "exit" or "landmark")
            {
                // Как на угольной карте: значок игры и подпись под ним
                var stack = new StackPanel { Background = Brushes.Transparent, Opacity = m.Done ? 0.6 : 1 };
                if (m.Sprite != null && Sprites.TryGetValue(m.Sprite, out var spr))
                {
                    double h = 26, w = h * spr.PixelWidth / Math.Max(1, spr.PixelHeight);
                    stack.Children.Add(new Image
                    {
                        Width = w,
                        Height = h,
                        Source = Tint(m.Sprite, spr, _charcoal),
                        Stretch = Stretch.Fill,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    });
                }
                else
                {
                    var e = Emoji(icon, 15);
                    e.Opacity = 0.85;
                    stack.Children.Add(e);
                }
                if (!string.IsNullOrWhiteSpace(m.Name)) stack.Children.Add(CharcoalText(m.Name, 11, _charcoal));
                body = stack;
            }
            else if (m.Type is "building" or "cave" or "cavescene" or "exit" or "carcass" or "landmark" or "foodpack" or "label")
            {
                // Значок с подписью
                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                panel.Children.Add(Emoji(icon, 15));
                panel.Children.Add(new TextBlock { Text = " " + m.Name, FontSize = 11, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                body = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(200, 20, 22, 28)),
                    BorderBrush = m.Done ? Theme.Muted : color,
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(4, 1, 6, 1),
                    Opacity = m.Done ? 0.6 : 1,
                    Child = panel,
                };
            }
            else if (m.Type is "container" or "loot")
            {
                // Точки: их бывает много
                double d = m.Type == "loot" ? 7 : 10;
                // Прозрачная область побольше, чтобы на точку было легко навести курсор.
                body = new Grid
                {
                    Width = 18,
                    Height = 18,
                    Background = Brushes.Transparent,
                    Children =
                    {
                        new Ellipse
                        {
                            Width = d,
                            Height = d,
                            Fill = m.Done ? Theme.Hex("#5A5F6B") : color,
                            Stroke = Brushes.Black,
                            StrokeThickness = 1,
                        },
                    },
                };
            }
            else
            {
                // Значок без подписи, название во всплывающей подсказке
                body = new Border
                {
                    Width = 22,
                    Height = 22,
                    CornerRadius = new CornerRadius(11),
                    Background = new SolidColorBrush(Color.FromArgb(210, 20, 22, 28)),
                    BorderBrush = color,
                    BorderThickness = new Thickness(1.5),
                    Opacity = m.Done ? 0.45 : 1,
                    Child = Emoji(icon, 12),
                };
            }
            string label = m.Type switch
            {
                "container" => m.Name + (m.Done ? " (обыскан)" : ""),
                "carcass" => m.Name + (m.Done ? " (разделана)" : ""),
                "plant" => m.Name + (m.Done ? " (собрано)" : ""),
                _ => m.Name,
            };
            body.MouseEnter += (_, e) =>
            {
                ShowHover(label);
                MoveHover(e);
            };
            body.MouseLeave += (_, _) => _hover.Visibility = Visibility.Collapsed;
            body.Cursor = Cursors.Hand;

            // Метка не растёт при увеличении карты; точка привязки — центр метки.
            var holder = new Canvas { Width = 0, Height = 0, RenderTransform = _inverse };
            body.Loaded += (_, _) =>
            {
                Canvas.SetLeft(body, -body.ActualWidth / 2);
                Canvas.SetTop(body, -body.ActualHeight / 2);
            };
            holder.Children.Add(body);
            return holder;
        }

        private static TextBlock Emoji(string text, double size) => new()
        {
            Text = text,
            FontSize = size,
            FontFamily = new FontFamily("Segoe UI Emoji"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        private async Task UpdatePlayer()
        {
            if (_posBusy || !_ctx.InGame || _size <= 0) return;
            _posBusy = true;
            try
            {
                var r = await Task.Run(() => _ctx.Client.Send(new { cmd = "mappos" }));
                string scene = r.GetProperty("scene").GetString();
                if (scene != _scene)
                {
                    _posBusy = false;
                    await RenderMap();
                    return;
                }
                var p = ToBoard(r.GetProperty("x").GetDouble(), r.GetProperty("z").GetDouble());
                _lastPlayer = p;
                Canvas.SetLeft(_arrow, p.X);
                Canvas.SetTop(_arrow, p.Y);
                _arrowRotate.Angle = BoardAngle(r.GetProperty("yaw").GetDouble());
                _arrow.Visibility = Visibility.Visible;
                if (_follow) CenterOnPlayer();
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

        private bool ClickTeleport => _ctx.Settings.MapClickTeleport ?? true;

        private Grid _mapArea;

        private void StartPan(Point at)
        {
            _panStart = at;
            _panOffset = new Vector(_view.HorizontalOffset, _view.VerticalOffset);
        }

        private static bool IsInside(DependencyObject el, DependencyObject parent)
        {
            for (var cur = el; cur != null; cur = cur is Visual || cur is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(cur) : LogicalTreeHelper.GetParent(cur))
                if (cur == parent) return true;
            return false;
        }

        // Подпись метки рядом с курсором.
        private void ShowHover(string text)
        {
            ((TextBlock)_hover.Child).Text = text;
            _hover.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void MoveHover(MouseEventArgs e)
        {
            if (_hover.Visibility != Visibility.Visible || _mapArea == null) return;
            var p = e.GetPosition(_mapArea);
            _hover.Margin = new Thickness(p.X + 14, p.Y + 12, 0, 0);
        }

        private async Task TeleportTo(Point boardPoint)
        {
            if (!ClickTeleport || _size <= 0 || !_ctx.InGame) return;
            var (x, z) = ToWorld(boardPoint);
            var r = await _ctx.Request(new { cmd = "teleport", x, z }, $"Телепорт: X {x:0}, Z {z:0}");
            if (r != null) await UpdatePlayer();
        }

        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            if (_size <= 0) return;
            double old = _zoom.ScaleX;
            double z = Math.Clamp(old * (e.Delta > 0 ? 1.25 : 0.8), 0.1, 8);
            var mouse = e.GetPosition(_view);
            var boardPoint = new Point((_view.HorizontalOffset + mouse.X) / old, (_view.VerticalOffset + mouse.Y) / old);
            SetZoom(z);
            _view.UpdateLayout();
            _view.ScrollToHorizontalOffset(boardPoint.X * z - mouse.X);
            _view.ScrollToVerticalOffset(boardPoint.Y * z - mouse.Y);
            _follow = false;
        }

        private void SetZoom(double z)
        {
            _zoom.ScaleX = _zoom.ScaleY = z;
            _inverse.ScaleX = _inverse.ScaleY = 1 / z;
            _gameLayer?.InvalidateVisual();
        }

        private void FitToView()
        {
            double w = Math.Max(200, _view.ActualWidth), h = Math.Max(200, _view.ActualHeight);
            SetZoom(Math.Min(w / _w, h / _h));
        }

        private void CenterOnPlayer()
        {
            if (_arrow.Visibility != Visibility.Visible) return;
            double z = _zoom.ScaleX;
            _view.ScrollToHorizontalOffset(_lastPlayer.X * z - _view.ViewportWidth / 2);
            _view.ScrollToVerticalOffset(_lastPlayer.Y * z - _view.ViewportHeight / 2);
        }

        private void SetAffine(double a, double b, double c, double d, double e, double f)
        {
            _a = a;
            _b = b;
            _c = c;
            _d = d;
            _e = e;
            _f = f;
        }

        private Point ToBoard(double x, double z) => new(_a * x + _b * z + _c, _d * x + _e * z + _f);

        private (double X, double Z) ToWorld(Point p)
        {
            double det = _a * _e - _b * _d;
            double px = p.X - _c, py = p.Y - _f;
            return ((_e * px - _b * py) / det, (_a * py - _d * px) / det);
        }

        // Направление взгляда в игре (0° — на север, по часовой) → угол стрелки на доске (карта игры бывает повёрнута).
        private double BoardAngle(double yaw)
        {
            double r = yaw * Math.PI / 180;
            double sx = Math.Sin(r), cz = Math.Cos(r);
            double bx = _a * sx + _b * cz, by = _d * sx + _e * cz;
            return Math.Atan2(bx, -by) * 180 / Math.PI;
        }

        // Панель фильтров в правом верхнем углу карты.
        private FrameworkElement FilterPanel()
        {
            var list = new StackPanel();
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var collapse = new Button { Content = "▾", Padding = new Thickness(8, 2, 8, 2), Background = Theme.Control, ToolTip = "Свернуть / развернуть" };
            DockPanel.SetDock(collapse, Dock.Right);
            head.Children.Add(collapse);
            head.Children.Add(Theme.Label("Фильтры", 14, Theme.Text, bold: true));
            var body = new StackPanel();
            collapse.Click += (_, _) =>
            {
                body.Visibility = body.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                collapse.Content = body.Visibility == Visibility.Visible ? "▾" : "▸";
            };

            var allNone = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var all = new Button { Content = "Все", Padding = new Thickness(10, 3, 10, 3), Background = Theme.Control, FontSize = 12 };
            var none = new Button { Content = "Ничего", Padding = new Thickness(10, 3, 10, 3), Background = Theme.Control, FontSize = 12, Margin = new Thickness(6, 0, 0, 0) };
            all.Click += (_, _) => SetAll(true);
            none.Click += (_, _) => SetAll(false);
            allNone.Children.Add(all);
            allNone.Children.Add(none);
            body.Children.Add(allNone);

            var enabledGroups = _ctx.Settings.MapFilters;
            foreach (var (key, label, color, def) in Groups)
            {
                var t = new ToggleButton
                {
                    IsChecked = enabledGroups == null ? def : enabledGroups.Contains(key),
                    Margin = new Thickness(0, 0, 0, 4),
                    Cursor = Cursors.Hand,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Content = MakeLegend(label, color),
                    Template = ChipTemplate(),
                };
                t.Checked += (_, _) => FiltersChanged();
                t.Unchecked += (_, _) => FiltersChanged();
                _filters[key] = t;
                body.Children.Add(t);
            }
            list.Children.Add(head);
            list.Children.Add(body);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(225, 22, 23, 29)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 10, 10, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                MinWidth = 210,
                Child = list,
            };
        }

        private bool _bulk;

        private void SetAll(bool on)
        {
            _bulk = true;
            foreach (var t in _filters.Values) t.IsChecked = on;
            _bulk = false;
            FiltersChanged();
        }

        private void FiltersChanged()
        {
            if (_bulk) return;
            _ctx.Settings.MapFilters = _filters.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
            _ctx.Settings.Save();
            DrawMarkers();
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            foreach (var (key, label, color, _) in Groups)
            {
                if (!_filters.TryGetValue(key, out var t)) continue;
                int n = _markerData.Count(m => GroupOf(m) == key);
                t.Content = MakeLegend(n > 0 ? $"{label}  ({n})" : label, color);
            }
        }

        private static FrameworkElement MakeLegend(string label, Brush color)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = color, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = label, Foreground = Theme.Text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            return sp;
        }

        private static ControlTemplate ChipTemplate()
        {
            var t = new ControlTemplate(typeof(ToggleButton));
            var border = new FrameworkElementFactory(typeof(Border), "b");
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.PaddingProperty, new Thickness(10, 4, 10, 4));
            border.SetValue(Border.BackgroundProperty, Theme.Control);
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.BorderBrushProperty, Theme.Control);
            border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            t.VisualTree = border;
            var on = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
            on.Setters.Add(new Setter(Border.BorderBrushProperty, Theme.Accent, "b"));
            on.Setters.Add(new Setter(Border.BackgroundProperty, Theme.ControlHi, "b"));
            t.Triggers.Add(on);
            var off = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = false };
            off.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5, "b"));
            t.Triggers.Add(off);
            return t;
        }
    }
}
