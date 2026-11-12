using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace TrainerByEMPEROR
{
    // Схема локации сверху. Строится понемногу в каждом кадре сразу после загрузки локации,
    // чтобы игра не замирала; окно трейнера забирает готовую картинку.
    // Без камеры и видеокарты: дополнительная камера роняет игру начиная с версии 2.58.
    internal static class MapBuilder
    {
        private const double FrameBudgetMs = 4;
        private const float StartDelay = 5f;
        private const byte Empty = 0, Ground = 1, Object = 2, Tree = 3, Ice = 4;

        private sealed class TerrainRect
        {
            public Terrain T;
            public float X0, Z0, X1, Z1, Y;
        }

        private sealed class Job
        {
            public string Scene;
            public bool Outdoors;
            public int Size;
            public float Cx, Cz, Half, Step, StartY, MaxDist, FloorY;
            public float[] Heights;
            public byte[] Kinds;
            public Dictionary<int, byte> ColliderKind = new();
            public List<TerrainRect> Terrains;
            public int LastTerrain = -1;
            public float HMin = float.MaxValue, HMax = float.MinValue;
            public int Next;
            public Stopwatch Clock = Stopwatch.StartNew();
        }

        private static Job _job;
        private static Task<object> _finish;
        private static Job _finishJob;
        private static object _result;
        private static string _resultScene;
        private static string _seenScene;
        private static float _startAt = float.MaxValue;
        private static Vector3 _down;

        // Каждый кадр, когда загружено сохранение.
        public static void Tick()
        {
            string scene = MapService.SceneName();
            if (scene != _seenScene)
            {
                // Новая локация: старая карта не годится, строим заново чуть позже, когда всё загрузится.
                _seenScene = scene;
                _job = null;
                _finish = null;
                _result = null;
                _startAt = Time.realtimeSinceStartup + StartDelay;
            }
            if (_finish != null)
            {
                if (!_finish.IsCompleted) return;
                var done = _finishJob;
                if (_finish.IsCompletedSuccessfully)
                {
                    _result = _finish.Result;
                    _resultScene = done.Scene;
                    MelonLogger.Msg($"Карта: готова за {done.Clock.Elapsed.TotalSeconds:0.0} с.");
                }
                else
                {
                    MelonLogger.Warning("Карта: не удалось раскрасить — " + _finish.Exception?.GetBaseException().Message);
                    _startAt = Time.realtimeSinceStartup + 10f;
                }
                _finish = null;
                _finishJob = null;
                return;
            }
            if (_job == null && _result == null && Time.realtimeSinceStartup >= _startAt) Start();
            if (_job != null) Step();
        }

        public static void Reset()
        {
            _job = null;
            _finish = null;
            _result = null;
            _seenScene = null;
        }

        public static object Request(bool force)
        {
            string scene = MapService.SceneName();
            if (force && _job == null && _finish == null)
            {
                _result = null;
                Start();
            }
            if (_result != null && _resultScene == scene) return _result;
            if (_finish != null) return new { ok = true, ready = false, progress = 99 };
            if (_job == null)
            {
                _startAt = 0f; // окно просит карту — начинаем сразу
                return new { ok = true, ready = false, progress = 0 };
            }
            return new { ok = true, ready = false, progress = _job.Next * 100 / (_job.Size * _job.Size) };
        }

        private static void Start()
        {
            _startAt = float.MaxValue;
            var player = GameManager.GetPlayerTransform();
            if (player == null)
            {
                _startAt = Time.realtimeSinceStartup + 2f;
                return;
            }
            bool outdoors = MapService.Outdoors();
            Vector3 pp = player.position;

            var terrains = outdoors ? TerrainRects() : new List<TerrainRect>();
            float minX, maxX, minZ, maxZ, top, bottom;
            if (terrains.Count > 0)
            {
                minX = minZ = top = float.MaxValue;
                maxX = maxZ = bottom = float.MinValue;
                foreach (var t in terrains)
                {
                    minX = Math.Min(minX, t.X0);
                    maxX = Math.Max(maxX, t.X1);
                    minZ = Math.Min(minZ, t.Z0);
                    maxZ = Math.Max(maxZ, t.Z1);
                }
                bottom = float.MaxValue;
                top = float.MinValue;
                foreach (var t in terrains)
                {
                    var size3 = t.T.terrainData.size;
                    bottom = Math.Min(bottom, t.Y);
                    top = Math.Max(top, t.Y + size3.y);
                }
            }
            else if (outdoors && ColliderBounds(pp, out minX, out maxX, out minZ, out maxZ, out top, out bottom))
            {
                // Локация без рельефа Unity (например, из модов): границы — по объектам сцены.
            }
            else
            {
                float r = outdoors ? 400f : 45f;
                minX = pp.x - r;
                maxX = pp.x + r;
                minZ = pp.z - r;
                maxZ = pp.z + r;
                top = pp.y + 300f;
                bottom = pp.y - 300f;
            }

            int size = outdoors ? 900 : 360;
            float half = Math.Max(maxX - minX, maxZ - minZ) / 2f;
            _down = Vector3.down;
            _job = new Job
            {
                Scene = MapService.SceneName(),
                Outdoors = outdoors,
                Size = size,
                Cx = (minX + maxX) / 2f,
                Cz = (minZ + maxZ) / 2f,
                Half = half,
                Step = half * 2f / size,
                StartY = outdoors ? top + 50f : pp.y + 2.2f,
                MaxDist = outdoors ? top - bottom + 100f : 6f,
                FloorY = pp.y,
                Heights = new float[size * size],
                Kinds = new byte[size * size],
                Terrains = terrains,
            };
            MelonLogger.Msg($"Карта: строю «{_job.Scene}» ({size}×{size}, {(outdoors ? "улица" : "помещение")}), X {minX:0}…{maxX:0}, Z {minZ:0}…{maxZ:0}.");
        }

        private static void Step()
        {
            var j = _job;
            var budget = Stopwatch.StartNew();
            int total = j.Size * j.Size;
            while (j.Next < total && budget.Elapsed.TotalMilliseconds < FrameBudgetMs)
            {
                // По 64 точки между проверками времени.
                int end = Math.Min(total, j.Next + 64);
                for (; j.Next < end; j.Next++) Sample(j, j.Next);
            }
            if (j.Next < total) return;

            // Раскраска и упаковка — чистые вычисления без вызовов игры, их можно делать в фоновом потоке.
            _job = null;
            _finishJob = j;
            _finish = Task.Run<object>(() => new
            {
                ok = true,
                ready = true,
                jpg = Convert.ToBase64String(Bmp(j.Size, Shade(j))),
                size = j.Size,
                minX = j.Cx - j.Half,
                maxX = j.Cx + j.Half,
                minZ = j.Cz - j.Half,
                maxZ = j.Cz + j.Half,
                scene = j.Scene,
                outdoors = j.Outdoors,
            });
        }

        private static void Sample(Job j, int i)
        {
            int row = i / j.Size, col = i % j.Size;
            float z = j.Cz + j.Half - (row + 0.5f) * j.Step;
            float x = j.Cx - j.Half + (col + 0.5f) * j.Step;

            bool hasGround = GroundHeight(j, x, z, out float ground);
            RaycastHit hit;
            bool hasHit = Physics.Raycast(new Vector3 { x = x, y = j.StartY, z = z }, _down, out hit, j.MaxDist,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (!hasGround && !hasHit) return;

            float hitY = 0f;
            byte kind = Ground;
            if (hasHit)
            {
                hitY = hit.point.y;
                var c = hit.collider;
                int id = c.GetInstanceID();
                if (!j.ColliderKind.TryGetValue(id, out byte ck))
                {
                    ck = c.TryCast<TerrainCollider>() != null ? Ground : IsWater(c.gameObject.name) ? Ice : Object;
                    j.ColliderKind[id] = ck;
                }
                float above = hasGround ? hitY - ground : 0f;
                if (ck == Ground) kind = above > 1f ? Tree : Ground; // деревья рельефа — часть TerrainCollider
                else if (ck == Ice) kind = Ice;
                else kind = !hasGround || above > 0.3f ? Object : Ground;
            }

            float h = j.Outdoors && hasGround ? ground : hitY;
            j.Heights[i] = h;
            j.Kinds[i] = kind;
            if (h < j.HMin) j.HMin = h;
            if (h > j.HMax) j.HMax = h;
        }

        // Границы локации по коллайдерам: берём 2…98 % центров, чтобы далёкие одиночные объекты не растягивали карту.
        private static bool ColliderBounds(Vector3 pp, out float minX, out float maxX, out float minZ, out float maxZ, out float top, out float bottom)
        {
            minX = maxX = minZ = maxZ = top = bottom = 0f;
            const float Limit = 2000f;
            var xs = new List<float>();
            var zs = new List<float>();
            var ys = new List<float>();
            var all = UnityEngine.Object.FindObjectsOfType<Collider>();
            foreach (var c in all)
            {
                if (c == null || c.isTrigger || !c.enabled) continue;
                var b = c.bounds;
                var ctr = b.center;
                if (Math.Abs(ctr.x - pp.x) > Limit || Math.Abs(ctr.z - pp.z) > Limit) continue;
                xs.Add(ctr.x);
                zs.Add(ctr.z);
                ys.Add(b.max.y);
                ys.Add(b.min.y);
            }
            MelonLogger.Msg($"Карта: рельефа Unity нет, границы по {xs.Count} объектам.");
            if (xs.Count < 20) return false;
            xs.Sort();
            zs.Sort();
            ys.Sort();
            float P(List<float> v, double q) => v[(int)Math.Clamp(q * (v.Count - 1), 0, v.Count - 1)];
            minX = Math.Min(P(xs, 0.02), pp.x - 100f) - 30f;
            maxX = Math.Max(P(xs, 0.98), pp.x + 100f) + 30f;
            minZ = Math.Min(P(zs, 0.02), pp.z - 100f) - 30f;
            maxZ = Math.Max(P(zs, 0.98), pp.z + 100f) + 30f;
            top = Math.Max(P(ys, 0.99), pp.y + 50f);
            bottom = Math.Min(P(ys, 0.01), pp.y - 50f);
            return true;
        }

        private static List<TerrainRect> TerrainRects()
        {
            var list = new List<TerrainRect>();
            var all = Terrain.activeTerrains;
            if (all == null) return list;
            for (int k = 0; k < all.Length; k++)
            {
                var t = all[k];
                if (t == null || t.terrainData == null) continue;
                var pos = t.GetPosition();
                var size = t.terrainData.size;
                list.Add(new TerrainRect { T = t, X0 = pos.x, Z0 = pos.z, X1 = pos.x + size.x, Z1 = pos.z + size.z, Y = pos.y });
            }
            return list;
        }

        private static bool GroundHeight(Job j, float x, float z, out float h)
        {
            h = 0f;
            var list = j.Terrains;
            if (list.Count == 0) return false;
            // Соседние точки почти всегда на том же участке рельефа — проверяем его первым.
            if (j.LastTerrain >= 0 && SampleTerrain(list[j.LastTerrain], x, z, out h)) return true;
            for (int k = 0; k < list.Count; k++)
            {
                if (k == j.LastTerrain || !SampleTerrain(list[k], x, z, out h)) continue;
                j.LastTerrain = k;
                return true;
            }
            return false;
        }

        private static bool SampleTerrain(TerrainRect r, float x, float z, out float h)
        {
            h = 0f;
            if (x < r.X0 || z < r.Z0 || x > r.X1 || z > r.Z1) return false;
            h = r.T.SampleHeight(new Vector3 { x = x, y = 0f, z = z }) + r.Y;
            return true;
        }

        private static bool IsWater(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            name = name.ToLowerInvariant();
            return name.StartsWith("ice") || name.Contains("_ice") || name.Contains(" ice") || name.Contains("frozen")
                || name.Contains("water") || name.Contains("lake") || name.Contains("river") || name.Contains("pond");
        }

        // ---------- Раскраска (только управляемый код, без вызовов в игру) ----------

        private struct Rgb
        {
            public float R, G, B;
            public Rgb(float r, float g, float b) { R = r; G = g; B = b; }
            public static Rgb operator *(Rgb c, float k) => new(c.R * k, c.G * k, c.B * k);
            public static Rgb Lerp(Rgb a, Rgb b, float t) => new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        }

        private static readonly Rgb Background = new(0.08f, 0.09f, 0.11f);
        private static readonly Rgb Low = new(0.62f, 0.68f, 0.72f), High = new(0.95f, 0.96f, 0.98f);
        private static readonly Rgb Forest = new(0.20f, 0.33f, 0.25f);
        private static readonly Rgb Building = new(0.58f, 0.48f, 0.38f), BuildingEdge = new(0.22f, 0.18f, 0.15f);
        private static readonly Rgb IceColor = new(0.68f, 0.82f, 0.92f);
        private static readonly Rgb Floor = new(0.78f, 0.72f, 0.62f), Furniture = new(0.52f, 0.46f, 0.40f), Wall = new(0.30f, 0.28f, 0.27f);

        // Оттенок по высоте, тени склонов с северо-запада, горизонтали через 25 м,
        // лес — зелёным, постройки и скалы — коричневым с контуром, лёд — голубым.
        private static byte[] Shade(Job j)
        {
            int size = j.Size;
            var h = j.Heights;
            var kinds = j.Kinds;
            var rgb = new byte[size * size * 3];
            float range = Math.Max(1f, j.HMax - j.HMin);
            float lx = -1f, ly = 2f, lz = 1f;
            float ll = MathF.Sqrt(lx * lx + ly * ly + lz * lz);
            lx /= ll; ly /= ll; lz /= ll;

            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    int i = row * size + col;
                    Rgb c;
                    byte kind = kinds[i];
                    if (kind == Empty)
                    {
                        c = Background;
                    }
                    else
                    {
                        float hl = Height(h, kinds, size, row, col - 1, h[i]);
                        float hr = Height(h, kinds, size, row, col + 1, h[i]);
                        float hu = Height(h, kinds, size, row - 1, col, h[i]);
                        float hd = Height(h, kinds, size, row + 1, col, h[i]);
                        float nx = (hl - hr) / (2f * j.Step), ny = 1f, nz = (hd - hu) / (2f * j.Step);
                        float nl = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                        float dot = (nx * lx + ny * ly + nz * lz) / nl;
                        float shade = Math.Clamp(0.45f + 0.75f * dot, 0.35f, 1.15f);

                        if (j.Outdoors)
                        {
                            var groundColor = Rgb.Lerp(Low, High, (h[i] - j.HMin) / range) * shade;
                            switch (kind)
                            {
                                case Tree:
                                    c = Rgb.Lerp(groundColor, Forest, 0.65f);
                                    break;
                                case Object:
                                    c = Edge(kinds, size, row, col, Object) ? BuildingEdge : Building;
                                    break;
                                case Ice:
                                    c = IceColor * (1f + (shade - 1f) * 0.3f);
                                    break;
                                default:
                                    c = groundColor;
                                    if (Contour(h, kinds, size, row, col, 25f)) c = c * 0.8f;
                                    break;
                            }
                        }
                        else
                        {
                            float above = h[i] - j.FloorY;
                            c = above < 0.4f ? Floor : above < 1.4f ? Furniture : Wall;
                            c = c * (1f + (shade - 1f) * 0.4f);
                        }
                    }
                    rgb[i * 3] = ToByte(c.R);
                    rgb[i * 3 + 1] = ToByte(c.G);
                    rgb[i * 3 + 2] = ToByte(c.B);
                }
            }
            return rgb;
        }

        private static byte ToByte(float v) => (byte)(Math.Clamp(v, 0f, 1f) * 255f);

        private static float Height(float[] h, byte[] kinds, int size, int row, int col, float fallback)
        {
            if (row < 0 || col < 0 || row >= size || col >= size) return fallback;
            int i = row * size + col;
            return kinds[i] == Empty ? fallback : h[i];
        }

        private static bool Contour(float[] h, byte[] kinds, int size, int row, int col, float every)
        {
            if (row + 1 >= size || col + 1 >= size) return false;
            int i = row * size + col;
            int b = (int)MathF.Floor(h[i] / every);
            return (kinds[i + 1] == Ground && (int)MathF.Floor(h[i + 1] / every) != b)
                || (kinds[i + size] == Ground && (int)MathF.Floor(h[i + size] / every) != b);
        }

        // Точка на границе области данного типа — для контура построек.
        private static bool Edge(byte[] kinds, int size, int row, int col, byte kind)
        {
            if (row == 0 || col == 0 || row == size - 1 || col == size - 1) return true;
            int i = row * size + col;
            return kinds[i - 1] != kind || kinds[i + 1] != kind || kinds[i - size] != kind || kinds[i + size] != kind;
        }

        // Картинка BMP (24 бита) собирается вручную, без текстур Unity. Строки rgb идут сверху вниз.
        private static byte[] Bmp(int size, byte[] rgb)
        {
            int stride = (size * 3 + 3) & ~3;
            int dataSize = stride * size;
            var b = new byte[54 + dataSize];
            void W32(int at, int v) { b[at] = (byte)v; b[at + 1] = (byte)(v >> 8); b[at + 2] = (byte)(v >> 16); b[at + 3] = (byte)(v >> 24); }
            b[0] = (byte)'B';
            b[1] = (byte)'M';
            W32(2, b.Length);
            W32(10, 54);
            W32(14, 40);
            W32(18, size);
            W32(22, size);
            b[26] = 1;
            b[28] = 24;
            W32(34, dataSize);
            for (int row = 0; row < size; row++)
            {
                int src = (size - 1 - row) * size * 3; // BMP хранит строки снизу вверх
                int dst = 54 + row * stride;
                for (int col = 0; col < size; col++)
                {
                    b[dst + col * 3] = rgb[src + col * 3 + 2];
                    b[dst + col * 3 + 1] = rgb[src + col * 3 + 1];
                    b[dst + col * 3 + 2] = rgb[src + col * 3];
                }
            }
            return b;
        }
    }
}
