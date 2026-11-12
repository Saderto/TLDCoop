using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TrainerByEMPEROR.Shared;

namespace TrainerByEMPEROR.App
{
    // Раздел «Предметы»: все предметы игры по группам, выдача 1 / 5 / 10 штук.
    internal sealed class ItemsPage : Grid
    {
        private const int MaxRows = 400;
        private const string AllGroups = "Все предметы";

        private sealed record Item(string Id, string Name, string Group);

        private readonly Ctx _ctx;
        private readonly TextBox _search;
        private readonly StackPanel _groups;
        private readonly StackPanel _list;
        private readonly TextBlock _status;
        private readonly DispatcherTimer _loadTimer;
        private List<Item> _items;
        private string _group = AllGroups;
        private bool _loading;

        public ItemsPage(Ctx ctx)
        {
            _ctx = ctx;
            // Список предметов собирается в фоне сразу после входа в игру, даже если раздел не открыт.
            _ctx.EnteredGame += () =>
            {
                if (_items == null) _loadTimer?.Start();
            };
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition());
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            ColumnDefinitions.Add(new ColumnDefinition());

            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var refresh = Theme.Button(L.T("Обновить список", "Refresh list"));
            refresh.Margin = new Thickness(10, 0, 0, 0);
            refresh.Click += async (_, _) =>
            {
                await _ctx.Request(new { cmd = "items_rebuild" });
                _items = null;
                Show();
            };
            DockPanel.SetDock(refresh, Dock.Right);
            top.Children.Add(refresh);
            _search = new TextBox { ToolTip = L.T("Поиск по названию или коду", "Search by name or code") };
            _search.TextChanged += (_, _) => Render();
            top.Children.Add(_search);
            SetColumnSpan(top, 2);
            Children.Add(top);

            _status = Theme.Label("", 12, Theme.Muted);
            _status.Margin = new Thickness(0, 0, 0, 10);
            SetRow(_status, 1);
            SetColumnSpan(_status, 2);
            Children.Add(_status);

            _groups = new StackPanel { Margin = new Thickness(0, 0, 14, 0) };
            var gs = new ScrollViewer { Content = _groups, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            SetRow(gs, 2);
            Children.Add(gs);

            _list = new StackPanel();
            var ls = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 10, 0) };
            SetRow(ls, 2);
            SetColumn(ls, 1);
            Children.Add(ls);

