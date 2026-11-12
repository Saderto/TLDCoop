using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MelonLoader;
using CoopByEMPEROR.Game;
using UnityEngine;

namespace CoopByEMPEROR.Net
{
    public enum Mode { Off, Host, Client }

    public class Player
    {
        public int Id;
        public string Name = "";
        public string Scene = "";
        public string SceneKey = "";  // файл сохранения локации (у домиков, деливших одну сцену Unity, у каждого свой)
        public float X, Y, Z, Yaw;
        public bool Crouched;
        public bool InWorld = true;   // в общем мире (друг загрузил мир хоста), а не в своём сохранении
        public byte Action;        // чем занят (Game/Activity.cs)
        public string Character = ""; // каким персонажем его показывать (Game/Characters.cs)
        public string Color = "";     // цвет ника, меток и стрелки (hex без #; пусто — обычный)
        public string CharacterAndColor => Color.Length > 0 ? Character + "#" + Color : Character;
        public string Held = "";      // что в руках (префаб вещи, Game/HeldItems.cs)
        public byte HoldFlags, Shots;  // горит / целится; счётчик выстрелов
        public string Busy = "";      // чем из мира сейчас пользуется (Game/Busy.cs)
        public string Carry = "";     // что тащит за собой (ключ волокуши, Game/CarryVisual.cs)
        public bool CarryPose;        // есть ли поза волокуши относительно игрока
        public Vector3 CarryRel;
        public Quaternion CarryRot = Quaternion.identity;
        public byte Gesture;          // жест (Game/Together.cs)
        public byte Hp = 255, Vitals;   // здоровье 0..100 (255 = неизвестно) и беды (Game/Vitals.cs)
        public sbyte Pitch;           // наклон взгляда в градусах: + вниз, − вверх
        public float LastState = -100f;
        public readonly RemotePlayer Puppet = new();

        // Плавность: последние точки движения. Фигура идёт по ним с задержкой Delay, поэтому при неровном
        // пинге движется ровно (между точками — по прямой, после последней — ещё немного по инерции).
        public const float Delay = 0.12f;
        private readonly List<(float t, Vector3 p, float yaw)> _track = new();

        public void AddPoint(float t)
        {
            var p = new Vector3(X, Y, Z);
            if (_track.Count > 0 && (p - _track[_track.Count - 1].p).sqrMagnitude > 100f) _track.Clear();   // телепорт
            _track.Add((t, p, Yaw));
            if (_track.Count > 24) _track.RemoveAt(0);
        }

        public void Sample(float at, out Vector3 pos, out float yaw)
        {
            pos = new Vector3(X, Y, Z);
            yaw = Yaw;
            int n = _track.Count;
            if (n == 0) return;
            if (at <= _track[0].t) { pos = _track[0].p; yaw = _track[0].yaw; return; }
            for (int i = n - 1; i > 0; i--)
            {
                var b = _track[i];
                var a = _track[i - 1];
                if (at < a.t) continue;
                if (at > b.t) break;
                float k = b.t > a.t ? (at - a.t) / (b.t - a.t) : 1f;
                pos = Vector3.Lerp(a.p, b.p, k);
                yaw = Mathf.LerpAngle(a.yaw, b.yaw, k);
                return;
            }
            var last = _track[n - 1];
            pos = last.p;
            yaw = last.yaw;
            if (n >= 2)
            {
                var prev = _track[n - 2];
                float span = last.t - prev.t;
                float extra = Mathf.Min(at - last.t, 0.15f);
                if (span > 0.01f && extra > 0f && (last.p - prev.p).sqrMagnitude < 4f)
                    pos = last.p + (last.p - prev.p) / span * extra;
            }
        }
    }

    // Сетевая игра целиком: кто хост, кто подключён, что и когда отправлять.
    // Хост главный: раздаёт id, пересылает сообщения клиентов друг другу и задаёт время и погоду.
    // Всё здесь выполняется в игровом потоке (из CoopMod.OnUpdate).
    public static class Session
    {
        public static Mode Mode { get; private set; }
        public static bool ViaSteam { get; private set; }
        public static string Status { get; private set; } = "Не подключено";
        public static int LocalId { get; private set; }
        public static string LocalName => string.IsNullOrWhiteSpace(Config.Name) ? "Игрок" : Config.Name.Trim();

        public static readonly Dictionary<int, Player> Players = new();

        // Что прислал хост (у самого хоста — его настройки).
        public static bool TeleportAllowed => Mode == Mode.Host ? Config.AllowTeleport : _hostAllowsTeleport;
        public static int HostModeType { get; private set; } = -1;
        public static string HostRegionKey { get; private set; } = "";
        public static bool HostWeatherSync { get; private set; }
        private static bool _hostAllowsTeleport = true;
        private static readonly int[] _hostForAll = new int[Config.ShownCount];

        // Ники, стрелки, карта, звуки…: хост может включить или выключить их всем принудительно.
        public static int Policy(Shown what) => Mode == Mode.Host ? Config.ForAll(what) : Mode == Mode.Client ? _hostForAll[(int)what] : 0;
        public static bool Sees(Shown what) { int p = Policy(what); return p == 1 || (p == 0 && Config.Own(what)); }
        public static int NamesPolicy => Policy(Shown.Names);
        public static bool NamesVisible => Sees(Shown.Names);
        public static readonly List<string> ChatLog = new();
        public static readonly List<bool> ChatSystem = new();   // строка от мода, а не от игрока

        private static readonly List<Peer> Peers = new();
        private static readonly ConcurrentQueue<Peer> Accepted = new();
        private static Peer _host;              // клиент: соединение с хостом
        private static ulong _hostSteamId;
        private static bool _welcomed;
        private static float _joinStarted;
        private static Task<TcpPeer> _connecting;
        // Переподключение: связь с хостом пропала, а друг в его мире — не выходим, пробуем снова до 90 с.
        private static float _reconnectUntil = -1f, _nextReconnectTry;
        private static string _lastIp = "";
        private static int _lastPort;
        public static bool Reconnecting => _reconnectUntil >= 0f;
        // HelloAgain без переподключения: после Welcome ещё надо спросить мир/раскладку заново.
        private static bool _reaskAfterWelcome;
        private static int _nextId = 1;

        private static float _nextPing, _nextState, _nextWorld, _nextScan, _nextHello;
        private static string _lastScene = "";

        private const float StateInterval = 1f / 15f;
        private const float Timeout = 20f;

        // ---------- Запуск и остановка ----------

        public static void HostSteam()
        {
            Stop();
            if (!Steam.Init())
            {
                Status = UI.L.T("Steam недоступен: ", "Steam is unavailable: ") + Steam.Error;
                return;
            }
            StartHost(true);
            ListenLocal();
            Steam.SetPresence("host");
            Status = GameApi.InGame() ? UI.L.T("Мир открыт. Друзья увидят тебя в окне мода.", "The world is open. Friends will see you in the mod window.") : UI.L.T("Лобби создано. Пригласи друзей и начни игру.", "Lobby created. Invite friends and start the game.");
        }

        public static void HostIp(int port)
        {
            Stop();
            try
            {
                TcpPeer.Listen(port, p => Accepted.Enqueue(p));
            }
            catch (Exception e)
            {
                Status = UI.L.T("Не удалось открыть порт ", "Could not open port ") + port + ": " + e.Message;
                return;
            }
            StartHost(false);
            Status = UI.L.T("Сервер создан на порту ", "Server created on port ") + port + ".";
        }

        // Хост через Steam ещё и слушает 127.0.0.1: так к нему подключается бот — вторая копия
        // игры на этом же ПК (Game/Bot.cs). Снаружи этот порт недоступен.
        private static void ListenLocal()
        {
            try
            {
                TcpPeer.Listen(Config.Port, p => Accepted.Enqueue(p), loopbackOnly: true);
            }
            catch (Exception e)
            {
                MelonLogger.Msg("[сеть] локальный порт для бота не открылся: " + e.Message);
            }
        }

        private static void StartHost(bool steam)
        {
            Mode = Mode.Host;
            Kicked.Clear();
            ViaSteam = steam;
            LocalId = 0;
            _nextId = 1;
            Log("Ты создал сервер.");
        }

        public static void JoinSteam(ulong hostId, string hostName)
        {
            Stop();
            if (!Steam.Init())
            {
                Status = UI.L.T("Steam недоступен: ", "Steam is unavailable: ") + Steam.Error;
                return;
            }
            Mode = Mode.Client;
            ViaSteam = true;
            _hostSteamId = hostId;
            SteamPeer.CloseLingering(hostId);   // прошлое соединение с ним, закрываемое после Bye, не закроет новое
            _host = new SteamPeer(hostId) { LastHeard = Time.unscaledTime };
            _welcomed = false;
            _join = JoinStep.None;
            _joinStarted = Time.unscaledTime;
            Steam.SetPresence("join:" + hostId);
            Status = UI.L.T("Подключаюсь к ", "Connecting to ") + hostName + "…";
        }

        public static void JoinIp(string ip, int port)
        {
            Stop();
            Mode = Mode.Client;
            ViaSteam = false;
            _welcomed = false;
            _join = JoinStep.None;
            _joinStarted = Time.unscaledTime;
            _lastIp = ip;
            _lastPort = port;
            _connecting = Task.Run(() => TcpPeer.Connect(ip, port));
            Status = UI.L.T("Подключаюсь к ", "Connecting to ") + ip + ":" + port + "…";
        }

        public static void Stop(string reason = null)
        {
            if (Mode != Mode.Off)
            {
                var bye = Msg.Done(Msg.New(MsgType.Bye));
                foreach (var p in AllPeers()) p.Send(bye);
                foreach (var p in AllPeers()) p.CloseAfterSend();   // Bye должен успеть уйти, иначе друг полторы минуты переподключается
                if (ViaSteam) Steam.SetPresence(null);
                try { SteamStatus.Clear(); } catch { }
                Log(reason ?? "Отключено.");
            }
            TcpPeer.StopListening();
            Peers.Clear();
            _host = null;
            _connecting = null;
            _reconnectUntil = -1f;
            _reaskAfterWelcome = false;
            foreach (var pl in Players.Values) pl.Puppet.Destroy();
            Players.Clear();
            while (Accepted.TryDequeue(out _)) { }
            while (Inbox.Queue.TryDequeue(out _)) { }
            Mode = Mode.Off;
            try { FoodBalanceLink.Tick(); } catch { }   // ползунки хоста больше не действуют
            Status = reason ?? UI.L.T("Не подключено", "Not connected");
            HostModeType = -1;
            _customApplied = "";
            HostRegionKey = "";
            _hostAllowsTeleport = true;
            Array.Clear(_hostForAll, 0, _hostForAll.Length);
            _join = JoinStep.None;
            _askWorldAt = -1f;
            _startMode = -1;
            _startRegion = "";
            _meetTarget = -1;
            _clientRules = null;
            Death.Reset();        // посреди смерти: таймеры не должны дойти до правил коопа без коопа
            ActionTime.Reset();
            ItemSync.Reset();
            EventLog.Reset();
            WorldTransfer.ResetClient();
            Outbox.Clear();
            ClientLog.Reset();
            SyncCheck.Reset();
            ArrowFlight.Reset();
            Game.FlareShots.Reset();
            WorldObjects.Reset();
            SceneHandoff.Reset();
            ContentCheck.Reset();
            ContainerLoot.Reset();
            WorldStates.Reset();
            Chronicle.Reset();
            SceneSnapshot.Reset();
            RadialSync.Reset();
            WeatherSync.Reset();
            FilterTags.Reset();
            QuestWait.Reset();
            AnimalSync.Reset();
            GlobalSync.Reset();
            VoiceSync.Reset();
            Together.Reset();
            Rescue.Reset();
            TrainSync.Reset();
            TrainSync.ForgetWorld();
            IslandSync.Reset();
            WorldEvents.Reset();
            SoundSync.Reset();
            Load.Reset();
            MapShare.Reset();
            Trade.Reset();
            Summary.Reset();
            FriendTracks.Reset();
            Challenges.Reset();
            SharedRecipes.Reset();
            SmokehouseLink.Reset();
            QuestItems.Reset();
            RandomObjects.Reset();
            BotTests.Reset();
            SleepSync.Reset();
            HostWeatherSync = false;
            PlayerProgress.Reset();
            InHostWorld = false;
            JoinInfo = "";
            Invited.Clear();
        }

