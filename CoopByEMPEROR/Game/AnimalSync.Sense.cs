using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Зверь видит всех игроков.
    //
    // В игре зверь ищет цель среди других зверей, людей из «Сказаний» и ОДНОГО игрока (BaseAi.ScanForNewTarget),
    // а почти все его проверки берут игрока из GameManager.GetPlayerTransform и состояние из PlayerManager
    // (присел, идёт, невидим), AiTarget игрока (факел, фальшфейер, копьё), Inventory (запах), SnowShelterManager.
    // Поэтому на время раздумий зверя (его Update) наш игрок стоит там, где ближайший к зверю игрок, а все эти
    // проверки отвечают про того игрока — по его же игре (сообщение AnimalSense 5 раз в секунду).
    //
    // Бить и начинать схватку должна игра того, на кого зверь бросился: зверь в нападении на чужого игрока
    // одалживается ему (Transfer) и возвращается хозяину локации, когда успокоится.
    public static partial class AnimalSync
    {
        private const float AttackLease = 25f;    // нападение на чужого игрока ближе этого — одалживаем зверя ему
        private const float HoldLease = 14f;      // стойка («держит позицию» медведь/лось) — ближе этого
        private const float StalkLease = 7f;      // выслеживание — ближе этого
        private const float FocusMargin = 5f;     // зверь переводит взгляд на другого игрока, если тот ближе на столько
        private const float CalmReturn = 4f;      // одолженный зверь спокоен столько — возвращаем хозяину локации
        // Ещё никого не заметивший зверь думает про каждого игрока рядом по очереди: вдвоём по 1,5 с, а чем
        // больше игроков, тем короче черёд (круг не дольше ~3 с, но не меньше 0,4 с на каждого; до 8 игроков).
        private const float ThinkRound = 3f, ThinkSliceMax = 1.5f, ThinkSliceMin = 0.4f;
        private const float ThinkRange = 80f;     // кто ближе этого (или не дальше ближайшего в 1,5 раза) — в очереди

        private static bool SeeAll => Session.CurrentRules.AnimalsSeeAll;

        private static int _borrows, _blockedHits, _focusLogs, _senseOverrides, _scentOverrides, _scentSwaps, _scentBlind;

        private static string SenseStats => SeeAll
            ? $"звери видят всех: подмен игрока {_borrows}, подсказок состояния {_senseOverrides}, запах за другого {_scentOverrides} (подмен запаха вещей {_scentSwaps}, без свежих данных {_scentBlind}), отменил ударов {_blockedHits}; думали по очереди {_rotations}, заметили в очередь другого {_commits}, перевели цель на подошедшего вплотную {_closeSwitches}"
            : "звери видят всех: выключено";

        // ---------- Игроки рядом (раз за кадр) ----------

        private static List<(int id, Vector3 pos, float yaw)> _here = new();
        private static int _hereFrame = -1;
        private static bool _activeNow;

        private static List<(int id, Vector3 pos, float yaw)> HereNow()
        {
            if (_hereFrame != Time.frameCount)
            {
                _hereFrame = Time.frameCount;
                _activeNow = Active;
                _here = _activeNow ? PlayersHere(_scene, Time.unscaledTime) : new List<(int, Vector3, float)>();
            }
            return _here;
        }

        private static bool OtherNear(Vector3 pos, float range)
        {
            foreach (var p in HereNow())
                if (p.id != Session.LocalId && (p.pos - pos).sqrMagnitude < range * range) return true;
            return false;
        }

        private static bool FindPlayer(int id, out Vector3 pos, out float yaw)
        {
            foreach (var p in HereNow())
                if (p.id == id) { pos = p.pos; yaw = p.yaw; return true; }
            pos = default; yaw = 0f;
            return false;
        }

        private static bool Locked(AiMode m) => m == AiMode.Attack || m == AiMode.PassingAttack || m == AiMode.Struggle || m == AiMode.HoldGround || m == AiMode.Stalking;

        // На кого зверь «смотрит»: на ближайшего игрока. Посреди нападения или выслеживания цель меняется, только
        // если другой подошёл к зверю вплотную (CloseSwitch), а в остальное время — если другой ближе на FocusMargin,
        // чтобы цель не прыгала.
        private static int ChooseFocus(Animal a, Vector3 at, List<(int id, Vector3 pos, float yaw)> here)
        {
            int best = -1;
            float bestD = float.MaxValue, curD = float.MaxValue;
            var mode = a.Ai.m_CurrentMode;
            // Зверь ещё никого не заметил: игрок думает про одного «игрока», и тот, кто незаметнее (чай FoodPack —
            // множитель обнаружения), для него как бы дальше. Раньше брали голое расстояние, и незаметность
            // ближайшего прятала и второго игрока, которого зверь заметил бы.
            bool unaware = Unaware(mode);
            foreach (var p in here)
            {
                float d = Vector3.Distance(p.pos, at);
                if (unaware) d /= DetectOf(p.id);
                // В машине или невидимый для зверей — не цель, если есть кто-то ещё (Game/AnimalSync.Car.cs).
                if (IgnoredByAnimals(p.id)) d += 1000f;
                // О другом игроке нет свежего состояния (присел ли, запах, факел) — думать о нём нечем: за него
                // ответили бы наши же данные. Тоже не цель, если есть кто-то ещё.
                else if (p.id != Session.LocalId && !SenseOf(p.id, out _)) d += 1000f;
                if (d < bestD) { bestD = d; best = p.id; }
                if (p.id == a.Focus) curD = d;
            }
            if (curD < float.MaxValue)
            {
                // Посреди нападения цель держим — кроме того, кто сел в машину, и кроме случая, когда другой
                // подошёл вплотную: раньше волк, выследивший друга, не замечал хоста, стоящего у самой морды.
                if (Locked(mode) && !InCar(a.Focus))
                {
                    if (best >= 0 && best != a.Focus && CloseSwitch(mode, bestD, curD))
                    {
                        _closeSwitches++;
                        if (_focusLogs++ < 30) MelonLogger.Msg($"[звери] {a.Prefab} ({mode}) переключился с {NameOf(a.Focus)} ({curD:0} м) на {NameOf(best)}: тот вплотную ({bestD:0} м)");
                        return best;
                    }
                    return a.Focus;
                }
                if (bestD + FocusMargin >= curD) return a.Focus;
            }
            return best;
        }

        private static int _closeSwitches;

        // Режимы, в которых зверь ещё не выбрал игрока целью (бродит, ест, спит и т. п.).
        private static bool Unaware(AiMode m) => m == AiMode.Wander || m == AiMode.WanderPaused || m == AiMode.Idle || m == AiMode.Feeding
            || m == AiMode.Sleep || m == AiMode.FollowWaypoints || m == AiMode.GoToPoint || m == AiMode.ScratchingAntlers
            || m == AiMode.PatrolPointsOfInterest || m == AiMode.JoinPack || m == AiMode.Howl || m == AiMode.InvestigateFood;

        private static int _detectFrame = -1;
        private static float _detectLocal = 1f;

        // Множитель обнаружения игрока от модов (FoodPackByEMPEROR): свой — раз за кадр, чужой — из его состояния.
        private static float DetectOf(int id)
        {
            float k;
            if (id == Session.LocalId)
            {
                if (_detectFrame != Time.frameCount) { _detectFrame = Time.frameCount; _detectLocal = FoodPackLink.LocalDetectMul(); }
                k = _detectLocal;
            }
            else k = SenseOf(id, out var s) && s.Detect > 0f ? s.Detect : 1f;
            return float.IsNaN(k) ? 1f : Mathf.Clamp(k, 0.05f, 2f);
        }

        // Посреди нападения: другой игрок у самой морды, а цель заметно дальше. В схватке и в проходном ударе — никогда.
        private static bool CloseSwitch(AiMode mode, float other, float cur)
        {
            switch (mode)
            {
                case AiMode.Attack: return other < 5f && other * 2f < cur;
                // Выслеживает одного, а другой подошёл заметно ближе: в 1,5 раза или на 6 м (раньше только на 6 м и
                // ближе 12 м — друг, подбежавший к волку, выслеживающему хоста в 15 м, оставался «невидим»).
                case AiMode.Stalking:
                case AiMode.HoldGround: return other < 15f && (cur - other > 6f || other * 1.5f < cur);
                default: return false;
            }
        }

        private static void SetFocus(Animal a, int who, string why = null)
        {
            if (a.Focus == who) return;
            a.Focus = who;
            SetCull(a);
            if (_focusLogs++ < 40) MelonLogger.Msg($"[звери] {a.Prefab} смотрит на {NameOf(who)}" + (why != null ? $" ({why})" : ""));
        }

        // Зверя у далёкого от нас игрока наша камера не видит, и игра выключила бы ему анимацию,
        // а ходит он анимацией (root motion): он бы встал. Пока смотрит на другого (или думает про него по очереди) —
        // анимация всегда.
        private static void SetCull(Animal a)
        {
            bool always = (a.Focus >= 0 && a.Focus != Session.LocalId) || a.Rotating;
            if (a.CullAlways == always || a.Ai == null) return;
            a.CullAlways = always;
            try { if (always) a.Ai.SetCullModeAlways(); else a.Ai.ResetDefaultCullmode(); } catch { }
        }

        // ---------- Очередь: зверь, ещё никого не заметивший, думает про каждого ----------
        // Игра зверя знает одного игрока. Раньше он думал только про ближайшего (и держал его, пока другой не
        // подойдёт ближе на 5 м): друг, стоящий рядом с хозяином или за его спиной, для волка не существовал —
        // ни видеть, ни слышать его тот не мог. Теперь, пока зверь никого не заметил (бродит, ест, спит), он по
        // очереди (по 0,4–1,5 с на каждого) думает про каждого, кто рядом. Заметил кого-то в его очередь — тот и цель
        // (CommitThought), дальше как раньше: цель держится, пока другой не окажется у самой морды.

        private static readonly List<(int id, float d)> _cands = new();
        private static int _rotations, _commits;

        private static int Think(Animal a, Vector3 at, List<(int id, Vector3 pos, float yaw)> here, int focus, AiMode mode, float now)
        {
            bool was = a.Rotating;
            a.Rotating = false;
            if (Unaware(mode))
            {
                _cands.Clear();
                float nearest = float.MaxValue;
                foreach (var p in here)
                {
                    if (IgnoredByAnimals(p.id)) continue;
                    if (p.id != Session.LocalId && !SenseOf(p.id, out _)) continue;
                    float d = Vector3.Distance(p.pos, at) / DetectOf(p.id);
                    _cands.Add((p.id, d));
                    if (d < nearest) nearest = d;
                }
                float limit = Mathf.Max(ThinkRange, nearest * 1.5f);
                _cands.RemoveAll(c => c.d > limit);
                if (_cands.Count >= 2)
                {
                    a.Rotating = true;
                    if (now >= a.ThinkUntil || !_cands.Exists(c => c.id == a.ThinkWho))
                    {
                        _cands.Sort((x, y) => x.id.CompareTo(y.id));
                        int i = _cands.FindIndex(c => c.id == a.ThinkWho);
                        a.ThinkWho = _cands[(i + 1) % _cands.Count].id;
                        a.ThinkUntil = now + Mathf.Clamp(ThinkRound / _cands.Count, ThinkSliceMin, ThinkSliceMax);
                        _rotations++;
                    }
                }
            }
            if (was != a.Rotating) SetCull(a);
            return a.Rotating ? a.ThinkWho : focus;
        }

        // Раз в секунду: другой игрок совсем рядом со зверем, а зверь думает не про него — пишем в лог почему
        // (по логу хоста 0.19.3 нельзя было понять, почему волк не замечал друга).
        private static int _whyLogs;

        private static void WhyNotOther(float now)
        {
            if (_whyLogs >= 40 || !SeeAll) return;
            var here = HereNow();
            if (here.Count < 2) return;
            foreach (var a in Animals.Values)
            {
                if (!a.Real || a.Dead || a.Shadow || a.Go == null || a.Ai == null || now - a.WhyLogAt < 15f) continue;
                var at = a.Go.transform.position;
                foreach (var p in here)
                {
                    if (p.id == Session.LocalId || p.id == a.Focus) continue;
                    float d = Vector3.Distance(p.pos, at);
                    if (d > 25f) continue;
                    if (a.Rotating && a.ThinkWho == p.id) continue;
                    AiMode mode;
                    try { mode = a.Ai.m_CurrentMode; } catch { continue; }
                    string why;
                    if (InCar(p.id)) why = "он в машине";
                    else if (IgnoredByAnimals(p.id)) why = "он невидим для зверей";
                    else if (!SenseOf(p.id, out _)) why = "о нём нет свежих данных (AnimalSense)";
                    else if (a.Rotating) why = "думает по очереди, сейчас не его черёд";
                    else if (Locked(mode)) why = "держит прежнюю цель";
                    else if (DetectOf(p.id) < 0.95f) why = $"он незаметнее (обнаружение ×{DetectOf(p.id):0.00})";
                    else why = $"тот ближе или разница меньше {FocusMargin:0} м";
                    float df = -1f;
                    if (a.Focus >= 0 && FindPlayer(a.Focus, out var fp, out _)) df = Vector3.Distance(fp, at);
                    a.WhyLogAt = now;
                    _whyLogs++;
                    MelonLogger.Msg($"[звери] {a.Prefab} ({mode}) думает про {NameOf(a.Focus)}{(df >= 0f ? $" ({df:0} м)" : "")}, а {NameOf(p.id)} в {d:0} м: {why}");
                    break;
                }
            }
        }

        private static Animal _thinkA;
        private static int _thinkWho = -1;
        private static AiMode _thinkMode;

        // После Update: зверь, думавший про игрока по очереди, его заметил (вышел из «ничего не замечаю») —
        // этот игрок и есть цель. Без этого следующий кадр снова думал про ближайшего, и волк, заметивший
        // друга, выслеживал хозяина.
        private static void CommitThought()
        {
            var a = _thinkA;
            _thinkA = null;
            if (a == null || a.Ai == null || _thinkWho < 0) return;
            try
            {
                var m = a.Ai.m_CurrentMode;
                if (!Unaware(_thinkMode) || Unaware(m) || m == AiMode.Dead || m == AiMode.None) return;
                a.Rotating = false;
                if (a.Focus != _thinkWho) { _commits++; SetFocus(a, _thinkWho, $"заметил: {_thinkMode} → {m}"); }
                else SetCull(a);
            }
            catch { }
        }

        // ---------- Подмена игрока на время Update зверя ----------

        internal static bool Borrowing;
        internal static int BorrowId = -1;      // за кого сейчас стоит наш игрок
        internal static int HitBy = -1;         // попадание от другого игрока: зверь отвечает ему
        private static bool _targetChecked;
        private static Transform _bPlayer, _bCam;
        private static BaseAi _bAi;
        private static Vector3 _bPos, _bCamPos;
        private static Quaternion _bRot, _bCamRot;
        private static int _busyFrame = -1;
        private static bool _busy;

        // Игрок в схватке: волк висит у нашей камеры, двигать нас нельзя.
        private static bool LocalBusy()
        {
            if (_busyFrame == Time.frameCount) return _busy;
            _busyFrame = Time.frameCount;
            _busy = false;
            try
            {
                var st = GameManager.m_PlayerStruggle;
                if (st != null && (st.InStruggle() || st.m_Active)) _busy = true;
            }
            catch { }
            return _busy;
        }

        // Зверь — партнёр нашей схватки (с броска до конца подъёма, пока m_Active). Его нельзя ни отдавать,
        // ни делать копией, ни перезапускать: игра ждёт от него конца схватки, иначе игрок так и лежит.
        private static bool IsLocalPartner(Animal a)
        {
            if (a?.Ai == null) return false;
            try
            {
                var st = GameManager.m_PlayerStruggle;
                var p = st != null && st.m_Active ? st.m_PartnerBaseAi : null;
                return p != null && p.Pointer == a.Ai.Pointer;
            }
            catch { return false; }
        }

        // Игрок id сейчас в схватке или встаёт после неё (его действие на фигуре).
        private static bool InStruggle(int id)
        {
            if (id == Session.LocalId) return LocalBusy();
            return Session.Players.TryGetValue(id, out var pl) && (pl.Action == 33 || pl.Action == 34);
        }

        private static int _partnerKept;

        // Сколько секунд после схватки зверь не нападает снова (в игре 5, по просьбе игроков 3).
        private const float GraceAfterStruggle = 3f;
        private static bool _graceLogged;

        private static void StruggleGrace()
        {
            try
            {
                var st = GameManager.m_PlayerStruggle;
                if (st == null || Mathf.Approximately(st.m_GracePeriodAfterStruggleInSeconds, GraceAfterStruggle)) return;
                if (!_graceLogged) { _graceLogged = true; MelonLogger.Msg($"[звери] передышка после схватки: {st.m_GracePeriodAfterStruggleInSeconds:0.#} с → {GraceAfterStruggle:0.#} с"); }
                st.m_GracePeriodAfterStruggleInSeconds = GraceAfterStruggle;
            }
            catch { }
        }

        // Патч PlayerStruggle.Begin: схватку начинает только настоящий зверь нашей игры и только одну.
        internal static bool AllowStruggle(GameObject partner)
        {
            if (!Active) return true;
            var ai = partner != null ? partner.GetComponentInParent<BaseAi>() : null;
            if (ai != null && IsPuppet(ai))
            {
                if (_partnerKept++ < 20) MelonLogger.Msg($"[звери] копия {ai.gameObject.name} хотела начать схватку, не дал");
                return false;
            }
            try
            {
                var st = GameManager.m_PlayerStruggle;
                var cur = st != null && st.m_Active ? st.m_PartnerBaseAi : null;
                if (cur != null && ai != null && cur.Pointer != ai.Pointer)
                {
                    if (_partnerKept++ < 20) MelonLogger.Msg($"[звери] {ai.gameObject.name} хотел начать вторую схватку посреди первой, не дал");
                    return false;
                }
            }
            catch { }
            return true;
        }

        internal static void BeforeAiUpdate(BaseAi ai) => BeforeAiUpdate(ai, false);

        internal static void BeforeAiUpdate(BaseAi ai, bool forHit)
        {
            if (Borrowing) AfterAiUpdate();   // прошлый Update упал, не вернув игрока
            _bForHit = forHit;
            if (ai == null) return;
            var here = HereNow();
            if (!_activeNow || here.Count < 2) return;
            MultiView = true;
            if (!SeeAll) return;
            if (!ByAi.TryGetValue(ai.Pointer, out var g) || !Animals.TryGetValue(g, out var a) || !a.Real || a.Shadow || a.Dead || a.Go == null) return;
            try
            {
                var at = a.Go.transform.position;
                int who;
                if (forHit && HitBy >= 0)
                {
                    who = HitBy;
                    // Шаг не переводит взгляд: зверь думает про идущего только на этот звук (ReceiveNoise решит,
                    // заметил ли). Выстрел и попадание — как раньше.
                    if (!_noiseStep) SetFocus(a, who);
                }
                else
                {
                    var mode = a.Ai.m_CurrentMode;
                    int focus = ChooseFocus(a, at, here);
                    SetFocus(a, focus);
                    who = Think(a, at, here, focus, mode, Time.unscaledTime);
                    _thinkA = a;
                    _thinkWho = who;
                    _thinkMode = mode;
                }
                if (who < 0 || who == Session.LocalId || LocalBusy()) return;
                if (!FindPlayer(who, out var pos, out float yaw)) return;
                var player = GameManager.GetPlayerTransform();
                if (player == null) return;
                _bPlayer = player;
                _bPos = player.position;
                _bRot = player.rotation;
                var cam = GameManager.GetMainCamera();
                _bCam = cam != null ? cam.transform : null;
                if (_bCam != null) { _bCamPos = _bCam.position; _bCamRot = _bCam.rotation; }
                Borrowing = true;
                BorrowId = who;
                _bAi = ai;
                _bTo = pos;
                _borrows++;
                SwapScent(who);
                var delta = pos - _bPos;
                player.position = pos;
                if (_bCam != null)
                {
                    if (!_bCam.IsChildOf(player)) _bCam.position = _bCamPos + delta;
                    float pitch = SenseOf(who, out var s) ? s.Pitch : 0f;
                    _bCam.rotation = Quaternion.Euler(pitch, yaw, 0f);
                }
                if (!_targetChecked)
                {
                    // Проверка один раз: «цель»-игрок, по которой зверь видит и слышит, переехала вместе с игроком.
                    _targetChecked = true;
                    var target = ai.m_PlayerTarget;
                    string tp = target != null ? $"{Vector3.Distance(target.transform.position, pos):0.0} м" : "нет цели";
                    MelonLogger.Msg($"[звери] первая подмена игрока: цель-игрок от места друга {tp}, камера {(_bCam != null ? Vector3.Distance(_bCam.position, pos).ToString("0.0") + " м" : "нет")}");
                }
            }
            catch (Exception e)
            {
                AfterAiUpdate();
                if (_focusLogs++ < 30) MelonLogger.Warning("[звери] подмена игрока: " + e.Message);
            }
        }

        internal static void AfterAiUpdate()
        {
            MultiView = false;
            RestoreScent();
            if (_thinkA != null) CommitThought();
            if (!Borrowing) return;
            Borrowing = false;
            BorrowId = -1;
            try
            {
                if (_bPlayer != null) { _bPlayer.position = _bPos; _bPlayer.rotation = _bRot; }
                if (_bCam != null) { _bCam.position = _bCamPos; _bCam.rotation = _bCamRot; }
            }
            catch { }
            // Попадание (или зверь умер, пока думал): смерть включает тряпичное тело, и физика успевает запомнить
            // игрока там, куда мы его ставили, — у друга. Без этого следующий шаг игрока уводил хоста к другу.
            bool sync = _bForHit;
            try { if (!sync && _bAi != null && (_bAi.m_CurrentMode == AiMode.Dead || _bAi.m_CurrentHP <= 0f)) sync = true; } catch { }
            if (sync) { try { Physics.SyncTransforms(); _syncs++; } catch { } }
            _borrowEndAt = Time.unscaledTime;
            _bPlayer = null;
            _bCam = null;
            _bAi = null;
            _bForHit = false;
        }

        // ---------- Запах того, о ком думает зверь ----------
        // Зверь чует игрока (BaseAi.CanSmellSource, AiTarget.EmitsTrackableSmell) так: если у Inventory не стоит
        // m_SuppressScentIndicator, запах = m_TotalScentIntensity (запомненный запах вещей: сырое мясо, кровь —
        // его считает Inventory.Update вне раздумий зверя) + GetExtraScentIntensity() (сейчас). Раньше мы подменяли
        // только второе, и запах вещей хозяина ехал за ним на друга. Теперь на время подмены подставляем оба поля.

        private static Inventory _bInv;
        private static float _bTotal;
        private static bool _bSuppress, _bScentSwapped;

        private static void SwapScent(int who)
        {
            try
            {
                var inv = GameManager.GetInventoryComponent();
                if (inv == null) return;
                _bInv = inv;
                _bTotal = inv.m_TotalScentIntensity;
                _bSuppress = inv.m_SuppressScentIndicator;
                _bScentSwapped = true;
                if (SenseOf(who, out var s) && !float.IsNaN(s.Total))
                {
                    inv.m_TotalScentIntensity = s.Total;
                    inv.m_SuppressScentIndicator = s.SuppressScent;
                    _scentSwaps++;
                }
                else
                {
                    // Свежих данных нет — запаха нет (как множитель обнаружения без данных), но не наш собственный.
                    inv.m_TotalScentIntensity = 0f;
                    _scentBlind++;
                }
            }
            catch { }
        }

        private static void RestoreScent()
        {
            if (!_bScentSwapped) return;
            _bScentSwapped = false;
            try { if (_bInv != null) { _bInv.m_TotalScentIntensity = _bTotal; _bInv.m_SuppressScentIndicator = _bSuppress; } } catch { }
            _bInv = null;
        }

        // Postfix Inventory.GetExtraScentIntensity во время подмены: запах того игрока (FoodPack добавляет «Вонь» раньше —
        // её заменяем его значением, где «Вонь» уже есть). Нет свежих данных — 0, а не наш.
        internal static void ExtraScent(ref float result)
        {
            if (!Borrowing || BorrowId < 0) return;
            if (Borrowed(out var s)) { result = s.Scent; _scentOverrides++; }
            else result = 0f;
        }

        // ---------- Сторож: игрок не должен уехать к другу ----------

        private static bool _bForHit, _guardHas;
        private static Vector3 _bTo, _guardPos;
        private static float _borrowEndAt = -100f, _teleportedAt = -100f, _guardFixAt = -100f;
        private static string _guardScene = "";
        private static int _syncs, _guardFixes;

        internal static string GuardStats => $"синхронизаций физики после подмены {_syncs}, возвратов игрока от друга {_guardFixes}";

        // Игра или мод сами переносят игрока (PlayerManager.TeleportPlayer) — это не сбой.
        internal static void NoteTeleport() => _teleportedAt = Time.unscaledTime;

        // Раз в кадр, после всех Update: игрок за кадр прыгнул больше чем на 5 м и оказался у другого игрока,
        // хотя его никто не переносил, а подмена была только что — значит, физика вернула его туда, куда мы
        // его ставили на время подмены. Возвращаем на место.
        internal static void TeleportGuard()
        {
            try
            {
                var t = GameManager.GetPlayerTransform();
                string scene = GameApi.Scene();
                if (!Active || t == null || GameApi.IsLoading() || Borrowing) { _guardHas = false; return; }
                float now = Time.unscaledTime;
                var p = t.position;
                if (_guardHas && scene == _guardScene && now - _teleportedAt > 1f && now - _borrowEndAt < 2f && now - _guardFixAt > 3f
                    && (p - _guardPos).sqrMagnitude > 25f && NearOther(p, _bTo))
                {
                    var back = _guardPos;
                    _guardFixes++;
                    _guardFixAt = now;
                    MelonLogger.Warning($"[звери] игрок прыгнул на {Vector3.Distance(p, back):0} м к месту подмены у другого игрока — вернул на место");
                    var pm = GameManager.GetPlayerManagerComponent();
                    if (pm != null) { pm.TeleportPlayer(back, t.rotation); p = back; }
                }
                _guardPos = p;
                _guardScene = scene;
                _guardHas = true;
            }
            catch { _guardHas = false; }
        }

        private static bool NearOther(Vector3 p, Vector3 lastBorrowTo)
        {
            if ((p - lastBorrowTo).sqrMagnitude < 16f) return true;
            foreach (var o in HereNow()) if (o.id != Session.LocalId && (p - o.pos).sqrMagnitude < 16f) return true;
            return false;
        }

        // Где наш игрок на самом деле (во время подмены он стоит у друга).
        internal static bool RealLocalPos(out Vector3 pos)
        {
            if (Borrowing && _bPlayer != null) { pos = _bPos; return true; }
            var t = GameManager.GetPlayerTransform();
            pos = t != null ? t.position : default;
            return t != null;
        }

        // Во время подмены игра решила ударить «игрока» или начать с ним схватку: это бросок на друга,
        // бить его будет его игра. Удар отменяем, зверя сразу одалживаем ему.
        internal static bool BlockHit(string what, BaseAi ai)
        {
            if (!Borrowing) return false;
            if (ai != null && !TargetsPlayer(ai)) return false;   // бьёт не игрока, а другого зверя — пусть бьёт
            _blockedHits++;
            int to = BorrowId;
            if (_blockedHits < 20) MelonLogger.Msg($"[звери] во время подмены игра хотела {what} {NameOf(to)}, отменил");
            ai ??= _bAi;
            if (ai != null && to >= 0 && ByAi.TryGetValue(ai.Pointer, out var g) && Animals.TryGetValue(g, out var a)) _leaseNow.Add((a, to));
            return true;
        }

        private static readonly List<(Animal a, int to)> _leaseNow = new();

        // ---------- Одалживание и возврат ----------

        // Каждый кадр: зверь бросился на чужого игрока — одалживаем его жертве.
        private static void CheckLeases(float now)
        {
            List<(Animal a, int to)> give = null;
            if (_leaseNow.Count > 0) { give = new List<(Animal, int)>(_leaseNow); _leaseNow.Clear(); }
            var here = HereNow();
            if (here.Count >= 2)
                foreach (var a in Animals.Values)
                {
                    if (!a.Real || a.Dead || a.Shadow || a.Go == null || a.Ai == null || a.Focus < 0 || a.Focus == Session.LocalId) continue;
                    if (now - a.RealSince < 1.5f || !FindPlayer(a.Focus, out var pos, out _)) continue;
                    var m = a.Ai.m_CurrentMode;
                    float d = Vector3.Distance(pos, a.Go.transform.position);
                    bool go = ((m == AiMode.Attack || m == AiMode.PassingAttack) && d < AttackLease)
                        || (m == AiMode.HoldGround && d < HoldLease)
                        || (m == AiMode.Stalking && d < StalkLease);
                    // Волк гонит оленя, а рядом друг: это не бросок на друга — не отдаём (раньше друг получал волка
                    // и тот перекидывался на него, а оленя так и не мог убить).
                    if (go && !TargetsPlayer(a.Ai)) go = false;
                    if (go) (give ??= new List<(Animal, int)>()).Add((a, a.Focus));
                }
            if (give == null) return;
            var done = new HashSet<long>();
            foreach (var (a, to) in give)
            {
                if (!a.Real || a.Go == null || a.Ai == null || !done.Add(a.Gid) || !Here(to, now)) continue;
                // Жертва уже в схватке с другим зверем: этот ждёт у нас, как ждал бы в одиночной.
                if (InStruggle(to)) continue;
                Transfer(a, to, $"бросок на {NameOf(to)}");
            }
        }

        // Раз в секунду, у того, кому зверя одолжили: успокоился (не нападает, не выслеживает, не в схватке)
        // CalmReturn секунд — возвращаем хозяину локации.
        private static void CheckReturns(float now)
        {
            if (IsAuthority || _authority < 0 || !Here(_authority, now)) return;
            foreach (var a in new List<Animal>(Animals.Values))
            {
                if (!a.Real || a.Dead || a.Shadow || a.Go == null || a.Ai == null) continue;
                // Убитый у нас — не «успокоился»: туша остаётся у нас (хозяин держит тень и заберёт её, когда мы уйдём).
                try { if (a.Ai.m_CurrentMode == AiMode.Dead || a.Ai.m_CurrentHP <= 0f) continue; } catch { continue; }
                if (Locked(a.Ai.m_CurrentMode) || LocalBusy()) { a.CalmSince = -1f; continue; }
                if (a.CalmSince < 0f) a.CalmSince = now;
                if (now - a.CalmSince < CalmReturn) continue;
                Transfer(a, _authority, "успокоился");
            }
        }

        // ---------- Состояние игрока для зверей (AnimalSense) ----------

        internal struct Sense
        {
            public float At;
            public bool Crouch, Walk, Sprint, Zoom, Invisible, Stealth, Shelter, Torch, Spear, Struggle, Pie;
            public byte Flares;   // биты FlareType
            public byte Control;  // PlayerControlMode (сон, схватка, сидит в машине и т. п.)
            public float Scent, Pitch;
            public float Detect;  // множитель дальности обнаружения от модов (FoodPackByEMPEROR), 1 = без эффекта
            public byte WolfFlee; // прибавка к шансу волка убежать, % (FeatsByEMPEROR, «Охотник на волков»), 0 — нет
            public float Total;           // Inventory.m_TotalScentIntensity (запах вещей); NaN — не прислал
            public bool SuppressScent;    // Inventory.m_SuppressScentIndicator
        }

        private static readonly Dictionary<int, Sense> Senses = new();
        private static float _nextSense;

        private static bool SenseOf(int id, out Sense s) => Senses.TryGetValue(id, out s) && Time.unscaledTime - s.At < 3f;

        private static void SendSense(float now, string scene)
        {
            if (now < _nextSense) return;
            _nextSense = now + 0.2f;
            if (PlayersHere(scene, now, false).Count == 0) return;
            ushort f = 0;
            byte flares = 0;
            float scent = 0f, pitch = 0f, total = 0f;
            byte control = 0;
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                if (pm != null)
                {
                    if (pm.PlayerIsCrouched()) f |= 1;
                    if (pm.PlayerIsWalking()) f |= 2;
                    if (pm.PlayerIsSprinting()) f |= 4;
                    if (pm.PlayerIsZooming()) f |= 8;
                    if (pm.PlayerIsInvisibleToAi()) f |= 16;
                    if (pm.IsPlayerInStealthArea()) f |= 32;
                    if (pm.IsPumpkinPieBuffActive()) f |= 1024;
                    control = (byte)pm.GetControlMode();
                }
                var ssm = GameManager.GetSnowShelterManager();
                if (ssm != null && ssm.PlayerInShelter()) f |= 64;
                var target = GameManager.GetPlayerObject()?.GetComponent<AiTarget>();
                if (target != null)
                {
                    if (target.HoldingLitTorch()) f |= 128;
                    if (target.HoldingRaisedSpear()) f |= 256;
                    if (target.HoldingLitFlare(FlareType.Red)) flares |= 1;
                    if (target.HoldingLitFlare(FlareType.Blue)) flares |= 2;
                }
                if (LocalBusy()) f |= 512;
                var inv = GameManager.GetInventoryComponent();
                if (inv != null)
                {
                    scent = inv.GetExtraScentIntensity();
                    total = inv.m_TotalScentIntensity;
                    if (inv.m_SuppressScentIndicator) f |= 2048;
                }
                var cam = GameManager.GetMainCamera();
                if (cam != null) { pitch = cam.transform.eulerAngles.x; if (pitch > 180f) pitch -= 360f; }
            }
            catch (Exception e) { if (_senseErr++ < 5) MelonLogger.Warning("[звери] своё состояние для зверей: " + e.Message); }
            var w = Msg.New(MsgType.AnimalSense);
            w.Write((byte)Session.LocalId);
            w.Write(f);
            w.Write(flares);
            w.Write(scent);
            w.Write((sbyte)Mathf.Clamp(Mathf.RoundToInt(pitch), -90, 90));
            w.Write(control);
            w.Write(FoodPackLink.LocalDetectMul());
            w.Write(total);   // хвост 0.19.1: запах вещей
            w.Write((byte)FeatsLink.LocalWolfFlee());   // хвост 0.19.47: подвиг «Охотник на волков»
            Session.SendFast(Msg.Done(w));
        }

        private static int _senseErr;

        private static void ReceiveSense(BinaryReader r)
        {
            int id = r.ReadByte();
            ushort f = r.ReadUInt16();
            byte flares = r.ReadByte();
            float scent = r.ReadSingle();
            float pitch = r.ReadSByte();
            byte control = r.ReadByte();
            // Хвост сообщения (0.18.6+): у старых версий его нет.
            float detect = r.BaseStream.Position + 4 <= r.BaseStream.Length ? r.ReadSingle() : 1f;
            float total = r.BaseStream.Position + 4 <= r.BaseStream.Length ? r.ReadSingle() : float.NaN;   // 0.19.1+
            byte wolfFlee = r.BaseStream.Position + 1 <= r.BaseStream.Length ? r.ReadByte() : (byte)0;   // 0.19.47+
            if (id == Session.LocalId || OtherWorld(id)) return;
            Senses[id] = new Sense
            {
                At = Time.unscaledTime,
                Crouch = (f & 1) != 0, Walk = (f & 2) != 0, Sprint = (f & 4) != 0, Zoom = (f & 8) != 0,
                Invisible = (f & 16) != 0, Stealth = (f & 32) != 0, Shelter = (f & 64) != 0, Torch = (f & 128) != 0,
                Spear = (f & 256) != 0, Struggle = (f & 512) != 0, Pie = (f & 1024) != 0,
                Flares = flares, Scent = scent, Pitch = pitch, Control = control, Detect = detect,
                Total = total, SuppressScent = (f & 2048) != 0, WolfFlee = wolfFlee,
            };
        }

        // Для патчей: сейчас зверь думает про другого игрока — его состояние.
        internal static bool Borrowed(out Sense s)
        {
            s = default;
            if (!Borrowing || BorrowId < 0) return false;
            if (!SenseOf(BorrowId, out s)) return false;
            _senseOverrides++;
            return true;
        }

        internal static void SenseBool(ref bool result, Func<Sense, bool> pick)
        {
            if (Borrowed(out var s)) result = pick(s);
        }

        internal static bool IsLocalPlayerTarget(AiTarget t)
        {
            try { return t != null && t.m_BaseAi == null && t.m_Npc == null; } catch { return false; }
        }

        // Зверь нападает на игрока (или цели нет)? В 2.59 зверь может выбрать целью другого зверя (волк — оленя)
        // или человека из «Сказаний»: такой удар не наш — его не отменяем и зверя из-за него не одалживаем.
        internal static bool TargetsPlayer(BaseAi ai)
        {
            if (ai == null) return true;
            try
            {
                var t = ai.m_CurrentTarget;
                return t == null || IsPlayerTarget(t);
            }
            catch { return true; }
        }

        // ---------- Плавная копия ----------

        // Часы у всех свои. Разница «пришло у нас − отправлено у него»: самая маленькая и есть задержка сети
        // плюс разница часов. Медленно отпускаем вверх, чтобы пережить сдвиг часов.
        private static readonly Dictionary<int, float> Clock = new();

        private static void NoteClock(int from, float off)
        {
            if (!Clock.TryGetValue(from, out var o) || off < o) o = off;
            else o += (off - o) * 0.01f;
            Clock[from] = o;
        }

        private static void AddSnap(Animal a, float t, Vector3 pos, Quaternion rot)
        {
            if (a.SnapCount > 0)
            {
                var last = a.Snaps[a.SnapHead];
                if (t <= last.T) return;   // пришло не по порядку
                float gap = t - last.T;
                NoteSnapSpeed(a, last, pos, gap);
                if (gap < 2f) a.SnapGap += (gap - a.SnapGap) * 0.2f;
            }
            a.SnapHead = (a.SnapHead + 1) % a.Snaps.Length;
            a.Snaps[a.SnapHead] = new Snap { T = t, Pos = pos, Rot = rot };
            if (a.SnapCount < a.Snaps.Length) a.SnapCount++;
        }

        private static Snap SnapAt(Animal a, int back) => a.Snaps[(a.SnapHead - back + a.Snaps.Length * 2) % a.Snaps.Length];

        // Где копия должна быть сейчас: время ведущего минус небольшая задержка, между двумя его точками.
        private static bool Sample(Animal a, float now, out Vector3 pos, out Quaternion rot)
        {
            pos = default; rot = Quaternion.identity;
            if (a.SnapCount == 0 || !Clock.TryGetValue(a.Controller, out var off)) return false;
            float delay = Mathf.Clamp(a.SnapGap * 1.5f, 0.1f, 0.8f);
            float t = now - off - delay;
            var newest = SnapAt(a, 0);
            if (t >= newest.T)
            {
                // Новых точек ещё нет: чуть продолжаем движение (не дольше 0,25 с), потом стоим.
                if (a.SnapCount >= 2)
                {
                    var prev = SnapAt(a, 1);
                    float dt = newest.T - prev.T;
                    float ahead = Mathf.Min(t - newest.T, 0.25f);
                    if (dt > 0.01f && dt < 1f) { pos = newest.Pos + (newest.Pos - prev.Pos) / dt * ahead; rot = newest.Rot; return true; }
                }
                pos = newest.Pos; rot = newest.Rot;
                return true;
            }
            for (int i = 0; i < a.SnapCount - 1; i++)
            {
                var s1 = SnapAt(a, i);
                var s0 = SnapAt(a, i + 1);
                if (t < s0.T) continue;
                float k = (t - s0.T) / Mathf.Max(0.001f, s1.T - s0.T);
                pos = Vector3.Lerp(s0.Pos, s1.Pos, k);
                rot = Quaternion.Slerp(s0.Rot, s1.Rot, k);
                return true;
            }
            var oldest = SnapAt(a, a.SnapCount - 1);
            pos = oldest.Pos; rot = oldest.Rot;
            return true;
        }

        // ---------- Поза схватки ----------

        // Модель зверя → анимация, которую он чаще всего играет, когда ест (и значение AiState при ней).
        private static readonly Dictionary<string, Dictionary<long, int>> PoseVotes = new();
        private static readonly Dictionary<string, (int state, int ai)> Poses = new();
        private static bool _posesLoaded, _posesDirty;
        private static readonly HashSet<string> PoseLogged = new();

        private static string PoseKey(string prefab)
        {
            // WILDLIFE_Wolf_grey → WILDLIFE_Wolf: у разновидностей одного зверя аниматор общий.
            if (string.IsNullOrEmpty(prefab)) return "";
            int i = prefab.IndexOf('_');
            int j = i >= 0 ? prefab.IndexOf('_', i + 1) : -1;
            return j > 0 ? prefab.Substring(0, j) : prefab;
        }

        private static string PosesFile => Path.Combine(MelonEnvironment.UserDataDirectory, "TLDCoop", "animal-poses.txt");

        private static void LoadPoses()
        {
            if (_posesLoaded) return;
            _posesLoaded = true;
            try
            {
                if (!File.Exists(PosesFile)) return;
                foreach (var line in File.ReadAllLines(PosesFile))
                {
                    var p = line.Split(' ');
                    if (p.Length == 3 && int.TryParse(p[1], out int st) && int.TryParse(p[2], out int ai)) Poses[p[0]] = (st, ai);
                }
            }
            catch (Exception e) { MelonLogger.Warning("[звери] позы: " + e.Message); }
        }

        private static void SavePoses()
        {
            if (!_posesDirty) return;
            _posesDirty = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PosesFile));
                var lines = new List<string>();
                foreach (var kv in Poses) lines.Add($"{kv.Key} {kv.Value.state} {kv.Value.ai}");
                File.WriteAllLines(PosesFile, lines);
            }
            catch (Exception e) { MelonLogger.Warning("[звери] позы: " + e.Message); }
        }

        private static void NotePose(string prefab, int state, int aiState)
        {
            if (state == 0) return;
            LoadPoses();
            string key = PoseKey(prefab);
            if (!PoseVotes.TryGetValue(key, out var votes)) PoseVotes[key] = votes = new Dictionary<long, int>();
            long id = ((long)aiState << 32) | (uint)state;
            votes.TryGetValue(id, out int n);
            votes[id] = ++n;
            if (n < 20) return;
            long best = id; int bestN = n;
            foreach (var kv in votes) if (kv.Value > bestN) { best = kv.Key; bestN = kv.Value; }
            var pose = ((int)(best & 0xffffffff), (int)(best >> 32));
            if (Poses.TryGetValue(key, out var old) && old == pose) return;
            Poses[key] = pose;
            _posesDirty = true;
            if (PoseLogged.Add(key)) MelonLogger.Msg($"[звери] поза схватки для {key}: запомнил анимацию еды");
        }

        private static bool PoseOf(string prefab, out int state, out int aiState)
        {
            LoadPoses();
            if (Poses.TryGetValue(PoseKey(prefab), out var p)) { state = p.state; aiState = p.ai; return true; }
            state = 0; aiState = 0;
            return false;
        }

        // Раз в секунду: наши звери, которые сейчас едят, тоже учат позу.
        private static void LearnPoses()
        {
            foreach (var a in Animals.Values)
            {
                if (!a.Real || a.Dead || a.Ai == null || a.Anim == null) continue;
                try
                {
                    if (a.Ai.m_CurrentMode != AiMode.Feeding || !a.Anim.isActiveAndEnabled || a.Anim.IsInTransition(0)) continue;
                    int st = a.Anim.GetCurrentAnimatorStateInfo(0).fullPathHash;
                    var ps = a.Params ??= ParamsOf(a.Anim);
                    int ai = ps != null && ps.Contains(IntH[0]) ? a.Anim.GetInteger(IntH[0]) : 0;
                    for (int i = 0; i < 4; i++) NotePose(a.Prefab, st, ai);
                }
                catch { }
            }
            SavePoses();
        }
    }

    // ---------- Патчи ----------

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.Update))]
    internal static class Patch_AnimalPerception
    {
        private static void Prefix(BaseAi __instance) => AnimalSync.BeforeAiUpdate(__instance);
        // Finalizer, а не Postfix: игрок вернётся на место, даже если Update зверя упал.
        private static Exception Finalizer(Exception __exception) { AnimalSync.AfterAiUpdate(); return __exception; }
    }

    // Состояние игрока, про которого сейчас думает зверь.
    [HarmonyPatch(typeof(PlayerManager))]
    internal static class Patch_AnimalSensePlayer
    {
        [HarmonyPatch(nameof(PlayerManager.PlayerIsCrouched)), HarmonyPostfix] private static void Crouch(ref bool __result) => AnimalSync.SenseBool(ref __result, s => s.Crouch);
        [HarmonyPatch(nameof(PlayerManager.PlayerIsWalking)), HarmonyPostfix] private static void Walk(ref bool __result) => AnimalSync.SenseBool(ref __result, s => s.Walk);
        [HarmonyPatch(nameof(PlayerManager.PlayerIsSprinting)), HarmonyPostfix] private static void Sprint(ref bool __result) => AnimalSync.SenseBool(ref __result, s => s.Sprint);
        [HarmonyPatch(nameof(PlayerManager.PlayerIsZooming)), HarmonyPostfix] private static void Zoom(ref bool __result) => AnimalSync.SenseBool(ref __result, s => s.Zoom);
        [HarmonyPatch(nameof(PlayerManager.PlayerIsInvisibleToAi)), HarmonyPostfix] private static void Invisible(ref bool __result) => AnimalSync.InvisibleToAi(ref __result);
        [HarmonyPatch(nameof(PlayerManager.IsPlayerInStealthArea)), HarmonyPostfix] private static void Stealth(ref bool __result) => AnimalSync.SenseBool(ref __result, s => s.Stealth);
        [HarmonyPatch(nameof(PlayerManager.IsPumpkinPieBuffActive)), HarmonyPostfix] private static void Pie(ref bool __result) => AnimalSync.SenseBool(ref __result, s => s.Pie);
        [HarmonyPatch(nameof(PlayerManager.GetControlMode)), HarmonyPostfix] private static void Control(ref PlayerControlMode __result) { if (AnimalSync.Borrowed(out var s)) __result = (PlayerControlMode)s.Control; }
    }

    [HarmonyPatch(typeof(SnowShelterManager), nameof(SnowShelterManager.PlayerInShelter))]
    internal static class Patch_AnimalSenseShelter
    {
        private static void Postfix(ref bool __result) => AnimalSync.SenseBool(ref __result, s => s.Shelter);
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetExtraScentIntensity))]
    internal static class Patch_AnimalSenseScent
    {
        // Обычный приоритет: после FoodPack (Priority.First), который добавляет «Вонь» своего игрока.
        private static void Postfix(ref float __result) => AnimalSync.ExtraScent(ref __result);
    }

    [HarmonyPatch(typeof(PlayerStruggle), nameof(PlayerStruggle.InStruggle))]
    internal static class Patch_AnimalSenseStruggle
    {
        private static void Postfix(ref bool __result) => AnimalSync.SenseBool(ref __result, s => s.Struggle);
    }

    [HarmonyPatch(typeof(AiTarget))]
    internal static class Patch_AnimalSenseHeld
    {
        [HarmonyPatch(nameof(AiTarget.HoldingLitTorch)), HarmonyPostfix]
        private static void Torch(AiTarget __instance, ref bool __result)
        {
            if (AnimalSync.Borrowing && AnimalSync.IsLocalPlayerTarget(__instance)) AnimalSync.SenseBool(ref __result, s => s.Torch);
        }

        [HarmonyPatch(nameof(AiTarget.HoldingRaisedSpear)), HarmonyPostfix]
        private static void Spear(AiTarget __instance, ref bool __result)
        {
            if (AnimalSync.Borrowing && AnimalSync.IsLocalPlayerTarget(__instance)) AnimalSync.SenseBool(ref __result, s => s.Spear);
        }

        [HarmonyPatch(nameof(AiTarget.HoldingLitFlare)), HarmonyPostfix]
        private static void Flare(AiTarget __instance, FlareType flareType, ref bool __result)
        {
            if (AnimalSync.Borrowing && AnimalSync.IsLocalPlayerTarget(__instance)) AnimalSync.SenseBool(ref __result, s => (s.Flares & (1 << (int)flareType)) != 0);
        }
    }

    // Удар и схватка во время подмены — по другу: отменяем, зверь одалживается ему (бьёт игра друга).
    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.MaybeApplyAttack))]
    internal static class Patch_AnimalPerceptionAttack
    {
        // Удар по другому зверю (волк по оленю) — всегда как в игре, в том числе пока кто-то сидит в машине.
        private static bool Prefix(BaseAi __instance) => !AnimalSync.TargetsPlayer(__instance)
            || (!AnimalSync.CarBlocks("ударить", __instance) && !AnimalSync.BlockHit("ударить", __instance));
    }

    [HarmonyPatch(typeof(PlayerStruggle), nameof(PlayerStruggle.Begin))]
    internal static class Patch_AnimalPerceptionStruggle
    {
        private static bool Prefix(GameObject partner) => !AnimalSync.CarBlocks("начать схватку с", partner != null ? partner.GetComponent<BaseAi>() : null)
            && AnimalSync.AllowStruggle(partner) && !AnimalSync.BlockHit("начать схватку с", partner != null ? partner.GetComponent<BaseAi>() : null);
    }

    [HarmonyPatch(typeof(PlayerStruggle), nameof(PlayerStruggle.BeginSpearStruggle))]
    internal static class Patch_AnimalPerceptionSpear
    {
        private static bool Prefix() => !AnimalSync.CarBlocks("начать схватку с копьём с", null) && !AnimalSync.BlockHit("начать схватку с копьём с", null);
    }

    [HarmonyPatch(typeof(Condition), nameof(Condition.AddHealth), new[] { typeof(float), typeof(DamageSource) })]
    internal static class Patch_AnimalPerceptionHp2
    {
        private static bool Prefix(float hp) => !(hp < 0f && AnimalSync.BlockHit("ранить", null));
    }

    [HarmonyPatch(typeof(Condition), nameof(Condition.AddHealth), new[] { typeof(float), typeof(DamageSource), typeof(bool) })]
    internal static class Patch_AnimalPerceptionHp3
    {
        private static bool Prefix(float hp) => !(hp < 0f && AnimalSync.BlockHit("ранить", null));
    }

    [HarmonyPatch(typeof(Condition), nameof(Condition.AddHealthWithNoHudNotification))]
    internal static class Patch_AnimalPerceptionHpQuiet
    {
        private static bool Prefix(float hp) => !(hp < 0f && AnimalSync.BlockHit("ранить", null));
    }

    // Перенос игрока самой игрой или модом (позвать, к другу, смерть, тренер) — сторож его не трогает.
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.TeleportPlayer))]
    internal static class Patch_AnimalGuardTeleport
    {
        private static void Prefix() => AnimalSync.NoteTeleport();
    }
}
