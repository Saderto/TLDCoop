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
    // Раздел «Значки»: значки испытаний и подвиги из меню игры «Значки». Хранятся в профиле, а не в сохранении.
    internal sealed class BadgesPage : Grid
    {
        private sealed record Badge(int Id, string Name, string Description, bool Unlocked);

        private readonly Ctx _ctx;
        private readonly StackPanel _list;
        private readonly TextBlock _status;
        private int _loadId;

        public BadgesPage(Ctx ctx)
        {
            _ctx = ctx;
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition());

            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var refresh = Theme.Button(L.T("Обновить", "Refresh"));
            refresh.Margin = new Thickness(10, 0, 0, 0);
            refresh.Click += async (_, _) => await Load();
            DockPanel.SetDock(refresh, Dock.Right);
            top.Children.Add(refresh);
            _status = Theme.Label("", 12, Theme.Muted);
            _status.VerticalAlignment = VerticalAlignment.Center;
            top.Children.Add(_status);
            Children.Add(top);

            _list = new StackPanel();
            var scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 10, 0) };
            SetRow(scroll, 1);
            Children.Add(scroll);
        }

        public async void Show() => await Load();

        private async System.Threading.Tasks.Task Load()
        {
            int id = ++_loadId;
            if (!_ctx.Connected)
            {
                _list.Children.Clear();
                _status.Text = L.T("Запустите игру: значки выдаются прямо в ней (можно из главного меню).", "Start the game: badges are given inside it (the main menu works too).");
                return;
            }
            JsonElement r;
            try
            {
                r = await System.Threading.Tasks.Task.Run(() => _ctx.Client.Send(new { cmd = "badges" }));
            }
            catch (Exception e)
            {
                _status.Text = Ctx.Friendly(e);
                return;
            }
            if (id != _loadId) return;
            var badges = Parse(r, "items");
            var feats = Parse(r, "feats");
            _list.Children.Clear();
            _status.Text = L.T("Значки и подвиги сохраняются в профиль игры, забрать их обратно трейнер не может.", "Badges and feats are saved to the game profile; the trainer cannot take them back.");

            _list.Children.Add(Header(L.T($"Испытания — получено {badges.Count(b => b.Unlocked)} из {badges.Count}", $"Challenges: {badges.Count(b => b.Unlocked)} of {badges.Count} earned"), "unlockallbadges", L.T("Все значки испытаний выданы", "All challenge badges given")));
            foreach (var b in badges) _list.Children.Add(Row(b, "unlockbadge", L.T("Значок выдан", "Badge given")));

            _list.Children.Add(Header(L.T($"Подвиги — открыто {feats.Count(b => b.Unlocked)} из {feats.Count}", $"Feats: {feats.Count(b => b.Unlocked)} of {feats.Count} unlocked"), "unlockallfeats", L.T("Все подвиги открыты", "All feats unlocked")));
            if (feats.Count == 0)
                _list.Children.Add(Theme.Label(L.T("Игра ещё не загрузила подвиги. Откройте главное меню или загрузите сохранение и нажмите «Обновить».", "The game has not loaded the feats yet. Open the main menu or load a save and press “Refresh”."), 12, Theme.Muted));
            foreach (var b in feats) _list.Children.Add(Row(b, "unlockfeat", L.T("Подвиг открыт", "Feat unlocked")));
        }

        private static List<Badge> Parse(JsonElement r, string prop) =>
            r.TryGetProperty(prop, out var arr)
                ? arr.EnumerateArray()
                    .Select(i => new Badge(i.GetProperty("Id").GetInt32(), i.GetProperty("Name").GetString(),
                        i.GetProperty("Description").GetString(), i.GetProperty("Unlocked").GetBoolean()))
                    .ToList()
                : new List<Badge>();

        private FrameworkElement Header(string title, string allCmd, string doneText)
        {
            var d = new DockPanel { Margin = new Thickness(0, 10, 0, 8) };
            var all = Theme.Button(L.T("Выдать все", "Give all"), Theme.Accent);
            all.Foreground = Brushes.White;
            all.Click += async (_, _) =>
            {
                if (await _ctx.Request(new { cmd = allCmd }, doneText) != null) await Load();
            };
            DockPanel.SetDock(all, Dock.Right);
            d.Children.Add(all);
            var label = Theme.Label(title, 16, Theme.Text, bold: true);
            label.VerticalAlignment = VerticalAlignment.Center;
            d.Children.Add(label);
            return d;
        }

        private FrameworkElement Row(Badge b, string cmd, string doneText)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(Theme.Label(b.Name, 14, Theme.Text, bold: true));
            if (!string.IsNullOrWhiteSpace(b.Description))
            {
                var d = Theme.Label(b.Description, 12, Theme.Muted);
                d.TextWrapping = TextWrapping.Wrap;
                text.Children.Add(d);
            }
            g.Children.Add(text);

            var chip = new Border
            {
                Background = b.Unlocked ? Theme.Hex("#1E3A2A") : Theme.Hex("#3A2A1E"),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = Theme.Label(b.Unlocked ? L.T("получен", "earned") : L.T("нет", "no"), 11, b.Unlocked ? Theme.Good : Theme.Warn),
            };
            Grid.SetColumn(chip, 1);
            g.Children.Add(chip);

            var give = Theme.Button(L.T("Выдать", "Give"));
            give.IsEnabled = !b.Unlocked;
            give.VerticalAlignment = VerticalAlignment.Center;
            give.Click += async (_, _) =>
            {
                if (await _ctx.Request(new { cmd, id = b.Id }, $"{doneText}: {b.Name}") != null) await Load();
            };
            Grid.SetColumn(give, 2);
            g.Children.Add(give);

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