        // Выход из игры: прощание (Bye) должно успеть уйти, пока игра не закрылась.
        public static void Quit()
        {
            bool active = Mode != Mode.Off;
            Stop();
            if (!active) return;
            // Сессии Steam здесь не закрываем: игра закрывается сама, а закрытие выбросило бы неотправленное.
            for (int i = 0; i < 6; i++)
            {
                try { SteamPeer.TickLingering(); } catch { }
                System.Threading.Thread.Sleep(50);
            }
        }

        // ---------- Хост вышел из мира ----------
        // Друг играет в копии мира хоста, без хоста этот мир не живёт. Когда хост вышел в главное меню
        // или пропал со связи, друг тоже выходит в главное меню. Вернётся хост в мир — друг загрузится к нему сам.

        private static float _leaveAt = -1f;
        private static int _hostMenuSeen;

        private static bool InMainMenu()
        {
            try { return Il2Cpp.GameManager.IsMainMenuActive(); } catch { return false; }
        }

        private static void HostGone(string reason)
        {
            LeaveHostWorld(reason, 3f, false);
            Stop(reason);
        }

        private static void LeaveHostWorld(string why, float delay, bool save)
        {
            if (_leaveAt >= 0f || Mode != Mode.Client || !InHostWorld || !GameApi.InGame()) return;
            Log(why + " Выхожу в главное меню.");
            GameApi.HudMessage(why + " Выхожу в главное меню.");
            Death.Reset();   // лежал раненым: поднимаем до сохранения, чтобы в прогресс не ушло «при смерти»
            // Хост ещё на связи: сохраняемся, чтобы прогресс успел уйти к нему.
            if (save) PlayerProgress.OnSaveRequest();
            _leaveAt = Time.unscaledTime + delay;
        }

        private static void TickLeaveWorld()
        {
            if (_leaveAt < 0f || Time.unscaledTime < _leaveAt) return;
            _leaveAt = -1f;
            _hostMenuSeen = 0;
            InHostWorld = false;
            _join = JoinStep.None;
            if (!GameApi.InGame()) return;
            Death.Reset();
            try { Il2Cpp.GameManager.DoExitToMainMenu(); }
            catch (Exception e)
            {
                MelonLogger.Warning("[кооп] выход в меню: " + e.Message);
                try { Il2Cpp.GameManager.LoadMainMenu(); } catch { }
            }
        }

        internal static Peer PeerById(int id) { foreach (var p in Peers) if (p.Id == id && p.Greeted && !p.Closed) return p; return null; }

        private static IEnumerable<Peer> AllPeers()
        {
            if (_host != null) yield return _host;
            foreach (var p in Peers) yield return p;
        }

        // ---------- Каждый кадр ----------

        public static void Tick()
        {
            TickLeaveWorld();
            try { SteamPeer.TickLingering(); } catch (Exception e) { MelonLogger.Warning("[сеть] закрытие Steam: " + e.Message); }
            try { QuickStart.Tick(); } catch (Exception e) { MelonLogger.Warning("[быстрый старт] " + e.Message); }
            if (Mode == Mode.Off) return;
            float now = Time.unscaledTime;
            Load.Begin();

            if (Mode == Mode.Client && _connecting != null && _connecting.IsCompleted)
            {
                if ((_connecting.IsFaulted || _connecting.Result == null) && Reconnecting)
                {
                    _connecting = null;
                    _nextReconnectTry = now + 5f;
                }
                else if (_connecting.IsFaulted || _connecting.Result == null)
                {
                    var msg = _connecting.Exception?.GetBaseException().Message ?? "нет ответа";
                    Stop(UI.L.T("Не удалось подключиться: ", "Could not connect: ") + msg);
                    return;
                }
                else
                {
                    _host = _connecting.Result;
                    _host.LastHeard = now;
                    _connecting = null;
                }
            }

            while (Accepted.TryDequeue(out var accepted))
            {
                accepted.LastHeard = now;
                Peers.Add(accepted);
                MelonLogger.Msg("[сеть] входящее подключение " + accepted.Describe);
            }

            if (ViaSteam) Steam.Poll(OnSteamMessage);
            while (Inbox.Queue.TryDequeue(out var item))
            {
                if (item.Data == null) OnClosed(item.Peer);
                else Handle(item.Peer, item.Data, now);
                if (Mode == Mode.Off) return;
            }

            if (Mode == Mode.Host && ViaSteam && now >= _nextScan)
            {
                _nextScan = now + 1f;
                AcceptSteamJoins(now);
            }

            if (Mode == Mode.Client && _host != null && !_welcomed && now >= _nextHello)
            {
                _nextHello = now + 1f;
                var w = Msg.New(MsgType.Hello);
                w.Write(Protocol.Version);
                w.Write(LocalName);
                w.Write(CoopByEMPEROR.Version.Number);
                EventLog.WriteHello(w);
                w.Write(Config.IpPassword);
                w.Write(ClientLog.LogId);
                _host.Send(Msg.Done(w));
            }
            foreach (var p in AllPeers()) p.Flush();
            if (Mode != Mode.Off && now >= _nextSeqTick)
            {
                _nextSeqTick = now + 1f;
                if (Mode == Mode.Client) { if (_welcomed) EventLog.ClientTick(ApplyLogged); }
                else foreach (var p in Peers) if (p.Greeted) ClientLog.HostTick(p, PlayerProgress.KeyOf(p, Players.TryGetValue(p.Id, out var tp) ? tp.Name : ""));
            }
            if (Mode != Mode.Off && now >= _nextNetLog)
            {
                _nextNetLog = now + 120f;
                if (EventLog.Current > 0 || SteamPeer.Failed > 0 || Mode == Mode.Client)
                    MelonLogger.Msg($"[сеть] {EventLog.Stats}; {ClientLog.Stats}; Steam не принял {SteamPeer.Failed}, дослал {SteamPeer.Retried}, бросил {SteamPeer.Dropped}");
            }
            if (Mode == Mode.Client && Reconnecting) TickReconnect(now);
            if (Mode == Mode.Off) return;
            if (Mode == Mode.Client && !_welcomed && !Reconnecting && now - _joinStarted > 30f)
            {
                Stop(UI.L.T("Хост не ответил. Он точно создал сервер?", "The host did not answer. Is the server really created?"));
                return;
            }

            if (now >= _nextPing)
            {
                _nextPing = now + 2f;
                var pw = Msg.New(MsgType.Ping);
                pw.Write(now);
                pw.Write((byte)0);
                var ping = Msg.Done(pw);
                foreach (var p in AllPeers()) p.Send(ping);
            }

            CheckTimeouts(now);
            if (Mode == Mode.Off) return;

            bool inGame = GameApi.InGame();
            if (inGame && now >= _nextState && (Mode == Mode.Host || _welcomed))
            {
                _nextState = now + StateInterval;
                SendOwnState();
            }
            if (Mode == Mode.Host && now >= _nextWorld)
            {
                _nextWorld = now + 2f;
                SendWorld();
                SendRules();
            }

            Load.Lap(Load.Net);
            UpdatePuppets(inGame, now);
            Load.Lap(Load.Puppets);
            if (Mode == Mode.Host) WorldTransfer.TickHost(now);
            ItemSync.Tick(now);
            Load.Lap(Load.Items);
            WorldObjects.Tick();
            ContainerLoot.Tick();
            FoodBalanceLink.Tick();
            WorldStates.Tick(now);
            Chronicle.Tick(now);
            Load.Lap(Load.Objects);
            WorldExtra.Tick(now);
            ArrowFlight.Tick(now);
            Game.FlareShots.Tick(now);
            Load.Lap(Load.Extra);
            GlobalSync.Tick(now);
            SharedRecipes.Tick(now);
            SmokehouseLink.Tick(now);
            Load.Lap(Load.Global);
            VoiceSync.Tick(now);
            Load.Lap(Load.Voice);
            Together.Tick();
            Trade.Tick();
            Load.Lap(Load.Together);
            TrainSync.Tick(now);
            Load.Lap(Load.Train);
            IslandSync.Tick(now);
            Load.Lap(Load.Island);
            WorldEvents.Tick(now);
            Load.Lap(Load.Events);
            SoundSync.Tick();
            Load.Lap(Load.Sound);
            if (Mode == Mode.Host) BotTests.HostTick(now);
            SceneSnapshot.Tick();
            LoadVeil.Tick();
            Load.Lap(Load.Snapshot);
            RadialSync.Tick();
            Load.Lap(Load.Radial);
            WeatherSync.Tick(now);
            FilterTags.Tick(now);
            QuestWait.Tick(now);
            Load.Lap(Load.Weather);
            AnimalSync.Tick(now);
            Load.Lap(Load.Animals);
            SleepSync.Tick(now);
            StruggleGuard.Tick(now);
            RenderCheck.Tick(now);
            ActionTime.Tick();
            Load.Lap(Load.Sleep);
            PlayerProgress.Tick(now);
            SceneHandoff.Tick(now);
            Summary.Tick(now);
            Challenges.Tick(now);
            Characters.ApplyOwnGender(now);
            SteamStatus.Tick(now);
            DeathPacks.Tick();
            Claims.Tick();
            PlantRegrow.Tick(now);
            QuestItems.Tick(now);
            Load.Lap(Load.Progress);
            SyncCheck.Tick(now);
            Load.Lap(Load.Check);
            if (Mode == Mode.Client && _welcomed) TickJoinWorld(inGame, now);
            TickMeeting(inGame, now);
            Load.Lap(Load.Other);
            Load.End(now);
        }

        // ---------- Лобби и старт игры ----------
        // Хост в главном меню = лобби. Друзья подключаются и ждут. Когда хост жмёт «Начать»,
        // всем приходит Start с режимом и регионом, и все начинают загрузку одновременно.
        // Если хост грузит своё сохранение (режим неизвестен), друзья начинают, когда хост уже в мире.

        private enum JoinStep { None, Starting, Done }
        private static JoinStep _join;
        private static float _joinStepTime;
        private static int _startMode = -1;
        private static string _startRegion = "";

        public static string JoinInfo { get; private set; } = "";
        // Друг сейчас в мире хоста (загрузил присланное сохранение), а не в своём.
        public static bool InHostWorld { get; private set; }
        // Игрок в общем мире: хост — в своём, друг — в мире хоста.
        public static bool InSharedWorld => Mode == Mode.Host || (Mode == Mode.Client && InHostWorld);
        // Друг подключён, но играет в своём сохранении: события мира хоста к нему не относятся.
        public static bool InOwnWorld => Mode == Mode.Client && !InHostWorld && GameApi.InGame();
        // Этот игрок со мной в общем мире и в этой локации. Друг, который подключён, но играет в своём сохранении
        // (InWorld = false), не считается рядом, даже если локация называется так же.
        public static bool With(Player p, string scene) => p != null && p.InWorld && InSharedWorld && p.Scene.Length > 0 && p.Scene == scene;
        public static bool InLobby => Mode != Mode.Off && !GameApi.InGame();

