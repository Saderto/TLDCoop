using System;
using TLDCoop.Game;
using UnityEngine;

namespace TLDCoop.UI
{
    // Вкладка «Настройки сервера» (только у хоста). Всё меняется сразу, во время игры,
    // и через пару секунд действует у всех игроков. Новое правило = новая строка здесь.
    // Тексты на двух языках (UI/Lang.cs), вид как у меню игры (UI/Widgets.cs).
    internal static class HostPanel
    {
        private static string[] PlaceNames => new[]
        {
            L.T("в точке старта этой локации", "at this location's start point"),
            L.T("рядом с другим игроком", "next to another player"),
            L.T("в точке старта любой локации", "at any location's start point"),
            L.T("на месте смерти", "at the place of death"),
        };

        // Настроек много, поэтому окно на этой вкладке широкое и они идут в две колонки
        // (на узком экране — одной колонкой).
        public static void Draw(float lx, ref float cy, float cw)
        {
            // Правило поменяли — сразу в файл, а не только при закрытии окна (вылет, выход с открытым окном).
            string before = Snapshot();
            DrawColumns(lx, ref cy, cw);
            if (Snapshot() != before) Rules.SaveHost();
        }

        private static string Snapshot()
        {
            using var ms = new System.IO.MemoryStream();
            using (var w = new System.IO.BinaryWriter(ms)) Rules.Host.Write(w);
            return Convert.ToBase64String(ms.ToArray());
        }

        private static void DrawColumns(float lx, ref float cy, float cw)
        {
            if (cw < 760f)
            {
                DrawLeft(lx, ref cy, cw);
                DrawRight(lx, ref cy, cw);
                return;
            }
            float col = (cw - 40f) / 2f;
            float left = cy, right = cy;
            DrawLeft(lx, ref left, col);
            DrawRight(lx + col + 40f, ref right, col);
            cy = Math.Max(left, right);
        }

