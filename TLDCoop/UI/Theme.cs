using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace TLDCoop.UI
{
    // Оформление окон мода как у меню игры (0.19.24): те же картинки из атласа интерфейса игры (Base Atlas, NGUI),
    // тот же шрифт (по-русски Nazhdak, по-английски PiS Creatinin Pro) и те же сочетания слоёв, что у окон
    // «Сон», «Подтверждение» и экрана после смерти. Раньше рамки рисовались плоскими прямоугольниками, а шрифт
    // по ошибке брался китайский (NotoSansCJK), отсюда и чужой вид.
    // Картинки рисуются Graphics.Internal_DrawTexture (его же зовёт GUI.DrawTexture): GUI.DrawTextureWithTexCoords
    // в сборке игры вырезан. Не нашли атлас или рисование не работает — остаётся плоский вид с теми же цветами.
    internal static class Theme
    {
        public static bool On { get; private set; }
        private static bool _tried;

        // Цвета игры (из префабов её окон).
        public static readonly Color Caption = new(0.43f, 0.51f, 0.49f, 1f);      // подписи: «Часы сна», «Запас калорий»
        public static readonly Color Body = new(0.78f, 0.78f, 0.78f, 1f);         // описание
        public static readonly Color White = new(0.93f, 0.93f, 0.93f, 1f);
        public static readonly Color Dim = new(0.60f, 0.62f, 0.61f, 1f);          // выключенное, второстепенное
        public static readonly Color Red = new(0.64f, 0.20f, 0.23f, 1f);          // причина смерти
        public static readonly Color RedIcon = new(0.76f, 0.31f, 0.31f, 1f);      // значки на экране смерти
        public static readonly Color Good = new(0.62f, 0.84f, 0.62f, 1f);
        public static readonly Color Warn = new(0.95f, 0.55f, 0.45f, 1f);

        // Плоский запасной вид (без картинок).
        public static readonly Color PanelBg = new(0.06f, 0.07f, 0.07f, 0.92f);
        public static readonly Color ButtonBg = new(0.10f, 0.11f, 0.11f, 0.85f);
        public static readonly Color ButtonHover = new(0.25f, 0.29f, 0.28f, 0.90f);
        public static readonly Color FieldBg = new(0.03f, 0.03f, 0.03f, 0.85f);
        public static readonly Color Border = new(1f, 1f, 1f, 0.16f);
        public static readonly Color Text = new(0.88f, 0.88f, 0.86f, 1f);
        public static readonly Color Accent = new(0.86f, 0.86f, 0.83f, 0.95f);   // выбранное (жесты, полоски)
        public static readonly Color Selected = new(0.25f, 0.29f, 0.28f, 0.95f);

        // ---------- Шрифты ----------

        private static Font _ru, _en;
        private static Font _current;
        private static readonly Dictionary<long, GUIStyle> Styles = new();

        // Вызывать из OnGUI (стили доступны только там). Повторные вызовы дёшевы: язык и атлас проверяются по таймеру.
        public static void Apply()
        {
            L.Refresh();
            if (!_tried)
            {
                _tried = true;
                try
                {
                    var label = GUI.skin.label;
                    label.normal.textColor = Text;
                    label.richText = true;
                    label.fontSize = 14;
                    // Строка шрифта игры выше 22 пикселей: без этого у букв срезается низ.
                    label.clipping = TextClipping.Overflow;
                    try { label.padding = new RectOffset(2, 2, 1, 1); } catch { }
                    PickFonts();
                    Fill(new Rect(-10f, -10f, 1f, 1f), Color.clear);   // проверка, что рисование текстурой работает
                    On = true;
                    MelonLogger.Msg($"[окно] оформление игры: шрифт {(_ru != null ? _ru.name : "нет")} / {(_en != null ? _en.name : "нет")}");
                }
                catch (Exception e)
                {
                    On = false;
                    MelonLogger.Msg("[окно] простое оформление: " + e.Message);
                }
            }
            // Общий стиль надписей (ники над головами, старые окна) — всегда русским шрифтом игры: в нём есть и латиница,
            // а в латинском шрифте нет русских букв, и русский ник в английской игре пропал бы.
            var want = _ru ?? _en;
            if (want != null && want != _current)
            {
                _current = want;
                try { GUI.skin.label.font = want; } catch { }
                Styles.Clear();
            }
            FindAtlas();
        }

        private static void PickFonts()
        {
            try
            {
                var fonts = Resources.FindObjectsOfTypeAll<Font>().Where(f => f != null).ToList();
                // «… - Dialogue» — тот же файл для субтитров, берём основной.
                _ru = fonts.FirstOrDefault(f => f.name == "Nazhdak-Regular") ?? fonts.FirstOrDefault(f => f.name.StartsWith("Nazhdak"));
                _en = fonts.FirstOrDefault(f => f.name == "PiS - PiS Creatinin Pro") ?? fonts.FirstOrDefault(f => f.name.StartsWith("PiS"));
                if (_ru == null || _en == null)
                    MelonLogger.Msg("[окно] шрифты игры: " + string.Join(", ", fonts.Select(f => f.name).Distinct().Take(40)));
            }
            catch { }
        }

        // В латинском шрифте игры нет русских букв: ник «Талян» в английской игре пишем русским шрифтом.
        private static Font FontFor(bool cyrillic) => L.En && !cyrillic && _en != null ? _en : _ru ?? _en;

        public static bool HasCyrillic(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s) if (c >= 'Ѐ' && c <= 'ӿ') return true;
            return false;
        }

        // Стиль надписи нужного размера (в пикселях экрана) и выравнивания.
        public static GUIStyle Style(int px, TextAnchor align, bool cyrillic, bool wrap = false)
        {
            var font = FontFor(cyrillic);
            long key = ((long)px << 16) | ((long)align << 8) | (font == _en ? 2L : 0L) | (wrap ? 1L : 0L);
            if (Styles.TryGetValue(key, out var s) && s != null) return s;
            s = new GUIStyle(GUI.skin.label) { fontSize = px, alignment = align, wordWrap = wrap, richText = true, clipping = TextClipping.Overflow };
            if (font != null) s.font = font;
            s.normal.textColor = Color.white;
            Styles[key] = s;
            return s;
        }

        // ---------- Атлас игры ----------

        private struct Spr
        {
            public Rect Uv;
            public float W, H;
            public float L, R, T, B;
        }

        private static Texture _atlas;
        private static readonly Dictionary<string, Spr> Sprites = new();
        private static float _nextFind;
        private static bool _drawBroken;
        private static Material _blend;
        private static UnityEngine.Internal_DrawTextureArguments _args;

        public static bool HasSprites => _atlas != null && !_drawBroken;

        private static readonly string[] Needed =
        {
            "inv_statBack", "innerGlow2", "clothing_slotSelected", "bg_texture1", "backHeader",
            "menuButtonCenter_fill", "menuButtonCenter_outer", "menuButtonBack_fill", "menuButtonBack_outer",
            "linebreak_description", "checkboxA", "checkbox_checkmarkA", "arrow_nav2", "arrow_nav3",
            "arrow_nav2_back", "arrow_nav3_back", "TLDHOME_MenuHighlight", "inv_slotBack", "vignetteFullscreen",
            "icoMap_location", "icoMap_campfireBurnt", "icoMap_grave", "ico_map", "whiteSquare", "radial_center",
        };

        private static void FindAtlas()
        {
            if (_atlas != null || _drawBroken) return;
            float now = Time.unscaledTime;
            if (now < _nextFind) return;
            _nextFind = now + 3f;
            try
            {
                foreach (var a in Resources.FindObjectsOfTypeAll<Il2Cpp.UIAtlas>())
                {
                    if (a == null) continue;
                    Il2Cpp.UISpriteData probe = null;
                    try { probe = a.GetSprite("menuButtonCenter_fill"); } catch { }
                    if (probe == null) continue;
                    var tex = a.texture;
                    if (tex == null || tex.width <= 0) continue;
                    float tw = tex.width, th = tex.height;
                    Sprites.Clear();
                    foreach (var n in Needed)
                    {
                        Il2Cpp.UISpriteData d = null;
                        try { d = a.GetSprite(n); } catch { }
                        if (d == null) continue;
                        // У NGUI y считается сверху, у текстуры — снизу.
                        Sprites[n] = new Spr
                        {
                            Uv = new Rect(d.x / tw, 1f - (d.y + d.height) / th, d.width / tw, d.height / th),
                            W = d.width, H = d.height,
                            L = d.borderLeft, R = d.borderRight, T = d.borderTop, B = d.borderBottom,
                        };
                    }
                    _blend = GUI.blendMaterial;
                    _atlas = tex;
                    MelonLogger.Msg($"[окно] атлас игры найден: {tex.name} {tex.width}x{tex.height}, картинок {Sprites.Count} из {Needed.Length}");
                    return;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Msg("[окно] атлас игры: " + e.Message);
                _nextFind = now + 30f;
            }
        }

        // Одна картинка атласа в прямоугольник экрана. Только на Repaint (иначе рисовать нельзя).
        private static void Raw(Rect r, Rect uv, Color c)
        {
            if (r.width <= 0.5f || r.height <= 0.5f || c.a <= 0.003f) return;
            var a = _args ??= new UnityEngine.Internal_DrawTextureArguments();   // один на все вызовы: без мусора каждый кадр
            a.screenRect = r;
            a.sourceRect = uv;
            a.texture = _atlas;
            a.mat = _blend;
            a.color = c;
            a.leftBorderColor = c;
            a.rightBorderColor = c;
            a.topBorderColor = c;
            a.bottomBorderColor = c;
            Graphics.Internal_DrawTexture(ref a);
        }

        private static bool Repaint
        {
            get { try { return Event.current != null && Event.current.type == EventType.Repaint; } catch { return false; } }
        }

        // Картинка атласа. sliced: края (рамка спрайта) не растягиваются, их размер умножается на k.
        // false — картинки нет (вызвавший рисует плоский запасной вид).
        public static bool Sprite(Rect r, string name, Color c, bool sliced = false, float k = 1f)
        {
            if (!HasSprites || !Sprites.TryGetValue(name, out var s)) return false;
            if (!Repaint) return true;
            try
            {
                if (!sliced || s.L + s.R + s.T + s.B <= 0f) { Raw(r, s.Uv, c); return true; }
                float l = s.L * k, rr = s.R * k, t = s.T * k, b = s.B * k;
                if (l + rr > r.width) { float f = r.width / (l + rr); l *= f; rr *= f; }
                if (t + b > r.height) { float f = r.height / (t + b); t *= f; b *= f; }
                float uw = s.Uv.width / s.W, uh = s.Uv.height / s.H;   // одна точка спрайта в долях текстуры
                float[] xs = { r.x, r.x + l, r.xMax - rr, r.xMax };
                float[] ys = { r.y, r.y + t, r.yMax - b, r.yMax };
                float[] us = { s.Uv.x, s.Uv.x + s.L * uw, s.Uv.xMax - s.R * uw, s.Uv.xMax };
                // Экран: y вниз, верхняя полоса — вверху спрайта (у текстуры это большие v).
                float[] vs = { s.Uv.yMax, s.Uv.yMax - s.T * uh, s.Uv.y + s.B * uh, s.Uv.y };
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                    {
                        var sr = new Rect(xs[i], ys[j], xs[i + 1] - xs[i], ys[j + 1] - ys[j]);
                        var uv = new Rect(us[i], vs[j + 1], us[i + 1] - us[i], vs[j] - vs[j + 1]);
                        Raw(sr, uv, c);
                    }
                return true;
            }
            catch (Exception e)
            {
                _drawBroken = true;
                MelonLogger.Msg("[окно] картинки игры не рисуются, плоский вид: " + e.Message);
                return false;
            }
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Frame(Rect r, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, 1f), c);
            Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            Fill(new Rect(r.x, r.y, 1f, r.height), c);
            Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

        public static void Centered(Rect r, string text) =>
            GUI.Label(r, text, Style(GUI.skin.label.fontSize > 0 ? GUI.skin.label.fontSize : 14, TextAnchor.MiddleCenter, HasCyrillic(text)));

        // ---------- Сочетания слоёв, как в окнах игры (все размеры — в пикселях экрана, k — масштаб) ----------

        // Окно: тёмная подложка, внутренняя тень, светлый ореол вокруг и «пятно» на фоне (Panel_Confirmation).
        public static void Window(Rect r, float k, bool mark = true)
        {
            if (!HasSprites) { Fill(r, PanelBg); Frame(r, Border); return; }
            float gx = 41f * k, gy = 40f * k;
            Sprite(new Rect(r.x - gx, r.y - gy, r.width + gx * 2f, r.height + gy * 2f), "clothing_slotSelected", new Color(0.98f, 0.98f, 0.98f, 0.24f), true, k);
            Sprite(r, "inv_statBack", new Color(0f, 0f, 0f, 0.62f), true, k);
            Sprite(r, "innerGlow2", new Color(0f, 0f, 0f, 0.24f), true, k);
            if (mark)
            {
                float h = Math.Min(r.height * 0.8f, r.width * 0.9f), w = h * 0.76f;
                Sprite(new Rect(r.center.x - w / 2f, r.center.y - h / 2f + 10f * k, w, h), "bg_texture1", new Color(0.98f, 0.98f, 0.98f, 0.08f));
            }
        }

        // Кнопка меню игры: тёмная заливка и светлое свечение; под мышью заливка зеленовато-серая, свечение ярче.
        public static void ButtonBack(Rect r, bool hover, bool active, bool back = false)
        {
            string fill = back ? "menuButtonBack_fill" : "menuButtonCenter_fill", outer = back ? "menuButtonBack_outer" : "menuButtonCenter_outer";
            if (!HasSprites)
            {
                Fill(r, hover || active ? ButtonHover : ButtonBg);
                Frame(r, hover || active ? new Color(1f, 1f, 1f, 0.45f) : Border);
                return;
            }
            // Наведение светит ярче выбранного, так что и на выбранной кнопке оно заметно.
            Color fc = hover ? new Color(0.43f, 0.51f, 0.49f, active ? 0.30f : 0.22f) : active ? new Color(0.43f, 0.51f, 0.49f, 0.14f) : new Color(0f, 0f, 0f, 0.45f);
            float oa = hover ? (active ? 0.92f : 0.78f) : active ? 0.55f : 0.36f;
            Sprite(r, fill, fc);
            float ow = r.width * 1.24f, oh = r.height * 2.13f;
            Sprite(new Rect(r.center.x - ow / 2f, r.center.y - oh / 2f, ow, oh), outer, new Color(0.98f, 0.98f, 0.98f, oa));
        }

        public static Color ButtonText(bool hover, bool active, bool disabled = false) =>
            disabled ? new Color(0.98f, 0.98f, 0.98f, 0.28f) : hover ? new Color(0.98f, 0.98f, 0.98f, 0.98f) : active ? new Color(0.98f, 0.98f, 0.98f, 0.82f) : new Color(0.98f, 0.98f, 0.98f, 0.62f);

        // Разделитель с тенью (linebreak_description).
        public static void Line(Rect r, float a = 0.5f)
        {
            if (!HasSprites) { Fill(new Rect(r.x, r.center.y, r.width, 1f), new Color(1f, 1f, 1f, a * 0.4f)); return; }
            Sprite(new Rect(r.x, r.y + 1f, r.width, r.height), "linebreak_description", new Color(0f, 0f, 0f, 0.24f));
            Sprite(r, "linebreak_description", new Color(a, a, a, 1f));
        }

        // Тёмная плашка (список игроков, поле чата) — inv_slotBack, как у блока цифр в меню «Сон».
        public static void Slot(Rect r, float k, float a = 0.44f)
        {
            if (!Sprite(r, "inv_slotBack", new Color(0.17f, 0.21f, 0.21f, a), true, k * 0.5f)) { Fill(r, new Color(0.1f, 0.12f, 0.12f, 0.7f)); }
        }
    }
}
