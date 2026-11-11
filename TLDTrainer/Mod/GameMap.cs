using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace TLDTrainer
{
    // Нарисованная карта региона из самой игры (та же, что в игровом меню «Карта», но целиком, без тумана).
    // Картинки берутся из интерфейса карты игры, по одному слою за кадр, сжимаются в фоновом потоке.
    internal static class GameMap
    {
        private const float StartDelay = 6f;
        private const int MaxBoard = 4096;

        private sealed class Layer
        {
            public UITexture Widget;
            public Texture Tex;
            public string Key;
            public AsyncOperationHandle<Texture2D> Handle;
            public bool Loading;
            public float X0, Y0, X1, Y1; // в координатах карты игры, ось Y вверх
            public float U0, V0, U1, V1;
            public float Opacity;
            public int Depth;
            public int W, H;
            public byte[] Rgba; // строки снизу вверх, как отдаёт видеокарта
        }

        private sealed class Job
        {
            public string Scene;
            public List<Layer> Layers;
            public int Next;
            public double[] Affine; // мир (x, z) → пиксели доски: bx = a·x + b·z + c, by = d·x + e·z + f
            public int Width, Height;
            public float K, X0, Y1;
            public bool Placed;
            public float LoadStarted;
            // Как игра переводит мир в координаты своей карты (замер в точке W0): S = P0 + J·(мир − W0).
            public Vector3 W0, P0;
            public double Dxx, Dxz, Dyx, Dyz;
            public System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        }

        private static string _seenScene;
        private static float _startAt = float.MaxValue;
        private static Job _job;
        private static Task<object> _finish;
        private static object _result;
        private static string _unavailable;

        public static void Tick()
        {
            string scene = MapService.SceneName();
            if (scene != _seenScene)
            {
                _seenScene = scene;
                Reset(keepScene: true);
                _startAt = Time.realtimeSinceStartup + StartDelay;
            }
            if (_finish != null)
            {
                if (!_finish.IsCompleted) return;
                if (_finish.IsCompletedSuccessfully)
                {
                    _result = _finish.Result;
                    MelonLogger.Msg("Карта игры: готова.");
                }
                else
                {
                    _unavailable = "не удалось сжать картинку карты";
                    MelonLogger.Warning("Карта игры: " + _finish.Exception?.GetBaseException().Message);
                }
                _finish = null;
                return;
            }
            if (_job == null && _result == null && _unavailable == null && Time.realtimeSinceStartup >= _startAt) Start();
            if (_job != null) Step();
        }

        public static void Fail(string why)
        {
            if (_job != null) ReleaseAll(_job);
            _job = null;
            _finish = null;
            _unavailable = why;
        }

        public static void Reset(bool keepScene = false)
        {
            if (_job != null) ReleaseAll(_job);
            _job = null;
            _finish = null;
            _result = null;
            _unavailable = null;
            _startAt = float.MaxValue;
            if (!keepScene) _seenScene = null;
        }

        public static object Request(bool force)
        {
            if (MapService.SceneName() != _seenScene) return new { ok = true, ready = false, progress = 0 };
            if (force && _job == null && _finish == null)
            {
                _result = null;
                _unavailable = null;
                Start();
            }
            if (_result != null) return _result;
            if (_unavailable != null) return new { ok = true, ready = true, unavailable = true, reason = _unavailable };
            if (_job == null && _finish == null) _startAt = 0f;
            int progress = _job == null ? (_finish != null ? 95 : 0) : _job.Next * 90 / Math.Max(1, _job.Layers.Count);
            return new { ok = true, ready = false, progress };
        }

        private static void Start()
        {
            // Имена картинок карты берём из каталога игры, он читается в фоне.
            if (!GameCatalog.Keys.IsCompleted)
            {
                _startAt = Time.realtimeSinceStartup + 1f;
                return;
            }
            _startAt = float.MaxValue;
            string scene = MapService.SceneName();
            try
            {
                _job = Prepare(scene, out _unavailable);
                if (_job == null) MelonLogger.Msg("Карта игры: " + _unavailable);
            }
            catch (Exception e)
            {
                _job = null;
                _unavailable = "ошибка: " + e.Message;
                MelonLogger.Warning("Карта игры: " + e);
            }
        }

        private static Job Prepare(string scene, out string why)
        {
            why = null;
            if (!MapService.Outdoors())
            {
                why = "в помещении у игры нет карты";
                return null;
            }
            if (!InterfaceManager.TryGetPanel<Panel_Map>(out var panel) || panel == null)
            {
                why = "окно карты игры ещё не загружено — откройте карту в игре один раз";
                return null;
            }
            RegionMap region = null;
            var maps = panel.m_MapObjects;
            for (int i = 0; maps != null && i < maps.Count; i++)
            {
                var rm = maps[i];
                if (rm == null) continue;
                bool match = string.Equals(rm.m_RegionName, scene, StringComparison.OrdinalIgnoreCase);
                if (!match)
                {
                    try
                    {
                        match = rm.IsRegionMatch(scene);
                    }
                    catch
                    {
                        // сравним только по имени
                    }
                }
                if (match)
                {
                    region = rm;
                    break;
                }
            }
            if (region == null || region.m_RegionMapObject == null)
            {
                why = $"у локации «{scene}» нет карты в игре";
                return null;
            }

            var root = region.m_RegionMapObject.transform;
            var space = panel.m_MapElementsTransform != null ? panel.m_MapElementsTransform : root.parent ?? root;
            MelonLogger.Msg($"Карта игры: «{region.m_RegionName}», центр {region.m_CenterOfScene}, радиус {region.m_RadiusOfScene}, угол {region.m_Angle}");

            var layers = new List<Layer>();
            var spots = new List<Layer>();
            Layer biggest = null;
            var widgets = region.m_RegionMapObject.GetComponentsInChildren<UITexture>(true);
            foreach (var w in widgets)
            {
                if (w == null || !w.enabled || !ActiveUpTo(w.transform, root)) continue;
                var c = w.worldCorners;
                float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    var p = space.InverseTransformPoint(c[i]);
                    x0 = Math.Min(x0, p.x);
                    x1 = Math.Max(x1, p.x);
                    y0 = Math.Min(y0, p.y);
                    y1 = Math.Max(y1, p.y);
                }
                var tex = w.mainTexture;
                var uv = w.uvRect;
                MelonLogger.Msg($"Карта игры: виджет «{w.gameObject.name}», текстура {(tex == null ? "нет" : $"{tex.name} {tex.width}×{tex.height}")}, " +
                                $"x {x0:0.0}…{x1:0.0}, y {y0:0.0}…{y1:0.0}, uv {uv}, глубина {w.depth}");
                if (x1 - x0 < 1f || y1 - y0 < 1f) continue;
                var l = new Layer
                {
                    Widget = w,
                    Tex = tex != null && tex.width >= 64 && tex.height >= 64 ? tex : null,
                    X0 = x0, Y0 = y0, X1 = x1, Y1 = y1,
                    U0 = uv.xMin, V0 = uv.yMin, U1 = uv.xMax, V1 = uv.yMax,
                    Opacity = Math.Clamp(w.color.a, 0f, 1f),
                    Depth = w.depth,
                };
                if (biggest == null || (x1 - x0) * (y1 - y0) > (biggest.X1 - biggest.X0) * (biggest.Y1 - biggest.Y0)) biggest = l;
                spots.Add(l);
                if (l.Tex != null) layers.Add(l);
            }

            // Пока карту в игре не открывали, картинок в окне карты нет — загружаем их сами по имени региона.
            if (layers.Count == 0)
            {
                if (biggest == null)
                {
                    // Виджетов под картинку нет (их создаёт окно карты при открытии). Тогда картинка занимает
                    // всю локацию: центр ± радиус из настроек карты игры.
                    LogHierarchy(region.m_RegionMapObject.transform, 0);
                    var cs = region.m_CenterOfScene;
                    var rs = region.m_RadiusOfScene;
                    float fx0 = float.MaxValue, fy0 = float.MaxValue, fx1 = float.MinValue, fy1 = float.MinValue;
                    foreach (var (dx, dz) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                    {
                        var p = panel.WorldPositionToMapPosition(region.m_RegionName, new Vector3 { x = cs.x + dx * rs.x, y = cs.y, z = cs.z + dz * rs.z });
                        fx0 = Math.Min(fx0, p.x);
                        fx1 = Math.Max(fx1, p.x);
                        fy0 = Math.Min(fy0, p.y);
                        fy1 = Math.Max(fy1, p.y);
                    }
                    MelonLogger.Msg($"Карта игры: картинка по центру и радиусу, x {fx0:0.0}…{fx1:0.0}, y {fy0:0.0}…{fy1:0.0}");
                    if (fx1 - fx0 < 1f || fy1 - fy0 < 1f)
                    {
                        why = "не нашлось места под картинку карты игры";
                        return null;
                    }
                    biggest = new Layer { X0 = fx0, Y0 = fy0, X1 = fx1, Y1 = fy1 };
                }
                var keys = TextureKeys(region.m_RegionName);
                if (keys.Count == 0)
                {
                    why = $"не нашлось картинок карты для «{region.m_RegionName}»";
                    return null;
                }
                int depth = 0;
                foreach (var key in keys)
                {
                    // Фон — на самом виджете карты региона, детали — на дочернем «...DetailMap».
                    bool details = key.StartsWith("map_details_", StringComparison.OrdinalIgnoreCase);
                    Layer spot = null;
                    foreach (var c in spots)
                    {
                        bool isDetail = c.Widget.gameObject.name.IndexOf("Detail", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool isRoot = c.Widget.transform == root;
                        if (details ? isDetail : isRoot)
                        {
                            spot = c;
                            break;
                        }
                    }
                    if (spot == null && !details)
                        foreach (var c in spots)
                            if (c.Widget.gameObject.name.IndexOf("Detail", StringComparison.OrdinalIgnoreCase) < 0 && (spot == null || c == biggest)) spot = c;
                    spot ??= biggest;
                    MelonLogger.Msg($"Карта игры: загружаю картинку «{key}» в {(spot.Widget != null ? "«" + spot.Widget.gameObject.name + "»" : "центр и радиус")}");
                    layers.Add(new Layer
                    {
                        Key = key,
                        X0 = spot.X0, Y0 = spot.Y0, X1 = spot.X1, Y1 = spot.Y1,
                        U0 = spot.Widget != null ? spot.U0 : 0, V0 = spot.Widget != null ? spot.V0 : 0,
                        U1 = spot.Widget != null ? spot.U1 : 1, V1 = spot.Widget != null ? spot.V1 : 1,
                        Opacity = spot.Widget != null && spot.Opacity > 0.05f ? spot.Opacity : 1f,
                        Depth = spot.Widget != null ? spot.Depth * 100 + depth++ : depth++,
                        Handle = Addressables.LoadAssetAsync<Texture2D>(key),
                        Loading = true,
                    });
                }
            }
            layers.Sort((a, b) => a.Depth.CompareTo(b.Depth));

            // Как игра переводит мировые координаты в координаты своей карты: замеряем в трёх точках.
            var player = GameManager.GetPlayerTransform();
            var w0 = player != null ? player.position : region.m_CenterOfScene;
            var p0 = panel.WorldPositionToMapPosition(region.m_RegionName, w0);
            var px = panel.WorldPositionToMapPosition(region.m_RegionName, new Vector3 { x = w0.x + 100f, y = w0.y, z = w0.z });
            var pz = panel.WorldPositionToMapPosition(region.m_RegionName, new Vector3 { x = w0.x, y = w0.y, z = w0.z + 100f });
            MelonLogger.Msg($"Карта игры: игрок {w0} → {p0}; +100 по X → {px}; +100 по Z → {pz}");

            var job = new Job
            {
                Scene = scene,
                Layers = layers,
                W0 = w0,
                P0 = p0,
                Dxx = (px.x - p0.x) / 100.0,
                Dxz = (pz.x - p0.x) / 100.0,
                Dyx = (px.y - p0.y) / 100.0,
                Dyz = (pz.y - p0.y) / 100.0,
                LoadStarted = Time.realtimeSinceStartup,
            };
            if (Math.Abs(job.Dxx * job.Dyz - job.Dxz * job.Dyx) < 1e-9)
            {
                why = "не удалось привязать карту игры к координатам";
                ReleaseAll(job);
                return null;
            }
            return job;
        }

        // Картинки карты региона в каталоге игры: map_bg_<регион> и map_details_<регион>, имена бывают сокращены.
        private static List<string> TextureKeys(string region)
        {
            var all = GameCatalog.Keys.Result;
            string Norm(string s) => s.ToLowerInvariant().Replace("region", "").Replace("sandbox", "").Replace("story", "").Replace("_", "");
            string want = Norm(region);
            var found = new List<string>();
            foreach (var prefix in new[] { "map_bg_", "map_details_" })
            {
                string best = null;
                int bestScore = 0;
                foreach (var k in all)
                {
                    if (!k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    string n = Norm(k.Substring(prefix.Length));
                    int score = n == want ? 3 : (want.StartsWith(n) || n.StartsWith(want)) && Math.Min(n.Length, want.Length) >= 5 ? 2 : 0;
                    if (score > bestScore || (score == bestScore && score > 0 && k.IndexOf("Sandbox", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        best = k;
                        bestScore = score;
                    }
                }
                if (best != null) found.Add(best);
            }
            return found;
        }

        // Когда размеры картинок известны: масштаб доски и перевод мира в пиксели доски.
        private static bool Place(Job j)
        {
            float bx0 = float.MaxValue, by0 = float.MaxValue, bx1 = float.MinValue, by1 = float.MinValue, k = 0f;
            foreach (var l in j.Layers)
            {
                bx0 = Math.Min(bx0, l.X0);
                by0 = Math.Min(by0, l.Y0);
                bx1 = Math.Max(bx1, l.X1);
                by1 = Math.Max(by1, l.Y1);
                k = Math.Max(k, l.W * Math.Abs(l.U1 - l.U0) / (l.X1 - l.X0));
            }
            k = Math.Min(k, MaxBoard / Math.Max(bx1 - bx0, by1 - by0));
            var w0 = j.W0;
            var p0 = j.P0;
            double a = k * j.Dxx, b = k * j.Dxz, cc = k * (p0.x - bx0) - a * w0.x - b * w0.z;
            double d = -k * j.Dyx, e = -k * j.Dyz, f = k * (by1 - p0.y) - d * w0.x - e * w0.z;
            j.Width = (int)Math.Ceiling((bx1 - bx0) * k);
            j.Height = (int)Math.Ceiling((by1 - by0) * k);
            j.K = k;
            j.X0 = bx0;
            j.Y1 = by1;
            j.Affine = new[] { a, b, cc, d, e, f };
            double ppx = a * w0.x + b * w0.z + cc, ppy = d * w0.x + e * w0.z + f;
            MelonLogger.Msg($"Карта игры: доска {j.Width}×{j.Height}, игрок на доске ({ppx:0}, {ppy:0})");
            return !(ppx < -j.Width * 0.5 || ppx > j.Width * 1.5 || ppy < -j.Height * 0.5 || ppy > j.Height * 1.5);
        }

        private static void ReleaseAll(Job j)
        {
            foreach (var l in j.Layers)
            {
                if (l.Key == null) continue;
                try
                {
                    Addressables.Release(l.Handle);
                }
                catch
                {
                    // уже освобождено
                }
                l.Key = null;
            }
        }

        // Для отладки: что лежит внутри карты региона (имена и компоненты), до трёх уровней.
        private static void LogHierarchy(Transform t, int depth)
        {
            if (depth > 3) return;
            var names = new List<string>();
            var comps = t.GetComponents<Component>();
            foreach (var c in comps)
                if (c != null) names.Add(c.GetIl2CppType().Name);
            MelonLogger.Msg($"Карта игры: {new string(' ', depth * 2)}{t.name} [{string.Join(", ", names)}]{(t.gameObject.activeSelf ? "" : " (скрыт)")}");
            for (int i = 0; i < t.childCount && i < 20; i++) LogHierarchy(t.GetChild(i), depth + 1);
        }

        private static bool ActiveUpTo(Transform t, Transform root)
        {
            // Сама карта региона скрыта, пока окно карты закрыто, поэтому её собственный флаг не проверяем.
            for (var x = t; x != null; x = x.parent)
            {
                if (x == root) return true;
                if (!x.gameObject.activeSelf) return false;
            }
            return true;
        }

        // Один слой за кадр: копия текстуры через видеокарту (без камеры) и чтение пикселей.
        private static void Step()
        {
            var j = _job;
            if (!j.Placed)
            {
                bool waiting = false;
                foreach (var l in j.Layers)
                    if (l.Loading && !l.Handle.IsDone) waiting = true;
                if (waiting && Time.realtimeSinceStartup - j.LoadStarted < 30f) return;
                foreach (var l in j.Layers)
                {
                    if (!l.Loading) continue;
                    l.Loading = false;
                    if (l.Handle.IsDone && l.Handle.Status == AsyncOperationStatus.Succeeded && l.Handle.Result != null) l.Tex = l.Handle.Result;
                    else MelonLogger.Warning($"Карта игры: картинка «{l.Key}» не загрузилась");
                }
                j.Layers.RemoveAll(l => l.Tex == null);
                foreach (var l in j.Layers)
                {
                    l.W = l.Tex.width;
                    l.H = l.Tex.height;
                }
                j.Placed = true;
                if (j.Layers.Count == 0 || !Place(j))
                {
                    ReleaseAll(j);
                    _job = null;
                    _unavailable = j.Layers.Count == 0 ? "картинки карты игры не загрузились" : "координаты карты игры не совпали с локацией";
                    MelonLogger.Msg("Карта игры: " + _unavailable);
                }
                return;
            }
            if (j.Next < j.Layers.Count)
            {
                var l = j.Layers[j.Next++];
                try
                {
                    l.Rgba = ReadTexture(l.Tex, l.W, l.H);
                }
                catch (Exception e)
                {
                    MelonLogger.Warning($"Карта игры: слой {j.Next} не прочитан — {e.Message}");
                }
                return;
            }

            _job = null;
            ReleaseAll(j);
            var ready = j.Layers.FindAll(l => l.Rgba != null);
            if (ready.Count == 0)
            {
                _unavailable = "не удалось прочитать картинки карты игры";
                return;
            }
            _finish = Task.Run<object>(() => Build(j, ready));
        }

        internal static byte[] ReadTexture(Texture tex, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var prev = RenderTexture.active;
            Texture2D copy = null;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                return copy.GetRawTextureData().AsSpan().ToArray();
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                if (copy != null) UnityEngine.Object.Destroy(copy);
            }
        }

        private static object Build(Job j, List<Layer> layers)
        {
            var list = new List<object>();
            foreach (var l in layers)
            {
                list.Add(new
                {
                    png = Convert.ToBase64String(Png(l.Rgba, l.W, l.H)),
                    x = (l.X0 - j.X0) * j.K,
                    y = (j.Y1 - l.Y1) * j.K,
                    w = (l.X1 - l.X0) * j.K,
                    h = (l.Y1 - l.Y0) * j.K,
                    u0 = l.U0, v0 = l.V0, u1 = l.U1, v1 = l.V1,
                    opacity = l.Opacity,
                });
                l.Rgba = null;
            }
            MelonLogger.Msg($"Карта игры: собрана за {j.Clock.Elapsed.TotalSeconds:0.0} с.");
            return new
            {
                ok = true,
                ready = true,
                kind = "game",
                width = j.Width,
                height = j.Height,
                affine = j.Affine,
                layers = list,
                scene = j.Scene,
                outdoors = true,
            };
        }

        // Простой PNG (RGBA, 8 бит), строки переворачиваются: у видеокарты первая строка — нижняя.
        internal static byte[] Png(byte[] rgba, int w, int h)
        {
            using var ms = new MemoryStream();
            ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var ihdr = new byte[13];
            BE(ihdr, 0, w);
            BE(ihdr, 4, h);
            ihdr[8] = 8;
            ihdr[9] = 6;
            Chunk(ms, "IHDR", ihdr);
            using (var raw = new MemoryStream())
            {
                using (var z = new ZLibStream(raw, System.IO.Compression.CompressionLevel.Fastest, true))
                {
                    int stride = w * 4;
                    for (int row = h - 1; row >= 0; row--)
                    {
                        z.WriteByte(0);
                        z.Write(rgba, row * stride, stride);
                    }
                }
                Chunk(ms, "IDAT", raw.ToArray());
            }
            Chunk(ms, "IEND", Array.Empty<byte>());
            return ms.ToArray();
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4];
            BE(len, 0, data.Length);
            s.Write(len);
            var t = System.Text.Encoding.ASCII.GetBytes(type);
            s.Write(t);
            s.Write(data);
            uint crc = Crc(t, 0xFFFFFFFFu);
            crc = Crc(data, crc) ^ 0xFFFFFFFFu;
            var cb = new byte[4];
            BE(cb, 0, (int)crc);
            s.Write(cb);
        }

        private static uint[] _crcTable;

        private static uint Crc(byte[] data, uint crc)
        {
            if (_crcTable == null)
            {
                var t = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    t[n] = c;
                }
                _crcTable = t;
            }
            foreach (byte b in data) crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        private static void BE(byte[] b, int o, int v)
        {
            b[o] = (byte)(v >> 24);
            b[o + 1] = (byte)(v >> 16);
            b[o + 2] = (byte)(v >> 8);
            b[o + 3] = (byte)v;
        }
    }
}