        private static void DrawLeft(float lx, ref float cy, float cw)
        {
            var r = Rules.Host;
            float half = cw / 2f;

            Header(lx, ref cy, cw, L.T("Мир", "World"));
            Config.SyncTime = Widgets.Toggle(new Rect(lx, cy, half, 24f), Config.SyncTime, L.T("Общее время суток", "Shared time of day"));
            Config.SyncWeather = Widgets.Toggle(new Rect(lx + half, cy, half, 24f), Config.SyncWeather, L.T("Общая погода", "Shared weather"));
            cy += 28f;
            Config.AllowTeleport = Widgets.Toggle(new Rect(lx, cy, cw, 24f), Config.AllowTeleport, L.T("Разрешить телепорт к другим игрокам", "Allow teleporting to other players"));
            cy += 26f;
            Note(lx, ref cy, cw, L.T("В окне мода у каждого игрока в списке появится кнопка «Телепорт к» (работает в одной локации).",
                "Each player in the mod window gets a “Teleport” button (works within one location)."));

            // Хост решает за всех: у игроков вместо своей галочки надпись «вкл/выкл хостом».
            Header(lx, ref cy, cw, L.T("Что видят и слышат все", "What everyone sees and hears"));
            string[] states = { L.T("каждый решает сам", "each player decides"), L.T("у всех включено", "on for everyone"), L.T("у всех выключено", "off for everyone") };
            var shown = ShownNames;
            for (int i = 0; i < Config.ShownCount; i++)
                Config.SetForAll((Shown)i, Cycle(lx, ref cy, cw, shown[i], Config.ForAll((Shown)i), states));

            Header(lx, ref cy, cw, L.T("Управление", "Control"));
            if (Game.GameApi.InGame())
            {
                float q = (cw - 18f) / 4f;
                Widgets.Label(new Rect(lx, cy, cw, 20f), L.T("Время суток", "Time of day"), 12f, Theme.Caption, TextAnchor.MiddleLeft);
                cy += 22f;
                string[] times = { L.T("Утро 7:00", "Morning 7:00"), L.T("День 12:00", "Noon 12:00"), L.T("Вечер 18:00", "Evening 18:00"), L.T("Ночь 23:00", "Night 23:00") };
                float[] hours = { 7f, 12f, 18f, 23f };
                for (int i = 0; i < 4; i++)
                    if (Widgets.Button(new Rect(lx + i * (q + 6f), cy, q, 28f), times[i], false, 13f)) Game.GameApi.SetTimeOfDay(hours[i] / 24f);
                cy += 36f;
                Widgets.Label(new Rect(lx, cy, cw, 20f), L.T("Погода (у всех сразу)", "Weather (for everyone at once)"), 12f, Theme.Caption, TextAnchor.MiddleLeft);
                cy += 22f;
                string[] weather = { L.T("Ясно", "Clear"), L.T("Облачно", "Cloudy"), L.T("Снег", "Snow"), L.T("Сильный снег", "Heavy snow"), L.T("Метель", "Blizzard"), L.T("Туман", "Fog") };
                int[] stages = { 4, 5, 1, 2, 7, 0 };
                float t = (cw - 12f) / 3f;
                for (int i = 0; i < 6; i++)
                    if (Widgets.Button(new Rect(lx + (i % 3) * (t + 6f), cy + (i / 3) * 34f, t, 28f), weather[i], false, 13f)) Game.GameApi.SetWeather(stages[i]);
                cy += 72f;
            }
            else Note(lx, ref cy, cw, L.T("Время и погоду можно менять, когда ты в игре.", "Time and weather can be changed while you are in the game."));
            bool anyone = false;
            foreach (var pl in new System.Collections.Generic.List<Net.Player>(Net.Session.Players.Values))
            {
                anyone = true;
                Widgets.Label(new Rect(lx, cy, cw - 130f, 28f), Widgets.Fit(pl.Name, cw - 130f, 15f), 15f, Theme.White, TextAnchor.MiddleLeft);
                if (Widgets.Button(new Rect(lx + cw - 120f, cy, 120f, 28f), L.T("Выгнать", "Kick"), false, 13f)) Net.Session.Kick(pl.Id);
                cy += 34f;
            }
            if (!anyone) Note(lx, ref cy, cw, L.T("Выгнать игрока можно здесь, когда кто-то подключён.", "You can kick a player here once someone has joined."));

            Header(lx, ref cy, cw, L.T("Смерть: где игрок воскреснет", "Death: where the player comes back"));
            // 0 = выбирает сам, 1..4 = всегда в одном месте (Place + 1).
            var places = PlaceNames;
            string always = L.T("всегда ", "always ");
            int where = r.PlayerChoosesPlace ? 0 : (int)r.DefaultPlace + 1;
            int next = Cycle(lx, ref cy, cw, L.T("Место", "Place"), where, new[]
                { L.T("умерший выбирает сам", "the player chooses"), always + places[0], always + places[1], always + places[2], always + places[3] });
            if (next != where)
            {
                r.PlayerChoosesPlace = next == 0;
                if (next > 0)
                {
                    r.DefaultPlace = (Place)(next - 1);
                    r.PlacesAllowed |= (byte)(1 << (next - 1));
                }
            }
            if (r.PlayerChoosesPlace)
            {
                Note(lx, ref cy, cw, L.T("На экране смерти игрок выбирает из отмеченных вариантов:", "On the death screen the player picks from the ticked options:"));
                for (int i = 0; i < places.Length; i++)
                {
                    r.PlacesAllowed = Bit(lx + 12f, cy, cw - 12f, r.PlacesAllowed, (Place)i, places[i]);
                    cy += 26f;
                }
                if (r.PlacesAllowed == 0) r.PlacesAllowed = 1 << (int)Place.NearPlayer;
                var allowed = new System.Collections.Generic.List<int>();
                for (int i = 0; i < places.Length; i++) if (r.IsAllowed((Place)i)) allowed.Add(i);
                int cur = Math.Max(0, allowed.IndexOf((int)r.DefaultPlace));
                int pick = Cycle(lx, ref cy, cw, L.T("Не выбрал за 30 секунд", "No choice in 30 seconds"), cur, allowed.ConvertAll(i => places[i]).ToArray());
                r.DefaultPlace = (Place)allowed[pick];
            }
        }

