using System;
using System.IO;
using MelonLoader;

namespace CoopByEMPEROR.Game
{
    public enum Place : byte { Local = 0, NearPlayer = 1, Anywhere = 2, DeathSpot = 3 }
    public enum LootMode : byte { Keep = 0, AtDeathSpot = 1, Lost = 2 }

    // Правила сервера. Хост меняет их в окне мода и рассылает всем (сообщение Rules), клиенты
    // используют присланные. Чтобы добавить правило: поле здесь + запись/чтение в Write/Read
    // + строка в окне (UI/HostPanel.cs) + увеличить Protocol.Version.
    public class Rules
    {
        // Смерть
        public byte PlacesAllowed = 0b1111;           // биты Place: 1 локально, 2 рядом с игроком, 4 где угодно, 8 на месте смерти
        public Place DefaultPlace = Place.NearPlayer;
        public bool PlayerChoosesPlace = true;        // умерший сам выбирает на экране смерти
        public LootMode Loot = LootMode.AtDeathSpot;
        public bool StarterKit = false;
        public byte HealthPercent = 50;               // 100 / 50 / 25
        public byte SkillLossPercent = 0;             // 0 / 25 / 50 / 75 / 100
        public bool Downed = true;                    // вместо смерти «ранен»: друг рядом может поднять (Game/Death.cs)
        public byte DownedSeconds = 15;
        public bool ReviveItems = true;               // чтобы поднять, нужен лечебный лут по причине (Game/Rescue.cs)
        public bool FriendlyFire = false;             // урон по своим (Game/FriendlyFire.cs)
        public bool SharedMap;                        // зарисовка углём открывает место на карте у всех (Game/MapShare.cs)
        public bool SoloSleep = true;                 // в меню отдыха можно выбрать «спать отдельно» (Game/SleepSync.cs)
        public bool QuestWait;                        // пункт задания не закрывается, пока его не сделают все (Game/QuestWait.cs)
        public bool AnimalsSeeAll = true;             // зверь думает в одной игре и видит ближайшего игрока (Game/AnimalSync.Perception.cs)
        // Здоровье зверей, в четвертях: 4 = ×1, 5 = ×1,25, 6 = ×1,5, 8 = ×2 (Game/AnimalSync.cs).
        // Порядок: волк, лесной волк, медведь, лось, пума, олень, кролик.
        public byte[] AnimalHp = { 6, 6, 8, 8, 8, 5, 4 };
        // Ползунки мода-регулятора еды FoodBalanceByEMPEROR у хоста (JSON, "" — регулятора нет). Не настройка окна:
        // хост берёт их из мода перед каждой рассылкой, друг подставляет у себя (Game/FoodBalanceLink.cs).
        public string FoodBalance = "";
        public static readonly string[] AnimalNames = { "Волк", "Лесной волк", "Медведь", "Лось", "Пума", "Олень", "Кролик" };

        public bool IsAllowed(Place p) => (PlacesAllowed & (1 << (int)p)) != 0;

        public void Write(BinaryWriter w)
        {
            w.Write(PlacesAllowed);
            w.Write((byte)DefaultPlace);
            w.Write(PlayerChoosesPlace);
            w.Write((byte)Loot);
            w.Write(StarterKit);
            w.Write(HealthPercent);
            w.Write(SkillLossPercent);
            w.Write(Downed);
            w.Write(DownedSeconds);
            w.Write(ReviveItems);
            w.Write(FriendlyFire);
            w.Write((byte)AnimalHp.Length);
            w.Write(AnimalHp);
            w.Write(SoloSleep);
            w.Write(SharedMap);
            w.Write(AnimalsSeeAll);
            w.Write(FoodBalance ?? "");
            w.Write(QuestWait);
        }

        public static Rules Read(BinaryReader r)
        {
            var x = ReadBase(r);
            int n = r.ReadByte();
            var hp = r.ReadBytes(n);
            for (int i = 0; i < Math.Min(n, x.AnimalHp.Length); i++) x.AnimalHp[i] = hp[i];
            x.SoloSleep = r.ReadBoolean();
            if (r.BaseStream.Position < r.BaseStream.Length) x.SharedMap = r.ReadBoolean();
            if (r.BaseStream.Position < r.BaseStream.Length) x.AnimalsSeeAll = r.ReadBoolean();
            if (r.BaseStream.Position < r.BaseStream.Length) x.FoodBalance = r.ReadString();
            if (r.BaseStream.Position < r.BaseStream.Length) x.QuestWait = r.ReadBoolean();
            return x;
        }

        private static Rules ReadBase(BinaryReader r) => new Rules
        {
            PlacesAllowed = r.ReadByte(),
            DefaultPlace = (Place)r.ReadByte(),
            PlayerChoosesPlace = r.ReadBoolean(),
            Loot = (LootMode)r.ReadByte(),
            StarterKit = r.ReadBoolean(),
            HealthPercent = r.ReadByte(),
            SkillLossPercent = r.ReadByte(),
            Downed = r.ReadBoolean(),
            DownedSeconds = r.ReadByte(),
            ReviveItems = r.ReadBoolean(),
            FriendlyFire = r.ReadBoolean(),
        };

