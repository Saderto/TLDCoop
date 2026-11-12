namespace SmokesByEMPEROR;

/// <summary>UserData/MelonPreferences.cfg, section SmokesByEMPEROR. Balance numbers are the owner's; the hand pose is for tuning in game.</summary>
public static class Settings
{
    public static MelonPreferences_Entry<bool> RealTime = null!;
    public static MelonPreferences_Entry<float> CigaretteRealMinutes = null!, CigarRealMinutes = null!, PipeRealMinutes = null!, CigarMinMinutes = null!, CigarMaxMinutes = null!;
    public static MelonPreferences_Entry<float> DragMinSeconds = null!, DragMaxSeconds = null!;
    public static MelonPreferences_Entry<float> HungerLess = null!;
    public static MelonPreferences_Entry<float> BreathPerDay = null!, BreathStaminaLess = null!;
    public static MelonPreferences_Entry<float> AddictionCount = null!, AddictionWindowHours = null!, CravingAfterHours = null!, AddictionEndsHours = null!;
    public static MelonPreferences_Entry<float> PipeHeldTurn = null!, ElbowDown = null!, PipeWristTurn = null!, PipeOverFist = null!, RestWristBend = null!, RestSmooth = null!, CigarThumbLift = null!, CigThumbLift = null!, CigPinkyDown = null!, CigarThumbStand = null!, SlimSqueeze = null!, CigFilterOut = null!, CigThumbToIndex = null!, CigThumbTipTurn = null!, CigarThumbDragDown = null!, CigarFlickScale = null!;
    public static MelonPreferences_Entry<bool> PipeHeldAsMouth = null!;
    public static MelonPreferences_Entry<float> CravingFatigueMore = null!, CravingSwayMore = null!;
    public static MelonPreferences_Entry<float> CutterWearPct = null!, KnifeTearChance = null!;
    public static MelonPreferences_Entry<string> PropPos = null!, PropRot = null!, PipePos = null!, PipeRot = null!;
    public static MelonPreferences_Entry<string> CigOffset = null!, CigarOffset = null!, PipeOffset = null!, CigTurn = null!, CigarTurn = null!, PipeTurnAt = null!;
    public static MelonPreferences_Entry<bool> ByOffset = null!, TuneKeys = null!, FollowView = null!, GameView = null!, CarryArms = null!, LockCamera = null!;
    public static MelonPreferences_Entry<string> DragHandBelow = null!;
    public static MelonPreferences_Entry<float> DragSplit = null!, FollowAmount = null!, ViewPivot = null!, CigarThumb = null!, PipeThumb = null!, PipeThumbAbove = null!, PipeThumbBaseLift = null!, PipeThumbClearance = null!, PipeThumbOverGap = null!;
    public static MelonPreferences_Entry<bool> CigNameToView = null!;
    public static MelonPreferences_Entry<float> CigNameRoll = null!;
    public static MelonPreferences_Entry<string> CutterSounds = null!, KnifeCutSounds = null!, RestOnScreen = null!, PipeHandAway = null!, CigInView = null!, CigarInView = null!, PipeInView = null!, CigAim = null!, CigarAim = null!, MouthAt = null!, CigInFingers = null!, RestFingers = null!, RestBack = null!, PipeMouthAt = null!, CigMouthDir = null!, PipeMouthDir = null!, CigMouthUp = null!;
    public static MelonPreferences_Entry<float> PropScale = null!, FingerOpen = null!, SoundVolume = null!, CigarSpread = null!, CigarJoint = null!, CigaretteGrip = null!, CigarGrip = null!, PipeStemUp = null!, PipeTurn = null!, RestHandTurn = null!, HandsFov = null!, EyePitch = null!, EyeHeight = null!, CigaretteFilterStop = null!, CigarBandStop = null!, ThrowSpeed = null!;
    public static MelonPreferences_Entry<int> HandsViewMode = null!;
    public static MelonPreferences_Entry<bool> Sounds = null!, SmokesTab = null!, CigarPinchOn = null!, Markers = null!, PoseOff = null!, BareHands = null!, FlipCigarette = null!;
    public static MelonPreferences_Entry<bool> DebugLog = null!, TestKeys = null!, GiveLightersKey = null!;
    public static MelonPreferences_Entry<float> LootContainer = null!, LootWorld = null!, LootCabinet = null!, LootAny = null!;
    public static MelonPreferences_Entry<bool> PlaceBoxKey = null!;