        // Хост начал новую кампанию через меню игры (Game/StartHooks.cs). mode = -1: регион
        // неизвестен (случайный или загрузка сохранения), друзья ждут, пока хост окажется в мире.
        public static void AnnounceNewGame(int mode, string region)
        {
            if (Mode != Mode.Host) return;
            var w = Msg.New(MsgType.Start);
            w.Write(mode);
            w.Write(region ?? "");
            Broadcast(Msg.Done(w));
            Log("Хост начинает игру. Друзья загрузятся в его мир, как только он там окажется.");
        }

        // Друг попадает в мир хоста, загружая копию его сохранения (Game/WorldTransfer.cs).
        // Из главного меню — сам, как только хост в мире. Из своей игры — кнопкой в окне.
        private static void TickJoinWorld(bool inGame, float now)
        {
            Players.TryGetValue(0, out var host);
            bool hostInWorld = host != null && host.Scene.Length > 0 && now - host.LastState < 5f;
            WorldTransfer.TickClient(now);
            TickAskWorld(now);
            if (!inGame && Il2Cpp.GameManager.IsMainMenuActive()) InHostWorld = false;
            if (WorldTransfer.JustLoaded)
            {
                _join = JoinStep.Done;
                InHostWorld = true;
                Log("Ты в мире хоста.");
                if (hostInWorld && !WorldTransfer.Restored) GoToPlayer(0);
            }
            if (WorldTransfer.State.Length > 0) JoinInfo = WorldTransfer.State;
            switch (_join)
            {
                case JoinStep.None:
                    if (inGame)
                    {
                        _join = JoinStep.Done;
                        JoinInfo = UI.L.T("Ты в своём мире. Чтобы играть в мире хоста, нажми «Загрузиться в мир хоста».", "You are in your own world. To play in the host's world, press “Load into host's world”.");
                        return;
                    }
                    if (!Config.AutoJoinWorld || !Il2Cpp.GameManager.IsMainMenuActive() || !hostInWorld) return;
                    LoadHostWorld();
                    return;

                case JoinStep.Starting:
                    if (!WorldTransfer.Busy && now - _joinStepTime > 60f && !inGame)
                    {
                        _join = JoinStep.None;   // хост не ответил — спросим снова
                        JoinInfo = UI.L.T("Хост не прислал мир, пробую ещё раз…", "The host did not send the world, trying again…");
                    }
                    return;
            }
        }

        // Друг: запросить мир хоста и загрузиться в него.
        public static void LoadHostWorld()
        {
            if (Mode != Mode.Client || _host == null || WorldTransfer.Busy || _askWorldAt >= 0f) return;
            _join = JoinStep.Starting;
            _joinStepTime = Time.unscaledTime;
            // Уже в мире хоста (перезагрузка): сначала свой прогресс хосту, иначе он вставит в копию мира
            // прогресс прошлого сохранения, и место, состояние, навыки и карта откатятся на минуты.
            if (InHostWorld && GameApi.InGame() && !Death.IsDowned)
            {
                _askWorldAt = Time.unscaledTime;
                PlayerProgress.OnSaveRequest();
                MelonLogger.Msg("[мир] перед перезагрузкой мира отправляю хосту свой прогресс");
                return;
            }
            WorldTransfer.Request(_host);
        }

        // Мир просим, когда свой прогресс ушёл хосту (сообщения идут по порядку) или через 20 с, если игра так и не сохранилась.
        private static float _askWorldAt = -1f;

        private static void TickAskWorld(float now)
        {
            if (_askWorldAt < 0f) return;
            if (PlayerProgress.ProgressSentAt < _askWorldAt && now - _askWorldAt < 20f) return;
            _askWorldAt = -1f;
            // Пока ждали, хост вышел в меню и мы выходим следом — мир не просим.
            if (Mode == Mode.Client && _host != null && !WorldTransfer.Busy && _leaveAt < 0f) WorldTransfer.Request(_host);
        }

        // ---------- Встать рядом с игроком ----------
        // Если он в другой сцене — сначала переходим в неё, потом ставим рядом.

        private static int _meetTarget = -1;
        private static float _meetTime;
        private static string _meetSceneRequested;

        public static void GoToPlayer(int id)
        {
            _meetTarget = id;
            _meetTime = Time.unscaledTime;
            _meetSceneRequested = null;
        }

        public static void MeetHost() => GoToPlayer(0);

        // Хост: позвать к себе одного игрока (id) или всех (-1). Друг сам перейдёт в локацию хоста.
        public static void Summon(int id)
        {
            if (Mode != Mode.Host) return;
            var data = Msg.Done(Msg.New(MsgType.Summon));
            foreach (var p in Peers)
                if (p.Greeted && (id < 0 || p.Id == id)) p.Send(data);
            Log(id < 0 ? "Ты позвал всех к себе." : "Ты позвал к себе " + (Players.TryGetValue(id, out var pl) ? pl.Name : "игрока") + ".");
        }

        private static void TickMeeting(bool inGame, float now)
        {
            if (_meetTarget < 0) return;
            if (!Players.TryGetValue(_meetTarget, out var target) || now - _meetTime > 150f)
            {
                _meetTarget = -1;
                JoinInfo = "";
                return;
            }
            // Только в общем мире: друг в своём сохранении — там хоста нет (и наоборот), переносить некуда.
            if (!InSharedWorld || !target.InWorld)
            {
                if (inGame && Mode == Mode.Host) Log(target.Name + " играет в своём сохранении, к нему не перейти.");
                _meetTarget = -1;
                JoinInfo = "";
                return;
            }
            if (!inGame || target.Scene.Length == 0 || now - target.LastState > 5f) return;
            if (now - _meetTime < 3f) return; // пусть сцена догрузится
            // На тросе или в машине телепорт сбросит с троса или поставит в кузов — ждём, пока слезет.
            // Одна сцена Unity бывает у нескольких домиков: та же копия — только при том же файле сохранения.
            string myKey = ItemSync.SceneKey();
            bool sameScene = target.Scene == GameApi.Scene() && (target.SceneKey.Length == 0 || target.SceneKey == myKey);
            string why = OnRopeOrInCar(null);
            if (why == null && sameScene) why = OnRopeOrInCar(target);
            if (why != null)
            {
                JoinInfo = UI.L.T("Жду: ", "Waiting: ") + why + "…";
                return;
            }
            if (sameScene)
            {
                // Чуть в стороне, чтобы не появиться внутри игрока.
                float a = (float)(target.Yaw * Math.PI / 180.0);
                GameApi.Teleport(target.X - (float)Math.Sin(a) * 1.5f, target.Y, target.Z - (float)Math.Cos(a) * 1.5f);
                _meetTarget = -1;
                JoinInfo = "";
                Log("Ты рядом с " + target.Name + ".");
                return;
            }
            string want = target.SceneKey.Length > 0 ? target.SceneKey : target.Scene;
            if (_meetSceneRequested == want) return;
            _meetSceneRequested = want;
            _meetTime = now;
            JoinInfo = UI.L.T("Перехожу в локацию ", "Moving to the location of ") + target.Name + "…";
            Log("Перехожу в локацию " + target.Name + " (" + target.Scene + ").");
            GameApi.LoadScene(target.Scene, target.SceneKey);
        }

        // ---------- Приглашения ----------
        // Хост дописывает SteamID приглашённых в свой Rich Presence: «host|id1,id2».
        // Друг видит, что его пригласили, и может принять.

        private static readonly HashSet<ulong> Invited = new();

        public static void Invite(ulong friend)
        {
            if (Mode != Mode.Host || !ViaSteam) return;
            Invited.Add(friend);
            Steam.SetPresence("host|" + string.Join(",", Invited));
            Log("Приглашение отправлено: " + Steam.FriendName(friend) + ".");
        }

        public static bool IsInvited(ulong friend) => Invited.Contains(friend);

        // Цвет игрока (hex без #) или запасной, если он цвет не выбирал.
        public static string ColorOf(int id, string fallback)
        {
            string c = id == LocalId ? Config.NickColor : Players.TryGetValue(id, out var pl) ? pl.Color : "";
            return c.Length > 0 ? c : fallback;
        }

        public static void Announce(string text) => SendChat(text);

        // ---------- Правила сервера ----------

        private static Rules _clientRules;
        public static Rules CurrentRules => Mode == Mode.Host ? Rules.Host : _clientRules ?? new Rules();

        private static void SendRules()
        {
            var w = Msg.New(MsgType.Rules);
            Rules.Host.FoodBalance = FoodBalanceLink.Export();
            Rules.Host.Write(w);
            Broadcast(Msg.Done(w));
        }

        // Хост: друзья, которые написали в Rich Presence «join:<мой SteamID>», хотят подключиться.
        // Отвечаем им первым сообщением — так Steam откроет сессию в обе стороны.
        private static void AcceptSteamJoins(float now)
        {
            string want = "join:" + Steam.MySteamId;
            foreach (var f in Steam.FriendsInGame(now))
            {
                if (f.Presence != want) continue;
                if (Peers.OfType<SteamPeer>().Any(p => p.SteamId == f.Id)) continue;
                if (SteamPeer.IsLingering(f.Id)) continue;   // только что закрыли (выгнан): сначала пусть уйдёт Reject
                var peer = new SteamPeer(f.Id) { LastHeard = now };
                Peers.Add(peer);
                peer.Send(Msg.Done(Msg.New(MsgType.Ping)));
                MelonLogger.Msg($"[steam] {f.Name} ({f.Id}) хочет подключиться");
            }
        }

        private static void OnSteamMessage(ulong from, byte[] data)
        {
            float now = Time.unscaledTime;
            if (Mode == Mode.Client)
            {
                if (_host is SteamPeer hp && hp.SteamId == from) Handle(_host, data, now);
                return;
            }
            var peer = Peers.OfType<SteamPeer>().FirstOrDefault(p => p.SteamId == from);
            if (peer != null) Handle(peer, data, now);
        }

        private static void CheckTimeouts(float now)
        {
            if (Mode == Mode.Client)
            {
                if (_host != null && now - _host.LastHeard > Timeout && !StartReconnect(now)) HostGone("Связь с хостом потеряна.");
                return;
            }
            foreach (var p in Peers.ToList())
                if (now - p.LastHeard > Timeout) DropPeer(p, "пропал со связи");
        }

        // ---------- Переподключение ----------

        private static bool StartReconnect(float now)
        {
            if (Reconnecting) return now <= _reconnectUntil && RetryConnect(now);
            if (!InHostWorld || !GameApi.InGame() || !_welcomed) return false;
            _reconnectUntil = now + 90f;
            Log("Связь с хостом пропала. Переподключаюсь…");
            GameApi.HudMessage("Связь с хостом пропала, переподключаюсь…");
            MelonLogger.Msg("[сеть] связь с хостом пропала, переподключаюсь (до 90 с)");
            return RetryConnect(now);
        }

        private static bool RetryConnect(float now)
        {
            _welcomed = false;
            _nextHello = 0f;
            if (ViaSteam)
            {
                _host = new SteamPeer(_hostSteamId) { LastHeard = now };
                return true;
            }
            try { _host?.Close(); } catch { }
            _host = null;
            if (_lastIp.Length == 0) return false;
            _connecting = Task.Run(() => TcpPeer.Connect(_lastIp, _lastPort));
            return true;
        }

        private static void TickReconnect(float now)
        {
            if (now > _reconnectUntil) { HostGone("Связь с хостом потеряна, переподключиться не вышло."); return; }
            if (!ViaSteam && _host == null && _connecting == null && now >= _nextReconnectTry) RetryConnect(now);
        }

