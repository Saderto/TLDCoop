using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using TLDCoop.Game;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.UI
{
    // Окно мода (по умолчанию клавиша \). Нарисовано IMGUI Unity, но картинками и шрифтом самой игры (UI/Theme.cs),
    // в точках экрана 1280×720, как её меню (UI/Widgets.cs). Тексты на двух языках (UI/Lang.cs).
    // Вкладка «Игра» — подключение, лобби, игроки, чат. Вкладка «Настройки сервера» — только у хоста.
    // Не влезает по высоте — содержимое прокручивается колёсиком.
    internal static class CoopWindow
    {
        public static bool Open;

        private static int _tab;               // 0 игра, 1 настройки сервера
        private static string _chat = "";
        private static string _port;
        private static bool _ipMode;
        private static List<Steam.Friend> _friends = new();
        private static float _nextFriends;
        private static float _height = 300f;   // высота окна с прошлого кадра
        private static float _content = 200f;  // высота содержимого с прошлого кадра
        private static float _scroll;

        private const float W = 520f, Pad = 26f;

        public static void Toggle()
        {
            Open = !Open;
            Widgets.Focus = null;
            _port ??= Config.Port.ToString();
            if (Open) NewGame.Refresh();
            else
            {
                Config.Save();
                Rules.SaveHost();
            }
        }

        // Пока окно открыто, нужен свободный курсор. Игра каждый кадр прячет его обратно.
        public static void KeepCursor()
        {
            if (!Open && !Game.Death.ScreenOpen && !Together.MenuOpen && !IslandSync.PromptOpen && !Game.Trade.PromptOpen) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Draw()
        {
            Theme.Apply();
            DrawLabels();
            try { Together.Draw(); } catch { }
            try { IslandSync.Draw(); } catch { }
            try { Game.Trade.DrawPrompt(); } catch { }
            try { Game.SleepSync.Draw(); } catch { }
            Widgets.Scaled();
            try
            {
                DrawBadge();
                DrawPauseSave();
                if (Open) DrawWindow();
            }
            finally { Widgets.Unscaled(); }
        }

        private static void DrawWindow()
        {
            // «Настройки сервера» не влезают в узкое окно — на этой вкладке оно широкое, в две колонки.
            float w = Session.Mode == Mode.Host && _tab == 1 ? Mathf.Clamp(Widgets.SW - 40f, W, 1000f) : W;
            float h = Math.Min(_height, Widgets.SH - 20f);
            // Сначала ставим окно в пределы экрана, потом берём его за заголовок именно там, где он нарисован.
            // (В 0.19.24 полоса для перетаскивания стояла по сохранённому месту, а окно рисовалось прижатым к краю:
            // старое место было в пикселях, а теперь точки 1280×720. Полоса попадала на вкладки и съедала их клик.)
            float x = Mathf.Clamp(Config.WindowX, 10f, Math.Max(10f, Widgets.SW - 10f - w));
            float y = Mathf.Clamp(Config.WindowY, 10f, Math.Max(10f, Widgets.SH - 10f - h));
            if (Widgets.Drag(new Rect(x, y, w, 44f), ref x, ref y, w, h)) Config.Save();
            x = Mathf.Clamp(x, 10f, Math.Max(10f, Widgets.SW - 10f - w));
            y = Mathf.Clamp(y, 10f, Math.Max(10f, Widgets.SH - 10f - h));
            if (Widgets.Dragging) { Config.WindowX = (int)x; Config.WindowY = (int)y; }   // иначе место не трогаем: широкая вкладка не сдвигает узкое окно
            float cy = Widgets.Window(new Rect(x, y, w, h), L.T("Кооп", "Co-op"), Version.Text);
            float lx = x + Pad, cw = w - Pad * 2f;

            if (Session.Mode == Mode.Host)
            {
                float tw = cw / 2f;
                if (Widgets.Tab(new Rect(lx, cy, tw, 30f), _tab == 0, L.T("Игра", "Game"))) _tab = 0;
                if (Widgets.Tab(new Rect(lx + tw, cy, tw, 30f), _tab == 1, L.T("Настройки сервера", "Server settings"))) _tab = 1;
                cy += 32f;
                Widgets.Line(lx, cy, cw);
                cy += 10f;
            }
            else _tab = 0;

            var view = new Rect(lx, cy, cw, y + h - 16f - cy);
            float top = cy;
            cy -= Widgets.BeginScroll(view, ref _scroll, _content);
            float start = cy;
            try { DrawContent(lx, ref cy, cw); }
            finally { Widgets.EndScroll(); }
            _content = cy - start;
            _height = top - y + _content + 16f;
        }

        private static void DrawContent(float lx, ref float cy, float cw)
        {
            if (_tab == 1) { HostPanel.Draw(lx, ref cy, cw); return; }

            Widgets.Text(lx, ref cy, cw, Session.Status, 8f, 15f, Theme.White);
            if (Session.Mode == Mode.Off) DrawConnect(lx, ref cy, cw);
            else if (Session.InLobby) DrawLobby(lx, ref cy, cw);
            else DrawInGame(lx, ref cy, cw);
            if (Bot.Active)
                Widgets.Text(lx, ref cy, cw, L.T("Это бот для проверки: он всё делает сам.", "This is a test bot: it does everything by itself."), 4f, 13f, Theme.Dim);

            Widgets.Section(lx, ref cy, cw, L.T("Персонаж", "Character"));
            Widgets.Row(lx, ref cy, cw, L.T("Модель игрока", "Player model"), Models.State, Theme.Body);
            if (!Characters.Available)
                Widgets.Text(lx, ref cy, cw, L.T("Нет файла Mods\\TLDCoop\\characters.bin. Без него друзья выглядят простой фигурой, без персонажей и анимаций. Закрой игру и запусти Mods\\TLDAssets.exe: он соберёт файл из WINTERMUTE твоей игры.",
                    "Mods\\TLDCoop\\characters.bin is missing. Without it friends look like a plain figure, with no characters or animations. Close the game and run Mods\\TLDAssets.exe: it builds the file from your game's WINTERMUTE."), 4f, 14f, Theme.Warn);
            else if (Characters.Broken)
                Widgets.Text(lx, ref cy, cw, "characters.bin: " + Characters.State + ". " + L.T("Скорее всего файл старый или битый: закрой игру и запусти Mods\\TLDAssets.exe заново.",
                    "The file is probably old or damaged: close the game and run Mods\\TLDAssets.exe again."), 4f, 14f, Theme.Warn);
            if (Characters.Ready && Characters.Defs.Count > 0)
            {
                // Каким персонажем тебя видят другие.
                int i = Characters.IndexOf(Config.Character);
                string who = i >= 0 ? Characters.Defs[i].Name : L.T("по очереди", "in turn");
                Widgets.Label(new Rect(lx, cy, cw * 0.45f, 26f), L.T("Меня видят как", "Others see me as"), 12f, Theme.Caption, TextAnchor.MiddleLeft);
                int d = Widgets.Arrows(new Rect(lx + cw - 200f, cy, 200f, 26f), who);
                if (d != 0)
                {
                    int n = Characters.Defs.Count;
                    int next = i + d;
                    next = next < -1 ? n - 1 : next >= n ? -1 : next;
                    Config.Character = next < 0 ? "" : Characters.Defs[next].Id;
                    Config.Save();
                }
                cy += 32f;
            }
            Config.ShowBadge = Widgets.Toggle(new Rect(lx, cy, cw, 24f), Config.ShowBadge, L.T("Значок «Кооп» в углу экрана", "“Co-op” badge in the screen corner"));
            cy += 30f;
        }

        // Кнопка хоста в меню паузы (Esc): сохранить мир и прогресс всех друзей.
        private static void DrawPauseSave()
        {
            if (Session.Mode != Mode.Host || !GameApi.InGame()) return;
            bool paused;
            try { paused = InterfaceManager.IsPanelEnabled<Panel_PauseMenu>(); } catch { return; }
            if (!paused) return;
            var r = new Rect(Widgets.SW / 2f - 180f, Widgets.SH - 78f, 360f, 36f);
            if (Widgets.Button(r, L.T("Сохранить (кооп): мир и прогресс друзей", "Save (co-op): world and friends' progress"))) PlayerProgress.HostSaveAll();
        }

        // ---------- Не подключён ----------

        private static void DrawConnect(float lx, ref float cy, float cw)
        {
            Widgets.Label(new Rect(lx, cy, 90f, 28f), L.T("Ник", "Name"), 12f, Theme.Caption, TextAnchor.MiddleLeft);
            Config.Name = Widgets.TextField(new Rect(lx + 90f, cy, cw - 90f, 28f), "name", Config.Name ?? "", 24);
            cy += 36f;

            if (Widgets.Toggle(new Rect(lx, cy, cw / 2f, 24f), !_ipMode, L.T("Через Steam", "Via Steam")) != !_ipMode) _ipMode = false;
            if (Widgets.Toggle(new Rect(lx + cw / 2f, cy, cw / 2f, 24f), _ipMode, L.T("По IP (запасной)", "By IP (fallback)")) != _ipMode) _ipMode = true;
            cy += 32f;

            bool inGame = GameApi.InGame();
            string hostLabel = inGame ? L.T("Перейти в кооп-режим: открыть этот мир друзьям", "Go co-op: open this world to friends")
                                      : L.T("Перейти в кооп-режим (создать лобби)", "Go co-op (create a lobby)");
            if (!_ipMode)
            {
                if (Widgets.Button(new Rect(lx, cy, cw, 34f), hostLabel)) Session.HostSteam();
                cy += 44f;
                Widgets.Section(lx, ref cy, cw, L.T("Друзья в кооп-режиме", "Friends in co-op mode"));
                RefreshFriends();
                var hosts = _friends.Where(f => f.Presence.StartsWith("host")).ToList();
                if (!Steam.Ready)
                    Widgets.Text(lx, ref cy, cw, "Steam: " + (Steam.Error ?? L.T("подключаюсь…", "connecting…")), 6f, 14f, Theme.Dim);
                else if (hosts.Count == 0)
                    Widgets.Text(lx, ref cy, cw, L.T("Никого. Друг должен быть в игре и перейти в кооп-режим.", "Nobody yet. A friend must be in the game and switch to co-op mode."), 6f, 14f, Theme.Dim);
                foreach (var f in hosts.OrderByDescending(InvitedMe).Take(6))
                {
                    bool inv = InvitedMe(f);
                    string line = f.Name + (inv ? L.T("  — приглашает тебя!", "  — invites you!") : "");
                    Widgets.Label(new Rect(lx, cy, cw - 150f, 30f), Widgets.Fit(line, cw - 150f, 15f), 15f, inv ? Theme.Good : Theme.White, TextAnchor.MiddleLeft);
                    if (Widgets.Button(new Rect(lx + cw - 140f, cy, 140f, 30f), inv ? L.T("Принять", "Accept") : L.T("Подключиться", "Join"), inv)) Session.JoinSteam(f.Id, f.Name);
                    cy += 36f;
                }
            }
            else
            {
                Widgets.Label(new Rect(lx, cy, 40f, 28f), "IP", 12f, Theme.Caption, TextAnchor.MiddleLeft);
                Config.Ip = Widgets.TextField(new Rect(lx + 40f, cy, cw - 170f, 28f), "ip", Config.Ip ?? "", 64);
                Widgets.Label(new Rect(lx + cw - 120f, cy, 50f, 28f), L.T("Порт", "Port"), 12f, Theme.Caption, TextAnchor.MiddleLeft);
                _port = Widgets.TextField(new Rect(lx + cw - 66f, cy, 66f, 28f), "port", _port ?? "", 5);
                cy += 38f;
                int port = int.TryParse(_port, out var p) && p > 0 && p < 65536 ? p : Protocol.DefaultPort;
                Config.Port = port;
                if (Widgets.Button(new Rect(lx, cy, cw / 2f - 6f, 32f), L.T("Подключиться", "Join"))) Session.JoinIp(Config.Ip.Trim(), port);
                if (Widgets.Button(new Rect(lx + cw / 2f + 6f, cy, cw / 2f - 6f, 32f), L.T("Стать хостом", "Host"))) Session.HostIp(port);
                cy += 42f;
                Widgets.Text(lx, ref cy, cw, L.T("Для игры по IP хосту нужен открытый порт или общая сеть (LAN/VPN). Обычно проще через Steam.",
                    "Playing by IP needs an open port on the host or a shared network (LAN/VPN). Steam is usually easier."), 6f, 13f, Theme.Dim);
            }
            DrawLocalToggles(lx, ref cy, cw, true);
            DrawQuickStart(lx, ref cy, cw);
        }

        private static void DrawQuickStart(float lx, ref float cy, float cw)
        {
            Widgets.Section(lx, ref cy, cw, L.T("Быстрый старт", "Quick start"));
            Config.QuickHost = Widgets.Toggle(new Rect(lx, cy, cw, 24f), Config.QuickHost, L.T("Загрузил свой мир — сразу открыть друзьям", "Loaded my world — open it to friends at once"));
            cy += 28f;
            string last = Config.LastHostName.Length > 0 ? Config.LastHostName : L.T("прошлому хосту", "the last host");
            Config.QuickJoin = Widgets.Toggle(new Rect(lx, cy, cw, 24f), Config.QuickJoin, L.T("Сразу подключаться к ", "Join at once: ") + last);
            cy += 30f;
        }

        // Что показывать и слышать у себя: ники, надписи о действиях, фразы других игроков.
        private static void DrawLocalToggles(float lx, ref float cy, float cw, bool namesFree)
        {
            Widgets.Section(lx, ref cy, cw, L.T("Что показывать", "What to show"));
            float half = cw / 2f;
            Toggle(new Rect(lx, cy, half, 24f), Shown.Names, L.T("Ники над головами", "Names above heads"), v => Config.ShowNames = v);
            Toggle(new Rect(lx + half, cy, half, 24f), Shown.Actions, L.T("Чем заняты игроки", "What players are doing"), v => Config.ShowActions = v);
            cy += 26f;
            Toggle(new Rect(lx, cy, half, 24f), Shown.Voices, L.T("Фразы других (до 20 м)", "Others' voice lines (20 m)"), v => Config.HearVoices = v);
            Toggle(new Rect(lx + half, cy, half, 24f), Shown.Bubbles, L.T("Чат над головой", "Chat above heads"), v => Config.ShowBubbles = v);
            cy += 26f;
            Toggle(new Rect(lx, cy, half, 24f), Shown.Arrows, L.T("Стрелка к друзьям", "Arrow to friends"), v => Config.ShowArrows = v);
            Toggle(new Rect(lx + half, cy, half, 24f), Shown.Map, L.T("Друзья на карте", "Friends on the map"), v => Config.ShowOnMap = v);
            cy += 26f;
            Toggle(new Rect(lx, cy, half, 24f), Shown.Sounds, L.T("Звуки друзей", "Friends' sounds"), v => Config.HearSounds = v);
            Toggle(new Rect(lx + half, cy, half, 24f), Shown.Status, L.T("Здоровье под ником", "Health under names"), v => Config.ShowStatus = v);
            cy += 26f;
            Config.SystemChat = Widgets.Toggle(new Rect(lx, cy, half, 24f), Config.SystemChat, L.T("Сообщения мода в чате", "Mod messages in chat"));
            cy += 32f;
            if (Game.Together.HasOwnMapMarker)
            {
                if (Widgets.Button(new Rect(lx, cy, 260f, 30f), L.T("Убрать мою метку на карте", "Remove my map marker"))) Game.Together.RemoveOwnMapMarker();
                cy += 38f;
            }
            Widgets.Label(new Rect(lx, cy, 120f, 26f), L.T("Громкость фраз", "Voice volume"), 12f, Theme.Caption, TextAnchor.MiddleLeft);
            int dv = Widgets.Arrows(new Rect(lx + 118f, cy, 104f, 26f), Config.VoiceVolume + "%");
            if (dv != 0) Config.VoiceVolume += dv * 10;
            Widgets.Label(new Rect(lx + half + 4f, cy, 90f, 26f), L.T("Цвет ника", "Name colour"), 12f, Theme.Caption, TextAnchor.MiddleLeft);
            float sx = lx + half + 92f, step = Math.Min(22f, (cw - (sx - lx)) / Math.Max(1, Config.Palette.Length));
            for (int i = 0; i < Config.Palette.Length; i++)
            {
                string c = Config.Palette[i];
                var col = c.Length == 0 ? new Color(0.93f, 0.93f, 0.93f, 0.9f) : ParseHex(c);
                if (Widgets.Swatch(new Rect(sx + i * step + 3f, cy + 6f, step - 7f, 14f), col, Config.NickColor == c)) Config.NickColor = c;
            }
            cy += 38f;
            if (Widgets.Button(new Rect(lx, cy, cw / 2f - 6f, 32f), L.T("Собрать логи", "Collect logs"))) Game.Diagnostics.CollectLogs();
            if (Session.Mode != Mode.Off && Widgets.Button(new Rect(lx + cw / 2f + 6f, cy, cw / 2f - 6f, 32f), L.T("Починить управление", "Fix controls")))
                try { Game.StruggleGuard.Repair(true); } catch (Exception e) { MelonLoader.MelonLogger.Warning("[схватка] починка: " + e.Message); }
            cy += 40f;
            if (Game.Diagnostics.LastResult.Length > 0) Widgets.Text(lx, ref cy, cw, Game.Diagnostics.LastResult, 4f, 13f, Theme.Body);
            if (Game.Load.Summary.Length > 0) Widgets.Text(lx, ref cy, cw, L.T("Нагрузка мода: ", "Mod load: ") + Game.Load.Summary, 4f, 12f, Theme.Dim);
            string key = Config.PingKeyName;
            if (key.Length > 0) key = char.ToUpperInvariant(key[0]) + key.Substring(1);
            Widgets.Text(lx, ref cy, cw, key + L.T(": рядом с раненым другом держи — лечить.", ": hold next to a downed friend to heal."), 6f, 12f, Theme.Dim, TextAnchor.UpperCenter);
        }

        private static Color ParseHex(string hex)
        {
            try
            {
                int v = Convert.ToInt32(hex, 16);
                return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 0.95f);
            }
            catch { return Color.white; }
        }

        // Своя галочка, или надпись, если хост решил за всех (UI/HostPanel.cs).
        private static void Toggle(Rect r, Shown what, string label, Action<bool> set)
        {
            int p = Session.Policy(what);
            if (p == 0) { bool v = Config.Own(what); bool nv = Widgets.Toggle(r, v, label); if (nv != v) set(nv); }
            else
            {
                string s = label + ": " + (p == 1 ? L.T("вкл. хостом", "on by host") : L.T("выкл. хостом", "off by host"));
                Widgets.Label(r, Widgets.Fit(s, r.width, 13f), 13f, Theme.Dim, TextAnchor.MiddleLeft);
            }
        }

        private static bool InvitedMe(Steam.Friend f) =>
            f.Presence.StartsWith("host|") && f.Presence.Substring(5).Split(',').Contains(Steam.MySteamId.ToString());

        private static void RefreshFriends()
        {
            float now = Time.unscaledTime;
            if (now < _nextFriends) return;
            _nextFriends = now + 2f;
            if (!Steam.Ready && !Steam.Init()) return;
            try
            {
                _friends = Steam.FriendsInGame(now);
            }
            catch (Exception e)
            {
                _friends = new List<Steam.Friend>();
                MelonLoader.MelonLogger.Warning("[steam] список друзей: " + e.Message);
            }
        }

        // Уведомление о приглашении, даже если окно закрыто.
        private static readonly HashSet<ulong> Notified = new();
        private static float _nextInviteCheck;

        public static void CheckInvites()
        {
            if (Session.Mode != Mode.Off || !Steam.Ready) return;
            float now = Time.unscaledTime;
            if (now < _nextInviteCheck) return;
            _nextInviteCheck = now + 3f;
            try
            {
                _friends = Steam.FriendsInGame(now);
                foreach (var f in _friends)
                {
                    if (!InvitedMe(f) || !Notified.Add(f.Id)) continue;
                    string text = f.Name + L.T(" приглашает тебя в кооп. Нажми ", " invites you to co-op. Press ") + Config.MenuKeyName;
                    Toast.Show(text);
                    GameApi.HudMessage(text);
                }
            }
            catch
            {
            }
        }

        // ---------- Лобби (подключены, но ещё в главном меню) ----------

        private static void DrawLobby(float lx, ref float cy, float cw)
        {
            DrawPlayerList(lx, ref cy, cw, false);

            if (Session.Mode == Mode.Host)
            {
                if (Session.ViaSteam) DrawInviteList(lx, ref cy, cw);
                DrawStart(lx, ref cy, cw);
            }
            else
            {
                Widgets.Text(lx, ref cy, cw, Session.JoinInfo.Length > 0 ? Session.JoinInfo : L.T("Ждём, пока хост начнёт игру…", "Waiting for the host to start the game…"), 6f, 14f, Theme.Body);
            }
            // Те же настройки, что до перехода в кооп-режим (0.19.30): что показывать, громкость, цвет ника, логи, быстрый старт.
            Widgets.Row(lx, ref cy, cw, L.T("Ник", "Name"), Config.Name ?? "", Theme.White);
            DrawLocalToggles(lx, ref cy, cw, Session.NamesPolicy == 0);
            DrawQuickStart(lx, ref cy, cw);
            DrawChat(lx, ref cy, cw, 5);
            if (Widgets.Button(new Rect(lx, cy, cw, 32f), L.T("Выйти из кооп-режима", "Leave co-op mode"))) Session.Stop();
            cy += 40f;
        }

        private static void DrawInviteList(float lx, ref float cy, float cw)
        {
            RefreshFriends();
            var connected = new HashSet<string>(Session.Players.Values.Select(p => p.Name));
            var list = _friends.Where(f => !f.Presence.StartsWith("join:" + Steam.MySteamId) && !connected.Contains(f.Name)).Take(6).ToList();
            Widgets.Section(lx, ref cy, cw, L.T("Пригласить друзей (сейчас в игре)", "Invite friends (in game now)"));
            if (list.Count == 0)
                Widgets.Text(lx, ref cy, cw, L.T("Нет друзей в The Long Dark.", "No friends in The Long Dark."), 6f, 14f, Theme.Dim);
            foreach (var f in list)
            {
                Widgets.Label(new Rect(lx, cy, cw - 150f, 30f), Widgets.Fit(f.Name, cw - 150f, 15f), 15f, Theme.White, TextAnchor.MiddleLeft);
                bool inv = Session.IsInvited(f.Id);
                if (Widgets.Button(new Rect(lx + cw - 140f, cy, 140f, 30f), inv ? L.T("Приглашён", "Invited") : L.T("Пригласить", "Invite"), false, 14f, false, inv) && !inv) Session.Invite(f.Id);
                cy += 36f;
            }
            cy += 4f;
        }

        // Хост начинает игру как в одиночной: через меню игры. Мод сам позовёт друзей.
        private static void DrawStart(float lx, ref float cy, float cw)
        {
            Widgets.Text(lx, ref cy, cw, L.T(
                "Когда все в лобби, начни игру как обычно: «Режим выживания» → новая игра или загрузка сохранения. Друзья загрузятся в твой мир сами.",
                "When everyone is in the lobby, start the game as usual: Survival → new game or load a save. Friends will load into your world by themselves."), 6f, 14f, Theme.Body);
        }

        // ---------- В игре ----------

        private static void DrawInGame(float lx, ref float cy, float cw)
        {
            if (Session.Mode == Mode.Host && SyncCheck.LastResult.Length > 0)
                Widgets.Row(lx, ref cy, cw, L.T("Проверка мира", "World check"), SyncCheck.LastResult, Theme.Body);
            DrawPlayerList(lx, ref cy, cw, true);
            // Ошибка в одном разделе не должна прятать всё окно ниже (так пропадали цвет ника и настройки).
            try { DrawSummary(lx, ref cy, cw); } catch (Exception e) { MelonLoader.MelonLogger.Warning("[окно] итоги: " + e.Message); }
            try { DrawChallenges(lx, ref cy, cw); } catch (Exception e) { MelonLoader.MelonLogger.Warning("[окно] испытания: " + e.Message); }

            if (Session.Mode == Mode.Client)
            {
                if (Session.JoinInfo.Length > 0) Widgets.Text(lx, ref cy, cw, Session.JoinInfo, 4f, 14f, Theme.Body);
                // В своём сохранении хоста рядом нет: кнопка только в его мире.
                if (Session.InSharedWorld && Widgets.Button(new Rect(lx, cy, cw / 2f - 6f, 32f), L.T("Встать рядом с хостом", "Stand next to host"))) Session.MeetHost();
                if (Widgets.Button(new Rect(lx + cw / 2f + 6f, cy, cw / 2f - 6f, 32f), L.T("Загрузиться в мир хоста", "Load into host's world"))) Session.LoadHostWorld();
                cy += 40f;
            }
            else
            {
                if (Session.Players.Count > 0)
                {
                    if (Widgets.Button(new Rect(lx, cy, cw, 32f), L.T("Телепортировать всех игроков ко мне", "Teleport all players to me"))) Session.Summon(-1);
                    cy += 40f;
                }
                if (Session.ViaSteam) DrawInviteList(lx, ref cy, cw);
            }

            DrawLocalToggles(lx, ref cy, cw, Session.NamesPolicy == 0);
            try { DrawChronicle(lx, ref cy, cw); } catch (Exception e) { MelonLoader.MelonLogger.Warning("[окно] хроника: " + e.Message); }
            DrawChat(lx, ref cy, cw, 7);
            if (Widgets.Button(new Rect(lx, cy, cw, 32f), L.T("Выйти из кооп-режима", "Leave co-op mode"))) Session.Stop();
            cy += 40f;
        }

        // Раскрывающийся раздел: строка-заголовок, нажатие открывает и закрывает.
        private static bool Fold(float lx, ref float cy, float cw, string title, bool open)
        {
            var r = new Rect(lx, cy, cw, 26f);
            bool click = Widgets.Button(r, "", open, 14f);
            Widgets.Label(new Rect(lx + 12f, cy, cw - 40f, 26f), Widgets.Fit(title, cw - 40f, 14f), 14f, Theme.White, TextAnchor.MiddleLeft);
            Widgets.Label(new Rect(lx + cw - 28f, cy, 20f, 26f), open ? "−" : "+", 16f, Theme.Caption, TextAnchor.MiddleCenter);
            cy += 32f;
            return click ? !open : open;
        }

        // Итоги вылазки: сколько прожили вместе, кто сколько прошёл и добыл.
        private static bool _summaryOpen = true;

        private static void DrawSummary(float lx, ref float cy, float cw)
        {
            if (!Session.InSharedWorld) return;
            var rows = Game.Summary.All.ToList();
            if (rows.Count == 0 && Game.Summary.HoursTogether < 0.1f) return;
            _summaryOpen = Fold(lx, ref cy, cw, L.T("Итоги вылазки: вместе ", "Trip summary: together ") + Game.Summary.Together(), _summaryOpen);
            if (!_summaryOpen) return;
            foreach (var row in rows.OrderByDescending(r => r.Km)) Widgets.Text(lx + 10f, ref cy, cw - 10f, Game.Summary.Line(row), 2f, 13f, Theme.Body);
            cy += 6f;
        }

        // Кооп-испытания: хост выбирает, все видят ход и что уже выполнено.
        private static bool _challengesOpen;

        private static void DrawChallenges(float lx, ref float cy, float cw)
        {
            if (!Session.InSharedWorld) return;
            bool host = Session.Mode == Mode.Host;
            if (!host && Game.Challenges.Now == Game.Challenges.State.None && Game.Challenges.Completed.Count == 0) return;
            string head = Game.Challenges.Now == Game.Challenges.State.Active
                ? L.T("Испытание: ", "Challenge: ") + Game.Challenges.Title + " — " + Game.Challenges.Progress
                : L.T("Кооп-испытания", "Co-op challenges");
            _challengesOpen = Fold(lx, ref cy, cw, head, _challengesOpen);
            if (!_challengesOpen) return;
            var c = Game.Challenges.Now;
            if (c == Game.Challenges.State.Done || c == Game.Challenges.State.Failed)
                Widgets.Text(lx + 10f, ref cy, cw - 10f, (c == Game.Challenges.State.Done ? L.T("Выполнено: ", "Done: ") : L.T("Не удалось: ", "Failed: ")) + Game.Challenges.Title + " — " + Game.Challenges.Progress,
                    2f, 14f, c == Game.Challenges.State.Done ? Theme.Good : Theme.Warn);
            if (host && c != Game.Challenges.State.Active)
                Widgets.Text(lx + 10f, ref cy, cw - 10f, L.T("Общая цель на всех: выбери одну, ход видят все игроки в этом окне. Одновременно идёт только одно испытание.",
                    "A shared goal for everyone: pick one, and every player sees the progress in this window. Only one challenge runs at a time."), 2f, 12f, Theme.Dim);
            if (host)
            {
                if (c == Game.Challenges.State.Active)
                {
                    if (Widgets.Button(new Rect(lx + 10f, cy, 240f, 30f), L.T("Отменить испытание", "Cancel the challenge"))) Game.Challenges.Cancel();
                    cy += 38f;
                }
                else if (Game.Challenges.CanStart)
                {
                    float bw = (cw - 10f) / 2f - 6f;
                    var titles = Game.Challenges.Titles;
                    for (int i = 0; i < titles.Length; i++)
                    {
                        var r = new Rect(lx + 10f + (i % 2) * (bw + 12f), cy, bw, 30f);
                        if (Widgets.Button(r, Widgets.Fit(titles[i], bw - 10f), false, 13f)) Game.Challenges.Start(i);
                        if (i % 2 == 1 || i == titles.Length - 1) cy += 38f;
                    }
                }
            }
            if (Game.Challenges.Completed.Count > 0)
                Widgets.Text(lx + 10f, ref cy, cw - 10f, L.T("Уже выполнено: ", "Already done: ") + string.Join(", ", Game.Challenges.Completed), 2f, 12f, Theme.Dim);
            cy += 6f;
        }

        private static void DrawPlayerList(float lx, ref float cy, float cw, bool inGame)
        {
            string scene = GameApi.Scene();
            var players = Session.Players.Values.ToList();
            bool botRows = Session.Mode == Mode.Host && inGame && players.Any(pl => pl.Name == "Бот" && pl.Scene == scene);
            float rows = 1 + Math.Max(1, players.Count);
            float boxH = rows * 34f + (botRows ? 3 * 32f : 0f) + 10f;
            Widgets.Slot(new Rect(lx - 6f, cy, cw + 12f, boxH));
            float y0 = cy;
            cy += 6f;
            Widgets.Label(new Rect(lx + 6f, cy, cw - 12f, 28f), Widgets.Fit(Session.LocalName + (Session.Mode == Mode.Host ? L.T("  (ты, хост)", "  (you, host)") : L.T("  (ты)", "  (you)")), cw - 12f, 15f),
                15f, Theme.White, TextAnchor.MiddleLeft);
            cy += 34f;
            if (players.Count == 0)
            {
                Widgets.Label(new Rect(lx + 6f, cy, cw - 12f, 28f), L.T("Пока никого нет.", "Nobody else yet."), 14f, Theme.Dim, TextAnchor.MiddleLeft);
                cy += 34f;
            }
            foreach (var pl in players)
            {
                bool same = inGame && pl.Scene.Length > 0 && pl.Scene == scene;
                string where = pl.Scene.Length == 0 ? L.T("в лобби", "in the lobby") : !pl.InWorld ? L.T("в своём сохранении", "in own save")
                    : same ? Distance(pl) : L.T("в другой локации (", "in another location (") + pl.Scene + ")";
                bool twoButtons = Session.Mode == Mode.Host && inGame && pl.Scene.Length > 0 && pl.InWorld && !same;
                float bw = 104f, nw = cw - 12f - (twoButtons ? bw * 2f + 16f : bw + 8f);
                string name = pl.Name + (pl.Id == 0 ? L.T(" (хост)", " (host)") : "");
                Widgets.Label(new Rect(lx + 6f, cy - 1f, nw, 18f), Widgets.Fit(name, nw, 15f), 15f, Theme.White, TextAnchor.MiddleLeft);
                Widgets.Label(new Rect(lx + 6f, cy + 16f, nw, 14f), Widgets.Fit(where, nw, 11f), 11f, Theme.Caption, TextAnchor.MiddleLeft);
                // Друг в своём сохранении: «Ко мне», «К нему» и «Телепорт к» перенесли бы в чужой мир.
                float bx = lx + cw - 6f - bw;
                if (Session.Mode == Mode.Host && inGame && pl.Scene.Length > 0 && pl.InWorld)
                {
                    if (Widgets.Button(new Rect(bx, cy, bw, 28f), L.T("Ко мне", "To me"), false, 13f)) Session.Summon(pl.Id);
                    // Хост в другой локации: сходить к игроку самому.
                    if (!same && Widgets.Button(new Rect(bx - bw - 8f, cy, bw, 28f), L.T("К нему", "Go to"), false, 13f)) Session.GoToPlayer(pl.Id);
                }
                else if (same && Session.With(pl, scene) && Session.TeleportAllowed && Widgets.Button(new Rect(bx, cy, bw, 28f), L.T("Телепорт к", "Teleport"), false, 13f)) Session.TeleportTo(pl.Id);
                cy += 34f;
                if (Session.Mode == Mode.Host && inGame && pl.Name == "Бот" && same)
                {
                    if (Widgets.Button(new Rect(lx + 12f, cy, cw - 24f, 26f), "Бот: уйди в другую локацию (проверка «друг пришёл первым»)", false, 12f)) Session.SendChat("бот уйди");
                    cy += 32f;
                    float half = (cw - 32f) / 2f;
                    if (Widgets.Button(new Rect(lx + 12f, cy, half, 26f), "Бот: отойди далеко (600 м)", false, 12f)) Session.SendChat("бот далеко");
                    if (Widgets.Button(new Rect(lx + 20f + half, cy, half, 26f), "Бот: вернись ко мне", false, 12f)) Session.SendChat("бот назад");
                    cy += 32f;
                    if (Widgets.Button(new Rect(lx + 12f, cy, cw - 24f, 26f), "Бот: пройди проверки (контейнер, дверь, очаг, подарок, метка…)", false, 12f)) Session.SendChat("бот проверка");
                    cy += 32f;
                }
            }
            cy = Math.Max(cy, y0 + boxH) + 10f;
        }

        private static string _note = "";
        private static bool _chronicleOpen;

        // Хроника вылазки и общие заметки (Game/Chronicle.cs).
        private static void DrawChronicle(float lx, ref float cy, float cw)
        {
            _chronicleOpen = Fold(lx, ref cy, cw, _chronicleOpen ? L.T("Хроника и заметки", "Chronicle and notes")
                : L.T("Хроника и заметки", "Chronicle and notes") + $" ({Chronicle.Notes.Count})", _chronicleOpen);
            if (!_chronicleOpen) return;
            int lines = Math.Min(6, Chronicle.Entries.Count);
            if (lines == 0) Widgets.Text(lx, ref cy, cw, L.T("Пока ничего не случилось.", "Nothing has happened yet."), 4f, 13f, Theme.Dim);
            for (int i = Chronicle.Entries.Count - lines; i < Chronicle.Entries.Count; i++)
            {
                Widgets.Label(new Rect(lx + 4f, cy, cw - 8f, 20f), Widgets.Fit(Chronicle.Entries[i], cw - 8f, 13f), 13f, Theme.Body, TextAnchor.MiddleLeft);
                cy += 20f;
            }
            cy += 4f;
            Widgets.Section(lx, ref cy, cw, L.T("Общие заметки", "Shared notes"));
            for (int i = 0; i < Chronicle.Notes.Count; i++)
            {
                Widgets.Label(new Rect(lx + 4f, cy, cw - 44f, 22f), Widgets.Fit(Chronicle.Notes[i], cw - 44f, 13f), 13f, Theme.Body, TextAnchor.MiddleLeft);
                if (Widgets.Button(new Rect(lx + cw - 32f, cy, 32f, 22f), "x", false, 12f)) { Chronicle.RemoveNote(i); break; }
                cy += 26f;
            }
            _note = Widgets.TextField(new Rect(lx, cy, cw - 126f, 28f), "note", _note ?? "", 160, out bool enter);
            if ((Widgets.Button(new Rect(lx + cw - 118f, cy, 118f, 28f), L.T("Записать", "Add")) || enter) && _note.Trim().Length > 0)
            {
                Chronicle.AddNote(_note);
                _note = "";
            }
            cy += 38f;
        }

        private static void DrawChat(float lx, ref float cy, float cw, int lines)
        {
            Widgets.Section(lx, ref cy, cw, L.T("Чат", "Chat"));
            var log = new List<string>();
            for (int i = 0; i < Session.ChatLog.Count; i++)
                if (Config.SystemChat || i >= Session.ChatSystem.Count || !Session.ChatSystem[i]) log.Add(Session.ChatLog[i]);
            float boxTop = cy, lh = 20f;
            Widgets.Slot(new Rect(lx - 6f, boxTop, cw + 12f, lines * lh + 12f));
            int start = Math.Max(0, log.Count - lines);
            for (int i = start; i < log.Count; i++)
                Widgets.Label(new Rect(lx + 4f, boxTop + 6f + (i - start) * lh, cw - 8f, lh), Widgets.Fit(log[i], cw - 8f, 13f), 13f, Theme.Body, TextAnchor.MiddleLeft);
            cy = boxTop + lines * lh + 20f;

            _chat = Widgets.TextField(new Rect(lx, cy, cw - 126f, 28f), "chat", _chat ?? "", 200, out bool enter);
            if ((Widgets.Button(new Rect(lx + cw - 118f, cy, 118f, 28f), L.T("Отправить", "Send")) || enter) && _chat.Trim().Length > 0)
            {
                Session.SendChat(_chat);
                _chat = "";
            }
            cy += 40f;
        }

        private static string Distance(Player pl)
        {
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return L.T("рядом", "nearby");
            float dx = pl.X - x, dy = pl.Y - y, dz = pl.Z - z;
            return $"{Math.Sqrt(dx * dx + dy * dy + dz * dz):0} " + L.T("м", "m");
        }

        // Значок в углу экрана, пока включён кооп-режим. Без него игра обычная одиночная:
        // мод ничего не отправляет и не меняет правила смерти.
        private static void DrawBadge()
        {
            if (Session.Mode == Mode.Off || !Config.ShowBadge) return;
            // Строка состояния: кто хост, сколько игроков, пинг и последняя проверка синхронизации.
            string text = (Session.Mode == Mode.Host ? L.T("Кооп: ты хост", "Co-op: you host") : L.T("Кооп: у ", "Co-op: at ") + HostName()) +
                          L.T(", игроков ", ", players ") + (Session.Players.Count + 1);
            int ping = Session.PingMs();
            if (Session.Reconnecting) text += L.T(" · переподключаюсь…", " · reconnecting…");
            else if (ping >= 0) text += L.T($" · пинг {ping} мс", $" · ping {ping} ms");
            if (Session.Mode == Mode.Host && SyncCheck.LastShort.Length > 0) text += " · " + SyncCheck.LastShort;
            float bw = Math.Max(200f, Widgets.TextWidth(text, 13f) + 28f);
            var r = new Rect(Widgets.SW - bw - 10f, 8f, bw, 26f);
            Widgets.Backdrop(r);
            Widgets.Label(r, text, 13f, Theme.Body, TextAnchor.MiddleCenter);
        }

        private static string HostName() => Session.Players.TryGetValue(0, out var h) ? h.Name : L.T("хоста", "the host");

        private static readonly HashSet<byte> MenuActivities = new() { 4, 5, 6, 12, 14, 15, 16, 17, 18, 19, 20, 22, 28, 29, 30 };

        // Ники над головами других игроков.
        private static void DrawLabels()
        {
            bool names = Session.NamesVisible, acts = Session.Sees(Shown.Actions);
            if (Session.Mode == Mode.Off || !GameApi.InGame()) return;
            // В рюкзаке, на карте, в ящике и других окнах игры ники поверх окна только мешают.
            if (MenuActivities.Contains(Activity.Current())) return;
            var cam = GameManager.GetMainCamera();
            if (cam == null) return;
            foreach (var pl in Session.Players.Values)
            {
                if (!pl.Puppet.Alive) continue;
                var sp = cam.WorldToScreenPoint(pl.Puppet.LabelPoint);
                if (sp.z <= 0f || sp.z > 300f) continue;
                // Подпись по центру над головой, крупнее вблизи; тёмная тень для читаемости на снегу.
                int size = (int)Mathf.Clamp(26f - sp.z * 0.4f, 14f, 24f);
                string name = pl.Name.Replace("<", "‹");
                float w = name.Length * size * 0.6f + 10f, h = size + 10f;
                float lx = sp.x - w / 2f, ly = Screen.height - sp.y - h;
                // Над ником — последнее сообщение из чата.
                string said = Together.BubbleOf(pl.Id);
                if (said != null && sp.z < 80f)
                {
                    said = said.Replace("<", "‹");
                    float bw = Math.Min(420f, said.Length * 8f + 24f);
                    float bh = Widgets.TextHeight(said, bw - 16f) + 8f;
                    var br = new Rect(sp.x - bw / 2f, ly - bh - 4f, bw, bh);
                    Theme.Fill(br, new Color(0f, 0f, 0f, 0.6f));
                    GUI.Label(new Rect(br.x + 8f, br.y + 3f, bw - 16f, bh), said);
                }
                bool status = Session.Sees(Shown.Status) && pl.Hp <= 100 && sp.z < 40f;
                if (!names && !acts && !status) continue;
                if (names)
                {
                    GUI.Label(new Rect(lx + 1.5f, ly + 1.5f, w + 20f, h), $"<size={size}><b><color=#000000cc>{name}</color></b></size>");
                    string nc = Session.ColorOf(pl.Id, "");
                    GUI.Label(new Rect(lx, ly, w + 20f, h), nc.Length > 0 ? $"<size={size}><b><color=#{nc}>{name}</color></b></size>" : $"<size={size}><b>{name}</b></size>");
                }
                else ly += h * 0.6f;
                float below = ly + h - 4f;
                // Под ником — полоска здоровья и беды (замерзает, кровь, голод…).
                if (status)
                {
                    float bw = 70f, bx = sp.x - bw / 2f;
                    Theme.Fill(new Rect(bx - 1f, below - 1f, bw + 2f, 7f), new Color(0f, 0f, 0f, 0.7f));
                    Theme.Fill(new Rect(bx, below, bw * pl.Hp / 100f, 5f), Game.Vitals.BarColor(pl.Hp));
                    below += 9f;
                    string bad = Game.Vitals.Words(pl.Vitals);
                    if (bad.Length > 0)
                    {
                        int s3 = Math.Max(12, size - 8);
                        var plain = System.Text.RegularExpressions.Regex.Replace(bad, "<[^>]+>", "");
                        float w3 = plain.Length * s3 * 0.55f + 10f, x3 = sp.x - w3 / 2f;
                        GUI.Label(new Rect(x3 + 1f, below + 1f, w3 + 20f, s3 + 8f), $"<size={s3}><color=#000000cc>{plain}</color></size>");
                        GUI.Label(new Rect(x3, below, w3 + 20f, s3 + 8f), $"<size={s3}>{bad}</size>");
                        below += s3 + 4f;
                    }
                }
                // Ниже — чем занят, если чем-то долгим.
                string act = acts ? Activity.Name(pl.Action) : "";
                if (act.Length == 0 || sp.z > 60f) continue;
                int s2 = Math.Max(12, size - 6);
                float w2 = act.Length * s2 * 0.55f + 10f, ax = sp.x - w2 / 2f, ay = below;
                GUI.Label(new Rect(ax + 1f, ay + 1f, w2 + 20f, s2 + 10f), $"<size={s2}><i><color=#000000cc>{act}</color></i></size>");
                GUI.Label(new Rect(ax, ay, w2 + 20f, s2 + 10f), $"<size={s2}><i><color=#e8dcc0>{act}</color></i></size>");
            }
        }
    }
}