    /// <summary>Per-pipe defaults found by tuning in game (offset, turn); null = the common pipe setting.</summary>
    public static string? PipeDefault(string id, bool offset) => id switch
    {
        "LitPipeCorncob" => offset ? "-0.09 0.23 -0.28" : "15 -65 -45",
        "LitPipeBent" => offset ? "-0.08 0.29 -0.31" : "25 -115 -45",
        "LitPipeBilliard" => offset ? "-0.09 0.22 -0.21" : "20 -120 -45",
        "LitPipeChurchwarden" => offset ? "-0.04 0.37 -0.43" : "25 -65 -60",
        _ => null,
    };

    public static void Create()
    {
        var c = MelonPreferences.CreateCategory("SmokesBySeagull");
        LootContainer = c.CreateEntry("LootContainerPercent", 100f, "Сколько курева в ящиках, % от обычного (0 = нет) / smokes in containers, % of normal (0 = none)");
        LootAny = c.CreateEntry("LootAnyContainerPercent3", 18f, "Шанс курева в любом контейнере сверх обычного, % (шкаф с ящиками считается одним) / extra chance of smokes in any container, % (a chest of drawers counts as one)");
        LootCabinet = c.CreateEntry("LootFilingCabinetPercent2", 30f, "Шанс найти пачку или сигару в тубе в шкафу для документов, % / chance of a pack or a tubed cigar in a filing cabinet, %");
        PlaceBoxKey = c.CreateEntry("PlaceCigarBoxKey2", false, "Тест: Ctrl+Shift+B ставит коробку сигар (каждый раз другую) / test: Ctrl+Shift+B places a cigar box (a different one each time)");
        LootWorld = c.CreateEntry("LootWorldPercent", 100f, "Сколько курева лежит на локациях, % от обычного (0 = нет) / smokes lying around, % of normal (0 = none)");
        RealTime = c.CreateEntry("BurnInRealTime", true, "Время горения в реальных минутах (иначе игровые 5/15/10) / burn time in real minutes (else game minutes 5/15/10)");
        CigaretteRealMinutes = c.CreateEntry("CigaretteRealMinutes2", 2.25f, "Обычная сигарета, реальных минут (другие форматы и марки по своей доле) / regular cigarette, real minutes (other formats and brands by their share)");
        CigarRealMinutes = c.CreateEntry("CigarRealMinutes2", 6f, "Сигара в среднем, реальных минут (каждая по своему размеру) / average cigar, real minutes (each by its size)");
        CigarMinMinutes = c.CreateEntry("CigarShortestRealMinutes", 3.2f, "Самая короткая сигара, реальных минут / shortest cigar, real minutes");
        CigarMaxMinutes = c.CreateEntry("CigarLongestRealMinutes", 7f, "Самая длинная сигара (черчилль), реальных минут / longest cigar (churchill), real minutes");
        PipeRealMinutes = c.CreateEntry("PipeRealMinutes3", 3f, "Трубка, реальных минут / pipe, real minutes");
        DragMinSeconds = c.CreateEntry("DragMinSeconds2", 5f, "Затяжка не чаще, секунд / drag at most every, seconds");
        DragMaxSeconds = c.CreateEntry("DragMaxSeconds2", 7.5f, "Затяжка не реже, секунд / drag at least every, seconds");
        HungerLess = c.CreateEntry("HungerBurnLess", .10f, "Голод медленнее на / hunger burns slower by (0.10 = 10%)");
        BreathPerDay = c.CreateEntry("BreathlessAfterPerDay", 5f, "Одышка, если за сутки больше / shortness of breath above this many per day");
        BreathStaminaLess = c.CreateEntry("BreathlessStaminaLess", .30f, "Одышка: выносливость восстанавливается медленнее на / stamina recovers slower by");
        AddictionCount = c.CreateEntry("AddictionCount", 15f, "Зависимость от стольких за окно / addiction from this many in the window");
        AddictionWindowHours = c.CreateEntry("AddictionWindowHours", 72f, "Окно, игровых часов / window, game hours");
        CravingAfterHours = c.CreateEntry("CravingAfterHours", 12f, "Тяга через, игровых часов без курения / craving after, game hours without smoking");
        AddictionEndsHours = c.CreateEntry("AddictionEndsHours", 72f, "Зависимость проходит через, игровых часов без курения / addiction ends after, game hours");
        CravingFatigueMore = c.CreateEntry("CravingFatigueMore", .18f, "Тяга: усталость растёт быстрее на / craving: fatigue grows faster by");
        CravingSwayMore = c.CreateEntry("CravingSwayMore", .25f, "Тяга: прицел качается сильнее на / craving: aim sway larger by");
        CutterWearPct = c.CreateEntry("CutterWearPercent", .5f, "Износ гильотины за обрезку, % / cutter wear per cut, %");
        KnifeTearChance = c.CreateEntry("KnifeTearChance", .30f, "Шанс порвать сигару ножом / chance a knife tears the cigar");
        PropPos = c.CreateEntry("HeldPosition", "0 -0.02 0", "Сигарета/сигара в руке: сдвиг x y z от ручки факела / held cigarette offset from the torch handle");
        PropRot = c.CreateEntry("HoldTilt", "0.7 0.55 -0.15", "Сигарета/сигара: горящий конец = вдоль кулака / вперёд от камеры / вправо от камеры / lit end: along the fist, camera forward, camera right");
        PipeStemUp = c.CreateEntry("PipeStemAboveFist", .03f, "Трубка: мундштук над кулаком, м (чаша в кулаке) / pipe stem above the fist, m (bowl in the fist)");
        CutterSounds = c.CreateEntry("CigarCutterSounds2", "cut1", "Звук гильотины: наш звук cut1/cut2/cut3 или события игры через запятую (Ctrl+Shift+F10 перебирает наши) / cutter sound: our cut1/cut2/cut3 or game events, comma separated (Ctrl+Shift+F10 tries ours)");
        KnifeCutSounds = c.CreateEntry("CigarKnifeCutSounds3", "knife2", "Звук обрезки ножом: наш knife1/knife2 или события игры через запятую / knife cut sound: our knife1/knife2 or game events, comma separated");
        RestOnScreen = c.CreateEntry("HeldSmokeOnScreen2", "0.76 0.16", "Сигарета/сигара в опущенной руке: где на экране, x y (0..1) / cigarette or cigar in the lowered hand: where on screen, x y (0..1)");
        PipeWristTurn = c.CreateEntry("PipeWristTurnMax", 50f, "Трубка ко рту: кисть поворачивается не больше, градусы (остальное трубка в пальцах) / pipe to the mouth: the wrist turns at most, degrees (the rest in the fingers)");
        ElbowDown = c.CreateEntry("DragElbowDown", .85f, "Затяжка: локоть опущен под кисть (0 = как согнут, 1 = прямо вниз) / drag: elbow turned down under the hand (0 = as bent, 1 = straight down)");
        PipeHeldTurn = c.CreateEntry("PipeHeldStemAcross", 55f, "Трубка в руке (кроме кукурузной): мундштук повёрнут поперёк, влево, градусы / pipe in the hand (not the corn cob): stem turned across to the left, degrees");
        PipeOverFist = c.CreateEntry("PipeBowlOverFist4", .018f, "Трубка в руке: дно чаши над кулаком, м / pipe in the hand: bowl foot above the fist, m");
        PipeHeldAsMouth = c.CreateEntry("PipeHeldAsMouth", true, "Трубка в кулаке сразу повёрнута как во рту (не крутится по дороге) / pipe held in the fist turned as in the mouth (no spin on the way)");
        PipeTurn = c.CreateEntry("PipeStemTurn", 0f, "Трубка: поворот мундштука вокруг чаши, градусы / pipe stem turned about the bowl, degrees");
        CigaretteGrip = c.CreateEntry("CigaretteGripJoint2", 1.15f, "Сигарета между указательным и средним: 0 = у костяшек, 1 = первый сустав, 3 = кончики / cigarette between index and middle: 0 knuckles, 1 first joint .. 3 fingertips");
        CigarGrip = c.CreateEntry("CigarPinch", 0.5f, "Сигара в щепоти: 0 = у подушечек пальцев, 1 = у большого пальца / cigar pinched: 0 at the finger pads .. 1 at the thumb");
        CigInFingers = c.CreateEntry("CigaretteInFingers2", "0.85 0.0 0.5", "Сигарета в пальцах, горящий конец: от тыльной стороны / от большого пальца / к кончикам / lit end: out of the back of the hand, away from the thumb, towards the tips");
        RestFingers = c.CreateEntry("RestFingers", "-0.45 0.1 0.9", "С сигаретой в покое пальцы смотрят: вправо/вверх/вперёд / resting hand, fingers point: right, up, forward");
        RestBack = c.CreateEntry("RestHandBack", "0.15 1 0.1", "Тыльная сторона кисти в покое: вправо/вверх/вперёд / resting hand, back of the hand faces: right, up, forward");
        CigPinkyDown = c.CreateEntry("CigarettePinkyDown", .012f, "Мизинец у сигареты ниже на столько метров (0 = как есть) / little finger with a cigarette this many metres lower (0 = as is)");
        CigFilterOut = c.CreateEntry("CigaretteFilterBehindFingers", .022f, "Сигарета: не меньше стольких метров выходит за пальцы со стороны фильтра / cigarette: at least this many metres out behind the fingers on the filter side");
        SlimSqueeze = c.CreateEntry("SlimFingerSqueeze2", 4f, "Тонкие сигареты: указательный и средний сводятся на столько градусов каждый (суперслим) / slim cigarettes: index and middle closed in by this many degrees each (superslim)");
        CigarThumbStand = c.CreateEntry("CigarThumbStandUp2", .025f, "Большой палец у сигары стоит: кончик выше ещё на столько метров (0 = лежит) / thumb stands up off a cigar: tip this many metres higher (0 = lies on it)");
        CigarThumbDragDown = c.CreateEntry("CigarThumbDownOnDrag", .012f, "При затяжке сигары кончик большого пальца ниже на столько метров (не виден в камере) / on a cigar drag the thumb tip goes this many metres lower");
        CigThumbToIndex = c.CreateEntry("CigaretteThumbTipToIndex", .012f, "Кончик большого пальца у сигареты повёрнут к указательному на столько метров (0 = нет) / cigarette thumb tip turned towards the index finger by this many metres");
        CigThumbTipTurn = c.CreateEntry("CigaretteThumbTipTurnToIndex", 35f, "Последняя фаланга большого пальца у сигареты повёрнута к указательному, до стольких градусов (0 = нет) / cigarette thumb's last joint turned towards the index finger, up to this many degrees");
        CigarFlickScale = c.CreateEntry("CigarThrowSwing", .45f, "Размах руки при выбрасывании сигары (1 = как у сигареты) / arm swing when throwing a cigar away (1 = as a cigarette)");
        CigThumbLift = c.CreateEntry("CigaretteThumbTipLift2", .013f, "Кончик большого пальца у сигареты выше на столько метров (0 = как есть) / thumb tip with a cigarette this many metres higher (0 = as is)");
        CigarThumbLift = c.CreateEntry("CigarThumbTipLift", .006f, "Кончик большого пальца на сигаре выше на столько метров / thumb tip on a cigar this many metres higher");
        RestSmooth = c.CreateEntry("RestArmSmoothing", 10f, "Сглаживание руки в покое (чем меньше, тем плавнее; 0 = выкл) / resting arm smoothing (lower = smoother; 0 = off)");
        RestWristBend = c.CreateEntry("RestWristBendMax", 30f, "Кисть в покое не выгибается от предплечья больше чем на столько градусов (иначе видно пустоту в рукаве) / resting hand bends from the forearm at most this many degrees (else the hollow sleeve shows)");
        RestHandTurn = c.CreateEntry("RestHandTurn", 1f, "Насколько повернуть кисть к позе выше (0..1) / how far the resting hand turns to that pose");
        HandsFov = c.CreateEntry("HandsFieldOfView", 0f, "Угол обзора рук для подноса ко рту, 0 = как у оружия / hands field of view for the mouth, 0 = the weapon's");
        HandsViewMode = c.CreateEntry("HandsViewMode", 0, "Откуда считать вид рук: 0 сам, 1 камера рук, 2 кость глаза / hands view: 0 auto, 1 rig camera, 2 eye bone");
        CigaretteFilterStop = c.CreateEntry("CigaretteBurnsTo", .027f, "Сигарета сгорает до (м от фильтра) / cigarette burns down to (m from the mouth end)");
        CigarBandStop = c.CreateEntry("CigarBurnsTo", .047f, "Сигара сгорает до этикетки (м от мундштучного конца) / cigar burns down to its band (m from the mouth end)");
        ByOffset = c.CreateEntry("MouthByOffset", true, "Поза у рта = поза в руке + сдвиг (настраивается клавишами) / pose at the mouth = pose in the hand + offset (tuned with keys)");
        TuneKeys = c.CreateEntry("TuneKeys2", false, "Тест: клавиши цифрового блока и стрелки двигают сигарету/трубку у рта / test: numpad and arrow keys move the smoke at the mouth");
        CigOffset = c.CreateEntry("CigaretteMouthOffset3", "-0.26 0.07 -0.62", "Сигарета у рта: сдвиг от руки, м (оси кости камеры рук) / cigarette at the mouth: offset from the hand, m");
        CigarOffset = c.CreateEntry("CigarMouthOffset3", "-0.26 0.09 -0.72", "Сигара у рта: сдвиг от руки, м / cigar at the mouth: offset from the hand, m");
        PipeOffset = c.CreateEntry("PipeMouthOffset3", "-0.20 -0.17 0.31", "Трубка во рту: сдвиг от руки, м / pipe in the mouth: offset from the hand, m");
        CigTurn = c.CreateEntry("CigaretteMouthTurn4", "-35 60 20", "Сигарета у рта: поворот, градусы / cigarette at the mouth: turn, degrees");
        CigarTurn = c.CreateEntry("CigarMouthTurn3", "5 45 10", "Сигара у рта: поворот, градусы / cigar at the mouth: turn, degrees");
        PipeTurnAt = c.CreateEntry("PipeMouthTurn3", "-5 -120 -10", "Трубка во рту: поворот, градусы / pipe in the mouth: turn, degrees");
        FollowView = c.CreateEntry("MouthFollowsView", true, "Сигарета/сигара у рта и трубка во рту двигаются вместе со взглядом вверх/вниз / smoke at the mouth follows looking up and down");
        FollowAmount = c.CreateEntry("MouthFollowsViewAmount2", 1f, "Насколько поза у рта следует за наклоном взгляда (1 = полностью, 0 = нет, минус = в другую сторону); клавиши = и - / how much the pose follows the look's pitch; = and - keys");
        LockCamera = c.CreateEntry("LockHandsCamera", true, "Пока куришь, камера рук не наклоняется со взглядом: рука и курево стоят на экране неподвижно / while smoking the hands camera does not tilt with the look, so the hand stays still on screen");
        GameView = c.CreateEntry("LockByGameView", true, "Закреплять по матрице, которой игра рисует руки (если она показывает сигарету на экране) / lock by the game's own hands view-projection");
        CarryArms = c.CreateEntry("CarryArmsWithView", true, "Поворачивать всю руку от плеча вместе со взглядом, пока куришь / turn the whole arms frame with the look while smoking");
        ViewPivot = c.CreateEntry("ViewPivotHeight", .12f, "Высота точки, вокруг которой наклоняется вид рук, над костью камеры, м; Shift + = / Shift + - / height of the point the hands view pitches about, m");
        PipeThumb = c.CreateEntry("PipeThumbOverStem", 45f, "Трубка в руке: большой палец поднимается над мундштуком, градусов на сустав (0 = как есть) / pipe in the hand: thumb raised over the stem, max degrees per joint");
        PipeThumbAbove = c.CreateEntry("PipeThumbAboveStem", .014f, "Трубка в руке: насколько выше оси мундштука лежит большой палец, м / pipe in the hand: thumb this far above the stem axis, m");
        PipeThumbBaseLift = c.CreateEntry("PipeThumbBaseLift", .008f, "Трубка в руке: нижняя часть большого пальца поднята выше, м / pipe in the hand: lower part of the thumb raised, m");
        PipeThumbClearance = c.CreateEntry("PipeThumbClearance", .016f, "Трубка в руке: большой палец обходит мундштук на таком расстоянии от его оси, м (0 = выкл.) / pipe in the hand: the thumb keeps this far from the stem axis, m");
        PipeThumbOverGap = c.CreateEntry("PipeThumbOverGap", .012f, "Трубка в руке: большой палец лежит над мундштуком, зазор от его поверхности до оси пальца, м / pipe in the hand: the thumb lies over the stem, gap from the stem surface to the thumb bone axis, m");
        CigNameToView = c.CreateEntry("CigaretteNameToView", true, "Сигарета повёрнута надписью марки к игроку / the cigarette is rolled so its brand faces the player");
        CigNameRoll = c.CreateEntry("CigaretteNameRoll", 0f, "Сигарета: доворот надписи вокруг оси, градусы (L в игре +30) / cigarette: extra roll of the brand about its axis, degrees (L in game +30)");
        CigarThumb = c.CreateEntry("CigarThumbWrap", 35f, "Большой палец ложится на сигару: на сколько градусов можно повернуть каждый сустав (0 = как есть) / thumb laid round the cigar: max turn per joint, degrees");
        DragHandBelow = c.CreateEntry("DragHandBelow3", "0 0 0", "При затяжке сигареты/сигары кисть ниже рта, за кадром, м (0 0 0 = держать у рта) / on a cigarette/cigar drag the hand stays this far below the mouth, out of view, m");
        DragSplit = c.CreateEntry("DragSplit", .5f, "Доля затяжки, когда рука уже внизу и к губам поднимается только сигарета / share of the move after which only the smoke rises to the lips");
        EyeHeight = c.CreateEntry("HandsEyeHeight2", 0f, "Высота глаза над костью камеры рук, м / hands eye above the rig's camera bone, m");
        EyePitch = c.CreateEntry("HandsEyePitch2", -9f, "Наклон взгляда камеры рук, градусы / hands eye pitch, degrees");
        Sounds = c.CreateEntry("Sounds2", true, "Звуки: обрезка сигары, затяжка, выдох / sounds: cigar cut, drag, exhale");
        SoundVolume = c.CreateEntry("SoundVolume", 1f, "Громкость звуков мода (0..2) / mod sound volume (0..2)");
        SmokesTab = c.CreateEntry("SmokesTab2", true, "Своя вкладка «Табак» в инвентаре / own «Tobacco» tab in the inventory");
        CigarPinchOn = c.CreateEntry("CigarPinched", false, "Сигара щепотью (иначе между пальцами, как сигарета) / cigar pinched (else between the fingers like a cigarette)");
        CigarSpread = c.CreateEntry("CigarFingerSpread", 9f, "Насколько раздвинуть указательный и средний под сигару, градусы каждый / index and middle spread for the cigar, degrees each");
        CigarJoint = c.CreateEntry("CigarGripJoint2", 1.0f, "Сигара между пальцами: 0 = у костяшек .. 3 = кончики / cigar between the fingers: 0 knuckles .. 3 tips");
        Markers = c.CreateEntry("CalibrationMarkers2", false, "Цветные точки для настройки вида рук первые 12 с курения / coloured calibration dots for the first 12 s of a smoke");
        PoseOff = c.CreateEntry("NoHandPose", false, "Не трогать руку (для проверки) / leave the hand alone (testing)");
        FlipCigarette = c.CreateEntry("FlipCigarette", false, "Развернуть сигарету в пальцах другим концом / turn the cigarette the other way in the fingers");
        ThrowSpeed = c.CreateEntry("ThrowSpeed", 3.2f, "Скорость броска окурка, м/с / thrown butt speed, m/s");
        PipeRot = c.CreateEntry("PipeRotation", "-90 0 0", "Трубка: поворот / held pipe rotation");
        PropScale = c.CreateEntry("HeldScale", 1f, "Размер в руке / held scale");
        FingerOpen = c.CreateEntry("FingerOpen", .85f, "Насколько раскрыть указательный и средний из кулака (0..1) / how far index and middle open from the fist");
        BareHands = c.CreateEntry("BareHandsWhileSmoking", true, "Снимать перчатки на время курения / gloves off while smoking");
        MouthAt = c.CreateEntry("MouthAtScreen5", "0.6 0.42 0.9", "Рот на экране рук: x y (0..1) и доля расстояния от руки в покое / mouth on the hands' view: x y (0..1), share of the resting hand's distance");
        CigMouthDir = c.CreateEntry("CigaretteInMouthDir", "0.1 -0.05 1", "Сигарета у рта, горящий конец: вправо/вверх/вперёд / cigarette at the mouth, lit end: right, up, forward");
        CigMouthUp = c.CreateEntry("FingersAtMouth", "-0.35 1 0", "Пальцы у рта смотрят: вправо/вверх/вперёд / fingers at the mouth point: right, up, forward");
        PipeHandAway = c.CreateEntry("PipeHandAway2", "0.3 -0.85 -0.05", "Куда уходит рука, пока трубка в зубах и при убирании (вправо, вверх, вперёд от глаза), м / where the hand goes while the pipe is in the teeth and when it is put away");
        CigInView = c.CreateEntry("CigaretteAtMouthOnScreen6", "0.54 0.00 0.21", "Затяжка: где на экране фильтр сигареты у рта: x y (0..1) и расстояние, м / drag: filter on screen x y (0..1) and distance m");
        CigarInView = c.CreateEntry("CigarAtMouthOnScreen4", "0.5 -0.12 0.19", "Затяжка: где на экране кончик сигары у рта / drag: cigar mouth end on screen x y and distance m");
        CigAim = c.CreateEntry("CigaretteAimAtMouth", "10 -4", "Затяжка: куда смотрит горящий конец сигареты, градусы вверх и вправо от взгляда / drag: cigarette lit end, degrees up and right of the view");
        CigarAim = c.CreateEntry("CigarAimAtMouth", "9 -4", "Затяжка: куда смотрит горящий конец сигары, градусы вверх и вправо / drag: cigar lit end, degrees up and right of the view");
        PipeInView = c.CreateEntry("PipeInTeethOnScreen", "0.5 0.0 0.26", "Трубка в зубах: где на экране мундштук / pipe in the teeth: mouthpiece on screen x y and distance m");
        PipeMouthAt = c.CreateEntry("PipeInMouth4", "0.52 0.3 0.75", "Мундштук во рту: x y на экране рук (0..1) и расстояние, м / mouthpiece in the mouth: x y on the hands' view, distance m");
        PipeMouthDir = c.CreateEntry("PipeInMouthDir", "0.06 -0.18 1", "Трубка у рта, к чаше: вправо/вверх/вперёд / pipe at the mouth, towards the bowl: right, up, forward");
        DebugLog = c.CreateEntry("DebugLog", true, "Подробный лог / verbose log");
        GiveLightersKey = c.CreateEntry("GiveLightersKey", true, "Ctrl+Shift+L: все 29 зажигалок, бензин и газ в рюкзак / all 29 lighters, fluid and butane into the pack");
        TestKeys = c.CreateEntry("TestKeys2", false, "Тест: Ctrl+Shift+F9 все предметы в рюкзак, Ctrl+Shift+F10 перебор звуков резки / test: Ctrl+Shift+F9 all items into the pack, Ctrl+Shift+F10 cycles cut sounds");
    }

    public static Vector3 Vec(MelonPreferences_Entry<string> e)
    {
        var p = e.Value.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        float F(int i) => i < p.Length && float.TryParse(p[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : 0f;
        return new Vector3(F(0), F(1), F(2));
    }
}