        private static void OnReconnected(bool hostRestarted)
        {
            if (!Reconnecting) return;
            _reconnectUntil = -1f;
            // Своё, что накопилось без связи, — хосту; раскладку своей локации спрашиваем заново.
            int sent = 0;
            while (Outbox.Count > 0) { _host?.Send(Outbox.Dequeue()); sent++; }
            if (sent > 0) MelonLogger.Msg($"[сеть] отправил хосту свои события, накопленные без связи: {sent}");
            Log("Связь с хостом восстановлена.");
            GameApi.HudMessage("Связь с хостом восстановлена");
            // Обрыв посреди передачи мира: недополученный мир сам не придёт — просим заново.
            if (WorldTransfer.Receiving)
            {
                MelonLogger.Msg("[сеть] переподключился к хосту посреди передачи мира, прошу мир заново");
                if (_host != null) WorldTransfer.Request(_host);
                return;
            }
            // Игра хоста перезапустилась (новый журнал): его мир откатился к сохранению, наша копия устарела.
            if (hostRestarted && InHostWorld && GameApi.InGame())
            {
                MelonLogger.Msg("[сеть] переподключился к хосту, но его игра перезапустилась: загружаю его мир заново");
                GameApi.HudMessage("Хост перезапустил игру — загружаю его мир заново");
                LoadHostWorld();
                return;
            }
            SceneSnapshot.AskAgain();
            // Ждали остальных, чтобы лечь спать: хост нас удалял и забыл — отмечаемся снова.
            var ready = SleepSync.ReadyAgain();
            if (ready != null) _host?.Send(ready);
            MelonLogger.Msg("[сеть] переподключился к хосту, остаюсь в его мире");
        }

        // ---------- Разбор сообщений ----------

        private static void Handle(Peer peer, byte[] data, float now)
        {
            peer.LastHeard = now;
            BinaryReader r;
            MsgType type;
            try
            {
                r = Msg.Read(data, out type);
            }
            catch
            {
                return;
            }
            if (type == MsgType.Big)
            {
                byte[] full = null;
                try { full = BigMsg.Add(peer, r); } catch (Exception e) { MelonLogger.Warning("[сеть] кусок большого сообщения: " + e.Message); }
                if (full != null) Handle(peer, full, now);
                return;
            }
            try
            {
                if (Mode == Mode.Host) HandleOnHost(peer, type, r, data);
                else HandleOnClient(type, r, data);
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[сеть] ошибка в сообщении {type}: {e.Message}");
            }
        }

        // Пинг: «0» — просим вернуть, «1» — наш вернулся, считаем время туда-обратно.
        private static void HandlePing(Peer peer, BinaryReader r)
        {
            if (r.BaseStream.Length - r.BaseStream.Position < 5) return;
            float t = r.ReadSingle();
            byte echo = r.ReadByte();
            if (echo == 0)
            {
                var w = Msg.New(MsgType.Ping);
                w.Write(t);
                w.Write((byte)1);
                peer.Send(Msg.Done(w), reliable: false);
                return;
            }
            float rtt = Time.unscaledTime - t;
            if (rtt < 0f || rtt > 30f) return;
            peer.Rtt = peer.Rtt < 0f ? rtt : peer.Rtt + (rtt - peer.Rtt) * 0.3f;
        }

        // Для строки состояния: пинг до хоста (у друга) или самый большой пинг среди друзей (у хоста), мс.
        public static int PingMs()
        {
            float best = -1f;
            foreach (var p in AllPeers()) if (p.Rtt >= 0f && p.Rtt > best) best = p.Rtt;
            return best < 0f ? -1 : Mathf.RoundToInt(best * 1000f);
        }

        private static float _lastVersionWarn = -100f;
        // Хост: кого выгнали (Steam id или ник по IP). Не пускаем обратно, пока хост не запустит сервер заново.
        private static readonly HashSet<string> Kicked = new();

        private static float _ownWorldWarn = -100f;

        // Номер игрока — один байт, 0 у хоста. Берём первый свободный: раньше номер рос с каждым подключением
        // (по IP — и с каждым переподключением и запуском бота) и после 255 становился 0, как у хоста.
        private static int FreeId()
        {
            for (int i = 1; i < 255; i++)
                if (!Players.ContainsKey(i) && !Peers.Exists(p => p.Greeted && p.Id == i)) return i;
            return _nextId++ & 0xFF;
        }

        private static bool IsLocalPeer(Peer peer) => peer is TcpPeer && (peer.Describe.StartsWith("127.0.0.1") || peer.Describe.StartsWith("[::1]") || peer.Describe.StartsWith("[::ffff:127.0.0.1]"));

