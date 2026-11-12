using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TrainerByEMPEROR.Shared;

namespace TrainerByEMPEROR.App
{
    // Навыки по одному: уровень и кнопки −1 / +1 (в карточке «Навыки» раздела «Читы»).
    internal sealed class SkillsPanel : StackPanel
    {
        private sealed record SkillRow(int Id, string Name, int Level, int MaxLevel);

        private readonly Ctx _ctx;
        private readonly StackPanel _rows = new();
        private readonly TextBlock _note = Theme.Label("", 12, Theme.Muted);
        private bool _busy;
        private bool _loaded;

        public SkillsPanel(Ctx ctx)
        {
            _ctx = ctx;
            Margin = new Thickness(0, 14, 0, 0);
            var head = new DockPanel();
            var refresh = Theme.Button(L.T("Обновить", "Refresh"));
            refresh.Click += async (_, _) => await Load();
            DockPanel.SetDock(refresh, Dock.Right);
            head.Children.Add(refresh);
            head.Children.Add(new StackPanel
            {
                Children =
                {
                    Theme.Label(L.T("Уровень каждого навыка", "Level of each skill"), 14, Theme.Text, bold: true),
                    Theme.Label(L.T("Кнопки −1 и +1 меняют уровень навыка на один.", "The −1 and +1 buttons change the skill level by one."), 12, Theme.Muted),
                },
            });
            Children.Add(head);
            _note.Margin = new Thickness(0, 6, 0, 0);
            Children.Add(_note);
            Children.Add(_rows);

            _ctx.EnteredGame += async () =>
            {
                await Task.Delay(2000);
                if (_ctx.InGame) await Load();
            };
            _ctx.StateChanged += async () =>
            {
                if (!_ctx.InGame)
                {
                    _loaded = false;
                    _rows.Children.Clear();
                    _note.Text = L.T("Загрузите сохранение, чтобы увидеть навыки.", "Load a save to see the skills.");
                }
                else if (!_loaded && IsVisible) await Load();
            };
            _note.Text = L.T("Загрузите сохранение, чтобы увидеть навыки.", "Load a save to see the skills.");
        }

        private async Task Load()
        {
            if (_busy || !_ctx.InGame) return;
            _busy = true;
            try
            {
                var r = await Task.Run(() => _ctx.Client.Send(new { cmd = "skills" }));
                var list = r.GetProperty("items").EnumerateArray()
                    .Select(i => new SkillRow(i.GetProperty("Id").GetInt32(), i.GetProperty("Name").GetString(),
                        i.GetProperty("Level").GetInt32(), i.GetProperty("MaxLevel").GetInt32()))
                    .ToList();
                _rows.Children.Clear();
                foreach (var s in list) _rows.Children.Add(Row(s));
                _note.Text = "";
                _loaded = true;
            }
            catch (Exception e)
            {
                _note.Text = Ctx.Friendly(e);
            }
            finally
            {
                _busy = false;
            }
        }

        private FrameworkElement Row(SkillRow s)
        {
            var g = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = Theme.Label(s.Name, 14, Theme.Text);
            name.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(name);

            var minus = Theme.Button("−1");
            minus.MinWidth = 48;
            minus.IsEnabled = s.Level > 1;
            minus.Click += async (_, _) => await Change(s, -1);
            Grid.SetColumn(minus, 1);
            g.Children.Add(minus);

            var level = Theme.Label(L.T($"{s.Level} из {s.MaxLevel}", $"{s.Level} of {s.MaxLevel}"), 14, Theme.Accent, bold: true);
            level.HorizontalAlignment = HorizontalAlignment.Center;
            level.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(level, 2);
            g.Children.Add(level);

            var plus = Theme.Button("+1");
            plus.MinWidth = 48;
            plus.IsEnabled = s.Level < s.MaxLevel;
            plus.Click += async (_, _) => await Change(s, +1);
            Grid.SetColumn(plus, 3);
            g.Children.Add(plus);
            return g;
        }

        private async Task Change(SkillRow s, int delta)
        {
            if (await _ctx.Request(new { cmd = "skilllevel", id = s.Id, delta }, L.T($"{s.Name}: уровень {Math.Clamp(s.Level + delta, 1, s.MaxLevel)}", $"{s.Name}: level {Math.Clamp(s.Level + delta, 1, s.MaxLevel)}")) != null)
                await Load();
        }
    }
}