        private static void DrawRight(float lx, ref float cy, float cw)
        {
            var r = Rules.Host;
            Header(lx, ref cy, cw, L.T("Правила", "Rules"));

            r.Downed = Widgets.Toggle(new Rect(lx, cy, cw, 24f), r.Downed, L.T("Сначала «ранен»", "“Downed” first"));
            cy += 24f;
            Note(lx + 27f, ref cy, cw - 27f, L.T("Друг рядом может поднять за 15 секунд.", "A friend nearby can pick you up within 15 seconds."));
            if (r.Downed)
            {
                r.ReviveItems = Widgets.Toggle(new Rect(lx, cy, cw, 24f), r.ReviveItems, L.T("Чтобы поднять, нужен лут по причине", "Picking up needs items for the cause"));
                cy += 24f;
                Note(lx + 27f, ref cy, cw - 27f, L.T("Бинты, лекарства и т. п. — смотря от чего ранен.", "Bandages, medicine and so on, depending on the cause."));
            }
            r.FriendlyFire = Widgets.Toggle(new Rect(lx, cy, cw, 24f), r.FriendlyFire, L.T("Урон по своим: пули и стрелы ранят друзей", "Friendly fire: bullets and arrows hurt friends"));
            cy += 28f;
            r.SoloSleep = Widgets.Toggle(new Rect(lx, cy, cw, 24f), r.SoloSleep, L.T("Можно спать отдельно", "Sleeping alone allowed"));
            cy += 24f;
            Note(lx + 27f, ref cy, cw - 27f, L.T("Выбор в меню отдыха, общее время при этом не проматывается.", "Chosen in the rest menu; shared time does not skip ahead."));
            r.SharedMap = Widgets.Toggle(new Rect(lx, cy, cw, 24f), r.SharedMap, L.T("Общая карта", "Shared map"));
            cy += 24f;
            Note(lx + 27f, ref cy, cw - 27f, L.T("Зарисовал местность углём — она открылась на карте у всех.", "Map an area with charcoal and it opens on everyone's map."));
            r.QuestWait = Widgets.Toggle(new Rect(lx, cy, cw, 24f), r.QuestWait, L.T("Задания вместе", "Quests together"));
            cy += 24f;
            Note(lx + 27f, ref cy, cw - 27f, L.T("Пункт задания у ушедшего вперёд не закроется, пока его не сделают остальные.",
                "A quest step does not close for whoever is ahead until the others finish it too."));

            Header(lx, ref cy, cw, L.T("Смерть: с чем игрок воскреснет", "Death: what the player comes back with"));
            r.Loot = (LootMode)Cycle(lx, ref cy, cw, L.T("Вещи умершего", "Player's items"), (int)r.Loot,
                new[] { L.T("остаются у него", "stay with them"), L.T("лежат на месте смерти", "lie at the place of death"), L.T("пропадают", "are lost") });
            if (r.Loot == LootMode.AtDeathSpot) Note(lx, ref cy, cw, L.T("Можно вернуться и забрать.", "You can go back and pick them up."));
            r.StarterKit = Widgets.Toggle(new Rect(lx, cy, cw, 24f), r.StarterKit, L.T("Дать стартовый набор, как в новой игре", "Give a starter kit, as in a new game"));
            cy += 30f;
            r.HealthPercent = Pick(lx, ref cy, cw, L.T("Здоровье", "Health"), r.HealthPercent, new byte[] { 100, 50, 25 }, "%");
            r.SkillLossPercent = Pick(lx, ref cy, cw, L.T("Навыки", "Skills"), r.SkillLossPercent, new byte[] { 0, 25, 50, 75, 100 }, "%",
                v => v == 0 ? L.T("не теряются", "kept") : L.T("теряется ", "lose ") + v + L.T("% уровней", "% of levels"));
            Note(lx, ref cy, cw, L.T("Стрелками справа меняется значение. Меняется сразу у всех.", "Use the arrows on the right to change a value. It changes for everyone at once."));

            Header(lx, ref cy, cw, L.T("Звери", "Animals"));
            r.AnimalsSeeAll = Widgets.Toggle(new Rect(lx, cy, cw, 24f), r.AnimalsSeeAll, L.T("Звери видят всех игроков", "Animals see every player"));
            cy += 24f;
            Note(lx + 27f, ref cy, cw - 27f, L.T("Зверь идёт на ближайшего из вас и переезжает к другу только в момент броска. Выключи, если что-то дёргается: тогда как в 0.12.",
                "An animal goes for the nearest of you and switches to a friend only when it lunges. Turn it off if something jitters: then it works as in 0.12."));
            Header(lx, ref cy, cw, L.T("Звери: запас здоровья", "Animals: health"));
            var steps = new byte[] { 4, 5, 6, 8, 10, 12 };
            var names = new[] { "×1", L.T("×1,25", "×1.25"), L.T("×1,5", "×1.5"), "×2", L.T("×2,5", "×2.5"), "×3" };
            var animals = AnimalNames;
            for (int i = 0; i < r.AnimalHp.Length; i++)
            {
                int cur = Math.Max(0, Array.IndexOf(steps, r.AnimalHp[i]));
                r.AnimalHp[i] = steps[Cycle(lx, ref cy, cw, i < animals.Length ? animals[i] : Rules.AnimalNames[i], cur, names)];
            }
            Note(lx, ref cy, cw, L.T("Урон не меняется, только сколько зверь выдерживает. Действует на зверей, которые появятся после изменения.",
                "Damage stays the same, only how much an animal can take. Applies to animals that appear after the change."));
        }

