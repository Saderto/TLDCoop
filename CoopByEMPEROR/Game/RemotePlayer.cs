using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Другой игрок в мире: персонаж WINTERMUTE, которого он выбрал (Game/Characters.cs), с анимациями
    // «стоит / идёт / бежит / присел / сидит / лежит», вещью в руках (Game/HeldItems.cs) и позой по занятию
    // (у костра и за разделкой сидит, во сне лежит). Если персонажей нет — модель из SkyCoop (Models.PlayerPrefab),
    // а если и её нет — цветная капсула с «козырьком» в сторону взгляда.
    public class RemotePlayer
    {
        private static readonly Color[] Colors =
        {
            new Color(0.95f, 0.45f, 0.1f), new Color(0.2f, 0.6f, 1f), new Color(0.3f, 0.85f, 0.3f),
            new Color(0.9f, 0.25f, 0.6f), new Color(0.95f, 0.85f, 0.2f),
        };

        private static Shader _shader;
        private static bool _shaderLogged;

        private GameObject _root;
        private Transform _body;      // капсула (только у простой фигуры)
        private Animator _animator;   // только у модели
        private CharacterAnimator _character;   // только у персонажа WINTERMUTE
        private float _retryAt;
        private bool _wasLit;
        private int _errors;
        private readonly HeldItems.Held _held = new();
        private readonly CarryVisual _carry = new();
        private int _shots = -1;
        private string _charId = "";
        private string _anim = "";
        private float _x, _y, _z, _yaw, _speed, _vy;
        private float _vx, _vz;   // сглаженная скорость по земле (куда он идёт — вперёд, назад или вбок)
        private float _lie;   // 0 стоит … 1 лежит: ложится и встаёт плавно, за ~0,8 с
        private float _sink;  // 0 стоит … 1 по грудь в полынье (провалился под лёд)
        private bool _hasPos;
        // Схватка со зверем: поворот самого игрока не берём (камера в схватке крутится с анимацией), кладём под зверя.
        private bool _struggling;
        public bool Struggling => _struggling;
        // Взгляд игрока в начале схватки. Камера схватки у него крутится, а копия зверя стояла по живому взгляду —
        // зверь и лежащая под ним фигура вращались по земле. Зверя ставим по этому, неподвижному.
        public float StruggleYaw(float fallback) => _struggling ? _struggleYaw : fallback;
        private float _struggleYaw, _nextWolfLook;
        private Transform _wolf;

        // На земле после схватки: «приходит в себя» (34) и «встаёт» (37), а ещё подъём после сна и ранения.
        // Корень тогда стоит на _gAnchor (поворот _gYaw), тело уложено костями (CharacterAnimator.GroundPose).
        private bool _ground;
        private float _downT;           // сколько лежит, приходя в себя
        private float _upT = -1f;       // сколько встаёт (−1 — не встаёт)
        private float _runT;            // сколько уже бежит, пока фигура встаёт
        private bool _downFromLie;      // лёг из позы «лежит» (тогда ноги сгибает плавно)
        private Vector3 _gAnchor;
        private float _gYaw;
        private CharacterAnimator.Ground _gPose, _gDown;
        private Vector3 _lieFeet;       // где лежал (поза «лежит»): отсюда и садится
        private float _lieYaw;
        private bool _lieValid;
        private bool _upDone;           // подъём по «встаёт» (37) доигран; сброс, когда пришло другое занятие

        // Опорные позы. Таз лёжа на 0,45 м позади места игрока: встанет ровно на его место.
        private static readonly CharacterAnimator.Ground GStiff = new() { Back = 88f, HipY = 0.15f, HipX = -0.45f, FootX = 0.6f, Knees = 1f, W = 1f };
        private static readonly CharacterAnimator.Ground GRecover = new() { Back = 82f, Curl = 10f, HipY = 0.13f, HipX = -0.45f, Flat = 1f, Knees = 1f, W = 1f };
        private static readonly CharacterAnimator.Ground GSit = new() { Back = 25f, Curl = 15f, HipY = 0.13f, HipX = -0.45f, Flat = 1f, Knees = 1f, Prop = 1f, W = 1f };
        private static readonly CharacterAnimator.Ground GCrouch = new() { Back = -30f, Curl = 12f, HipY = 0.48f, HipX = -0.15f, Flat = 1f, Knees = 0.4f, OnKnees = 1f, W = 1f };
        private static readonly CharacterAnimator.Ground GStand = new() { Flat = 1f, Up = 1f, W = 1f };
        private const float UpSit = 1.0f, UpCrouch = 1.8f, UpStand = 2.6f;   // садится, на корточки, выпрямился

        public bool Alive => _root != null;
        public float Pitch;    // куда он смотрит по вертикали: + вниз, − вверх (градусы камеры)
        private float _pitch;

        // В машине (Game/Cars.cs): сидит в кресле, голова там, где у него камера.
        private bool _inSeat;
        private Vector3 _seatEye;
        private float _seatYaw;

        public void SetSeat(bool has, Vector3 eye, float yaw)
        {
            _inSeat = has;
            _seatEye = eye;
            _seatYaw = yaw;
        }
        // Спит в кровати или спальнике (Game/BedPose.cs): середина тела на матрасе и куда голова.
        private bool _inBed;
        private Vector3 _bedMid;
        private float _bedYaw;

        public void SetBed(bool has, Vector3 mid, float yaw)
        {
            _inBed = has;
            _bedMid = mid;
            _bedYaw = yaw;
        }

        public byte Gesture;   // жест (Game/Together.cs): 1 помахать, 2 показать, 3 сесть, 4 позвать, 5 стоп
        public bool? Female { get; private set; }   // пол персонажа (для голоса); null — не персонаж
        private GameObject _voice, _sound;

        // Откуда звучат его фразы: точка у головы фигуры (Game/VoiceSync.cs).
        // Звуки друга (шаги, выстрелы, рубка): отдельный источник на уровне пояса, со своей громкостью.
        // Зарядка оружия (по звукам его игры, Game/SoundSync.cs): руки на затвор и к окну.
        public void Reload(byte act) => _held.Reload(act);

        public GameObject SoundEmitter()
        {
            if (_root == null) return null;
            if (_sound != null) return _sound;
            _sound = new GameObject("TLDCoop_Sound");
            _sound.transform.SetParent(_root.transform, false);
            _sound.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            try { _sound.AddComponent<AkGameObj>(); } catch { }
            return _sound;
        }

        // Шаги: отдельный источник не на фигуре. Звук шагов в игре рассчитан на «у самого уха» и с расстоянием
        // глохнет очень быстро, поэтому ставим источник ближе к слушателю (в ту же сторону) — Game/SoundSync.cs.
        private GameObject _step;

        public GameObject StepEmitter()
        {
            if (_root == null) return null;
            if (_step != null) return _step;
            _step = new GameObject("TLDCoop_Step");
            try { _step.AddComponent<AkGameObj>(); } catch { }
            return _step;
        }

        public Vector3 FeetPoint => _root != null ? _root.transform.position : Vector3.zero;

        public GameObject VoiceEmitter()
        {
            if (_root == null) return null;
            if (_voice != null) return _voice;
            _voice = new GameObject("TLDCoop_Voice");
            _voice.transform.SetParent(_root.transform, false);
            _voice.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            try { _voice.AddComponent<AkGameObj>(); } catch { }
            return _voice;
        }

        public void Ensure(int id, string character = "")
        {
            character ??= "";
            // Персонаж не собрался или упала его анимация: пробуем снова, но не чаще раза в 3 секунды
            // (раньше простая фигура пересобиралась каждый кадр).
            if (_root != null && character == _charId && (_character != null || !Characters.Ready || Time.unscaledTime < _retryAt)) return;
            _retryAt = Time.unscaledTime + 3f;
            // Выбрал другого персонажа — пересобираем, оставаясь на том же месте.
            bool keepPos = _root != null && _hasPos;
            Destroy();
            _charId = character;
            _root = null;
            if (Characters.Ready)
            {
                try { _root = BuildCharacter(id, character); }
                catch (Exception e) { if (_errors++ < 5) MelonLogger.Warning("[модель] персонаж не собрался, пока простая фигура: " + e); _root = null; }
            }
            _root ??= Models.PlayerPrefab != null ? BuildModel() : BuildCapsule(id);
            _hasPos = keepPos;
            _anim = "";
        }

        public void Destroy()
        {
            _held.Clear();
            _carry.Clear();
            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (_step != null) UnityEngine.Object.Destroy(_step);
            _root = null;
            _step = null;
            _voice = null;
            _sound = null;
            Female = null;
            _animator = null;
            _body = null;
            _character = null;
            // Новая фигура не должна повторять старый выстрел, бросок или лежание (с другой сцены).
            _shots = -1;
            _wasLit = false;
            _lie = 0f;
            _lieValid = false;
            _sink = 0f;
            _struggling = false;
            _harvesting = false; _carcass = null;
            _wolf = null;
            _strikeT = -1f;
            _upDone = false;
            EndGround();
        }

        // Плавно двигаем фигуру к последней присланной позиции.
        // Поза по занятию (Game/Activity.cs): null — по скорости (стоит / идёт / бежит).
        private byte _act;   // 0.19.34: занятие с последней позиции (укол рисуется в UpdateCharacter)
        private static string PoseFor(byte action) => action switch
        {
            1 or Activity.Downed or Activity.Dead or 33 or 34 => "lie",
            23 or Activity.FellThroughIce => "climb",   // в полынье руки подняты на край льда
            2 or 3 or 7 or 8 or 9 or 10 or 13 or 20 or Activity.Treating => "sit",
            _ => null,
        };

        // Анимация рук и головы по занятию (клипы из characters.bin): разделка — сидя, еда и питьё — как стоит.
        private static string UpperFor(byte action) => action switch
        {
            3 or Activity.Treating => "harvest",
            Activity.Eating => "eat",
            Activity.Drinking => "drink",
            _ => null,
        };

        public void Update(float tx, float ty, float tz, float tyaw, bool crouched, float dt, byte action = 0, string held = "", byte holdFlags = 0, byte shots = 0)
        {
            bool resting = action == 1 || action == 2;
            if (_root == null) return;
            if (_inSeat)
            {
                // Ноги примерно на 1,15 м ниже глаз; точнее голову ставим после позы.
                tx = _seatEye.x; ty = _seatEye.y - 1.15f; tz = _seatEye.z; tyaw = _seatYaw;
                held = ""; crouched = false;
                if (action == 33 || action == 34) action = 0;
            }
            float dx = tx - _x, dy = ty - _y, dz = tz - _z;
            if (!_hasPos || dx * dx + dy * dy + dz * dz > 100f)
            {
                _x = tx; _y = ty; _z = tz; _yaw = tyaw;
                _speed = 0f;
                _vy = 0f;
                _vx = _vz = 0f;
                _hasPos = true;
            }
            else
            {
                float k = 1f - (float)Math.Exp(-20f * dt);
                float mx = dx * k, mz = dz * k;
                _x += mx; _y += dy * k; _z += mz;
                float dyaw = ((tyaw - _yaw) % 360f + 540f) % 360f - 180f;
                _yaw += dyaw * k;
                if (dt > 0f)
                {
                    float v = (float)Math.Sqrt(mx * mx + mz * mz) / dt;
                    _speed += (v - _speed) * Math.Min(1f, dt * 8f);
                    _vx += (mx / dt - _vx) * Math.Min(1f, dt * 8f);
                    _vz += (mz / dt - _vz) * Math.Min(1f, dt * 8f);
                    _vy += (dy * k / dt - _vy) * Math.Min(1f, dt * 8f);
                }
            }
            string pose = PoseFor(action);
            _act = action;
            if (pose == null && Gesture == Together.GestureSit && _speed < 0.4f) pose = "sit";
            if (_inSeat) { pose = _character != null && _character.Has("chair") ? "chair" : "sit"; _speed = 0f; _vx = _vz = _vy = 0f; }
            bool struggle = action == 33;
            if (struggle)
            {
                if (!_struggling) { _struggling = true; _struggleYaw = _yaw; _nextWolfLook = 0f; _wolf = null; }
                if (Time.unscaledTime >= _nextWolfLook)
                {
                    _nextWolfLook = Time.unscaledTime + 0.25f;
                    _wolf = NearestAnimal(new Vector3(tx, ty, tz), 5f);
                }
            }
            else { _struggling = false; _wolf = null; }
            float useYaw = _struggling ? _struggleYaw : _yaw;
            // Разделка: лицом к туше. Взгляд игрока в окне разделки не совпадает с тем, где туша, и фигура сидела
            // боком, а руки тянулись к собственным ботинкам (туша сбоку). Тушу ищем один раз в начале разделки.
            if (action == 3)
            {
                if (!_harvesting) { _harvesting = true; _carcass = NearestCarcass(new Vector3(tx, ty, tz), 2.5f); }
                if (_carcass != null)
                {
                    var cp = _carcass.position;
                    float cx = cp.x - _x, cz = cp.z - _z;
                    if (cx * cx + cz * cz > 0.04f) useYaw = Mathf.Atan2(cx, cz) * Mathf.Rad2Deg;
                }
            }
            else { _harvesting = false; _carcass = null; }
            var feet = new Vector3(_x, _y, _z);
            if (_struggling && _wolf != null)
            {
                // Головой туда, куда смотрит зверь (голова под его мордой), грудью под его грудь; высота — земля под игроком.
                // Лёжа голова у фигуры смотрит против её «вперёд», поэтому поворот на 180°. feet здесь — середина тела.
                float wolfYaw = _wolf.eulerAngles.y;
                useYaw = wolfYaw + 180f;
                var wf = Quaternion.Euler(0f, wolfYaw, 0f) * Vector3.forward;
                var chest = _wolf.position + wf * 0.35f;
                feet = new Vector3(chest.x, _y, chest.z) - wf * 0.4f;
            }
            bool bed = _inBed && action == 1 && !_struggling;
            if (bed)
            {
                // Лёжа середина тела на 0,85 м от «ног» по направлению головы: ноги ставим так, чтобы середина легла на матрас.
                useYaw = _bedYaw;
                feet = _bedMid - Quaternion.Euler(0f, _bedYaw, 0f) * Vector3.forward * 0.85f;
            }
            var yawRot = Quaternion.Euler(0f, useYaw, 0f);
            UpdateGround(action, pose, dt);
            if (_ground) pose = "ground";
            _lie = Mathf.MoveTowards(_lie, pose == "lie" && _character != null ? 1f : 0f, dt / 0.8f);
            if (_ground)
            {
                // Корень стоит на месте, где лежит тело; к концу подъёма — ровно на месте игрока и лицом туда, куда он смотрит.
                var net = new Vector3(_x, _y, _z);
                _gAnchor = Vector3.MoveTowards(new Vector3(_gAnchor.x, _y, _gAnchor.z), net, dt * 0.6f);
                _root.transform.position = Vector3.Lerp(_gAnchor, net, _gPose.Up);
                _root.transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(_gYaw, _yaw, _gPose.Up), 0f);
            }
            else if (_lie > 0f && _character != null)
            {
                // Своей анимации сна нет: кладём персонажа на спину (поза «стоит»), серединой тела на место игрока.
                // Ложится плавно: поворачиваем вокруг пяток, а под конец сдвигаем серединой на место.
                float k = Mathf.SmoothStep(0f, 1f, _lie);
                _root.transform.rotation = yawRot * Quaternion.Euler(-90f * k, 0f, 0f);
                _root.transform.position = feet + yawRot * new Vector3(0f, 0.15f * k, 0.85f * k);
                // С кровати не «садится на пол» позой подъёма: просто встаёт обратно на место игрока.
                if (_lie >= 0.99f) { _lieFeet = feet; _lieYaw = useYaw; _lieValid = !bed; }
            }
            else
            {
                _root.transform.position = new Vector3(_x, _y, _z);
                _root.transform.rotation = yawRot;
                _lieValid = false;
            }
            // Провалился под лёд: уходит в воду по грудь и потом так же плавно «выбирается».
            _sink = Mathf.MoveTowards(_sink, action == Activity.FellThroughIce ? 1f : 0f, dt / (action == Activity.FellThroughIce ? 0.35f : 0.8f));
            if (_sink > 0f) _root.transform.position += new Vector3(0f, -1.15f * Mathf.SmoothStep(0f, 1f, _sink), 0f);

            if (_body != null)
            {
                float h = crouched ? 0.55f : 0.9f;
                _body.localScale = new Vector3(0.55f, h, 0.55f);
                _body.localPosition = new Vector3(0f, h, 0f);
            }
            if (_character != null)
            {
                try
                {
                    UpdateCharacter(pose, UpperFor(action), crouched, held, holdFlags, shots, dt, action == 33);
                    if (_inSeat && _character?.Head != null)
                    {
                        // Спина прямо и чуть откинута на спинку, голова (кость у основания черепа) у глаз-камеры:
                        // чуть ниже и позади. Таз тогда под головой на подушке, а не в спинке; руки на бёдрах.
                        var fwd = Quaternion.Euler(0f, _seatYaw, 0f) * Vector3.forward;
                        _character.SitUpright(fwd, 12f);
                        // В 0.18.4 на 12 см выше: бёдра и таз были под подушкой сиденья (фигура выше, чем считает камера игры).
                        var want = _seatEye + new Vector3(0f, 0.08f, 0f) - fwd * 0.06f;
                        _root.transform.position += want - _character.Head.position;
                        _character.HandsOnThighs();
                    }
                }
                catch (Exception e) { if (_errors++ < 5) MelonLogger.Warning("[модель] анимация упала, пересоберу персонажа: " + e); _character = null; _held.Clear(); }
            }
            if (_animator != null)
            {
                string want = crouched ? "Ctrl" : _speed > 3.2f ? "Run" : _speed > 0.4f ? "Walk" : "Idle";
                if (want != _anim)
                {
                    _anim = want;
                    try { _animator.Play(want, 0); } catch { }
                }
            }
        }

        // Лежит, приходит в себя, встаёт. Своих анимаций у персонажей нет — позы собираются в GroundPose.
        // 34 «приходит в себя»: лежит на спине, колени согнуты, дышит, крутит головой, трёт лоб.
        // 37 «встаёт» (или просто перестал лежать): садится, опираясь на руки, встаёт на корточки, выпрямляется.
        private void UpdateGround(byte action, string pose, float dt)
        {
            if (action != Activity.GettingUp) _upDone = false;
            bool can = _character != null && !_inSeat && action != Activity.FellThroughIce && pose != "climb";
            if (!can || pose == "lie" && action != 34)
            {
                if (_ground) EndGround();
                return;
            }
            if (action == 34)
            {
                if (!_ground) BeginGround();
                if (_upT >= 0f) { _upT = -1f; _downT = 0f; _downFromLie = false; _gDown = _gPose; }
                _downT += dt;
                float k = _downFromLie ? Mathf.SmoothStep(0f, 1f, _downT / 0.8f) : Mathf.SmoothStep(0f, 1f, _downT / 0.5f);
                var from = _downFromLie ? GStiff : _gDown;
                var g = CharacterAnimator.Ground.Lerp(from, GRecover, k);
                float t = _downT;
                g.Back += 1.5f * Mathf.Sin(t * 1.8f) * k;                                   // дышит
                g.Turn = (22f * Mathf.Sin(t * 0.55f) + 8f * Mathf.Sin(t * 1.3f)) * k;       // крутит головой
                float ph = (t + 1.5f) % 7f;
                g.Face = (ph < 2.4f ? Mathf.Sin(Mathf.PI * ph / 2.4f) : 0f) * k;           // трёт лоб
                _gPose = g;
                return;
            }
            // Встаёт: по «встаёт», или лежал и перестал (старая версия у него, сон, ранение).
            if (!_ground)
            {
                // Подъём по 37 уже доигран, а «встаёт» ещё приходит (камера у него встаёт дольше) — второй раз не падаем.
                if (!((action == Activity.GettingUp && !_upDone) || (_lie >= 0.99f && _lieValid))) return;
                BeginGround();
            }
            // Уже бежит (дольше 0,3 с — не рывок позиции после схватки): встал без нас.
            _runT = _speed > 1.5f ? _runT + dt : 0f;
            if (_runT > 0.3f) { _upDone = true; EndGround(); return; }
            if (_upT < 0f) { _upT = 0f; _gDown = _gPose; }
            _upT += dt;
            if (_upT >= UpStand) { _upDone = true; EndGround(); return; }
            CharacterAnimator.Ground p;
            if (_upT < UpSit) p = CharacterAnimator.Ground.Lerp(_gDown, GSit, Mathf.SmoothStep(0f, 1f, _upT / UpSit));
            else if (_upT < UpCrouch) p = CharacterAnimator.Ground.Lerp(GSit, GCrouch, Mathf.SmoothStep(0f, 1f, (_upT - UpSit) / (UpCrouch - UpSit)));
            else p = CharacterAnimator.Ground.Lerp(GCrouch, GStand, Mathf.SmoothStep(0f, 1f, (_upT - UpCrouch) / (UpStand - UpCrouch)));
            // Последние 0,25 с — к клипу «стоит» целиком.
            p.W = Mathf.Clamp01((UpStand - _upT) / 0.25f);
            _gPose = p;
        }

        private void BeginGround()
        {
            _ground = true;
            _downT = 0f;
            _upT = -1f;
            _runT = 0f;
            if (_lie > 0.5f && _lieValid)
            {
                // Тело остаётся там, где лежало: таз лёжа — на 0,1 м позади «ног» позы «лежит», у нас — на 0,45 м позади корня.
                _gYaw = _lieYaw;
                _gAnchor = _lieFeet + Quaternion.Euler(0f, _gYaw, 0f) * Vector3.forward * 0.35f;
                _downFromLie = true;
                _gDown = GStiff;
            }
            else
            {
                // Схватку не видели (пришло сразу «приходит в себя»): ложится сразу с согнутыми коленями.
                _gYaw = _yaw;
                _gAnchor = new Vector3(_x, _y, _z) + Quaternion.Euler(0f, _gYaw, 0f) * Vector3.forward * 0.45f;
                _downFromLie = false;
                _gDown = GRecover;
            }
            _gPose = _gDown;
            _lie = 0f;
            _lieValid = false;
        }

        private void EndGround()
        {
            _ground = false;
            _upT = -1f;
            _gPose = default;
        }

        // Ближайший зверь (настоящий или копия) — партнёр по схватке.
        private bool _harvesting;
        private Transform _carcass;

        private static Transform NearestCarcass(Vector3 pos, float radius)
        {
            Transform best = null;
            float bestD = radius * radius;
            try
            {
                foreach (var bh in UnityEngine.Object.FindObjectsOfType<BodyHarvest>())
                {
                    if (bh == null) continue;
                    var p = bh.transform.position;
                    float dx = p.x - pos.x, dz = p.z - pos.z, d = dx * dx + dz * dz;
                    if (d < bestD && Math.Abs(p.y - pos.y) < 2f) { bestD = d; best = bh.transform; }
                }
            }
            catch { }
            return best;
        }

        private static Transform NearestAnimal(Vector3 pos, float radius)
        {
            Transform best = null;
            float bestD = radius * radius;
            try
            {
                foreach (var ai in UnityEngine.Object.FindObjectsOfType<BaseAi>())
                {
                    if (ai == null) continue;
                    var p = ai.transform.position;
                    float dx = p.x - pos.x, dz = p.z - pos.z, d = dx * dx + dz * dz;
                    if (d < bestD && Math.Abs(p.y - pos.y) < 3f) { bestD = d; best = ai.transform; }
                }
            }
            catch { }
            return best;
        }

        // Куда он идёт относительно того, куда смотрит: 0 вперёд, 90 вправо, -90 влево, ±180 назад.
        private float MoveAngle()
        {
            if (_vx * _vx + _vz * _vz < 0.04f) return 0f;
            float a = Mathf.Atan2(_vx, _vz) * Mathf.Rad2Deg - _yaw;
            return ((a % 360f) + 540f) % 360f - 180f;
        }

        private float _strikeT = -1f;
        private int _smokePhase;
        private GameObject _crankGo;   // фонарь CrankLampByEMPEROR в руке фигуры, у которого сейчас крутится ручка

        private void UpdateCharacter(string pose, string busy, bool crouched, string held, byte flags, byte shots, float dt, bool struggle)
        {
            bool aiming = (flags & HeldItems.FlagAim) != 0;
            // Во сне и на тросе вещь не показываем; в схватке в руке то, чем отбивается.
            // За разделкой руки заняты — вещь убираем; за едой и питьём в руке то, что он ест.
            bool hide = (pose == "lie" && !struggle) || pose == "ground" || pose == "climb" || busy == "harvest" || Gesture == 1 || Gesture == 5;
            _held.Set(hide ? "" : held, _root.transform);
            // Один перехват рук на ~0,6 м троса, вверх и вниз одинаково; висит — руки на месте.
            if (pose == "climb") _character.ClimbPhase += dt * Math.Min(Math.Abs(_vy), 2f) * (2f * Mathf.PI / 0.6f);
            var rule = _held.Rule;
            string upper = "";
            var mask = CharacterAnimator.Mask.None;
            if (busy != null && _character.Has(busy))
            {
                upper = busy;
                mask = CharacterAnimator.Mask.Upper;
            }
            bool fresh = _shots < 0;   // первый кадр новой фигуры: старое состояние не сравниваем
            bool shot = _shots >= 0 && (shots & 0x3F) != (_shots & 0x3F);
            if (shot && struggle) _strikeT = 0f;           // в схватке этот счётчик — удары
            else if (shot && pose == null) _held.Kick();   // выстрел: отдача
            if (_shots >= 0 && (shots & 0xC0) != (_shots & 0xC0) && pose == null)
                _character.Play("throw", CharacterAnimator.Mask.RightArm);   // бросил камень, факел, фальшфейер…
            _shots = shots;
            // Зажёг спичку — взмах рукой, как чиркнул о коробок.
            bool litNow = (flags & HeldItems.FlagLit) != 0;
            if (litNow && !_wasLit && !fresh && HeldItems.IsMatches(held) && pose == null) _character.Play("throw", CharacterAnimator.Mask.RightArm);
            _wasLit = litNow;
            bool mouthNow = (flags & HeldItems.FlagMouth) != 0;
            bool fistR = struggle || (rule != null && rule.HasRight && _held.MouthWeight < 0.5f);
            bool fistL = struggle || (rule != null && rule.HasLeft);
            _character.Gesture = pose == null && busy == null && Gesture != 0 && Gesture != Together.GestureSit ? Gesture : (byte)0;
            _character.PinchRight = rule != null && rule.Pinch;
            _character.ScissorRight = rule != null && rule.Scissor;
            _character.LooseRight = rule != null && !struggle ? rule.Loose : 0f;
            _character.LooseRightThumb = rule != null ? rule.LooseThumb : 0f;
            _character.LooseLeft = rule != null && !struggle ? rule.LooseLeft : 0f;
            _character.LooseLeftThumb = rule != null ? rule.LooseLeftThumb : 0f;
            _character.TriggerRight = rule != null && rule.Trigger && !struggle;
            // Ружьё стоя (ходит, стоит, присел) — руки по клипам rifle / rifle_aim; сидя, за делом, в схватке — как раньше.
            bool holdClip = rule != null && rule.ArmsClip != null && pose == null && !struggle && busy == null && _character.Gesture == 0 && !hide;
            if (holdClip) { _character.HoldClip = rule.ArmsClip; _character.HoldAimClip = rule.ArmsAimClip; }
            _character.HoldTarget = holdClip;
            _character.HoldAimW = _held.AimWeight;   // и пока вес клипа уходит: прицел не обрывается рывком
            _character.UpperWeight = 1f;
            _character.MoveAngle = MoveAngle();
            _character.Update(_speed, pose == "lie" || pose == "ground" ? "idle" : pose, crouched, upper, mask, fistR, fistL, dt);
            if (pose == "ground") _character.GroundPose(_gPose);
            if (upper == "harvest") _character.StraightWrists(1f);
            _character.Inject(_act == Activity.Injecting && !_inSeat, dt);   // 0.19.34: укол шприцем в бедро
            if (struggle)
            {
                if (_strikeT >= 0f) { _strikeT += dt / 0.35f; if (_strikeT >= 1f) _strikeT = -1f; }
                _character.StruggleArms(Time.time, _strikeT);
            }
            else _strikeT = -1f;
            // Смотрит вверх или вниз: стоя — всем корпусом, сидя и за делом — только головой.
            _pitch += (Pitch - _pitch) * Math.Min(1f, dt * 10f);
            if (struggle || pose == "lie" || pose == "ground" || pose == "climb") _character.AimPitch = 0f;
            else _character.LookPitch(_pitch, pose != null || busy != null);
            if (rule != null)
            {
                _held.SetLit((flags & HeldItems.FlagLit) != 0);
                // Рука занята своим (ест, пьёт, жест, схватка) — вещь в кулаке; иначе вещь от тела, руки к ней.
                bool inHand = struggle || busy != null || _character.Gesture != 0;
                _held.Pose(_character, inHand ? HeldItems.Held.Mode.Hand : HeldItems.Held.Mode.Stance, aiming && pose == null, dt, mouthNow && pose == null && !inHand);
                // Курит (SmokesByEMPEROR): фаза затяжки — мод выпускает выдох, когда рука ушла от рта.
                if (SmokesLink.IsLitSmoke(held))
                {
                    int ph = aiming ? 1 : mouthNow ? 2 : 0;
                    if (ph != _smokePhase) { _smokePhase = ph; SmokesLink.Remote(_held.Go, ph, Female == true); }
                }
                else _smokePhase = 0;
            }
            // Крутит ручку фонаря (CrankLampByEMPEROR): мод играет звук и крутит ручку у копии в руке. Сменилась вещь
            // или перестал — у прежней копии выключаем.
            var crankGo = !hide && (flags & HeldItems.FlagCrank) != 0 && CrankLampLink.IsCrankLamp(held) ? _held.Go : null;
            if (_crankGo != crankGo && _crankGo != null) CrankLampLink.Remote(_crankGo, false);
            if (crankGo != null && _crankGo != crankGo) CrankLampLink.Remote(crankGo, true);
            _crankGo = crankGo;
            _character.FinishArms();
        }

        // Волокуша, которую он тащит: едет за ним по земле.
        public void UpdateCarry(string key, bool hasPose, Vector3 rel, Quaternion rot, float dt)
        {
            if (_root == null || !_hasPos) { _carry.Clear(); return; }
            try { _carry.Update(key, new Vector3(_x, _y, _z), hasPose, rel, rot, dt); }
            catch (Exception e) { MelonLogger.Warning("[модель] волокуша: " + e.Message); _carry.Clear(); }
        }

        public Vector3 LabelPoint => new Vector3(_x, _y + 1.95f, _z);

        private GameObject BuildCharacter(int id, string character)
        {
            var def = Characters.Pick(character, id);
            if (def == null) return null;
            var shader = FindShader();
            Shader cutout = null;
            foreach (var name in new[] { "Legacy Shaders/Transparent/Cutout/Diffuse", "Transparent/Cutout/Diffuse" })
            {
                var s = Shader.Find(name);
                if (s != null && s.isSupported) { cutout = s; break; }
            }
            var go = Characters.Build(def, shader, cutout, out _character);
            Female = def.Female;
            go.name = "TLDCoop_Player_" + id;
            MelonLogger.Msg($"[модель] игрок {id}: {def.Name}");
            return go;
        }

        private GameObject BuildModel()
        {
            var go = UnityEngine.Object.Instantiate(Models.PlayerPrefab);
            go.name = "TLDCoop_PlayerModel";
            Models.FixMaterials(go, FindShader());
            foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) a.enabled = false;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
            _animator = go.GetComponent<Animator>();
            go.SetActive(true);
            return go;
        }

        private GameObject BuildCapsule(int id)
        {
            var color = Colors[id % Colors.Length];
            var root = new GameObject("TLDCoop_Player_" + id);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.SetParent(root.transform, false);
            Prepare(body, color);
            _body = body.transform;

            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visor.transform.SetParent(body.transform, false);
            visor.transform.localPosition = new Vector3(0f, 0.65f, 0.5f);
            visor.transform.localScale = new Vector3(0.5f, 0.15f, 0.4f);
            Prepare(visor, Color.white);
            return root;
        }

        private static void Prepare(GameObject go, Color color)
        {
            // Коллайдер не нужен: иначе об фигуру можно споткнуться, а звери и пули будут в неё упираться.
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var shader = FindShader();
            if (shader != null) r.material = new Material(shader);
            r.material.color = color;
        }

        // Не все шейдеры попадают в сборку игры, берём первый, что нашёлся.
        private static Shader FindShader()
        {
            if (_shader != null) return _shader;
            foreach (var name in new[] { "Standard", "Legacy Shaders/Diffuse", "Unlit/Color", "Sprites/Default", "UI/Default" })
            {
                var s = Shader.Find(name);
                if (s == null || !s.isSupported) continue;
                _shader = s;
                break;
            }
            if (!_shaderLogged)
            {
                _shaderLogged = true;
                MelonLogger.Msg("[фигура] шейдер: " + (_shader != null ? _shader.name : "не найден, будет стандартный"));
            }
            return _shader;
        }
    }
}
