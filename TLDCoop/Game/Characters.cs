using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using MelonLoader;
using UnityEngine;

namespace TLDCoop.Game
{
    // Модели игроков — персонажи сюжетного режима WINTERMUTE (Астрид, Маккензи, Иеремия…) с их анимациями
    // ходьбы, бега и покоя. Готовые объекты из пакетов эпизода игра в режиме выживания загрузить не даёт
    // (имена пакетов совпадают с её собственными), поэтому сетка, кости, текстуры и анимации заранее
    // вынуты из файлов игры в Mods/TLDCoop/characters.bin, а здесь модель собирается заново.
    public static class Characters
    {
        public const string PackPath = "Mods/TLDCoop/characters.bin";

        public class Clip
        {
            public string Name;
            public float Fps;
            public int Frames;
            public string[] Paths;
            public float[][] Rot, Pos;
            public float Length => (Frames - 1) / Fps;
        }

        public class Def
        {
            public string Id, Name;
            public bool Female;
            public string[] TName;
            public int[] TParent;
            public float[][] TLocal;
            public int RendererT;
            public Vector3[] V, N;
            public Vector2[] UV;
            public BoneWeight[] W;
            public int[] Bones;
            public Matrix4x4[] BindPoses;
            public List<(string mat, int tex, int[] idx)> Subs = new();
            public List<(int w, int h, bool alpha, byte[] data)> Texs = new();
            // Объекты Unity, созданные по первому требованию (общие для всех копий персонажа).
            public Mesh Mesh;
            public Material[] Materials;
        }

        public static readonly List<Def> Defs = new();
        private static readonly Dictionary<string, Clip> Clips = new();
        private static Task _load;
        public static string State { get; private set; } = "";
        public static bool Broken { get; private set; }   // файл есть, но не загрузился
        public static bool Ready { get; private set; }

        public static bool Available => File.Exists(PackPath);

        // Файл собирает у игрока Mods\TLDAssets.exe из WINTERMUTE его игры (в архиве мода частей игры нет).
        // Нет файла — один раз говорим об этом в игре, а не только в окне мода.
        private static bool _hintShown;
        private static float _hintAt = -1f;
        public static void HintTick()
        {
            if (_hintShown) return;
            if (!GameApi.InGame()) { _hintAt = -1f; return; }
            if (_hintAt < 0f) { _hintAt = UnityEngine.Time.unscaledTime + 6f; return; }
            if (UnityEngine.Time.unscaledTime < _hintAt) return;
            _hintShown = true;
            if (Available) return;
            try
            {
                Il2Cpp.HUDMessage.AddMessage(UI.L.T("Кооп: нет персонажей (characters.bin). Закройте игру и запустите Mods\\TLDAssets.exe",
                    "Co-op: characters missing (characters.bin). Close the game and run Mods\\TLDAssets.exe"), 10f, false, true);
            }
            catch { }
        }

        public static void StartLoad()
        {
            if (_load != null || Ready) return;
            State = UI.L.T("загружаются…", "loading…");
            _load = Task.Run(Load);
        }

        // Раз в кадр: закончилась ли загрузка.
        public static bool Poll(out string error)
        {
            error = null;
            if (_load == null || !_load.IsCompleted) return false;
            error = _load.Exception?.GetBaseException().Message;
            _load = null;
            if (error == null) Ready = true;
            Broken = error != null;
            State = error == null ? UI.L.T($"персонажи WINTERMUTE ({Defs.Count})", $"WINTERMUTE characters ({Defs.Count})") : UI.L.T("не загрузились: ", "failed to load: ") + error;
            return true;
        }

        public static Clip GetClip(string name, bool female)
        {
            if (Clips.TryGetValue(name + (female ? "_f" : "_m"), out var c)) return c;
            return Clips.TryGetValue(name, out c) ? c : null;
        }

        // Фрею заменила Джейс (0.17.4): у кого в настройках осталась Фрея, тот теперь Джейс.
        private static string Alias(string id) => id == "freya" && Defs.Exists(d => d.Id == "jace") ? "jace" : id;

        public static Def Pick(string id, int playerId)
        {
            if (Defs.Count == 0) return null;
            id = Alias(id);
            foreach (var d in Defs) if (d.Id == id) return d;
            return Defs[Math.Abs(playerId) % Defs.Count];
        }

        // Выбрал в окне мода женского или мужского персонажа — так же и у игры (голос, кукла в «Одежде»):
        // раньше в разделе одежды всегда стоял мужчина. Пока персонаж не выбран (по очереди), не трогаем.
        private static float _nextGender;

        public static void ApplyOwnGender(float now)
        {
            if (now < _nextGender) return;
            _nextGender = now + 1f;   // раз в секунду: игра сама иногда возвращает голос по своему сохранению
            try
            {
                string id = Alias(Config.Character ?? "");
                if (id.Length == 0 || !GameApi.InGame()) return;
                Def d = null;
                foreach (var x in Defs) if (x.Id == id) d = x;
                if (d == null) return;
                var want = d.Female ? Il2Cpp.VoicePersona.Female : Il2Cpp.VoicePersona.Male;
                if (Il2Cpp.PlayerManager.m_VoicePersona == want) return;
                Il2Cpp.PlayerManager.m_VoicePersona = want;
                // Сама игра при смене голоса ставит переключатель Wwise «PlayerGender» на звуке игрока —
                // без этого фразы шли прежним голосом.
                try { Il2Cpp.GameManager.GetPlayerVoiceComponent()?.SetPlayerVoicePersona(); } catch { }
                MelonLoader.MelonLogger.Msg($"[модель] свой персонаж {d.Name}: у игры теперь {(d.Female ? "женский" : "мужской")} голос и кукла в «Одежде»");
            }
            catch { }
        }

        public static int IndexOf(string id)
        {
            id = Alias(id);
            for (int i = 0; i < Defs.Count; i++) if (Defs[i].Id == id) return i;
            return -1;
        }

        // ---------- Чтение файла ----------

        private static void Load()
        {
            using var file = File.OpenRead(PackPath);
            using var z = new DeflateStream(file, CompressionMode.Decompress);
            using var r = new BinaryReader(z);
            if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "TLDC") throw new IOException("не тот файл");
            r.ReadInt32();
            int nc = r.ReadInt32();
            for (int c = 0; c < nc; c++)
            {
                var clip = new Clip { Name = Str(r), Fps = r.ReadSingle(), Frames = r.ReadInt32() };
                int nt = r.ReadInt32();
                clip.Paths = new string[nt];
                clip.Rot = new float[nt][];
                clip.Pos = new float[nt][];
                for (int t = 0; t < nt; t++)
                {
                    clip.Paths[t] = Str(r);
                    int flags = r.ReadInt32();
                    if ((flags & 1) != 0) clip.Rot[t] = Floats(r, clip.Frames * 4);
                    if ((flags & 2) != 0) clip.Pos[t] = Floats(r, clip.Frames * 3);
                }
                if (nt > 0) Clips[clip.Name] = clip;
            }
            int nd = r.ReadInt32();
            for (int k = 0; k < nd; k++)
            {
                var d = new Def { Id = Str(r), Name = Str(r), Female = r.ReadInt32() != 0 };
                int n = r.ReadInt32();
                d.TName = new string[n];
                d.TParent = new int[n];
                d.TLocal = new float[n][];
                for (int i = 0; i < n; i++)
                {
                    d.TName[i] = Str(r);
                    d.TParent[i] = r.ReadInt32();
                    d.TLocal[i] = Floats(r, 10);
                }
                d.RendererT = r.ReadInt32();
                int nv = r.ReadInt32();
                var f = Floats(r, nv * 3);
                d.V = new Vector3[nv];
                for (int i = 0; i < nv; i++) d.V[i] = new Vector3(f[i * 3], f[i * 3 + 1], f[i * 3 + 2]);
                f = Floats(r, nv * 3);
                d.N = new Vector3[nv];
                for (int i = 0; i < nv; i++) d.N[i] = new Vector3(f[i * 3], f[i * 3 + 1], f[i * 3 + 2]);
                f = Floats(r, nv * 2);
                d.UV = new Vector2[nv];
                for (int i = 0; i < nv; i++) d.UV[i] = new Vector2(f[i * 2], f[i * 2 + 1]);
                d.W = new BoneWeight[nv];
                for (int i = 0; i < nv; i++)
                {
                    var b = r.ReadBytes(4);
                    var w = Floats(r, 4);
                    d.W[i] = new BoneWeight
                    {
                        boneIndex0 = b[0], boneIndex1 = b[1], boneIndex2 = b[2], boneIndex3 = b[3],
                        weight0 = w[0], weight1 = w[1], weight2 = w[2], weight3 = w[3],
                    };
                }
                int nb = r.ReadInt32();
                d.Bones = new int[nb];
                for (int i = 0; i < nb; i++) d.Bones[i] = r.ReadInt32();
                d.BindPoses = new Matrix4x4[nb];
                for (int i = 0; i < nb; i++)
                {
                    var m = Floats(r, 16);
                    var mat = new Matrix4x4();
                    for (int row = 0; row < 4; row++)
                        for (int col = 0; col < 4; col++)
                            mat[row, col] = m[row * 4 + col];
                    d.BindPoses[i] = mat;
                }
                int ns = r.ReadInt32();
                for (int i = 0; i < ns; i++)
                {
                    string mat = Str(r);
                    int tex = r.ReadInt32();
                    int ni = r.ReadInt32();
                    var idx = new int[ni];
                    var raw = r.ReadBytes(ni * 4);
                    Buffer.BlockCopy(raw, 0, idx, 0, raw.Length);
                    d.Subs.Add((mat, tex, idx));
                }
                int nx = r.ReadInt32();
                for (int i = 0; i < nx; i++)
                {
                    int w = r.ReadInt32(), h = r.ReadInt32();
                    bool alpha = r.ReadInt32() != 0;
                    d.Texs.Add((w, h, alpha, r.ReadBytes(w * h * (alpha ? 4 : 3))));
                }
                Defs.Add(d);
            }
            MelonLogger.Msg($"[модель] персонажей {Defs.Count}, анимаций {Clips.Count}");
        }

