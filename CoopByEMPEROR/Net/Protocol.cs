using System.IO;
using System.Text;

namespace CoopByEMPEROR.Net
{
    // Типы сообщений. Чтобы добавить своё: новый номер здесь, запись через Msg.New(...),
    // разбор в Session.Handle(...). Если меняешь формат старых сообщений — увеличь Version,
    // тогда игроки с разными версиями мода не смогут подключиться друг к другу.
    public enum MsgType : byte
    {
        Hello = 1,       // клиент → хост: версия, ник
        Welcome = 2,     // хост → клиент: твой id и список игроков
        Reject = 3,      // хост → клиент: причина отказа
        Joined = 4,      // хост → все: id, ник
        Left = 5,        // хост → все: id
        PlayerState = 6, // все: id, сцена, позиция, поворот, флаги
        World = 7,       // хост → клиенты: настройки хоста, режим и регион, время суток, погода
        Chat = 8,        // все: id, текст
        Ping = 9,        // все: «я на связи» (раз в 2 секунды)
        Bye = 10,        // все: «я отключаюсь»
        Start = 11,      // хост → клиенты: игра начинается (режим, регион; -1 = хост грузит своё сохранение)
        Rules = 12,      // хост → клиенты: правила сервера (Game/Rules.cs)
        WorldRequest = 13, // клиент → хост: пришли мне свой мир (сохранение)
        WorldChunk = 14,   // хост → клиент: кусок файла сохранения (Game/WorldTransfer.cs)
        ItemDrop = 15,     // все: предмет выброшен (Game/ItemSync.cs)
        ItemPick = 16,     // все: предмет поднят с земли
        SeqCtl = 77,       // события с номером: «получил всё до N», «дошли с N», «их у меня уже нет», «загрузи мир заново» (Net/ClientLog.cs)
        Big = 78,          // кусок большого сообщения (больше 400 КБ), собирается на приёме (Net/Peer.cs)
        ItemUpdate = 76,   // все: вещь в мире поменялась (котелок, силок, фонарь, переставили) — замени свою копию, номер тот же
        Check = 17,        // клиент → хост: что лежит на земле, время, погода (Game/SyncCheck.cs)
        ObjectState = 18,  // все: контейнер, дверь, разборка, растение, костёр, палки (Game/WorldObjects.cs)
        WorldInfo = 19,    // хост → клиент: перед миром; восстановлен ли прогресс друга (Game/PlayerProgress.cs)
        Progress = 20,     // клиент → хост: прогресс игрока из его сохранения
        SaveRequest = 21,  // хост → клиенты: хост сохранился, сохранитесь и вы
        Content = 22,      // все: список DLC и модов, которые меняют мир (Game/ContentCheck.cs)
        Summon = 23,       // хост → клиент: «иди ко мне» (телепорт к хосту, при нужде через смену локации)
        SceneJoin = 24,    // клиент → хост: я в локации, какая там раскладка (Game/SceneSnapshot.cs)
        SceneState = 25,   // все: раскладка предметов на земле в локации
        SceneFirst = 26,   // хост → клиент: в этой локации ты первый, пришли свою раскладку
        SceneSaved = 27,   // хост → клиент: локация есть в сохранении хоста, выравнивать нечего
        SpawnClaim = 28,   // клиент → хост: можно мне разложить палки у этого дерева? (Game/RadialSync.cs)
        SpawnGrant = 29,   // хост → клиент: можно, ты здесь первый
        WeatherState = 30, // хост → клиенты: погода (Game/WeatherSync.cs)
        AnimalState = 31,    // у кого мозг зверя → все: где звери и что делают (Game/AnimalSync.cs)
        AnimalHit = 32,      // → ведущему зверя: попал в него
        AnimalTransfer = 33, // → жертве: зверь бросился на тебя, мозг у тебя; → хозяину локации: возвращаю
        AnimalRemove = 34,   // хозяин локации → все: этих зверей игра убрала
        AnimalAuthority = 35, // хозяин животных локации напоминает о себе
        SleepReady = 36,     // клиент → хост: готов спать/ждать (или передумал) (Game/SleepSync.cs)
        SleepWait = 37,      // хост → все: кого ждём
        SleepStart = 38,     // хост → все: все готовы, засыпаем
        SleepStop = 73,      // друг → хост: я проснулся раньше; хост → все: общий сон окончен, просыпаемся (Game/SleepSync.cs)
        AnimalHarvest = 39,  // кто разделывал → все: состояние туши
        AnimalSound = 40,    // ведущий зверя → все: зверь издал звук (Game/AnimalSound.cs)
        TraderState = 41,    // кто говорил с торговцем → хост → все: состояние торговца (Game/GlobalSync.cs)
        AuroraState = 42,    // хост → все: полярное сияние
        PlayerVoice = 43,    // все: игрок сказал фразу выживальщика (Game/VoiceSync.cs)
        Marker = 44,         // все: метка «сюда» (Game/Together.cs)
        GiveItem = 45,       // все (берёт адресат): вещь из рюкзака в подарок
        Revive = 46,         // все (берёт адресат): друг поднял раненого
        BotCheck = 47,       // бот → хост: что у бота получилось после шага проверки (Game/BotTests.cs)
        DownedInfo = 48,     // все: раненый сообщает, от чего упал (какой лут нужен, Game/Rescue.cs)
        ReviveHold = 49,     // все (берёт адресат): друг лечит раненого — таймер стоит
        TrainSync = 50,      // все: поезд Iron Road — ход, части, топливо, стрелки (Game/TrainSync.cs)
        Hit = 52,            // все (берёт адресат): попал в друга, урон по своим (Game/FriendlyFire.cs)
        IslandSync = 51,     // все: лодка на остров — общий ящик обмена, «плыть вместе» (Game/IslandSync.cs)
        WorldEvent = 53,     // все: четвертование туши, большие вещи на берегу и др. (Game/WorldEvents.cs)
        PlayerSound = 54,    // все: звук игрока — выстрел, шаг, рубка, дверь (Game/SoundSync.cs)
        Trade = 55,          // все (берёт адресат): окно обмена вещами (Game/Trade.cs)
        AnimalSense = 59,    // все: что о тебе знают звери — присел, факел, запах, укрытие (Game/AnimalSync.Sense.cs)
        ContainerAsk = 61,   // друг → хост: что в неразложенном контейнере; хост → друг: «раскладывай сам» (Game/ContainerLoot.cs)
        PickDenied = 58,     // хост → друг: эту вещь первым взял другой, убери её у себя (Game/ItemSync.cs)
        Quest = 57,          // все ↔ хост: квестовые вещи локации, кто что взял, что вернуть (Game/QuestItems.cs)
        Seq = 62,            // хост → друг: событие мира с номером (Net/EventLog.cs)
        SeqResend = 63,      // друг → хост: дошли события с такого по такой номер
        CSeq = 69,           // друг → хост: своё событие мира с номером (Net/ClientLog.cs)
        SeqSkip = 68,
        InvProgress = 70,    // друг → хост: свежий рюкзак после выброса или подбора (Game/PlayerProgress.cs)
        ProgressPart = 71,
        Recipes = 75,        // кто выучил рецепт → хост → все; друг при входе шлёт свои, хост отвечает всеми (Game/SharedRecipes.cs)
        SceneSave = 74,      // друг → хост: сохранение локации, где он был один, кусками по 200 КБ (Game/SceneHandoff.cs)
        GiftState = 72,      // подарок решает хост: друг → хост «беру» / «отменяю», хост → друг «твой» / «доставлен» / «отменён» (Game/Together.cs)   // друг → хост: прогресс, сжатый, кусками по 200 КБ        // хост → друг: событий старше такого номера в журнале уже нет, не жди их (Net/EventLog.cs)
        GiveAck = 64,
        Chronicle = 66,
        Drag = 67,           // все (берёт адресат): тащу раненого, вот где я (Game/Drag.cs)      // хроника вылазки и общие заметки (Game/Chronicle.cs)
        ContainerClaim = 65, // друг → хост: начинаю обыск ящика; хост → друг: его уже обыскивает другой (Game/ContainerLoot.cs)        // адресат → все (берёт даривший): подарок получен (Game/Together.cs)
        Summary = 56,        // друг → хост: что прошёл и добыл; хост → все: итоги вылазки (Game/Summary.cs)
        Fx = 60,             // все, без номера: след чужой пули; уголь и факел — «беру» другу → хост, ответ хоста (Game/Features.cs)
        Feature = 79,        // все, с номером: рюкзак на месте смерти появился / убран, растение отросло (Game/Features.cs)
        AnimalArrow = 96,    // все: стрела воткнулась в зверя (кость, место, угол) / вышла из него (Game/AnimalSync.Arrows.cs)
        AnimalNoise = 80,    // все: я выстрелил — пусть услышат звери, чей мозг у других (Game/AnimalSync.Noise.cs)
        HelloAgain = 81,     // хост → друг (Steam): я тебя не знаю (сбросил соединение) — поздоровайся заново
        CheckRelay = 83,     // хост → друг: отчёт другого игрока из локации, где хоста нет, — сверь со своим (Game/SyncCheck.cs)
        CheckFix = 84,       // друг → хост: моя раскладка для такого-то игрока, перешли ему (Game/SyncCheck.cs)
        QuestState = 85,     // друг → хост: мой журнал заданий и на каких пунктах я жду (Game/QuestWait.cs)
        QuestGate = 86,      // хост → друг: какие пункты закрыть, кого ещё ждём (Game/QuestWait.cs)
        CheckResend = 87,    // хост ↔ друг: состояние этих вещей разошлось, а менял их ты — пришли заново (Game/SyncCheck.cs)
        FlareShot = 97,      // все: я выстрелил из сигнального пистолета — откуда и куда (Game/FlareShots.cs)
        ArrowFlight = 88,    // все: я выстрелил из лука — откуда, куда и с какой скоростью летит стрела (Game/ArrowFlight.cs)
        AuroraScreens = 89,  // друг → хост: мои назначения картинок экранам; хост → все: общие назначения (Game/AuroraPower.cs)
        PotAmounts = 90,     // все, без номера: точное количество снега и воды и время готовки в котелке на огне (Game/PotAmounts.cs)
        FilterTags = 82,     // хост → все: его разрешённые метки предметов заданий (Game/FilterTags.cs)
        OwnProgress = 98,    // друг → хост: моя копия моего прогресса в этом мире (мир, время, сжатые куски; Game/PlayerProgress.cs)
        Smokehouse = 99,     // все, с номером: бочка коптильни изменилась — JSON одной бочки (SmokehouseByEMPEROR, Game/SmokehouseLink.cs)
        SmokehouseAll = 100, // друг → хост: дай все бочки мира (пустая строка); хост → друг: все бочки (Game/SmokehouseLink.cs)
        SmokehouseAsk = 101, // друг → хост: номер и действие с бочкой; хост → друг: номер и ответ (решает хост; Game/SmokehouseLink.cs)
        Jellyfish = 102,     // все, с номером: событие мода медуз — медуза в лунке, аквариум (JellyfishByEMPEROR, Game/JellyfishLink.cs)
        JellyfishAll = 103,  // друг → хост: дай все аквариумы; хост → друг: все аквариумы (Game/JellyfishLink.cs)
        JellyfishAsk = 104,  // друг → хост: номер и действие (забрать медузу, перелить, отловить); хост → друг: номер и ответ (Game/JellyfishLink.cs)
    }

    public static class Protocol
    {
        public const int Version = 64;   // 64 (0.19.27): PotAmounts, SleepReady/Start/Wait несут точное время ожидания
        // было: 63   // 63 (0.19.26): сияние несёт часы хоста и силу сияния, AuroraScreens
        // было: 62   // 62 (0.19.0): полировка по проверке 7: HelloAgain, Decoration/Lantern, обмен с номером согласия, ящик лодки через хоста, добыча по стрелку
        public const int DefaultPort = 26960;
        public const int MaxFrame = 1 << 20;
    }

    // Сборка сообщения: var w = Msg.New(MsgType.Chat); w.Write(...); conn.Send(Msg.Done(w));
    public static class Msg
    {
        public static BinaryWriter New(MsgType type)
        {
            var w = new BinaryWriter(new MemoryStream(64), Encoding.UTF8);
            w.Write((byte)type);
            return w;
        }

        public static byte[] Done(BinaryWriter w)
        {
            w.Flush();
            return ((MemoryStream)w.BaseStream).ToArray();
        }

        public static BinaryReader Read(byte[] data, out MsgType type)
        {
            var r = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
            type = (MsgType)r.ReadByte();
            return r;
        }
    }
}