        // ---------- Хранение у хоста (UserData/MelonPreferences.cfg, раздел [TLDCoopRules]) ----------

        private static MelonPreferences_Category _cat;
        private static MelonPreferences_Entry<int> _places, _default, _loot, _health, _skills;
        private static MelonPreferences_Entry<bool> _choose, _kit, _downed, _reviveItems, _friendlyFire, _soloSleep, _sharedMap, _seeAll, _questWait;
        private static MelonPreferences_Entry<string> _animalHp;

        public static Rules Host = new();

        public static void Load()
        {
            _cat = MelonPreferences.CreateCategory("TLDCoopRules");
            _places = _cat.CreateEntry("DeathPlacesAllowed", 15, "Где можно воскреснуть: 1 в этой локации, 2 рядом с игроком, 4 где угодно, 8 на месте смерти (сумма)");
            _default = _cat.CreateEntry("DeathDefaultPlace", 1, "Место по умолчанию: 0 в этой локации, 1 рядом с игроком, 2 где угодно, 3 на месте смерти");
            _choose = _cat.CreateEntry("DeathPlayerChooses", true, "Умерший сам выбирает место");
            _loot = _cat.CreateEntry("DeathLoot", 1, "Лут: 0 остаётся в инвентаре, 1 на месте смерти, 2 пропадает");
            _kit = _cat.CreateEntry("DeathStarterKit", false, "Выдать стартовый набор");
            _health = _cat.CreateEntry("DeathHealth", 50, "Здоровье после воскрешения, %");
            _skills = _cat.CreateEntry("DeathSkillLoss", 0, "Потеря уровней навыков, %");
            _downed = _cat.CreateEntry("DeathDowned", true, "Вместо смерти «ранен»: друг рядом может поднять за 15 секунд");
            _reviveItems = _cat.CreateEntry("DeathReviveItems", true, "Чтобы поднять раненого, нужен лечебный лут по причине (бинты, лекарства, горячее, еда)");
            _friendlyFire = _cat.CreateEntry("FriendlyFire", false, "Урон по своим: пули и стрелы ранят друзей");
            _soloSleep = _cat.CreateEntry("SoloSleep", true, "В меню отдыха можно выбрать «спать отдельно»: у остальных время не проматывается");
            _sharedMap = _cat.CreateEntry("SharedMap", false, "Общая карта: что один зарисовал углём, появляется на карте у всех");
            _questWait = _cat.CreateEntry("QuestWait", false, "Задания вместе: пункт задания не закрывается у ушедшего вперёд, пока его не сделают остальные");
            _seeAll = _cat.CreateEntry("AnimalsSeeAll", true, "Звери видят всех: зверь думает в одной игре и идёт на ближайшего игрока, переезжает к другу только в момент броска");
            _animalHp = _cat.CreateEntry("AnimalHp", "6,6,8,8,8,5,4", "Здоровье зверей в четвертях (4 = ×1): волк, лесной волк, медведь, лось, пума, олень, кролик");
            Host = new Rules
            {
                PlacesAllowed = (byte)(_places.Value & 15),
                DefaultPlace = (Place)System.Math.Clamp(_default.Value, 0, 3),
                PlayerChoosesPlace = _choose.Value,
                Loot = (LootMode)System.Math.Clamp(_loot.Value, 0, 2),
                StarterKit = _kit.Value,
                HealthPercent = (byte)System.Math.Clamp(_health.Value, 1, 100),
                SkillLossPercent = (byte)System.Math.Clamp(_skills.Value, 0, 100),
                Downed = _downed.Value,
                ReviveItems = _reviveItems.Value,
                FriendlyFire = _friendlyFire.Value,
                SoloSleep = _soloSleep.Value,
                SharedMap = _sharedMap.Value,
                AnimalsSeeAll = _seeAll.Value,
                QuestWait = _questWait.Value,
            };
            if (Host.PlacesAllowed == 0) Host.PlacesAllowed = 15;
            var parts = (_animalHp.Value ?? "").Split(',');
            for (int i = 0; i < Math.Min(parts.Length, Host.AnimalHp.Length); i++)
                if (byte.TryParse(parts[i].Trim(), out var q) && q >= 1 && q <= 40) Host.AnimalHp[i] = q;
        }

        public static void SaveHost()
        {
            if (_cat == null) return;
            _places.Value = Host.PlacesAllowed;
            _default.Value = (int)Host.DefaultPlace;
            _choose.Value = Host.PlayerChoosesPlace;
            _loot.Value = (int)Host.Loot;
            _kit.Value = Host.StarterKit;
            _health.Value = Host.HealthPercent;
            _skills.Value = Host.SkillLossPercent;
            _downed.Value = Host.Downed;
            _reviveItems.Value = Host.ReviveItems;
            _friendlyFire.Value = Host.FriendlyFire;
            _soloSleep.Value = Host.SoloSleep;
            _sharedMap.Value = Host.SharedMap;
            _seeAll.Value = Host.AnimalsSeeAll;
            _questWait.Value = Host.QuestWait;
            _animalHp.Value = string.Join(",", Host.AnimalHp);
            _cat.SaveToFile(false);
        }
    }
}
