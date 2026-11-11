using MelonLoader;
using UnityEngine;

namespace TLDCoop
{
    // Что каждый игрок видит и слышит у себя. Хост может решить за всех (Config.ForAll, Session.Sees).
    public enum Shown { Names, Actions, Voices, Bubbles, Arrows, Map, Sounds, Status }

    // Настройки хранятся в UserData/MelonPreferences.cfg, раздел [TLDCoop].
    public static class Config
    {
        private static MelonPreferences_Category _cat;
        private static MelonPreferences_Entry<string> _name, _ip, _key, _coopSlots, _character;
        private static MelonPreferences_Entry<int> _port, _namesForAll, _winX, _winY, _voiceVolume;
        private static MelonPreferences_Entry<bool> _syncTime, _syncWeather, _useModel, _allowTeleport, _showNames, _autoJoin, _showBadge, _showActions, _hearVoices, _showBubbles, _showArrows, _showOnMap, _hearSounds, _showStatus, _systemChat;
        private static MelonPreferences_Entry<string> _pingKey, _nickColor, _forAll, _password, _lastHostId, _lastHostName;
        private static MelonPreferences_Entry<bool> _quickHost, _quickJoin;
        private static MelonPreferences_Entry<string> _dragKey;

        public static void Load()
        {
            _cat = MelonPreferences.CreateCategory("TLDCoop");
            _name = _cat.CreateEntry("Name", "", "Ник (пусто = ник Steam)");
            _ip = _cat.CreateEntry("LastIp", "127.0.0.1", "Адрес хоста для подключения");
            _port = _cat.CreateEntry("Port", Net.Protocol.DefaultPort, "Порт");
            _password = _cat.CreateEntry("IpPassword", "", "Пароль для входа по IP: хост пускает только с ним, друг отправляет его при подключении (пусто — без пароля; через Steam не нужен)");
            _key = _cat.CreateEntry("MenuKey", "Backslash", "Клавиша окна кооп-мода (имя из UnityEngine.KeyCode)");
            if (_key.Value == "F8") _key.Value = "Backslash";   // F8 у многих делает скриншоты (Steam)
            _syncTime = _cat.CreateEntry("SyncTime", true, "Хост задаёт время суток");
            _syncWeather = _cat.CreateEntry("SyncWeather", true, "Хост задаёт погоду");
            _allowTeleport = _cat.CreateEntry("AllowTeleport", true, "Хост разрешает телепорт к другим игрокам");
            _showNames = _cat.CreateEntry("ShowNames", true, "Показывать ники над головами");
            _showActions = _cat.CreateEntry("ShowActions", true, "Показывать над игроками, чем они заняты");
            _showStatus = _cat.CreateEntry("ShowStatus", true, "Здоровье и беды друзей под ником (замерзает, кровь, голод…)");
            _hearVoices = _cat.CreateEntry("HearVoices", true, "Слышать фразы других игроков (до 20 м)");
            _nickColor = _cat.CreateEntry("NickColor", "", "Цвет ника, меток и стрелки у других (hex, например ff6b6b; пусто — обычный)");
            _voiceVolume = _cat.CreateEntry("VoiceVolume", 100, "Громкость фраз других игроков, 0–100 %");
            _systemChat = _cat.CreateEntry("SystemChat", true, "Показывать в чате окна системные сообщения мода (подключился, вышел…)");
            _hearSounds = _cat.CreateEntry("HearSounds", true, "Слышать звуки других игроков: выстрелы, шаги, рубку, двери");
            _showBubbles = _cat.CreateEntry("ShowBubbles", true, "Сообщения из чата над головой написавшего");
            _showArrows = _cat.CreateEntry("ShowArrows", true, "Стрелка у края экрана в сторону друга");
            _showOnMap = _cat.CreateEntry("ShowOnMap", true, "Друзья на карте игры");
            _pingKey = _cat.CreateEntry("PingKey", "Mouse2", "Клавиша лечения раненого друга (держать рядом; имя из UnityEngine.KeyCode; Mouse2 = средняя кнопка мыши)");
            _namesForAll = _cat.CreateEntry("NamesForAll", 0, "Хост: ники для всех (0 = как хочет каждый, 1 = всем включить, 2 = всем выключить)");
            _forAll = _cat.CreateEntry("ForAll", "", "Хост: что видят и слышат все (по символу на настройку: 0 = как хочет каждый, 1 = всем включить, 2 = всем выключить)");
            _autoJoin = _cat.CreateEntry("AutoJoinWorld", true, "Друг: после подключения сразу загрузиться в мир хоста");
            _useModel = _cat.CreateEntry("UseModel", true, "Модель игрока из SkyCoop (false = простая фигура)");
            _character = _cat.CreateEntry("Character", "", "Мой персонаж для других игроков (astrid, mackenzie, jeremiah…; пусто = по очереди)");
            _coopSlots = _cat.CreateEntry("CoopSlots", "", "Друг: слоты «Кооп у …», созданные модом (старые удаляются)");
            _showBadge = _cat.CreateEntry("ShowBadge", true, "Значок «Кооп» в углу экрана");
            _dragKey = _cat.CreateEntry("DragKey", "G", "Клавиша «тащить раненого друга» (имя из UnityEngine.KeyCode)");
            _quickHost = _cat.CreateEntry("QuickHost", false, "Быстрый старт хоста: загрузил свой мир — он сразу открыт друзьям");
            _quickJoin = _cat.CreateEntry("QuickJoin", false, "Быстрый старт друга: после запуска игры сам подключиться к прошлому хосту, как только он откроет мир");
            _lastHostId = _cat.CreateEntry("LastHostSteamId", "", "Друг: у кого играл в прошлый раз (Steam)");
            _lastHostName = _cat.CreateEntry("LastHostName", "", "");
            _winX = _cat.CreateEntry("WindowX", 20, "Положение окна мода (перетаскивается за заголовок)");
            _winY = _cat.CreateEntry("WindowY", 50, "");
        }

