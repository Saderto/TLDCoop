using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TLDCoop.Game
{
    // Вещь в руках другого игрока. Модель берём из самой игры (тот же префаб, что лежит на земле),
    // снимаем с неё всё, что думает (скрипты, физику, коллайдеры), и оставляем только сетку, свет и огонь.
    // Как держать, задано правилом для каждого вида вещей (точки ручек сняты с моделей игры): где вещь
    // относительно тела (у груди, у бедра, у плеча в прицел) и за какие места её берут кисти. Вещь ставится
    // от тела, а руки персонажа WINTERMUTE тянутся к её ручкам и обхватывают их (Game/Characters.cs, Grip).
    public static class HeldItems
    {
        public const byte FlagLit = 1, FlagAim = 2, FlagMouth = 4;   // FlagMouth: трубка в зубах, рука опущена (SmokesBySeagull)
        public const byte FlagCrank = 8;   // крутит ручку фонаря TLDCrankLamp (Game/CrankLampLink.cs)

        private static readonly Dictionary<string, GameObject> Templates = new();
        // Не нашлась модель: до какого времени не спрашиваем снова (float.MaxValue — вещь игре неизвестна совсем).
        private static readonly Dictionary<string, float> Missing = new();
        private const float MissingRetry = 30f;
        // Модели, которые ещё грузятся из Addressables (без ожидания: WaitForCompletion при загрузке сцены вешает игру).
        private static readonly Dictionary<string, UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject>> Loads = new();

        private static void MarkMissing(string prefab, bool forever = false) =>
            Missing[prefab] = forever ? float.MaxValue : Time.realtimeSinceStartup + MissingRetry;

        private static bool IsMissing(string prefab)
        {
            if (!Missing.TryGetValue(prefab, out var until)) return false;
            if (Time.realtimeSinceStartup < until) return true;
            Missing.Remove(prefab);   // срок вышел — пробуем ещё раз
            return false;
        }

        private static GameObject _holder;

        // Ручка: точка вещи, ось ручки (от указательного к мизинцу) и куда смотрит ладонь (от кисти к ручке),
        // всё в осях модели вещи.
        public class GripDef
        {
            public bool Right = true;
            public Vector3 At, Hole, Palm;
            public GripDef(bool right, Vector3 at, Vector3 hole, Vector3 palm) { Right = right; At = at; Hole = hole.normalized; Palm = palm.normalized; }
        }

        // Рукоять, которую обхватывают пальцы правой (CharacterAnimator.WrapFingers): эллиптический цилиндр в осях модели —
        // центр, ось вдоль рукояти, направление к правой щёчке, полуоси вбок и вперёд-назад; Taper — насколько полуось
        // вперёд-назад растёт (и центр уходит вперёд) на метр вдоль оси.
        public class GripShape
        {
            public Vector3 Center, Axis, Side;
            public float HalfSide, HalfFront, Taper;
        }

        // Где вещь относительно тела: точка от кости (в осях персонажа: x вправо, y вверх, z вперёд), куда смотрит
        // вещь (рысканье вправо +, тангаж вверх +), крен, доворот корпуса. Place — какая точка вещи ставится
        // в эту точку (по умолчанию первая ручка).
        public class Stance
        {
            public CharacterAnimator.Anchor Anchor = CharacterAnimator.Anchor.ShoulderR;
            public Vector3 Off;
            public float Yaw, Pitch, Roll, Twist;
            public Vector3? Place, Up;
            public bool OneHand;   // только первая ручка (второй рукой берётся, когда целится)
            // Одной рукой в прицел: рука вытянута от плеча — рысканье (вправо +) и подъём от взгляда, сгиб локтя.
            // Вещь тогда ставится по кисти, а не по Off (CharacterAnimator.WristShift).
            public float ArmYaw, ArmUp, ArmBend;
            // Одной рукой у бедра: плечо висит вдоль Hang (в осях персонажа), запястье прямое, вещь ставится по кисти,
            // а не по Off (CharacterAnimator.HangShift). Ствол за взглядом — от PitchMin (вверх) до PitchMax (вниз),
            // в приседе вниз не больше PitchMaxCrouch (иначе ствол уходил в колено).
            public Vector3? Hang;
            public float PitchMin = -60f, PitchMax = 60f, PitchMaxCrouch = 60f;
        }

        public class Rule
        {
            public Vector3 Fwd = Vector3.forward, Up = Vector3.up;   // оси модели вещи: куда она «смотрит» и где у неё верх
            public float Scale = 1f;
            public List<GripDef> Grips = new();
            public Stance Relax, Aim;
            public Stance Mouth;    // вещь во рту без руки (трубка в зубах): рука опущена, вещь у рта
            public float AimTime = 0.2f;   // за сколько секунд вещь переходит в позу Aim и обратно
            public bool Pinch;      // спичка: щепотью, а не в кулаке
            public bool Scissor;    // сигарета, сигара: между указательным и средним (вместе с Pinch)
            // Куда смотрит правый локоть (в осях персонажа) в покое и в позе Aim; null — как у всех вещей.
            public Vector3? ElbowRelax, ElbowAim, ElbowMid;   // ElbowMid: локоть в начале затяжки (как в 0.19.58)
            public float Loose, LooseThumb;   // камень: пальцы полусогнуты вокруг него (доля от кулака к прямым)
            public float LooseLeft, LooseLeftThumb;   // то же для левой (ружьё: цевьё лежит на полураскрытой ладони)
            public bool Trigger;    // указательный правой руки на спуске (полувыпрямлен)
            public bool Break;      // переломное ружьё (дробовик): при зарядке ствол вниз, левая вкладывает патрон в казённик
            public Vector3? Bolt;   // рукоять затвора (в осях модели): после выстрела правая рука передёргивает его
            public Vector3? Muzzle; // дуло (в осях модели): там вспышка и дым выстрела (Game/MuzzleFx.cs)
            public Vector3? Port;   // окно затвора: туда правая вкладывает патроны при зарядке
            public Vector3? TriggerAt;   // спуск: туда кончик указательного правой
            public float PalmWeight = 0.3f;   // насколько строго держать ладонь, как сказано в ручке (подбор поворота кисти)
            public Stance Reload;   // поза при зарядке (нет — от позы в прицел, стволом вниз)
            public bool AutoAxis;   // длинная ось по модели, горящим концом (свет/искры) вперёд
            public float GripAt;    // >0: ручка на этой доле длины от нижнего конца (считается по модели)
            public bool Recoil;     // ружьё, револьвер: отдача при выстреле
            public bool Cheek;      // в прицел — щекой к прикладу
            public GripShape Shape; // рукоять для пальцев (револьвер): пальцы обхватывают её, а не сжимаются кулаком сквозь неё
            public bool NoRoll;     // правая не поворачивается вокруг ручки (ладонь ровно по ручке): пальцы лежат по Shape
            public Bounds? Ref;     // размеры модели, с которой сняты точки: у похожих вещей (другие ружья, луки) точки растягиваются по их размеру
            public string Name = "";
            // Руки по анимации людей из игры (у пояса и в прицел), вещь ставится по правой кисти (CharacterAnimator.HoldClip).
            public string ArmsClip, ArmsAimClip;
            // Ружьё в прицел по клипу: центр затыльника (в осях модели) встаёт в ямку правого плеча, а правый глаз — на
            // линию прицела (SightY — её высота в осях модели). Нет — ружьё по правой кисти из клипа.
            public Vector3? Butt;
            public float SightY;

            public bool HasRight => Grips.Exists(g => g.Right);
            public bool HasLeft => Grips.Exists(g => !g.Right);
        }

        private static Bounds Box(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            var b = new Bounds();
            b.SetMinMax(new Vector3(x0, y0, z0), new Vector3(x1, y1, z1));
            return b;
        }

        private static Stance At(CharacterAnimator.Anchor a, float x, float y, float z, float yaw, float pitch, float twist = 0f) =>
            new Stance { Anchor = a, Off = new Vector3(x, y, z), Yaw = yaw, Pitch = pitch, Twist = twist };

        private const CharacterAnimator.Anchor ShR = CharacterAnimator.Anchor.ShoulderR, ShL = CharacterAnimator.Anchor.ShoulderL,
                                               HipR = CharacterAnimator.Anchor.HipR, HipL = CharacterAnimator.Anchor.HipL;

        // Небольшая вещь в кулаке перед собой (камень, баллончик, неизвестное).
        private static Stance InFront(float pitch) => At(ShR, 0f, -0.35f, 0.28f, 0f, pitch);

        // ---------- Своя вещь ----------

        public static byte Shots;   // счётчики, уходят с позицией: младшие 6 бит — выстрелы (и удары в схватке), старшие 2 — броски

        public static void OnThrow() => Shots = (byte)((Shots + 0x40) & 0xFF);
        private static bool _stoneAimLogged;
        public static bool StoneAim;   // замахнулся камнем (StoneItem.ZoomStart … ZoomEnd / бросок)
        public static void OnShot() => Shots = (byte)((Shots & 0xC0) | ((Shots + 1) & 0x3F));

        public static string Current(out byte flags)
        {
            flags = 0;
            // 0.19.34 (просьба игрока): пока ест или пьёт, в руке у фигуры ничего нет — видна только анимация
            // (раньше в руке было то, что он ест). Пока делает укол — тоже без шприца: рука идёт к бедру сама.
            if (Activity.Consume != 0) return "";
            if (Activity.Current() == Activity.Injecting) return "";
            try
            {
                // В схватке в руке то, чем отбивается (нож, топорик, монтировка), или ничего — голыми руками.
                var st = GameManager.GetPlayerStruggleComponent();
                if (st != null && st.m_Active && st.InStruggle())
                {
                    var w = st.m_WeaponUsed;
                    return w != null ? ItemSync.PrefabName(w) : "";
                }
                var gi = GameManager.GetPlayerManagerComponent()?.m_ItemInHands;
                if (gi == null) return "";
                if (gi.IsLitLightsource()) flags |= FlagLit;
                // Курит (SmokesBySeagull): затяжка — рука у рта (как «целится»), трубка в зубах — рука опущена.
                string pn = ItemSync.PrefabName(gi);
                if (SmokesLink.IsLitSmoke(pn))
                {
                    int ph = SmokesLink.LocalPhase();
                    flags |= FlagLit;
                    if (ph == 1) flags |= FlagAim; else if (ph == 2) flags |= FlagMouth;
                    return pn;
                }
                if (CrankLampLink.IsCrankLamp(pn) && CrankLampLink.LocalCranking()) flags |= FlagCrank;
                var gun = gi.m_GunItem;
                if (gun != null && gun.IsAiming()) flags |= FlagAim;
                var bow = gi.m_BowItem;
                if (bow != null && (bow.m_BowState == BowState.Aim || bow.m_BowState == BowState.ReadyToAim || bow.m_BowState == BowState.Firing)) flags |= FlagAim;
                // Замах камнем: одних ZoomStart/ZoomEnd мало (у друга замаха не было видно) — спрашиваем и анимацию рук.
                if (gi.m_StoneItem != null)
                {
                    bool aim = StoneAim;
                    try { aim |= GameManager.GetPlayerAnimationComponent()?.IsAiming() == true; } catch { }
                    try { aim |= GameManager.GetPlayerManagerComponent().PlayerIsZooming(); } catch { }
                    if (aim) flags |= FlagAim;
                    if (aim && !_stoneAimLogged) { _stoneAimLogged = true; MelonLogger.Msg($"[вещь] замах камнем виден другим (ZoomStart {StoneAim})"); }
                }
                else StoneAim = false;
                return ItemSync.PrefabName(gi);
            }
            catch { return ""; }
        }

        // ---------- Правила ----------

        public static Rule RuleFor(string prefab, GameObject template)
        {
            string n = prefab ?? "";
            bool junk = n.Contains("Ammo") || n.Contains("Casing") || n.Contains("Cleaning") || n.Contains("Box");
            var r = Make(n, junk, template);
            r.Name = n;
            return r;
        }

        private static Rule Make(string n, bool junk, GameObject template)
        {
            if (!junk && (n.StartsWith("GEAR_Rifle") || n == "GEAR_Shotgun"))
            {
                // Ружьё: правая на шейке приклада, левая на цевье. Без прицела — наискосок у груди стволом
                // вверх-влево, в прицел — прикладом в плечо, корпус довёрнут вправо, щекой к прикладу.
                // Все ружья из DLC (Barbs, Trader, Curators, Vaughns) сделаны на той же основе: спуск, скоба, шейка
                // и цевьё у них на тех же местах (сверено по моделям, scratchpad dlc/solid.png), отличаются магазин
                // и приклад. Поэтому точки не растягиваем по размерам модели (у Trader магазин выше — растяжка
                // сдвигала руку на сантиметр).
                // Пальцы разжаты: шейка 3.4×6.6 см, сжатый кулак насквозь уходил в дерево (половина кисти внутри
                // ружья, считал на макете hold2/grip_eval.py по всем восьми моделям). Указательный сгибается ровно
                // настолько, чтобы кончик лёг на спуск, — под длину пальца каждой модели (CharacterAnimator.AimIndex).
                var r = new Rule { Fwd = Vector3.back, Up = Vector3.up, Scale = 0.8f, Recoil = true, Cheek = true,
                                   Loose = 0.5f, LooseThumb = 0.3f, LooseLeft = 0.3f, LooseLeftThumb = 0.6f };
                // Правая — на шейке сразу за скобой, ось кисти круто вниз, как у пистолетной рукояти; в 0.17.16 на 3 см
                // ближе к дулу и на 2 см ниже (было «рука в 10 см за спуском»), чуть правее середины шейки.
                r.Trigger = true;
                r.TriggerAt = new Vector3(0f, 0.175f, 0.235f);   // одинаково у обычного ружья и у всех из DLC (Barbs, Trader, Curators, Vaughns)
                r.Bolt = new Vector3(-0.052f, 0.234f, 0.215f);
                r.Port = new Vector3(-0.01f, 0.26f, 0.12f);
                r.Muzzle = new Vector3(0f, 0.27f, -0.717f);
                r.PalmWeight = 3f;   // ладонью строго слева от шейки, иначе подбор кисти разворачивал её и палец уходил от спуска
                r.Grips.Add(new GripDef(true, new Vector3(-0.01f, 0.18f, 0.31f), new Vector3(0f, -0.85f, 0.5f), Vector3.right));
                // Левая — под цевьём, на 4 см ближе к дулу, чем в 0.17.15. Точка кулака на 2–3 см выше нижнего края
                // цевья: при полуразжатых пальцах она над ладонью, и в 0.17.16 ладонь висела в 3–4 см под цевьём.
                // В 0.18.4 ещё на 11 см ближе к дулу: кисть была почти у магазина.
                r.Grips.Add(new GripDef(false, new Vector3(0.005f, 0.222f, -0.15f), new Vector3(-0.7f, 0f, 0.7f).normalized, new Vector3(-0.5f, 0.85f, 0f)));
                // Наискосок перед собой, приклад сбоку от правого бедра, а не перед ним (в 0.17.13 приклад уходил
                // в бедро и полы куртки): ствол меньше влево (−30°) и ниже (20°), кулак правее. В 0.17.17 ещё на 5 см
                // правее и на 7 см вперёд: у Джейс запястье уходило в куртку.
                // В 0.18.4 ствол сильнее влево (−48° вместо −30°): левая кисть была правее середины груди, и рука
                // шла через туловище. Теперь кисть на 10 см левее и дотягивается у всех восьми моделей (hold2/rifle_x.py).
                r.Relax = At(HipR, 0.12f, 0.175f, 0.30f, -48f, 22f, 10f);
                r.Aim = At(ShR, -0.05f, -0.03f, 0.07f, -3f, 0f, 35f);
                r.Aim.Place = new Vector3(0f, 0.12f, 0.78f);
                // Зарядка: ружьё перед животом почти горизонтально, приклад сбоку под правой рукой, левая держит цевьё,
                // правая открывает затвор и по одному вкладывает патроны в окно.
                r.Reload = At(HipR, 0.02f, 0.24f, 0.25f, -30f, 5f, 10f);
                // 0.19.3: руки — из клипов rifle / rifle_aim (люди из WINTERMUTE так держали ружьё): локти и кисти
                // естественные, ружьё ставится по правой кисти и чуть доворачивается в левую ладонь. Позы Relax/Aim
                // выше остаются для сидя и запасными.
                r.ArmsClip = "rifle"; r.ArmsAimClip = "rifle_aim";
                // 0.19.4: в прицел ружьё ставится затыльником в плечо, а не по кисти из клипа: у кукол WINTERMUTE кисть
                // в rifle_aim у самого лица, и приклад ложился на верх плеча и в щёку (по макету затыльник был внутри
                // плечевого сустава, глаз на 2–6 см ниже прицела, голова внутри приклада). Затыльник — по сетке модели
                // (плита z 0,78–0,80, y −0,01…0,22), линия прицела — верх коробки и мушка (y ≈ 0,30).
                r.Butt = new Vector3(0f, 0.105f, 0.79f);
                r.SightY = 0.30f;
                if (n == "GEAR_Shotgun")
                {
                    // 0.19.32: дробовик из мода TLDShotgun (одноствольный переломный из WINTERMUTE). Модель той же основы,
                    // что и ружьё: ствол по −Z, дуло z −0,71, затыльник z 0,79 (замерено по сетке, scratchpad shotgun.obj).
                    // Отличия: ствол выше (ось y ≈ 0,28), спуск в скобе у z 0,20, шейка у z 0,31 (y 0,17–0,26), цевьё
                    // снизу y 0,22 на z −0,40…0, затыльник y 0,01–0,22. Затвора нет: «окно» — место перелома у z 0,05.
                    r.TriggerAt = new Vector3(0f, 0.175f, 0.21f);
                    r.Grips[0] = new GripDef(true, new Vector3(-0.01f, 0.20f, 0.31f), new Vector3(0f, -0.85f, 0.5f), Vector3.right);
                    r.Grips[1] = new GripDef(false, new Vector3(0.005f, 0.232f, -0.15f), new Vector3(-0.7f, 0f, 0.7f).normalized, new Vector3(-0.5f, 0.85f, 0f));
                    r.Muzzle = new Vector3(0f, 0.283f, -0.708f);
                    // 0.19.33: затвора нет — после выстрела рука его не дёргает; зарядка переломом (Break): ружьё
                    // клонится стволом вниз вокруг правой руки, левая несёт патрон из кармана в казённик (Port).
                    r.Bolt = null;
                    r.Break = true;
                    r.Port = new Vector3(0f, 0.28f, 0.05f);
                    // 0.19.58: своя поза зарядки — у правого бедра, стволом вперёд-вниз на 30° (у винтовки ствол почти
                    // ровно влево-вперёд: с ней дробовик путали). Перелом добавляет ещё до 25° вокруг правой руки.
                    r.Reload = At(HipR, 0.02f, 0.20f, 0.27f, -15f, -30f, 10f);
                    r.Butt = new Vector3(0f, 0.115f, 0.79f);
                    r.SightY = 0.305f;
                }
                return r;
            }
            if (!junk && (n.StartsWith("GEAR_Revolver") || n.StartsWith("GEAR_FlareGun")))
            {
                // Револьвер и ракетница — одной правой рукой. У бедра: кисть на уровне пояса, ствол вперёд, куда
                // повёрнут персонаж (раньше смотрел в землю). В прицел: рука вытянута вперёд от правого плеча.
                // Левая берётся только при зарядке: от кармана к барабану, по патрону (раньше левая и у бедра
                // тянулась к рукояти через туловище, а в прицеле держала двумя руками).
                // У модели ствол смотрит по -Z, верх — по +X, правый бок — по +Y.
                var r = new Rule { Fwd = Vector3.back, Up = Vector3.right, Recoil = true, Trigger = true,
                                   Loose = 0.5f, LooseThumb = 0.3f, PalmWeight = 3f, NoRoll = true,
                                   Ref = n == "GEAR_Revolver" ? null : Box(-0.127f, -0.013f, -0.269f, 0.097f, 0.055f, 0.177f) };
                var hole = new Vector3(-0.84f, 0f, 0.54f);
                r.Muzzle = new Vector3(0.06f, 0.02f, -0.27f);
                r.Port = new Vector3(0.035f, 0.045f, 0.03f);   // дверца барабана справа сзади
                r.TriggerAt = new Vector3(-0.04f, 0.021f, 0.015f);   // спуск внутри скобы под задним краем барабана (gearpts, rev_side.png)
                // 0.19.4: правая держит рукоять, а не сжимает кулак сквозь неё. Рукоять большая (4,3 см в ширину, 7–9 см
                // спереди назад), кулак из анимации её не обхватывает. Теперь хват высокий: ладонь на правой щёчке и за спинкой
                // рукояти, перепонка под курком, большой палец идёт через верх рукояти и ложится на левый бок рамки (WrapFingers),
                // средний, безымянный и мизинец обхватывают рукоять спереди (WrapFingers по Shape), указательный на спуске
                // (AimIndex). Кисть вокруг ручки не поворачивается (NoRoll). Подобрано на макете по всем восьми моделям
                // (hold4-rev/v2: fit4.py, runfit4.py): внутри рукояти ладонь 0%, основания пальцев 9–13%, большой 0–6% вершин,
                // кончики среднего, безымянного и мизинца — на левой щёчке (было: кулак насквозь, 26–51% кисти; в первой
                // версии 0.19.4 большой уходил в рамку на 30–47%).
                r.Grips.Add(new GripDef(true, new Vector3(-0.0339f, 0.0346f, 0.0912f), new Vector3(-0.999f, -0.04f, 0.016f), new Vector3(0.04f, -0.998f, 0.046f)));
                // У ракетницы рукоять того же размера, но скоба ниже и рукоять прямая (без расширения книзу): кисть на 2 см ниже
                // и чуть повёрнута (подобрано так же, runfit5.py: ладонь 0%, основания пальцев 4–15%, большой 2–11%).
                if (n.StartsWith("GEAR_FlareGun"))
                    r.Grips[0] = new GripDef(true, new Vector3(-0.0524f, 0.0357f, 0.1063f), new Vector3(-0.991f, -0.047f, 0.126f), new Vector3(0.06f, -0.992f, 0.11f));
                r.Grips.Add(new GripDef(false, new Vector3(-0.085f, -0.03f, 0.105f), hole, Vector3.up));
                r.Shape = new GripShape { Center = new Vector3(-0.065f, 0.021f, 0.113f), Axis = hole.normalized, Side = Vector3.up,
                                          HalfSide = 0.0215f, HalfFront = 0.0405f, Taper = 0.2f };
                // У бедра: ствол на 12° вниз и за взглядом (вверх до 25°, вниз до 60°, в приседе вниз до 15°), а кисть — по руке:
                // плечо висит чуть вперёд и наружу, предплечье по кисти, запястье прямое (HangShift). Точка от бедра — только
                // если руки нет. Раньше кисть тянулась к точке от бедра: глядя вниз, не доставала, запястье ломалось на 25–40°.
                r.Relax = At(HipR, 0.05f, 0.15f, 0.33f, 0f, -12f);
                r.Relax.OneHand = true;
                r.Relax.Hang = new Vector3(0.28f, -1f, 0.1f);
                r.Relax.PitchMin = -25f; r.Relax.PitchMaxCrouch = 15f;
                // Чуть завален верхом внутрь (20°): при прямом запястье кисть иначе повёрнута против предплечья на 45–65°, теперь 10–45°.
                r.Relax.Roll = 20f;
                // В прицел: рука от правого плеча вперёд, на 14° выше взгляда (револьвер между плечом и глазом, на высоте
                // подбородка), локоть согнут на 12° вниз-наружу. Корпус не доворачиваем: в 0.19.3 доворот на 15° вправо и
                // револьвер в 6 см левее плеча уводили руку через грудь.
                r.Aim = At(ShR, -0.06f, -0.04f, 0.52f, -1f, 0f);
                r.Aim.OneHand = true;
                r.Aim.ArmYaw = 0f; r.Aim.ArmUp = 14f; r.Aim.ArmBend = 12f;
                // Зарядка: револьвер перед грудью стволом вниз и влево, правым боком (дверцей) вверх.
                r.Reload = At(ShR, -0.18f, -0.30f, 0.30f, -35f, -40f);
                r.Reload.Roll = -60f;
                return r;
            }
            if (n.StartsWith("GEAR_Flashlight"))
            {
                // Фонарик: в кулаке за корпус, перед собой на уровне пояса, светит вперёд и чуть вниз.
                var r = new Rule { Fwd = Vector3.forward, Up = Vector3.up };
                r.Grips.Add(new GripDef(true, new Vector3(0f, 0.03f, -0.09f), Vector3.back, Vector3.left));
                r.Relax = At(ShR, -0.02f, -0.30f, 0.32f, -8f, -15f);
                return r;
            }
            if (n == "GEAR_Torch")
            {
                // Факел: поднят перед собой справа, огнём вверх и вперёд.
                var r = new Rule { Fwd = Vector3.right, Up = Vector3.up };
                r.Grips.Add(new GripDef(true, new Vector3(-0.22f, 0.03f, 0f), Vector3.left, Vector3.forward));
                r.Relax = At(ShR, 0.05f, -0.15f, 0.28f, 10f, 55f);
                return r;
            }
            if (n == "GEAR_FlareA" || n == "GEAR_BlueFlare")
            {
                var r = new Rule { Fwd = Vector3.right, AutoAxis = true, GripAt = 0.3f };
                r.Relax = At(ShR, 0.05f, -0.12f, 0.30f, 10f, 60f);
                return r;
            }
            if (SmokesLink.IsLitSmoke(n))
            {
                // Сигарета, сигара, трубка (SmokesBySeagull): модель «для руки» — начало у фильтра/мундштука, +Z к горящему
                // концу. Сигарета и сигара в пальцах щепотью у груди, на затяжке фильтром ко рту; трубка в кулаке за чашку,
                // во рту — мундштуком в зубах, чашкой вперёд и вниз.
                bool pipe = n.Contains("LitPipe");
                float len = SmokesLink.Length(n);
                if (len < 0.02f) len = pipe ? 0.13f : n.Contains("LitCigar") ? 0.14f : 0.085f;
                var r = new Rule { Fwd = Vector3.forward, Up = Vector3.up, Pinch = !pipe, AimTime = 0.45f, Name = "smoke" };
                r.Grips.Add(new GripDef(true, new Vector3(0f, pipe ? 0.01f : 0f, len * (pipe ? 0.78f : 0.3f)), Vector3.back, Vector3.down));
                // 11-10 (пользователь): рука у живота держала сигарету в куртку, на затяжке локоть уходил в туловище.
                // Покой: кисть дальше вперёд и чуть наружу, огонёк от тела; затяжка: фильтр у губ, локоть вниз-наружу-вперёд.
                // 11-10 (2): elbow out to the side looked like a bent wing → elbow hangs down by the body, hand in front of the belly
                // 11-10 (3): локоть задирался вперёд до плеча → рука опущена вдоль тела, локоть чуть согнут назад, кисть у бедра
                r.Relax = pipe ? At(ShR, -0.06f, -0.30f, 0.24f, -10f, 0f) : At(ShR, 0.04f, -0.50f, 0.14f, 20f, -10f);
                r.Aim = At(ShR, -0.17f, 0.15f, 0.15f, 12f, -12f);
                r.Scissor = !pipe;
                if (!pipe) { r.ElbowRelax = new Vector3(0.3f, -0.2f, -1f); r.ElbowMid = new Vector3(0.25f, -1f, -0.2f); r.ElbowAim = new Vector3(1f, -0.8f, 0.35f); }   // затяжка как в 0.19.55: понравилась пользователю, не трогать
                r.Aim.Place = Vector3.zero;
                if (pipe)
                {
                    r.Mouth = At(ShR, -0.17f, 0.18f, 0.12f, 4f, -18f);   // 11-10: было на 3 см ниже — торчала из подбородка
                    r.Mouth.Place = Vector3.zero;
                }
                return r;
            }
            if (IsMatches(n))
            {
                // Спичка: щепотью перед собой на уровне груди, огоньком вверх (у модели палочка идёт по +Y).
                var r = new Rule { Fwd = Vector3.up, Up = Vector3.forward, Pinch = true };
                r.Grips.Add(new GripDef(true, new Vector3(0f, 0.012f, 0f), Vector3.down, Vector3.back));
                r.Relax = At(ShR, -0.05f, -0.30f, 0.30f, 0f, 70f);
                return r;
            }
            if (n.StartsWith("GEAR_KeroseneLamp") || n.Contains("Lantern"))
            {
                // Лампа: за дужку, висит перед собой.
                var r = new Rule { Fwd = Vector3.forward, Up = Vector3.up,
                                   Ref = n == "GEAR_KeroseneLampB" ? null : Box(-0.071f, 0.001f, -0.081f, 0.071f, 0.303f, 0.08f) };
                r.Grips.Add(new GripDef(true, new Vector3(0f, 0.29f, 0f), Vector3.right, Vector3.down));
                r.Relax = At(ShR, 0f, -0.35f, 0.30f, 0f, 0f);
                return r;
            }
            if (n.StartsWith("GEAR_Bow") && !n.Contains("String"))
            {
                // Лук: левой рукой за рукоять. Опущен у бедра; в прицел — левая вытянута вперёд, правая тянет тетиву
                // к лицу, корпус боком. У модели тетива с -X, стрела летит по +X.
                var r = new Rule { Fwd = Vector3.right, Up = Vector3.up,
                                   Ref = n == "GEAR_Bow" ? null : Box(-0.087f, -0.633f, -0.017f, 0.191f, 0.695f, 0.016f) };
                r.Grips.Add(new GripDef(false, new Vector3(0.165f, 0f, 0f), Vector3.down, Vector3.right));
                r.Grips.Add(new GripDef(true, new Vector3(-0.40f, 0f, 0f), Vector3.down, Vector3.left));
                r.Relax = At(HipL, -0.08f, 0.15f, 0.12f, 0f, -10f);
                r.Relax.Up = new Vector3(0f, 1f, 0.3f);
                r.Relax.OneHand = true;
                r.Aim = At(ShL, 0.10f, -0.02f, 0.42f, -3f, 0f, 60f);
                r.Aim.Roll = -10f;
                return r;
            }
            if (n == "GEAR_Stone")
            {
                // Камень 11×8×8 см в кулак не помещается: при 0,7 и полусогнутых пальцах они всё равно шли сквозь
                // него. Теперь 0,55 (6×4,5×4,5 см) и пальцы раскрыты сильнее, камень лежит в согнутых пальцах.
                // Замах (правая кнопка): кисть над плечом и позади, корпус довёрнут.
                // В 0.18.4 камень сдвинут в ладонь (точка хвата — середина пальцев, камень был прямо в них), пальцы
                // согнуты сильнее и обхватывают его: пальцев внутри камня 2–3% вместо 13% (hold2/stone_x.py).
                var r = new Rule { Fwd = Vector3.right, Up = Vector3.up, Scale = 0.55f, Loose = 0.5f, LooseThumb = 0.4f };
                r.Grips.Add(new GripDef(true, new Vector3(0.071f, 0.012f, 0.006f), Vector3.back, Vector3.down));
                r.Relax = At(ShR, 0f, -0.40f, 0.25f, 0f, 0f);
                r.Aim = At(ShR, 0.15f, 0.14f, -0.20f, 0f, 60f, 25f);
                return r;
            }
            if (n == "GEAR_SprayPaintCan")
            {
                var r = new Rule { Fwd = Vector3.up, Up = Vector3.forward };
                r.Grips.Add(new GripDef(true, new Vector3(0f, 0.10f, 0f), Vector3.down, Vector3.left));
                r.Relax = InFront(70f);
                return r;
            }
            // Оружие схватки (если окажется в руке и стоя): опущено у бедра, остриём вперёд-вниз.
            if (n == "GEAR_Knife")
                return Weapon(new Vector3(-0.06f, 0.012f, 0f), Vector3.up, Vector3.up);
            if (n == "GEAR_Hatchet")
                return Weapon(new Vector3(-0.15f, 0f, -0.035f), Vector3.forward, Vector3.up);
            if (n == "GEAR_Prybar")
                return Weapon(new Vector3(-0.20f, 0.037f, -0.017f), Vector3.up, Vector3.forward);
            return Generic(template);
        }

        private static Rule Weapon(Vector3 grip, Vector3 up, Vector3 palm)
        {
            var r = new Rule { Fwd = Vector3.right, Up = up };
            r.Grips.Add(new GripDef(true, grip, Vector3.left, palm));
            r.Relax = At(HipR, 0.06f, -0.02f, 0.12f, 0f, -60f);
            return r;
        }

        // Точки сняты с одной модели, а вещь другая (другое ружьё, лук): растягиваем их по её размерам.
        private static Vector3 Fit(Vector3 p, Bounds from, Bounds to)
        {
            Vector3 a = from.min, sa = from.size, b = to.min, sb = to.size;
            float F(float v, float a0, float s0, float b0, float s1) => s0 > 1e-4f && s1 > 1e-4f ? b0 + (v - a0) / s0 * s1 : v;
            return new Vector3(F(p.x, a.x, sa.x, b.x, sb.x), F(p.y, a.y, sa.y, b.y, sb.y), F(p.z, a.z, sa.z, b.z, sb.z));
        }

        private static void Prepare(Rule r, GameObject template)
        {
            if (r.Ref.HasValue && Bounds(template, out var bb))
            {
                // Пистолеты (Shape: ракетница, другие револьверы): рукоять у моделей та же, разная длина ствола. Точки рукояти
                // и спуска — только в масштабе по высоте (по Up), иначе по длине ствола они уезжали на 3 см назад, а рукоять
                // ракетницы сжималась до 6 см вместо 7 — пальцы снова уходили внутрь. Ствол и дверца — по всей коробке.
                float hs = 1f;
                if (r.Shape != null)
                {
                    float h0 = Vector3.Scale(r.Ref.Value.size, r.Up).magnitude, h1 = Vector3.Scale(bb.size, r.Up).magnitude;
                    if (h0 > 1e-4f && h1 > 1e-4f) hs = h1 / h0;
                }
                foreach (var g in r.Grips) g.At = r.Shape != null ? g.At * hs : Fit(g.At, r.Ref.Value, bb);
                if (r.Bolt.HasValue) r.Bolt = Fit(r.Bolt.Value, r.Ref.Value, bb);
                if (r.Muzzle.HasValue) r.Muzzle = Fit(r.Muzzle.Value, r.Ref.Value, bb);
                if (r.Port.HasValue) r.Port = Fit(r.Port.Value, r.Ref.Value, bb);
                if (r.TriggerAt.HasValue) r.TriggerAt = r.Shape != null ? r.TriggerAt.Value * hs : Fit(r.TriggerAt.Value, r.Ref.Value, bb);
                foreach (var st in new[] { r.Relax, r.Aim, r.Mouth })
                    if (st != null && st.Place.HasValue) st.Place = Fit(st.Place.Value, r.Ref.Value, bb);
                if (r.Shape != null)
                {
                    r.Shape.Center *= hs; r.Shape.HalfSide *= hs; r.Shape.HalfFront *= hs;
                }
            }
            if (r.GripAt > 0f && Bounds(template, out var b)) GripByLength(r, template, b);
            r.Relax ??= InFront(70f);
        }

        // Держим за нижнюю часть, а не за самый конец: в кулаке середина, снизу видно ручку.
        private static void GripByLength(Rule r, GameObject template, Bounds b)
        {
            if (r.AutoAxis)
            {
                // Ось по модели: у фальшфейера она не X, и он торчал из кулака боком.
                var e = b.extents;
                Vector3 ax = e.x >= e.y && e.x >= e.z ? Vector3.right : e.y >= e.z ? Vector3.up : Vector3.forward;
                // Горящий конец — тот, ближе к которому огонь (свет, искры), он смотрит вперёд (вверх).
                Vector3? fire = null;
                var inv = template.transform.worldToLocalMatrix;
                foreach (var l in template.GetComponentsInChildren<Light>(true)) { fire = inv.MultiplyPoint3x4(l.transform.position); break; }
                if (fire == null) foreach (var p in template.GetComponentsInChildren<ParticleSystem>(true)) { fire = inv.MultiplyPoint3x4(p.transform.position); break; }
                if (fire.HasValue && Vector3.Dot(fire.Value - b.center, ax) < 0f) ax = -ax;
                r.Fwd = ax;
                r.Up = Mathf.Abs(ax.y) > 0.5f ? Vector3.forward : Vector3.up;
                if (!_axisLogged.Contains(template.name))
                {
                    _axisLogged.Add(template.name);
                    MelonLogger.Msg($"[вещи] {template.name}: длинная ось {ax}, длина {2f * Vector3.Dot(e, new Vector3(Mathf.Abs(ax.x), Mathf.Abs(ax.y), Mathf.Abs(ax.z))):0.00} м, огонь {(fire.HasValue ? "найден" : "не найден")}");
                }
            }
            var axis = r.Fwd.normalized;
            float half = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z))));
            var at = b.center + axis * (half * (2f * r.GripAt - 1f));
            r.Grips.Clear();
            r.Grips.Add(new GripDef(true, at, -axis, Vector3.Cross(axis, r.Up)));
        }

        private static readonly HashSet<string> _axisLogged = new();

        private static bool Bounds(GameObject template, out Bounds b)
        {
            b = new Bounds();
            if (template == null) return false;
            bool any = false;
            var inv = template.transform.worldToLocalMatrix;
            foreach (var mr in template.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = inv * mr.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(c);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return any;
        }

        // Неизвестная вещь: в кулаке перед собой, длинной стороной вверх; длинную держим ближе к одному краю.
        private static Rule Generic(GameObject template)
        {
            var r = new Rule();
            r.Relax = InFront(70f);
            if (!Bounds(template, out var b))
            {
                r.Grips.Add(new GripDef(true, Vector3.zero, Vector3.down, Vector3.left));
                return r;
            }
            var e = b.extents;
            Vector3 axis = e.x >= e.y && e.x >= e.z ? Vector3.right : e.y >= e.z ? Vector3.up : Vector3.forward;
            Vector3 side = axis == Vector3.up ? Vector3.forward : Vector3.up;
            float half = Vector3.Dot(e, axis);
            r.Fwd = axis;
            r.Up = side;
            var at = half > 0.12f ? b.center - axis * half * 0.6f : b.center;
            r.Grips.Add(new GripDef(true, at, -axis, Vector3.Cross(axis, side)));
            return r;
        }

        // ---------- Модели ----------

        public static bool IsMatches(string prefab) => prefab == "GEAR_PackMatches" || prefab == "GEAR_WoodMatches";

        // Спички: у игры в мире это коробок, а в руке горит одна спичка — её и собираем сами:
        // палочка, головка и огонёк со светом (огонёк и свет включаются, когда спичка горит).
        private static GameObject MatchTemplate()
        {
            var go = new GameObject("Held_Match");
            go.transform.SetParent(_holder.transform, false);
            GameObject Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color, bool glow)
            {
                var p = GameObject.CreatePrimitive(type);
                p.name = name;
                foreach (var c in p.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                p.transform.SetParent(go.transform, false);
                p.transform.localPosition = pos;
                p.transform.localScale = scale;
                var r = p.GetComponent<Renderer>();
                var m = r.material;
                m.hideFlags = HideFlags.DontUnloadUnusedAsset;
                m.color = color;
                if (glow)
                {
                    m.EnableKeyword("_EMISSION");
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 4f);
                }
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return p;
            }
            Part(PrimitiveType.Cube, "Stick", new Vector3(0f, 0.035f, 0f), new Vector3(0.004f, 0.07f, 0.004f), new Color(0.75f, 0.6f, 0.4f), false);
            Part(PrimitiveType.Sphere, "Head", new Vector3(0f, 0.072f, 0f), new Vector3(0.007f, 0.009f, 0.007f), new Color(0.35f, 0.08f, 0.05f), false);
            var flame = Part(PrimitiveType.Sphere, "FlameGlow", new Vector3(0f, 0.085f, 0f), new Vector3(0.011f, 0.024f, 0.011f), new Color(1f, 0.55f, 0.15f), true);
            flame.GetComponent<Renderer>().enabled = false;
            var light = new GameObject("MatchLight").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            light.type = LightType.Point;
            light.range = 3.5f;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.enabled = false;
            return go;
        }

        internal static GameObject Template(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return null;
            if (Templates.TryGetValue(prefab, out var t) && t != null) return t;
            if (IsMatches(prefab))
            {
                try
                {
                    if (_holder == null)
                    {
                        _holder = new GameObject("TLDCoop_HeldItems");
                        _holder.SetActive(false);
                        UnityEngine.Object.DontDestroyOnLoad(_holder);
                    }
                    return Templates[prefab] = MatchTemplate();
                }
                catch (Exception e) { MarkMissing(prefab); MelonLogger.Warning("[вещи] спичка: " + e.Message); return null; }
            }
            if (IsMissing(prefab)) return null;
            GameObject src = null;
            // Вещей FoodPackBySeagull (GEAR_Seagull_*) в Addressables нет: не спрашиваем, чтобы не сыпать ошибками в лог.
            if (!prefab.StartsWith("GEAR_Seagull_", StringComparison.Ordinal))
            {
                // Загрузку запускаем один раз и не ждём: пока грузится — null, Held.Set спросит в следующем кадре.
                if (!Loads.TryGetValue(prefab, out var h))
                {
                    try
                    {
                        var key = new Il2CppSystem.Object(IL2CPP.ManagedStringToIl2Cpp(prefab));
                        h = Addressables.LoadAssetAsync<GameObject>(key);
                        Loads[prefab] = h;
                    }
                    catch { h = default; }
                }
                if (Loads.ContainsKey(prefab))
                {
                    bool done;
                    try { done = h.IsDone; } catch { done = true; }
                    if (!done) return null;
                    Loads.Remove(prefab);
                    try
                    {
                        if (h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded) src = h.Result;
                    }
                    catch { src = null; }
                }
            }
            GearItem temp = null;
            try
            {
                if (src == null)
                {
                    // Вещь мода: игра создаёт её по имени (мод сам даёт модель и текстуру) — берём модель с временной копии.
                    temp = GearItem.InstantiateGearItem(prefab);
                    if (temp == null) { MarkMissing(prefab, true); return null; }
                    temp.gameObject.SetActive(false);   // чтобы её не увидели раскладки локаций и проверки
                    src = temp.gameObject;
                }
                if (_holder == null)
                {
                    _holder = new GameObject("TLDCoop_HeldItems");
                    _holder.SetActive(false);
                    UnityEngine.Object.DontDestroyOnLoad(_holder);
                }
                // Под выключенным родителем у копии не запускается ни один скрипт (Awake тоже), и мы успеваем всё снять.
                var go = UnityEngine.Object.Instantiate(src, _holder.transform);
                go.name = "Held_" + prefab;
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                Strip(go);
                SmokesLink.Look(go, prefab);   // сигарета/трубка SmokesBySeagull: модель для руки, огонёк на кончике
                go.SetActive(true);   // копия временной вещи выключена; показ всё равно решает выключенный _holder и Set()
                Templates[prefab] = go;
                return go;
            }
            catch (Exception e)
            {
                MarkMissing(prefab);
                MelonLogger.Warning($"[вещи] модель {prefab}: {e.Message}");
                return null;
            }
            finally
            {
                if (temp != null) { try { UnityEngine.Object.Destroy(temp.gameObject); } catch { } }
            }
        }

        // Копия настоящего предмета мира (например, волокуши с грузом перед тем, как её забрал другой игрок):
        // дальше показываем её, а не голый префаб.
        internal static void Remember(string key, GameObject src)
        {
            if (string.IsNullOrEmpty(key) || src == null) return;
            try
            {
                if (_holder == null)
                {
                    _holder = new GameObject("TLDCoop_HeldItems");
                    _holder.SetActive(false);
                    UnityEngine.Object.DontDestroyOnLoad(_holder);
                }
                var go = UnityEngine.Object.Instantiate(src, _holder.transform);
                go.name = "Held_" + key;
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                Strip(go);
                if (Templates.TryGetValue(key, out var old) && old != null) UnityEngine.Object.Destroy(old);
                Templates[key] = go;
                Missing.Remove(key);
            }
            catch (Exception e) { MelonLogger.Warning($"[вещи] копия {key}: {e.Message}"); }
        }

        private static readonly HashSet<string> Keep = new()
        {
            "Transform", "MeshFilter", "MeshRenderer", "SkinnedMeshRenderer", "Light", "ParticleSystem", "ParticleSystemRenderer", "LODGroup",
        };

        private static void Strip(GameObject go)
        {
            // Сначала скрипты (к ним могут быть привязаны требования на физику), потом всё остальное лишнее.
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var c in go.GetComponentsInChildren<Component>(true))
                {
                    if (c == null) continue;
                    string n = c.GetIl2CppType().Name;
                    if (Keep.Contains(n)) continue;
                    bool script = c.TryCast<MonoBehaviour>() != null;
                    bool body = n == "Rigidbody";
                    if (pass == 0 && !script) continue;
                    if (pass == 1 && body) continue;
                    try { UnityEngine.Object.DestroyImmediate(c); } catch { }
                }
            }
            foreach (var mr in go.GetComponentsInChildren<Renderer>(true))
            {
                string n = mr.gameObject.name;
                if (n.Contains("Burnt") || n.Contains("Helper")) mr.enabled = false;
            }
        }

        // Есть ли у модели что рисовать: хоть один включённый рендерер с сеткой и материалом.
        internal static bool HasLook(GameObject go)
        {
            try
            {
                bool any = false;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null || !r.enabled) continue;
                    any = true;
                    if (r.sharedMaterial == null) continue;
                    var smr = r.TryCast<SkinnedMeshRenderer>();
                    if (smr != null) { if (smr.sharedMesh != null) return true; continue; }
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh != null) return true;   // частицы и прочее без сетки считаем видимыми
                }
                return !any;   // одни огоньки/свет без рендереров — проверять нечего
            }
            catch { return true; }
        }

        private static readonly Dictionary<string, int> Rebuilt = new();
        private static readonly HashSet<string> NoLookLogged = new();

        private static void Forget(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return;
            if (Templates.TryGetValue(prefab, out var t) && t != null) { try { UnityEngine.Object.Destroy(t); } catch { } }
            Templates.Remove(prefab);
        }

        // ---------- Вещь в руках куклы ----------

        public class Held
        {
            private string _prefab = "";
            private GameObject _go;
            private Rule _rule;
            private bool? _lit;
            private readonly List<Light> _lights = new();
            private readonly List<ParticleSystem> _fx = new();
            private readonly List<Renderer> _glow = new();

            private float _checkAt;
            private bool _lookOk;
            public Rule Rule => _go != null ? _rule : null;
            public float AimWeight => Mathf.SmoothStep(0f, 1f, _aim);
            public GameObject Go => _go;
            private Rule _mouthCalFor; private CharacterAnimator _mouthCalCh; private Vector3 _mouthOff; private Quaternion _mouthRot;
            public float MouthWeight => _rule != null && _rule.Mouth != null ? Mathf.SmoothStep(0f, 1f, _mouth) : 0f;

            public void Set(string prefab, Transform parent)
            {
                prefab ??= "";
                if (prefab == _prefab && _go != null && _lookOk && Time.unscaledTime >= _checkAt)
                {
                    // Раз в 2 с: видна ли модель. Если у неё пропали сетки или материалы (выгрузились при смене
                    // локации), фигура держала бы пустую руку — собираем модель заново.
                    _checkAt = Time.unscaledTime + 2f;
                    if (!HasLook(_go))
                    {
                        Rebuilt.TryGetValue(prefab, out int n);
                        Rebuilt[prefab] = ++n;
                        MelonLogger.Warning($"[вещи] у модели {prefab} в руках пропал вид (сетки/материалы){(n <= 3 ? " — собираю заново" : ", больше не пересобираю")}");
                        if (n <= 3) { Forget(prefab); Clear(); }
                        else _lookOk = false;
                    }
                }
                if (prefab == _prefab && (_go != null || prefab.Length == 0 || IsMissing(prefab))) return;
                Clear();
                _prefab = prefab;
                var t = Template(prefab);
                if (t == null) return;
                _go = UnityEngine.Object.Instantiate(t, parent);
                _go.name = t.name;
                _rule = RuleFor(prefab, t);
                Prepare(_rule, t);
                _w = 0f; _aim = 0f; _mouth = 0f; _kick = 0f; _boltT = -1f;
                _relW = 0f; _relUntil = 0f; _relOpen = false; _pulseT = -1f; _closeT = -1f; _autoPulse = 0f;
                _go.transform.localScale = t.transform.localScale * _rule.Scale;
                foreach (var l in _go.GetComponentsInChildren<Light>(true)) _lights.Add(l);
                foreach (var p in _go.GetComponentsInChildren<ParticleSystem>(true)) _fx.Add(p);
                foreach (var r in _go.GetComponentsInChildren<Renderer>(true))
                    if (r.gameObject.name.Contains("RadialGradient") || r.gameObject.name.Contains("Glow")) _glow.Add(r);
                _go.SetActive(true);
                _lit = null;
                _lookOk = HasLook(_go);
                _checkAt = Time.unscaledTime + 2f;
                if (!_lookOk && NoLookLogged.Add(prefab)) MelonLogger.Warning($"[вещи] у модели {prefab} нечего рисовать (нет сеток/материалов) — в руках будет пусто");
                if (_rule.Muzzle.HasValue) MuzzleFx.Preload(prefab);   // загрузить вспышку сразу, а не на первом выстреле
                MelonLogger.Msg($"[вещи] в руках {prefab}, ручек {_rule.Grips.Count}{(_rule.Aim != null ? ", целится по-своему" : "")}");
            }

            public void Clear()
            {
                if (_go != null) UnityEngine.Object.Destroy(_go);
                _go = null;
                _prefab = "";
                _lights.Clear(); _fx.Clear(); _glow.Clear();
            }

            public void SetLit(bool lit)
            {
                if (_go == null || _lit == lit) return;
                _lit = lit;
                bool outdoor = true;
                try { var w = GameManager.GetWeatherComponent(); outdoor = w == null || !w.IsIndoorEnvironment(); } catch { }
                bool hasOutdoor = _lights.Exists(l => l != null && l.name.Contains("Outdoor"));
                foreach (var l in _lights)
                {
                    if (l == null) continue;
                    bool want = lit;
                    if (want && hasOutdoor)
                        want = l.name.Contains("Outdoor") == outdoor || (!l.name.Contains("Outdoor") && !l.name.Contains("Indoor"));
                    if (want && l.intensity < 0.1f) l.intensity = 1.5f;
                    l.enabled = want;
                }
                foreach (var p in _fx)
                {
                    if (p == null) continue;
                    if (lit) p.Play(true); else p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                foreach (var r in _glow) if (r != null) r.enabled = lit;
            }

            public enum Mode { Stance, Hand }

            private float _w, _aim, _mouth, _kick, _boltT = -1f;
            private float _clipYaw = -20f;   // насколько ствол в клипе rifle_aim левее, чем смотрит человек (меряется в прицеле)
            private bool _flash;   // выстрел: вспышку у дула ставим, когда вещь уже на месте в этом кадре

            // Выстрел: ружьё отходит назад и стволом вверх, потом возвращается.
            public void Kick()
            {
                if (_rule == null || !_rule.Recoil) return;
                _kick = 1f;
                _flash = _rule.Muzzle.HasValue;
                if (_rule.Bolt.HasValue) _boltT = 0f;
            }

            // Зарядка у другого игрока — по звукам его игры (Game/SoundSync.cs): 1 открыл затвор, 2 вложил патрон,
            // 4 закрыл затвор. Отдельного звука на каждый патрон может не быть — пока затвор открыт, правая
            // сама ходит к окну раз в 1,1 с. Без звука закрытия зарядка кончается через 12 с.
            private float _relW, _relUntil, _pulseT = -1f, _closeT = -1f, _autoPulse;
            private bool _relOpen;
            public const byte ReloadOpen = 1, ReloadRound = 2, ReloadClose = 4;

            public void Reload(byte act)
            {
                if (_rule == null || !_rule.Recoil || act == 0) return;
                float now = Time.time;
                if (_rule.Break)
                {
                    // 0.19.58: дробовик — один патрон. Что бы ни пришло (открыл / патрон / закрыл), показываем весь
                    // перелом: ствол вниз, левая с патроном к казённику, закрыл. Раньше при одном звуке закрытия
                    // зарядка шла 0,6 с в позе винтовки и перелома не было видно.
                    _relOpen = true;
                    if ((act & ReloadClose) != 0)
                    {
                        if (_relUntil < now) { _pulseT = 0f; _closeT = -0.99f; _relUntil = now + 1.6f; }   // открытия не видели
                        else { _closeT = -0.3f; _relUntil = now + 0.9f; }
                    }
                    else
                    {
                        _closeT = -1f;
                        if ((act & ReloadRound) != 0) _pulseT = 0f;
                        _autoPulse = 0.5f;
                        _relUntil = now + 3f;   // закрытия не услышим — закроется само
                    }
                    return;
                }
                if ((act & ReloadOpen) != 0) { _relOpen = true; _closeT = -1f; _autoPulse = 0.6f; }
                if ((act & ReloadRound) != 0) { _pulseT = 0f; _autoPulse = 1.1f; }
                _relUntil = Mathf.Max(_relUntil, now + 12f);
                if ((act & ReloadClose) != 0)
                {
                    _relOpen = true;   // открытия не слышали — всё равно показать, как затвор закрывается
                    _closeT = (act & ReloadRound) != 0 ? -0.6f : 0f;   // патрон и закрытие одним звуком: сначала патрон
                    _relUntil = now + 0.6f + ((act & ReloadRound) != 0 ? 0.6f : 0f);
                }
            }

            // Насколько оттянут затвор при зарядке и вес правой у окна (вкладывает патрон).
            private void ReloadPhase(float dt, out float pull, out float port)
            {
                pull = 0f; port = 0f;
                if (_pulseT >= 0f) { _pulseT += dt; if (_pulseT > 0.6f) _pulseT = -1f; }
                else if (_relOpen && _closeT < 0f && Time.time < _relUntil) { _autoPulse -= dt; if (_autoPulse <= 0f) { _pulseT = 0f; _autoPulse = 1.1f; } }
                if (_pulseT >= 0f) port = Mathf.Sin(Mathf.Clamp01(_pulseT / 0.6f) * Mathf.PI);
                if (_closeT > -1f && _closeT < 10f)
                {
                    _closeT += dt;
                    if (_closeT >= 0.3f) { _relOpen = false; _closeT = 10f; }
                }
                if (_relOpen) pull = 0.08f * (_closeT > 0f && _closeT < 10f ? 1f - _closeT / 0.3f : 1f);
            }

            // Затвор после выстрела: 0,35 с отдача, потом рука к рукояти, назад, вперёд и обратно на шейку приклада.
            // Вес руки на рукояти и насколько затвор оттянут (в осях модели, к прикладу).
            private static void BoltPhase(float t, out float w, out float pull)
            {
                w = 0f; pull = 0f;
                if (t < 0.35f || t > 1.1f) return;
                w = t < 0.5f ? (t - 0.35f) / 0.15f : t > 0.95f ? (1.1f - t) / 0.15f : 1f;
                w = Mathf.SmoothStep(0f, 1f, w);
                if (t >= 0.5f && t < 0.7f) pull = (t - 0.5f) / 0.2f;
                else if (t >= 0.7f && t < 0.9f) pull = 1f - (t - 0.7f) / 0.2f;
                pull = Mathf.SmoothStep(0f, 1f, pull) * 0.08f;
            }

            // Stance — вещь стоит от тела, руки тянутся к её ручкам. Hand — вещь в кулаке, рука как в анимации
            // (ест, пьёт, жест, схватка): вещь ставится по тому, как сейчас лежит кулак.
            public void Pose(CharacterAnimator ch, Mode mode, bool aiming, float dt, bool mouth = false)
            {
                if (_go == null || ch == null || !ch.HasArms || _rule.Grips.Count == 0) return;
                var r = _rule;
                var tr = _go.transform;
                // Огонёк спички подрагивает.
                if (_lit == true && _lights.Count > 0 && IsMatches(_prefab))
                    foreach (var l in _lights) if (l != null) l.intensity = 0.9f + 0.5f * Mathf.PerlinNoise(Time.time * 9f, 0.3f);
                var scale = tr.lossyScale;
                if (mode == Mode.Hand)
                {
                    _w = 0f;
                    var g = r.Grips.Find(x => x.Right) ?? r.Grips[0];
                    if (!ch.HandGrip(g.Right, r.Pinch && g.Right, out var at, out var hole, out var palm)) return;
                    var q = Quaternion.LookRotation(hole, palm) * Quaternion.Inverse(Quaternion.LookRotation(g.Hole, g.Palm));
                    tr.rotation = q;
                    tr.position = at - q * Vector3.Scale(g.At, scale);
                    return;
                }
                bool reloading = Time.time < _relUntil;
                if (!reloading) { _relOpen = false; _pulseT = -1f; _closeT = -1f; }
                _relW = Mathf.MoveTowards(_relW, reloading ? 1f : 0f, dt / 0.3f);
                _aim = Mathf.MoveTowards(_aim, aiming && r.Aim != null && !reloading ? 1f : 0f, dt / Mathf.Max(0.05f, r.AimTime));
                _mouth = Mathf.MoveTowards(_mouth, mouth && r.Mouth != null ? 1f : 0f, dt / 0.4f);
                _w = Mathf.MoveTowards(_w, ch.OneShotOnRight ? 0f : 1f, dt / 0.25f);
                _kick = Mathf.Max(0f, _kick - dt / 0.3f);
                if (_boltT >= 0f) { _boltT += dt; if (_boltT > 1.1f) _boltT = -1f; }
                float a = Mathf.SmoothStep(0f, 1f, _aim), w = Mathf.SmoothStep(0f, 1f, _w);
                var aimSt = r.Aim ?? r.Relax;
                // Руки по клипу (ружьё стоя): hw — вес клипа (плавно 0..1, CharacterAnimator.HoldW). При 1 корпус, щека
                // и руки как в анимации, ружьё — по правой кисти; при 0 — как раньше (поза Relax/Aim и решатель).
                float hw = r.ArmsClip != null ? Mathf.Clamp01(ch.HoldW) : 0f;
                // Сначала корпус (от него считаются плечи и бёдра), потом вещь, потом руки к ней.
                if (hw < 0.999f)
                {
                    ch.TwistTorso(Mathf.Lerp(r.Relax.Twist, aimSt.Twist, a) * w * (1f - hw));
                    if (r.Cheek) ch.CheekWeld(a * w * (1f - hw));
                }
                bool clip = false;
                Vector3 pHand = default, pFit = default; Quaternion qHand = Quaternion.identity, qFit = Quaternion.identity;
                if (hw > 0.001f)
                {
                    // Грудь: в приседе назад на наклон приседа (иначе ствол смотрел в землю); в прицеле ружьё вверх-вниз на
                    // весь взгляд (спина по LookPitch даёт только половину), голова остаётся смотреть, куда смотрела.
                    float lookHalf = 0.5f * ch.AimPitch * a;
                    ch.BendChest(hw * (lookHalf - CharacterAnimator.CrouchChest * ch.CrouchAmount), hw * lookHalf);
                    clip = HandPlace(ch, r, scale, out pHand, out qHand, out pFit, out qFit);
                    // В клипе rifle_aim ствол смотрит на 15–25° левее, чем стоит человек: доворачиваем корпус вместе с
                    // головой и руками, чтобы ружьё смотрело туда же, куда смотрит игрок. Угол меряем в полном прицеле
                    // (в переходе руки ещё наполовину в клипе «у пояса», там ствол левее на 60°) и держим его.
                    if (clip && a > 0f)
                    {
                        if (a > 0.99f && hw > 0.99f)
                        {
                            var fl = Quaternion.Inverse(ch.Root.rotation) * (qFit * r.Fwd);
                            float yawErr = Mathf.Clamp(Mathf.Atan2(fl.x, fl.z) * Mathf.Rad2Deg, -40f, 40f);
                            _clipYaw = Mathf.Lerp(_clipYaw, yawErr, 1f - Mathf.Exp(-dt * 6f));
                        }
                        if (Mathf.Abs(_clipYaw * a * hw) > 0.1f)
                        {
                            ch.TwistTorso(-_clipYaw * a * hw, false);
                            clip = HandPlace(ch, r, scale, out pHand, out qHand, out pFit, out qFit);
                        }
                    }
                }
                // Где ружьё по клипу (pClip/qClip). qFit остаётся связью левой ладони с ружьём, qHand — правой кисти.
                Vector3 pClip = pFit; Quaternion qClip = qFit;
                float bk = 0f;   // вес «затыльник в плече» (прицел × клип × руки на ружье)
                if (clip && a > 0f && r.Butt.HasValue)
                {
                    // В прицел — затыльником в ямку правого плеча, стволом туда, куда смотрит игрок; руки ниже идут за
                    // ружьём (Follow), голова ложится щекой на приклад. Раньше ружьё ставилось по правой кисти из клипа,
                    // а она у кукол у самого лица: приклад лежал на верху плеча, в щеке и подбородке.
                    // С весом w: пока руки не на ружье (взял его, жест), плечо и голова тоже не доходят до прицела.
                    float k = a * hw * w;
                    // Правое плечо чуть вперёд (ключица на 18°): так поступают и люди, и только так затыльник встаёт на
                    // плечо, а не уходит на 10–15 см в куртку — плечевой сустав у кукол в прицеле глубоко сзади. Левое —
                    // на 12° назад: иначе левая рука до цевья вытягивается в струну и гнёт запястье.
                    ch.SquareShoulders(ShoulderFwd * k, ShoulderBackL * k);
                    // Правая кисть сдвинулась с плечом: её поворот к ружью — заново (левая и qFit — как были).
                    if (HandPlace(ch, r, scale, out var pH2, out var qH2, out _, out _)) { pHand = pH2; qHand = qH2; }
                    if (ButtPlace(ch, r, scale, out var pB, out var qB))
                    {
                        pClip = Vector3.Lerp(pFit, pB, a); qClip = Quaternion.Slerp(qFit, qB, a);
                        bk = k;
                        // Щекой на гребень: голова чуть набок, шея доводит правый глаз на линию прицела.
                        ch.CheekOnStock(pB + qB * Vector3.Scale(new Vector3(0f, r.SightY, 0f), scale), qB * r.Fwd, qB * r.Up,
                                        EyeBelowSight, CheekRoll, k);
                    }
                }
                Vector3 p0 = pClip, p1 = pClip; Quaternion q0 = qClip, q1 = qClip;
                if (!clip || hw < 0.999f)
                {
                    // Одной рукой у бедра (револьвер, ракетница) ствол смотрит туда же, куда игрок: вверх и вниз вместе со взглядом.
                    var rl = r.Relax;
                    Place(ch, rl, scale, rl.OneHand ? Mathf.Clamp(ch.AimPitch, rl.PitchMin, Mathf.Lerp(rl.PitchMax, rl.PitchMaxCrouch, ch.CrouchAmount)) : 0f, out p0, out q0);
                    // Кисть по руке (плечо висит, запястье прямое), вещь — в эту кисть. По позе анимации, до хвата.
                    if (rl.Hang.HasValue && r.Grips[0].Right &&
                        ch.HangShift(true, p0 + q0 * Vector3.Scale(r.Grips[0].At, scale), q0 * r.Grips[0].Hole, q0 * r.Grips[0].Palm,
                                     ch.Root.rotation * rl.Hang.Value, out var hsh, out _))
                        p0 += hsh;
                    p1 = p0; q1 = q0;
                    if (a > 0f) Place(ch, aimSt, scale, ch.AimPitch, out p1, out q1);   // в прицеле ружьё туда, куда он смотрит
                    if (clip)
                    {
                        // Переход между клипом и старой позой (сел/встал, взял ружьё, жест): ружьё плавно между ними.
                        var ps = Vector3.Lerp(p0, p1, a); var qs = Quaternion.Slerp(q0, q1, a);
                        p0 = p1 = Vector3.Lerp(ps, pClip, hw); q0 = q1 = Quaternion.Slerp(qs, qClip, hw);
                    }
                }
                var pos = Vector3.Lerp(p0, p1, a);
                var rot = Quaternion.Slerp(q0, q1, a);
                // Сигарета: из покоя у бедра к губам — дугой вперёд, а не по прямой сквозь куртку.
                if (r.Scissor && a > 0f && a < 1f) pos += ch.Root.rotation * new Vector3(0.04f, 0f, 0.10f) * Mathf.Sin(a * Mathf.PI);
                float rw = Mathf.SmoothStep(0f, 1f, _relW);
                if (rw > 0f)
                {
                    var relSt = r.Reload ?? ReloadFromAim(r);
                    if (relSt != null)
                    {
                        Place(ch, relSt, scale, 0f, out var p2, out var q2);
                        pos = Vector3.Lerp(pos, p2, rw);
                        rot = Quaternion.Slerp(rot, q2, rw);
                    }
                }
                float rpull = 0f, rport = 0f;
                if (rw > 0f) ReloadPhase(dt, out rpull, out rport);
                if (r.Break && rw > 0f && rpull > 0f)
                {
                    // Перелом: до 35° стволом вниз вокруг правой руки на шейке приклада.
                    var gp = Vector3.Scale(r.Grips[0].At, scale);
                    var q = rot * Quaternion.AngleAxis(-25f * (rpull / 0.08f) * rw, Vector3.right);
                    pos += rot * gp - q * gp;
                    rot = q;
                }
                if (_kick > 0f)
                {
                    var f = rot * r.Fwd;
                    var side = Vector3.Cross(Vector3.up, f);
                    float k = Mathf.Sin(_kick * Mathf.PI * 0.5f);
                    // Отдача: ствол вверх на 12°, вещь на 6 см назад (раньше 7° и 4 см — со стороны не было видно).
                    // Затыльником в плече (вес bk) ствол подбрасывает вокруг верха затыльника (он упирается в плечо),
                    // а назад — только на 1,5 см: на 6 см приклад уходил на 7–8 см в грудь.
                    var kq = Quaternion.AngleAxis(-12f * k, side);
                    if (bk > 0f && r.Butt.HasValue)
                    {
                        var piv = Vector3.Lerp(pos, pos + rot * Vector3.Scale(r.Butt.Value + Vector3.up * KickPivotUp, scale), a * hw);
                        pos = piv + kq * (pos - piv);
                    }
                    rot = kq * rot;
                    pos -= f * Mathf.Lerp(0.06f, KickBack, bk > 0f ? a * hw : 0f) * k;
                }
                // Одной рукой в прицел: рука вытянута от правого плеча вперёд, локоть чуть согнут, запястье почти прямое
                // (кисть продолжает предплечье), вещь сдвигается в такую кисть. Ствол остаётся смотреть, куда смотрит игрок.
                // Сдвиг считается от позы анимации — до того, как рука взялась за вещь.
                bool straight = false; Vector3 sElbow = default;
                if (hw < 0.001f && aimSt.OneHand && r.Aim != null && a > 0f && rw < 0.99f)
                {
                    var g0 = r.Grips[0];
                    var look = ch.Root.rotation * Quaternion.Euler(ch.AimPitch, 0f, 0f);
                    float ay = aimSt.ArmYaw * Mathf.Deg2Rad, au = aimSt.ArmUp * Mathf.Deg2Rad;
                    var armDir = look * new Vector3(Mathf.Sin(ay) * Mathf.Cos(au), Mathf.Sin(au), Mathf.Cos(ay) * Mathf.Cos(au));
                    var outDir = ch.Root.rotation * new Vector3(0.5f, -1f, 0f);
                    if (g0.Right && ch.WristShift(true, pos + rot * Vector3.Scale(g0.At, scale), rot * g0.Hole, rot * g0.Palm, armDir, 15f, aimSt.ArmBend, outDir, out var sh, out sElbow))
                    {
                        float k = a * (1f - rw);
                        pos += sh * k;
                        sElbow += sh * (k - 1f);
                        straight = true;
                    }
                }
                // Трубка в зубах: вещь у рта, рука отпускает её и опускается (вес рук ниже — на 1 − mw).
                float mw = r.Mouth != null ? Mathf.SmoothStep(0f, 1f, _mouth) : 0f;
                if (mw > 0f)
                {
                    Place(ch, r.Mouth, scale, ch.AimPitch, out var pm, out var qm);
                    // 11-10: трубка во рту ходит вместе с головой. Где она относительно головы — снимаем, когда игрок смотрит
                    // прямо, дальше ставим по кости головы (раньше шла за плечом и отставала от головы).
                    var head = ch.Head;
                    if (head != null)
                    {
                        if (_mouthCalFor != r || _mouthCalCh != ch)
                        {
                            _mouthCalFor = null;
                            if (Mathf.Abs(ch.AimPitch) < 4f)
                            {
                                _mouthOff = Quaternion.Inverse(head.rotation) * (pm - head.position);
                                _mouthRot = Quaternion.Inverse(head.rotation) * qm;
                                _mouthCalFor = r; _mouthCalCh = ch;
                            }
                        }
                        if (_mouthCalFor == r && _mouthCalCh == ch)
                        {
                            pm = head.position + head.rotation * _mouthOff;
                            qm = head.rotation * _mouthRot;
                        }
                    }
                    pos = Vector3.Lerp(pos, pm, mw);
                    rot = Quaternion.Slerp(rot, qm, mw);
                }
                tr.rotation = rot;
                tr.position = pos;
                // Руки по клипу идут за ружьём, если его сдвинули (доворот в левую ладонь, отдача, зарядка):
                // правая поворачивается вместе с ним, левая ложится на цевьё там, где она в клипе.
                if (clip && w > 0.001f)
                {
                    // С весом w: взял ружьё или кончился жест — руки въезжают на него за 0,25 с, а не прыгают.
                    float lw = w * ((r.Relax.OneHand ? Mathf.Max(a, rw) : 1f) * (aimSt.OneHand ? 1f - a : 1f));
                    var gR = r.Grips.Find(x => x.Right); var gL = r.Grips.Find(x => !x.Right);
                    // Затыльником в плече (bk) ружьё на 8–12 см дальше и ниже, чем по клипу: локти ставятся так, чтобы
                    // запястья остались как в клипе (левый локоть под цевьём), а левая ладонь съезжает по цевью назад
                    // до 14 см (до магазина), чтобы рука не вытягивалась в струну.
                    ch.Follow(true, pos + rot * Vector3.Scale(gR.At, scale), rot * Quaternion.Inverse(qHand), w, bk);
                    if (gL != null && ch.HandGrip(false, false, out var lat, out _, out _))
                        ch.Follow(false, ForendPoint(rot, pos, gL, scale, lat, Mathf.Lerp(ForendSlide, ForendSlideButt, bk)),
                                  rot * Quaternion.Inverse(qFit), lw, bk);
                }
                if (_flash)
                {
                    _flash = false;
                    try { MuzzleFx.Play(_prefab, tr.TransformPoint(r.Muzzle.Value), rot * r.Fwd); } catch { }
                }
                var root = ch.Root;
                float leftW = (r.Relax.OneHand ? Mathf.Max(a, rw) : 1f) * (aimSt.OneHand ? 1f - a : 1f);
                foreach (var g in r.Grips)
                {
                    bool second = g != r.Grips[0];
                    float gw = w * (second ? leftW : 1f) * (1f - mw);
                    if (gw <= 0.001f) continue;
                    var at = tr.TransformPoint(g.At);
                    float gwFull = gw;   // вес руки до урезания клипом: от него палец на спуске (иначе палец прыгал)
                    float clipBolt = 0f;
                    if (g.Right && _boltT >= 0f && r.Bolt.HasValue)
                    {
                        BoltPhase(_boltT, out float bw, out float pull);
                        // По клипу решатель включается только ради затвора (вес bw), остальное — вес старой позы (1 − hw):
                        // доля затвора в цели — bw от общего веса. Без клипа — как раньше.
                        float toBolt = clip ? bw / Mathf.Max(1e-3f, Mathf.Max(bw, 1f - hw)) : bw;
                        if (bw > 0f) at = Vector3.Lerp(at, tr.TransformPoint(r.Bolt.Value + Vector3.forward * pull), toBolt);
                        clipBolt = bw;
                    }
                    // Руки по клипу уже на ружье: решатель включается на затвор (правая), на зарядку и в переходе к старой позе.
                    if (clip)
                    {
                        gw *= Mathf.Max(Mathf.Max(rw, 1f - hw), g.Right ? clipBolt : 0f);
                        if (gw <= 0.001f)
                        {
                            if (g.Right && r.TriggerAt.HasValue)
                            {
                                float bolt0 = 0f;
                                if (_boltT >= 0f && r.Bolt.HasValue) BoltPhase(_boltT, out bolt0, out _);
                                float tw0 = gwFull * (1f - Mathf.Max(bolt0, rw));
                                if (tw0 > 0.01f) ch.AimIndex(tr.TransformPoint(r.TriggerAt.Value), tw0);
                            }
                            continue;
                        }
                    }
                    if (g.Right && rw > 0f && r.Bolt.HasValue)
                    {
                        // Зарядка: правая на рукояти затвора (открыт — оттянут к прикладу), с патроном — к окну.
                        var boltAt = tr.TransformPoint(r.Bolt.Value + Vector3.forward * rpull);
                        if (r.Port.HasValue && rport > 0f) boltAt = Vector3.Lerp(boltAt, tr.TransformPoint(r.Port.Value + Vector3.up * 0.04f * (1f - rport)), rport);
                        at = Vector3.Lerp(at, boltAt, rw);
                    }
                    else if (!g.Right && rw > 0f && !r.Bolt.HasValue && r.Port.HasValue)
                    {
                        // Револьвер: левая берёт патрон у левого кармана и щепотью вкладывает в дверцу барабана,
                        // по одному (rport — каждый патрон от 0 до 1 и обратно).
                        var side = rot * Vector3.Cross(r.Up, r.Fwd);
                        // Щепоть у задней кромки барабана снаружи справа; кисть позади неё (ладонью к дулу, пальцы поперёк
                        // к левому боку): раньше ладонь смотрела в бок револьвера и согнутые пальцы уходили внутрь него.
                        var door = tr.TransformPoint(r.Port.Value) + side * 0.02f - rot * r.Fwd * 0.012f;
                        var hipL = ch.AnchorBone(HipL);
                        var pocket = (hipL != null ? hipL.position : root.position) + root.rotation * new Vector3(0.04f, 0.12f, 0.12f);
                        float k = Mathf.SmoothStep(0f, 1f, rport);
                        var lat = Vector3.Lerp(pocket, door, 0.35f + 0.65f * k);
                        var lpole = root.rotation * (Vector3.down + Vector3.left * 0.6f + Vector3.back * 0.3f);
                        ch.Grip(false, Vector3.Lerp(at, lat, rw), -(rot * r.Up), rot * r.Fwd, lpole, gw, pinch: true, search: true, palmWeight: 3f);
                        continue;
                    }
                    var out_ = g.Right ? Vector3.right : Vector3.left;
                    // В прицеле локти больше вниз (раньше правый задирался почти до плеча).
                    // Левый локоть у бедра сильнее наружу: рука с цевьём не должна идти через туловище.
                    var pole = root.rotation * (Vector3.down + out_ * Mathf.Lerp(g.Right ? 0.5f : 0.8f, 0.35f, a) + Vector3.back * 0.2f);
                    if (g.Right && r.ElbowRelax.HasValue && r.ElbowAim.HasValue)
                    {
                        var rel = r.ElbowMid.HasValue ? Vector3.Lerp(r.ElbowRelax.Value, r.ElbowMid.Value, Mathf.Clamp01(a * 3f)) : r.ElbowRelax.Value;
                        pole = root.rotation * Vector3.Lerp(rel, r.ElbowAim.Value, a);
                    }
                    if (g.Right) ch.ScissorAim = a;
                    ch.Grip(g.Right, at, rot * g.Hole, rot * g.Palm, pole, gw, r.Pinch && g.Right, search: true, aim: r.Aim != null && a > 0.5f,
                            palmWeight: g.Right ? r.PalmWeight : 0.3f, roll: !(g.Right && r.NoRoll));
                    // Одной рукой в прицел: поверх — рука прямо, локоть там, где его посчитал WristShift.
                    if (straight && g.Right && !second)
                    {
                        // Плечевой сустав лежит на линии плечо–запястье: от него направление на локоть и есть подсказка.
                        var up = ch.AnchorBone(ShR);
                        if (up != null) ch.Grip(true, at, rot * g.Hole, rot * g.Palm, sElbow - up.position, gw * a * (1f - rw), r.Pinch, search: false);
                        // Лишний поворот кисти против предплечья (больше 35°) на три четверти уходит в плечо.
                        ch.EaseTwist(true, 35f, 0.75f, gw * a * (1f - rw));
                    }
                    // Пальцы обхватывают рукоять (револьвер), потом указательный — на спуск.
                    if (g.Right && !second && r.Shape != null)
                    {
                        var sh = r.Shape;
                        float sc = (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f;
                        ch.WrapFingers(tr.TransformPoint(sh.Center), rot * sh.Axis, rot * sh.Side, sh.HalfSide * sc, sh.HalfFront * sc, sh.Taper,
                                       0.01f * sc, gwFull);
                    }
                    // Палец на спуск, пока правая на шейке (не у затвора).
                    if (g.Right && r.TriggerAt.HasValue)
                    {
                        float bolt = 0f;
                        if (_boltT >= 0f && r.Bolt.HasValue) BoltPhase(_boltT, out bolt, out _);
                        float tw = gwFull * (1f - Mathf.Max(bolt, rw));
                        if (tw > 0.01f) ch.AimIndex(tr.TransformPoint(r.TriggerAt.Value), tw);
                    }
                }
                // Пока клип с винтовкой проявляется или гаснет, доворот к левой ладони гаснет по его весу (без скачка ружья).
                if (leftW > 0.5f) RestOnLeft(ch, r, tr, root, w, a, rw, clip ? 1f - hw : 1f);
            }

            // Ружьё по рукам из клипа: в правой кисти, как в ручке, потом доворот вокруг правой кисти (до 8°), чтобы
            // цевьё легло в левую ладонь. qHand/pHand — только по правой кисти, qFit/pFit — с доворотом.
            private const float FitMax = 8f, ForendSlide = 0.10f;

            private static bool HandPlace(CharacterAnimator ch, Rule r, Vector3 scale, out Vector3 pHand, out Quaternion qHand, out Vector3 pFit, out Quaternion qFit)
            {
                pHand = pFit = default; qHand = qFit = Quaternion.identity;
                var gR = r.Grips.Find(x => x.Right); var gL = r.Grips.Find(x => !x.Right);
                if (gR == null || !ch.HandGrip(true, false, out var at, out var hole, out var palm)) return false;
                qHand = Quaternion.LookRotation(hole, palm) * Quaternion.Inverse(Quaternion.LookRotation(gR.Hole, gR.Palm));
                pHand = at - qHand * Vector3.Scale(gR.At, scale);
                qFit = qHand; pFit = pHand;
                if (gL == null || !ch.HandGrip(false, false, out var lat, out _, out _)) return true;
                var want = ForendPoint(qHand, pHand, gL, scale, lat);
                if ((lat - want).sqrMagnitude < 2.5e-5f) return true;
                var t = Quaternion.FromToRotation(want - at, lat - at);
                t.ToAngleAxis(out float ang, out var axis);
                if (ang > 180f) ang -= 360f;
                if (float.IsNaN(ang) || axis.sqrMagnitude < 0.5f) return true;
                t = Quaternion.AngleAxis(Mathf.Clamp(ang, -FitMax, FitMax), axis);
                qFit = t * qHand;
                pFit = at + t * (pHand - at);
                return true;
            }

            // Точка на оси цевья (вдоль ружья, ось модели Z) ближе всего к near, не дальше ForendSlide от точки левой ручки.
            private static Vector3 ForendPoint(Quaternion rot, Vector3 pos, GripDef gL, Vector3 scale, Vector3 near, float slide = ForendSlide)
            {
                var a = pos + rot * Vector3.Scale(gL.At, scale);
                var ax = rot * Vector3.forward;
                float t = Mathf.Clamp(Vector3.Dot(near - a, ax), -slide, slide);
                return a + ax * t;
            }

            // Затыльник в плече: ямка — от плечевого сустава на ButtFwd вперёд по взгляду (снято на макете по сетке всех
            // восьми кукол: дальше затыльник висит перед курткой, ближе — тонет в ней; глядя вверх грудь прогибается назад
            // под затыльник — ямка дальше на ButtFwdUp за градус, глядя вниз — ближе на ButtFwdDown). Вбок — под правый
            // глаз, но чуть внутрь от сустава (от ButtIn до ButtOut): тогда шее не надо класть голову набок, а затыльник
            // ложится между ключицей и дельтой. По высоте — так, чтобы правый глаз был на EyeBelowSight ниже линии
            // прицела, но не выше сустава и не ниже 7 см под ним (у женских кукол глаз ниже — затыльник на 3–6 см под
            // суставом, у мужских — на 0–4 см).
            private const float ButtFwd = 0.12f, ButtFwdUp = 0.0006f, ButtFwdDown = 0.0006f, ButtIn = -0.05f, ButtOut = -0.005f;
            private const float ButtLow = -0.07f, ButtHigh = 0f, EyeBelowSight = 0f, CheekRoll = 5f;
            private const float ShoulderFwd = 18f, ShoulderBackL = 12f, ForendSlideButt = 0.14f, KickBack = 0.015f, KickPivotUp = 0.10f;

            private static bool ButtPlace(CharacterAnimator ch, Rule r, Vector3 scale, out Vector3 pos, out Quaternion rot)
            {
                pos = default; rot = Quaternion.identity;
                var sh = ch.AnchorBone(ShR);
                if (sh == null || !r.Butt.HasValue || ch.Root == null) return false;
                float pitch = ch.AimPitch;
                var root = ch.Root.rotation * Quaternion.Euler(pitch, 0f, 0f);
                var fwd = root * Vector3.forward; var up = root * Vector3.up; var right = root * Vector3.right;
                rot = Quaternion.LookRotation(fwd, up) * Quaternion.Inverse(Quaternion.LookRotation(r.Fwd, r.Up));
                var butt = r.Butt.Value;
                float h = ButtLow * 0.5f, x = (ButtIn + ButtOut) * 0.5f;
                if (ch.EyeR != null)
                {
                    var e = ch.EyeR.position - sh.position;
                    h = Vector3.Dot(e, up) - (r.SightY - butt.y) * scale.y - EyeBelowSight;
                    x = Vector3.Dot(e, right);
                }
                h = Mathf.Clamp(h, ButtLow, ButtHigh);
                x = Mathf.Clamp(x, ButtIn, ButtOut);
                float p = Mathf.Clamp(pitch, -45f, 45f);
                float f = ButtFwd + ButtFwdUp * Mathf.Max(-p, 0f) - ButtFwdDown * Mathf.Max(p, 0f);
                var at = sh.position + right * x + fwd * f + up * h;
                pos = at - rot * Vector3.Scale(butt, scale);
                return true;
            }

            // Ружьё в двух руках: если левая не дотянулась до цевья (руки персонажа короче, поза дальше), цевьё
            // не висит над ладонью, а ложится в неё — поворачиваем ружьё вокруг правой кисти (до 10°) и берёмся
            // левой ещё раз. Раньше левая кисть оставалась в воздухе в 3–4 см под цевьём.
            private float _restLogAt;
            private Quaternion _restQ = Quaternion.identity;
            private int _restFrame = -100;

            private void RestOnLeft(CharacterAnimator ch, Rule r, Transform tr, Transform root, float w, float a, float rw, float k = 1f)
            {
                if (!r.Trigger || r.Grips.Count < 2 || w < 0.5f || rw > 0.01f || _boltT >= 0f) return;
                var gR = r.Grips.Find(x => x.Right); var gL = r.Grips.Find(x => !x.Right);
                if (gR == null || gL == null || !ch.HandGrip(false, false, out var lat, out _, out _)) return;
                var pivot = tr.TransformPoint(gR.At);
                var want = tr.TransformPoint(gL.At);
                float err = (lat - want).magnitude;
                float ang = 0f;
                var turn = Quaternion.identity;
                if (err >= 0.01f)
                {
                    turn = Quaternion.FromToRotation(want - pivot, lat - pivot);
                    turn.ToAngleAxis(out ang, out var axis);
                    if (ang > 180f) ang -= 360f;
                    turn = float.IsNaN(ang) || axis.sqrMagnitude < 0.5f ? Quaternion.identity : Quaternion.AngleAxis(Mathf.Clamp(ang, -10f, 10f), axis);
                    if (float.IsNaN(ang)) ang = 0f;
                }
                // Доворот сглаживаем по времени (в осях персонажа): он зависит от того, куда встала левая кисть,
                // а та — от перебора поворотов кисти; без сглаживания ружьё дрожало.
                var rr = root.rotation;
                var local = Quaternion.Inverse(rr) * turn * rr;
                // В переходе клипа доворот начинаем с нуля и сглаживаем (раньше он брался сразу целиком — ружьё дёргалось).
                if (Time.frameCount - _restFrame > 2) _restQ = k < 0.999f ? Quaternion.Slerp(Quaternion.identity, local, 1f - Mathf.Exp(-Time.deltaTime * 8f)) : local;
                else if (Time.frameCount != _restFrame) _restQ = Quaternion.Slerp(_restQ, local, 1f - Mathf.Exp(-Time.deltaTime * 8f));
                _restFrame = Time.frameCount;
                if (k <= 0.001f) return;   // ружьё по клипу: доворот только запоминаем, чтобы при переходе не было рывка
                turn = rr * (k < 0.999f ? Quaternion.Slerp(Quaternion.identity, _restQ, k * Mathf.Clamp01((w - 0.5f) * 2f)) : _restQ) * Quaternion.Inverse(rr);
                if (err < 0.01f && Quaternion.Angle(_restQ, Quaternion.identity) < 0.2f) return;   // дотянулась, доворот уже сошёл на нет
                tr.rotation = turn * tr.rotation;
                tr.position = pivot + turn * (tr.position - pivot);
                var rot = tr.rotation;
                var pole = root.rotation * (Vector3.down + Vector3.left * Mathf.Lerp(0.8f, 0.35f, a) + Vector3.back * 0.2f);
                // В переходе вес левой растёт от нуля с того кадра, как w перешёл 0,5 (иначе рука прыгала на 0,25 сразу).
                float lk = k < 0.999f ? w * k * Mathf.Clamp01((w - 0.5f) * 2f) : w;
                ch.Grip(false, tr.TransformPoint(gL.At), rot * gL.Hole, rot * gL.Palm, pole, lk, false, search: true, aim: r.Aim != null && a > 0.5f, palmWeight: 0.3f);
                if (r.TriggerAt.HasValue) ch.AimIndex(tr.TransformPoint(r.TriggerAt.Value), lk);
                if (k < 0.999f) return;   // лог — только про обычную позу
                if (err > 0.02f && Time.unscaledTime > _restLogAt)
                {
                    _restLogAt = Time.unscaledTime + 60f;
                    float left = ch.HandGrip(false, false, out var lat2, out _, out _) ? (lat2 - tr.TransformPoint(gL.At)).magnitude : -1f;
                    MelonLogger.Msg($"[вещь] {_prefab}: левая не доставала до цевья {err * 100f:0} см ({(a > 0.5f ? "в прицел" : "у бедра")}), повернул ружьё на {Mathf.Clamp(ang, -10f, 10f):0}°, осталось {left * 100f:0} см");
                }
            }

            // Поза зарядки, если у вещи своей нет: как в прицел, но ниже, ближе к себе и стволом вниз.
            private static Stance ReloadFromAim(Rule r)
            {
                var src = r.Aim ?? r.Relax;
                if (src == null) return null;
                return new Stance { Anchor = src.Anchor, Off = src.Off + new Vector3(0f, -0.18f, -0.12f), Yaw = src.Yaw - 15f,
                                    Pitch = src.Pitch - 40f, Roll = src.Roll, Twist = src.Twist * 0.5f, Place = src.Place, Up = src.Up };
            }

            // Где стоит вещь в этой позе: точка от кости, повёрнута как сказано.
            private void Place(CharacterAnimator ch, Stance st, Vector3 scale, float lookPitch, out Vector3 pos, out Quaternion rot)
            {
                var r = _rule;
                var root = ch.Root.rotation;
                if (lookPitch != 0f) root = root * Quaternion.Euler(lookPitch, 0f, 0f);
                var bone = ch.AnchorBone(st.Anchor);
                var anchor = (bone != null ? bone.position : ch.Root.position) + root * st.Off;
                float yaw = st.Yaw * Mathf.Deg2Rad, pitch = st.Pitch * Mathf.Deg2Rad;
                var fwd = root * new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));
                var up = root * (st.Up ?? Vector3.up).normalized;
                if (Mathf.Abs(Vector3.Dot(up, fwd)) > 0.95f) up = root * (fwd.y > 0f ? Vector3.back : Vector3.forward);
                rot = Quaternion.LookRotation(fwd, up) * Quaternion.Inverse(Quaternion.LookRotation(r.Fwd, r.Up));
                if (st.Roll != 0f) rot = Quaternion.AngleAxis(st.Roll, fwd) * rot;
                var place = st.Place ?? r.Grips[0].At;
                pos = anchor - rot * Vector3.Scale(place, scale);
            }
        }
    }

    // Свой выстрел: счётчик уходит с позицией, у остальных кукла проигрывает отдачу.
    // Снимок фотоаппаратом в игре тоже идёт через GunItem.Fired — это не выстрел.
    [HarmonyPatch(typeof(GunItem), nameof(GunItem.Fired))]
    internal static class Patch_GunFired
    {
        private static void Postfix(GunItem __instance)
        {
            try { if (__instance != null && __instance.m_GunType == GunType.Camera) return; } catch { }
            HeldItems.OnShot();
        }
    }

    // Замах камнем: у остальных фигура заносит руку.
    [HarmonyPatch(typeof(StoneItem), nameof(StoneItem.ZoomStart))]
    internal static class Patch_StoneAimStart
    {
        private static void Postfix() => HeldItems.StoneAim = true;
    }

    [HarmonyPatch(typeof(StoneItem), nameof(StoneItem.ZoomEnd))]
    internal static class Patch_StoneAimEnd
    {
        private static void Postfix() => HeldItems.StoneAim = false;
    }

    [HarmonyPatch(typeof(StoneItem), nameof(StoneItem.Throw))]
    internal static class Patch_StoneThrow
    {
        private static void Postfix() => HeldItems.StoneAim = false;
    }

    // Удар в схватке (каждое нажатие): у остальных фигура бьёт тем, что в руке.
    [HarmonyPatch(typeof(PlayerStruggle), nameof(PlayerStruggle.DoTap))]
    internal static class Patch_StruggleTap
    {
        private static void Postfix() => HeldItems.OnShot();
    }
}
