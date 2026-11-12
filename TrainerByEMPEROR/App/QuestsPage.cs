using System;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TrainerByEMPEROR.App
{
    // Раздел «Задания»: активные задания (в выживании — все линейки «Сказаний») и их задачи.
    internal sealed class QuestsPage : Grid
    {
        private readonly Ctx _ctx;
        private readonly StackPanel _list;
        private readonly TextBlock _status;

        public QuestsPage(Ctx ctx)
        {
            _ctx = ctx;
            // Список готовится сразу после входа в игру, даже если раздел не открыт.
            _ctx.EnteredGame += async () => { await System.Threading.Tasks.Task.Delay(3000); if (_ctx.InGame) await Load(); };
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition());

            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var refresh = Theme.Button("Обновить");
            refresh.Margin = new Thickness(10, 0, 0, 0);
            refresh.Click += async (_, _) => await Load();
            DockPanel.SetDock(refresh, Dock.Right);
            top.Children.Add(refresh);
            _status = Theme.Label("", 12, Theme.Muted);
            top.Children.Add(_status);
            Children.Add(top);

            _list = new StackPanel();
            var scroll = new ScrollViewer { Content = _list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 10, 0) };
            SetRow(scroll, 1);
            Children.Add(scroll);
        }

        public async void Show() => await Load();

        private int _loadId;

        private async System.Threading.Tasks.Task Load()
        {
            // Список могут запросить сразу из нескольких мест (вход в игру, открытие раздела) — показываем только последний ответ.
            int id = ++_loadId;
            if (!_ctx.InGame)
            {
                _list.Children.Clear();
                _status.Text = "Загрузите сохранение в игре, чтобы увидеть задания.";
                return;
            }

            JsonElement r;
            try
            {
                r = await System.Threading.Tasks.Task.Run(() => _ctx.Client.Send(new { cmd = "quests" }));
            }
            catch (Exception e)
            {
                _status.Text = Ctx.Friendly(e);
                return;
            }

            if (id != _loadId) return;
            _list.Children.Clear();
            var missions = r.GetProperty("missions").EnumerateArray().ToList();
            _status.Text = missions.Count == 0
                ? "Активных заданий нет."
                : $"Активных заданий: {missions.Count}. Перед завершением задач лучше сделать копию сохранения.";
            foreach (var m in missions) _list.Children.Add(MissionCard(m));
        }

        private FrameworkElement MissionCard(JsonElement m)
        {
            string id = m.GetProperty("Id").GetString();
            string name = m.GetProperty("Name").GetString();
            string chapter = m.GetProperty("Chapter").GetString();
            bool side = m.GetProperty("Side").GetBoolean();

            var card = new StackPanel();
            var head = new DockPanel();
            var finish = Theme.Button("Завершить задание целиком");
            finish.Click += async (_, _) =>
            {
                if (MessageBox.Show($"Завершить задание «{name}» целиком?", "TrainerByEMPEROR", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                if (await _ctx.Request(new { cmd = "completemission", id }, $"Задание завершено: {name}") != null) await Load();
            };
            DockPanel.SetDock(finish, Dock.Right);
            head.Children.Add(finish);
            var title = new StackPanel();
            title.Children.Add(Theme.Label(name, 16, Theme.Text, bold: true));
            string sub = (side ? "Побочное задание" : "Основное задание") + (string.IsNullOrWhiteSpace(chapter) ? "" : " · " + chapter);
            title.Children.Add(Theme.Label(sub, 12, Theme.Muted));
            head.Children.Add(title);
            card.Children.Add(head);

            var objectives = m.GetProperty("Objectives").EnumerateArray().ToList();
            if (objectives.Count == 0) card.Children.Add(Pad(Theme.Label("Задачи пока не открыты.", 12, Theme.Muted)));
            foreach (var o in objectives) card.Children.Add(ObjectiveRow(o));
            return Theme.CardBorder(card);
        }

        private FrameworkElement ObjectiveRow(JsonElement o)
        {
            string id = o.GetProperty("Id").GetString();
            string name = o.GetProperty("Name").GetString();
            bool done = o.GetProperty("Completed").GetBoolean();
            bool child = o.GetProperty("Child").GetBoolean();

            var g = new Grid { Margin = new Thickness(child ? 28 : 0, 10, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var mark = Theme.Label(done ? "✓" : "○", 15, done ? Theme.Good : Theme.Muted, bold: true);
            mark.Margin = new Thickness(0, 0, 10, 0);
            g.Children.Add(mark);

            var text = Theme.Label(name, 13, done ? Theme.Muted : Theme.Text);
            SetColumn(text, 1);
            g.Children.Add(text);

            var b = Theme.Button(done ? "Выполнено" : "Завершить", done ? null : Theme.Accent);
            b.IsEnabled = !done;
            if (!done) b.Foreground = Brushes.White;
            b.Margin = new Thickness(10, 0, 0, 0);
            b.Click += async (_, _) =>
            {
                if (await _ctx.Request(new { cmd = "completeobjective", id }, $"Задача завершена: {name}") != null) await Load();
            };
            SetColumn(b, 2);
            g.Children.Add(b);
            return g;
        }

        private static FrameworkElement Pad(FrameworkElement e)
        {
            e.Margin = new Thickness(0, 10, 0, 0);
            return e;
        }
    }
}