        public static string Name { get => _name.Value; set => _name.Value = value; }
        public static string Ip { get => _ip.Value; set => _ip.Value = value; }
        public static int Port { get => _port.Value; set => _port.Value = value; }
        public static string IpPassword => _password.Value ?? "";
        public static KeyCode DragKey => System.Enum.TryParse(_dragKey.Value, true, out KeyCode k) ? k : KeyCode.G;
        public static bool QuickHost { get => _quickHost.Value; set => _quickHost.Value = value; }
        public static bool QuickJoin { get => _quickJoin.Value; set => _quickJoin.Value = value; }
        public static string LastHostId { get => _lastHostId.Value ?? ""; set => _lastHostId.Value = value; }
        public static string LastHostName { get => _lastHostName.Value ?? ""; set => _lastHostName.Value = value; }
        public static bool SyncTime { get => _syncTime.Value; set => _syncTime.Value = value; }
        public static bool SyncWeather { get => _syncWeather.Value; set => _syncWeather.Value = value; }
        public static bool UseModel => _useModel.Value;
        public static string Character { get => _character.Value; set => _character.Value = value; }
        public static bool AllowTeleport { get => _allowTeleport.Value; set => _allowTeleport.Value = value; }
        public static bool ShowNames { get => _showNames.Value; set => _showNames.Value = value; }
        public static bool ShowActions { get => _showActions.Value; set => _showActions.Value = value; }
        public static bool ShowStatus { get => _showStatus.Value; set => _showStatus.Value = value; }
        public static bool HearVoices { get => _hearVoices.Value; set => _hearVoices.Value = value; }
        public static int VoiceVolume { get => Mathf.Clamp(_voiceVolume.Value, 0, 100); set => _voiceVolume.Value = Mathf.Clamp(value, 0, 100); }
        public static string NickColor { get => CleanColor(_nickColor.Value); set => _nickColor.Value = CleanColor(value); }

