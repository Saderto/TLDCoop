using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TLDTrainer.Shared;

namespace TLDTrainer.App
{
    // Раздел «Читы»: все функции по категориям, как в Wand.
    internal sealed class CheatsPage : Grid
    {
        private readonly Ctx _ctx;
        private readonly Border _banner;
        private readonly TextBlock _bannerText;
        private readonly List<Action> _refreshers = new();
        private readonly List<FrameworkElement> _needConnection = new();
        private readonly List<FrameworkElement> _needGame = new();
        private readonly List<Action> _bindRefreshers = new();
        private string _capturing;
        private bool _hooked;
        private bool _suppress;

        public CheatsPage(Ctx ctx)
        {
            _ctx = ctx;
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition());

            _bannerText = Theme.Label("", 13, Theme.Text);
            _banner = new Border
            {
                Background = Theme.Hex("#2A2416"),
                BorderBrush = Theme.Warn,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 14),
                Child = _bannerText,
            };
            Children.Add(_banner);

            var stack = new StackPanel();
            var keysRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var resetKeys = Theme.Button("Клавиши по умолчанию");
            resetKeys.Click += async (_, _) =>
            {
                _capturing = null;
                _ctx.ResetBinds();
                RefreshBinds();
                await _ctx.PushBinds();
            };
            DockPanel.SetDock(resetKeys, Dock.Right);
            keysRow.Children.Add(resetKeys);
            keysRow.Children.Add(Theme.Label("Горячие клавиши работают в игре. Щёлкните по клавише рядом с функцией, чтобы назначить свою или сочетание (Ctrl, Shift, Alt + клавиша). Правый щелчок убирает клавишу.", 12, Theme.Muted));
            stack.Children.Add(keysRow);
            foreach (var cat in Features.Categories)
            {
                var defs = Features.All.Where(f => f.Category == cat).ToList();
                if (defs.Count == 0) continue;
                var card = new StackPanel();
                card.Children.Add(Theme.Label(cat, 16, Theme.Text, bold: true));
                foreach (var def in defs) card.Children.Add(Row(def));
                if (cat == Features.CatStats) card.Children.Add(new SkillsPanel(_ctx));
                stack.Children.Add(Theme.CardBorder(card));
            }