        private static void HandleOnHost(Peer peer, MsgType type, BinaryReader r, byte[] raw)
        {
            // Steam-друг, которого мы сбросили (выгнан, тишина, Steam не принимал): по «join:» хост тут же завёл
            // новое соединение, а друг считает себя подключённым и Hello не шлёт. Его события молча выбрасывались:
            // просим поздороваться заново.
            if (!peer.Greeted && peer is SteamPeer && type != MsgType.Ping && type != MsgType.Hello && type != MsgType.Bye
                && Time.unscaledTime - peer.HelloAskedAt > 2f)
            {
                peer.HelloAskedAt = Time.unscaledTime;
                peer.Send(Msg.Done(Msg.New(MsgType.HelloAgain)));
                MelonLogger.Msg($"[сеть] {peer.Describe} шлёт {type} без приветствия: прошу поздороваться заново");
            }
            // Друг подключён, но играет в своём сохранении: его выбросы, подборы и ящики к нашему миру не относятся.
            if (peer.Greeted && Players.TryGetValue(peer.Id, out var ownWorld) && !ownWorld.InWorld && ownWorld.LastState > 0f
                && (EventLog.Logged((byte)type) || type == MsgType.ContainerClaim || type == MsgType.ContainerAsk || type == MsgType.SceneJoin || type == MsgType.SceneState))
            {
                if (Time.unscaledTime - _ownWorldWarn > 30f) { _ownWorldWarn = Time.unscaledTime; MelonLogger.Msg($"[мир] {ownWorld.Name} в своём мире, его события мира не применяю ({type})"); }
                return;
            }
            switch (type)
            {
                case MsgType.Ping:
                    HandlePing(peer, r);
                    break;
                case MsgType.Hello:
                {
                    int ver = r.ReadInt32();
                    string name = Clean(r.ReadString());
                    string theirMod = r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : "";
                    int theirLog = 0;
                    uint theirLast = 0;
                    if (r.BaseStream.Length - r.BaseStream.Position >= 8) { theirLog = r.ReadInt32(); theirLast = r.ReadUInt32(); }
                    string theirPassword = r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : "";
                    int theirOutLog = r.BaseStream.Length - r.BaseStream.Position >= 4 ? r.ReadInt32() : 0;
                    // Вход по IP: если хост задал пароль, без него не пускаем (через Steam заходят только друзья по приглашению).
                    // С этого же компьютера (бот) — без пароля.
                    if (peer is TcpPeer && !IsLocalPeer(peer) && Config.IpPassword.Length > 0 && theirPassword != Config.IpPassword && ver == Protocol.Version)
                    {
                        if (Time.unscaledTime - _lastVersionWarn > 10f)
                        {
                            _lastVersionWarn = Time.unscaledTime;
                            Log($"{name} ({peer.Describe}) не смог подключиться: неверный пароль.");
                        }
                        var bad = Msg.New(MsgType.Reject);
                        bad.Write("Неверный пароль сервера. Впиши пароль хоста в UserData/MelonPreferences.cfg, раздел [TLDCoop], строка IpPassword.");
                        peer.Send(Msg.Done(bad));
                        return;
                    }
                    if (ver != Protocol.Version)
                    {
                        string theirs = theirMod.Length > 0 ? theirMod : $"старая (протокол {ver})";
                        if (Time.unscaledTime - _lastVersionWarn > 10f)
                        {
                            _lastVersionWarn = Time.unscaledTime;
                            Log($"{name} не смог подключиться: у него мод {theirs}, у тебя {CoopByEMPEROR.Version.Number}.");
                            GameApi.HudMessage($"{name}: другая версия мода ({theirs}), нужна {CoopByEMPEROR.Version.Number}");
                        }
                        var rej = Msg.New(MsgType.Reject);
                        rej.Write($"Разные версии мода: у хоста {CoopByEMPEROR.Version.Number}, у тебя {theirs}. " +
                                  $"Поставьте оба одну и ту же версию CoopByEMPEROR.dll.");
                        peer.Send(Msg.Done(rej));
                        return;
                    }
                    if (Kicked.Contains(PlayerProgress.KeyOf(peer, name)))
                    {
                        if (Time.unscaledTime - _lastVersionWarn > 10f)
                        {
                            _lastVersionWarn = Time.unscaledTime;
                            Log($"{name} выгнан и пробует зайти снова: не пускаю до перезапуска сервера.");
                        }
                        var kicked = Msg.New(MsgType.Reject);
                        kicked.Write("Хост выгнал тебя из игры. Зайти снова можно, когда хост перезапустит сервер.");
                        peer.Send(Msg.Done(kicked));
                        return;
                    }
                    bool newcomer = !peer.Greeted;
                    // По IP прогресс и журнал привязаны к нику: второй игрок с тем же ником перезаписывал бы прогресс первого.
                    // Тот же ник с того же адреса — это он же переподключается: старое соединение закрываем.
                    if (!peer.Greeted && peer is TcpPeer)
                    {
                        // Сравниваем ключ (KeyOf), а не ник: «Саша 1» и «Саша-1» дают один ключ — общий журнал, прогресс и бан.
                        string nameKey = PlayerProgress.KeyOf(peer, name);
                        var same = Peers.Find(p => p != peer && p.Greeted && p is TcpPeer && Players.TryGetValue(p.Id, out var op) && PlayerProgress.KeyOf(p, op.Name) == nameKey);
                        if (same != null)
                        {
                            string Host(string d) { int k = d.LastIndexOf(':'); return k > 0 ? d.Substring(0, k) : d; }
                            if (Host(same.Describe) == Host(peer.Describe)) DropPeer(same, "переподключился");
                            else
                            {
                                var dup = Msg.New(MsgType.Reject);
                                dup.Write($"Ник «{name}» уже занят другим игроком (или слишком похож на его ник). Поменяй ник в настройках мода.");
                                peer.Send(Msg.Done(dup));
                                return;
                            }
                        }
                    }
                    if (!peer.Greeted)
                    {
                        peer.Greeted = true;
                        peer.Id = FreeId();
                        Players[peer.Id] = new Player { Id = peer.Id, Name = name };
                        var joined = Msg.New(MsgType.Joined);
                        joined.Write((byte)peer.Id);
                        joined.Write(name);
                        Broadcast(Msg.Done(joined), except: peer);
                        Log(name + " подключился.");
                        GameApi.HudMessage(name + " подключился");
                        peer.Send(ContentCheck.Build());
                    }
                    var w = Msg.New(MsgType.Welcome);
                    w.Write((byte)peer.Id);
                    w.Write((byte)(Players.Count + 1));
                    w.Write((byte)0);
                    w.Write(LocalName);
                    foreach (var pl in Players.Values)
                    {
                        w.Write((byte)pl.Id);
                        w.Write(pl.Name);
                    }
                    w.Write(EventLog.LogId);
                    w.Write(EventLog.Current);
                    w.Write(ClientLog.HostHas(PlayerProgress.KeyOf(peer, name), theirOutLog));   // до какого его события мы всё получили
                    w.Write(CoopByEMPEROR.Version.Number);   // протокол тот же, но сборки могут отличаться — пусть друг увидит
                    w.Write(PlayerProgress.HostWorldKey() ?? "");   // 0.19.51: какой это мир — друг принесёт свою копию прогресса
                    peer.Send(Msg.Done(w));
                    // Протокол совпал, а сборка разная: пускаем, но предупреждаем — исправления последних версий у него не работают.
                    if (theirMod.Length > 0 && theirMod != CoopByEMPEROR.Version.Number)
                    {
                        Log($"{name}: у него мод {theirMod}, у тебя {CoopByEMPEROR.Version.Number}. Исправления новой версии у него не работают, поставьте одинаковую.");
                        GameApi.HudMessage($"{name}: у него мод {theirMod}, у тебя {CoopByEMPEROR.Version.Number}");
                    }
                    if (newcomer)
                    {
                        Chronicle.SendHistory(peer);
                        Chronicle.Add(name + " присоединился");
                    }
                    // Друг вернулся после обрыва связи: досылаем всё, что случилось в мире без него.
                    if (theirLog == EventLog.LogId && theirLast < EventLog.Current && peer.Resent < EventLog.Current)
                    {
                        int n = EventLog.HostResend(peer, PlayerProgress.KeyOf(peer, name), Math.Max(theirLast + 1, peer.Resent + 1), EventLog.Current);
                        if (theirLast + 1 < EventLog.Oldest) EventLog.SendSkipOrReload(peer, !Players.TryGetValue(peer.Id, out var back) || back.InWorld);
                        peer.Resent = EventLog.Current;
                        MelonLogger.Msg($"[сеть] {name} вернулся после обрыва: дослал пропущенные события мира ({n}, с №{theirLast + 1})" + (theirLast + 1 < EventLog.Oldest ? $", но старше №{EventLog.Oldest} уже нет в журнале" : ""));
                    }
                    break;
                }
                case MsgType.CSeq:
                {
                    if (!peer.Greeted) return;
                    int clog = r.ReadInt32();
                    uint cseq = r.ReadUInt32();
                    string ckey = PlayerProgress.KeyOf(peer, Players.TryGetValue(peer.Id, out var cpl) ? cpl.Name : "");
                    int cleft = (int)(r.BaseStream.Length - r.BaseStream.Position);
                    ClientLog.HostReceive(peer, ckey, clog, cseq, cleft > 0 ? r.ReadBytes(cleft) : null, ApplyFromClient);
                    return;
                }
                case MsgType.SeqCtl:
                {
                    if (!peer.Greeted) return;
                    byte kind = r.ReadByte();
                    if (kind == ClientLog.KHostAck) EventLog.HostAcked(peer, r);
                    else if (kind == ClientLog.KGone) ClientLog.HostGone(peer, PlayerProgress.KeyOf(peer, Players.TryGetValue(peer.Id, out var gpl) ? gpl.Name : ""), r, ApplyFromClient);
                    return;
                }
                case MsgType.SeqResend:
                    if (peer.Greeted) EventLog.HostReceiveResend(peer, PlayerProgress.KeyOf(peer, Players.TryGetValue(peer.Id, out var resender) ? resender.Name : ""), r, resender == null || resender.InWorld);
                    break;
                case MsgType.GiveAck:
                    if (!peer.Greeted) return;
                    Together.ReceiveGiveAck(r);
                    Broadcast(raw, except: peer);
                    break;
                case MsgType.WorldRequest:
                    if (peer.Greeted) WorldTransfer.HostRequest(peer, PlayerProgress.KeyOf(peer, Players.TryGetValue(peer.Id, out var asker) ? asker.Name : ""));
                    break;
                case MsgType.ItemDrop:
                case MsgType.ItemPick:
                case MsgType.ItemUpdate:
                    if (!peer.Greeted) return;
                    if (type == MsgType.ItemPick && !ItemSync.HostAcceptPick(peer, raw)) return;
                    if (type == MsgType.ItemUpdate && ItemSync.PickedRecently(raw)) return;   // вещь уже подняли: дальше не шлём
                    ItemSync.Receive(type, raw);
                    Broadcast(raw, except: peer);
                    break;
                case MsgType.ObjectState:
                    if (!peer.Greeted) return;
                    if (WorldObjects.Header(raw).kind == WorldObjects.Kind.Spawner && !RadialSync.HostAccept(peer, raw)) return;
                    WorldObjects.Receive(raw);
                    Broadcast(raw, except: peer);
                    break;
                case MsgType.SpawnClaim:
                    if (peer.Greeted) RadialSync.HostClaim(peer, r);
                    break;
                case MsgType.Recipes:
                    if (peer.Greeted) SharedRecipes.HostReceive(peer, r);
                    break;
                case MsgType.Smokehouse:
                    if (!peer.Greeted) return;
                    SmokehouseLink.Receive(r);
                    Broadcast(raw, except: peer);
                    break;
                case MsgType.SmokehouseAll:
                    if (peer.Greeted) SmokehouseLink.HostAll(peer, r);
                    break;
                case MsgType.SmokehouseAsk:
                    if (peer.Greeted) SmokehouseLink.HostAsk(peer, r);
                    break;
                case MsgType.Chronicle:
                    if (peer.Greeted) Chronicle.HostReceive(r, Players.TryGetValue(peer.Id, out var writer) ? writer.Name : "друг");
                    break;
                case MsgType.ContainerClaim:
                    if (peer.Greeted) ContainerLoot.HostClaim(peer.Id, r, d => peer.Send(d));
                    break;
                case MsgType.ContainerAsk:
                    if (peer.Greeted) ContainerLoot.HostAsk(Players.TryGetValue(peer.Id, out var asker) ? asker.Name : "игрок " + peer.Id, r, d => peer.Send(d));
                    break;
                case MsgType.AnimalState:
                case MsgType.AnimalRemove:
                case MsgType.AnimalAuthority:
                case MsgType.AnimalHarvest:
                case MsgType.AnimalSound:
                case MsgType.AnimalSense:
                case MsgType.AnimalNoise:
                case MsgType.AnimalArrow:
                    // Животных может вести любой игрок: хост читает сам и пересылает остальным.
                    if (!peer.Greeted) return;
                    AnimalSync.ReceiveBroadcast(type, r);
                    Broadcast(raw, except: peer, reliable: type != MsgType.AnimalState && type != MsgType.AnimalAuthority && type != MsgType.AnimalSense);
                    break;
                case MsgType.PlayerVoice:
                    if (!peer.Greeted) return;
                    VoiceSync.Receive(r);
                    Broadcast(raw, except: peer);
                    break;
                case MsgType.Marker:
                    if (!peer.Greeted) return;
                    Together.ReceiveMarker(r);
                    Broadcast(raw, except: peer);
                    break;
                case MsgType.BotCheck:
                    if (peer.Greeted) BotTests.ReceiveCheck(r);
                    break;
                case MsgType.Hit:
                    if (!peer.Greeted) return;
                    FriendlyFire.Receive(r);
                    Broadcast(raw, except: peer);
                    break;
                case MsgType.TrainSync:
                case MsgType.IslandSync:
                    if (!peer.Greeted) return;
                    if (type == MsgType.TrainSync) TrainSync.Receive(r); else IslandSync.Receive(r);
                    Broadcast(raw, except: peer, reliable: !(raw.Length > 1 && raw[1] == 1 && type == MsgType.TrainSync));
                    break;
                case MsgType.PlayerSound:
                    if (!peer.Greeted) return;
                    SoundSync.Receive(r);
                    Broadcast(raw, except: peer, reliable: raw.Length > 1 && raw[1] == 0);
                    break;
                case MsgType.WorldEvent:
                    if (!peer.Greeted) return;
                    WorldEvents.Receive(r, raw, peer);
                    if (raw.Length > 1 && raw[1] != 3) Broadcast(raw, except: peer);
                    break;
                case MsgType.Feature:
                    if (!peer.Greeted) return;
                    Features.Receive(r);
                    Broadcast(raw, except: peer);
                    break;
                case MsgType.Fx:
                    if (!peer.Greeted) return;
                    if (Features.HostFx(r, peer)) Broadcast(raw, except: peer, reliable: false);
                    break;
                case MsgType.Drag:
                    if (!peer.Greeted) return;
                    Drag.Receive(r);
                    Broadcast(raw, except: peer, reliable: raw.Length > 3 && raw[3] == 0);
                    break;
                case MsgType.DownedInfo:
                case MsgType.ReviveHold:
                    if (!peer.Greeted) return;
                    if (type == MsgType.DownedInfo) Rescue.ReceiveInfo(r); else Rescue.ReceiveHold(r);
                    Broadcast(raw, except: peer);
                    break;
                case MsgType.Trade:
                    if (!peer.Greeted) return;
                    Trade.Receive(r);
                    Broadcast(raw, except: peer);
                    break;
                case MsgType.Summary:
                    if (peer.Greeted && Players.TryGetValue(peer.Id, out var walker)) Summary.Receive(r, walker.Name);
                    break;
                case MsgType.Quest:
                    if (peer.Greeted && Players.TryGetValue(peer.Id, out var quester)) QuestItems.Receive(r, peer, quester.Name);
                    break;
                case MsgType.GiftState:
                    if (peer.Greeted) Together.HostGiftState(peer.Id, r);
                    break;
                case MsgType.GiveItem:
                case MsgType.Revive:
                    if (!peer.Greeted) return;
                    if (type == MsgType.GiveItem) Together.ReceiveGive(r); else Together.ReceiveRevive(r);
                    Broadcast(raw, except: peer);
                    break;
                case MsgType.TraderState:
                    if (!peer.Greeted) return;
                    // Торговец один на всех, порядок решает хост: принял — рассылает всем, и тому, кто прислал.
                    // Раньше двое, поменявшие торговца почти одновременно, применяли чужое и менялись состояниями.
                    GlobalSync.Receive(type, r);
                    Broadcast(raw);
                    break;
                case MsgType.SleepReady:
                    if (peer.Greeted) SleepSync.HostReceive(peer.Id, r);
                    break;
                case MsgType.SleepStop:
                    if (peer.Greeted) SleepSync.HostWoke(Players.TryGetValue(peer.Id, out var waker) ? waker.Name : "друг");
                    break;
                case MsgType.AnimalHit:
                case MsgType.AnimalTransfer:
                {
                    if (!peer.Greeted) return;
                    string from = Players.TryGetValue(peer.Id, out var sender) ? sender.Name : "друг";
                    if (AnimalSync.AddressOf(raw) == LocalId) AnimalSync.ReceiveAddressed(type, r, from);
                    else Broadcast(raw, except: peer);
                    break;
                }
                case MsgType.Progress:
                    if (peer.Greeted && Players.TryGetValue(peer.Id, out var saver)) PlayerProgress.Receive(PlayerProgress.KeyOf(peer, saver.Name), saver.Name, r);
                    break;
                case MsgType.ProgressPart:
                    if (peer.Greeted && Players.TryGetValue(peer.Id, out var parter)) PlayerProgress.ReceivePart(PlayerProgress.KeyOf(peer, parter.Name), parter.Name, r);
                    break;
                case MsgType.OwnProgress:
                    if (peer.Greeted && Players.TryGetValue(peer.Id, out var owner)) PlayerProgress.ReceiveOwn(PlayerProgress.KeyOf(peer, owner.Name), owner.Name, r);
                    break;
                case MsgType.SceneSave:
                    if (peer.Greeted && Players.TryGetValue(peer.Id, out var handing)) SceneHandoff.HostReceive(peer.Id, handing.Name, r);
                    break;
                case MsgType.InvProgress:
                    if (peer.Greeted && Players.TryGetValue(peer.Id, out var packer)) PlayerProgress.ReceiveInventory(PlayerProgress.KeyOf(peer, packer.Name), packer.Name, r);
                    break;
                case MsgType.SceneJoin:
                case MsgType.SceneState:
                    if (peer.Greeted) SceneSnapshot.HostReceive(peer, type, r);
                    break;
                case MsgType.Content:
                    if (peer.Greeted && Players.TryGetValue(peer.Id, out var who)) ContentCheck.Receive(r, who.Name, true, peer.Id);
                    break;
                case MsgType.Check:
                    if (peer.Greeted && Players.TryGetValue(peer.Id, out var checker)) SyncCheck.Receive(peer, checker.Name, r);
                    break;
                case MsgType.CheckFix:
                    if (peer.Greeted) SyncCheck.HostForwardFix(peer, r);
                    break;
                case MsgType.CheckResend:
                    if (peer.Greeted) SyncCheck.HostForwardResend(r);
                    break;
                case MsgType.AuroraScreens:
                    if (peer.Greeted) AuroraPower.HostReceiveScreens(r);
                    break;
                case MsgType.PotAmounts:
                    if (!peer.Greeted) return;
                    PotAmounts.Receive(r);
                    Broadcast(raw, except: peer, reliable: false);
                    break;
                case MsgType.ArrowFlight:
                    if (!peer.Greeted) return;
                    ArrowFlight.Receive(r);
                    Broadcast(raw, except: peer);
                    break;
                case MsgType.FlareShot:
                    if (!peer.Greeted) return;
                    Game.FlareShots.Receive(r);
                    Broadcast(raw, except: peer);
                    break;
                case MsgType.QuestState:
                    if (peer.Greeted) QuestWait.HostReceive(peer, r);
                    break;
                case MsgType.PlayerState:
                {
                    if (!peer.Greeted) return;
                    r.ReadByte(); // id от клиента не доверяем
                    var pl = Players[peer.Id];
                    ReadState(r, pl);
                    Broadcast(WriteState(pl.Id, pl.Scene, pl.X, pl.Y, pl.Z, pl.Yaw, (byte)((pl.Crouched ? 1 : 0) | (pl.InWorld ? 2 : 0)), pl.Action, pl.CharacterAndColor, pl.Held, pl.HoldFlags, pl.Shots, pl.Busy,
                                         pl.Carry, pl.CarryPose, pl.CarryRel, pl.CarryRot, pl.Gesture, pl.Hp, pl.Vitals, pl.Pitch, pl.SceneKey), except: peer, reliable: false);
                    break;
                }
                case MsgType.Chat:
                {
                    if (!peer.Greeted) return;
                    r.ReadByte();
                    string text = Clean(r.ReadString(), 200);
                    ShowChat(peer.Id, text);
                    Broadcast(WriteChat(peer.Id, text), except: peer);
                    break;
                }
                case MsgType.Bye:
                    DropPeer(peer, "вышел");
                    break;
            }
        }