        // Цвета на выбор в окне (пусто — обычный: белый ник, жёлтые метки).
        public static readonly string[] Palette = { "", "ff6b6b", "ffa040", "ffe066", "8ef08e", "7fe0ff", "6f9dff", "d7a6ff", "ff8fd1" };

        // Только 6 шестнадцатеричных цифр — чтобы чужая строка не сломала разметку надписей.
        public static string CleanColor(string c)
        {
            c = (c ?? "").Trim().TrimStart('#').ToLowerInvariant();
            if (c.Length != 6) return "";
            foreach (char ch in c) if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f'))) return "";
            return c;
        }
        public static bool SystemChat { get => _systemChat.Value; set => _systemChat.Value = value; }
        public static bool HearSounds { get => _hearSounds.Value; set => _hearSounds.Value = value; }
        public static bool ShowBubbles { get => _showBubbles.Value; set => _showBubbles.Value = value; }
        public static bool ShowArrows { get => _showArrows.Value; set => _showArrows.Value = value; }
        public static bool ShowOnMap { get => _showOnMap.Value; set => _showOnMap.Value = value; }
        public static KeyCode PingKey => System.Enum.TryParse(_pingKey.Value, true, out KeyCode k) ? k : KeyCode.Mouse2;
        public static string PingKeyName => PingKey == KeyCode.Mouse2 ? UI.L.T("средняя кнопка мыши", "middle mouse button") : PingKey.ToString();
        public static bool AutoJoinWorld => _autoJoin.Value;
        public static int NamesForAll { get => _namesForAll.Value; set => _namesForAll.Value = value; }

        // Хост: для каждой настройки из Shown (кроме ников — у них NamesForAll) 0 = каждый сам, 1 = всем вкл, 2 = всем выкл.
        public static int ForAll(Shown what)
        {
            if (what == Shown.Names) return Mathf.Clamp(NamesForAll, 0, 2);
            string v = _forAll.Value ?? "";
            int i = (int)what;
            return i < v.Length && v[i] >= '0' && v[i] <= '2' ? v[i] - '0' : 0;
        }

        public static void SetForAll(Shown what, int value)
        {
            value = Mathf.Clamp(value, 0, 2);
            if (what == Shown.Names) { NamesForAll = value; return; }
            var c = (_forAll.Value ?? "").PadRight(ShownCount, '0').ToCharArray();
            c[(int)what] = (char)('0' + value);
            _forAll.Value = new string(c);
        }

        public const int ShownCount = 8;

        // Своя галочка для каждой такой настройки.
        public static bool Own(Shown what) => what switch
        {
            Shown.Names => ShowNames,
            Shown.Actions => ShowActions,
            Shown.Voices => HearVoices,
            Shown.Bubbles => ShowBubbles,
            Shown.Arrows => ShowArrows,
            Shown.Map => ShowOnMap,
            Shown.Sounds => HearSounds,
            Shown.Status => ShowStatus,
            _ => true,
        };

        public static System.Collections.Generic.List<string> CoopSlots
        {
            get => new(_coopSlots.Value.Split(',', System.StringSplitOptions.RemoveEmptyEntries));
            set { _coopSlots.Value = string.Join(",", value); Save(); }
        }
        public static bool ShowBadge { get => _showBadge.Value; set => _showBadge.Value = value; }
        public static int WindowX { get => _winX.Value; set => _winX.Value = value; }
        public static int WindowY { get => _winY.Value; set => _winY.Value = value; }

        public static KeyCode MenuKey => System.Enum.TryParse(_key.Value, true, out KeyCode k) ? k : KeyCode.Backslash;
        public static string MenuKeyName => MenuKey == KeyCode.Backslash ? "\\" : MenuKey.ToString();

        public static void Save() => _cat.SaveToFile(false);
    }
}