            var scroll = new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0, 0, 10, 0),
            };
            SetRow(scroll, 1);
            Children.Add(scroll);

            ctx.StateChanged += Refresh;
            Refresh();
            Loaded += (_, _) =>
            {
                var w = Window.GetWindow(this);
                if (w != null && !_hooked)
                {
                    w.PreviewKeyDown += OnKey;
                    _hooked = true;
                }
            };
        }

        private FrameworkElement Row(FeatureDef def)
        {
            var g = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });

            var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(Theme.Label(def.Name, 14, Theme.Text, bold: true));
            if (!string.IsNullOrEmpty(def.Hint)) title.Children.Add(Theme.Label(def.Hint, 12, Theme.Muted));
            g.Children.Add(title);

            if (Features.Bindable(def))
            {
                var bind = BindButton(def);
                SetColumn(bind, 1);
                g.Children.Add(bind);
            }

            var control = def.Kind switch
            {
                FeatureKind.Toggle => ToggleControl(def),
                FeatureKind.Slider => SliderControl(def),
                FeatureKind.Action => ActionControl(def),
                FeatureKind.Time => TimeControl(def),
                FeatureKind.Weather => WeatherControl(def),
                _ => new TextBlock(),
            };
            SetColumn(control, 2);
            g.Children.Add(control);
            return g;
        }

        // Кнопка с назначенной клавишей: щелчок — назначить новую, правый щелчок — убрать.
        private FrameworkElement BindButton(FeatureDef def)
        {
            var b = new Button
            {
                MinWidth = 96,
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(10, 0, 14, 0),
                FontSize = 12,
                Background = Theme.Control,
                Foreground = Theme.Muted,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Щелчок — назначить клавишу или сочетание (например Ctrl+Shift+H).\nПравый щелчок — убрать клавишу. Esc — отмена.",
            };
            void Show()
            {
                bool capturing = _capturing == def.Id;
                b.Content = capturing ? "нажмите клавиши…" : "⌨ " + Features.DisplayCombo(_ctx.GetBind(def.Id));
                b.Background = capturing ? Theme.Accent : Theme.Control;
                b.Foreground = capturing ? Brushes.White : Theme.Muted;
            }
            b.Click += (_, _) =>
            {
                _capturing = _capturing == def.Id ? null : def.Id;
                RefreshBinds();
            };
            b.MouseRightButtonUp += async (_, _) =>
            {
                _capturing = null;
                _ctx.SetBind(def.Id, "");
                RefreshBinds();
                await _ctx.PushBinds();
            };
            _bindRefreshers.Add(Show);
            Show();
            return b;
        }

        private void RefreshBinds()
        {
            foreach (var r in _bindRefreshers) r();
        }

        private async void OnKey(object sender, KeyEventArgs e)
        {
            if (_capturing == null || !IsVisible) return;
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.Escape)
            {
                _capturing = null;
                RefreshBinds();
                return;
            }
            if (KeyNames.IsModifier(key)) return;

            string name = KeyNames.ToUnity(key);
            if (name == null)
            {
                _ctx.Notify("Эту клавишу назначить нельзя, выберите другую.", true);
                return;
            }

            var mods = Keyboard.Modifiers;
            string combo = (mods.HasFlag(ModifierKeys.Control) ? "Ctrl+" : "") +
                           (mods.HasFlag(ModifierKeys.Shift) ? "Shift+" : "") +
                           (mods.HasFlag(ModifierKeys.Alt) ? "Alt+" : "") + name;
            string id = _capturing;
            _capturing = null;
            _ctx.SetBind(id, combo);
            RefreshBinds();
            _ctx.Notify($"{Features.Find(id)?.Name}: {Features.DisplayCombo(combo)}", false);
            await _ctx.PushBinds();
        }

        private FrameworkElement ToggleControl(FeatureDef def)
        {
            var sw = new OffOnSwitch { VerticalAlignment = VerticalAlignment.Center };
            sw.Changed += async v =>
            {
                _ctx.SetLocal(def.Id, v ? 1f : 0f);
                await _ctx.Request(new { cmd = "set", id = def.Id, value = v ? 1.0 : 0.0 }, $"{def.Name}: {(v ? "включено" : "выключено")}");
            };
            _refreshers.Add(() => sw.Value = _ctx.Get(def.Id) > 0.5f);
            _needConnection.Add(sw);
            return sw;
        }

        private FrameworkElement SliderControl(FeatureDef def)
        {
            var slider = new Slider
            {
                Minimum = def.Min,
                Maximum = def.Max,
                TickFrequency = def.Step,
                IsSnapToTickEnabled = true,
                Width = 180,
                VerticalAlignment = VerticalAlignment.Center,
                Value = def.Default,
            };
            var value = Theme.Label(Format(def, def.Default), 13, Theme.Accent, bold: true);
            value.Width = 90;
            value.TextAlignment = TextAlignment.Right;
            var reset = new Button { Content = "↺", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(6, 0, 0, 0), ToolTip = "Как в игре" };

            var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            debounce.Tick += async (_, _) =>
            {
                debounce.Stop();
                float v = (float)slider.Value;
                await _ctx.Request(new { cmd = "set", id = def.Id, value = (double)v });
            };
            slider.ValueChanged += (_, e) =>
            {
                value.Text = Format(def, (float)e.NewValue);
                if (_suppress) return;
                _ctx.SetLocal(def.Id, (float)e.NewValue);
                debounce.Stop();
                debounce.Start();
            };
            reset.Click += (_, _) => slider.Value = def.Default;

            _refreshers.Add(() =>
            {
                if (debounce.IsEnabled || slider.IsMouseCaptureWithin) return;
                slider.Value = _ctx.Get(def.Id);
            });
            _needConnection.Add(slider);
            _needConnection.Add(reset);

            var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            sp.Children.Add(value);
            sp.Children.Add(new Border { Width = 10 });
            sp.Children.Add(slider);
            sp.Children.Add(reset);
            return sp;
        }

        private static string Format(FeatureDef def, float v) => def.Id switch
        {
            "carry" => v < 0.5f ? "как в игре" : $"{v:0} кг",
            "flyspeed" => $"{v:0}",
            "hungerlvl" or "thirstlvl" or "fatiguelvl" or "freezelvl" => $"{v:0} %",
            _ => $"×{v:0.0#}",
        };

        private FrameworkElement ActionControl(FeatureDef def)
        {
            var b = Theme.Button("Применить", Theme.Accent);
            b.HorizontalAlignment = HorizontalAlignment.Right;
            b.Foreground = Brushes.White;
            b.Click += async (_, _) => await _ctx.Request(new { cmd = "action", id = def.Id }, $"{def.Name}: готово");
            _needGame.Add(b);
            return b;
        }

        private FrameworkElement TimeControl(FeatureDef def)
        {
            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 23,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                Width = 130,
                Value = 12,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var label = Theme.Label("12:00", 13, Theme.Accent, bold: true);
            label.Width = 50;
            slider.ValueChanged += (_, e) => label.Text = $"{(int)e.NewValue:00}:00";
            var apply = Theme.Button("Установить", Theme.Accent);
            apply.Foreground = Brushes.White;
            apply.Margin = new Thickness(10, 0, 0, 0);
            apply.Click += async (_, _) =>
                await _ctx.Request(new { cmd = "settime", hour = (int)slider.Value }, $"Время установлено: {(int)slider.Value:00}:00");
            _needGame.Add(apply);

            var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            sp.Children.Add(label);
            sp.Children.Add(slider);
            sp.Children.Add(apply);
            return sp;
        }

        private FrameworkElement WeatherControl(FeatureDef def)
        {
            var wrap = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            foreach (var w in Features.Weathers)
            {
                var b = new Button { Content = w.Name, Margin = new Thickness(4, 4, 0, 0), Padding = new Thickness(10, 5, 10, 5), FontSize = 12 };
                var stage = w.Stage;
                var name = w.Name;
                b.Click += async (_, _) => await _ctx.Request(new { cmd = "weather", stage }, $"Погода: {name}");
                _needGame.Add(b);
                wrap.Children.Add(b);
            }
            return wrap;
        }

        private void Refresh()
        {
            _suppress = true;
            try
            {
                foreach (var r in _refreshers) r();
            }
            finally
            {
                _suppress = false;
            }

            foreach (var c in _needConnection) c.IsEnabled = _ctx.Connected;
            foreach (var c in _needGame) c.IsEnabled = _ctx.InGame;

            if (!_ctx.Connected)
            {
                _banner.Visibility = Visibility.Visible;
                _bannerText.Text = _ctx.GameRunning
                    ? "Игра запущена, но трейнер в ней не отвечает. Если игра только запускается, подождите. Если нет — откройте раздел «Установка» и установите трейнер, затем перезапустите игру."
                    : "Игра не запущена. Нажмите «Играть» вверху справа. Переключатели станут активны, когда игра запустится.";
            }
            else if (!_ctx.InGame)
            {
                _banner.Visibility = Visibility.Visible;
                _bannerText.Text = "Игра подключена. Переключатели можно включать уже сейчас, они сработают после загрузки сохранения. Кнопки «Применить» доступны в самой игре.";
            }
            else
            {
                _banner.Visibility = Visibility.Collapsed;
            }
        }
    }
}