        private static void HandleOnClient(MsgType type, BinaryReader r, byte[] raw)
        {
            switch (type)
            {
                case MsgType.Ping:
                    if (_host != null) HandlePing(_host, r);
                    break;
                case MsgType.Seq:
                {
                    int logId = r.ReadInt32();
                    uint seq = r.ReadUInt32();
                    // Пока идёт мир хоста, события пропускаем: что из них уже в файле, решит номер отсечения,
                    // остальное хост дошлёт после последней части мира.
                    if (WorldTransfer.Receiving) break;
                    int left = (int)(r.BaseStream.Length - r.BaseStream.Position);
                    EventLog.ClientReceive(logId, seq, left > 0 ? r.ReadBytes(left) : null, ApplyLogged);   // пустое — своё событие, только номер
                    break;
                }
                case MsgType.SeqSkip:
                    EventLog.ClientSkipTo(r, ApplyLogged);
                    break;
                case MsgType.SeqCtl:
                {
                    byte kind = r.ReadByte();
                    if (kind == ClientLog.KReload) EventLog.ClientReload(r, ApplyLogged);
                    else ClientLog.ClientControl(kind, r, d => _host?.Send(d));
                    break;
                }
                case MsgType.GiveAck:
                    Together.ReceiveGiveAck(r);
                    break;
                case MsgType.GiftState:
                    Together.ClientGiftState(r);
                    break;
                case MsgType.Welcome:
                {
                    LocalId = r.ReadByte();
                    int count = r.ReadByte();
                    var seen = new HashSet<int>();
                    string hostName = "";
                    for (int i = 0; i < count; i++)
                    {
                        int id = r.ReadByte();
                        string name = r.ReadString();
                        if (id == 0) hostName = name;
                        if (id == LocalId) continue;
                        seen.Add(id);
                        if (!Players.TryGetValue(id, out var pl)) Players[id] = pl = new Player { Id = id };
                        pl.Name = name;
                    }
                    foreach (var gone in Players.Keys.Where(k => !seen.Contains(k)).ToList()) RemovePlayer(gone);
                    bool hostRestarted = r.BaseStream.Length - r.BaseStream.Position >= 8 && EventLog.ClientWelcome(r.ReadInt32(), r.ReadUInt32());
                    uint hostHas = r.BaseStream.Length - r.BaseStream.Position >= 4 ? r.ReadUInt32() : 0;
                    string hostMod = r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : "";
                    string hostWorld = r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : "";
                    // До запроса мира: хост этого мира может не знать моего прогресса (был у прошлого хоста).
                    if (hostWorld.Length > 0 && _host != null) PlayerProgress.OfferOwn(hostWorld, d => _host?.Send(d));
                    if (hostMod.Length > 0 && hostMod != CoopByEMPEROR.Version.Number)
                    {
                        Log($"У хоста мод {hostMod}, у тебя {CoopByEMPEROR.Version.Number}. Исправления новой версии не работают, поставьте одинаковую.");
                        GameApi.HudMessage($"У хоста мод {hostMod}, у тебя {CoopByEMPEROR.Version.Number}");
                    }
                    ClientLog.Resend(hostHas, d => _host?.Send(d));
                    bool reask = _reaskAfterWelcome && !Reconnecting;
                    _reaskAfterWelcome = false;
                    OnReconnected(hostRestarted);
                    if (reask)
                    {
                        // Хост сбросил соединение, а с ним и передачу мира и наш SceneJoin: просим заново.
                        SceneSnapshot.AskAgain();
                        if (WorldTransfer.Receiving && _host != null)
                        {
                            MelonLogger.Msg("[сеть] хост сбросил соединение посреди передачи мира, прошу мир заново");
                            WorldTransfer.Request(_host);
                        }
                    }
                    if (!_welcomed)
                    {
                        _welcomed = true;
                        if (ViaSteam) QuickStart.RememberHost(_hostSteamId, hostName);
                        Status = UI.L.T("Подключено к ", "Connected to ") + hostName + ".";
                        Log("Ты подключился к " + hostName + ".");
                        _host?.Send(ContentCheck.Build());
                    }
                    break;
                }
                case MsgType.Reject:
                {
                    string why = r.ReadString();
                    if (why.StartsWith("Хост выгнал")) HostGone(why);
                    else Stop(UI.L.T("Хост отказал: ", "The host refused: ") + why);
                    break;
                }
                case MsgType.Joined:
                {
                    int id = r.ReadByte();
                    string name = r.ReadString();
                    if (id == LocalId) return;
                    Players[id] = new Player { Id = id, Name = name };
                    Log(name + " подключился.");
                    GameApi.HudMessage(name + " подключился");
                    break;
                }
                case MsgType.Left:
                    RemovePlayer(r.ReadByte(), announce: true);
                    break;
                case MsgType.PlayerState:
                {
                    int id = r.ReadByte();
                    if (id == LocalId) return;
                    if (!Players.TryGetValue(id, out var pl)) return;
                    ReadState(r, pl);
                    break;
                }
                case MsgType.World:
                    ApplyWorld(r);
                    break;
                case MsgType.ItemDrop:
                case MsgType.ItemPick:
                case MsgType.ItemUpdate:
                    ItemSync.Receive(type, raw);
                    break;
                case MsgType.ObjectState:
                    WorldObjects.Receive(raw);
                    break;
                case MsgType.SpawnGrant:
                    RadialSync.ClientGrant(r);
                    break;
                case MsgType.WeatherState:
                    WeatherSync.Receive(r);
                    break;
                case MsgType.FilterTags:
                    FilterTags.Receive(r);
                    break;
                case MsgType.CheckRelay:
                    SyncCheck.ClientRelay(r);
                    break;
                case MsgType.CheckResend:
                    SyncCheck.ClientResend(r);
                    break;
                case MsgType.AuroraScreens:
                    AuroraPower.ClientReceiveScreens(r);
                    break;
                case MsgType.PotAmounts:
                    PotAmounts.Receive(r);
                    break;
                case MsgType.ArrowFlight:
                    ArrowFlight.Receive(r);
                    break;
                case MsgType.FlareShot:
                    Game.FlareShots.Receive(r);
                    break;
                case MsgType.QuestGate:
                    QuestWait.ClientGate(r);
                    break;
                case MsgType.AnimalState:
                case MsgType.AnimalRemove:
                case MsgType.AnimalAuthority:
                case MsgType.AnimalHarvest:
                case MsgType.AnimalSound:
                case MsgType.AnimalSense:
                case MsgType.AnimalNoise:
                case MsgType.AnimalArrow:
                    AnimalSync.ReceiveBroadcast(type, r);
                    break;
                case MsgType.PlayerVoice:
                    VoiceSync.Receive(r);
                    break;
                case MsgType.ContainerAsk:
                    ContainerLoot.ClientSelf(r);
                    break;
                case MsgType.ContainerClaim:
                    ContainerLoot.ClientDenied(r);
                    break;
                case MsgType.Chronicle:
                    Chronicle.ClientReceive(r);
                    break;
                case MsgType.Marker:
                    Together.ReceiveMarker(r);
                    break;
                case MsgType.GiveItem:
                    Together.ReceiveGive(r);
                    break;
                case MsgType.Trade:
                    Trade.Receive(r);
                    break;
                case MsgType.Summary:
                    Summary.Receive(r, "");
                    break;
                case MsgType.Quest:
                    QuestItems.Receive(r, null, "");
                    break;
                case MsgType.PickDenied:
                    ItemSync.ReceiveDenied(r);
                    break;
                case MsgType.Revive:
                    Together.ReceiveRevive(r);
                    break;
                case MsgType.DownedInfo:
                    Rescue.ReceiveInfo(r);
                    break;
                case MsgType.TrainSync:
                    TrainSync.Receive(r);
                    break;
                case MsgType.Hit:
                    FriendlyFire.Receive(r);
                    break;
                case MsgType.IslandSync:
                    IslandSync.Receive(r);
                    break;
                case MsgType.WorldEvent:
                    WorldEvents.Receive(r, raw, null);
                    break;
                case MsgType.Feature:
                    Features.Receive(r);
                    break;
                case MsgType.Fx:
                    Features.ClientFx(r);
                    break;
                case MsgType.PlayerSound:
                    SoundSync.Receive(r);
                    break;
                case MsgType.ReviveHold:
                    Rescue.ReceiveHold(r);
                    break;
                case MsgType.Drag:
                    Drag.Receive(r);
                    break;
                case MsgType.TraderState:
                case MsgType.AuroraState:
                    GlobalSync.Receive(type, r);
                    break;
                case MsgType.Recipes:
                    SharedRecipes.ClientReceive(r);
                    break;
                case MsgType.Smokehouse:
                    SmokehouseLink.Receive(r);
                    break;
                case MsgType.SmokehouseAll:
                    SmokehouseLink.ClientAll(r);
                    break;
                case MsgType.SmokehouseAsk:
                    SmokehouseLink.ClientAnswer(r);
                    break;
                case MsgType.AnimalHit:
                case MsgType.AnimalTransfer:
                    AnimalSync.ReceiveAddressed(type, r, "другой игрок");
                    break;
                case MsgType.SleepWait:
                    SleepSync.ReceiveWait(r);
                    break;
                case MsgType.SleepStart:
                    SleepSync.ReceiveStart(r);
                    break;
                case MsgType.SleepStop:
                    SleepSync.ReceiveStop(r);
                    break;
                case MsgType.SceneState:
                case MsgType.SceneFirst:
                case MsgType.SceneSaved:
                    SceneSnapshot.ClientReceive(type, r);
                    break;
                case MsgType.Summon:
                    if (InHostWorld)
                    {
                        Log("Хост позвал тебя к себе.");
                        GameApi.HudMessage("Хост зовёт тебя к себе");
                        GoToPlayer(0);
                        RenderCheck.LogSoon("после зова хоста");
                    }
                    break;
                case MsgType.Content:
                    ContentCheck.Receive(r, Players.TryGetValue(0, out var hp0) ? hp0.Name : "хост", false);
                    break;
                case MsgType.WorldInfo:
                    WorldTransfer.ClientInfo(r);
                    break;
                case MsgType.SaveRequest:
                    PlayerProgress.OnSaveRequest();
                    break;
                case MsgType.WorldChunk:
                    WorldTransfer.ClientChunk(r, Players.TryGetValue(0, out var h) ? h.Name : "хоста");
                    break;
                case MsgType.Rules:
                    _clientRules = Rules.Read(r);
                    break;
                case MsgType.Start:
                    _startMode = r.ReadInt32();
                    _startRegion = r.ReadString();
                    JoinInfo = UI.L.T("Хост загружается. Как только он окажется в мире, ты загрузишься к нему.", "The host is loading. As soon as the host is in the world, you will load in too.");
                    break;
                case MsgType.Chat:
                {
                    int id = r.ReadByte();
                    string chat = r.ReadString();
                    ShowChat(id, chat);
                    Bot.OnChat(id, chat);
                    break;
                }
                case MsgType.Bye:
                    HostGone("Хост завершил игру.");
                    break;
                case MsgType.HelloAgain:
                {
                    // Хост нас сбросил и не знает: здороваемся заново. В его мире — как после обрыва (копим своё, потом досылаем).
                    if (!_welcomed) break;   // Hello и так уходит раз в секунду
                    MelonLogger.Msg("[сеть] хост сбросил соединение и меня не знает: здороваюсь заново");
                    float hnow = Time.unscaledTime;
                    if (!StartReconnect(hnow)) { _welcomed = false; _nextHello = 0f; _joinStarted = hnow; _reaskAfterWelcome = true; }
                    break;
                }
            }
        }

