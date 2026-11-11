using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace TLDTrainer
{
    // Значки с угольной карты игры: вырезаем их из атласа NGUI окна карты, чтобы трейнер рисовал те же значки.
    internal static class MapSprites
    {
        private static readonly Dictionary<string, string> Cache = new();
        private static readonly HashSet<string> Missing = new();
        private static float[] _color;

        public static object Get(string[] names)
        {
            var want = new List<string>();
            foreach (var n in names)
                if (!string.IsNullOrEmpty(n) && !Cache.ContainsKey(n) && !Missing.Contains(n)) want.Add(n);
            if (want.Count > 0) Load(want);
            var result = new Dictionary<string, string>();
            foreach (var n in names)
                if (n != null && Cache.TryGetValue(n, out var png)) result[n] = png;
            return new { ok = true, sprites = result, color = _color ?? new[] { 0.16f, 0.15f, 0.14f, 1f } };
        }

        private static void Load(List<string> want)
        {
            if (!InterfaceManager.TryGetPanel<Panel_Map>(out var panel) || panel == null) return;
            var atlases = new List<UIAtlas>();
            void Collect(GameObject go)
            {
                if (go == null) return;
                foreach (var s in go.GetComponentsInChildren<UISprite>(true))
                {
                    var a = s != null ? s.atlas : null;
                    if (a != null && !atlases.Exists(x => x.Pointer == a.Pointer)) atlases.Add(a);
                }
            }
            Collect(panel.gameObject);
            Collect(panel.m_BigSpritePrefab);
            Collect(panel.m_SmallSpritePrefab);
            Collect(panel.m_DetailEntryPrefab);
            if (_color == null && panel.m_SmallSpritePrefab != null)
            {
                var icon = panel.m_SmallSpritePrefab.GetComponent<MapIcon>();
                if (icon != null)
                {
                    var c = icon.m_DefaultColour;
                    _color = new[] { c.r, c.g, c.b, c.a };
                }
            }

            foreach (var atlas in atlases)
            {
                var found = new List<(string Name, UISpriteData D)>();
                foreach (var n in want)
                {
                    if (Cache.ContainsKey(n)) continue;
                    UISpriteData d = null;
                    try
                    {
                        d = atlas.GetSprite(n);
                    }
                    catch
                    {
                        // нет такого значка в этом атласе
                    }
                    if (d != null && d.width > 0 && d.height > 0) found.Add((n, d));
                }
                if (found.Count == 0) continue;
                var tex = atlas.texture;
                if (tex == null) continue;
                int W = tex.width, H = tex.height;
                byte[] all = GameMap.ReadTexture(tex, W, H);
                foreach (var (n, d) in found)
                {
                    int w = Math.Min(d.width, W - d.x), h = Math.Min(d.height, H - d.y);
                    if (w <= 0 || h <= 0) continue;
                    var px = new byte[w * h * 4];
                    // Атлас NGUI считает y сверху, а данные текстуры идут снизу вверх.
                    for (int r = 0; r < h; r++)
                        Buffer.BlockCopy(all, ((H - d.y - h + r) * W + d.x) * 4, px, r * w * 4, w * 4);
                    Cache[n] = Convert.ToBase64String(GameMap.Png(px, w, h));
                }
            }
            foreach (var n in want)
                if (!Cache.ContainsKey(n) && Missing.Add(n)) MelonLogger.Msg($"Карта: значок «{n}» не найден в атласах карты");
        }
    }
}
