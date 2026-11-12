using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace TrainerByEMPEROR.App
{
    // Раздел «Установка»: папка игры, установка MelonLoader и встроенной части трейнера.
    internal sealed class SetupPage : StackPanel
    {
        private readonly Ctx _ctx;
        private readonly TextBox _path;
        private readonly TextBlock _mlStatus;
        private readonly TextBlock _modStatus;
        private readonly TextBox _log;
        private readonly Button _install;
        private readonly Button _remove;

        public SetupPage(Ctx ctx)
        {
            _ctx = ctx;

            var card = new StackPanel();
            card.Children.Add(Theme.Label("Папка игры", 16, Theme.Text, bold: true));
            card.Children.Add(Gap(8));
            var pathRow = new DockPanel();
            var browse = Theme.Button("Выбрать tld.exe…");
            browse.Margin = new Thickness(8, 0, 0, 0);
            browse.Click += (_, _) => Browse();
            DockPanel.SetDock(browse, Dock.Right);
            pathRow.Children.Add(browse);
            var find = Theme.Button("Найти автоматически");
            find.Margin = new Thickness(8, 0, 0, 0);
            find.Click += (_, _) => AutoFind(true);
            DockPanel.SetDock(find, Dock.Right);
            pathRow.Children.Add(find);
            _path = new TextBox { IsReadOnly = true };
            pathRow.Children.Add(_path);
            card.Children.Add(pathRow);
            Children.Add(Theme.CardBorder(card));

            var status = new StackPanel();
            status.Children.Add(Theme.Label("Состояние", 16, Theme.Text, bold: true));
            status.Children.Add(Gap(8));
            _mlStatus = Theme.Label("", 13);
            _modStatus = Theme.Label("", 13);
            status.Children.Add(_mlStatus);
            status.Children.Add(Gap(4));
            status.Children.Add(_modStatus);
            status.Children.Add(Gap(12));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            _install = Theme.Button("Установить / обновить", Theme.Accent);
            _install.Foreground = Brushes.White;
            _install.Click += async (_, _) => await Install();
            _remove = Theme.Button("Удалить трейнер из игры");
            _remove.Margin = new Thickness(8, 0, 0, 0);
            _remove.Click += (_, _) => Remove();
            buttons.Children.Add(_install);
            buttons.Children.Add(_remove);
            status.Children.Add(buttons);
            status.Children.Add(Gap(12));
            status.Children.Add(Theme.Label(
                "Трейнер ставит в игру загрузчик модов MelonLoader и свой модуль TrainerByEMPEROR.dll в папку Mods. " +
                "Сама игра и сохранения не меняются. Удалить можно кнопкой выше.", 12, Theme.Muted));
            Children.Add(Theme.CardBorder(status));

            Children.Add(Theme.CardBorder(OverlayCard()));

            _log = new TextBox
            {
                IsReadOnly = true,
                Height = 150,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
            };
            Children.Add(_log);

            AutoFind(false);
        }

        public void Show()
        {
            UpdateStatus();
            _miniSwitch.Value = _ctx.Settings.MiniMapOn ?? false;
        }

        private OffOnSwitch _miniSwitch;

        // Настройки трейнера поверх игры и мини-карты.
        private FrameworkElement OverlayCard()
        {
            var card = new StackPanel();
            card.Children.Add(Theme.Label("Поверх игры", 16, Theme.Text, bold: true));
            card.Children.Add(Gap(8));
            card.Children.Add(Theme.Label(
                "Во время игры окно трейнера открывается прямо над игрой и прячется той же клавишей. Игра при этом не сворачивается и продолжает идти. " +
                "Мини-карта показывается в правом верхнем углу экрана игры. Пока трейнер открыт поверх игры, мини-карту можно перетащить мышью, а колесом мыши над ней менять масштаб.", 12, Theme.Muted));
            card.Children.Add(Gap(10));

            card.Children.Add(Row("Показать / скрыть трейнер", KeyButton(() => _ctx.Settings.OverlayKey, System.Windows.Input.Key.Insert, v => _ctx.Settings.OverlayKey = v)));
            card.Children.Add(Gap(6));
            card.Children.Add(Row("Мини-карта: клавиша", KeyButton(() => _ctx.Settings.MiniMapKey, System.Windows.Input.Key.Home, v => _ctx.Settings.MiniMapKey = v)));
            card.Children.Add(Gap(6));
            _miniSwitch = new OffOnSwitch { Value = _ctx.Settings.MiniMapOn ?? false };
            _miniSwitch.Changed += v =>
            {
                if (_ctx.Overlay != null) _ctx.Overlay.SetMiniMap(v);
                else
                {
                    _ctx.Settings.MiniMapOn = v;
                    _ctx.Settings.Save();
                }
            };
            card.Children.Add(Row("Мини-карта включена", _miniSwitch));
            card.Children.Add(Gap(6));
            var radiusLabel = Theme.Label("", 13, Theme.Muted);
            var radius = new Slider
            {
                Minimum = MiniMap.MinRadius,
                Maximum = MiniMap.MaxRadius,
                Width = 220,
                SmallChange = 10,
                LargeChange = 50,
                IsSnapToTickEnabled = true,
                TickFrequency = 10,
                Value = _ctx.Settings.MiniMapRadius ?? MiniMap.DefaultRadius,
            };
            void ShowRadius() => radiusLabel.Text = $"{(int)radius.Value} м от центра до края  ";
            ShowRadius();
            bool fromMap = false;
            radius.ValueChanged += (_, _) =>
            {
                ShowRadius();
                if (fromMap) return;
                _ctx.Settings.MiniMapRadius = (int)radius.Value;
                _ctx.Settings.Save();
            };
            MiniMap.RadiusChanged += () =>
            {
                fromMap = true;
                radius.Value = _ctx.Settings.MiniMapRadius ?? MiniMap.DefaultRadius;
                fromMap = false;
            };
            var radiusBox = new StackPanel { Orientation = Orientation.Horizontal };
            radiusBox.Children.Add(radiusLabel);
            radiusBox.Children.Add(radius);
            card.Children.Add(Row("Мини-карта: масштаб", radiusBox));
            card.Children.Add(Gap(6));
            var hide = new OffOnSwitch { Value = _ctx.Settings.HideWhenPlaying ?? true };
            hide.Changed += v =>
            {
                _ctx.Settings.HideWhenPlaying = v;
                _ctx.Settings.Save();
            };
            card.Children.Add(Row("Прятать окно трейнера, когда запущена игра", hide));
            card.Children.Add(Gap(6));
            var auto = new OffOnSwitch { Value = _ctx.Settings.StartWithGame ?? true };
            auto.Changed += v =>
            {
                _ctx.Settings.StartWithGame = v;
                _ctx.Settings.Save();
                AutoStart.Update(_ctx.Settings);
            };
            card.Children.Add(Row("Запускать трейнер вместе с игрой (открывается клавишей)", auto));
            return card;
        }

        private static FrameworkElement Row(string text, FrameworkElement control)
        {
            var row = new DockPanel();
            control.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(control, Dock.Right);
            row.Children.Add(control);
            var label = Theme.Label(text, 13);
            label.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(label);
            return row;
        }

        // Кнопка с именем клавиши: щелчок, затем нажатие новой клавиши. Esc — отмена.
        private Button KeyButton(Func<string> get, System.Windows.Input.Key fallback, Action<string> set)
        {
            var b = Theme.Button(MainWindow.KeyTitle(Overlay.ParseKey(get(), fallback)));
            b.MinWidth = 110;
            bool waiting = false;
            b.Click += (_, _) =>
            {
                waiting = true;
                b.Content = "Нажмите клавишу…";
                b.Focus();
            };
            b.PreviewKeyDown += (_, e) =>
            {
                if (!waiting) return;
                e.Handled = true;
                var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
                waiting = false;
                if (key != System.Windows.Input.Key.Escape)
                {
                    set(key.ToString());
                    _ctx.Settings.Save();
                }
                b.Content = MainWindow.KeyTitle(Overlay.ParseKey(get(), fallback));
            };
            b.LostFocus += (_, _) =>
            {
                if (!waiting) return;
                waiting = false;
                b.Content = MainWindow.KeyTitle(Overlay.ParseKey(get(), fallback));
            };
            return b;
        }

        private void AutoFind(bool force)
        {
            if (!force && GameInstaller.IsGameFolder(_ctx.Settings.GamePath))
            {
                UpdateStatus();
                return;
            }
            var dir = GameInstaller.FindGame();
            if (dir != null)
            {
                _ctx.Settings.GamePath = dir;
                AutoStart.Update(_ctx.Settings);
                _ctx.Settings.Save();
                Log("Игра найдена: " + dir);
            }
            else if (force)
            {
                Log("Игра не найдена автоматически. Укажите tld.exe вручную.");
            }
            UpdateStatus();
        }

        private void Browse()
        {
            var dlg = new OpenFileDialog { Filter = "The Long Dark (tld.exe)|tld.exe", Title = "Укажите tld.exe в папке игры" };
            if (dlg.ShowDialog() != true) return;
            _ctx.Settings.GamePath = System.IO.Path.GetDirectoryName(dlg.FileName);
            _ctx.Settings.Save();
            UpdateStatus();
        }

        public void UpdateStatus()
        {
            string dir = _ctx.Settings.GamePath;
            bool ok = GameInstaller.IsGameFolder(dir);
            _path.Text = ok ? dir : "не выбрана";
            _install.IsEnabled = ok;
            _remove.IsEnabled = ok;
            if (!ok)
            {
                Status(_mlStatus, "MelonLoader: —", Theme.Muted);
                Status(_modStatus, "Трейнер в игре: —", Theme.Muted);
                return;
            }

            bool ml = GameInstaller.MelonLoaderInstalled(dir);
            Status(_mlStatus, ml ? "MelonLoader: установлен" : "MelonLoader: не установлен", ml ? Theme.Good : Theme.Warn);
            switch (GameInstaller.GetModStatus(dir))
            {
                case ModStatus.Installed:
                    Status(_modStatus, "Трейнер в игре: установлен, актуальная версия", Theme.Good);
                    break;
                case ModStatus.Outdated:
                    Status(_modStatus, "Трейнер в игре: старая версия, нажмите «Установить / обновить»", Theme.Warn);
                    break;
                default:
                    Status(_modStatus, "Трейнер в игре: не установлен", Theme.Warn);
                    break;
            }
        }

        public bool Ready()
        {
            string dir = _ctx.Settings.GamePath;
            return GameInstaller.IsGameFolder(dir) && GameInstaller.MelonLoaderInstalled(dir) &&
                   GameInstaller.GetModStatus(dir) == ModStatus.Installed;
        }

        public async System.Threading.Tasks.Task<bool> Install()
        {
            _install.IsEnabled = false;
            try
            {
                await GameInstaller.Install(_ctx.Settings.GamePath, Log);
                _ctx.Notify("Трейнер установлен в игру", false);
                return true;
            }
            catch (Exception e)
            {
                Log("Ошибка: " + e.Message);
                _ctx.Notify("Не удалось установить: " + e.Message, true);
                return false;
            }
            finally
            {
                UpdateStatus();
            }
        }

        private void Remove()
        {
            try
            {
                GameInstaller.RemoveMod(_ctx.Settings.GamePath);
                Log("Модуль трейнера удалён из папки Mods. MelonLoader оставлен (его можно удалить вручную: version.dll и папка MelonLoader).");
            }
            catch (Exception e)
            {
                Log("Ошибка: " + e.Message);
            }
            UpdateStatus();
        }

        private void Log(string line) => _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}\n");

        private static void Status(TextBlock t, string text, Brush color)
        {
            t.Text = text;
            t.Foreground = color;
        }

        private static FrameworkElement Gap(double h) => new Border { Height = h };
    }
}