        // Событие друга с номером (Net/ClientLog.cs), уже по порядку номеров.
        private static void ApplyFromClient(Peer peer, byte[] inner)
        {
            if (Mode != Mode.Host) return;
            using var cr = Msg.Read(inner, out var ctype);
            if (ctype != MsgType.CSeq && ctype != MsgType.Seq) HandleOnHost(peer, ctype, cr, inner);
        }

        // Событие мира из журнала (Net/EventLog.cs), уже по порядку номеров.
        private static void ApplyLogged(byte[] inner)
        {
            if (Mode != Mode.Client) return;
            using var ir = Msg.Read(inner, out var innerType);
            if (innerType != MsgType.Seq) HandleOnClient(innerType, ir, inner);
        }

        private static void OnClosed(Peer peer)
        {
            // По IP обрыв обычно закрывает сокет: сначала пробуем переподключиться, как при тихом обрыве.
            if (Mode == Mode.Client && peer == _host) { if (!StartReconnect(Time.unscaledTime)) HostGone("Соединение с хостом закрыто."); }
            else if (Mode == Mode.Host && Peers.Contains(peer)) DropPeer(peer, "отключился");
        }

        // Хост выгоняет игрока: тот выходит из мира хоста в главное меню.
        public static void Kick(int id)
        {
            if (Mode != Mode.Host) return;
            var peer = Peers.Find(p => p.Greeted && p.Id == id);
            if (peer == null) return;
            var rej = Msg.New(MsgType.Reject);
            rej.Write("Хост выгнал тебя из игры.");
            peer.Send(Msg.Done(rej));
            Kicked.Add(PlayerProgress.KeyOf(peer, Players.TryGetValue(id, out var kp) ? kp.Name : ""));
            peer.CloseAfterSend();   // Reject должен дойти, иначе друг не узнает, что выгнан
            DropPeer(peer, "выгнан хостом");
        }

        private static void DropPeer(Peer peer, string why)
        {
            Peers.Remove(peer);
            PlayerProgress.OnPeerLeft();
            if (!peer.Closed) peer.Close();
            if (!peer.Greeted) return;
            if (Players.TryGetValue(peer.Id, out var pl))
            {
                Log(pl.Name + " " + why + ".");
                GameApi.HudMessage(pl.Name + " " + why);
                Chronicle.Add(pl.Name + " " + why);
            }
            RemovePlayer(peer.Id);
            var left = Msg.New(MsgType.Left);
            left.Write((byte)peer.Id);
            Broadcast(Msg.Done(left));
        }

        private static void RemovePlayer(int id, bool announce = false)
        {
            if (!Players.TryGetValue(id, out var pl)) return;
            pl.Puppet.Destroy();
            Players.Remove(id);
            ContentCheck.Forget(id);
            if (announce)
            {
                Log(pl.Name + " вышел.");
                GameApi.HudMessage(pl.Name + " вышел");
            }
        }

        // Событие мира от этого игрока: клиент шлёт хосту, хост — всем.
        public static void SendGameEvent(byte[] data)
        {
            try { SyncCheck.NoteSent(data); } catch { }
            if (Mode == Mode.Host) { Broadcast(data); return; }
            if (Mode != Mode.Client) return;
            // События мира — с номером: после обрыва хост скажет, до какого дошло, остальное дошлём (Net/ClientLog.cs).
            if (data.Length > 0 && (EventLog.Logged(data[0]) || data[0] == (byte)MsgType.SceneSave))
            {
                if (data[0] != (byte)MsgType.SceneSave) PlayerProgress.InventoryChanged();
                var wrapped = ClientLog.Wrap(data);
                if (_welcomed) _host?.Send(wrapped);
                return;
            }
            if (_welcomed) _host?.Send(data);
            else if (Reconnecting)
            {
                // Связь пропала, мы переподключаемся: копим свои события и отправим, когда хост ответит.
                if (Outbox.Count < 3000) Outbox.Enqueue(data);
            }
        }

        private static readonly Queue<byte[]> Outbox = new();
        public static bool OutboxEmpty => Outbox.Count == 0;
        private static float _nextSeqTick, _nextNetLog;

        // Хост: сообщение одному игроку по номеру.
        public static void SendTo(int id, byte[] data)
        {
            if (Mode != Mode.Host) return;
            var peer = Peers.Find(p => p.Greeted && p.Id == id);
            peer?.Send(data);
        }

        // Друг: сообщение только хосту (не событие мира).
        public static void SendToHost(byte[] data)
        {
            if (Mode == Mode.Client && _welcomed) _host?.Send(data);
        }

        // Служебное друг → хост, даже до приветствия (номера событий).
        public static void SendControl(byte[] data)
        {
            if (Mode == Mode.Client) _host?.Send(data);
        }

        // Частые данные, которые не страшно потерять (животные): без гарантии доставки.
        public static void SendFast(byte[] data)
        {
            if (Mode == Mode.Host) Broadcast(data, reliable: false);
            else if (Mode == Mode.Client && _welcomed) _host?.Send(data, false);
        }

        // Всем без номера и без гарантии доставки (следы пуль).
        public static void BroadcastFast(byte[] data) => Broadcast(data, null, false);

        private static void Broadcast(byte[] data, Peer except = null, bool reliable = true)
        {
            // События мира — с номером: друг заметит пропуск и попросит дослать (Net/EventLog.cs).
            if (reliable && Mode == Mode.Host && data.Length > 0 && EventLog.Logged(data[0]))
            {
                EventLog.HostSend(data, except, Peers, p => PlayerProgress.KeyOf(p, Players.TryGetValue(p.Id, out var pl) ? pl.Name : ""));
                return;
            }
            foreach (var p in Peers)
                if (p != except && p.Greeted) p.Send(data, reliable);
        }

        // ---------- Позиции игроков ----------

