using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TLDTrainer.App
{
    // Раздел «Рецепты»: блюда из книги рецептов (рагу егеря, пирог ловчего и т. д.) — открыть рецепт или выдать готовое блюдо.
    internal sealed class RecipesPage : Grid
    {
        private sealed record Recipe(int Index, string Name, string Description, string Ingredients, int Skill, string Dish, bool Unlocked, bool Cooked);

        private readonly Ctx _ctx;
        private readonly TextBox _search;
        private readonly StackPanel _list;
        private readonly TextBlock _status;
        private List<Recipe> _items = new();
        private int _loadId;

        public RecipesPage(Ctx ctx)
        {
            _ctx = ctx;
            // Список готовится сразу после входа в игру, даже если раздел не открыт.
            _ctx.EnteredGame += async () => { await System.Threading.Tasks.Task.Delay(3000); if (_ctx.InGame) await Load(); };
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition());

            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var all = Theme.Button("Открыть все", Theme.Accent);
            all.Foreground = Brushes.White;
            all.Margin = new Thickness(10, 0, 0, 0);
            all.Click += async (_, _) =>
            {
                if (await _ctx.Request(new { cmd = "unlockallrecipes" }, "Все рецепты открыты") != null) await Load();
            };
            DockPanel.SetDock(all, Dock.Right);
            top.Children.Add(all);
            var refresh = Theme.Button("Обновить");
            refresh.Margin = new Thickness(10, 0, 0, 0);
            refresh.Click += async (_, _) => await Load();
            DockPanel.SetDock(refresh, Dock.Right);
            top.Children.Add(refresh);
            _search = new TextBox { ToolTip = "Поиск рецепта или ингредиента" };
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
            int id = ++_loadId;
            if (!_ctx.InGame)
            {
                _status.Text = "Загрузите сохранение в игре, чтобы увидеть рецепты.";
                _items.Clear();
                Render();
                return;
            }
            JsonElement r;
            try
            {
                r = await System.Threading.Tasks.Task.Run(() => _ctx.Client.Send(new { cmd = "recipes" }));
            }
            catch (Exception e)
            {
                _status.Text = Ctx.Friendly(e);
                return;
            }
            if (id != _loadId) return;
            _items = r.GetProperty("items").EnumerateArray()
                .Select(i => new Recipe(i.GetProperty("Index").GetInt32(), i.GetProperty("Name").GetString(),
                    i.GetProperty("Description").GetString() ?? "", i.GetProperty("Ingredients").GetString() ?? "",
                    i.GetProperty("Skill").GetInt32(), i.GetProperty("Dish").ValueKind == JsonValueKind.String ? i.GetProperty("Dish").GetString() : null,
                    i.GetProperty("Unlocked").GetBoolean(), i.GetProperty("Cooked").GetBoolean()))
                .ToList();
            Render();
        }

        private void Render()
        {
            _list.Children.Clear();
            string q = _search.Text.Trim();
            var found = _items.Where(b => q.Length == 0 || b.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                                                        || b.Ingredients.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList();
            if (_items.Count > 0)
                _status.Text = $"Рецептов: {_items.Count}, открыто: {_items.Count(b => b.Unlocked)}. Показано: {found.Count}. Открытые рецепты сохраняются вместе с игрой.";
            foreach (var b in found) _list.Children.Add(Row(b));
        }

        private FrameworkElement Row(Recipe rc)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var text = new StackPanel();
            text.Children.Add(Theme.Label(rc.Name, 14, Theme.Text, bold: true));
            string details = rc.Ingredients.Length > 0 ? "Нужно: " + rc.Ingredients : "";
            if (rc.Skill > 1) details += (details.Length > 0 ? " · " : "") + $"навык готовки {rc.Skill}";
            if (rc.Cooked) details += (details.Length > 0 ? " · " : "") + "уже готовили";
            if (details.Length > 0)
            {
                var d = Theme.Label(details, 11, Theme.Muted);
                d.TextWrapping = TextWrapping.Wrap;
                text.Children.Add(d);
            }
            if (rc.Description.Length > 0) text.ToolTip = rc.Description;
            g.Children.Add(text);

            var chip = new Border
            {
                Background = rc.Unlocked ? Theme.Hex("#1E3A2A") : Theme.Hex("#3A2A1E"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = Theme.Label(rc.Unlocked ? "открыт" : "закрыт", 11, rc.Unlocked ? Theme.Good : Theme.Warn),
            };
            Grid.SetColumn(chip, 1);
            g.Children.Add(chip);

            var open = Theme.Button("Открыть");
            open.IsEnabled = !rc.Unlocked;
            open.VerticalAlignment = VerticalAlignment.Center;
            open.Click += async (_, _) =>
            {
                if (await _ctx.Request(new { cmd = "unlockrecipe", index = rc.Index }, $"Рецепт открыт: {rc.Name}") != null) await Load();
            };
            Grid.SetColumn(open, 2);
            g.Children.Add(open);

            if (rc.Dish != null)
            {
                var give = Theme.Button("Выдать блюдо");
                give.Margin = new Thickness(6, 0, 0, 0);
                give.VerticalAlignment = VerticalAlignment.Center;
                give.Click += async (_, _) => await _ctx.Request(new { cmd = "spawn", item = rc.Dish, count = 1 }, $"Выдано: {rc.Name}");
                Grid.SetColumn(give, 3);
                g.Children.Add(give);
            }

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