        private static string[] AnimalNames => L.En
            ? new[] { "Wolf", "Timberwolf", "Bear", "Moose", "Cougar", "Deer", "Rabbit" }
            : Rules.AnimalNames;

        private static void Note(float lx, ref float cy, float cw, string text) =>
            Widgets.Text(lx, ref cy, cw, text, 4f, 12f, Theme.Dim);

        private static void Header(float lx, ref float cy, float cw, string text)
        {
            cy += 4f;
            Widgets.Section(lx, ref cy, cw, text);
        }

        private static byte Bit(float x, float y, float w, byte mask, Place p, string label)
        {
            int bit = 1 << (int)p;
            bool on = Widgets.Toggle(new Rect(x, y, w, 24f), (mask & bit) != 0, label);
            return (byte)(on ? mask | bit : mask & ~bit);
        }

        private static string[] ShownNames => new[]
        {
            L.T("Ники над головами", "Names above heads"), L.T("Чем заняты игроки", "What players are doing"), L.T("Фразы других", "Others' voice lines"),
            L.T("Чат над головой", "Chat above heads"), L.T("Стрелка к друзьям", "Arrow to friends"), L.T("Друзья на карте", "Friends on the map"),
            L.T("Звуки друзей", "Friends' sounds"), L.T("Здоровье под ником", "Health under names"),
        };

        // Строка «Название   ‹ значение ›», как настройки в меню игры.
        private static int Cycle(float lx, ref float cy, float cw, string label, int value, string[] names)
        {
            value = Math.Clamp(value, 0, names.Length - 1);
            float lw = cw * 0.40f;
            Widgets.Label(new Rect(lx, cy, lw, 26f), Widgets.Fit(label, lw, 13f), 13f, Theme.Caption, TextAnchor.MiddleLeft);
            int d = Widgets.Arrows(new Rect(lx + lw, cy, cw - lw, 26f), Widgets.Fit(names[value], cw - lw - 56f, 14f), 14f);
            if (d != 0) value = (value + d + names.Length) % names.Length;
            cy += 30f;
            return value;
        }

        private static byte Pick(float lx, ref float cy, float cw, string label, byte value, byte[] options, string suffix,
            Func<byte, string> name = null)
        {
            int i = Array.IndexOf(options, value);
            if (i < 0) i = 0;
            var names = Array.ConvertAll(options, o => name != null ? name(o) : o + suffix);
            return options[Cycle(lx, ref cy, cw, label, i, names)];
        }
    }
}