        private static string Str(BinaryReader r) => Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));

        private static float[] Floats(BinaryReader r, int n)
        {
            var raw = r.ReadBytes(n * 4);
            var f = new float[n];
            Buffer.BlockCopy(raw, 0, f, 0, raw.Length);
            return f;
        }

        // ---------- Сборка модели ----------

        // Модель целиком: кости (пустые объекты), кожа на них и проигрыватель анимаций.
        public static GameObject Build(Def d, Shader shader, Shader cutout, out CharacterAnimator anim)
        {
            var root = new GameObject("TLDCoop_Character_" + d.Id);
            var ts = new Transform[d.TName.Length];
            for (int i = 0; i < ts.Length; i++)
            {
                var go = new GameObject(d.TName[i]);
                ts[i] = go.transform;
                ts[i].SetParent(d.TParent[i] >= 0 ? ts[d.TParent[i]] : root.transform, false);
                var l = d.TLocal[i];
                ts[i].localPosition = new Vector3(l[0], l[1], l[2]);
                ts[i].localRotation = new Quaternion(l[3], l[4], l[5], l[6]);
                ts[i].localScale = new Vector3(l[7], l[8], l[9]);
            }
            EnsureAssets(d, shader, cutout);
            var smr = ts[d.RendererT].gameObject.AddComponent<SkinnedMeshRenderer>();
            var bones = new Transform[d.Bones.Length];
            for (int i = 0; i < bones.Length; i++) bones[i] = d.Bones[i] >= 0 ? ts[d.Bones[i]] : root.transform;
            smr.bones = bones;
            smr.rootBone = ts[0];
            smr.sharedMesh = d.Mesh;
            smr.sharedMaterials = d.Materials;
            smr.updateWhenOffscreen = true;
            anim = new CharacterAnimator(d, ts);
            return root;
        }

        private static void EnsureAssets(Def d, Shader shader, Shader cutout)
        {
            if (d.Mesh != null && d.Materials != null) return;
            // DontUnloadUnusedAsset: при загрузке новой сцены (вышел из дома на улицу) Unity выгружает «ничейные»
            // меш, текстуры и материалы. Наши созданы в коде и держатся только ссылкой мода — без флага их
            // уничтожало, и после первой смены сцены персонаж больше не собирался (простая фигура).
            var mesh = new Mesh { name = "TLDCoop_" + d.Id, hideFlags = HideFlags.DontUnloadUnusedAsset };
            if (d.V.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = d.V;
            mesh.normals = d.N;
            mesh.uv = d.UV;
            mesh.boneWeights = d.W;
            mesh.bindposes = d.BindPoses;
            mesh.subMeshCount = d.Subs.Count;
            for (int i = 0; i < d.Subs.Count; i++) mesh.SetTriangles(d.Subs[i].idx, i);
            mesh.RecalculateBounds();
            d.Mesh = mesh;

            var texs = new Texture2D[d.Texs.Count];
            for (int i = 0; i < texs.Length; i++)
            {
                var t = d.Texs[i];
                var tex = new Texture2D(t.w, t.h, t.alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
                tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
                tex.LoadRawTextureData(t.data);
                tex.Apply(false, true);
                texs[i] = tex;
            }
            d.Materials = new Material[d.Subs.Count];
            for (int i = 0; i < d.Subs.Count; i++)
            {
                int ti = d.Subs[i].tex;
                bool alpha = ti >= 0 && d.Texs[ti].alpha;
                var m = new Material(alpha && cutout != null ? cutout : shader) { name = d.Subs[i].mat, hideFlags = HideFlags.DontUnloadUnusedAsset };
                if (ti >= 0)
                {
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texs[ti]);
                    m.mainTexture = texs[ti];
                }
                if (m.HasProperty("_Color")) m.color = Color.white;
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
                if (alpha && m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.4f);
                d.Materials[i] = m;
            }
            // Сырые данные больше не нужны: всё уже в объектах Unity.
            d.V = null; d.N = null; d.UV = null; d.W = null;
            d.Texs = d.Texs.ConvertAll(t => (t.w, t.h, t.alpha, (byte[])null));
        }
    }

    // Проигрыватель анимаций персонажа. Анимации записаны как повороты костей по кадрам, их и ставим костям сами.
    // Два слоя: основа на всё тело (стоит / идёт / бежит / сидит / на стуле) с плавным переходом
    // и верх поверх неё (ружьё в руках, фонарь в поднятой руке, выстрел) — только на нужные кости,
    // поэтому с ружьём можно идти: ноги от ходьбы, руки от ружья. Плюс сжатые пальцы той руки, что держит вещь.
    public class CharacterAnimator
    {
        private class Bound
        {
            public Characters.Clip Clip;
            public Transform[] T;
            public bool[] UsePos;
            public string[] Paths;
        }

        // Какие кости берёт верхний слой.
        public enum Mask { None, Upper, RightArm, LeftArm }

        private readonly Characters.Def _def;
        private readonly Dictionary<string, Transform> _byPath = new();
        private readonly List<(Transform t, Quaternion rest)> _rightFingers = new();   // пальцы правой руки и их поза у модели (прямые)
        private readonly List<(Transform t, Quaternion rest)> _leftFingers = new();
        private readonly Dictionary<string, Bound> _bound = new();
        private Bound _cur, _prev;
        private float _curT, _prevT, _blend = 1f;
        private bool _curRev, _prevRev;   // клип идёт задом наперёд (шаг назад)
        private string _state = "", _base = "idle";
        private float _cw;
        private static bool IsCrouchState(string s) => s == "cidle" || s == "cwalk" || s == "cback" || s == "cside" || s == "csidel";

        // Куда он идёт относительно взгляда: 0 вперёд, 90 вправо, -90 влево, ±180 назад (ставит RemotePlayer).
        // У персонажей WINTERMUTE есть только ходьба вперёд. Назад — та же ходьба задом наперёд, вбок — шаг вбок,
        // ноги которого ставим сами. Наискосок — ближайшее из четырёх, а таз с ногами доворачиваем на остаток угла
        // (верх тела при этом смотрит туда же, куда игрок).
        public float MoveAngle;
        private float _side, _sideDir = 1f, _sidePhase, _twist;

        private Bound _over;          // верхний слой
        private string _overName = "";
        private Mask _overMask;
        private float _overT, _overW;
        private Bound _shot;          // разовая анимация поверх верхнего слоя (выстрел, бросок)
        private Mask _shotMask;
        private float _shotT;

        public Transform RightHand { get; }
        public Transform LeftHand { get; }

        // Щепоть правой руки (спичка): средний, безымянный и мизинец в кулаке, большой и указательный почти прямые.
        public bool PinchRight;
        // Сигарета, сигара (11-10): зажата между указательным и средним у их второго сустава; эти два пальца почти прямые,
        // безымянный и мизинец согнуты, большой расслаблен. Ось вещи идёт сквозь щель между пальцами (от ладони к тыльной стороне).
        public bool ScissorRight;
        public float ScissorAim;   // 0 — покой (сигарета поперёк пальцев), 1 — затяжка (хват как в 0.19.58, понравился)
        // Крупная вещь (камень): пальцы правой руки разжаты на эту долю от кулака к прямым, большой — на свою.
        public float LooseRight, LooseRightThumb;
        public float LooseLeft, LooseLeftThumb;     // то же для левой (ружьё: цевьё на полураскрытой ладони)
        // Руки держат вещь, как в анимации людей из игры (ружьё: клипы rifle у пояса и rifle_aim в прицел). Плечи,
        // локти и кисти берутся из клипа целиком, вещь ставится по правой кисти (Game/HeldItems.cs). Раньше руки
        // тянулись к вещи решателем, и левый локоть уходил внутрь, а кисть на цевье выгибалась вверх.
        // HoldTarget ставит RemotePlayer (держит ружьё стоя или нет), HoldW идёт к нему плавно за 0,25 с: сел/встал,
        // жест, взял ружьё — руки и ружьё переходят между клипом и старой позой без рывка. Имена клипов остаются,
        // пока вес уходит в ноль.
        public string HoldClip, HoldAimClip;
        public bool HoldTarget;
        public float HoldW { get; private set; }
        public float HoldAimW;
        private float _holdT;

        // Наклон спины в приседе (Crouch) и сколько из него сейчас: ружьё по клипу выпрямляет грудь обратно.
        public const float CrouchChest = 20f;
        public float CrouchAmount => Mathf.SmoothStep(0f, 1f, _crouch);

        // Грудь (и всё выше: руки, голова) вперёд-вниз на deg вокруг оси «вправо» персонажа (минус — назад), шея обратно
        // на headBack: голова остаётся смотреть, куда смотрела.
        public void BendChest(float deg, float headBack)
        {
            if (_root == null || _spine3 == null) return;
            var right = _root.right;
            if (Math.Abs(deg) > 0.01f) _spine3.rotation = Quaternion.AngleAxis(deg, right) * _spine3.rotation;
            var h = _neck != null ? _neck : _head;
            if (h != null && Math.Abs(headBack) > 0.01f) h.rotation = Quaternion.AngleAxis(-headBack, right) * h.rotation;
        }
        public bool TriggerRight;   // ружьё, револьвер: указательный правой почти выпрямлен — на спуске
        public float UpperWeight = 1f;   // вес верхнего клипа (поза руки с вещью): меньше 1 — рука ниже, у груди
        private Transform _indexTip, _thumbTip;
        public bool PinchPoint(out Vector3 at)
        {
            at = default;
            if (_indexTip == null || _thumbTip == null) return false;
            at = (_indexTip.position + _thumbTip.position) * 0.5f;
            return true;
        }

        // ---------- Руки к вещи ----------
        // Вещь ставится от тела (Game/HeldItems.cs), а кисть сама берётся за её ручку: рука сгибается в плече
        // и локте (две кости, локоть смотрит туда, куда скажут), кисть разворачивается так, чтобы ручка
        // прошла сквозь кулак. Раньше было наоборот: вещь крепилась к кулаку под своим углом и «торчала» из него.

        public enum Anchor { ShoulderR, ShoulderL, HipR, HipL, Chest }

        public Transform AnchorBone(Anchor a) => a switch
        {
            Anchor.ShoulderR => _armR, Anchor.ShoulderL => _armL, Anchor.HipR => _hipR, Anchor.HipL => _hipL, _ => _spine3,
        };

        public Transform Root => _root;
        public bool HasArms => _root != null && _armR != null && _armL != null && _elbowR != null && _elbowL != null && RightHand != null && LeftHand != null;

        // Корпус доворачивается вправо (+) вокруг вертикали, голова остаётся смотреть вперёд: так держат ружьё и лук.
        // keepHead = false: голова поворачивается вместе с корпусом (ружьё по клипу: щека остаётся на прикладе).
        public void TwistTorso(float deg, bool keepHead = true)
        {
            if (_root == null || _spine == null || Math.Abs(deg) < 0.1f) return;
            var up = _root.up;
            _spine.rotation = Quaternion.AngleAxis(deg * 0.3f, up) * _spine.rotation;
            if (_spine2 != null) _spine2.rotation = Quaternion.AngleAxis(deg * 0.3f, up) * _spine2.rotation;
            if (_spine3 != null) _spine3.rotation = Quaternion.AngleAxis(deg * 0.4f, up) * _spine3.rotation;
            if (!keepHead) return;
            if (_neck != null) _neck.rotation = Quaternion.AngleAxis(-deg * 0.6f, up) * _neck.rotation;
            if (_head != null) _head.rotation = Quaternion.AngleAxis(-deg * 0.4f, up) * _head.rotation;
        }

        public Transform Head => _head;

        // Наклон взгляда: + вниз, − вверх (как у камеры игрока). Целиком — спина, шея и голова;
        // headOnly — только шея и голова (сидит, разделывает). AimPitch — на сколько повернуть ружьё в прицеле.
        public float AimPitch;

        public void LookPitch(float deg, bool headOnly)
        {
            AimPitch = 0f;
            if (_root == null || _head == null || Math.Abs(deg) < 0.5f) return;
            var right = _root.right;
            if (headOnly)
            {
                deg = Mathf.Clamp(deg, -45f, 45f);
                if (_neck != null) _neck.rotation = Quaternion.AngleAxis(deg * 0.4f, right) * _neck.rotation;
                _head.rotation = Quaternion.AngleAxis(deg * (_neck != null ? 0.5f : 0.9f), right) * _head.rotation;
                return;
            }
            deg = Mathf.Clamp(deg, -70f, 70f);
            AimPitch = deg;
            if (_spine != null) _spine.rotation = Quaternion.AngleAxis(deg * 0.12f, right) * _spine.rotation;
            if (_spine2 != null) _spine2.rotation = Quaternion.AngleAxis(deg * 0.18f, right) * _spine2.rotation;
            if (_spine3 != null) _spine3.rotation = Quaternion.AngleAxis(deg * 0.2f, right) * _spine3.rotation;
            if (_neck != null) _neck.rotation = Quaternion.AngleAxis(deg * 0.2f, right) * _neck.rotation;
            _head.rotation = Quaternion.AngleAxis(deg * 0.3f, right) * _head.rotation;
        }

        // Щекой к прикладу: голова чуть вниз и вправо.
        public void CheekWeld(float w)
        {
            if (_root == null || _head == null || w <= 0f) return;
            _head.rotation = Quaternion.AngleAxis(12f * w, _root.right) * Quaternion.AngleAxis(-10f * w, _root.forward) * _head.rotation;
        }

        public Transform EyeR => _eyeR;

        // Плечи для ружья в прицел (обе ключицы в одну сторону вокруг вертикали персонажа): правое вперёд на right° —
        // затыльник упирается в плечо; левое назад на left° — в клипе оно далеко впереди, рука до цевья вытягивалась
        // и гнула запястье (так левый локоть ниже и ближе под цевьё). Плечевой пояс становится чуть менее боком.
        public void SquareShoulders(float right, float left)
        {
            if (_root == null) return;
            if (_clavR != null && Math.Abs(right) > 0.01f) _clavR.rotation = Quaternion.AngleAxis(-right, _root.up) * _clavR.rotation;
            if (_clavL != null && Math.Abs(left) > 0.01f) _clavL.rotation = Quaternion.AngleAxis(-left, _root.up) * _clavL.rotation;
        }

        // Щекой на приклад: голова чуть набок к прикладу (roll° вокруг ствола — челюсть и борода уходят с гребня влево),
        // потом шея (в два шага, до 20° за шаг) доводит правый глаз на линию прицела (sight — точка на ней, fwd — вдоль
        // ствола), на drop ниже неё. w — вес (прицел, клип, руки на ружье). Затыльник ставится по высоте и вбок так,
        // чтобы глаз и без того был над прицелом (HeldItems.ButtPlace), — шее остаётся 2–10°. Раньше голова
        // наклонялась на 15° и шея доводила глаз сбоку: с наклоном самого клипа (21–22°) выходило 37–40°, голова
        // лежала на плече; теперь 24–26°.
        public void CheekOnStock(Vector3 sight, Vector3 fwd, Vector3 up, float drop, float roll, float w)
        {
            if (_head == null || _neck == null || _eyeR == null || w <= 0.001f) return;
            fwd = fwd.normalized; up = up.normalized;
            if (Math.Abs(roll) > 0.01f) _head.rotation = Quaternion.AngleAxis(-roll * w, fwd) * _head.rotation;
            for (int i = 0; i < 2; i++)
            {
                var e = _eyeR.position; var n = _neck.position;
                var want = sight + fwd * Vector3.Dot(e - sight, fwd) - up * drop;
                var q = Quaternion.FromToRotation(e - n, want - n);
                q.ToAngleAxis(out float ang, out var axis);
                if (ang > 180f) ang -= 360f;
                if (float.IsNaN(ang) || axis.sqrMagnitude < 0.5f || Math.Abs(ang) < 0.01f) break;
                _neck.rotation = Quaternion.AngleAxis(Mathf.Clamp(ang, -20f, 20f) * w, axis) * _neck.rotation;
            }
        }

        // Как кулак держит ручку сейчас: точка внутри кулака, ось ручки (от указательного к мизинцу)
        // и куда смотрит ладонь (от кисти к ручке). Всё в мире.
        public bool HandGrip(bool right, bool pinch, out Vector3 at, out Vector3 hole, out Vector3 palm)
        {
            at = hole = palm = default;
            var hand = right ? RightHand : LeftHand;
            var i1 = right ? _idx1R : _idx1L; var p1 = right ? _pinky1R : _pinky1L;
            var m1 = right ? _mid1R : _mid1L; var m3 = right ? _mid3R : _mid3L;
            if (hand == null || i1 == null || p1 == null || m1 == null || m3 == null) return false;
            hole = (p1.position - i1.position).normalized;
            var v = m3.position - m1.position;
            palm = (v - Vector3.Dot(v, hole) * hole).normalized;
            at = (m1.position + m3.position) * 0.5f;
            if (pinch && right && ScissorRight && _idx2R != null && _mid2R != null)
            {
                // 11-10 (3): сигарета идёт поперёк пальцев сквозь щель между указательным и средним: фильтр со стороны
                // ладони, огонёк с тыльной. Раньше её ось шла вдоль пальцев — сигарета пряталась в пальцах.
                at = (_idx2R.position + _mid2R.position) * 0.5f;
                float sa = Mathf.Clamp01(ScissorAim);
                if (_idx3R != null && _mid3R != null) at = Vector3.Lerp(at, (_idx3R.position + _mid3R.position) * 0.5f, Mathf.Lerp(0.3f, 0.6f, sa));
                var fingers = palm;                                       // от основания к кончикам
                var knuckles = hole;                                      // от указательного к мизинцу
                var palmSide = Vector3.Cross(knuckles, fingers).normalized;   // сторона ладони
                // 11-10 (4): на затяжке хват как в 0.19.58 (ось вдоль пальцев) — так рука шла к лицу правильно
                hole = Vector3.Slerp(palmSide, fingers, sa).normalized;
                palm = Vector3.Slerp(fingers, knuckles, sa).normalized;
                return hole.sqrMagnitude > 0.5f && palm.sqrMagnitude > 0.5f;
            }
            if (pinch)
            {
                var it = right ? _indexTip : _idx4L; var th = right ? _thumbTip : _thumb3L;
                if (it != null && th != null) at = (it.position + th.position) * 0.5f;
            }
            return hole.sqrMagnitude > 0.5f && palm.sqrMagnitude > 0.5f;
        }

        // Взяться кистью за ручку: at — точка ручки, hole — её ось (от указательного к мизинцу), palm — куда смотрит
        // ладонь, pole — куда смотрит локоть. w — насколько (0 — рука как в анимации). Вернёт false, если не дотянулся.
        // search: кисть сама выбирает, как повернуться вокруг ручки (palm — лишь пожелание), а локоть встаёт туда,
        // откуда предплечье входит в кисть прямо, как в анимации: запястье не выворачивается. aim — при прицеливании
        // локоть можно поднять до плеча и сильнее держаться подсказки pole. roll = false — кисть не поворачивается вокруг
        // ручки (ладонь ровно по palm), ищется только локоть: у револьвера пальцы обхватывают рукоять по её форме, и
        // поворот кисти на 10–30° (смотрит вниз, присел) уводил их обратно внутрь рукояти.
        public bool Grip(bool right, Vector3 at, Vector3 hole, Vector3 palm, Vector3 pole, float w, bool pinch = false, bool search = false, bool aim = false, float palmWeight = 0.3f, bool roll = true)
        {
            if (w <= 0f || !HasArms) return true;
            var up = right ? _armR : _armL; var el = right ? _elbowR : _elbowL; var hand = right ? RightHand : LeftHand;
            if (!HandGrip(right, pinch, out var gAt, out var gHole, out var gPalm)) return true;
            if (hole.sqrMagnitude < 1e-6f || palm.sqrMagnitude < 1e-6f) return true;
            var r0 = hand.rotation;
            var inv = Quaternion.Inverse(Quaternion.LookRotation(gHole, gPalm));
            // Поворот кисти: её ось ручки и ладонь ложатся на нужные.
            var turn = Quaternion.LookRotation(hole, palm) * inv;
            var wrist = at - turn * (gAt - hand.position);
            var elbowPole = pole;
            if (search)
            {
                var fore = (hand.position - el.position).normalized;   // предплечье в анимации
                PickTurn(right, up, el, hand, at, hole, palm, pole, gAt, inv, aim, palmWeight, roll, ref turn, ref wrist, ref elbowPole);
                elbowPole = Unclip(right, up.position, (el.position - up.position).magnitude, (hand.position - el.position).magnitude, wrist, elbowPole, turn * fore, aim);
                // Перебор идёт шагами по 10°: на границе выбора кисть и локоть перескакивали туда-обратно, и вещь
                // в руках дрожала. Сглаживаем по времени (~0,07 с); точка хвата остаётся точно на месте.
                SmoothArm(right, ref turn, ref elbowPole);
                wrist = at - turn * (gAt - hand.position);
            }
            var handRot = turn * r0;
            var q0u = up.localRotation; var q0e = el.localRotation; var q0h = hand.localRotation;
            bool ok = TwoBone(up, el, hand, wrist, elbowPole);
            hand.rotation = handRot;
            if (w < 1f)
            {
                up.localRotation = Quaternion.Slerp(q0u, up.localRotation, w);
                el.localRotation = Quaternion.Slerp(q0e, el.localRotation, w);
                hand.localRotation = Quaternion.Slerp(q0h, hand.localRotation, w);
            }
            return ok;
        }

        // Кисть переносится так, чтобы точка хвата встала в at, и поворачивается на turn (вместе с вещью). Локоть
        // остаётся в своей плоскости, а кисть — как была относительно вещи: поза из анимации почти не меняется.
        // keep (0..1) — насколько локоть ставить так, чтобы предплечье шло относительно кисти, как в анимации
        // (запястье не гнётся), а не в прежней плоскости руки: когда вещь уехала далеко от позы клипа (ружьё в прицел
        // затыльником в плечо), прежняя плоскость задирала левый локоть до плеча и гнула левое запястье на 30–36°.
        public void Follow(bool right, Vector3 at, Quaternion turn, float w = 1f, float keep = 0f)
        {
            if (!HasArms || w <= 0.001f) return;
            var up = right ? _armR : _armL; var el = right ? _elbowR : _elbowL; var hand = right ? RightHand : LeftHand;
            if (!HandGrip(right, false, out var gAt, out _, out _)) return;
            var hp = hand.position;
            var handRot = turn * hand.rotation;
            var wrist = at - turn * (gAt - hp);
            if ((wrist - hp).sqrMagnitude < 1e-8f && Quaternion.Angle(handRot, hand.rotation) < 0.05f) return;
            // Вес: рука въезжает на вещь плавно (взял в руки, кончился жест), а не прыгает за кадр.
            if (w < 0.999f)
            {
                wrist = Vector3.Lerp(hp, wrist, w);
                handRot = Quaternion.Slerp(hand.rotation, handRot, w);
            }
            var pole = el.position - (up.position + wrist) * 0.5f;
            if (keep > 0.001f)
            {
                // Где был бы локоть, если предплечье относительно кисти — как сейчас (в позе клипа): к нему и тянем.
                var fore = Quaternion.Inverse(hand.rotation) * (hp - el.position);
                var keepPole = wrist - handRot * fore - (up.position + wrist) * 0.5f;
                pole = Vector3.Lerp(pole.normalized, keepPole.normalized, Mathf.Clamp01(keep));
            }
            TwoBone(up, el, hand, wrist, pole);
            hand.rotation = handRot;
        }

        // Одной рукой в прицел (револьвер): рука вытянута от плеча вдоль линии arm, локоть чуть согнут (elbowBend)
        // и смотрит вниз-наружу (outDir), запястье почти прямое — предплечье идёт так же, как в анимации относительно
        // кисти, с изгибом не больше maxBend (к линии руки). shift — насколько сдвинуть вещь, чтобы ручка оказалась
        // в такой кисти, elbow — где тогда локоть. Считать до того, как рука взялась за вещь: предплечье берётся из позы
        // анимации. В 0.19.3 рука шла к вещи, поставленной от плеча, и при такой ручке предплечье смотрело на 50° влево:
        // револьвер оказывался перед серединой груди, а плечо уходило в туловище.
        public bool WristShift(bool right, Vector3 at, Vector3 hole, Vector3 palm, Vector3 arm, float maxBend, float elbowBend, Vector3 outDir, out Vector3 shift, out Vector3 elbow)
        {
            shift = Vector3.zero; elbow = Vector3.zero;
            if (!HasArms || hole.sqrMagnitude < 1e-6f || palm.sqrMagnitude < 1e-6f || arm.sqrMagnitude < 1e-6f) return false;
            var up = right ? _armR : _armL; var el = right ? _elbowR : _elbowL; var hand = right ? RightHand : LeftHand;
            if (!HandGrip(right, false, out var gAt, out var gHole, out var gPalm)) return false;
            Vector3 s = up.position, e = el.position, h = hand.position;
            float a = (e - s).magnitude, b = (h - e).magnitude;
            if (a < 1e-4f || b < 1e-4f) return false;
            var turn = Quaternion.LookRotation(hole, palm) * Quaternion.Inverse(Quaternion.LookRotation(gHole, gPalm));
            var d = turn * ((h - e) / b);                    // предплечье, как в анимации, при такой кисти
            arm.Normalize();
            float ang = Vector3.Angle(d, arm);
            if (ang > 1e-3f && maxBend > 0f)
            {
                var axis = Vector3.Cross(d, arm);
                if (axis.sqrMagnitude > 1e-8f) d = Quaternion.AngleAxis(Mathf.Min(maxBend, ang), axis.normalized) * d;
            }
            // Плечо — от предплечья на elbowBend в сторону outDir: локоть чуть согнут вниз-наружу, а не в грудь.
            var n = outDir - Vector3.Dot(outDir, d) * d;
            if (n.sqrMagnitude < 1e-8f)
            {
                // Предплечье смотрит прямо по outDir (почти не бывает): локоть тогда в сторону от тела, а не отказ —
                // иначе вещь встала бы по Off, и рука снова ушла бы в грудь.
                var side = _root != null ? (right ? _root.right : -_root.right) : (right ? Vector3.right : Vector3.left);
                n = side - Vector3.Dot(side, d) * d;
                if (n.sqrMagnitude < 1e-8f) return false;
            }
            n.Normalize();
            float t = elbowBend * Mathf.Deg2Rad;
            elbow = s + (d * Mathf.Cos(t) + n * Mathf.Sin(t)) * a;
            var wrist = elbow + d * b;
            shift = wrist + turn * (gAt - h) - at;
            return true;
        }

        // Одной рукой в прицел (револьвер): кисть повёрнута против предплечья больше чем на start° — долю share лишнего
        // забирает плечо: вся рука поворачивается вокруг линии плечо–запястье (локоть почти прямой, сдвигается на 1–2 см),
        // кисть остаётся как была. В приседе, глядя вверх, кисть была повёрнута на 75–85°, у LimitWristTwist (85°) не
        // оставалось запаса; теперь не больше ~45°. Вызывать после хвата, до FinishArms.
        public void EaseTwist(bool right, float start, float share, float w)
        {
            if (!HasArms || w <= 0.001f) return;
            var up = right ? _armR : _armL; var hand = right ? RightHand : LeftHand;
            if (up == null || hand == null) return;
            float t = HandTwist(hand, right ? _handBindR : _handBindL, out _);
            float ex = Mathf.Max(0f, Mathf.Abs(t) - start) * share * Mathf.Sign(t) * Mathf.Clamp01(w);
            if (Mathf.Abs(ex) < 0.1f) return;
            var line = hand.position - up.position;
            if (line.sqrMagnitude < 1e-6f) return;
            var hr = hand.rotation;
            up.rotation = Quaternion.AngleAxis(ex, line.normalized) * up.rotation;
            hand.rotation = hr;
        }

        // Одной рукой у бедра (револьвер): плечо висит вдоль hang (почти вниз, чуть вперёд и наружу), предплечье идёт так же,
        // как в анимации относительно кисти, — запястье прямое. shift — насколько сдвинуть вещь, чтобы ручка оказалась
        // в такой кисти, elbow — где тогда локоть. Считать до того, как рука взялась за вещь (по позе анимации). Раньше
        // вещь стояла от бедра, и рука до неё тянулась: глядя вниз, рука не доставала (ручка проходила сквозь кисть),
        // запястье заламывалось на 25–40°, а в приседе револьвер упирался в бедро или в грудь.
        public bool HangShift(bool right, Vector3 at, Vector3 hole, Vector3 palm, Vector3 hang, out Vector3 shift, out Vector3 elbow)
        {
            shift = Vector3.zero; elbow = Vector3.zero;
            if (!HasArms || hole.sqrMagnitude < 1e-6f || palm.sqrMagnitude < 1e-6f || hang.sqrMagnitude < 1e-6f) return false;
            var up = right ? _armR : _armL; var el = right ? _elbowR : _elbowL; var hand = right ? RightHand : LeftHand;
            if (!HandGrip(right, false, out var gAt, out var gHole, out var gPalm)) return false;
            Vector3 s = up.position, e = el.position, h = hand.position;
            float a = (e - s).magnitude, b = (h - e).magnitude;
            if (a < 1e-4f || b < 1e-4f) return false;
            var turn = Quaternion.LookRotation(hole, palm) * Quaternion.Inverse(Quaternion.LookRotation(gHole, gPalm));
            var d = turn * ((h - e) / b);
            elbow = s + hang.normalized * a;
            shift = elbow + d * b + turn * (gAt - h) - at;
            return true;
        }

        // Указательный правой на спуск: выпрямить его (основание полностью, остальные суставы на 70%) и повернуть
        // от основания так, чтобы кончик лёг на точку спуска. Вызывается после того, как рука встала на ружьё.
        // Из кулака палец смотрит назад, к ладони, поэтому поворот до 140° (проверено на макете, hold2/rifle_f.py).
        public void AimIndex(Vector3 target, float w)
        {
            if (w <= 0.001f || RightHand == null || _rightFingers.Count == 0) return;
            Transform i1 = null, i2 = null, i3 = null, i4 = null;
            Quaternion r2 = Quaternion.identity, r3 = Quaternion.identity;
            foreach (var (t, rest) in _rightFingers)
            {
                if (t.name.Contains("Index1")) { i1 = t; t.localRotation = Quaternion.Slerp(t.localRotation, rest, w); }
                else if (t.name.Contains("Index2")) { i2 = t; r2 = rest; }
                else if (t.name.Contains("Index3")) { i3 = t; r3 = rest; }
                else if (t.name.Contains("Index4")) i4 = t;
            }
            if (i1 == null || i4 == null) return;
            // Сгиб пальца под расстояние до спуска: от прямого (0) через нынешний (1) к вдвое согнутому (2) —
            // выбираем, при каком кончик дальше всего не перелетает спуск. Короткий палец почти прямой, длинный согнут.
            if (i2 != null && i3 != null)
            {
                Quaternion c2 = i2.localRotation, c3 = i3.localRotation;
                float need = (target - i1.position).magnitude, bestErr = float.MaxValue, best = 0.7f;
                for (int k = 0; k <= 10; k++)
                {
                    float c = k * 0.2f;
                    i2.localRotation = Quaternion.SlerpUnclamped(r2, c2, c);
                    i3.localRotation = Quaternion.SlerpUnclamped(r3, c3, c);
                    float err = Mathf.Abs((i4.position - i1.position).magnitude - need);
                    if (err < bestErr) { bestErr = err; best = c; }
                }
                i2.localRotation = Quaternion.Slerp(c2, Quaternion.SlerpUnclamped(r2, c2, best), w);
                i3.localRotation = Quaternion.Slerp(c3, Quaternion.SlerpUnclamped(r3, c3, best), w);
            }
            var from = i4.position - i1.position;
            var to = target - i1.position;
            if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) return;
            var q = Quaternion.FromToRotation(from, to);
            q.ToAngleAxis(out float ang, out var axis);
            if (ang > 180f) ang -= 360f;
            ang = Mathf.Clamp(ang, -140f, 140f) * w;
            i1.rotation = Quaternion.AngleAxis(ang, axis) * i1.rotation;
        }

        // Средний, безымянный, мизинец и большой правой обхватывают рукоять (револьвер): рукоять — эллиптический цилиндр
        // (центр c, ось axis вдоль рукояти, side — к правой щёчке, полуоси rs вбок и rf вперёд-назад, taper — насколько
        // rf растёт и центр уходит вперёд на метр вдоль оси: рукоять книзу шире). Пальцы начинают прямыми и сгибаются
        // по суставам от основания к кончику: каждый сустав — пока фаланги дальше него не упрутся в рукоять (толщина
        // пальца rad). Сгиб — по тому же пути, что от прямых пальцев к кулаку, не больше 1,25 кулака. Раньше пальцы
        // стояли кулаком, и рукоять (4,3 × 8 см) проходила сквозь кулак, пальцы торчали с другой стороны. Большой —
        // так же (у него за последним суставом ещё фаланга, её конец берём по длине предыдущей): ложится через верх
        // рукояти на левый бок рамки, а не уходит в неё на 1,5 см.
        // Найденный сгиб сглаживается по времени (~0,07 с): палец, который только что касался рукояти, при сдвиге кисти
        // на волосок мог промахнуться и согнуться до кулака — кончик прыгал на 5–8 см за кадр.
        // Вызывать после того, как рука взялась за вещь, и до AimIndex.
        private Transform[][] _wrapChains;
        private readonly float[,] _wrapT = new float[4, 3];
        private int _wrapFrame = -10;

        public void WrapFingers(Vector3 c, Vector3 axis, Vector3 side, float rs, float rf, float taper, float rad, float w)
        {
            if (w <= 0.001f || RightHand == null || _rightFingers.Count == 0) return;
            if (_wrapChains == null)
            {
                var names = new[] { "Middle", "Ring", "Pinky", "Thumb" };
                _wrapChains = new Transform[names.Length][];
                for (int f = 0; f < names.Length; f++)
                {
                    int len = names[f] == "Thumb" ? 3 : 4;
                    var ch = new Transform[len];
                    foreach (var (t, _) in _rightFingers)
                        for (int k = 0; k < len; k++)
                            if (t.name.Contains(names[f] + (k + 1))) ch[k] = t;
                    _wrapChains[f] = Array.Exists(ch, t => t == null) ? null : ch;
                }
            }
            axis.Normalize();
            side = (side - Vector3.Dot(side, axis) * axis).normalized;
            var front = Vector3.Cross(axis, side);
            float minR = Mathf.Min(rs, rf);
            float Depth(Vector3 p)
            {
                var q = p - c;
                float t = Vector3.Dot(q, axis);
                float x = Vector3.Dot(q, side) / (rs + rad), y = (Vector3.Dot(q, front) - taper * t) / (Mathf.Max(0.005f, rf + taper * t) + rad);
                return (1f - Mathf.Sqrt(x * x + y * y)) * minR;   // > 0 — внутри, м
            }
            const float tol = 0.003f, step = 0.05f;
            int frame = Time.frameCount;
            bool smooth = frame - _wrapFrame <= 2 && frame != _wrapFrame;
            float ks = 1f - Mathf.Exp(-Time.deltaTime * 14f);
            var rest = new Quaternion[3]; var cur = new Quaternion[3]; var pts = new Vector3[4];
            for (int f = 0; f < _wrapChains.Length; f++)
            {
                var chain = _wrapChains[f];
                if (chain == null) continue;
                bool thumb = chain.Length == 3;
                float tMax = 1.25f / Mathf.Max(0.2f, 1f - (thumb ? LooseRightThumb : LooseRight));
                for (int k = 0; k < 3; k++)
                {
                    cur[k] = chain[k].localRotation;
                    rest[k] = RestOf(chain[k]);
                    chain[k].localRotation = rest[k];
                }
                for (int k = 0; k < 3; k++)
                {
                    // Сустав k: самый большой сгиб, при котором фаланги дальше него не входят в рукоять глубже,
                    // чем на допуск. Если они уже внутри у прямого пальца — ищем, где выходят, иначе где меньше всего.
                    float best = -1f, minDep = float.MaxValue, minT = 0f;
                    for (float t = 0f; t <= tMax + 1e-4f; t += step)
                    {
                        chain[k].localRotation = Quaternion.SlerpUnclamped(rest[k], cur[k], t);
                        for (int j = 0; j < 3; j++) pts[j] = chain[j].position;
                        pts[3] = thumb ? chain[2].TransformPoint(chain[2].localPosition * 0.9f) : chain[3].position;
                        float dep = float.MinValue;
                        for (int j = k; j < 3; j++)
                            for (int u = 1; u <= 4; u++) dep = Mathf.Max(dep, Depth(Vector3.Lerp(pts[j], pts[j + 1], u * 0.25f)));
                        if (dep < minDep) { minDep = dep; minT = t; }
                        if (dep <= tol) best = t;
                        else if (best >= 0f) break;
                    }
                    if (best < 0f) best = minT;
                    if (smooth) best = Mathf.Lerp(_wrapT[f, k], best, ks);
                    _wrapT[f, k] = best;
                    chain[k].localRotation = Quaternion.SlerpUnclamped(rest[k], cur[k], best);
                }
                if (w < 0.999f)
                    for (int k = 0; k < 3; k++) chain[k].localRotation = Quaternion.Slerp(cur[k], chain[k].localRotation, w);
            }
            _wrapFrame = frame;
        }

        private Quaternion RestOf(Transform t)
        {
            foreach (var (f, rest) in _rightFingers) if (f == t) return rest;
            return t.localRotation;
        }

        // Перебор поворота кисти вокруг ручки (через 10°). Для каждого: где запястье, куда должно смотреть предплечье
        // (как в анимации относительно кисти), куда тогда встанет локоть и насколько согнётся запястье. Штрафы: изгиб
        // запястья больше 30°, не дотянуться, далеко от пожелания palm, локоть выше плеча (кроме прицеливания),
        // локоть не туда, куда pole. Проверено на макете (scratchpad hold2/ik.py, grip2).
        private int _turnR = -1, _turnL = -1;   // какой поворот выбран в прошлом кадре: держимся его, чтобы кисть не прыгала

        private void PickTurn(bool right, Transform up, Transform el, Transform hand, Vector3 at, Vector3 hole, Vector3 palm, Vector3 pole,
            Vector3 gAt, Quaternion inv, bool aim, float palmWeight, bool roll, ref Quaternion turn, ref Vector3 wrist, ref Vector3 elbowPole)
        {
            int last = right ? _turnR : _turnL, pick = -1;
            Vector3 s = up.position, e0 = el.position, h0 = hand.position;
            float a = (e0 - s).magnitude, b = (h0 - e0).magnitude;
            if (a < 1e-4f || b < 1e-4f) return;
            var nat = (h0 - e0) / b;                       // предплечье в анимации (вместе с кистью его и поворачиваем)
            var holeN = hole.normalized;
            var palmN = (palm - Vector3.Dot(palm, holeN) * holeN).normalized;
            var third = Vector3.Cross(holeN, palmN);
            var upDir = _root != null ? _root.up : Vector3.up;
            var hint = pole.normalized;
            var grip = gAt - h0;
            float best = float.MaxValue;
            for (int k = 0, n = roll ? 36 : 1; k < n; k++)
            {
                float th = k * 10f * Mathf.Deg2Rad, c = Mathf.Cos(th);
                var p = palmN * c + third * Mathf.Sin(th);
                var t = Quaternion.LookRotation(holeN, p) * inv;
                var wt = at - t * grip;
                var d = t * nat;
                var ed = wt - d * b;
                var toW = wt - s;
                float reach = toW.magnitude;
                if (reach < 1e-4f) continue;
                var dn = toW / reach;
                float dist = Mathf.Clamp(reach, 1e-3f, a + b - 1e-3f);
                float cosA = Mathf.Clamp((a * a + dist * dist - b * b) / (2f * a * dist), -1f, 1f), sinA = Mathf.Sqrt(1f - cosA * cosA);
                var pv = ed - s; pv -= Vector3.Dot(pv, dn) * dn;
                if (pv.sqrMagnitude < 1e-8f) continue;
                var pn = pv.normalized;
                var ei = s + dn * (a * cosA) + pn * (a * sinA);
                var wi = s + dn * dist;
                float bend = Vector3.Angle(wi - ei, d);
                float over = Mathf.Max(0f, reach - (a + b));
                float cost = Sq(Mathf.Max(0f, bend - 30f) / 20f) + over * over * 400f + palmWeight * (1f - c) * 0.5f
                    + Mathf.Max(0f, Vector3.Dot(ei - s, upDir) + (aim ? 0.15f : 0.12f)) * 12f
                    + (aim ? 2f : 0.4f) * (1f - Vector3.Dot(pn, hint));
                if (k == last) cost -= 0.05f;
                if (cost < best) { best = cost; pick = k; turn = t; wrist = wt; elbowPole = ed - (s + wt) * 0.5f; }
            }
            if (right) _turnR = pick; else _turnL = pick;
        }

        private static float Sq(float x) => x * x;

        // Сглаживание поворота кисти и направления локтя (в осях персонажа) между кадрами. Несколько хватов
        // одной руки за кадр сглаживаются от прошлого кадра; руку не трогали дольше двух кадров — без сглаживания.
        private struct ArmSmooth { public int Frame; public bool Has; public Quaternion Turn, PrevTurn; public Vector3 Pole, PrevPole; }
        private ArmSmooth _smR, _smL;

        private void SmoothArm(bool right, ref Quaternion turn, ref Vector3 pole)
        {
            if (_root == null) return;
            ref ArmSmooth s = ref (right ? ref _smR : ref _smL);
            int f = Time.frameCount;
            var rr = _root.rotation;
            var inv = Quaternion.Inverse(rr);
            var lt = inv * turn;
            var lp = inv * pole;
            if (!s.Has || f - s.Frame > 2) { s.PrevTurn = lt; s.PrevPole = lp; s.Has = true; }
            else if (f != s.Frame) { s.PrevTurn = s.Turn; s.PrevPole = s.Pole; }
            float k = 1f - Mathf.Exp(-Time.deltaTime * 14f);
            lt = Quaternion.Slerp(s.PrevTurn, lt, k);
            if (s.PrevPole.sqrMagnitude > 1e-8f && lp.sqrMagnitude > 1e-8f)
                lp = Vector3.Slerp(s.PrevPole.normalized, lp.normalized, k) * lp.magnitude;
            s.Turn = lt; s.Pole = lp; s.Frame = f;
            turn = rr * lt;
            pole = rr * lp;
        }

        // Локоть не в туловище, а в прицеле не выше, чем на 15 см ниже плеча. Поворачиваем локоть вокруг линии
        // плечо–запястье (кисть и запястье остаются на месте), пока он не выйдет наружу. Туловище — эллипс вокруг
        // оси таз–грудь шириной по плечам персонажа, чуть сдвинут вперёд (куртка). Раньше левый локоть с ружьём
        // у бедра уходил в живот, а в прицеле правый задирался до уровня плеча.
        private int _unR, _unL;

        private Vector3 Unclip(bool right, Vector3 s, float a, float b, Vector3 wrist, Vector3 pole, Vector3 fore, bool aim)
        {
            if (_root == null || _hips == null || _spine3 == null || _armL == null || _armR == null) return pole;
            var toW = wrist - s;
            float reach = toW.magnitude;
            if (reach < 1e-4f || a < 1e-4f || b < 1e-4f) return pole;
            var dn = toW / reach;
            float dist = Mathf.Clamp(reach, 1e-3f, a + b - 1e-3f);
            float cosA = Mathf.Clamp((a * a + dist * dist - b * b) / (2f * a * dist), -1f, 1f), sinA = Mathf.Sqrt(1f - cosA * cosA);
            var pn0 = pole - Vector3.Dot(pole, dn) * dn;
            if (pn0.sqrMagnitude < 1e-8f) return pole;
            pn0.Normalize();
            var wi = s + dn * dist;
            Vector3 h = _hips.position, ab = _spine3.position - _hips.position, rgt = _root.right, fw = _root.forward, upD = _root.up;
            float half = 0.5f * Mathf.Abs(Vector3.Dot(_armR.position - _armL.position, rgt));
            // Куртки толстые: эллипс чуть шире плеч и глубже (раньше предплечье с ружьём всё ещё шло через куртку).
            float rx = Mathf.Clamp(half * 1.1f, 0.12f, 0.24f), rz = rx * 0.9f, cz = rx * 0.3f;
            float len2 = Math.Max(1e-6f, ab.sqrMagnitude);

            float Inside(Vector3 p)
            {
                float t = Mathf.Clamp01(Vector3.Dot(p - h, ab) / len2);
                var d = p - (h + ab * t);
                float x = Vector3.Dot(d, rgt) / rx, z = (Vector3.Dot(d, fw) - cz) / rz;
                float q = Mathf.Sqrt(x * x + z * z);
                return q < 1f ? (1f - q) * rx : 0f;   // насколько глубоко внутри, м
            }

            int last = right ? _unR : _unL, pick = 0;
            float best = float.MaxValue;
            for (int k = -12; k <= 12; k++)
            {
                var pn = Quaternion.AngleAxis(k * 10f, dn) * pn0;
                var e = s + dn * (a * cosA) + pn * (a * sinA);
                float cost = (Inside(e) + Inside(Vector3.Lerp(s, e, 0.6f)) + Inside(Vector3.Lerp(e, wi, 0.3f))) * 40f + Math.Abs(k) * 0.01f;
                if (aim) cost += Mathf.Max(0f, Vector3.Dot(e - s, upD) + 0.15f) * 12f;
                if (fore.sqrMagnitude > 0.5f) cost += Sq(Mathf.Max(0f, Vector3.Angle(wi - e, fore) - 35f) / 25f) * 0.5f;
                if (k == last) cost -= 0.03f;
                if (cost < best) { best = cost; pick = k; }
            }
            if (right) _unR = pick; else _unL = pick;
            return pick == 0 ? pole : Quaternion.AngleAxis(pick * 10f, dn) * pn0;
        }

        // Сидит в машине. Поза «стул» сделана для стола: корпус сильно наклонён вперёд, и когда голову ставили
        // к камере, таз уходил назад в спинку, а ноги в подушку. Выпрямляем спину (чуть откинута на спинку).
        public void SitUpright(Vector3 fwd, float recline)
        {
            if (_root == null || _spine == null) return;
            var top = _neck != null ? _neck : _head;
            if (top == null) return;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = _root.forward;
            fwd.Normalize();
            float r = recline * Mathf.Deg2Rad;
            var want = Vector3.up * Mathf.Cos(r) - fwd * Mathf.Sin(r);
            var cur = top.position - _spine.position;
            if (cur.sqrMagnitude > 1e-6f) _spine.rotation = Quaternion.FromToRotation(cur, want) * _spine.rotation;
        }

        // Руки лежат на бёдрах (сидит в машине): ладонью вниз, пальцами к колену, локти назад и в стороны.
        public void HandsOnThighs()
        {
            if (!HasArms || _hipL == null || _hipR == null || _kneeL == null || _kneeR == null) return;
            var rgt = _root.right;
            for (int i = 0; i < 2; i++)
            {
                bool r = i == 0;
                var hip = r ? _hipR : _hipL; var knee = r ? _kneeR : _kneeL;
                var along = knee.position - hip.position;
                if (along.sqrMagnitude < 1e-4f) continue;
                var side = r ? rgt : -rgt;
                var at = Vector3.Lerp(hip.position, knee.position, 0.65f) + Vector3.up * 0.09f;
                var palm = (along.normalized * 0.5f - Vector3.up * 0.85f).normalized;
                Grip(r, at, side, palm, -along.normalized * 0.7f + side * 0.5f - Vector3.up * 0.3f, 1f);
            }
        }

        // Поза на земле: после схватки приходит в себя и встаёт, после сна и ранения встаёт. Своих клипов нет.
        // Корень стоит (только поворот), таз откидываем назад (Back, весь скелет), спину сгибаем вперёд (Curl),
        // таз ставим на высоту HipY и сдвиг HipX от корня, ноги по IK: стопы на земле в FootX от корня
        // (дальше, чем достаёт нога, — ноги прямые, лежит). Knees: колени вверх (1) или вперёд (0).
        // Flat: стопа ровно на земле (1) или по голени, носком вверх (0). Up: таз и стопы как в клипе «стоит».
        // Руки: Face — правой трёт лоб, Prop — упирается ладонями в землю у таза, OnKnees — ладонями в колени.
        // W — вес всей позы (0 — как в клипе).
        public struct Ground
        {
            public float Back, Curl, HipY, HipX, FootX, Flat, Knees, Prop, OnKnees, Up, Turn, Face, W;

            public static Ground Lerp(in Ground a, in Ground b, float t) => new Ground
            {
                Back = Mathf.Lerp(a.Back, b.Back, t), Curl = Mathf.Lerp(a.Curl, b.Curl, t),
                HipY = Mathf.Lerp(a.HipY, b.HipY, t), HipX = Mathf.Lerp(a.HipX, b.HipX, t),
                FootX = Mathf.Lerp(a.FootX, b.FootX, t), Flat = Mathf.Lerp(a.Flat, b.Flat, t),
                Knees = Mathf.Lerp(a.Knees, b.Knees, t), Prop = Mathf.Lerp(a.Prop, b.Prop, t),
                OnKnees = Mathf.Lerp(a.OnKnees, b.OnKnees, t), Up = Mathf.Lerp(a.Up, b.Up, t),
                Turn = Mathf.Lerp(a.Turn, b.Turn, t), Face = Mathf.Lerp(a.Face, b.Face, t), W = Mathf.Lerp(a.W, b.W, t),
            };
        }

        private Transform[] _gBones;
        private Quaternion[] _gSave;

        public void GroundPose(in Ground g)
        {
            if (g.W <= 0f || _root == null || _hips == null || _spine == null || _hipL == null || _hipR == null
                || _kneeL == null || _kneeR == null || _footL == null || _footR == null) return;
            _gBones ??= new[] { _hips, _spine, _spine2, _spine3, _neck, _head, _hipL, _hipR, _kneeL, _kneeR, _footL, _footR };
            _gSave ??= new Quaternion[_gBones.Length];
            for (int i = 0; i < _gBones.Length; i++) if (_gBones[i] != null) _gSave[i] = _gBones[i].localRotation;
            var hipsLocal0 = _hips.localPosition;
            Vector3 right = _root.right, fwd = _root.forward, up = _root.up;
            // Как стоит в клипе: таз, стопы и их поворот (стопа ровно на земле).
            var hips0 = _hips.position;
            Vector3 footL0 = _footL.position, footR0 = _footR.position;
            Quaternion footRotL0 = _footL.rotation, footRotR0 = _footR.rotation;
            float ankleY = Math.Min(_root.InverseTransformPoint(footL0).y, _root.InverseTransformPoint(footR0).y);

            Bend(_hips, -g.Back, right);
            if (Math.Abs(g.Curl) > 0.01f)
            {
                Bend(_spine, g.Curl * 0.35f, right);
                if (_spine2 != null) Bend(_spine2, g.Curl * 0.35f, right);
                if (_spine3 != null) Bend(_spine3, g.Curl * 0.3f, right);
            }
            var hl = _root.InverseTransformPoint(hips0);
            _hips.position = Vector3.Lerp(_root.TransformPoint(new Vector3(hl.x, g.HipY, g.HipX)), hips0, g.Up);

            for (int i = 0; i < 2; i++)
            {
                bool l = i == 0;
                var hip = l ? _hipL : _hipR; var knee = l ? _kneeL : _kneeR; var foot = l ? _footL : _footR;
                var f0 = l ? footL0 : footR0;
                var fl = _root.InverseTransformPoint(f0);
                var onGround = _root.TransformPoint(new Vector3(fl.x * 1.15f, ankleY, g.FootX));
                var side = l ? -right : right;
                TwoBone(hip, knee, foot, Vector3.Lerp(onGround, f0, g.Up), up * g.Knees + fwd * (1f - g.Knees) + side * 0.12f);
                foot.rotation = Quaternion.Slerp(foot.rotation, l ? footRotL0 : footRotR0, g.Flat);
            }

            // Голова поворачивается в стороны (вокруг шеи): шея и голова поровну.
            if (_head != null && Math.Abs(g.Turn) > 0.1f)
            {
                var basePos = _neck != null ? _neck.position : _spine3 != null ? _spine3.position : _spine.position;
                var ax = _head.position - basePos;
                if (ax.sqrMagnitude > 1e-6f)
                {
                    ax.Normalize();
                    if (_neck != null) _neck.rotation = Quaternion.AngleAxis(g.Turn * 0.4f, ax) * _neck.rotation;
                    _head.rotation = Quaternion.AngleAxis(g.Turn * (_neck != null ? 0.6f : 1f), ax) * _head.rotation;
                }
            }

            if (g.W < 1f)
            {
                for (int i = 0; i < _gBones.Length; i++)
                    if (_gBones[i] != null) _gBones[i].localRotation = Quaternion.Slerp(_gSave[i], _gBones[i].localRotation, g.W);
                _hips.localPosition = Vector3.Lerp(hipsLocal0, _hips.localPosition, g.W);
            }

            // Руки — после тела, к его новым костям.
            if (g.Face > 0.01f && _head != null)
            {
                // Лицо и макушка повёрнуты вместе с корпусом (откинут назад на Back, спина согнута на Curl).
                var tilt = Quaternion.AngleAxis(-(g.Back - g.Curl) * g.W, right);
                var hup = tilt * up;
                var face = Quaternion.AngleAxis(g.Turn * 0.5f, hup) * (tilt * fwd);
                var at = _head.position + face * 0.11f + hup * 0.09f;
                Grip(true, at, hup, -face, right * 0.7f - hup * 0.2f + face * 0.2f, g.Face * g.W);
            }
            for (int i = 0; i < 2; i++)
            {
                bool r = i == 0;
                var side = r ? right : -right;
                if (g.Prop > 0.01f)
                {
                    // Ладони на земле сбоку от таза, чуть позади; пальцы вперёд, локти назад.
                    var at = _hips.position + side * 0.26f - fwd * 0.12f;
                    at.y = _root.position.y + 0.05f;
                    Grip(r, at, side, -up, -fwd * 0.8f + side * 0.5f, g.Prop * g.W);
                }
                if (g.OnKnees > 0.01f)
                {
                    // Ладони на колени, локти в стороны: оттолкнуться и выпрямиться.
                    var knee = r ? _kneeR : _kneeL;
                    Grip(r, knee.position + fwd * 0.05f + up * 0.05f, side, -up, side * 0.7f - fwd * 0.1f + up * 0.2f, g.OnKnees * g.W);
                }
            }
        }

        // 0.19.34: укол шприцем (Activity.Injecting). Правая рука с кулаком поднимается над правым бедром (~0,6 с),
        // коротко бьёт вниз в переднюю сторону бедра и держится там, пока игрок колет; потом плавно возвращается.
        // Своего клипа укола у моделей нет — рука ставится по двум костям (TwoBone), локоть наружу-назад.
        private float _injT = -1f, _injW;
        public void Inject(bool on, float dt)
        {
            if (on) { if (_injT < 0f) _injT = 0f; _injT += dt; }
            _injW = Mathf.MoveTowards(_injW, on ? 1f : 0f, dt / 0.35f);
            if (_injW <= 0f) { _injT = -1f; return; }
            if (!HasArms || _hipR == null || _kneeR == null || _root == null) return;
            var right = _root.right; var fwd = _root.forward; var up = _root.up;
            var thigh = Vector3.Lerp(_hipR.position, _kneeR.position, 0.4f) + fwd * 0.09f + right * 0.03f;
            float t = Math.Max(0f, _injT);
            float lift = t < 0.6f ? 0.16f : t < 0.8f ? Mathf.Lerp(0.16f, 0f, (t - 0.6f) / 0.2f) : 0f;
            // Кисть (кость RightHand) — у запястья, кулак со шприцем ниже на ~6 см: запястье ставим выше бедра.
            var target = thigh + up * (0.06f + lift) + fwd * (0.04f * lift / 0.16f);
            float w = Mathf.SmoothStep(0f, 1f, _injW);
            var q0u = _armR.localRotation; var q0e = _elbowR.localRotation; var q0h = RightHand.localRotation;
            TwoBone(_armR, _elbowR, RightHand, target, right * 0.8f - fwd * 0.3f);
            // Кулак костяшками вниз к бедру: кисть продолжает предплечье.
            var fore = RightHand.position - _elbowR.position;
            if (fore.sqrMagnitude > 1e-6f) RightHand.rotation = Quaternion.FromToRotation(_mid1R != null ? (_mid1R.position - RightHand.position) : fore, fore) * RightHand.rotation;
            if (w < 1f)
            {
                _armR.localRotation = Quaternion.Slerp(q0u, _armR.localRotation, w);
                _elbowR.localRotation = Quaternion.Slerp(q0e, _elbowR.localRotation, w);
                RightHand.localRotation = Quaternion.Slerp(q0h, RightHand.localRotation, w);
            }
            var fist = Get("fist");
            if (fist != null && w > 0.5f) Fingers(fist, "RightHand_jnt/");
        }

        private static bool TwoBone(Transform up, Transform el, Transform hand, Vector3 target, Vector3 pole)
        {
            Vector3 s = up.position, e = el.position, h = hand.position;
            float a = (e - s).magnitude, b = (h - e).magnitude;
            if (a < 1e-4f || b < 1e-4f) return false;
            var d = target - s;
            float full = d.magnitude;
            bool ok = full <= a + b;
            float dist = Mathf.Clamp(full, 1e-3f, a + b - 1e-3f);
            var dn = full > 1e-5f ? d / full : up.forward;
            float cosA = Mathf.Clamp((a * a + dist * dist - b * b) / (2f * a * dist), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            var pn = pole - Vector3.Dot(pole, dn) * dn;
            if (pn.sqrMagnitude < 1e-6f) pn = Vector3.Cross(dn, Vector3.right);
            pn.Normalize();
            var eNew = s + dn * (a * cosA) + pn * (a * sinA);
            up.rotation = Quaternion.FromToRotation(e - s, eNew - s) * up.rotation;
            Vector3 e2 = el.position, h2 = hand.position;
            el.rotation = Quaternion.FromToRotation(h2 - e2, s + dn * dist - e2) * el.rotation;
            return ok;
        }

        // Схватка: лежит на спине (корень повёрнут, «вперёд» у него смотрит в небо). Левая рука держит зверя
        // за загривок и отталкивает, правая бьёт тем, что в руке (или кулаком): strike — от 0 до 1 после удара.
        // Разделка (клип harvest). В клипе левая кисть вывернута назад: пальцы смотрят обратно к локтю (угол между
        // предплечьем и пальцами 164°, у правой 7°) — в генераторе клипов у левой кисти перепутана ось пальцев.
        // Ставим кисти сами: пальцы — по продолжению предплечья, чуть вниз к туше. Левая придерживает тушу ладонью
        // вниз (от указательного к мизинцу — наружу, влево); у правой (режет, кулак) ладонь не трогаем.
        public void StraightWrists(float w, float droop = 0.2f)
        {
            if (!HasArms || w <= 0f) return;
            Straight(RightHand, _elbowR, _mid1R, null, null, Vector3.zero, w, droop);
            Straight(LeftHand, _elbowL, _mid1L, _idx1L, _pinky1L, -_root.right, w, droop);
        }

        private void Straight(Transform hand, Transform el, Transform mid, Transform idx, Transform pinky, Vector3 side, float w, float droop)
        {
            if (hand == null || el == null || mid == null) return;
            var cur = mid.position - hand.position;
            var fore = hand.position - el.position;
            if (cur.sqrMagnitude < 1e-6f || fore.sqrMagnitude < 1e-6f) return;
            var want = Vector3.Slerp(fore.normalized, -_root.up, droop).normalized;
            Quaternion turn;
            var across = idx != null && pinky != null ? pinky.position - idx.position : Vector3.zero;
            var sideWant = side - Vector3.Dot(side, want) * want;
            if (across.sqrMagnitude > 1e-6f && sideWant.sqrMagnitude > 1e-4f)
                turn = Quaternion.LookRotation(want, sideWant.normalized) * Quaternion.Inverse(Quaternion.LookRotation(cur.normalized, across.normalized));
            else turn = Quaternion.FromToRotation(cur.normalized, want);
            hand.rotation = Quaternion.Slerp(Quaternion.identity, turn, w) * hand.rotation;
        }

        public void StruggleArms(float t, float strike)
        {
            if (!HasArms || _spine3 == null) return;
            Vector3 sky = _root.forward, head = _root.up, right = _root.right;
            var chest = _spine3.position;
            float n1 = Mathf.PerlinNoise(t * 2.7f, 0.1f) - 0.5f, n2 = Mathf.PerlinNoise(t * 3.1f, 0.6f) - 0.5f, n3 = Mathf.PerlinNoise(t * 2.3f, 0.9f) - 0.5f;
            var lAt = chest + sky * (0.38f + 0.06f * n1) + head * (0.16f + 0.05f * n2) - right * (0.06f + 0.03f * n3);
            Grip(false, lAt, (head - sky).normalized, sky, -right - head * 0.4f, 1f);
            var raised = chest + sky * (0.30f + 0.03f * n2) + right * 0.32f + head * (0.02f + 0.03f * n1);
            var hit = chest + sky * 0.44f + head * 0.24f + right * 0.03f;
            float k = strike > 0f && strike < 1f ? Mathf.Sin(strike * Mathf.PI) : 0f;
            var rAt = Vector3.Lerp(raised, hit, k);
            var toward = (hit - raised).normalized;
            // Остриё (у вещи «вперёд» = от мизинца к указательному) смотрит на зверя.
            var hole = -toward;
            var palm = Vector3.Cross(hole, sky).normalized;
            if (palm.sqrMagnitude < 0.5f) palm = -right;
            Grip(true, rAt, hole, palm, right - head * 0.3f, 1f);
        }

        // Есть ли сейчас разовая анимация на правой руке (бросок, чирк спичкой): на это время рука не тянется к вещи.
        public bool OneShotOnRight => _shot != null && (_shotMask == Mask.RightArm || _shotMask == Mask.Upper);

        // Присед: своей анимации у персонажей нет, сгибаем ноги сами (бёдра вперёд, колени назад, стопы ровно)
        // и опускаем таз ровно настолько, чтобы нижняя стопа осталась на земле. Работает и на ходу.
        private readonly Transform _root, _hips, _spine, _hipL, _hipR, _kneeL, _kneeR, _footL, _footR;
        private readonly Transform _armL, _armR, _elbowL, _elbowR;
        private readonly Transform _spine2, _spine3, _neck, _head;
        private readonly Transform _clavR, _clavL, _eyeR;   // ключицы и правый глаз: ружьё в прицел (затыльник в плечо, щека на приклад)
        private readonly Transform _idx1R, _idx2R, _idx3R, _mid2R, _pinky1R, _mid1R, _mid3R, _idx1L, _pinky1L, _mid1L, _mid3L, _idx4L, _thumb3L;
        private float _crouch;

        // Поворот кисти вокруг предплечья. В клипах еды, питья и разделки кисть повёрнута на 115–137°,
        // а косточка предплечья (Forearm_jnt — сестра кисти под локтем) стоит на месте: запястье
        // перекручивалось «фантиком», кулак выворачивался к лицу. Поворот из клипа ограничиваем 85°
        // (кулак у рта большим пальцем вверх), а в конце кадра половину поворота кисти отдаём предплечью.
        private readonly Transform _foreR, _foreL;
        private readonly Quaternion _handBindR = Quaternion.identity, _handBindL = Quaternion.identity,
                                    _foreBindR = Quaternion.identity, _foreBindL = Quaternion.identity;
        private const float WristTwistMax = 85f;

        private static float TwistAbout(Quaternion q, Vector3 axis)
        {
            float d = q.x * axis.x + q.y * axis.y + q.z * axis.z;
            float a = 2f * Mathf.Atan2(d, q.w) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(0f, a);
        }

        private static float HandTwist(Transform hand, Quaternion bind, out Vector3 axis)
        {
            axis = hand.localPosition.normalized;
            return TwistAbout(hand.localRotation * Quaternion.Inverse(bind), axis);
        }

        private void LimitWristTwist(Transform hand, Quaternion bind, float w = 1f)
        {
            if (hand == null || w <= 0f) return;
            float t = HandTwist(hand, bind, out var axis);
            if (Math.Abs(t) <= WristTwistMax) return;
            hand.localRotation = Quaternion.AngleAxis(-(t - Math.Sign(t) * WristTwistMax) * Mathf.Clamp01(w), axis) * hand.localRotation;
        }

        private static void ShareTwist(Transform hand, Quaternion bind, Transform fore, Quaternion foreBind)
        {
            if (hand == null || fore == null) return;
            float t = HandTwist(hand, bind, out var axis);
            fore.localRotation = Quaternion.AngleAxis(t * 0.5f, axis) * foreBind;
        }

        // Последний шаг кадра, после рук к вещи: предплечье берёт половину поворота кисти.
        public void FinishArms()
        {
            ShareTwist(RightHand, _handBindR, _foreR, _foreBindR);
            ShareTwist(LeftHand, _handBindL, _foreL, _foreBindL);
        }

        // Лазанье по тросу (поза "climb"): своей анимации тоже нет. Руки над головой у троса перехватываются
        // по очереди, ноги поджаты. Фазу перехвата двигает RemotePlayer по скорости подъёма или спуска.
        public float ClimbPhase;

        // Жест (Game/Together.cs): 1 помахать, 2 показать, 4 позвать, 5 «стоп». У персонажей WINTERMUTE таких
        // анимаций нет, их клипы сделаны отдельно и лежат в characters.bin («wave», «point», «beckon», «stop»).
        // Если клипа нет (старый characters.bin) — машем и показываем, поворачивая руку сами.
        public byte Gesture;
        private byte _gesture;
        private float _gestureT, _gestureW;

        private static string GestureClip(byte g) => g switch { 1 => "wave", 2 => "point", 4 => "beckon", 5 => "stop", _ => "" };

        // Скорость, на которой записаны ходьба и бег (м/с, по сдвигу корня в анимации).
        private const float WalkSpeed = 1.6f, RunSpeed = 4.1f;
        // Клипы Quaternius стоят на месте; скорость оценена по длине шага (м/с).
        private const float CrouchSpeed = 1.0f, CrouchSideSpeed = 0.42f, JogSpeed = 3.4f, SprintSpeed = 4.5f;

        public CharacterAnimator(Characters.Def def, Transform[] ts)
        {
            _def = def;
            var path = new string[ts.Length];
            for (int i = 0; i < ts.Length; i++)
            {
                int p = def.TParent[i];
                // Пути в анимациях — от корня модели (сам корень не входит).
                path[i] = p < 0 ? "" : (path[p].Length == 0 ? def.TName[i] : path[p] + "/" + def.TName[i]);
                _byPath[path[i]] = ts[i];
                switch (def.TName[i])
                {
                    case "RightHand_jnt": RightHand = ts[i]; break;
                    case "RightIndex4_jnt": _indexTip = ts[i]; break;
                    case "RightThumb3_jnt": _thumbTip = ts[i]; break;
                    case "LeftHand_jnt": LeftHand = ts[i]; break;
                    case "Hips_jnt": _hips = ts[i]; break;
                    case "Spine1_jnt": _spine = ts[i]; break;
                    case "LeftHip_jnt": _hipL = ts[i]; break;
                    case "RightHip_jnt": _hipR = ts[i]; break;
                    case "LeftKnee_jnt": _kneeL = ts[i]; break;
                    case "RightKnee_jnt": _kneeR = ts[i]; break;
                    case "LeftFoot_jnt": _footL = ts[i]; break;
                    case "RightFoot_jnt": _footR = ts[i]; break;
                    case "LeftUpArm_jnt": _armL = ts[i]; break;
                    case "RightUpArm_jnt": _armR = ts[i]; break;
                    case "LeftElbow_jnt": _elbowL = ts[i]; break;
                    case "RightElbow_jnt": _elbowR = ts[i]; break;
                    case "Spine2_jnt": _spine2 = ts[i]; break;
                    case "Spine3_jnt": _spine3 = ts[i]; break;
                    case "Neck_jnt": _neck = ts[i]; break;
                    case "Head_jnt": _head = ts[i]; break;
                    case "RightShoulder_jnt": _clavR = ts[i]; break;
                    case "LeftShoulder_jnt": _clavL = ts[i]; break;
                    case "r_eye_jnt": _eyeR = ts[i]; break;
                    case "RightIndex1_jnt": _idx1R = ts[i]; break;
                    case "RightIndex2_jnt": _idx2R = ts[i]; break;
                    case "RightIndex3_jnt": _idx3R = ts[i]; break;
                    case "RightMiddle2_jnt": _mid2R = ts[i]; break;
                    case "RightPinky1_jnt": _pinky1R = ts[i]; break;
                    case "RightMiddle1_jnt": _mid1R = ts[i]; break;
                    case "RightMiddle3_jnt": _mid3R = ts[i]; break;
                    case "LeftIndex1_jnt": _idx1L = ts[i]; break;
                    case "LeftPinky1_jnt": _pinky1L = ts[i]; break;
                    case "LeftMiddle1_jnt": _mid1L = ts[i]; break;
                    case "LeftMiddle3_jnt": _mid3L = ts[i]; break;
                    case "LeftIndex4_jnt": _idx4L = ts[i]; break;
                    case "LeftThumb3_jnt": _thumb3L = ts[i]; break;
                    case "RightForearm_jnt": _foreR = ts[i]; break;
                    case "LeftForearm_jnt": _foreL = ts[i]; break;
                }
            }
            _root = ts.Length > 0 ? ts[0].parent : null;
            if (RightHand != null) _handBindR = RightHand.localRotation;
            if (LeftHand != null) _handBindL = LeftHand.localRotation;
            if (_foreR != null) _foreBindR = _foreR.localRotation;
            if (_foreL != null) _foreBindL = _foreL.localRotation;
            for (int i = 0; i < ts.Length; i++)
            {
                if (path[i].Contains("RightHand_jnt/")) _rightFingers.Add((ts[i], ts[i].localRotation));
                if (path[i].Contains("LeftHand_jnt/")) _leftFingers.Add((ts[i], ts[i].localRotation));
            }
        }

        private Bound Get(string state)
        {
            if (string.IsNullOrEmpty(state)) return null;
            if (_bound.TryGetValue(state, out var b)) return b;
            var clip = Characters.GetClip(state, _def.Female);
            if (clip == null) { _bound[state] = null; return null; }
            b = new Bound { Clip = clip, T = new Transform[clip.Paths.Length], UsePos = new bool[clip.Paths.Length], Paths = clip.Paths };
            for (int i = 0; i < clip.Paths.Length; i++)
            {
                _byPath.TryGetValue(clip.Paths[i], out b.T[i]);
                // Лицо (веки, глаза, губы, челюсть) в клипах записано с другого персонажа: у остальных оно
                // искажается — веки закрывают глаза. Лицо не анимируем, оно остаётся как у модели.
                if (IsFace(clip.Paths[i])) b.T[i] = null;
                // Корень (Origin) в анимации уезжает вперёд — это сдвиг персонажа, его делает сама игра.
                b.UsePos[i] = clip.Pos[i] != null && !clip.Paths[i].EndsWith("Origin_jnt");
            }
            _bound[state] = b;
            return b;
        }

        private static readonly string[] FaceParts = { "eye", "lid", "brow", "lip", "jaw", "cheek", "nose", "mouth", "tongue", "chin", "teeth" };

        private static bool IsFace(string path)
        {
            int k = path.LastIndexOf('/');
            string leaf = (k >= 0 ? path.Substring(k + 1) : path).ToLowerInvariant();
            foreach (var f in FaceParts) if (leaf.Contains(f)) return true;
            return false;
        }

        public bool Has(string clip) => Get(clip) != null;

        // pose: "idle" | "walk" | "run" (по скорости) или готовая поза ("sit", "chair").
        // upper: клип для рук ("" — без него), mask: на какие кости он ложится.
        public void Update(float speed, string pose, bool crouch, string upper, Mask mask, bool fistRight, bool fistLeft, float dt)
        {
            bool climb = pose == "climb";
            if (climb) { pose = "idle"; upper = ""; fistRight = fistLeft = true; }
            bool moving = string.IsNullOrEmpty(pose) && speed > 0.4f;
            // Присед — свои клипы (Quaternius, 0.19.28): стоит / идёт вперёд / назад (тот же клип задом) / вбок
            // (клип вперёд, таз довёрнут к движению). Нет клипов в characters.bin — по-старому, сгибом ног.
            bool crouchClips = crouch && string.IsNullOrEmpty(pose) && Has("crouchwalk") && Has("crouchidle");
            string want = pose;
            if (string.IsNullOrEmpty(pose))
            {
                _base = Locomotion(speed);
                want = crouchClips ? "c" + (_base == "run" ? "walk" : _base) : _base;
                // Вбок в приседе — свой клип «шаг вправо» (0.19.29); влево — он же задом наперёд.
                if (want == "cside" && MoveAngle < 0f) want = "csidel";
            }
            if (want != _state)
            {
                bool rev = want == "back" || want == "cback" || (want == "csidel" && Has("crouchside"));
                var next = Get(ClipOf(want)) ?? Get("idle");
                if (next != null && (next != _cur || rev != _curRev))
                {
                    _prev = _cur; _prevT = _curT; _prevRev = _curRev;
                    _cur = next; _curT = 0f; _curRev = rev;
                    _blend = _prev == null ? 1f : 0f;
                }
                _state = want;
            }
            if (_cur == null) return;
            float rate = _state == "walk" || _state == "back" ? Mathf.Clamp(speed / WalkSpeed, 0.6f, 1.8f)
                       : _state == "run" ? Mathf.Clamp(speed / RunSpeedOf(ClipOf("run")), 0.7f, 1.5f)
                       : _state == "cside" || _state == "csidel" ? Mathf.Clamp(speed / (Has("crouchside") ? CrouchSideSpeed : CrouchSpeed), 0.6f, 1.4f)
                       : _state == "cwalk" || _state == "cback" ? Mathf.Clamp(speed / CrouchSpeed, 0.5f, 2f) : 1f;
            _curT += dt * rate * (_curRev ? -1f : 1f);
            if (_prev != null) _prevT += dt * (_prevRev ? -1f : 1f);
            _blend = Mathf.Min(1f, _blend + dt / 0.25f);
            Apply(_cur, _curT, _prev, _prevT, _blend);
            // Клипы приседа (Quaternius) сильно сгорблены — грудь почти лежит на коленях, голова смотрит в пол.
            // Спина и шея выпрямляются поверх клипа (0.19.31): наклон груди ~80° → ~43°.
            _cw = Mathf.MoveTowards(_cw, IsCrouchState(_state) && Has("crouchwalk") ? 1f : 0f, dt / 0.25f);
            if (_cw > 0f && _root != null)
            {
                var r = _root.right;
                float w = Mathf.SmoothStep(0f, 1f, _cw);
                if (_spine != null) Bend(_spine, -5f * w, r);
                if (_spine2 != null) Bend(_spine2, -10f * w, r);
                if (_spine3 != null) Bend(_spine3, -20f * w, r);
                if (_neck != null) Bend(_neck, -5f * w, r);
                if (_head != null) Bend(_head, 10f * w, r);
            }
            if (_blend >= 1f) _prev = null;

            // Шаг вбок поверх позы «стоит»: появляется и уходит плавно, как смена клипа.
            if (_state == "side")
            {
                _sideDir = MoveAngle >= 0f ? 1f : -1f;
                _sidePhase += dt * speed / SideStride;
            }
            _side = Mathf.MoveTowards(_side, _state == "side" ? 1f : 0f, dt / 0.25f);
            if (_side > 0f) SideStep(Mathf.SmoothStep(0f, 1f, _side));

            // Доворот таза наискосок: остаток угла после выбора «вперёд / вбок / назад».
            bool crabClip = (_state == "cside" || _state == "csidel") && Has("crouchside");
            float center = _state == "back" || _state == "cback" ? 180f : _state == "side" ? 90f * _sideDir
                         : crabClip ? (_state == "cside" ? 90f : -90f) : 0f;
            // Без клипа «шаг вбок» (старый characters.bin): ноги идут клипом «вперёд», таз развёрнут к движению.
            float twistMax = (_state == "cside" || _state == "csidel") && !crabClip ? 95f : 50f;
            float twistWant = moving ? Mathf.Clamp(Mathf.DeltaAngle(center, MoveAngle), -twistMax, twistMax) : 0f;
            _twist += (twistWant - _twist) * Math.Min(1f, dt * 8f);

            // Верхний слой: новый клип сразу, вес плавно к 1; убрали — вес плавно к 0 на старом клипе.
            upper ??= "";
            if (upper.Length > 0 && upper != _overName)
            {
                var b = Get(upper);
                if (b != null)
                {
                    if (_over == null || _overW < 0.05f) _overW = 0f;
                    _over = b; _overName = upper; _overMask = mask; _overT = 0f;
                }
            }
            float target = upper.Length > 0 && _over != null && upper == _overName ? Mathf.Clamp01(UpperWeight) : 0f;
            _overW = Mathf.MoveTowards(_overW, target, dt / 0.25f);
            if (_over != null)
            {
                _overT += dt;
                if (_overW > 0f) Overlay(_over, _overT, _overMask, _overW, false);
                else if (target == 0f) { _over = null; _overName = ""; }
            }

            // Руки с ружьём — поверх ходьбы и стойки, но под разовыми (бросок) и под пальцами.
            HoldW = Mathf.MoveTowards(HoldW, HoldTarget && !string.IsNullOrEmpty(HoldClip) ? 1f : 0f, dt / 0.25f);
            if (HoldW > 0f && !string.IsNullOrEmpty(HoldClip))
            {
                _holdT += dt;
                var hb = Get(HoldClip);
                if (hb != null) Overlay(hb, _holdT, Mask.Upper, HoldW, false);
                if (HoldAimW > 0f && !string.IsNullOrEmpty(HoldAimClip))
                {
                    var ab = Get(HoldAimClip);
                    if (ab != null) Overlay(ab, _holdT, Mask.Upper, HoldW * Mathf.Clamp01(HoldAimW), false);
                }
            }

            if (_shot != null)
            {
                _shotT += dt;
                if (_shotT >= _shot.Clip.Length) _shot = null;
                else
                {
                    // Короткий вход и выход, чтобы рука не дёргалась.
                    float w = Mathf.Clamp01(Math.Min(_shotT, _shot.Clip.Length - _shotT) / 0.1f);
                    Overlay(_shot, _shotT, _shotMask, w, true);
                }
            }

            // Клип с ружьём сам по себе естественный: левая ладонь под цевьём повёрнута на 120–150° от позы модели,
            // ограничение (оно для клипов еды и питья) развернуло бы её обратно.
            // Клип с винтовкой сам крутит кисть на 120–150°: пока он виден, предел ослабляем по его весу (без рывка).
            LimitWristTwist(RightHand, _handBindR, 1f - HoldW);
            LimitWristTwist(LeftHand, _handBindL, 1f - HoldW);

            if (fistRight || fistLeft)
            {
                var fist = Get("fist");
                if (fist != null)
                {
                    if (fistRight) Fingers(fist, "RightHand_jnt/");
                    if (fistLeft) Fingers(fist, "LeftHand_jnt/");
                }
                if (fistRight && PinchRight && ScissorRight)
                    foreach (var (t, rest) in _rightFingers)
                    {
                        if (t.name.Contains("Index") || t.name.Contains("Middle")) t.localRotation = Quaternion.Slerp(t.localRotation, rest, 0.92f);
                        else if (t.name.Contains("Thumb")) t.localRotation = Quaternion.Slerp(t.localRotation, rest, 0.5f);
                    }
                else if (fistRight && PinchRight)
                    foreach (var (t, rest) in _rightFingers)
                        if (t.name.Contains("Index") || t.name.Contains("Thumb")) t.localRotation = Quaternion.Slerp(t.localRotation, rest, 0.6f);
                if (fistRight && TriggerRight)
                    foreach (var (t, rest) in _rightFingers)
                        if (t.name.Contains("Index")) t.localRotation = Quaternion.Slerp(t.localRotation, rest, 0.5f);
                if (fistRight && LooseRight > 0f)
                    foreach (var (t, rest) in _rightFingers)
                        t.localRotation = Quaternion.Slerp(t.localRotation, rest, t.name.Contains("Thumb") ? LooseRightThumb : LooseRight);
                if (fistLeft && LooseLeft > 0f)
                    foreach (var (t, rest) in _leftFingers)
                        t.localRotation = Quaternion.Slerp(t.localRotation, rest, t.name.Contains("Thumb") ? LooseLeftThumb : LooseLeft);
            }

            // Жест ложится на правую руку поверх всего, кроме приседа и троса.
            if (Gesture != 0 && Gesture != _gesture) { _gesture = Gesture; _gestureT = 0f; }
            _gestureW = Mathf.MoveTowards(_gestureW, Gesture != 0 && !climb ? 1f : 0f, dt / 0.25f);
            if (_gestureW > 0f)
            {
                _gestureT += dt;
                float w = Mathf.SmoothStep(0f, 1f, _gestureW);
                var gb = Get(GestureClip(_gesture));
                if (gb != null) Overlay(gb, _gestureT, Mask.RightArm, w, false);
                else DoGesture(_gesture, w, _gestureT);
                // Помахать и «стоп» — открытой ладонью: пальцы прямые, как у модели, даже если в руке была вещь.
                if (_gesture == 1 || _gesture == 5)
                    foreach (var (t, rest) in _rightFingers) t.localRotation = Quaternion.Slerp(t.localRotation, rest, w);
            }
            else { _gestureT = 0f; _gesture = 0; }

            _crouch = Mathf.MoveTowards(_crouch, crouch && string.IsNullOrEmpty(pose) ? 1f : 0f, dt / 0.3f);
            // Сгиб ног — только если нет клипов приседа (старый characters.bin): клип уже сам присел.
            if (_crouch > 0f && !Has("crouchwalk")) Crouch(Mathf.SmoothStep(0f, 1f, _crouch));
            if (Math.Abs(_twist) > 0.5f) Twist(_twist);
            if (climb) Climb(ClimbPhase);
        }

        // Вперёд (или бег), назад, вбок — по углу движения. На границе секторов запас в 8°, чтобы не дёргался.
        private string Locomotion(float speed)
        {
            if (speed <= 0.4f) return "idle";
            float a = Math.Abs(MoveAngle);
            const float m = 8f;
            bool fwd = _base == "walk" || _base == "run", side = _base == "side", back = _base == "back";
            if (a <= 45f + (fwd ? m : side ? -m : 0f)) return speed > 3.2f ? "run" : "walk";
            if (a >= 135f + (back ? -m : side ? m : 0f)) return "back";
            return "side";
        }

        private string ClipOf(string state) => state switch
        {
            "back" => "walk",
            "side" => "idle",
            "run" => !string.IsNullOrEmpty(RunClip) && Has(RunClip) ? RunClip : "run",
            "cidle" => "crouchidle",
            "cwalk" or "cback" => "crouchwalk",
            "cside" or "csidel" => Has("crouchside") ? "crouchside" : "crouchwalk",
            _ => state,
        };

        // Какой клип бега: "run" — WINTERMUTE, "jog" / "sprint" — Quaternius (выбирает игрок, 0.19.28).
        public static string RunClip = "run";
        private static float RunSpeedOf(string clip) => clip == "jog" ? JogSpeed : clip == "sprint" ? SprintSpeed : RunSpeed;

        // Шаг вбок. Ведущая нога (в сторону шага) отходит и встаёт, вторая подтягивается к ней —
        // ноги не перекрещиваются. Поднятая нога сгибается в колене, таз опускается так, чтобы опорная стопа
        // осталась на земле (разведённые ноги иначе висели бы над ней).
        private const float SideStride = 0.9f, SideSpread = 15f;

        private void SideStep(float w)
        {
            if (_root == null || _hips == null || _hipL == null || _hipR == null || _kneeL == null || _kneeR == null || _footL == null || _footR == null) return;
            float before = Math.Min(_root.InverseTransformPoint(_footL.position).y, _root.InverseTransformPoint(_footR.position).y);
            float ph = _sidePhase - (float)Math.Floor(_sidePhase);
            float q = ph + 0.5f; q -= (float)Math.Floor(q);
            bool toRight = _sideDir > 0f;
            // Ведущая: стоит (0…0,6) — отведение спадает, пока таз едет над ней; переносится (0,6…1) — снова отходит.
            float leadSpread, leadLift, trailSpread, trailLift;
            if (ph < 0.6f) { leadSpread = 1f - ph / 0.6f; leadLift = 0f; }
            else { float u = (ph - 0.6f) / 0.4f; leadSpread = Mathf.SmoothStep(0f, 1f, u); leadLift = Mathf.Sin(u * Mathf.PI); }
            // Вторая: стоит — отводится всё больше (таз уходит от неё); переносится — подтягивается обратно.
            if (q < 0.6f) { trailSpread = q / 0.6f; trailLift = 0f; }
            else { float u = (q - 0.6f) / 0.4f; trailSpread = 1f - Mathf.SmoothStep(0f, 1f, u); trailLift = Mathf.Sin(u * Mathf.PI); }
            // Поворот вокруг «вперёд» с плюсом уводит стопу вправо.
            if (toRight)
            {
                Leg(_hipR, _kneeR, _footR, SideSpread * leadSpread, leadLift, w);
                Leg(_hipL, _kneeL, _footL, -SideSpread * trailSpread, trailLift, w);
            }
            else
            {
                Leg(_hipL, _kneeL, _footL, -SideSpread * leadSpread, leadLift, w);
                Leg(_hipR, _kneeR, _footR, SideSpread * trailSpread, trailLift, w);
            }
            float after = Math.Min(_root.InverseTransformPoint(_footL.position).y, _root.InverseTransformPoint(_footR.position).y);
            _hips.position -= _root.up * (after - before);
        }

        private void Leg(Transform hip, Transform knee, Transform foot, float spread, float lift, float w)
        {
            var right = _root.right;
            hip.rotation = Quaternion.AngleAxis(spread * w, _root.forward) * hip.rotation;
            Bend(hip, -22f * lift * w, right);
            Bend(knee, 45f * lift * w, right);
            Bend(foot, -23f * lift * w, right);
        }

        // Таз с ногами повёрнут на угол, верх тела обратно — смотрит, куда смотрит игрок.
        private void Twist(float deg)
        {
            if (_root == null || _hips == null || _spine == null) return;
            var up = _root.up;
            _hips.rotation = Quaternion.AngleAxis(deg, up) * _hips.rotation;
            _spine.rotation = Quaternion.AngleAxis(-deg, up) * _spine.rotation;
        }

        private void DoGesture(byte g, float w, float t)
        {
            if (_root == null || _armR == null || _elbowR == null) return;
            var right = _root.right;
            var fwd = _root.forward;
            if (g == 1)
            {
                // Рука поднята в сторону и вверх, предплечье качается.
                Bend(_armR, -150f * w, right);
                _armR.rotation = Quaternion.AngleAxis(35f * w, fwd) * _armR.rotation;
                Bend(_elbowR, (-15f - 30f * (0.5f + 0.5f * Mathf.Sin(t * 9f))) * w, fwd);
            }
            else if (g == 2)
            {
                // Рука вперёд, почти прямая.
                Bend(_armR, -85f * w, right);
                Bend(_elbowR, -5f * w, right);
            }
        }

        private void Climb(float phase)
        {
            if (_root == null || _armL == null || _armR == null || _elbowL == null || _elbowR == null || _hipL == null || _hipR == null || _kneeL == null || _kneeR == null) return;
            var right = _root.right;
            var fwd = _root.forward;
            float sw = 25f * Mathf.Sin(phase);
            Bend(_armR, -135f + sw, right); _armR.rotation = Quaternion.AngleAxis(28f, fwd) * _armR.rotation;
            Bend(_armL, -135f - sw, right); _armL.rotation = Quaternion.AngleAxis(-28f, fwd) * _armL.rotation;
            Bend(_elbowR, -25f - sw * 0.8f, right);
            Bend(_elbowL, -25f + sw * 0.8f, right);
            Bend(_hipL, -35f - 0.4f * sw, right); Bend(_hipR, -35f + 0.4f * sw, right);
            Bend(_kneeL, 60f, right); Bend(_kneeR, 60f, right);
        }

        private void Crouch(float c)
        {
            if (_root == null || _hips == null || _hipL == null || _hipR == null || _kneeL == null || _kneeR == null || _footL == null || _footR == null) return;
            var right = _root.right;
            float before = Math.Min(_root.InverseTransformPoint(_footL.position).y, _root.InverseTransformPoint(_footR.position).y);
            Bend(_hipL, -70f * c, right); Bend(_hipR, -70f * c, right);
            Bend(_kneeL, 110f * c, right); Bend(_kneeR, 110f * c, right);
            Bend(_footL, -40f * c, right); Bend(_footR, -40f * c, right);
            if (_spine != null) Bend(_spine, 20f * c, right);
            float after = Math.Min(_root.InverseTransformPoint(_footL.position).y, _root.InverseTransformPoint(_footR.position).y);
            _hips.position -= _root.up * Math.Max(0f, after - before);
        }

        // Поворот кости вокруг оси персонажа «вправо» (плюс — верх кости наклоняется вперёд).
        private static void Bend(Transform t, float deg, Vector3 axis) => t.rotation = Quaternion.AngleAxis(deg, axis) * t.rotation;

        // Разовая анимация поверх (выстрел, бросок).
        public void Play(string clip, Mask mask)
        {
            var b = Get(clip);
            if (b == null) return;
            _shot = b; _shotMask = mask; _shotT = 0f;
        }

        private static bool InMask(string path, Mask m) => m switch
        {
            Mask.Upper => path.Contains("Spine1_jnt"),
            Mask.RightArm => path.Contains("RightShoulder_jnt"),
            Mask.LeftArm => path.Contains("LeftShoulder_jnt"),
            _ => false,
        };

        private static void Overlay(Bound b, float t, Mask mask, float w, bool once)
        {
            var c = b.Clip;
            if (once) t = Math.Min(t, c.Length - 0.001f);
            Frame(c, t, out int f0, out int f1, out float u);
            for (int i = 0; i < c.Paths.Length; i++)
            {
                var tr = b.T[i];
                if (tr == null || c.Rot[i] == null || !InMask(b.Paths[i], mask)) continue;
                var q = Rot(c.Rot[i], f0, f1, u);
                tr.localRotation = w >= 1f ? q : Quaternion.Slerp(tr.localRotation, q, w);
            }
        }

        private static void Fingers(Bound b, string hand)
        {
            var c = b.Clip;
            for (int i = 0; i < c.Paths.Length; i++)
            {
                var tr = b.T[i];
                if (tr == null || c.Rot[i] == null || !b.Paths[i].Contains(hand)) continue;
                tr.localRotation = Rot(c.Rot[i], 0, 0, 0f);
            }
        }

        private static void Frame(Characters.Clip c, float t, out int f0, out int f1, out float u)
        {
            float len = Math.Max(0.001f, c.Length);
            float x = ((t % len + len) % len) * c.Fps;   // время может идти назад (шаг назад)
            f0 = Math.Max(0, Math.Min(c.Frames - 2, (int)x));
            f1 = Math.Min(c.Frames - 1, f0 + 1);
            u = x - f0;
        }

        private static Quaternion Rot(float[] a, int f0, int f1, float u) =>
            Quaternion.Slerp(new Quaternion(a[f0 * 4], a[f0 * 4 + 1], a[f0 * 4 + 2], a[f0 * 4 + 3]),
                             new Quaternion(a[f1 * 4], a[f1 * 4 + 1], a[f1 * 4 + 2], a[f1 * 4 + 3]), u);

        private static Vector3 Pos(float[] a, int f0, int f1, float u) =>
            Vector3.Lerp(new Vector3(a[f0 * 3], a[f0 * 3 + 1], a[f0 * 3 + 2]), new Vector3(a[f1 * 3], a[f1 * 3 + 1], a[f1 * 3 + 2]), u);

        private static void Apply(Bound cur, float ct, Bound prev, float pt, float blend)
        {
            var c = cur.Clip;
            Frame(c, ct, out int a0, out int a1, out float au);
            Dictionary<Transform, (Quaternion r, Vector3 p, bool hasP)> old = null;
            if (prev != null && blend < 1f)
            {
                old = new Dictionary<Transform, (Quaternion, Vector3, bool)>();
                var pc = prev.Clip;
                Frame(pc, pt, out int b0, out int b1, out float bu);
                for (int i = 0; i < pc.Paths.Length; i++)
                {
                    var t = prev.T[i];
                    if (t == null || pc.Rot[i] == null) continue;
                    old[t] = (Rot(pc.Rot[i], b0, b1, bu), prev.UsePos[i] ? Pos(pc.Pos[i], b0, b1, bu) : Vector3.zero, prev.UsePos[i]);
                }
            }
            for (int i = 0; i < c.Paths.Length; i++)
            {
                var t = cur.T[i];
                if (t == null) continue;
                if (c.Rot[i] != null)
                {
                    var q = Rot(c.Rot[i], a0, a1, au);
                    if (old != null && old.TryGetValue(t, out var o)) q = Quaternion.Slerp(o.r, q, blend);
                    t.localRotation = q;
                }
                if (cur.UsePos[i])
                {
                    var p = Pos(c.Pos[i], a0, a1, au);
                    if (old != null && old.TryGetValue(t, out var o) && o.hasP) p = Vector3.Lerp(o.p, p, blend);
                    t.localPosition = p;
                }
            }
        }
    }
}
