using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using TrainerByEMPEROR.Shared;

namespace TrainerByEMPEROR.App
{
    internal sealed class MainWindow : Window
    {
        private readonly Ctx _ctx = new();
        private readonly ContentControl _page = new();
        private readonly Dictionary<string, Button> _nav = new();
        private readonly Ellipse _dot = new() { Width = 9, Height = 9, Margin = new Thickness(0, 0, 8, 0) };
        private readonly TextBlock _connText = Theme.Label("", 13, Theme.Muted);
        private readonly TextBlock _message = Theme.Label("", 12, Theme.Muted);
        private readonly DispatcherTimer _messageTimer = new() { Interval = TimeSpan.FromSeconds(6) };

        private CheatsPage _cheats;
        private ItemsPage _items;
        private BlueprintsPage _blueprints;
        private RecipesPage _recipes;
        private QuestsPage _quests;
        private BadgesPage _badges;
        private MapPage _map;
        private SetupPage _setup;
        private bool _polling;
        private Button _hide;
        private string _current = "cheats";

        // Язык из настроек; если не выбран — как в Windows.
        public static bool ResolveEnglish(string lang) =>
            lang == "en" || (lang != "ru" && !System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase));

        public MainWindow(bool background = false, bool openNow = false)
        {
            Title = "TrainerByEMPEROR";
            Width = 1180;
            Height = 820;
            MinWidth = 900;
            MinHeight = 560;
            Background = Theme.Window;
            Foreground = Theme.Text;
            FontFamily = new FontFamily("Segoe UI");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            _ctx.Notify = ShowMessage;
            _ctx.Navigate = Navigate;
            _messageTimer.Tick += (_, _) =>
            {
                _messageTimer.Stop();
                _message.Text = "";
            };

            L.En = ResolveEnglish(_ctx.Settings.Language);
            BuildUi();
            Navigate(_setup.Ready() ? "cheats" : "setup");
            Title = "TrainerByEMPEROR " + Shared.Features.Version;

            var poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            poll.Tick += async (_, _) => await Poll();
            poll.Start();
            UpdateConnection();

            _ctx.Overlay = new Overlay(this, _ctx);
            if (background) _ctx.Overlay.StartInBackground(openNow);
            AutoStart.Update(_ctx.Settings);
            _ctx.Overlay.ModeChanged += () =>
            {
                _hide.Visibility = _ctx.Overlay.Active ? Visibility.Visible : Visibility.Collapsed;
                _hide.Content = L.T($"Скрыть ({KeyTitle(_ctx.Overlay.OverlayKey)})", $"Hide ({KeyTitle(_ctx.Overlay.OverlayKey)})");
            };
            // Окно поверх игры закрывать нельзя — только прятать; иначе трейнер выключится.
            SizeChanged += (_, _) => _ctx.Overlay?.SizeChanged();
            Closing += (_, e) =>
            {
                if (_ctx.Overlay.Active && GameInstaller.IsGameRunning())
                {
                    e.Cancel = true;
                    _ctx.Overlay.Close();
                }
            };
        }