            _loadTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _loadTimer.Tick += async (_, _) => await Load();
        }

        // Вызывается при открытии раздела.
        public void Show()
        {
            if (_items == null) _loadTimer.Start();
            Render();
        }

        private async System.Threading.Tasks.Task Load()
        {
            if (_loading) return;
            if (!_ctx.Connected)
            {
                _status.Text = L.T("Запустите игру: список предметов берётся прямо из неё.", "Start the game: the item list is taken from it.");
                return;
            }
            _loading = true;
            try
            {
                JsonElement r;
                try
                {
                    r = await System.Threading.Tasks.Task.Run(() => _ctx.Client.Send(new { cmd = "items" }));
                }
                catch (Exception e)
                {
                    _status.Text = Ctx.Friendly(e);
                    return;
                }

                if (r.TryGetProperty("ready", out var ready) && ready.GetBoolean())
                {
                    _items = r.GetProperty("items").EnumerateArray()
                        .Select(i => new Item(i.GetProperty("Id").GetString(), i.GetProperty("Name").GetString(), i.GetProperty("Group").GetString()))
                        .ToList();
                    _loadTimer.Stop();
                    RenderGroups();
                    Render();
                }
                else if (r.TryGetProperty("error", out var err))
                {
                    _status.Text = err.GetString();
                }
                else
                {
                    int done = r.GetProperty("progress").GetInt32();
                    int total = r.GetProperty("total").GetInt32();
                    _status.Text = L.T($"Игра собирает список предметов: {done} из {total}… (только при первом запуске)", $"The game is collecting the item list: {done} of {total}… (first run only)");
                }
            }
            finally
            {
                _loading = false;
            }
        }

        // Группы приходят из модуля в игре по-русски (они же ключи), на экран — на выбранном языке.
        internal static string GroupName(string g) => !L.En ? g : g switch
        {
            AllGroups => "All items",
            "Из модов" => "From mods",
            "Оружие и боеприпасы" => "Weapons and ammo",
            "Еда и напитки" => "Food and drinks",
            "Одежда" => "Clothing",
            "Медицина" => "First aid",
            "Розжиг и свет" => "Fire and light",
            "Инструменты" => "Tools",
            "Материалы" => "Materials",
            "Прочее" => "Other",
            "FoodPack: напитки" => "FoodPack: drinks",
            "FoodPack: блюда" => "FoodPack: dishes",
            "FoodPack: консервы" => "FoodPack: canned food",
            "FoodPack: перекусы" => "FoodPack: snacks",
            "FoodPack: упаковки" => "FoodPack: packs",
            "FoodPack: карточки рецептов" => "FoodPack: recipe cards",
            "FoodPack: ингредиенты" => "FoodPack: ingredients",
            "Табак: заправки для зажигалок" => "Tobacco: lighter refills",
            "Табак: зажигалки" => "Tobacco: lighters",
            "Табак: сигареты" => "Tobacco: cigarettes",
            "Табак: сигары" => "Tobacco: cigars",
            "Табак: трубки и табак" => "Tobacco: pipes and tobacco",
            "Табак: прочее" => "Tobacco: other",
            _ => g,
        };

        private void RenderGroups()
        {
            _groups.Children.Clear();
            var groups = new List<(string Name, int Count)> { (AllGroups, _items.Count) };
            groups.AddRange(_items.GroupBy(i => i.Group).OrderBy(g => g.Key).Select(g => (g.Key, g.Count())));
            foreach (var (name, count) in groups)
            {
                var b = new Button
                {
                    Content = $"{GroupName(name)}  ({count})",
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 6),
                    Background = name == _group ? Theme.Accent : Theme.Control,
                };
                var n = name;
                b.Click += (_, _) =>
                {
                    _group = n;
                    RenderGroups();
                    Render();
                };
                _groups.Children.Add(b);
            }
        }

        private void Render()
        {
            _list.Children.Clear();
            if (_items == null) return;

            string q = _search.Text.Trim();
            var found = _items
                .Where(i => _group == AllGroups || i.Group == _group)
                .Where(i => q.Length == 0 || i.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) || i.Id.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();

            _status.Text = found.Count > MaxRows
                ? L.T($"Найдено {found.Count}, показаны первые {MaxRows}. Уточните поиск.", $"Found {found.Count}, showing the first {MaxRows}. Narrow the search.")
                : L.T($"Найдено: {found.Count}. Предметы появляются в инвентаре сразу.", $"Found: {found.Count}. Items appear in the inventory right away.");

            foreach (var item in found.Take(MaxRows)) _list.Children.Add(Row(item));
        }

        private FrameworkElement Row(Item item)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            title.Children.Add(Theme.Label(item.Name, 14, Theme.Text, bold: true));
            title.Children.Add(Theme.Label($"{GroupName(item.Group)} · {item.Id}", 11, Theme.Muted));
            g.Children.Add(title);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (int n in new[] { 1, 5, 10 })
            {
                var b = new Button { Content = $"+{n}", Width = 52, Margin = new Thickness(6, 0, 0, 0), Background = Theme.ControlHi };
                int count = n;
                b.Click += async (_, _) =>
                    await _ctx.Request(new { cmd = "spawn", item = item.Id, count }, L.T($"Выдано: {item.Name} ×{count}", $"Given: {item.Name} ×{count}"));
                buttons.Children.Add(b);
            }
            Grid.SetColumn(buttons, 1);
            g.Children.Add(buttons);

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
