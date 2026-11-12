using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace CoopByEMPEROR.Game
{
    // Животные, версия 4 (0.14): одна жизнь зверей на локацию, один мозг.
    //
    // Как устроено в самой игре (код игры 2.58 разобран через Cpp2IL, см. память проекта):
    //  - места появления создают и убирают зверей, глядя только на камеру своей игры (Utils.DistanceToMainCamera,
    //    PositionIsOnscreen, PositionIsInLOSOfPlayer); SpawnRegion.UpdateFromManager игра встраивает в
    //    SpawnRegionManager.Update, поэтому патч на него никогда не срабатывал;
    //  - зверь ищет цель среди других зверей, людей из «Сказаний» и одного игрока — своего.
    //
    // Поэтому теперь:
    //  1. У локации один хозяин животных (первый пришедший, AnimalAuthority). ВСЕ звери локации живут и думают
    //     в его игре. У остальных игр места появления молчат, а звери — копии, созданные их же игрой
    //     (настоящий BaseAi с навигацией, ИИ выключен), которые идут и играют анимацию по хозяину.
    //     Дублей быть не может: зверей создаёт только одна игра.
    //  2. Для мест появления и для решений зверя «камера» — это ближайший из игроков (AnimalSync.Spawn.cs),
    //     поэтому звери появляются и вокруг друга за километр и не пропадают у него на глазах.
    //  3. Во время раздумий зверя (BaseAi.Update) игрок хозяина стоит там, где ближайший к зверю игрок, а игра
    //     получает его состояние: присел, факел, фальшфейер, копьё, запах, укрытие (AnimalSync.Sense.cs).
    //  4. Бросок на чужого игрока: зверя на время одалживаем жертве (бьёт и начинает схватку игра того, на кого
    //     он бросился). Успокоился — он возвращается хозяину локации. Это единственный переезд мозга.
    //  5. В разных локациях каждый сам себе хозяин — как в одиночной.
    public static partial class AnimalSync
    {
        // Животных дальше этого от игрока ему не рассылаем и копий не держим.
        private const float Range = 450f;
        // Часто (10 раз в секунду) рассылаем зверей ближе этого к кому-то из остальных, остальных — 2 раза.
        private const float CloseRange = 150f;

        private class Animal
        {
            public long Gid;
            public string Prefab = "";
            public GameObject Go;
            public BaseAi Ai;
            public Animator Anim;
            public HashSet<int> Params;
            public bool Real;          // мозг у нас
            public int Controller = -1;
            public float Seen;         // последнее состояние от ведущего (или наше обновление)
            public Vector3 Pos;
            public Quaternion Rot;
            public float Hp;
            public bool Dead;
            public bool DeadHandled;
            public bool Shadow;        // у хозяина: копия зверя, погибшего у одолжившего; убита для учёта, сама не рассылается
            public string Harvest;     // последнее известное состояние туши (BodyHarvest.Serialize)
            public float HarvestAt = -100f;   // когда туша последний раз пришла от другого игрока
            public readonly List<Behaviour> Off = new();   // что мы выключили, чтобы сделать копию
            public bool RootMotion;
            public int Tick;
            public readonly float[] F = new float[Floats.Length];
            public readonly int[] I = new int[Ints.Length];
            public readonly bool[] B = new bool[Bools.Length];
            public int State;          // состояние аниматора у ведущего (слой 0)
            public float StateTime;    // и где в нём (0..1)
            public float NextFade;
            public int FadeState, Fades;
            public Vector3 LocalPos;   // место модели на родителе (с навигацией), пока зверь был настоящим
            public Quaternion LocalRot;
            public bool HasLocal;
            public HashSet<int> NoState;   // состояния, которых нет в нашем аниматоре
            public float LastJumpLog, LastKeepLog;
            public bool Scaled;        // запас здоровья уже умножен по правилу хоста
            public string RegionKey;   // место появления; null — зверь не из места появления
            public int RegionHash;
            public bool Built;         // копия собрана из модели (места появления не нашлось), игра её не создавала
            public float RealSince = -100f;
            public float NextGround, GroundY = float.NaN;
            public Transform[] Bones;      // кости модели: по самой нижней ставим копию в схватке на землю
            public float NextLow, LowRel;  // где самая нижняя кость относительно корня
            public float DeadAt = -1f;     // когда узнали о смерти (туша оседает на землю несколько секунд)
            public float NextSettle, SettleDrop;   // копия туши: на сколько опустить ниже присланного места
            public string HarvestSig;      // состояние туши без того, что меняется само (порча, время)
            public AiMode PendingMode = AiMode.None;   // режим, который включим чуть позже (новому зверю нужен кадр на Start)
            public float PendingAt;
            public readonly List<(Rigidbody rb, bool kinematic, RigidbodyInterpolation interp)> Bodies = new();
            // Номер владения: растёт при каждом переезде мозга. Состояния и передачи со старым номером — запоздавшие.
            public int Epoch;
            public int Focus = -1;          // на кого из игроков зверь сейчас «смотрит» (AnimalSync.Sense.cs)
            // Зверь, который ещё никого не заметил, думает про игроков рядом по очереди (AnimalSync.Sense.cs, Think).
            public int ThinkWho = -1;
            public float ThinkUntil;
            public bool Rotating;
            public bool CullAlways;         // анимация не выключается вне нашей камеры (зверь думает про другого)
            public float WhyLogAt = -100f;
            public float ViewLogAt = -100f;   // туша: когда писали, что игра прятала её модель (AnimalSync.CarcassView.cs)
            public Vector3 DeadPos;           // туша: где лежала при прошлой проверке
            public bool HasDeadPos;
            public float CalmSince = -1f;   // с какого времени одолженный зверь спокоен (вернуть хозяину локации)
            public AiMode CtrlMode;         // режим зверя у ведущего (из его состояния)
            public bool Posed;              // копия сейчас в позе схватки
            public bool WasPosed;           // была в позе на прошлом кадре (AnimalSync.StruggleEnd.cs)
            public float PoseSince, FarLogAt = -100f;
            public int PoseLooks;
            public SkinnedMeshRenderer[] Skins;
            public MatchTransform[] Matches;   // капсулы на костях (по ним игра ищет, с чем взаимодействовать)
            public float ColSyncUntil = -1f, ColSeenAt = -1f;   // туша умерла у нас «по присланному»: ставим капсулы на кости сами
            public readonly Snap[] Snaps = new Snap[8];
            public int SnapCount, SnapHead;
            public float SnapGap = 0.1f;
            public int LastHitBy = -1;      // кто последним попал (для хроники: кто добыл)
            public bool KillNoted;
            public float RegionAt = -200f;  // для какого RealSince записано RegionOnAtReal (AnimalSync.Region.cs)
            public bool RegionOnAtReal;     // было ли место появления включено у нас, когда зверь стал настоящим
            // Адрес BaseAi, под которым зверь записан в ByAi. У уничтоженного игрой зверя a.Ai == null, а запись
            // по адресу оставалась и могла скрыть нового зверя с тем же адресом.
            public IntPtr Ptr;
            public bool JustDied;           // смерть замечена (NoteDeaths), ещё не разослана
            // Цель после передачи мозга: -1 — не указана (Restart решает сам), 0 — игрок, иначе общий номер зверя.
            public long WantTarget = -1;
            public float SentMeat = float.NaN;   // мясо туши в последнем разосланном или принятом состоянии
            public float MeatSentAt = -100f;     // когда мы последний раз разослали изменение туши
        }

        private struct Snap { public float T; public Vector3 Pos; public Quaternion Rot; }

        private static readonly Dictionary<long, Animal> Animals = new();
        private static readonly Dictionary<IntPtr, long> ByAi = new();
        private static readonly Dictionary<string, GameObject> Prefabs = new();
        private static readonly Dictionary<string, AssetReferenceBaseAi> Refs = new();   // модель → ссылка из места появления
        private static readonly Dictionary<string, SpawnRegion> RefRegion = new();       // модель → место появления, где она есть
        private static readonly HashSet<string> MissingPrefabs = new();
        private static readonly Dictionary<IntPtr, HashSet<int>> ParamCache = new();
        private static string _scene = "";
        private static int _counter = UnityEngine.Random.Range(1, 1 << 20);

        private static int _authority = -1;
        private static float _nextHarvest;
        private static float _authorityHeard, _enteredAt, _nextAnnounce, _nextSend, _nextControl, _nextLog, _nextRegions, _nextScan;
        private static int _stray, _leasesOut, _leasesIn, _returns, _blockedSpawns, _replicas, _builtReplicas;
        private static int _byQuiet, _staleStates, _staleTransfers, _yields;
        private static int _staleByAi, _staleByAiLogs, _skinsShown, _carcassesTaken, _yieldTransfers, _ringResets;
        // Хозяин животных, который только что ушёл с локации или пропал: его ещё видно в списке игроков локации
        // несколько секунд, но ни зверей его, ни хозяйства он больше не ведёт.
        private static int _goneAuthority = -1;
        private static float _goneAuthorityAt = -100f;

        // Зверь погиб, пока его мозг был у нас. Срабатывает один раз на зверя и только в той игре, где он умер
        // (у хозяина локации — тень зверя, убитого у одолжившего, и туши, принятые от других, его не вызывают).
        // shooter — кто последним попал в зверя (номер игрока), -1 — погиб не от игрока; prefab — модель
        // (WILDLIFE_Wolf, ...); gid — общий номер зверя (один на всех игроков). Для итогов вылазки и испытаний.
        public static event Action<int, string, long> Killed;
        private static int _hitsForwarded, _hitsDup, _hitsLost, _harvestResent;
        private static bool _despawning;
        internal static bool CreatingReplica;   // наша игра создаёт копию по просьбе мода — места появления не трогаем

        private static readonly string[] Floats = { "TurnAngle", "TurnSpeed", "Speed", "Wounded", "Roll", "Pitch", "TargetHeading", "TargetHeadingSmooth", "TapMeter" };
        private static readonly string[] Ints = { "AiState", "DamageSide", "DamageBodyPart", "AttackId" };
        private static readonly string[] Bools = { "Corpse", "Dead", "Stunned" };
        private static readonly int[] FloatH = Hash(Floats), IntH = Hash(Ints), BoolH = Hash(Bools);

        private static int[] Hash(string[] names)
        {
            var h = new int[names.Length];
            for (int i = 0; i < names.Length; i++) h[i] = Animator.StringToHash(names[i]);
            return h;
        }

        private static bool Active => Session.Mode != Mode.Off && Session.InSharedWorld && GameApi.InGame() && !GameApi.IsLoading();
        public static bool IsAuthority => _authority == Session.LocalId;

        // Создавать зверей на этой локации может только игра хозяина животных. Пока хозяин не выяснен (только
        // пришли), не создаёт никто: иначе пришедший успевал наплодить своих, и они двоились с хозяйскими.
        // Один на локации (и при загрузке, пока других тут нет) — создаём сами, как в одиночной: так сохранённые
        // звери появляются на своих местах. Другие тут есть — только если хозяин животных мы.
        public static bool MaySpawn
        {
            get
            {
                if (CreatingReplica || Session.Mode == Mode.Off || !Session.InSharedWorld) return true;
                string scene = GameApi.Scene();
                if (IsAuthority && scene == _scene) return true;
                float now = Time.unscaledTime;
                foreach (var pl in Session.Players.Values)
                    if (pl.Id != Session.LocalId && pl.InWorld && pl.Scene == scene && now - pl.LastState < 5f) return false;
                return true;
            }
        }

        public static bool IsPuppet(BaseAi ai) => ai != null && ByAi.TryGetValue(ai.Pointer, out var g) && Animals.TryGetValue(g, out var a) && !a.Real;

        public static string Status { get; private set; } = "";

        public static void Reset()
        {
            ClearAll(false);
            _authority = -1;
            ResetRegionSync();
            Senses.Clear();
            Status = "";
        }

        private static void ClearAll(bool despawnReal)
        {
            foreach (var a in Animals.Values)
            {
                if (a.Go == null) continue;
                if (!a.Real) DestroyReplica(a);
                else if (despawnReal) DespawnReal(a);
            }
            Animals.Clear();
            ByAi.Clear();
            InHands.Clear();
            PendingHarvest.Clear();
            MapCarcassWaiting.Clear();
            GoneHere.Clear();
            GoneLogged.Clear();
            HarvestSeen.Clear();
            _harvestResendAt = -1f;
            ClearSounds();
            ClearArrows();
        }

        public static void Tick(float now)
        {
            StartWatch();
            AfterAiUpdate();   // на всякий случай: игрок не должен остаться подменённым
            MultiView = false;
            if (!Active)
            {
                if (Animals.Count > 0)
                {
                    // Раньше молча: в логе зависания хозяин зверей «пропадал» без единой строки.
                    if (_resetLogs++ < 30) MelonLogger.Msg($"[звери] {_scene}: {WhyInactive()} — убрал зверей {Animals.Count} (копии уничтожил)");
                    ClearAll(false);
                }
                _authority = -1;
                _scene = "";
                _frameAt = 0;   // не в игре: сторож молчит
                return;
            }
            _frameAt = Environment.TickCount64;
            string scene = GameApi.Scene();
            if (scene != _scene)
            {
                if (_resetLogs++ < 30 && (_scene != "" || Animals.Count > 0))
                    MelonLogger.Msg($"[звери] локация {(_scene == "" ? "—" : _scene)} → {scene}: убрал зверей {Animals.Count}");
                // Хозяин уходит: остальные выбирают нового сразу, не ждут 6 секунд тишины.
                if (IsAuthority && _scene != "") Announce(_scene, true);
                ClearAll(false);
                _scene = scene;
                Prefabs.Clear();
                Refs.Clear();
                RefRegion.Clear();
                MissingPrefabs.Clear();
                PrefabLoads.Clear();
                PrefabKeys.Clear();
                _prefabScene = null;
                DeferredCarcasses.Clear();
                DeferredTransfers.Clear();
                StrayKilled.Clear();
                ResetRegionSync();
                SentCarcasses.Clear();
                _lateRegionsScene = "";
                RegionIndex.Clear();
                _authority = -1;
                _goneAuthority = -1;
                _enteredAt = now;
            }
            string prev = Enter("кадр зверей");
            try
            {
                Step("хозяин локации"); UpdateAuthority(now, scene);
                Step("модели"); PollPrefabs(now); RetryCarcasses(now); RetryTransfers(now);
                Step("новые звери игры"); ScanNew(now);
                Step("копии: движение"); MoveReplicas();
                Step("отложенные режимы"); ApplyPending(now);
                Step("оглушённые копии"); StunTick();
                Step("броски на игроков"); CheckLeases(now);
                Step("отправка чувств"); SendSense(now, scene);
                if (now >= _nextControl) { _nextControl = now + 1f; Step("кто ведёт"); CheckControl(now, scene); WhyNotOther(now); }
                if (now >= _nextSend) { _nextSend = now + 0.1f; Step("отправка состояний"); SendStates(scene); }
                if (now >= _nextHarvest) { _nextHarvest = now + 1f; Step("туши"); HarvestTick(); ResendHarvestForNewcomers(now); }
                Step("стрелы в зверях"); TickArrows(now, scene);
                Step("туши: оседание"); SettleCarcasses(now);
                Step("смерти"); DeathWatchTick(now);
                Step("конец схватки"); StruggleEndTick(now);
                Step("места появления"); TickRegionSync(now, scene);
                if (Session.Mode == Mode.Client && IsAuthority && now >= _nextRegions) { _nextRegions = now + 20f; Step("туши хосту"); SendCarcasses(); }
                Step("состояние"); UpdateStatus();
                if (now >= _nextLog) { _nextLog = now + 60f; Step("счётчики"); LogStats(); }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[звери] " + e.Message);
            }
            finally { Leave(prev); }
        }

        private static int _resetLogs;

        private static string WhyInactive()
        {
            try
            {
                if (Session.Mode == Mode.Off) return "мод выключен";
                if (!Session.InSharedWorld) return "не в общем мире";
                if (GameApi.IsLoading()) return "экран загрузки";
                if (!GameApi.InGame()) return "не в игре (меню или загрузка)";
            }
            catch { }
            return "неактивно";
        }

        // ---------- Сторож: где застряли звери, если игра встала ----------
        // Лог зависания 0.19.2 обрывался на «…убрал», и нельзя было понять, встала игра в коде зверей или
        // после него. Сторож — отдельный поток: если кадр не приходит 10 с, он пишет в лог, какой шаг зверей
        // идёт (или что код зверей в этот момент не работал — значит, встало в другом месте).
        private static volatile string _step;
        private static long _stepAt, _frameAt;
        private static System.Threading.Thread _watch;

        private static string Enter(string step)
        {
            var prev = _step;
            _step = step;
            System.Threading.Interlocked.Exchange(ref _stepAt, Environment.TickCount64);
            return prev;
        }

        private static void Step(string step)
        {
            _step = step;
            System.Threading.Interlocked.Exchange(ref _stepAt, Environment.TickCount64);
        }

        private static void Leave(string prev)
        {
            _step = prev;
            System.Threading.Interlocked.Exchange(ref _stepAt, Environment.TickCount64);
        }

        private static void StartWatch()
        {
            if (_watch != null) return;
            try
            {
                _watch = new System.Threading.Thread(WatchLoop) { IsBackground = true, Name = "CoopByEMPEROR animals watch" };
                _watch.Start();
            }
            catch { }
        }

        private static void WatchLoop()
        {
            bool reported = false;
            while (true)
            {
                try
                {
                    System.Threading.Thread.Sleep(2000);
                    long now = Environment.TickCount64;
                    long frame = System.Threading.Interlocked.Read(ref _frameAt);
                    if (frame == 0 || now - frame < 10000)
                    {
                        if (reported && frame != 0) MelonLogger.Msg("[звери] сторож: кадры снова идут");
                        reported = false;
                        continue;
                    }
                    if (reported) continue;
                    reported = true;
                    string step = _step;
                    long stepFor = now - System.Threading.Interlocked.Read(ref _stepAt);
                    MelonLogger.Warning(step != null
                        ? $"[звери] сторож: кадра нет уже {(now - frame) / 1000} с (игра встала, грузится или окно свёрнуто); звери в шаге «{step}» уже {stepFor / 1000} с"
                        : $"[звери] сторож: кадра нет уже {(now - frame) / 1000} с (игра встала, грузится или окно свёрнуто); код зверей в это время не работал — встало не у зверей");
                }
                catch { }
            }
        }

        // ---------- Хозяин животных локации ----------

        private static List<(int id, Vector3 pos, float yaw)> PlayersHere(string scene, float now, bool includeMe = true)
        {
            var list = new List<(int, Vector3, float)>();
            if (includeMe && GameApi.TryGetPlayer(out float x, out float y, out float z, out float yaw, out _)) list.Add((Session.LocalId, new Vector3(x, y, z), yaw));
            foreach (var pl in Session.Players.Values)
                if (pl.Id != Session.LocalId && pl.InWorld && pl.Scene == scene && now - pl.LastState < 5f) list.Add((pl.Id, new Vector3(pl.X, pl.Y, pl.Z), pl.Yaw));
            return list;
        }

        private static bool Here(int id, float now)
        {
            if (id == Session.LocalId) return true;
            return Session.Players.TryGetValue(id, out var pl) && pl.InWorld && pl.Scene == _scene && now - pl.LastState < 5f;
        }

        // Подключён, но играет в своём сохранении (Player.InWorld = false): в нашем мире его нет, хотя локация
        // может называться так же. Зверей ему не одалживаем, хозяином локации не считаем, его сообщения о зверях не берём.
        internal static bool OtherWorld(int id)
        {
            if (id == Session.LocalId || !Session.Players.TryGetValue(id, out var pl) || pl.InWorld) return false;
            _otherWorldDropped++;
            return true;
        }

        private static int _otherWorldDropped;

        private static string OtherWorldStats()
        {
            int n = 0;
            float now = Time.unscaledTime;
            foreach (var pl in Session.Players.Values)
                if (pl.Id != Session.LocalId && !pl.InWorld && pl.Scene == _scene && now - pl.LastState < 5f) n++;
            return $"в своём сохранении на локации с тем же именем {n}, их сообщений о зверях не взял {_otherWorldDropped}";
        }

        // Игрок здесь и может вести зверей: не хозяин, который только что ушёл или пропал.
        private static bool Present(int id, float now) => Here(id, now) && !Gone(id, now);

        private static bool Gone(int id, float now) => id >= 0 && id == _goneAuthority && now - _goneAuthorityAt < 15f;

        private static void UpdateAuthority(float now, string scene)
        {
            if (IsAuthority)
            {
                if (now >= _nextAnnounce) { _nextAnnounce = now + 2f; Announce(scene, false); }
                return;
            }
            bool heard = _authority >= 0 && now - _authorityHeard < 6f && Here(_authority, now);
            if (heard) return;
            if (_authority >= 0)
            {
                MelonLogger.Msg("[звери] хозяин животных локации пропал");
                _goneAuthority = _authority;
                _goneAuthorityAt = now;
                _authority = -1;
                // Выбираем нового, как после «ушёл с локации»: наименьший номер берёт сразу, остальные ждут его
                // объявления. Раньше тут оставалось время входа в локацию, и все брали хозяина одновременно.
                _enteredAt = now - 3f;
            }
            var here = PlayersHere(scene, now);
            here.RemoveAll(p => Gone(p.id, now));
            // Один на локации: сразу хозяин, как в одиночной.
            if (here.Count <= 1) { BecomeAuthority(scene, now); return; }
            // Только что пришли к другим — ждём, не напомнит ли о себе хозяин.
            if (now - _enteredAt < 3f) return;
            // Хозяина нет: им становится игрок локации с наименьшим номером.
            int min = Session.LocalId;
            foreach (var p in here) if (p.id < min) min = p.id;
            if (min != Session.LocalId && now - _enteredAt < 8f) return;   // ждём, пока объявит он
            BecomeAuthority(scene, now);
        }

        private static void BecomeAuthority(string scene, float now)
        {
            _authority = Session.LocalId;
            _authorityHeard = now;
            _nextAnnounce = 0f;
            int adopted = 0, carcasses = 0;
            // Звери прежнего хозяина (у нас копии) теперь живут у нас. Кроме одолженных тем, кто сейчас здесь:
            // те вернутся к нам сами, когда успокоятся.
            foreach (var a in new List<Animal>(Animals.Values))
            {
                if (a.Go == null) { Forget(a); continue; }
                if (a.Real) continue;
                if (a.Controller >= 0 && a.Controller != Session.LocalId && Present(a.Controller, now) && now - a.Seen < 2f) continue;
                if (a.Dead)
                {
                    // Туша прежнего хозяина: у нас только копия, а её никто больше не рассылает — и пришедший потом
                    // (сам прежний хозяин из домика) её бы уже не увидел. Делаем её настоящей тушей у нас.
                    if (MakeCarcass(a, true, "прежний хозяин ушёл")) { a.Epoch++; carcasses++; }
                    continue;
                }
                a.Epoch++;
                MakeReal(a, a.Go.transform.position, a.Go.transform.rotation, a.Hp, a.CtrlMode);
                a.Controller = Session.LocalId;
                adopted++;
            }
            MelonLogger.Msg($"[звери] я хозяин животных локации {scene}" + (adopted > 0 ? $", веду зверей прежнего хозяина: {adopted}" : "")
                + (carcasses > 0 ? $", туш взял: {carcasses}" : ""));
            Announce(scene, false);
        }

        private static void Announce(string scene, bool leaving)
        {
            var w = Msg.New(MsgType.AnimalAuthority);
            w.Write((byte)Session.LocalId);
            w.Write(scene);
            w.Write(leaving);
            Session.SendFast(Msg.Done(w));
            if (leaving) Session.SendGameEvent(Msg.Done(w));
        }

        public static void ReceiveAuthority(BinaryReader r)
        {
            int id = r.ReadByte();
            string scene = r.ReadString();
            bool leaving = r.BaseStream.Position < r.BaseStream.Length && r.ReadBoolean();
            if (!Active || scene != GameApi.Scene() || id == Session.LocalId || OtherWorld(id)) return;
            float now = Time.unscaledTime;
            if (leaving)
            {
                if (_authority != id) return;
                MelonLogger.Msg($"[звери] хозяин животных {NameOf(id)} ушёл с локации");
                _authority = -1;
                _authorityHeard = 0f;
                _goneAuthority = id;      // его ещё видно на локации несколько секунд — хозяином его не ждём
                _goneAuthorityAt = now;
                _enteredAt = now - 3f;   // выбираем нового сразу: наименьший номер берёт, остальные ждут его
                return;
            }
            if (id == _goneAuthority) _goneAuthority = -1;   // вернулся (или пропадал на миг)
            if (IsAuthority)
            {
                // Двое объявили себя хозяином одновременно: остаётся тот, у кого номер меньше.
                if (id > Session.LocalId) return;
                MelonLogger.Msg($"[звери] хозяин локации — {NameOf(id)}, уступаю; моих зверей отдаю ему");
                _authority = id;
                _authorityHeard = now;
                // Раньше своих зверей просто убирали: зверь, которого вели только мы (одолженный нам, новый),
                // пропадал у всех, а партнёр схватки исчезал посреди неё. Теперь отдаём каждого новому хозяину
                // (у кого такой уже есть — оставит свой), а партнёра схватки держим, пока она не кончится.
                foreach (var a in new List<Animal>(Animals.Values))
                {
                    if (!a.Real) continue;
                    if (a.Go == null || a.Ai == null) { Forget(a); continue; }
                    if (IsLocalPartner(a)) { a.Controller = Session.LocalId; continue; }   // теперь одолжен нам
                    Transfer(a, id, "уступил хозяина локации");
                    _yieldTransfers++;
                }
                _yields++;
            }
            if (_authority != id) MelonLogger.Msg($"[звери] хозяин животных локации: {NameOf(id)}");
            _authority = id;
            _authorityHeard = now;
        }

        private static string NameOf(int id) => id == Session.LocalId ? "я" : Session.Players.TryGetValue(id, out var pl) ? pl.Name : "игрок " + id;

        // Новые звери нашей игры. У хозяина — все его (общий номер, мозг у нас). У остальных игра создавать
        // зверей не должна (места появления молчат); если какой-то скрипт всё же создал — убираем, такой же
        // придёт от хозяина копией.
        private static void ScanNew(float now)
        {
            if (now < _nextScan) return;
            _nextScan = now + 0.25f;
            var list = BaseAiManager.m_BaseAis;
            if (list != null)
            {
                List<BaseAi> kill = null, unlist = null;
                for (int i = 0; i < list.Count; i++)
                {
                    var ai = list[i];
                    if (ai == null) continue;
                    if (ByAi.TryGetValue(ai.Pointer, out var known))
                    {
                        if (Animals.TryGetValue(known, out var ka))
                        {
                            // Копия снова попала в список зверей игры (её включили): там ей не место, иначе её
                            // «увидят» звери-соседи и места появления.
                            if (!ka.Real) (unlist ??= new List<BaseAi>()).Add(ai);
                            continue;
                        }
                        // Запись от уничтоженного зверя, а игра дала новому тот же адрес: забываем её, этот — новый.
                        ByAi.Remove(ai.Pointer);
                        _staleByAi++;
                        if (_staleByAiLogs++ < 10) MelonLogger.Msg($"[звери] {ai.gameObject.name}: по его адресу числился давно убранный зверь — забыл старую запись");
                    }
                    if (!ai.gameObject.activeInHierarchy) continue;
                    if (IsAuthority) Register(ai, now);
                    else if (_authority >= 0) (kill ??= new List<BaseAi>()).Add(ai);
                }
                if (unlist != null) foreach (var ai in unlist) Unlist(ai);
                if (kill != null)
                {
                    int n = 0;
                    foreach (var ai in kill)
                        if (KillStray(ai, now) && ++n >= 3) break;   // остальных — в следующий проход (через 0,25 с)
                }
            }
            // Игра убрала зверя, которого вёл я — убираем у всех.
            // Копия пропала сама — возвращаем, пока ведущий о ней рассказывает.
            List<long> gone = null;
            foreach (var a in Animals.Values)
            {
                if (a.Go != null && a.Go.activeInHierarchy) continue;
                // Сам зверь не выключен, выключен кто-то выше (место появления, контейнер игры) — это не «игра убрала»:
                // выносим зверя из выключенного родителя.
                if (a.Go != null && (!a.Real || !IsAuthority) && Unhide(a)) continue;
                if (a.Go != null && !a.Real && !a.Dead && now - a.Seen < 2f)
                {
                    try
                    {
                        var parent = a.Go.transform.parent;
                        if (parent != null && !parent.gameObject.activeSelf) parent.gameObject.SetActive(true);
                        if (!a.Go.activeSelf) a.Go.SetActive(true);
                    }
                    catch { }
                    if (now - a.LastKeepLog > 30f) { a.LastKeepLog = now; MelonLogger.Msg($"[звери] игра хотела убрать копию {a.Prefab}, оставил"); }
                    if (a.Go.activeInHierarchy) continue;
                }
                (gone ??= new List<long>()).Add(a.Gid);
            }
            if (gone == null) return;
            var mine = new List<long>();
            foreach (var g in gone)
            {
                var a = Animals[g];
                if (a.Real) mine.Add(g);
                if (a.Real || _goneLogged++ < 20)
                    MelonLogger.Msg($"[звери] игра убрала {a.Prefab} ({(a.Real ? "вёл я" : "вёл " + NameOf(a.Controller))})" + (a.Real ? ", убираю у всех" : "")
                        + (a.Dead ? $"; туша{(a.Shadow ? " (убита у другого)" : "")}, {GoneWhy(a, now)}" : ""));
                if (a.Real) SendCarcassGone(a);
                // Копию (её объект — наш) уничтожаем, а не бросаем выключенной: брошенная без записи в учёте потом,
                // включившись, считалась «лишним зверем моей игры». Так, похоже, и появился «WILDLIFE_Stag» без
                // номера в имени (имя модели — значит, наша копия из модели) за миг до зависания 0.19.2.
                if (!a.Real && a.Go != null) { DestroyReplica(a); _orphansDestroyed++; }
                Forget(a);
            }
            if (mine.Count > 0) SendRemove(mine);
        }

        private static int _goneLogged, _orphansDestroyed;

        // Лишний зверь нашей игры у того, кто не хозяин локации: такой же придёт от хозяина копией.
        // Живого убираем, как игра (Despawn). Тушу — уничтожаем, как копию: на Despawn туши (олень, которого
        // у друга задрали, а моя игра создала из его сохранения) встал хост 0.19.2. Строки «убираю» и «убрал»
        // по обе стороны: если игра снова встанет, по логу видно, внутри или после.
        // Одного и того же 30 с не трогаем: остался — значит, его держит игра, повторять каждые 0,25 с незачем.
        private static readonly Dictionary<IntPtr, float> StrayKilled = new();
        private static int _strayDestroyed;

        private static bool KillStray(BaseAi ai, float now)
        {
            var ptr = ai.Pointer;
            if (StrayKilled.TryGetValue(ptr, out var at) && now - at < 30f) return false;
            if (StrayKilled.Count > 256) StrayKilled.Clear();
            StrayKilled[ptr] = now;
            string name = "?";
            bool dead = false, inRegion = false;
            try { name = ai.gameObject.name; } catch { }
            try { dead = ai.m_CurrentMode == AiMode.Dead || ai.m_CurrentHP <= 0f; } catch { }
            try { inRegion = ai.m_SpawnRegionParent != null; } catch { }
            bool log = _stray++ < 20;
            if (log) MelonLogger.Msg($"[звери] моя игра создала {name}{(dead ? " (туша)" : "")}{(inRegion ? "" : " без места появления")}, а звери тут у {NameOf(_authority)} — убираю");
            string prev = Enter("убираю лишнего " + name + (dead ? " (туша)" : ""));
            long t0 = Environment.TickCount64;
            try
            {
                if (dead)
                {
                    try { ai.m_SpawnRegionParent?.m_Spawns?.Remove(ai); } catch { }
                    try { SpawnWildlife_Trigger.OnDespawn(ai); } catch { }
                    Unlist(ai);
                    Silence(TopOf(ai.gameObject));
                    UnityEngine.Object.Destroy(TopOf(ai.gameObject));
                    _strayDestroyed++;
                }
                else
                {
                    _despawning = true;
                    try { ai.Despawn(); } catch { Silence(TopOf(ai.gameObject)); UnityEngine.Object.Destroy(TopOf(ai.gameObject)); }
                    finally { _despawning = false; }
                }
            }
            catch (Exception e) { MelonLogger.Warning($"[звери] убрать {name}: {e.Message}"); }
            finally { Leave(prev); }
            if (log) MelonLogger.Msg($"[звери] {name}: убрал за {Environment.TickCount64 - t0} мс{(dead ? " (уничтожил, без Despawn)" : "")}");
            return true;
        }

        private static void Register(BaseAi ai, float now)
        {
            long gid = ((long)Session.LocalId << 32) | (uint)(++_counter);
            string key = KeyOf(ai);
            var a = new Animal
            {
                Gid = gid, Go = ai.gameObject, Ai = ai, Real = true, Controller = Session.LocalId,
                Prefab = SpawnRegion.GetPrefabNameFromInstanceName(ai.gameObject.name) ?? "",
                Anim = ai.m_Animator, Seen = now, RegionKey = key, RegionHash = Hash(key), RealSince = now,
            };
            a.Params = ParamsOf(a.Anim);
            ScaleHp(a, true);
            Animals[gid] = a;
            MapAi(a, ai);
            ScanArrows(a);   // стрелы из сохранения
        }

        private static void MapAi(Animal a, BaseAi ai)
        {
            a.Ptr = ai.Pointer;
            ByAi[a.Ptr] = a.Gid;
        }

        // По сохранённому адресу, а не через a.Ai: у уничтоженного игрой зверя a.Ai == null, и запись оставалась.
        private static void UnmapAi(Animal a)
        {
            if (a.Ptr != IntPtr.Zero && ByAi.TryGetValue(a.Ptr, out var g) && g == a.Gid) ByAi.Remove(a.Ptr);
            a.Ptr = IntPtr.Zero;
        }

        private static void Forget(Animal a)
        {
            Animals.Remove(a.Gid);
            UnmapAi(a);
            StopSent(a.Gid, true);
            StopHeard(a.Gid);
        }

        private static void SendRemove(List<long> gids)
        {
            var w = Msg.New(MsgType.AnimalRemove);
            w.Write(GameApi.Scene());
            w.Write((short)gids.Count);
            foreach (var g in gids) w.Write(g);
            Session.SendGameEvent(Msg.Done(w));
        }

        public static void ReceiveRemove(BinaryReader r)
        {
            string scene = r.ReadString();
            int n = r.ReadInt16();
            for (int i = 0; i < n; i++)
            {
                long g = r.ReadInt64();
                if (scene != _scene || !Animals.TryGetValue(g, out var a)) continue;
                if (a.Real && a.Controller == Session.LocalId && !IsAuthority) continue;   // одолжен нам — пока наш
                if (IsLocalPartner(a)) continue;   // в схватке с нами — уберём после неё
                MelonLogger.Msg($"[звери] ведущий убрал {a.Prefab}" + (a.Real ? " (вёл я)" : ""));
                Forget(a);
                MarkGone(g);
                if (a.Go == null) continue;
                if (a.Real) DespawnReal(a);
                else DestroyReplica(a);
            }
        }

        // ---------- Раз в секунду ----------

        private static void CheckControl(float now, string scene)
        {
            LearnPoses();
            StruggleGrace();
            CheckReturns(now);
            // Копии: модель могла остаться выключенной (вне камеры, когда зверь стал копией) — включаем.
            // Только живые: тушу игра может прятать сама (разделана на четверти и т. п.).
            foreach (var a in Animals.Values) if (!a.Real && !a.Dead && a.Go != null) ShowSkins(a, false);
            // Ведущий зверя пропал (ушёл, отключился, одолжил и замолчал): зверя забирает хозяин локации.
            if (!IsAuthority) return;
            foreach (var a in new List<Animal>(Animals.Values))
            {
                if (a.Go == null) continue;
                if (a.Dead)
                {
                    // Туша, которую ведёт другой: тень (зверь погиб у одолжившего) или копия. Её ведущий ушёл —
                    // теперь её ведём и рассылаем мы, иначе туша пропадала у того, кто вернётся на локацию.
                    if (a.Real && !a.Shadow) continue;
                    if (Present(a.Controller, now) && now - a.Seen < 15f) continue;
                    if (a.Shadow)
                    {
                        a.Shadow = false;
                        a.Controller = Session.LocalId;
                        a.KillNoted = true;
                        a.Epoch++;
                        _carcassesTaken++;
                        if (_carcassLogs++ < 20) MelonLogger.Msg($"[звери] туша {a.Prefab}: убивший ушёл — теперь её веду и рассылаю я");
                    }
                    else if (MakeCarcass(a, true, "её ведущий ушёл")) a.Epoch++;
                    continue;
                }
                if (a.Real || a.Shadow || now - a.Seen <= 4f) continue;
                // Ведущий в схватке с этим зверем: молчание — не повод забирать, иначе схватка у него оборвётся.
                if (InStruggle(a.Controller) && Present(a.Controller, now) && now - a.Seen < 60f) continue;
                // Ведущий здесь (его игрок шлёт себя), но о звере молчит: бывает, его игра на миг замирает
                // (затемнение и пропуск времени после схватки). Ждём дольше, чем ушедшего.
                if (Present(a.Controller, now) && now - a.Seen < 15f) continue;
                MelonLogger.Msg($"[звери] {NameOf(a.Controller)} молчит о {a.Prefab}, веду сам");
                a.Epoch++;
                MakeReal(a, a.Go.transform.position, a.Go.transform.rotation, a.Hp, a.CtrlMode);
                a.Controller = Session.LocalId;
                _byQuiet++;
            }
        }

        // ---------- Переезд мозга (только одалживание жертве и возврат) ----------

        // Отдаём мозг зверя игроку to. Если to — хозяин локации, это возврат.
        private static void Transfer(Animal a, int to, string why)
        {
            if (IsLocalPartner(a))
            {
                if (_partnerKept++ < 20) MelonLogger.Msg($"[звери] {a.Prefab} в схватке со мной — не отдаю {NameOf(to)} ({why})");
                return;
            }
            var t = a.Go.transform;
            a.Epoch++;
            bool dead = a.Dead || a.Ai.m_CurrentMode == AiMode.Dead || a.Ai.m_CurrentHP <= 0f;
            var w = Msg.New(MsgType.AnimalTransfer);
            w.Write((byte)to);
            w.Write(_scene);
            w.Write(a.Gid);
            w.Write(a.Prefab);
            w.Write(a.RegionHash);
            w.Write(t.position.x); w.Write(t.position.y); w.Write(t.position.z);
            w.Write(t.rotation.x); w.Write(t.rotation.y); w.Write(t.rotation.z); w.Write(t.rotation.w);
            w.Write(a.Ai.m_CurrentHP);
            w.Write((int)a.Ai.m_CurrentMode);
            w.Write(a.Epoch);
            // Хвост (0.19): кто последним попал (для добычи), на кого зверь нападал (игрок или другой зверь) и,
            // для туши, её состояние — новый ведущий создаст тушу у себя уже разделанной.
            w.Write((sbyte)Mathf.Clamp(a.LastHitBy, -1, 127));
            long target = TargetGid(a.Ai);
            w.Write(target >= 0);
            if (target >= 0) w.Write(target);
            w.Write(dead ? (a.Harvest ?? "") : "");
            Session.SendGameEvent(Msg.Done(w));
            a.Hp = a.Ai.m_CurrentHP;
            a.CtrlMode = a.Ai.m_CurrentMode;
            a.SnapCount = 0;
            a.Focus = -1;
            a.CalmSince = -1f;
            a.Pos = t.position;
            a.Rot = t.rotation;
            a.Seen = Time.unscaledTime;
            if (dead) a.Dead = a.DeadHandled = a.KillNoted = true;
            a.Shadow = false;
            ToReplica(a);
            a.Controller = to;
            if (to == _authority) _returns++; else _leasesOut++;
            MelonLogger.Msg($"[звери] {a.Prefab} → {NameOf(to)} ({why}, режим {a.CtrlMode})");
        }

        // На кого зверь сейчас нападает или кого выслеживает: 0 — игрок (или цели нет), номер — другой зверь
        // (в 2.59 волк охотится на оленя), -1 — кто-то, кого другие не знают (люди «Сказаний»).
        private static long TargetGid(BaseAi ai)
        {
            try
            {
                var t = ai.m_CurrentTarget;
                if (t == null || IsPlayerTarget(t)) return 0;
                var other = t.m_BaseAi;
                if (other != null && ByAi.TryGetValue(other.Pointer, out var g) && Animals.ContainsKey(g)) return g;
                return -1;
            }
            catch { return 0; }
        }

        private static bool IsPlayerTarget(AiTarget t)
        {
            try { return t.IsPlayer(); } catch { return IsLocalPlayerTarget(t); }
        }

        // Передача пришла, пока модели локации ещё грузятся (без ожидания): копию создать не из чего, а зверь
        // уже ушёл от отдающего. Держим сообщение и разбираем его ещё раз, когда модели готовы (до 30 с).
        private static readonly List<(byte[] data, string who, float at)> DeferredTransfers = new();
        private static int _transfersDeferred;

        private static void RetryTransfers(float now)
        {
            if (DeferredTransfers.Count == 0 || PrefabsLoading) return;
            var list = new List<(byte[] data, string who, float at)>(DeferredTransfers);
            DeferredTransfers.Clear();
            foreach (var t in list)
            {
                if (now - t.at > 30f) continue;
                try { using var ms = new MemoryStream(t.data); ReceiveTransfer(new BinaryReader(ms), t.who, false); }
                catch (Exception e) { MelonLogger.Warning("[звери] отложенная передача: " + e.Message); }
            }
        }

        private static void ReceiveTransfer(BinaryReader r, string who) => ReceiveTransfer(r, who, true);

        private static void ReceiveTransfer(BinaryReader r, string who, bool mayDefer)
        {
            long start = -1;
            try { if (r.BaseStream.CanSeek) start = r.BaseStream.Position; } catch { }
            int to = r.ReadByte();
            string scene = r.ReadString();
            long gid = r.ReadInt64();
            string prefab = r.ReadString();
            int regionHash = r.ReadInt32();
            var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var rot = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            float hp = r.ReadSingle();
            var mode = (AiMode)r.ReadInt32();
            int epoch = r.ReadInt32();
            int hitBy = -1;
            long target = -1;
            string harvest = "";
            if (r.BaseStream.Position < r.BaseStream.Length)
            {
                hitBy = r.ReadSByte();
                if (r.ReadBoolean()) target = r.ReadInt64();
                harvest = r.ReadString();
            }
            if (to != Session.LocalId || scene != _scene) return;
            bool dead = mode == AiMode.Dead || hp <= 0f;
            if (dead && (!Animals.TryGetValue(gid, out var ga) || ga.Go == null) && IsGone(gid, Time.unscaledTime)) { SkipGone(prefab, $"{who} отдал мне тушу"); return; }
            if (Animals.TryGetValue(gid, out var a) && a.Go != null && epoch <= a.Epoch)
            {
                MelonLogger.Msg($"[звери] {who} отдал мне {a.Prefab}, но передача устарела (номер {epoch}, у меня {a.Epoch})");
                _staleTransfers++;
                return;
            }
            if (a != null && hitBy >= 0) a.LastHitBy = hitBy;
            if (a != null && a.Go != null && a.Real)
            {
                // Зверь и так у нас (например, партнёр нашей схватки): берём номер, но не перезапускаем.
                a.Epoch = epoch;
                a.Controller = Session.LocalId;
                // Тень у хозяина (зверь погиб у отдающего): отдающий её больше не ведёт — это наша обычная туша.
                if (a.Shadow) { a.Shadow = false; a.KillNoted = true; }
                MelonLogger.Msg($"[звери] {who} отдал мне {a.Prefab}, а он и так у меня — оставил как есть");
                return;
            }
            if (a == null || a.Go == null)
            {
                a = CreateReplica(gid, prefab, regionHash, pos, rot);
                if (a == null)
                {
                    if (mayDefer && PrefabsLoading && start >= 0 && DeferredTransfers.Count < 64)
                    {
                        try
                        {
                            r.BaseStream.Position = start;
                            DeferredTransfers.Add((r.ReadBytes((int)(r.BaseStream.Length - start)), who, Time.unscaledTime));
                            _transfersDeferred++;
                            MelonLogger.Msg($"[звери] {who} отдал мне {prefab}, а модели локации ещё грузятся — приму, когда загрузятся");
                        }
                        catch { }
                    }
                    return;
                }
                if (hitBy >= 0) a.LastHitBy = hitBy;
            }
            a.Epoch = epoch;
            if (a.RegionHash != regionHash) SetRegion(a, regionHash);
            if (dead)
            {
                // Туша: у отдающего зверь уже мёртв. Раньше её оживляли (MakeReal) — убитый волк вставал.
                a.Pos = pos;
                a.Rot = rot;
                if (!string.IsNullOrEmpty(harvest)) a.Harvest = harvest;
                a.Dead = true;
                if (a.DeadAt < 0f) a.DeadAt = Time.unscaledTime;
                MakeCarcass(a, false, $"{who} отдал мёртвого");
                _leasesIn++;
                return;
            }
            // Мы в схватке с другим зверем: этот не бросается, а ждёт рядом, как в одиночной.
            if (LocalBusy() && (mode == AiMode.Attack || mode == AiMode.PassingAttack || mode == AiMode.Struggle)) mode = AiMode.HoldGround;
            a.WantTarget = target;
            MakeReal(a, pos, rot, hp, mode);
            Unhide(a);
            a.Controller = Session.LocalId;
            a.Focus = -1;
            a.CalmSince = -1f;
            _leasesIn++;
            MelonLogger.Msg($"[звери] {who} отдал мне {a.Prefab} ({(IsAuthority ? "вернулся" : "бросок на меня")}, режим {mode}; {BodyState(a)})");
        }

        // Копию туши делаем настоящей тушей нашей игры: её ведущий ушёл или отдал её нам, а копию никто не
        // рассылает. Как ApplyCarcass: оживляем и сразу убиваем. unlink — место появления у нас уже учло эту смерть
        // (его состояние пришло от прежнего хозяина), второй раз не считаем.
        private static bool MakeCarcass(Animal a, bool unlink, string why)
        {
            if (a.Go == null || a.Ai == null) return false;
            float now = Time.unscaledTime;
            if (!a.Real)
            {
                var t = a.Go.transform;
                Vector3 pos = t.position;
                Quaternion rot = t.rotation;
                if (a.Built)
                {
                    // Модель без навигации: тушу создаёт место появления, модель убираем.
                    var made = InstantiateFromRegion(a.Prefab, a.RegionKey, pos, rot);
                    if (made == null)
                    {
                        if (_carcassLogs++ < 20) MelonLogger.Msg($"[звери] туша {a.Prefab} ({why}): место появления не создало — оставил копию");
                        return false;
                    }
                    var old = a.Go;
                    var oldAi = a.Ai;
                    UnmapAi(a);
                    Unlist(oldAi);
                    StopHeard(a.Gid);
                    Silence(TopOf(old));
                    UnityEngine.Object.Destroy(TopOf(old));
                    Adopt(a, made);
                    a.Built = false;
                    a.Real = true;
                    a.RealSince = now;
                }
                else ToReal(a, pos, rot, 1f, AiMode.None);
            }
            if (unlink) { try { a.Ai.m_SpawnRegionParent?.m_Spawns?.Remove(a.Ai); a.Ai.SetSpawnRegionParent(null); } catch { } }
            _applyingHit = true;
            try { if (a.Ai.m_CurrentMode != AiMode.Dead) a.Ai.ApplyDamage(100000f, 0f, DamageSource.Player, ""); }
            catch (Exception e) { MelonLogger.Warning($"[звери] туша {a.Prefab}: {e.Message}"); }
            finally { _applyingHit = false; }
            a.Shadow = false;
            a.Controller = Session.LocalId;
            a.Dead = a.DeadHandled = a.KillNoted = true;
            a.JustDied = false;
            if (a.DeadAt < 0f) a.DeadAt = now;
            StartColliderSync(a);
            if (!string.IsNullOrEmpty(a.Harvest)) PendingHarvest[a.Gid] = a.Harvest;   // HarvestTick применит к нашей туше
            _carcassesTaken++;
            if (_carcassLogs++ < 20) MelonLogger.Msg($"[звери] туша {a.Prefab} теперь моя ({why})");
            return true;
        }

        private static int _carcassLogs, _preyLogs;

        // ---------- Копия и настоящий зверь ----------

        private static bool KeepOn(Behaviour b)
        {
            if (b.TryCast<Animator>() != null) return true;
            if (b.TryCast<BodyHarvest>() != null) return true;
            if (b.TryCast<ObjectGuid>() != null) return true;
            string n = b.GetIl2CppType().Name;
            return n.StartsWith("Ak");   // звук (Wwise)
        }

        private static IEnumerable<Behaviour> BrainParts(GameObject go)
        {
            foreach (var b in go.GetComponents<Behaviour>()) if (b != null && !KeepOn(b)) yield return b;
            // У настоящего зверя навигация (NavMeshAgent, MoveAgent) висит на родителе.
            var parent = go.transform.parent;
            if (parent != null && (parent.GetComponent<UnityEngine.AI.NavMeshAgent>() != null || parent.GetComponent<MoveAgent>() != null))
                foreach (var b in parent.GetComponents<Behaviour>()) if (b != null && !KeepOn(b)) yield return b;
        }

        private static void ToReplica(Animal a)
        {
            if (!a.Real || a.Go == null) return;
            a.Real = false;
            a.LocalPos = a.Go.transform.localPosition;
            a.LocalRot = a.Go.transform.localRotation;
            a.HasLocal = true;
            a.Off.Clear();
            foreach (var b in BrainParts(a.Go))
                if (b.enabled) { b.enabled = false; a.Off.Add(b); }
            if (a.Anim != null)
            {
                a.RootMotion = a.Anim.applyRootMotion;
                a.Anim.applyRootMotion = false;
                // Копию видно издалека: анимацию не выключаем, когда её не видит наша камера.
                try { a.Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; } catch { }
            }
            Unlist(a.Ai);
            if (!a.Dead) ShowSkins(a, true);
            FreezeBodies(a);
            SoundsOnReplica(a);
        }

        // Игра выключает модель зверя вне камеры (BaseAiManager → BaseAi.SetIsOnScreen → EnableSkinnedMeshRenderers)
        // и включает обратно только зверям из своего списка. Копию мы из списка убираем (Unlist): если в этот миг
        // зверь был вне камеры, модель так и оставалась выключенной — «схватка с невидимым волком». Включаем сами:
        // через игру (у неё запомнено «выключено», поэтому сначала выкл, потом вкл), затем проверяем каждую часть.
        // viaGame = false — только проверка частей (раз в секунду для всех копий).
        private static int ShowSkins(Animal a, bool viaGame)
        {
            if (a.Go == null) return 0;
            int n = 0;
            try
            {
                IEnumerable<SkinnedMeshRenderer> parts;
                var rs = a.Ai != null ? a.Ai.m_Renderers : null;
                if (rs != null && rs.Length > 0) parts = rs;
                else parts = a.Skins ??= a.Go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var r in parts) if (r != null && !r.enabled) n++;
                if (viaGame && a.Ai != null)
                {
                    try { a.Ai.EnableSkinnedMeshRenderers(false); a.Ai.EnableSkinnedMeshRenderers(true); } catch { }
                }
                if (n > 0) foreach (var r in parts) if (r != null && !r.enabled) r.enabled = true;
            }
            catch { a.Skins = null; }
            if (n > 0)
            {
                _skinsShown += n;
                if (_skinLogs++ < 20) MelonLogger.Msg($"[звери] у копии {a.Prefab} (ведёт {NameOf(a.Controller)}) модель была выключена (частей {n}) — включил");
            }
            return n;
        }

        private static int _skinLogs;

        // Копию делаем настоящим зверем (нам его одолжили, вернули или мы стали хозяином локации).
        private static void MakeReal(Animal a, Vector3 pos, Quaternion rot, float hp, AiMode mode)
        {
            if (a.Built && RealFromGame(a, pos, rot, hp, mode)) return;
            ToReal(a, pos, rot, hp, mode);
        }

        private static void ToReal(Animal a, Vector3 pos, Quaternion rot, float hp, AiMode mode)
        {
            if (a.Go == null) return;
            EnsureAgent(a);
            var t = a.Go.transform;
            var parent = t.parent;
            bool onParent = parent != null && parent.GetComponent<UnityEngine.AI.NavMeshAgent>() != null;
            if (onParent)
            {
                // Копию мы двигали за всего зверя вместе с родителем, но модель могла уехать от родителя:
                // возвращаем ей прежнее место на родителе и ставим родителя туда, где зверь сейчас.
                if (a.HasLocal) { t.localPosition = a.LocalPos; t.localRotation = a.LocalRot; }
                parent.rotation = rot * Quaternion.Inverse(t.localRotation);
                parent.position = pos - (t.position - parent.position);
            }
            else
            {
                t.position = pos;
                t.rotation = rot;
            }
            if (!a.Real)
            {
                StopHeard(a.Gid);
                a.Real = true;
                foreach (var b in a.Off) if (b != null) b.enabled = true;
                a.Off.Clear();
                if (a.Anim != null) a.Anim.applyRootMotion = a.RootMotion;
                try { if (a.Ai != null) a.Ai.ResetDefaultCullmode(); } catch { }
                try { if (a.Ai != null && BaseAiManager.m_BaseAis != null && !BaseAiManager.m_BaseAis.Contains(a.Ai)) BaseAiManager.Add(a.Ai); } catch { }
                ThawBodies(a);
            }
            a.RealSince = Time.unscaledTime;
            try
            {
                var agent = a.Go.GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.enabled) agent.Warp(agent.transform.position);
            }
            catch { }
            SnapToNavMesh(a);
            if (a.Ai != null)
            {
                if (hp > 0f) a.Ai.m_CurrentHP = hp;
                Restart(a, mode);
            }
        }

        // Пока зверь был копией, его ИИ стоял, а параметры аниматора писали мы. Ожив, ИИ продолжал бы старый
        // режим со старым путём, а аниматор играл чужое. Поэтому сбрасываем аниматор и заново входим в режим.
        private static void Restart(Animal a, AiMode mode)
        {
            if (a.Ai.m_CurrentMode == AiMode.Dead || mode == AiMode.Dead || a.Dead) return;
            if (mode == AiMode.None || mode == AiMode.Struggle || mode == AiMode.ScriptedSequence) mode = AiMode.Wander;
            try
            {
                if (a.Anim != null)
                    foreach (var prm in a.Anim.parameters)
                        switch (prm.type)
                        {
                            case AnimatorControllerParameterType.Float: a.Anim.SetFloat(prm.nameHash, prm.defaultFloat); break;
                            case AnimatorControllerParameterType.Int: a.Anim.SetInteger(prm.nameHash, prm.defaultInt); break;
                            case AnimatorControllerParameterType.Bool: a.Anim.SetBool(prm.nameHash, prm.defaultBool); break;
                            case AnimatorControllerParameterType.Trigger: a.Anim.ResetTrigger(prm.nameHash); break;
                        }
            }
            catch { }
            a.State = 0;
            // Нападение и выслеживание без цели падали внутри игры (NullReference в SetAiMode): цель — наш игрок,
            // зверя нам одалживают потому, что он бросился на нас. Но в 2.59 зверь может гнать другого зверя (волк —
            // оленя): такую цель не трогаем. Отдающий говорит, на кого зверь нападал (WantTarget); не сказал —
            // оставляем цель-зверя, если она у нашей копии есть и жива, иначе — наш игрок.
            if (mode == AiMode.Attack || mode == AiMode.Stalking || mode == AiMode.PassingAttack || mode == AiMode.HoldGround)
            {
                try
                {
                    AiTarget target = null;
                    var cur = a.Ai.m_CurrentTarget;
                    if (a.WantTarget > 0)
                    {
                        if (Animals.TryGetValue(a.WantTarget, out var prey) && prey.Ai != null && !prey.Dead) target = prey.Ai.m_AiTarget;
                        if (target == null) mode = AiMode.Wander;   // того зверя у нас нет: на игрока не перекидываем
                    }
                    else if (a.WantTarget < 0 && cur != null && !IsPlayerTarget(cur) && !cur.IsDead()) target = cur;
                    if (target == null && mode != AiMode.Wander)
                    {
                        target = a.Ai.m_PlayerTarget;
                        if (target == null) target = GameManager.GetPlayerObject()?.GetComponent<AiTarget>();
                    }
                    if (target != null) a.Ai.m_CurrentTarget = target;
                    else mode = AiMode.Wander;
                    if (target != null && !IsPlayerTarget(target) && _preyLogs++ < 20) MelonLogger.Msg($"[звери] {a.Prefab}: продолжает гнать {target.gameObject.name}, на игрока не перекидываю");
                }
                catch { mode = AiMode.Wander; }
            }
            // Зверю, у которого ещё не было Start (копия ни разу не думала), SetAiMode может упасть:
            // тогда пока бродит, а нужный режим включаем через 0,3 с.
            try
            {
                a.Ai.m_CurrentMode = AiMode.None;   // иначе тот же режим игра не перезапустит
                _ownMode = true;
                try { a.Ai.SetAiMode(mode); }
                finally { _ownMode = false; }
                a.WantTarget = -1;
            }
            catch (Exception e)
            {
                try { a.Ai.SetAiMode(AiMode.Wander); } catch { }
                if (mode != AiMode.Wander) { a.PendingMode = mode; a.PendingAt = Time.unscaledTime + 0.3f; }
                MelonLogger.Warning($"[звери] {a.Prefab}: режим {mode}: {e.Message}");
            }
        }

        // Копия, собранная из модели (место появления не нашлось): игра такого зверя не создавала, ему не хватает
        // навигации, места бродить, стаи. Оживляя, просим место появления создать настоящего там же.
        private static bool RealFromGame(Animal a, Vector3 pos, Quaternion rot, float hp, AiMode mode)
        {
            if (a.Dead) return false;
            var created = InstantiateFromRegion(a.Prefab, a.RegionKey, pos, rot);
            if (created == null) return false;
            var old = a.Go;
            var oldAi = a.Ai;
            UnmapAi(a);
            if (oldAi != null)
            {
                try { oldAi.m_SpawnRegionParent?.m_Spawns?.Remove(oldAi); } catch { }
                Unlist(oldAi);
            }
            StopHeard(a.Gid);
            if (old != null) { Silence(TopOf(old)); UnityEngine.Object.Destroy(TopOf(old)); }
            Adopt(a, created);
            a.Real = true;
            a.Built = false;
            a.RealSince = Time.unscaledTime;
            if (hp > 0f) created.m_CurrentHP = Mathf.Min(hp, created.m_MaxHP);
            if (mode == AiMode.None || mode == AiMode.Struggle || mode == AiMode.ScriptedSequence || mode == AiMode.Dead) mode = AiMode.Wander;
            if (mode != AiMode.Wander) { a.PendingMode = mode; a.PendingAt = Time.unscaledTime + 0.3f; }
            return true;
        }

        // Убрать зверя из учёта игры целиком: из списка зверей и из воющих волков (WolfHowlController).
        // Раньше убирали только из списка — волк оставался в воющих, после уничтожения копии игра каждый кадр
        // падала в BaseAiManager.Update (NullReferenceException в WolfHowlController.UpdateTimer).
        private static void Unlist(BaseAi ai)
        {
            if (ai == null) return;
            try { BaseAiManager.Remove(ai); }
            catch { try { BaseAiManager.m_BaseAis?.Remove(ai); } catch { } }
        }

        private static int _howlPurged, _aiAddsSkipped, _deadAiPurged, _aiListLogs;

        // Список зверей игры (BaseAiManager.m_BaseAis) пополняет только BaseAi.Start, и без проверки на повтор.
        // Копия, созданная местом появления, выключается (ToReplica) раньше, чем у неё прошёл Start. Когда она становится
        // настоящей (ToReal: смерть «по присланному», отдали мозг), мы сами вносим её в список, а в следующем кадре
        // Unity запускает отложенный Start, и он вносит её второй раз. Уничтожение (BaseAi.OnDestroy → Remove) убирает
        // одну запись, вторая остаётся на уничтоженного зверя: BaseAiManager.Update каждый кадр падал
        // (NullReferenceException в MaybeSetAiModeWhenInactiveOrDisabled) и не доходил до остальных зверей. Так было
        // в 0.19.34 с кроликом, которого застрелил GNOM: игра убрала его тушу (BodyHarvestDestroy), и 30 с до конца лога
        // хоста — ~3500 ошибок. Теперь повтор не вносим.
        internal static bool AllowAiAdd(BaseAi bai)
        {
            if (bai == null) return true;
            try
            {
                var list = BaseAiManager.m_BaseAis;
                if (list == null || !list.Contains(bai)) return true;
                _aiAddsSkipped++;
                if (_aiListLogs++ < 20)
                {
                    string name = "?";
                    try { name = bai.gameObject.name; } catch { }
                    MelonLogger.Msg($"[звери] {name} уже в списке зверей игры — второй раз не вношу (отложенный Start после копии)");
                }
                return false;
            }
            catch { return true; }
        }

        // Каждый кадр перед BaseAiManager.Update (страховка к AllowAiAdd): уничтоженных зверей из списка игры убираем,
        // иначе на первом же из них Update падает и остальные звери в этом кадре не обновляются.
        internal static void PurgeDeadAi()
        {
            if (Session.Mode == Mode.Off) return;
            try
            {
                var list = BaseAiManager.m_BaseAis;
                if (list == null) return;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var b = list[i];
                    if (!ReferenceEquals(b, null) && b != null) continue;
                    list.RemoveAt(i);
                    _deadAiPurged++;
                    if (_aiListLogs++ < 20) MelonLogger.Msg("[звери] в списке зверей игры остался уничтоженный зверь — убрал");
                }
            }
            catch { }
        }

        // Каждый кадр перед BaseAiManager.Update: уничтоженных волков из воющих выбрасываем (страховка).
        internal static void PurgeHowl()
        {
            try
            {
                var h = BaseAiManager.s_WolfHowlController;
                if (h == null) return;
                var w = h.m_Wolves;
                if (w != null) for (int i = w.Count - 1; i >= 0; i--) if (w[i] == null) { w.RemoveAt(i); _howlPurged++; }
                var r = h.m_RespondersList;
                if (r != null) for (int i = r.Count - 1; i >= 0; i--) if (r[i] == null) { r.RemoveAt(i); _howlPurged++; }
                var last = h.m_LastHowler;
                if (!ReferenceEquals(last, null) && last == null) h.m_LastHowler = null;
            }
            catch { }
        }

        private static int _unhidden, _unhideLogs;

        // Зверь из места появления висит под объектом игры (местом появления), а его игра может выключить:
        // у второго игрока место выпало «неактивным», и копия под ним невидима. Выносим зверя в корень сцены.
        private static bool Unhide(Animal a)
        {
            try
            {
                var top = TopOf(a.Go).transform;
                var up = top.parent;
                if (up == null || up.gameObject.activeInHierarchy || !top.gameObject.activeSelf || !a.Go.activeSelf) return false;
                if (_unhideLogs++ < 10)
                {
                    string path = up.name;
                    for (var t = up.parent; t != null; t = t.parent) path = t.name + "/" + path;
                    MelonLogger.Msg($"[звери] {a.Prefab} висел под выключенным объектом игры ({path}) — вынес в корень сцены");
                }
                top.SetParent(null, true);
                _unhidden++;
                return a.Go.activeInHierarchy;
            }
            catch { return false; }
        }

        private static GameObject TopOf(GameObject go)
        {
            var p = go.transform.parent;
            return p != null && p.GetComponent<UnityEngine.AI.NavMeshAgent>() != null ? p.gameObject : go;
        }

        private static void Adopt(Animal a, BaseAi ai)
        {
            ArrowsToNewModel(a, ai.gameObject);   // стрелы в звере — в ту же кость новой модели
            a.Go = ai.gameObject;
            a.Ai = ai;
            a.Anim = ai.m_Animator ?? ai.GetComponentInChildren<Animator>();
            a.Params = ParamsOf(a.Anim);
            a.Off.Clear();
            a.HasLocal = false;
            a.Scaled = false;
            a.Skins = null;   // части модели и кости — от прежнего объекта
            a.Bones = null;
            a.CullAlways = false;
            a.Rotating = false;
            MapAi(a, ai);
            ScaleHp(a, false);
        }

        private static void ApplyPending(float now)
        {
            foreach (var a in Animals.Values)
            {
                if (a.PendingMode == AiMode.None || now < a.PendingAt) continue;
                var m = a.PendingMode;
                a.PendingMode = AiMode.None;
                if (!a.Real || a.Ai == null || a.Dead) continue;
                Restart(a, m);
            }
        }

        // У копии физика не должна двигать модель (Rigidbody с интерполяцией возвращал её на старое место).
        private static void FreezeBodies(Animal a)
        {
            a.Bodies.Clear();
            try
            {
                foreach (var rb in Root(a).GetComponentsInChildren<Rigidbody>(true))
                {
                    if (rb == null) continue;
                    a.Bodies.Add((rb, rb.isKinematic, rb.interpolation));
                    rb.isKinematic = true;
                    rb.interpolation = RigidbodyInterpolation.None;
                }
            }
            catch { }
        }

        private static void ThawBodies(Animal a)
        {
            foreach (var (rb, kin, interp) in a.Bodies)
                if (rb != null) { try { rb.isKinematic = kin; rb.interpolation = interp; } catch { } }
            a.Bodies.Clear();
        }

        // Верх зверя: родитель с навигацией (у зверя, созданного игрой) или сама модель.
        private static Transform Root(Animal a)
        {
            var t = a.Go.transform;
            var p = t.parent;
            if (p != null && (p.GetComponent<UnityEngine.AI.NavMeshAgent>() != null || p.GetComponent<MoveAgent>() != null)) return p;
            return t;
        }

        private static void PlaceReplica(Animal a, Vector3 pos, Quaternion rot)
        {
            var t = a.Go.transform;
            var root = Root(a);
            if (root != t) root.position += pos - t.position;
            t.position = pos;
            t.rotation = rot;
        }

        private static void EnsureAgent(Animal a)
        {
            if (a.Ai == null || a.Real) return;
            try
            {
                if (a.Ai.m_MoveAgent != null || a.Ai.m_NavMeshAgent != null) return;
                if (a.Go.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) return;
                bool ok = a.Ai.CreateMoveAgent(null);
                if (_agentLogged++ < 10) MelonLogger.Msg($"[звери] у {a.Prefab} не было навигации, создал: {(ok ? "да" : "нет")}");
            }
            catch (Exception e) { MelonLogger.Warning($"[звери] навигация {a.Prefab}: {e.Message}"); }
        }

        private static int _agentLogged;

        private static void DespawnReal(Animal a)
        {
            if (a.Ai != null) { _despawning = true; try { a.Ai.Despawn(); return; } catch { } finally { _despawning = false; } }
            if (a.Go != null) { Silence(a.Go); UnityEngine.Object.Destroy(a.Go); }
        }

        private static void DestroyReplica(Animal a)
        {
            if (a.Go == null) return;
            try { a.Ai?.m_SpawnRegionParent?.m_Spawns?.Remove(a.Ai); } catch { }
            // Игра у своих зверей перед уничтожением глушит звук (BaseAi.Despawn), мы у копии — тоже (Silence):
            // иначе рык или петля, начатые копией, доигрывали на месте, где зверя уже нет.
            Silence(TopOf(a.Go));
            UnityEngine.Object.Destroy(TopOf(a.Go));
        }

        // Наша игра создаёт настоящего зверя у места появления (как создала бы сама), но по просьбе мода.
        private static BaseAi InstantiateFromRegion(string prefab, string regionKey, Vector3 pos, Quaternion rot)
        {
            if (!Refs.ContainsKey(prefab)) FindPrefab(prefab);
            if (!Refs.TryGetValue(prefab, out var aref) || aref == null) return null;
            var sr = FindRegion(regionKey);
            if (sr == null || !HasPrefab(sr, aref)) RefRegion.TryGetValue(prefab, out sr);
            if (sr == null) return null;
            BaseAi ai = null;
            CreatingReplica = true;
            SpawnVisibility.Off = true;
            try { ai = sr.InstantiateSpawn(aref, pos, rot, AiMode.Wander, sr.m_WildlifeMode); }
            catch (Exception e) { if (_instLogged++ < 10) MelonLogger.Warning($"[звери] {prefab}: место появления не создало зверя: {e.Message}"); }
            finally { CreatingReplica = false; SpawnVisibility.Off = false; }
            return ai;
        }

        private static int _instLogged;

        private static bool HasPrefab(SpawnRegion sr, AssetReferenceBaseAi aref)
        {
            try { return sr.HasSpawnAnimal(aref); } catch { return false; }
        }

        // Копия зверя, которого ведёт другой: настоящий зверь нашей игры (через место появления), ИИ выключен.
        // Места не нашлось — модель без навигации (Built), её оживляем через место появления при надобности.
        private static Animal CreateReplica(long gid, string prefab, int regionHash, Vector3 pos, Quaternion rot)
        {
            string prev = Enter("копия " + prefab);
            try { return CreateReplicaCore(gid, prefab, regionHash, pos, rot); }
            finally { Leave(prev); }
        }

        private static Animal CreateReplicaCore(long gid, string prefab, int regionHash, Vector3 pos, Quaternion rot)
        {
            string key = KeyOfHash(regionHash);
            var ai = InstantiateFromRegion(prefab, key, pos, rot);
            Animal a;
            if (ai != null)
            {
                a = new Animal { Gid = gid, Prefab = prefab, Real = true, Pos = pos, Rot = rot, Seen = Time.unscaledTime, RegionHash = regionHash, RegionKey = key };
                Adopt(a, ai);
                Animals[gid] = a;
                ToReplica(a);
                Unhide(a);
                _replicas++;
            }
            else
            {
                a = BuildFromModel(gid, prefab, pos, rot);
                if (a == null) return null;
                a.RegionHash = regionHash;
                a.RegionKey = key;
                _builtReplicas++;
            }
            return a;
        }

        private static Animal BuildFromModel(long gid, string prefab, Vector3 pos, Quaternion rot)
        {
            var src = FindPrefab(prefab);
            if (src == null) return null;
            var holder = new GameObject("TLDCoop_AnimalHolder");
            holder.SetActive(false);
            var go = UnityEngine.Object.Instantiate(src, pos, rot, holder.transform);
            go.name = src.name;
            var a = new Animal { Gid = gid, Prefab = prefab, Go = go, Ai = go.GetComponent<BaseAi>(), Real = false, Pos = pos, Rot = rot, Seen = Time.unscaledTime };
            foreach (var b in BrainParts(go))
                if (b.enabled) { b.enabled = false; a.Off.Add(b); }
            go.transform.SetParent(null, true);
            UnityEngine.Object.Destroy(holder);
            go.SetActive(true);
            a.Anim = go.GetComponentInChildren<Animator>();
            if (a.Anim != null)
            {
                a.RootMotion = a.Anim.applyRootMotion;
                a.Anim.applyRootMotion = false;
                try { a.Anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; } catch { }
            }
            a.Params = ParamsOf(a.Anim);
            if (a.Ai != null)
            {
                Unlist(a.Ai);
                MapAi(a, a.Ai);
            }
            foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
            ScaleHp(a, false);
            a.Built = true;
            Animals[gid] = a;
            ShowSkins(a, true);
            return a;
        }

        private static void SetRegion(Animal a, int hash)
        {
            a.RegionHash = hash;
            a.RegionKey = KeyOfHash(hash);
        }

        // Запас здоровья по правилу хоста (волк ×1,5, медведь ×2 и т. д.). Урон не трогаем.
        private static float HpScale(BaseAi ai)
        {
            var hp = Session.CurrentRules.AnimalHp;
            int i;
            switch (ai.m_AiSubType)
            {
                case AiSubType.Wolf: i = ai.Timberwolf != null ? 1 : 0; break;
                case AiSubType.Bear: i = 2; break;
                case AiSubType.Moose: i = 3; break;
                case AiSubType.Cougar: i = 4; break;
                case AiSubType.Stag: i = 5; break;
                case AiSubType.Rabbit: i = 6; break;
                default: return 1f;
            }
            return i < hp.Length ? hp[i] / 4f : 1f;
        }

        private static int _hpLogged;

        private static void ScaleHp(Animal a, bool real)
        {
            if (a.Scaled || a.Ai == null) return;
            a.Scaled = true;
            try
            {
                float k = HpScale(a.Ai);
                if (Math.Abs(k - 1f) < 0.01f) return;
                float baseMax = a.Ai.m_MaxHP;
                a.Ai.m_MaxHP = baseMax * k;
                if (real && Math.Abs(a.Ai.m_CurrentHP - baseMax) < 0.5f) a.Ai.m_CurrentHP = baseMax * k;
                if (_hpLogged++ < 10) MelonLogger.Msg($"[звери] {a.Prefab}: здоровье ×{k:0.##} ({baseMax:0} → {a.Ai.m_MaxHP:0})");
            }
            catch (Exception e) { MelonLogger.Warning("[звери] здоровье: " + e.Message); }
        }

        private static void MoveReplicas()
        {
            float dt = Time.deltaTime, now = Time.unscaledTime;
            float k = Mathf.Clamp01(dt * 20f);
            List<Animal> drop = null;
            foreach (var a in Animals.Values)
            {
                if (a.Real) continue;
                if (a.Go == null) { (drop ??= new List<Animal>()).Add(a); continue; }
                // Копия без вестей: убираем (зверь ушёл далеко от нас). Хозяин локации ждёт и забирает (CheckControl).
                if (!IsAuthority && !a.Dead && now - a.Seen > 6f) { (drop ??= new List<Animal>()).Add(a); continue; }
                var t = a.Go.transform;
                // Где копия сейчас: между присланными точками с небольшой задержкой (плавно, без догонялок).
                if (!Sample(a, now, out var p, out var q)) { p = a.Pos; q = a.Rot; }
                a.Posed = StrugglePlace(a, ref p, ref q);
                PoseWatch(a, now);
                if (!a.Dead) p = Ground(a, p);   // и в схватке: копия лежит на земле у фигуры, а не висит
                else p.y -= a.SettleDrop;
                if (a.Posed && !a.Dead) p = PoseOnGround(a, p);
                if ((t.position - p).sqrMagnitude > 25f)
                {
                    if (now - a.LastJumpLog > 10f)
                    {
                        a.LastJumpLog = now;
                        MelonLogger.Msg($"[звери] {a.Prefab} (ведёт {NameOf(a.Controller)}) перескочил на {Vector3.Distance(t.position, p):0} м");
                    }
                    PlaceReplica(a, p, q);
                }
                else PlaceReplica(a, Vector3.Lerp(t.position, p, k), Quaternion.Slerp(t.rotation, q, k));
                SetAnim(a);
            }
            if (drop == null) return;
            foreach (var a in drop)
            {
                if (a.Go != null && _dropLogged++ < 30) MelonLogger.Msg($"[звери] от {NameOf(a.Controller)} давно нет вестей о {a.Prefab}, убираю копию");
                Forget(a);
                DestroyReplica(a);
            }
        }

        private static int _dropLogged;

        // Схватка: у ведущего зверь держится у камеры, а фигуру игрока мы кладём на спину. Ставим копию на грудь
        // фигуры, мордой к лицу.
        private static bool StrugglePlace(Animal a, ref Vector3 p, ref Quaternion q)
        {
            if (a.Dead || !Session.Players.TryGetValue(a.Controller, out var pl) || pl.Action != 33) return false;
            var at = new Vector3(pl.X, pl.Y, pl.Z);
            if ((a.Pos - at).sqrMagnitude > 16f)
            {
                PoseTooFar(a, Vector3.Distance(a.Pos, at));
                return false;
            }
            var yaw = Quaternion.Euler(0f, pl.Puppet != null ? pl.Puppet.StruggleYaw(pl.Yaw) : pl.Yaw, 0f);
            p = at + yaw * new Vector3(0f, 0f, 0.8f);
            q = yaw;
            return true;
        }

        // Анимация схватки у ведущего рассчитана на камеру игрока: медведь встаёт над ней, волк висит у лица,
        // и тело модели в клипе поднято над корнем. Копию у лежащей фигуры опускаем так, чтобы её самая нижняя
        // кость (лапа) касалась земли.
        private static Vector3 PoseOnGround(Animal a, Vector3 p)
        {
            if (float.IsNaN(a.GroundY) || a.Go == null) return p;
            float now = Time.unscaledTime;
            if (now >= a.NextLow)
            {
                a.NextLow = now + 0.2f;
                try
                {
                    a.Bones ??= a.Go.GetComponentsInChildren<Transform>();
                    float root = a.Go.transform.position.y, low = float.MaxValue;
                    foreach (var b in a.Bones)
                        if (b != null && b != a.Go.transform) low = Mathf.Min(low, b.position.y);
                    if (low < float.MaxValue) a.LowRel = Mathf.Lerp(a.LowRel, Mathf.Clamp(low - root, -0.5f, 3f), 0.5f);
                }
                catch { a.Bones = null; }
            }
            p.y = a.GroundY - a.LowRel;
            return p;
        }

        // Висящую выше земли копию опускаем на землю (поднимать не трогаем: ведущий знает лучше).
        private static Vector3 Ground(Animal a, Vector3 p)
        {
            float now = Time.unscaledTime;
            if (now >= a.NextGround)
            {
                a.NextGround = now + 0.2f;
                a.GroundY = float.NaN;
                try
                {
                    var hits = Physics.RaycastAll(p + Vector3.up * 1.0f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
                    var root = Root(a);
                    float best = float.MaxValue;
                    foreach (var h in hits)
                    {
                        var c = h.collider;
                        if (c == null || c.attachedRigidbody != null || c.transform.IsChildOf(root) || c.gameObject.name.StartsWith("TLDCoop")) continue;
                        if (h.distance < best) { best = h.distance; a.GroundY = h.point.y; }
                    }
                }
                catch { }
            }
            if (!float.IsNaN(a.GroundY) && p.y > a.GroundY + 0.5f) p.y = a.GroundY;
            return p;
        }

        // ---------- Рассылка состояния ----------

        // Смерть своих зверей замечаем всегда — и когда рядом никого (раньше только при рассылке, а её не было
        // без других игроков и дальше 450 м от них): хроника, добыча (Killed), и одолженный не уходит «успокоившимся».
        private static void NoteDeaths()
        {
            foreach (var a in Animals.Values)
            {
                if (!a.Real || a.Shadow || a.Go == null || a.Ai == null) continue;
                bool dead;
                try { dead = a.Ai.m_CurrentMode == AiMode.Dead || a.Ai.m_CurrentHP <= 0f; } catch { continue; }
                a.Dead = dead;
                if (!dead || a.KillNoted) continue;
                a.KillNoted = true;
                a.JustDied = true;
                if (a.DeadAt < 0f) a.DeadAt = Time.unscaledTime;
                MelonLogger.Msg($"[звери] {a.Prefab} погиб у меня{(IsAuthority ? "" : " (одолжен мне)")}" + (a.LastHitBy >= 0 ? $", последним попал {NameOf(a.LastHitBy)}" : ""));
                WatchDeath(a, "погиб у меня");
                if (a.LastHitBy >= 0) Chronicle.Add($"{(a.LastHitBy == Session.LocalId ? Session.LocalName : NameOf(a.LastHitBy))} добыл {Chronicle.Prey(a.Prefab)}");
                try { Killed?.Invoke(a.LastHitBy, a.Prefab, a.Gid); }
                catch (Exception e) { MelonLogger.Warning("[звери] добыча: " + e.Message); }
            }
        }

        private static void SendStates(string scene)
        {
            float now = Time.unscaledTime;
            NoteDeaths();
            var others = PlayersHere(scene, now, false);
            if (others.Count == 0) return;
            // Пачками до ~1000 байт: большое сообщение без гарантии доставки Steam режет на куски, и потеря
            // любого куска теряет всех зверей пачки.
            BinaryWriter w = null;
            long countPos = 0;
            short n = 0;
            void Flush()
            {
                if (w == null || n == 0) return;
                long end = w.BaseStream.Position;
                w.BaseStream.Position = countPos;
                w.Write(n);
                w.BaseStream.Position = end;
                Session.SendFast(Msg.Done(w));
                w = null;
                n = 0;
            }
            foreach (var a in Animals.Values)
            {
                if (!a.Real || a.Shadow || a.Go == null || a.Ai == null || (!a.Go.activeInHierarchy && !IsLocalPartner(a))) continue;
                var t = a.Go.transform;
                var p = t.position;
                float best = float.MaxValue;
                foreach (var o in others) best = Mathf.Min(best, (o.pos - p).sqrMagnitude);
                bool dead = a.Dead;
                bool justDied = a.JustDied;
                // Хозяин локации шлёт только зверей у других игроков. Одолженного нам — всегда: иначе при хозяине
                // дальше 450 м он не слышал о звере (даже о его смерти), забирал его со старым местом и здоровьем,
                // и убитый у нас волк вставал.
                if (IsAuthority && best > Range * Range && !justDied) continue;
                a.Tick++;
                a.JustDied = false;
                // Туши — раз в секунду, далёкие звери — 2 раза в секунду, близкие — 10.
                // Смерть — сразу, не дожидаясь очереди туши.
                if (!justDied && (dead ? a.Tick % 10 != 0 : best > CloseRange * CloseRange && a.Tick % 5 != 0)) continue;
                if (w != null && w.BaseStream.Position > 1000) Flush();
                if (w == null)
                {
                    w = Msg.New(MsgType.AnimalState);
                    w.Write((byte)Session.LocalId);
                    w.Write(scene);
                    w.Write(now);
                    countPos = w.BaseStream.Position;
                    w.Write((short)0);
                }
                w.Write(a.Gid);
                w.Write(a.Prefab);
                w.Write(a.RegionHash);
                w.Write(p.x); w.Write(p.y); w.Write(p.z);
                var q = t.rotation;
                w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w);
                w.Write((byte)(dead ? 1 : 0));
                w.Write(a.Ai.m_CurrentHP);
                w.Write(a.Epoch);
                w.Write((byte)a.Ai.m_CurrentMode);
                WriteAnim(w, a.Anim);
                n++;
            }
            Flush();
        }

        public static void ReceiveState(BinaryReader r)
        {
            int from = r.ReadByte();
            string scene = r.ReadString();
            float sentAt = r.ReadSingle();
            int n = r.ReadInt16();
            if (!Active || scene != _scene || from == Session.LocalId || OtherWorld(from)) return;
            float now = Time.unscaledTime;
            NoteClock(from, now - sentAt);
            Vector3 me = GameManager.GetPlayerTransform() != null ? GameManager.GetPlayerTransform().position : Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                long gid = r.ReadInt64();
                string prefab = r.ReadString();
                int regionHash = r.ReadInt32();
                var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                var rot = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                bool dead = r.ReadByte() != 0;
                float hp = r.ReadSingle();
                int epoch = r.ReadInt32();
                var ctrlMode = (AiMode)r.ReadByte();
                var f = new float[FloatH.Length];
                for (int j = 0; j < f.Length; j++) f[j] = r.ReadSingle();
                var ints = new int[IntH.Length];
                for (int j = 0; j < ints.Length; j++) ints[j] = r.ReadInt16();
                byte b = r.ReadByte();
                int state = r.ReadInt32();
                float stateTime = r.ReadUInt16() / 65535f;
                if (!Animals.TryGetValue(gid, out var a) || a.Go == null)
                {
                    if ((pos - me).sqrMagnitude > Range * Range) continue;
                    if (IsGone(gid, now)) { if (GoneLogged.Add(gid)) SkipGone(prefab, $"пришло состояние от {NameOf(from)}"); else _goneSkipped++; continue; }
                    // Зверь хозяина, а хозяин — мы? Значит, пока мы им не стали, его вёл другой: тоже берём копию,
                    // CheckControl заберёт, если тот замолчит.
                    a = CreateReplica(gid, prefab, regionHash, pos, rot);
                    if (a == null) continue;
                    a.Epoch = epoch;
                }
                else if (a.RegionHash != regionHash) SetRegion(a, regionHash);
                if (a.Shadow)
                {
                    // Убивший ещё ведёт свою тушу. Первые секунды повторяем его позу смерти.
                    if (from == a.Controller) { a.Seen = now; if (dead) ShadowFollow(a, state, stateTime, now); }
                    continue;
                }
                if (epoch < a.Epoch)
                {
                    if (_staleStates++ < 20) MelonLogger.Msg($"[звери] старое состояние {a.Prefab} от {NameOf(from)} (номер {epoch}, у меня {a.Epoch}), пропустил");
                    continue;
                }
                if (a.Real)
                {
                    // Двое считают зверя своим. Номер новее — честно забрал другой, уступаем. Номер тот же —
                    // уступает тот, кто не хозяин локации (а без хозяина — у кого номер игрока больше).
                    bool yield = epoch > a.Epoch || (!IsAuthority && (from == _authority || from < Session.LocalId));
                    if (!yield) continue;
                    if (IsLocalPartner(a))
                    {
                        // Партнёра схватки не уступаем: поднимаем свой номер, чтобы уступил другой.
                        a.Epoch = epoch + 1;
                        if (_partnerKept++ < 20) MelonLogger.Msg($"[звери] {a.Prefab} в схватке со мной — не уступаю {NameOf(from)}");
                        continue;
                    }
                    MelonLogger.Msg($"[звери] {a.Prefab} ведёт {NameOf(from)} (номер {epoch}, у меня {a.Epoch}), уступаю");
                    ToReplica(a);
                    a.SnapCount = 0;
                    a.SnapGap = 0.1f;
                }
                else if (a.Controller != from && a.SnapCount > 0)
                {
                    // Зверя повёл другой игрок (бросок, возврат, новый хозяин): точки прежнего ведущего — по его часам,
                    // и новые точки отбрасывались бы как «старые» — копия стояла на месте минутами.
                    a.SnapCount = 0;
                    a.SnapGap = 0.1f;
                    _ringResets++;
                }
                a.Epoch = epoch;
                a.CtrlMode = ctrlMode;
                AddSnap(a, sentAt, pos, rot);
                if (ctrlMode == AiMode.Feeding) NotePose(a.Prefab, state, ints[0]);
                a.Controller = from;
                a.Pos = pos;
                a.Rot = rot;
                a.Hp = hp;
                a.Seen = now;
                Array.Copy(f, a.F, f.Length);
                Array.Copy(ints, a.I, ints.Length);
                for (int j = 0; j < a.B.Length; j++) a.B[j] = (b & (1 << j)) != 0;
                a.State = state;
                a.StateTime = stateTime;
                if (dead && !a.DeadHandled) OnDead(a);
            }
        }

        private static void OnDead(Animal a)
        {
            a.Dead = true;
            a.DeadHandled = true;
            a.DeadAt = Time.unscaledTime;
            MelonLogger.Msg($"[звери] животное погибло: {a.Prefab} (вёл {NameOf(a.Controller)})");
            // Копия погибает у нас по-настоящему, своей смертью игры (EnterDead): слой туши, точки «Взять»/«Разделать»,
            // всё, по чему игра ищет, с чем взаимодействовать, метка на карте — как у своей туши. Раньше так было
            // только у хозяина локации (чтобы место появления учло убитого), а у остальных копия оставалась копией,
            // которой мы сами пытались повторить смерть (CarcassInteraction), и в 0.19.12 этого не хватило: друг не
            // видел кролика, убитого хостом, и не мог разделать его волков, а после перезахода (туши стали
            // настоящими, из его сохранения) всё работало. Не хозяин отвязывает тушу от места появления: его места
            // появления ведёт хозяин, считать убитого у себя ему не нужно. Убил другой — смерть «без причины»
            // (DamageSource.Unspecified): игра не засчитывает добычу в статистику тому, кто не стрелял
            // (BaseAi.EnterDead считает убитых только при m_DamageSource == Player).
            if (a.Ai != null && !a.Built && a.Go != null)
            {
                ToReal(a, a.Pos, a.Rot, 1f, AiMode.None);
                a.Shadow = true;
                if (!IsAuthority) { try { a.Ai.m_SpawnRegionParent?.m_Spawns?.Remove(a.Ai); a.Ai.SetSpawnRegionParent(null); } catch { } }
                try { _applyingHit = true; a.Ai.ApplyDamage(100000f, 0f, DamageSource.Unspecified, ""); }
                catch (Exception e) { MelonLogger.Warning("[звери] смерть копии: " + e.Message); }
                finally { _applyingHit = false; }
                // Из стойки на месте игра переходит в смерть только через ~5 с (волк стоял, потом падал): сразу в позу
                // смерти, как у убившего (Game/AnimalSync.CarcassView.cs).
                DeathStart(a);
                _shadowDeaths++;
                if (!IsAuthority) _shadowDeathsHere++;
                StartColliderSync(a);
                WatchDeath(a, "погиб у " + NameOf(a.Controller));
                return;
            }
            try
            {
                var bh = a.Go.GetComponent<BodyHarvest>();
                if (bh != null) bh.enabled = true;
            }
            catch { }
            // Копия туши: разделка (BodyHarvestInteraction) выключена вместе с «мозгом» копии (ToReplica) —
            // друг видел точку на туше волка, убитого хостом, но «Разделать» не появлялось.
            CarcassInteraction(a, "погиб у " + NameOf(a.Controller));
        }

        private static HashSet<int> ParamsOf(Animator an)
        {
            if (an == null) return null;
            var ctrl = an.runtimeAnimatorController;
            if (ctrl == null) return null;
            if (ParamCache.TryGetValue(ctrl.Pointer, out var set)) return set;
            set = new HashSet<int>();
            foreach (var prm in an.parameters) set.Add(prm.nameHash);
            ParamCache[ctrl.Pointer] = set;
            return set;
        }

        private static void WriteAnim(BinaryWriter w, Animator an)
        {
            var ps = an != null ? ParamsOf(an) : null;
            for (int i = 0; i < FloatH.Length; i++) w.Write(ps != null && ps.Contains(FloatH[i]) ? an.GetFloat(FloatH[i]) : 0f);
            for (int i = 0; i < IntH.Length; i++) w.Write((short)(ps != null && ps.Contains(IntH[i]) ? an.GetInteger(IntH[i]) : 0));
            byte b = 0;
            for (int i = 0; i < BoolH.Length; i++) if (ps != null && ps.Contains(BoolH[i]) && an.GetBool(BoolH[i])) b |= (byte)(1 << i);
            w.Write(b);
            // Какая анимация идёт сейчас: олень сам выбирает, щипать траву или смотреть вдаль.
            int state = 0;
            float t = 0f;
            try
            {
                if (an != null && an.isActiveAndEnabled && an.runtimeAnimatorController != null)
                {
                    var st = an.IsInTransition(0) ? an.GetNextAnimatorStateInfo(0) : an.GetCurrentAnimatorStateInfo(0);
                    state = st.fullPathHash;
                    t = Mathf.Repeat(st.normalizedTime, 1f);
                }
            }
            catch { }
            w.Write(state);
            w.Write((ushort)Mathf.RoundToInt(t * 65535f));
        }

        private static void SetAnim(Animal a)
        {
            var an = a.Anim;
            if (an == null) return;
            a.Params ??= ParamsOf(an);
            if (a.Params == null) return;
            // Схватка: у ведущего зверь играет анимацию «от первого лица». Для остальных показываем, как он ест.
            int poseState = 0, poseAi = 0;
            bool pose = a.Posed && PoseOf(a.Prefab, out poseState, out poseAi);
            for (int i = 0; i < FloatH.Length; i++) if (a.Params.Contains(FloatH[i])) an.SetFloat(FloatH[i], pose && i <= 2 ? 0f : a.F[i]);
            for (int i = 0; i < IntH.Length; i++) if (a.Params.Contains(IntH[i])) an.SetInteger(IntH[i], pose && i == 0 ? poseAi : a.I[i]);
            for (int i = 0; i < BoolH.Length; i++) if (a.Params.Contains(BoolH[i])) an.SetBool(BoolH[i], a.B[i]);
            if (pose)
            {
                int keep = a.State;
                a.State = poseState;
                SyncState(a, an);
                a.State = keep;
                return;
            }
            SyncState(a, an);
        }

        // Копия играет ту же анимацию, что зверь у ведущего: если у неё другая, плавно переводим.
        private static void SyncState(Animal a, Animator an)
        {
            float now = Time.unscaledTime;
            if (a.State == 0 || now < a.NextFade || !an.isActiveAndEnabled) return;
            if (a.NoState != null && a.NoState.Contains(a.State)) return;
            try
            {
                if (an.GetCurrentAnimatorStateInfo(0).fullPathHash == a.State) return;
                if (an.IsInTransition(0) && an.GetNextAnimatorStateInfo(0).fullPathHash == a.State) return;
                if (!an.HasState(0, a.State)) { (a.NoState ??= new HashSet<int>()).Add(a.State); return; }
                if (a.State == a.FadeState && ++a.Fades >= 3) { a.Fades = 0; a.NextFade = now + 4f; return; }
                if (a.State != a.FadeState) { a.FadeState = a.State; a.Fades = 0; }
                an.CrossFade(a.State, 0.2f, 0, a.Fades == 0 ? a.StateTime : float.NegativeInfinity);
                a.NextFade = now + 0.5f;
            }
            catch { a.NextFade = now + 5f; }
        }

        // Игра не убирает: копию (её ведёт другой), одолженного зверя и зверя рядом с другим игроком.
        public static bool AllowDespawn(BaseAi ai)
        {
            if (!Active || ai == null || _despawning) return true;
            if (RegionCheckBlocks(ai, "убрать его")) return false;
            if (!ByAi.TryGetValue(ai.Pointer, out var g) || !Animals.TryGetValue(g, out var a)) return true;
            float now = Time.unscaledTime;
            if (a.Dead) return true;
            if (!a.Real)
            {
                if (now - a.Seen > 2f) return true;
                if (now - a.LastKeepLog > 30f) { a.LastKeepLog = now; MelonLogger.Msg($"[звери] игра хотела убрать копию {a.Prefab} (ведёт {NameOf(a.Controller)}), не дал"); }
                return false;
            }
            if (a.Go != null && OtherNear(a.Go.transform.position, 120f))
            {
                if (now - a.LastKeepLog > 30f) { a.LastKeepLog = now; MelonLogger.Msg($"[звери] игра хотела убрать {a.Prefab}, а рядом с ним другой игрок, не дал"); }
                return false;
            }
            return true;
        }

        // ---------- Туши ----------
        // Кто бы ни срезал мясо или шкуру со своей копии туши, состояние туши уходит остальным.

        // Мясо (m_MeatAvailableKG — ItemWeight, в json объект) тоже убираем: его едят звери, и оно убывает каждую
        // секунду. Изменения только мяса рассылаем отдельно и реже (HarvestTick).
        private static readonly System.Text.RegularExpressions.Regex Volatile = new(
            "\"m_(Condition|HoursPlayed|PercentFrozen|HoursRemainingOnCloseFire|DecimationHoursRemaining|LastHarvestTimeHours)\"\\s*:\\s*[-0-9.eE+]+"
            + "|\"m_MeatAvailableKG\"\\s*:\\s*(\\{[^}]*\\}|[-0-9.eE+]+)",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex MeatRx = new(
            "\"m_MeatAvailableKG\"\\s*:\\s*\\{?[^}0-9-]*(-?[0-9]+(\\.[0-9]+)?([eE][-+]?[0-9]+)?)",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string HarvestSig(string state) => state == null ? null : Volatile.Replace(state, "");

        private static float MeatOf(string state)
        {
            if (string.IsNullOrEmpty(state)) return float.NaN;
            var m = MeatRx.Match(state);
            return m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? (float)d : float.NaN;
        }

        private static bool SameMeat(float a, float b) => float.IsNaN(a) ? float.IsNaN(b) : !float.IsNaN(b) && Math.Abs(a - b) <= Math.Max(1e-4f, Math.Abs(a) * 1e-6f);

        private const float MeatGap = 20f;   // изменения только мяса (тушу едят звери) — не чаще
        private static float _harvestPanelAt = -100f;
        private static IntPtr _harvestPanelGo = IntPtr.Zero;   // туша, которую разделываем (её объект)
        private static int _meatHeld, _meatStale;

        // Сами разделываем тушу (окно разделки открыто сейчас или было только что): срезанное мясо — сразу всем.
        // Только у этой туши: раньше открытое окно разделки (заяц у хоста) слало без задержки мясо всех туш,
        // и олениху, которую ел волк, рассылали каждую секунду, пока окно было открыто.
        private static bool HarvestingNow(float now)
        {
            try
            {
                if (InterfaceManager.TryGetPanel<Panel_BodyHarvest>(out var p) && p != null && p.IsEnabled())
                {
                    _harvestPanelAt = now;
                    var bh = p.m_BodyHarvest;
                    _harvestPanelGo = bh != null ? bh.gameObject.Pointer : IntPtr.Zero;
                }
            }
            catch { }
            return now - _harvestPanelAt < 5f;
        }

        // Туша, убитая у другого игрока (копия или тень у хозяина), падает по его месту и анимации и может
        // повиснуть над склоном. Первые 10 секунд после смерти опускаем её, пока ближайшая к земле кость не ляжет.
        private static void SettleCarcasses(float now)
        {
            foreach (var a in Animals.Values)
            {
                if (!a.Dead || a.Go == null || a.DeadAt < 0f || now - a.DeadAt > 10f || now < a.NextSettle) continue;
                if (a.Real && !a.Shadow) continue;   // своя туша легла сама, как в одиночной
                a.NextSettle = now + 0.5f;
                float gap = CarcassGap(a);
                if (gap < 0.05f || gap > 2f) continue;
                if (a.Real) TopOf(a.Go).transform.position -= Vector3.up * gap;
                else a.SettleDrop = Mathf.Min(a.SettleDrop + gap, 2f);
                if (_settleLogs++ < 10) MelonLogger.Msg($"[звери] туша {a.Prefab} висела на {gap:0.00} м — опустил");
            }
        }

        private static int _settleLogs;

        // Что с тушей первые секунды после смерти (волк, убитый другом, у хоста несколько секунд стоял и только потом
        // падал): режим, аниматор, состояние анимации — у убившего и у хозяина, чтобы сравнить.
        private static readonly List<(Animal a, float at, int step, string why)> DeathWatch = new();
        private static readonly float[] DeathWatchAt = { 0.3f, 1f, 2f, 4f, 6f };
        private static int _deathWatches;

        private static void WatchDeath(Animal a, string why)
        {
            if (_deathWatches++ >= 8) return;
            DeathWatch.Add((a, Time.unscaledTime, 0, why));
            MelonLogger.Msg($"[звери] {a.Prefab} {why}: {DeathLook(a)}; {InteractLook(a)}");
        }

        private static void DeathWatchTick(float now)
        {
            for (int i = DeathWatch.Count - 1; i >= 0; i--)
            {
                var (a, at, step, why) = DeathWatch[i];
                if (now - at < DeathWatchAt[step]) continue;
                MelonLogger.Msg($"[звери] {a.Prefab} ({why}) через {now - at:0.0} с: {DeathLook(a)}");
                if (++step >= DeathWatchAt.Length) DeathWatch.RemoveAt(i);
                else DeathWatch[i] = (a, at, step, why);
            }
        }

        private static string DeathLook(Animal a)
        {
            var parts = new List<string>();
            try
            {
                if (a.Go == null || a.Ai == null) return "тела нет";
                parts.Add("режим " + a.Ai.m_CurrentMode);
                parts.Add($"здоровье {a.Ai.m_CurrentHP:0.#}");
                parts.Add(a.Real ? (a.Shadow ? "тень" : "настоящий") : "копия");
                var an = a.Anim;
                if (an == null) parts.Add("аниматора нет");
                else
                {
                    parts.Add("аниматор " + (an.isActiveAndEnabled ? "вкл" : "ВЫКЛ") + ", отсечение " + an.cullingMode);
                    var ps = a.Params ?? ParamsOf(an);
                    if (ps != null)
                        for (int i = 0; i < Bools.Length; i++)
                            if (ps.Contains(BoolH[i])) parts.Add($"{Bools[i]} {(an.GetBool(BoolH[i]) ? "да" : "нет")}");
                    var st = an.GetCurrentAnimatorStateInfo(0);
                    parts.Add($"анимация {st.fullPathHash} «{ClipOf(an)}» ({st.normalizedTime:0.00}){(an.IsInTransition(0) ? ", в переходе" : "")}");
                }
                parts.Add($"y {a.Go.transform.position.y:0.00}, от присланного места {Vector3.Distance(a.Go.transform.position, a.Pos):0.0} м");
            }
            catch (Exception e) { parts.Add(e.Message); }
            return string.Join(", ", parts);
        }

        // Наименьший зазор между костями туши и землёй под ними.
        private static float CarcassGap(Animal a)
        {
            try
            {
                a.Bones ??= a.Go.GetComponentsInChildren<Transform>();
                var root = Root(a);
                int step = Math.Max(1, a.Bones.Length / 24);
                float best = float.MaxValue;
                for (int i = 0; i < a.Bones.Length; i += step)
                {
                    var b = a.Bones[i];
                    if (b == null) continue;
                    var p = b.position;
                    foreach (var h in Physics.RaycastAll(p + Vector3.up * 0.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        var c = h.collider;
                        if (c == null || c.attachedRigidbody != null || c.transform.IsChildOf(root) || c.gameObject.name.StartsWith("TLDCoop")) continue;
                        best = Mathf.Min(best, p.y - h.point.y);
                    }
                }
                return best;
            }
            catch { a.Bones = null; return 0f; }
        }

        // Состояние туши, пришедшее раньше, чем у нас появилась сама туша.
        private static readonly Dictionary<long, string> PendingHarvest = new();
        private static readonly HashSet<int> HarvestSeen = new();
        private static float _harvestResendAt = -1f;

        private static void HarvestTick()
        {
            float now = Time.unscaledTime;
            bool cutting = HarvestingNow(now);
            SendCarcassItem(now);
            ApplyWaitingMapCarcasses();
            ResolvePickup(false);
            WatchCarcasses();
            int others = -1;
            foreach (var a in Animals.Values)
            {
                if (!a.Dead || a.Go == null) continue;
                var bh = a.Go.GetComponent<BodyHarvest>();
                if (bh == null) continue;
                if (PendingHarvest.TryGetValue(a.Gid, out var pend))
                {
                    PendingHarvest.Remove(a.Gid);
                    ApplyHarvest(a, bh, pend);
                    continue;
                }
                string state;
                try { state = bh.Serialize(); } catch { continue; }
                if (string.IsNullOrEmpty(state)) continue;
                float meat;
                if (state == a.Harvest)
                {
                    // Ничего не изменилось — разве что мясо ждёт отправки (его придержали, пока ели звери).
                    if (SameMeat(meat = MeatOf(state), a.SentMeat)) continue;
                }
                else meat = MeatOf(state);
                bool first = a.Harvest == null;
                bool cuttingThis = cutting && a.Go.Pointer == _harvestPanelGo;
                a.Harvest = state;
                // Порча и счётчики времени меняются сами каждую секунду: из-за них не рассылаем (раньше туша
                // «менялась» раз в секунду и затирала у других срезанное).
                string sig = HarvestSig(state);
                bool cut = sig != a.HarvestSig;   // шкура, внутренности, разделана — сразу
                a.HarvestSig = sig;
                // Первое состояние туши рассылает только тот, у кого мозг (у него туша настоящая). Тень у хозяина
                // (зверь убит у другого) сама первой не рассылает: настоящая туша у убившего.
                if (first && (!a.Real || a.Shadow)) { a.SentMeat = meat; continue; }
                if (!first && !cut)
                {
                    if (SameMeat(meat, a.SentMeat)) continue;
                    // Изменилось только мясо. Срезали мы — сразу. Иначе тушу едят звери: не чаще раза в MeatGap
                    // и только когда на локации есть другие (раньше надёжное событие каждую секунду: 244 за 4 минуты).
                    if (!cuttingThis)
                    {
                        if (others < 0) others = PlayersHere(_scene, now, false).Count;
                        if (others == 0 || now - a.MeatSentAt < MeatGap) { _meatHeld++; continue; }
                    }
                }
                var w = Msg.New(MsgType.AnimalHarvest);
                w.Write(_scene);
                w.Write(a.Gid);
                w.Write(state);
                Session.SendGameEvent(Msg.Done(w));
                a.SentMeat = meat;
                a.MeatSentAt = now;
                if (!first && (cut || cuttingThis)) MelonLogger.Msg($"[звери] туша {a.Prefab} изменилась, разослал{(cut ? "" : " (мясо)")}");
            }
        }

        private static void ReceiveHarvest(BinaryReader r)
        {
            string scene = r.ReadString();
            long gid = r.ReadInt64();
            string state = r.ReadString();
            if (scene != _scene) return;
            BodyHarvest bh = null;
            if (!Animals.TryGetValue(gid, out var a) || a.Go == null || !a.Dead || (bh = a.Go.GetComponent<BodyHarvest>()) == null)
            {
                // Туши у нас ещё нет (только зашли в локацию): применим, когда появится.
                PendingHarvest[gid] = state;
                return;
            }
            // Мы только что срезали мясо и разослали, а пришло состояние, где мяса больше и больше ничем не отличается:
            // оно отправлено до нашего (тушу у того ели звери). Не возвращаем себе срезанное — наше дойдёт до него.
            if (Time.unscaledTime - a.MeatSentAt < 5f && HarvestSig(state) == a.HarvestSig)
            {
                float inMeat = MeatOf(state), mine = MeatOf(a.Harvest);
                if (!float.IsNaN(inMeat) && !float.IsNaN(mine) && inMeat > mine && !SameMeat(inMeat, mine)) { _meatStale++; return; }
            }
            ApplyHarvest(a, bh, state);
        }

        private static void ApplyHarvest(Animal a, BodyHarvest bh, string state)
        {
            try
            {
                bh.enabled = true;
                if (!a.Real) CarcassInteraction(a, "пришло состояние туши");
                bh.Deserialize(state, false);
                NoteHarvestApplied(a, bh);
                a.Harvest = bh.Serialize();
                a.HarvestSig = HarvestSig(a.Harvest);
                a.SentMeat = MeatOf(a.Harvest);   // пришедшее обратно не рассылаем
                a.HarvestAt = Time.unscaledTime;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[звери] туша: " + e.Message);
            }
        }

        // Хозяин локации: в локацию пришёл новый игрок — через 3 секунды (когда у него появятся туши)
        // шлём ему последнее состояние всех туш, иначе разделанная до него туша у него целая.
        private static void ResendHarvestForNewcomers(float now)
        {
            var here = HereNow();
            bool fresh = false;
            foreach (var p in here)
                if (p.id != Session.LocalId && HarvestSeen.Add(p.id)) fresh = true;
            HarvestSeen.RemoveWhere(id => !here.Exists(p => p.id == id));
            if (fresh && IsAuthority) _harvestResendAt = now + 3f;
            if (_harvestResendAt < 0f || now < _harvestResendAt) return;
            _harvestResendAt = -1f;
            int n = 0;
            foreach (var a in Animals.Values)
            {
                if (!a.Dead || string.IsNullOrEmpty(a.Harvest)) continue;
                var w = Msg.New(MsgType.AnimalHarvest);
                w.Write(_scene);
                w.Write(a.Gid);
                w.Write(a.Harvest);
                Session.SendGameEvent(Msg.Done(w));
                n++;
            }
            _harvestResent += n;
            if (n > 0) MelonLogger.Msg($"[звери] в локацию пришёл игрок, разослал состояние туш: {n}");
        }

        // Для износа (KDecay): эта туша только что пришла от другого игрока — старое состояние износа её не трогает.
        public static bool HarvestFresh(BodyHarvest bh)
        {
            if (bh == null) return false;
            foreach (var a in Animals.Values)
                if (a.Go != null && a.Go.Pointer == bh.gameObject.Pointer) return Time.unscaledTime - a.HarvestAt < 15f;
            return false;
        }

        // ---------- Места появления ----------
        // Рассылка и приём — Game/AnimalSync.RegionSync.cs.

        // Состояние места появления от другого: ставим, только пока зверей здесь ведём не мы.
        public static bool AcceptRegion(SpawnRegion sr) => sr != null && !IsAuthority;

        // ---------- Модели ----------

        // Модели зверей берём из мест появления этой карты: там ссылки на все её виды. Раньше каждую грузили
        // с ожиданием (WaitForCompletion) прямо в кадре, все виды всех мест, вместе с «северным сиянием», и так
        // после каждой смены локации. Сразу после загрузки у игры ещё идут свои загрузки, и такое ожидание
        // может не кончиться никогда: хост 0.19.2 встал намертво через 2 с после входа в Озеро. Теперь загрузку
        // только запускаем, а готовые модели забираем в следующих кадрах. Пока модель не готова, копию не
        // создаём: следующее состояние зверя придёт через 0,1 с, туши от друга подождут (DeferCarcass).
        private sealed class PrefabLoad
        {
            public UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject> H;
            public AssetReferenceBaseAi Ref;
            public SpawnRegion Region;
            public string Name;      // загрузка по имени модели (её нет в местах появления), иначе null
            public float Since;
        }

        private static readonly List<PrefabLoad> PrefabLoads = new();
        private static readonly HashSet<string> PrefabKeys = new();   // ключи, загрузку которых уже запустили на этой локации
        private static string _prefabScene;
        private static float _prefabScanAt = -100f;
        private static int _prefabStarted, _prefabDone, _prefabFailed, _prefabWaits, _prefabScans;
        private const float PrefabTimeout = 30f;

        // Модели этой локации ещё грузятся: «нет модели» сейчас значит «подожди», а не «такой нет».
        private static bool PrefabsLoading => PrefabLoads.Count > 0;

        // again — ещё раз (не чаще раза в 2 с): места появления части локации могли загрузиться позже первого прохода.
        // Уже запущенные ключи второй раз не грузим.
        private static void ScanPrefabs(float now, bool again)
        {
            if (_prefabScene == _scene && !(again && now - _prefabScanAt >= 2f)) return;
            _prefabScene = _scene;
            _prefabScanAt = now;
            _prefabScans++;
            int started = 0, regions = 0;
            string prev = Enter("модели зверей: места появления");
            try
            {
                foreach (var sr in UnityEngine.Object.FindObjectsOfType<SpawnRegion>(true))
                {
                    var entries = sr?.m_SpawnList;
                    if (entries == null) continue;
                    regions++;
                    foreach (var e in entries)
                    {
                        foreach (var aref in new[] { e?.m_Normal, e?.m_Aurora })
                        {
                            if (aref == null || !aref.RuntimeKeyIsValid()) continue;
                            string key;
                            try { key = aref.RuntimeKey?.ToString() ?? ""; } catch { continue; }
                            if (key.Length == 0 || !PrefabKeys.Add(key)) continue;
                            try
                            {
                                var h = Addressables.LoadAssetAsync<GameObject>(aref.RuntimeKey);
                                PrefabLoads.Add(new PrefabLoad { H = h, Ref = aref, Region = sr, Since = now });
                                started++;
                            }
                            catch (Exception ex) { _prefabFailed++; if (_prefabLogs++ < 10) MelonLogger.Warning($"[звери] модель из места появления {sr.name}: {ex.Message}"); }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[звери] модели из мест появления: " + e.Message);
            }
            finally { Leave(prev); }
            _prefabStarted += started;
            PollPrefabs(now);
            if (started > 0 || _prefabScanLogs++ < 5)
                MelonLogger.Msg($"[звери] модели зверей {_scene}{(again ? " (ещё раз)" : "")}: мест появления {regions}, загрузок запустил {started}, ещё грузятся {PrefabLoads.Count}");
        }

        private static int _prefabLogs, _prefabScanLogs;

        // Каждый кадр: забираем готовые модели. Ничего не ждём.
        private static void PollPrefabs(float now)
        {
            if (PrefabLoads.Count == 0) return;
            string prev = Enter("модели зверей: готовые");
            try
            {
                for (int i = PrefabLoads.Count - 1; i >= 0; i--)
                {
                    var l = PrefabLoads[i];
                    bool done;
                    try { done = l.H.IsDone; } catch { done = true; }
                    if (!done)
                    {
                        if (now - l.Since < PrefabTimeout) continue;
                        _prefabFailed++;
                        if (_prefabLogs++ < 10) MelonLogger.Warning($"[звери] модель {(l.Name ?? l.Region?.name ?? "?")} не загрузилась за {PrefabTimeout:0} с, бросил");
                        PrefabLoads.RemoveAt(i);
                        continue;
                    }
                    PrefabLoads.RemoveAt(i);
                    GameObject obj = null;
                    try
                    {
                        if (l.H.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded) obj = l.H.Result;
                    }
                    catch { }
                    if (obj == null) { _prefabFailed++; continue; }
                    _prefabDone++;
                    string name = l.Name ?? obj.name;
                    Prefabs[name] = obj;
                    if (l.Ref != null)
                    {
                        if (!Refs.ContainsKey(name)) Refs[name] = l.Ref;
                        if (!RefRegion.ContainsKey(name) && l.Region != null) RefRegion[name] = l.Region;
                    }
                }
            }
            finally { Leave(prev); }
            if (PrefabLoads.Count == 0)
                MelonLogger.Msg($"[звери] модели зверей {_scene} готовы за {now - _prefabScanAt:0.0} с: {Prefabs.Count} ({string.Join(", ", Prefabs.Keys)})");
        }

        private static GameObject FindPrefab(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Prefabs.TryGetValue(name, out var go) && go != null) return go;
            if (MissingPrefabs.Contains(name)) return null;
            float now = Time.unscaledTime;
            ScanPrefabs(now, false);
            PollPrefabs(now);
            if (Prefabs.TryGetValue(name, out go) && go != null) return go;
            if (PrefabsLoading) { _prefabWaits++; return null; }   // ещё грузятся — спросят снова
            // Среди загруженных нет: пройдём места появления ещё раз — вдруг они появились после первого прохода.
            ScanPrefabs(now, true);
            if (Prefabs.TryGetValue(name, out go) && go != null) return go;
            if (PrefabsLoading) { _prefabWaits++; return null; }
            // В местах появления такой нет: пробуем по имени модели (тоже без ожидания).
            if (PrefabKeys.Add("name:" + name))
            {
                try
                {
                    var key = new Il2CppSystem.Object(IL2CPP.ManagedStringToIl2Cpp(name));
                    var h = Addressables.LoadAssetAsync<GameObject>(key);
                    PrefabLoads.Add(new PrefabLoad { H = h, Name = name, Since = now });
                    _prefabStarted++;
                    PollPrefabs(now);
                    if (Prefabs.TryGetValue(name, out go) && go != null) return go;
                    if (PrefabsLoading) { _prefabWaits++; return null; }
                }
                catch { }
            }
            MissingPrefabs.Add(name);
            MelonLogger.Warning("[звери] не нашёл модель " + name + ", есть: " + string.Join(", ", Prefabs.Keys));
            return null;
        }

        private static string PrefabStats => $"моделей загрузил {_prefabDone} (запустил {_prefabStarted}, не вышло {_prefabFailed}, проходов {_prefabScans}), копий ждали модель {_prefabWaits}, передач ждали модель {_transfersDeferred}";

        // ---------- Попадания ----------

        private static bool _applyingHit;
        private static int _lastHitFrame = -1;
        private static long _lastHitGid;
        private static float _lastHitDamage;

        // Патч урона: попадание в копию уходит тому, у кого мозг зверя; копии сама урон не наносим.
        public static bool OnDamage(BaseAi ai, float damage, float bleed, DamageSource source, string collider)
        {
            if (_applyingHit || ai == null || !ByAi.TryGetValue(ai.Pointer, out long gid) || !Animals.TryGetValue(gid, out var a)) return true;
            if (a.Real) { if (source == DamageSource.Player) a.LastHitBy = Session.LocalId; return true; }
            if (Time.frameCount == _lastHitFrame && gid == _lastHitGid && Mathf.Approximately(damage, _lastHitDamage)) return false;
            _lastHitFrame = Time.frameCount; _lastHitGid = gid; _lastHitDamage = damage;
            if (a.Dead) return false;
            int to = a.Controller >= 0 ? a.Controller : _authority;
            if (to < 0 || to == Session.LocalId)
            {
                _hitsLost++;
                MelonLogger.Msg($"[звери] попал в {a.Prefab}, но не знаю, у кого его мозг — урон пропал");
                return false;
            }
            SendHit(to, gid, damage, bleed, source, collider, Session.LocalId, (Session.LocalId << 24) | (++_hitSeq & 0xFFFFFF), 0);
            MelonLogger.Msg($"[звери] попал в {a.Prefab}: урон {damage:0.#}, отправил {NameOf(to)}");
            return false;
        }

        private static int _hitSeq;
        private const int MaxHitHops = 3;
        // Номера уже применённых попаданий: одно попадание, пришедшее дважды (пересылка, повтор), не ранит дважды.
        private static readonly HashSet<int> AppliedHits = new();
        private static readonly Queue<int> AppliedOrder = new();

        private static void SendHit(int to, long gid, float damage, float bleed, DamageSource source, string collider, int shooter, int hitId, int hops)
        {
            var w = Msg.New(MsgType.AnimalHit);
            w.Write((byte)to);
            w.Write(gid);
            w.Write(damage);
            w.Write(bleed);
            w.Write((int)source);
            w.Write(collider ?? "");
            w.Write((byte)shooter);
            w.Write(hitId);
            w.Write((byte)hops);
            Session.SendGameEvent(Msg.Done(w));
        }

        private static void ReceiveHit(BinaryReader r, string who)
        {
            int to = r.ReadByte();
            long gid = r.ReadInt64();
            float damage = r.ReadSingle(), bleed = r.ReadSingle();
            int source = r.ReadInt32();
            string collider = r.ReadString();
            int shooter = r.ReadByte();
            bool hasId = r.BaseStream.Position < r.BaseStream.Length;
            int hitId = hasId ? r.ReadInt32() : 0;
            int hops = hasId ? r.ReadByte() : MaxHitHops;
            if (to != Session.LocalId || OtherWorld(shooter)) return;
            if (hasId && AppliedHits.Contains(hitId)) { _hitsDup++; return; }
            if (!Animals.TryGetValue(gid, out var a) || a.Ai == null || !a.Real)
            {
                // Мозг зверя успел уйти к другому игроку, пока летело попадание: пересылаем ему.
                int next = a != null && !a.Dead ? (a.Controller >= 0 ? a.Controller : _authority) : -1;
                if (next >= 0 && next != Session.LocalId && hops < MaxHitHops)
                {
                    SendHit(next, gid, damage, bleed, (DamageSource)source, collider, shooter, hitId, hops + 1);
                    _hitsForwarded++;
                    MelonLogger.Msg($"[звери] {who} попал в {a.Prefab}, мозг уже у {NameOf(next)} — переслал");
                }
                else
                {
                    _hitsLost++;
                    MelonLogger.Msg($"[звери] {who} попал в животное, но мозг его уже не у меня" + (a != null && a.Dead ? " (уже мёртв)" : ""));
                }
                return;
            }
            if (hasId)
            {
                AppliedHits.Add(hitId);
                AppliedOrder.Enqueue(hitId);
                while (AppliedOrder.Count > 256) AppliedHits.Remove(AppliedOrder.Dequeue());
            }
            // Испуг или оглушение брошенной вещью у копии (Game/AnimalSync.Stone.cs) — не урон.
            _applyingHit = true;
            try { if (ApplyScare(a, source, collider, who)) return; }
            finally { _applyingHit = false; }
            a.LastHitBy = shooter;
            // Пока зверь думает, «игрок» для него — стрелявший: на выстрел зверь отвечает ему (бежит от него
            // или бросается на него), а не на нас.
            HitBy = shooter;
            _applyingHit = true;
            try
            {
                if (shooter != Session.LocalId && a.Go != null) a.Focus = shooter;
                // Смертельное попадание отвечать некому: игрока к стрелявшему не переносим (смерть зверя
                // при подмене уводила хоста к другу).
                bool lethal = damage >= a.Ai.m_CurrentHP;
                if (!lethal) BeforeAiUpdate(a.Ai, true);
                a.Ai.ApplyDamage(damage, bleed, (DamageSource)source, collider);
            }
            finally { AfterAiUpdate(); _applyingHit = false; HitBy = -1; }
            MelonLogger.Msg($"[звери] {who} попал в {a.Prefab}: урон {damage:0.#}, осталось {a.Ai.m_CurrentHP:0.#}");
        }

        // ---------- Сообщения ----------

        public static int AddressOf(byte[] raw) => raw.Length > 1 ? raw[1] : -1;

        private static readonly Dictionary<MsgType, string> StepNames = new();

        private static string StepOf(MsgType type)
        {
            if (!StepNames.TryGetValue(type, out var s)) StepNames[type] = s = "сообщение " + type;
            return s;
        }

        public static void ReceiveAddressed(MsgType type, BinaryReader r, string who)
        {
            string prev = Enter(StepOf(type));
            try
            {
                if (type == MsgType.AnimalHit) ReceiveHit(r, who);
                else if (type == MsgType.AnimalTransfer) ReceiveTransfer(r, who);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[звери] " + type + ": " + e.Message);
            }
            finally { Leave(prev); }
        }

        public static void ReceiveBroadcast(MsgType type, BinaryReader r)
        {
            string prev = Enter(StepOf(type));
            try
            {
                if (type == MsgType.AnimalState) ReceiveState(r);
                else if (type == MsgType.AnimalRemove) ReceiveRemove(r);
                else if (type == MsgType.AnimalAuthority) ReceiveAuthority(r);
                else if (type == MsgType.AnimalHarvest) ReceiveHarvest(r);
                else if (type == MsgType.AnimalSound) ReceiveSound(r);
                else if (type == MsgType.AnimalSense) ReceiveSense(r);
                else if (type == MsgType.AnimalNoise) ReceiveNoise(r);
                else if (type == MsgType.AnimalArrow) ReceiveArrow(r);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[звери] " + type + ": " + e.Message);
            }
            finally { Leave(prev); }
        }

        // ---------- Состояние для окна и лога ----------

        private static void UpdateStatus()
        {
            int real = 0, copies = 0;
            foreach (var a in Animals.Values) { if (a.Real) real++; else copies++; }
            Status = _authority < 0 ? "животные: выясняю хозяина локации"
                : $"хозяин животных: {NameOf(_authority)}; у меня думают {real}, копий {copies}";
        }

        private static void LogStats()
        {
            MelonLogger.Msg($"[звери] {Status}; копий через место появления {_replicas}, из модели {_builtReplicas}; одолжил при броске {_leasesOut}, вернули {_returns}, принял {_leasesIn}; "
                + $"моя игра создала лишних {_stray}, не дал создать {_blockedSpawns}, уступил хозяина {_yields}; {SenseStats}; {SpawnStats}; "
                + $"забрал у молчащих {_byQuiet}, старых состояний {_staleStates}, старых передач {_staleTransfers}; попаданий переслал {_hitsForwarded}, повторов {_hitsDup}, потерял {_hitsLost}; туш дослал {_harvestResent}, мяса придержал {_meatHeld}, старых туш не взял {_meatStale}, туш взял себе {_carcassesTaken}; "
                + $"включил выключенных моделей копий {_skinsShown}, устаревших адресов зверей {_staleByAi}, отдал при уступке хозяина {_yieldTransfers}, сбросил точки копии при смене ведущего {_ringResets}; вынес из выключенных {_unhidden}, убрал мёртвых из воющих {_howlPurged}, повторов в списке зверей игры не внёс {_aiAddsSkipped}, уничтоженных из него убрал {_deadAiPurged}; {CarryStats}; {PrefabStats}; лишних туш уничтожил {_strayDestroyed}, выключенных копий уничтожил {_orphansDestroyed}; {GuardStats}; {RegionStats}; {RegionSyncStats}; {OtherWorldStats()}; {CarStats}; {StruggleEndStats}; {TimeLapseStats}; {NoiseStats}; {StoneStats}; {StunStats}; {CarcassItemStats}; {CarcassViewStats}; {ArrowStats}; включил взаимодействие копиям туш {_carcassInteractions}; звуков отправил {_soundsOut}, проиграл чужих {_soundsIn}, событий анимации у копий {_puppetAnimEvents}, заглушил перед уничтожением {_silenced}, не сыграл у копий без вестей {_staleMuted}, звуков схватки чужой копии у себя не сыграл {_struggleMuted}, оборвал чужих при сбросе {_heardStopped}");
        }
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.Despawn))]
    internal static class Patch_AnimalDespawn
    {
        private static bool Prefix(BaseAi __instance) => AnimalSync.AllowDespawn(__instance);
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), new[] { typeof(float), typeof(float), typeof(DamageSource), typeof(string) })]
    internal static class Patch_AnimalDamage4
    {
        private static bool Prefix(BaseAi __instance, float damage, float bleedOutMintues, DamageSource damageSource, string collider) =>
            AnimalSync.OnDamage(__instance, damage, bleedOutMintues, damageSource, collider);
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), new[] { typeof(float), typeof(DamageSource), typeof(string) })]
    internal static class Patch_AnimalDamage3
    {
        private static bool Prefix(BaseAi __instance, float damage, DamageSource damageSource, string collider) =>
            AnimalSync.OnDamage(__instance, damage, 0f, damageSource, collider);
    }
}