        private static void SendOwnState()
        {
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out float yaw, out bool crouched)) return;
            if (Activity.InIce) { var ip = Activity.IcePos; x = ip.x; y = ip.y; z = ip.z; }   // для всех он ещё в полынье
            string held = HeldItems.Current(out byte holdFlags);
            Vitals.Own(out byte hp, out byte vit);
            string carry = CarryVisual.Own(new Vector3(x, y, z), out bool carryPose, out var carryRel, out var carryRot);
            if (carry.Length > 0) { held = ""; holdFlags = 0; }   // в руках у игры пустышка-ручка, её не показываем
            var data = WriteState((byte)LocalId, GameApi.Scene(), x, y, z, yaw, (byte)((crouched ? 1 : 0) | (InSharedWorld ? 2 : 0)), Activity.Current(), (Config.Character ?? "") + (Config.NickColor.Length > 0 ? "#" + Config.NickColor : ""), held, holdFlags, HeldItems.Shots, Busy.Current(),
                                  carry, carryPose, carryRel, carryRot, Together.Gesture, hp, vit, OwnPitch(), ItemSync.SceneKey());
            if (Mode == Mode.Host) Broadcast(data, reliable: false);
            else _host?.Send(data, false);
        }

        // Куда смотрит камера по вертикали (+ вниз), в целых градусах.
        private static sbyte OwnPitch()
        {
            try
            {
                var cam = Il2Cpp.GameManager.GetMainCamera();
                if (cam == null) return 0;
                float p = cam.transform.eulerAngles.x;
                if (p > 180f) p -= 360f;
                return (sbyte)Mathf.RoundToInt(Mathf.Clamp(p, -89f, 89f));
            }
            catch { return 0; }
        }

        // flags: 1 присел, 2 в общем мире.
        private static byte[] WriteState(int id, string scene, float x, float y, float z, float yaw, byte flags, byte action, string character,
                                         string held, byte holdFlags, byte shots, string busy,
                                         string carry, bool carryPose, Vector3 carryRel, Quaternion carryRot, byte gesture, byte hp, byte vitals, sbyte pitch, string sceneKey)
        {
            var w = Msg.New(MsgType.PlayerState);
            w.Write((byte)id);
            w.Write(scene ?? "");
            w.Write(x);
            w.Write(y);
            w.Write(z);
            w.Write(yaw);
            w.Write(flags);
            w.Write(action);
            w.Write(character);
            w.Write(held ?? "");
            w.Write(holdFlags);
            w.Write(shots);
            w.Write(busy ?? "");
            w.Write(carry ?? "");
            if (!string.IsNullOrEmpty(carry))
            {
                w.Write((byte)(carryPose ? 1 : 0));
                if (carryPose)
                {
                    w.Write(carryRel.x); w.Write(carryRel.y); w.Write(carryRel.z);
                    w.Write(carryRot.x); w.Write(carryRot.y); w.Write(carryRot.z); w.Write(carryRot.w);
                }
            }
            w.Write(gesture);
            w.Write(hp);
            w.Write(vitals);
            w.Write(pitch);
            w.Write(sceneKey ?? "");
            return Msg.Done(w);
        }

        private static void ReadState(BinaryReader r, Player pl)
        {
            pl.Scene = r.ReadString();
            pl.X = r.ReadSingle();
            pl.Y = r.ReadSingle();
            pl.Z = r.ReadSingle();
            pl.Yaw = r.ReadSingle();
            byte stateFlags = r.ReadByte();
            pl.Crouched = (stateFlags & 1) != 0;
            pl.InWorld = (stateFlags & 2) != 0;
            byte wasAction = pl.Action;
            pl.Action = r.BaseStream.Position < r.BaseStream.Length ? r.ReadByte() : (byte)0;
            if (pl.Action == Activity.FellThroughIce && wasAction != Activity.FellThroughIce && pl.Id != LocalId) SoundSync.OnFriendFellThroughIce(pl);
            pl.Character = r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : "";
            // Цвет ника едет в той же строке, что персонаж: «astrid#ff6b6b».
            int hash = pl.Character.IndexOf('#');
            pl.Color = hash >= 0 ? Config.CleanColor(pl.Character.Substring(hash + 1)) : "";
            if (hash >= 0) pl.Character = pl.Character.Substring(0, hash);
            pl.Held = r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : "";
            pl.HoldFlags = r.BaseStream.Position < r.BaseStream.Length ? r.ReadByte() : (byte)0;
            pl.Shots = r.BaseStream.Position < r.BaseStream.Length ? r.ReadByte() : (byte)0;
            pl.Busy = r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : "";
            pl.Carry = r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : "";
            pl.CarryPose = pl.Carry.Length > 0 && r.ReadByte() == 1;
            if (pl.CarryPose)
            {
                pl.CarryRel = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                pl.CarryRot = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            }
            pl.Gesture = r.BaseStream.Position < r.BaseStream.Length ? r.ReadByte() : (byte)0;
            pl.Hp = r.BaseStream.Position < r.BaseStream.Length ? r.ReadByte() : (byte)255;
            pl.Vitals = r.BaseStream.Position < r.BaseStream.Length ? r.ReadByte() : (byte)0;
            pl.Pitch = r.BaseStream.Position < r.BaseStream.Length ? r.ReadSByte() : (sbyte)0;
            pl.SceneKey = r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : "";
            pl.LastState = Time.unscaledTime;
            pl.AddPoint(pl.LastState);
        }

        private static void UpdatePuppets(bool inGame, float now)
        {
            string scene = inGame ? GameApi.Scene() : "";
            if (scene != _lastScene)
            {
                // Объекты старой сцены Unity уже уничтожил, просто забываем их.
                foreach (var pl in Players.Values) pl.Puppet.Destroy();
                _lastScene = scene;
            }
            float dt = Time.unscaledDeltaTime;
            foreach (var pl in Players.Values)
            {
                bool show = inGame && With(pl, scene) && now - pl.LastState < 5f;
                if (!show)
                {
                    if (pl.Puppet.Alive) FriendTracks.Forget(pl.Id);
                    pl.Puppet.Destroy();
                    continue;
                }
                try
                {
                    pl.Puppet.Ensure(pl.Id, pl.Character);
                    pl.Puppet.Gesture = pl.Gesture;
                    pl.Sample(now - Player.Delay, out var sp, out float syaw);
                    bool seated = Cars.Seat(pl.Busy, out var eye, out float seatYaw);
                    pl.Puppet.SetSeat(seated, eye, seatYaw);
                    pl.Puppet.SetBed(BedPose.Parse(pl.Busy, out var bedMid, out float bedYaw), bedMid, bedYaw);
                    pl.Puppet.Pitch = pl.Pitch;
                    pl.Puppet.Update(sp.x, sp.y, sp.z, syaw, pl.Crouched, dt, pl.Action, pl.Carry.Length > 0 ? "" : pl.Held, pl.HoldFlags, pl.Shots);
                    pl.Puppet.UpdateCarry(pl.Carry, pl.CarryPose, pl.CarryRel, pl.CarryRot, dt);
                    if (!Cars.InCar(pl)) FriendTracks.Step(pl, sp, syaw);
                }
                catch (Exception e)
                {
                    MelonLogger.Warning("[фигура] " + e.Message);
                }
            }
        }

        public static void TeleportTo(int id)
        {
            if (!TeleportAllowed || !Players.TryGetValue(id, out var pl)) return;
            if (!With(pl, GameApi.Scene())) return;
            string why = OnRopeOrInCar(null) ?? OnRopeOrInCar(pl);
            if (why != null) { GameApi.HudMessage(why + " — телепорт позже"); return; }
            GameApi.Teleport(pl.X, pl.Y, pl.Z);
        }

        // Кто мешает телепорту: p на тросе (23) или в машине; p == null — я сам. null — можно.
        private static string OnRopeOrInCar(Player p)
        {
            byte act = p != null ? p.Action : Activity.Current();
            bool rope = act == 23 || (p != null && p.Busy != null && p.Busy.StartsWith("rope:", StringComparison.Ordinal));
            bool car = act == Activity.InCar || (p != null && Cars.InCar(p));
            if (!rope && !car) return null;
            string who = p != null ? p.Name : "ты";
            return who + (rope ? " на тросе" : " в машине");
        }

        // ---------- Время и погода (хост → клиенты) ----------

        // Флаги: 1 общее время, 2 общая погода, 4 телепорт разрешён, 8 хост в игре (есть время и погода),
        // 16 ники всем включены, 32 ники всем выключены.
        private static void SendWorld()
        {
            try
            {
                bool inGame = GameApi.InGame();
                var w = Msg.New(MsgType.World);
                w.Write((byte)((Config.SyncTime ? 1 : 0) | (Config.SyncWeather ? 2 : 0) | (Config.AllowTeleport ? 4 : 0) | (inGame ? 8 : 0)
                    | (Config.NamesForAll == 1 ? 16 : 0) | (Config.NamesForAll == 2 ? 32 : 0) | (InMainMenu() ? 64 : 0)));
                w.Write(inGame ? GameApi.ModeType() : -1);
                w.Write(inGame ? NewGame.CurrentRegionKey() : "");
                w.Write(inGame ? ActionTime.SharedTimeOfDay() : 0f);
                w.Write(inGame ? ActionTime.SharedDay() : 0);
                w.Write(inGame ? GameApi.WeatherStage() : -1);
                w.Write(inGame ? GameApi.CustomModeString() : "");
                // Что хост решил за всех (ники — ещё и во флагах 16/32, как раньше).
                w.Write((byte)Config.ShownCount);
                for (int i = 0; i < Config.ShownCount; i++) w.Write((byte)Config.ForAll((Shown)i));
                Broadcast(Msg.Done(w));
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[мир] " + e.Message);
            }
        }

        private static string _customApplied = "";
        private static bool _modeWarned;

        private static void ApplyWorld(BinaryReader r)
        {
            byte flags = r.ReadByte();
            int modeType = r.ReadInt32();
            string region = r.ReadString();
            float tod = r.ReadSingle();
            int day = r.ReadInt32();
            int stage = r.ReadInt32();
            string custom = r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : "";
            _hostAllowsTeleport = (flags & 4) != 0;
            int names = (flags & 16) != 0 ? 1 : (flags & 32) != 0 ? 2 : 0;
            int[] got = new int[Config.ShownCount];
            got[(int)Shown.Names] = names;
            if (r.BaseStream.Position < r.BaseStream.Length)
            {
                int n = r.ReadByte();
                for (int i = 0; i < n; i++) { int v = r.ReadByte(); if (i < got.Length) got[i] = Math.Min(v, 2); }
            }
            for (int i = 0; i < got.Length; i++)
                if (_hostForAll[i] != got[i])
                {
                    MelonLogger.Msg($"[мир] хост: {(Shown)i} — {(got[i] == 0 ? "каждый сам" : got[i] == 1 ? "всем включено" : "всем выключено")}");
                    _hostForAll[i] = got[i];
                }
            // Хост в главном меню (два раза подряд, чтобы не спутать с загрузкой): выходим и мы.
            if ((flags & 64) != 0) { if (++_hostMenuSeen >= 2) LeaveHostWorld("Хост вышел в главное меню.", 8f, true); }
            else _hostMenuSeen = 0;
            if ((flags & 8) == 0) return;
            HostModeType = modeType;
            if (region.Length > 0) HostRegionKey = region;
            if (!GameApi.InGame()) return;

            // Режим сложности как у хоста. Раньше друг загружал мир хоста в своём последнем режиме
            // (в «Пилигриме» волки от него сразу убегали) — теперь режим ставится при загрузке, а тут страховка.
            if (InSharedWorld && modeType >= 0 && GameApi.ModeType() != modeType)
            {
                int mine = GameApi.ModeType();
                try
                {
                    if (NewGame.MatchHostMode(modeType))
                    {
                        _customApplied = "";
                        MelonLogger.Msg($"[мир] режим сложности был {mine}, поставил как у хоста: {modeType}");
                    }
                    else if (!_modeWarned) { _modeWarned = true; MelonLogger.Warning($"[мир] режим сложности {mine}, у хоста {modeType}: не смог поставить"); }
                }
                catch (Exception e)
                {
                    if (!_modeWarned) { _modeWarned = true; MelonLogger.Warning("[мир] режим сложности хоста: " + e.Message); }
                }
            }

            // «Персонализация»: настройки едут в копии сохранения хоста, но на всякий случай сверяем их постоянно.
            if (custom.Length > 0 && InSharedWorld && custom != _customApplied)
            {
                _customApplied = custom;
                try { if (GameApi.MatchCustomMode(custom)) MelonLogger.Msg("[мир] настройки «Персонализации» поставлены как у хоста: " + custom); }
                catch (Exception e) { MelonLogger.Warning("[мир] настройки «Персонализации»: " + e.Message); }
            }

            // Пока мы спим или ждём, часы идут ускоренно у всех одинаково — не подгоняем.
            // Во время своего долгого действия тоже: часы вернутся сами, когда оно кончится (Game/ActionTime.cs).
            if ((flags & 1) != 0 && InSharedWorld && !SleepSync.HoldClock && !ActionTime.Active)
            {
                // Сравниваем полное время (день + часы): иначе день мог уйти на сутки после сна отдельно
                // или долгого действия через полночь, а часы при этом совпадали.
                int myDay = GameApi.DayNumber();
                float mine = myDay + GameApi.TimeOfDay();
                float diff;
                if (day > 0) diff = Math.Abs(mine - (day + tod));
                else { diff = Math.Abs(GameApi.TimeOfDay() - tod); diff = Math.Min(diff, 1f - diff); }
                // 0.0014 суток ≈ 2 минуты игрового времени (было 15, 0.19.23): часы у всех почти минута в минуту.
                // Время в сообщении отстаёт на задержку сети — это доли секунды, секунды игрового времени.
                if (diff > 0.0014f)
                {
                    GameApi.SetDayAndTime(day, tod);
                    MelonLogger.Msg($"[мир] время подогнано под хоста: день {myDay} {mine - myDay:0.000} → день {day} {tod:0.000}" + (day > 0 && day != myDay ? $" (теперь день {GameApi.DayNumber()})" : ""));
                }
            }
            // Погоду ведёт WeatherSync (сообщение WeatherState): здесь только флаг.
            HostWeatherSync = (flags & 2) != 0;
        }

        // ---------- Чат ----------

        public static void SendChat(string text)
        {
            text = Clean(text, 200);
            if (text.Length == 0 || Mode == Mode.Off) return;
            ShowChat(LocalId, text);
            var data = WriteChat(LocalId, text);
            if (Mode == Mode.Host) Broadcast(data);
            else _host?.Send(data);
        }

        private static byte[] WriteChat(int id, string text)
        {
            var w = Msg.New(MsgType.Chat);
            w.Write((byte)id);
            w.Write(text);
            return Msg.Done(w);
        }

        private static void ShowChat(int id, string text)
        {
            string name = id == LocalId ? LocalName : Players.TryGetValue(id, out var pl) ? pl.Name : (id == 0 ? "Хост" : "?");
            Log(name + ": " + text, false);
            Together.OnChat(id, text);
            if (id != LocalId) GameApi.HudMessage(name + ": " + text);
        }

        public static void Log(string line) => Log(line, true);

        private static void Log(string line, bool system)
        {
            ChatLog.Add(DateTime.Now.ToString("HH:mm") + "  " + line);
            ChatSystem.Add(system);
            while (ChatLog.Count > 50) { ChatLog.RemoveAt(0); ChatSystem.RemoveAt(0); }
            MelonLogger.Msg("[кооп] " + line);
        }

        internal static string Clean(string s, int max = 32)
        {
            s = (s ?? "").Replace('\n', ' ').Replace('\r', ' ').Trim();
            return s.Length > max ? s.Substring(0, max) : s;
        }
    }
}
