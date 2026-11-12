using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TrainerByEMPEROR.Shared;

namespace TrainerByEMPEROR.App
{
    // Раздел «Чертежи»: открыть конкретный чертёж или все сразу.
    internal sealed class BlueprintsPage : Grid
    {
        private sealed record Bp(int Index, string Name, int Count, bool Unlocked);

        private readonly Ctx _ctx;
        private readonly TextBox _search;
        private readonly StackPanel _list;
        private readonly TextBlock _status;
        private List<Bp> _items = new();

        public BlueprintsPage(Ctx ctx)
        {
            _ctx = ctx;
            // Список готовится сразу после входа в игру, даже если раздел не открыт.
            _ctx.EnteredGame += async () => { await System.Threading.Tasks.Task.Delay(3000); if (_ctx.InGame) await Load(); };
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition());

            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var all = Theme.Button(L.T("Открыть все", "Unlock all"), Theme.Accent);
            all.Foreground = Brushes.White;
            all.Margin = new Thickness(10, 0, 0, 0);
            all.Click += async (_, _) =>
            {
                if (await _ctx.Request(new { cmd = "unlockallbp" }, L.T("Все чертежи открыты", "All blueprints unlocked")) != null) await Load();
            };
            DockPanel.SetDock(all, Dock.Right);
            top.Children.Add(all);
            var refresh = Theme.Button(L.T("Обновить", "Refresh"));
            refresh.Margin = new Thickness(10, 0, 0, 0);
            refresh.Click += async (_, _) => await Load();
            DockPanel.SetDock(refresh, Dock.Right);
            top.Children.Add(refresh);
            _search = new TextBox { ToolTip = L.T("Поиск чертежа", "Search blueprints") };
            _search.TextChanged += (_, _) => Render();
            top.Children.Add(_search);
            Children.Add(top);

            _status = Theme.Label("", 12, Theme.Muted);
            _status.Margin = new Thickness(0, 0, 0, 10);
            SetRow(_status, 1);
            Children.Add(_status);

            _list = new StackPanel();
            var scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 10, 0) };
            SetRow(scroll, 2);
            Children.Add(scroll);
        }

        public async void Show() => await Load();

        private async System.Threading.Tasks.Task Load()
        {
            if (!_ctx.InGame)
            {
                _status.Text = L.T("Загрузите сохранение в игре, чтобы увидеть чертежи.", "Load a save in the game to see the blueprints.");
                _items.Clear();
                Render();
                return;
            }
            JsonElement r;
            try
            {
                r = await System.Threading.Tasks.Task.Run(() => _ctx.Client.Send(new { cmd = "blueprints" }));
            }
            catch (Exception e)
            {
                _status.Text = Ctx.Friendly(e);
                return;
            }
            _items = r.GetProperty("items").EnumerateArray()
                .Select(i => new Bp(i.GetProperty("Index").GetInt32(), i.GetProperty("Name").GetString(),
                    i.GetProperty("Count").GetInt32(), i.GetProperty("Unlocked").GetBoolean()))
                .ToList();
            Render();
        }

        private void Render()
        {
            _list.Children.Clear();
            string q = _search.Text.Trim();
            var found = _items.Where(b => q.Length == 0 || b.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
            if (_items.Count > 0)
                _status.Text = L.T($"Чертежей: {_items.Count}, закрыто: {_items.Count(b => !b.Unlocked)}. Показано: {found.Count}.", $"Blueprints: {_items.Count}, locked: {_items.Count(b => !b.Unlocked)}. Shown: {found.Count}.");
            foreach (var bp in found) _list.Children.Add(Row(bp));
        }

        private FrameworkElement Row(Bp bp)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            g.Children.Add(Theme.Label(bp.Count > 1 ? $"{bp.Name} ×{bp.Count}" : bp.Name, 14, Theme.Text, bold: true));

            var chip = new Border
            {
                Background = bp.Unlocked ? Theme.Hex("#1E3A2A") : Theme.Hex("#3A2A1E"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = Theme.Label(bp.Unlocked ? L.T("открыт", "unlocked") : L.T("закрыт", "locked"), 11, bp.Unlocked ? Theme.Good : Theme.Warn),
            };
            Grid.SetColumn(chip, 1);
            g.Children.Add(chip);

            var open = Theme.Button(L.T("Открыть", "Unlock"));
            open.IsEnabled = !bp.Unlocked;
            open.Click += async (_, _) =>
            {
                if (await _ctx.Request(new { cmd = "unlockbp", index = bp.Index }, L.T($"Чертёж открыт: {bp.Name}", $"Blueprint unlocked: {bp.Name}")) != null) await Load();
            };
            Grid.SetColumn(open, 2);
            g.Children.Add(open);

            return new Border
            {
                Background = Theme.Card,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6),
                Child = g,
            };
        }
    }
}
