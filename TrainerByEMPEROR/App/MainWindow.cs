using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

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

        private readonly CheatsPage _cheats;
        private readonly ItemsPage _items;
        private readonly BlueprintsPage _blueprints;
        private readonly RecipesPage _recipes;
        private readonly QuestsPage _quests;
        private readonly BadgesPage _badges;
        private readonly MapPage _map;
        private readonly SetupPage _setup;
        private bool _polling;
        private readonly Button _hide;

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
                Content = "▶  Играть",
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
                Content = "Скрыть",
                FontSize = 14,
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
            };
            _hide.Click += (_, _) => _ctx.Overlay?.Close();
            DockPanel.SetDock(_hide, Dock.Right);
            top.Children.Add(_hide);

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
            AddNav(nav, "cheats", "Читы");
            AddNav(nav, "items", "Предметы");
            AddNav(nav, "blueprints", "Чертежи");
            AddNav(nav, "recipes", "Рецепты");
            AddNav(nav, "map", "Карта");
            AddNav(nav, "quests", "Задания");
            AddNav(nav, "badges", "Значки");
            AddNav(nav, "setup", "Установка");
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
                _hide.Content = $"Скрыть ({KeyTitle(_ctx.Overlay.OverlayKey)})";
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
                ShowMessage("Игра уже запущена.", false);
                return;
            }
            if (!GameInstaller.IsGameFolder(_ctx.Settings.GamePath))
            {
                Navigate("setup");
                ShowMessage("Сначала укажите папку игры.", true);
                return;
            }
            if (!_setup.Ready())
            {
                Navigate("setup");
                ShowMessage("Устанавливаю трейнер в игру…", false);
                if (!await _setup.Install()) return;
            }
            try
            {
                GameInstaller.Launch(_ctx.Settings.GamePath);
                ShowMessage("Запускаю игру…", false);
                Navigate("cheats");
            }
            catch (Exception e)
            {
                ShowMessage("Не удалось запустить игру: " + e.Message, true);
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
                _connText.Text = "Подключено к игре";
            }
            else if (_ctx.Connected)
            {
                _dot.Fill = Theme.Warn;
                _connText.Text = "Подключено, загрузите сохранение";
            }
            else if (_ctx.GameRunning)
            {
                _dot.Fill = Theme.Warn;
                _connText.Text = "Игра запускается…";
            }
            else
            {
                _dot.Fill = Theme.Muted;
                _connText.Text = "Игра не запущена";
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