        // Строит всё содержимое окна на текущем языке (при смене языка — заново).
        private void BuildUi()
        {
            // Эти элементы живут всё время; при перестройке снимаем их со старой разметки.
            foreach (FrameworkElement e in new FrameworkElement[] { _page, _message, _dot, _connText })
                (e.Parent as Panel)?.Children.Remove(e);
            _cheats = new CheatsPage(_ctx);
            _items = new ItemsPage(_ctx);
            _blueprints = new BlueprintsPage(_ctx);
            _recipes = new RecipesPage(_ctx);
            _quests = new QuestsPage(_ctx);
            _badges = new BadgesPage(_ctx);
            _map = new MapPage(_ctx);
            _setup = new SetupPage(_ctx);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) });
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            root.ColumnDefinitions.Add(new ColumnDefinition());

            // Верхняя панель
            var top = new DockPanel { Margin = new Thickness(20, 0, 20, 0), LastChildFill = false };
            var title = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(Theme.Label("TrainerByEMPEROR", 22, Theme.Text, bold: true));
            title.Children.Add(new Border { Width = 12 });
            title.Children.Add(Theme.Label("The Long Dark", 14, Theme.Muted));
            DockPanel.SetDock(title, Dock.Left);
            top.Children.Add(title);

            var play = new Button
            {
                Content = L.T("▶  Играть", "▶  Play"),
                Background = Brushes.White,
                Foreground = Theme.Window,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(22, 9, 22, 9),
                VerticalAlignment = VerticalAlignment.Center,
            };
            play.Click += async (_, _) => await Play();
            DockPanel.SetDock(play, Dock.Right);
            top.Children.Add(play);

            // Поверх игры у окна нет рамки — прячем его этой кнопкой или той же клавишей.
            _hide = new Button
            {
                Content = L.T("Скрыть", "Hide"),
                FontSize = 14,
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
            };
            _hide.Click += (_, _) => _ctx.Overlay?.Close();
            DockPanel.SetDock(_hide, Dock.Right);
            top.Children.Add(_hide);

            // Язык окна: RU / EN. Выбор сохраняется и передаётся модулю в игре.
            var langBox = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
            foreach (var (code, text) in new[] { ("ru", "RU"), ("en", "EN") })
            {
                bool active = (code == "en") == L.En;
                var lb = new Button
                {
                    Content = text,
                    FontSize = 12,
                    FontWeight = active ? FontWeights.Bold : FontWeights.Normal,
                    Padding = new Thickness(9, 5, 9, 5),
                    Background = active ? Theme.Control : Brushes.Transparent,
                    Foreground = active ? Theme.Text : Theme.Muted,
                    ToolTip = code == "en" ? "English" : "Русский",
                    VerticalAlignment = VerticalAlignment.Center,
                };
                lb.Click += (_, _) => SetLanguage(code);
                langBox.Children.Add(lb);
            }
            DockPanel.SetDock(langBox, Dock.Right);
            top.Children.Add(langBox);

            var conn = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
            conn.Children.Add(_dot);
            conn.Children.Add(_connText);
            DockPanel.SetDock(conn, Dock.Right);
            top.Children.Add(conn);

            var topBorder = new Border { Background = Theme.Panel, Child = top };
            // Поверх игры у окна нет рамки: двигаем его за верхнюю полосу (например, на второй экран).
            // Двойной щелчок по полосе возвращает окно в центр игры.
            topBorder.MouseLeftButtonDown += (_, e) =>
            {
                if (_ctx.Overlay == null || !_ctx.Overlay.Active || e.OriginalSource is DependencyObject d && IsInButton(d)) return;
                if (e.ClickCount == 2)
                {
                    _ctx.Overlay.ResetPosition();
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
                _ctx.Overlay.RememberPosition();
            };
            Grid.SetColumnSpan(topBorder, 2);
            root.Children.Add(topBorder);

            // Меню слева
            var nav = new StackPanel { Margin = new Thickness(14, 16, 14, 0) };
            AddNav(nav, "cheats", L.T("Читы", "Cheats"));
            AddNav(nav, "items", L.T("Предметы", "Items"));
            AddNav(nav, "blueprints", L.T("Чертежи", "Blueprints"));
            AddNav(nav, "recipes", L.T("Рецепты", "Recipes"));
            AddNav(nav, "map", L.T("Карта", "Map"));
            AddNav(nav, "quests", L.T("Задания", "Quests"));
            AddNav(nav, "badges", L.T("Значки", "Badges"));
            AddNav(nav, "setup", L.T("Установка", "Setup"));
            var navBorder = new Border { Background = Theme.Panel, Child = nav };
            Grid.SetRow(navBorder, 1);
            Grid.SetRowSpan(navBorder, 2);
            root.Children.Add(navBorder);

            _page.Margin = new Thickness(22, 18, 12, 6);
            Grid.SetRow(_page, 1);
            Grid.SetColumn(_page, 1);
            root.Children.Add(_page);

            _message.Margin = new Thickness(22, 0, 12, 0);
            Grid.SetRow(_message, 2);
            Grid.SetColumn(_message, 1);
            root.Children.Add(_message);

            Content = root;
            if (_ctx.Overlay != null)
            {
                _hide.Visibility = _ctx.Overlay.Active ? Visibility.Visible : Visibility.Collapsed;
                _hide.Content = L.T($"Скрыть ({KeyTitle(_ctx.Overlay.OverlayKey)})", $"Hide ({KeyTitle(_ctx.Overlay.OverlayKey)})");
            }
        }

        private void SetLanguage(string code)
        {
            bool en = code == "en";
            _ctx.Settings.Language = code;
            _ctx.Settings.Save();
            if (en == L.En) return;
            L.En = en;
            string page = _current;
            _ctx.ClearPageEvents();
            BuildUi();
            Navigate(page);
            UpdateConnection();
            _ctx.Overlay?.ApplyLanguage();
            ShowMessage(L.T("Язык: русский", "Language: English"), false);
        }

        private static bool IsInButton(DependencyObject d)
        {
            for (; d != null; d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
                if (d is System.Windows.Controls.Primitives.ButtonBase) return true;
            return false;
        }

        public static string KeyTitle(System.Windows.Input.Key k) => k switch
        {
            System.Windows.Input.Key.Insert => "Ins",
            System.Windows.Input.Key.Home => "Home",
            System.Windows.Input.Key.Delete => "Del",
            System.Windows.Input.Key.PageUp => "PgUp",
            System.Windows.Input.Key.PageDown => "PgDn",
            _ => k.ToString(),
        };

        private void AddNav(Panel nav, string key, string text)
        {
            var b = new Button
            {
                Content = text,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                FontSize = 15,
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 6),
                Background = Brushes.Transparent,
            };
            b.Click += (_, _) => Navigate(key);
            _nav[key] = b;
            nav.Children.Add(b);
        }

        private void Navigate(string key)
        {
            _current = key;
            foreach (var kv in _nav) kv.Value.Background = kv.Key == key ? Theme.Control : Brushes.Transparent;
            switch (key)
            {
                case "cheats":
                    _page.Content = _cheats;
                    break;
                case "items":
                    _page.Content = _items;
                    _items.Show();
                    break;
                case "blueprints":
                    _page.Content = _blueprints;
                    _blueprints.Show();
                    break;
                case "recipes":
                    _page.Content = _recipes;
                    _recipes.Show();
                    break;
                case "map":
                    _page.Content = _map;
                    _map.Show();
                    break;
                case "quests":
                    _page.Content = _quests;
                    _quests.Show();
                    break;
                case "badges":
                    _page.Content = _badges;
                    _badges.Show();
                    break;
                default:
                    _page.Content = _setup;
                    _setup.Show();
                    break;
            }
        }

        private async Task Play()
        {
            if (GameInstaller.IsGameRunning())
            {
                ShowMessage(L.T("Игра уже запущена.", "The game is already running."), false);
                return;
            }
            if (!GameInstaller.IsGameFolder(_ctx.Settings.GamePath))
            {
                Navigate("setup");
                ShowMessage(L.T("Сначала укажите папку игры.", "Choose the game folder first."), true);
                return;
            }
            if (!_setup.Ready())
            {
                Navigate("setup");
                ShowMessage(L.T("Устанавливаю трейнер в игру…", "Installing the trainer into the game…"), false);
                if (!await _setup.Install()) return;
            }
            try
            {
                GameInstaller.Launch(_ctx.Settings.GamePath);
                ShowMessage(L.T("Запускаю игру…", "Starting the game…"), false);
                Navigate("cheats");
            }
            catch (Exception e)
            {
                ShowMessage(L.T("Не удалось запустить игру: ", "Could not start the game: ") + e.Message, true);
            }
        }

        private async Task Poll()
        {
            if (_polling) return;
            _polling = true;
            try
            {
                await Task.Run(_ctx.Poll);
                UpdateConnection();
                _ctx.RaiseStateChanged();
            }
            finally
            {
                _polling = false;
            }
        }

        private void UpdateConnection()
        {
            if (_ctx.Connected && _ctx.InGame)
            {
                _dot.Fill = Theme.Good;
                _connText.Text = L.T("Подключено к игре", "Connected to the game");
            }
            else if (_ctx.Connected)
            {
                _dot.Fill = Theme.Warn;
                _connText.Text = L.T("Подключено, загрузите сохранение", "Connected, load a save");
            }
            else if (_ctx.GameRunning)
            {
                _dot.Fill = Theme.Warn;
                _connText.Text = L.T("Игра запускается…", "The game is starting…");
            }
            else
            {
                _dot.Fill = Theme.Muted;
                _connText.Text = L.T("Игра не запущена", "Game not running");
            }
        }

        private void ShowMessage(string text, bool error)
        {
            _message.Text = text;
            _message.Foreground = error ? Theme.Bad : Theme.Muted;
            _messageTimer.Stop();
            _messageTimer.Start();
        }
    }
}
