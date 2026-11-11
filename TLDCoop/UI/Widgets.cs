using System;
using UnityEngine;

namespace TLDCoop.UI
{
    // В сборке игры 2.58 у IMGUI вырезаны GUI.Button, GUI.Toggle и GUI.TextField
    // («Method unstripping failed»). Работают только GUI.Box и GUI.Label, поэтому кнопки,
    // галочки и поля ввода сделаны здесь из них. Мышь и клавиатура читаются через Input
    // в Update (Capture), а в OnGUI виджет проверяет, попал ли клик в его прямоугольник.
    internal static class Widgets
    {
        private static bool _click;            // клик в этом кадре, ещё никем не забран
        private static bool _clickRaw;         // клик в этом кадре вообще (для снятия фокуса)
        private static Vector2 _mouse;         // координаты GUI (y сверху)
        private static bool _held;             // левая кнопка зажата
        private static bool _dragging;
        public static bool Dragging => _dragging;
        private static Vector2 _dragOffset;
        private static string _typed = "";     // набранные в этом кадре символы
        private static bool _inputStringBroken;
        private static float _wheel;           // колёсико в этом кадре (прокрутка длинного окна)

        public static string Focus;            // id поля ввода с фокусом
        public static bool Typing => Focus != null;
        private static int _focusDrawn;        // кадр, в котором поле с фокусом последний раз рисовалось
        private static int _escFrame = -100;

        // Вызывать в начале OnUpdate.
        public static void Capture()
        {
            var mp = Input.mousePosition;
            _mouse = new Vector2(mp.x, Screen.height - mp.y);
            _clickRaw = _click = Input.GetMouseButtonDown(0);
            _held = Input.GetMouseButton(0);
            try { _wheel = Input.mouseScrollDelta.y; } catch { _wheel = 0f; }
            // Поле с фокусом в прошлом кадре не рисовалось (скрыли хронику, сменили вкладку, кооп закончился) —
            // фокус снимаем, иначе \ не закроет окно. Esc тоже снимает фокус.
            if (Focus != null && _focusDrawn < Time.frameCount - 1) Focus = null;
            if (Focus != null && Input.GetKeyDown(KeyCode.Escape)) { Focus = null; _escFrame = Time.frameCount; }
            // Окно мода открыто в игре: игра не получает ни мышь, ни клавиши — камера не крутится за курсором, щелчок по
            // кнопке окна не стреляет, персонаж не идёт (0.19.30). Esc без поля в фокусе закрывает окно.
            bool windowInGame = false;
            try { windowInGame = CoopWindow.Open && Game.GameApi.InGame(); } catch { }
            if (windowInGame && Focus == null && _escFrame != Time.frameCount && Input.GetKeyDown(KeyCode.Escape)) { CoopWindow.Toggle(); _escFrame = Time.frameCount; windowInGame = false; }
            BlockGameInput(Focus != null || windowInGame || Time.frameCount - _escFrame < 3);   // после Esc держим ещё пару кадров, чтобы игра не открыла паузу
            _typed = Focus == null ? "" : ReadTyped();
        }

        // Пока печатаем в поле окна или окно мода открыто в игре, игра не должна получать клавиши и мышь (ц/ы/ф/в — это W/S/A/D, пробел, цифры…).
        // В 2.59 весь ввод игры идёт через InputManager.HasContext, а он молчит, пока m_InputDisableTime > Time.time
        // (s_IgnoreInput там проверяет только колёсико). Продлеваем на 0,25 с каждый кадр; фокус снят — возвращаем прежнее.
        private static bool _blocking;
        private static float _prevDisable, _ourDisable;

        private static void BlockGameInput(bool on)
        {
            if (!on && !_blocking) return;
            try
            {
                if (on)
                {
                    if (!_blocking) { _prevDisable = Il2Cpp.InputManager.m_InputDisableTime; _blocking = true; }
                    float t = Math.Max(Il2Cpp.InputManager.m_InputDisableTime, Time.time + 0.25f);
                    Il2Cpp.InputManager.m_InputDisableTime = t;
                    _ourDisable = t;
                    return;
                }
                _blocking = false;
                // Игра за это время сама не продлевала (закрытие своего окна) — ставим как было.
                if (Il2Cpp.InputManager.m_InputDisableTime <= _ourDisable) Il2Cpp.InputManager.m_InputDisableTime = _prevDisable;
            }
            catch { _blocking = false; }
        }

        private static string ReadTyped()
        {
            if (!_inputStringBroken)
            {
                try
                {
                    return Input.inputString ?? "";
                }
                catch
                {
                    _inputStringBroken = true;
                }
            }
            // Запасной вариант без Input.inputString: латиница, цифры, пробел.
            string s = "";
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            for (var k = KeyCode.A; k <= KeyCode.Z; k++)
                if (Input.GetKeyDown(k)) s += shift ? k.ToString() : k.ToString().ToLowerInvariant();
            for (var k = KeyCode.Alpha0; k <= KeyCode.Alpha9; k++)
                if (Input.GetKeyDown(k)) s += (char)('0' + (k - KeyCode.Alpha0));
            if (Input.GetKeyDown(KeyCode.Space)) s += " ";
            if (Input.GetKeyDown(KeyCode.Period)) s += ".";
            if (Input.GetKeyDown(KeyCode.Minus)) s += shift ? "_" : "-";
            if (Input.GetKeyDown(KeyCode.Backspace)) s += "\b";
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) s += "\n";
            return s;
        }

        // ---------- Масштаб (0.19.24) ----------
        // Окна коопа, смерти и сна размечены в точках экрана игры 1280×720 (как её меню) и растягиваются
        // под высоту экрана: на 1080p всё в 1,5 раза крупнее, как у окон игры. Остальное (ники, стрелки, обмен)
        // по-прежнему в пикселях: K = 1.
        public static float K { get; private set; } = 1f;
        public static float SW => Screen.width / K;
        public static float SH => Screen.height / K;

        public static void Scaled() => K = Mathf.Max(1f, Screen.height / 720f);
        public static void Unscaled() { K = 1f; ClipTop = float.MinValue; ClipBottom = float.MaxValue; }

        private static Rect R(Rect v) => K == 1f ? v : new Rect(v.x * K, v.y * K, v.width * K, v.height * K);
        private static Vector2 M => K == 1f ? _mouse : _mouse / K;
        private static int Px(float size) => Mathf.Max(8, Mathf.RoundToInt(size * K));

        // Прокрутка: вне [ClipTop, ClipBottom] (в точках окна) ничего не рисуется и не нажимается.
        public static float ClipTop = float.MinValue, ClipBottom = float.MaxValue;
        private static bool Visible(Rect v) => v.y >= ClipTop - 1f && v.yMax <= ClipBottom + 1f;

        // Длинное окно: колёсико над областью view двигает scroll. Вернёт сдвиг, который вычесть из y содержимого.
        public static float BeginScroll(Rect view, ref float scroll, float content)
        {
            float max = Math.Max(0f, content - view.height);
            if (max > 0f && view.Contains(M) && Math.Abs(_wheel) > 0.01f) { scroll -= _wheel * 40f; _wheel = 0f; }
            scroll = Mathf.Clamp(scroll, 0f, max);
            ClipTop = view.y;
            ClipBottom = view.yMax;
            if (max > 0f)
            {
                // Полоска прокрутки справа.
                var track = new Rect(view.xMax + 4f, view.y, 3f, view.height);
                Theme.Fill(R(track), new Color(1f, 1f, 1f, 0.08f));
                float th = Math.Max(24f, view.height * view.height / content);
                Theme.Fill(R(new Rect(track.x, view.y + (view.height - th) * scroll / max, 3f, th)), new Color(1f, 1f, 1f, 0.35f));
            }
            return scroll;
        }

        public static void EndScroll() { ClipTop = float.MinValue; ClipBottom = float.MaxValue; }

        private static bool Hover(Rect r) => Visible(r) && r.Contains(M);

        private static bool TakeClick(Rect r)
        {
            if (!_click || !Hover(r)) return false;
            _click = false;
            return true;
        }

        // Перетаскивание окна за полосу заголовка. Возвращает true, когда окно отпустили (пора сохранить).
        public static bool Drag(Rect titleBar, ref float x, ref float y, float w, float h)
        {
            if (!_dragging && TakeClick(titleBar))
            {
                _dragging = true;
                _dragOffset = new Vector2(M.x - x, M.y - y);
            }
            if (!_dragging) return false;
            if (_held)
            {
                x = Mathf.Clamp(M.x - _dragOffset.x, 0f, Math.Max(0f, SW - w));
                y = Mathf.Clamp(M.y - _dragOffset.y, 0f, Math.Max(0f, SH - Math.Min(h, 60f)));
                return false;
            }
            _dragging = false;
            return true;
        }

        // ---------- Надписи ----------

        private static readonly System.Text.RegularExpressions.Regex SizeTag = new("<size=(\\d+)>");

        private static string ScaleTags(string text) =>
            K == 1f || text == null || text.IndexOf("<size=", StringComparison.Ordinal) < 0 ? text
                : SizeTag.Replace(text, new System.Text.RegularExpressions.MatchEvaluator(ScaleTag));

        private static string ScaleTag(System.Text.RegularExpressions.Match m) =>
            "<size=" + Px(float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)) + ">";

        // Надпись: размер шрифта в точках окна, цвет и выравнивание как у игры. По умолчанию — сверху слева, как GUI.Label.
        public static void Label(Rect r, string text, float size = 14f, Color? color = null, TextAnchor align = TextAnchor.UpperLeft, bool wrap = false)
        {
            if (string.IsNullOrEmpty(text) || !Visible(r)) return;
            Theme.Apply();
            var style = Theme.Style(Px(size), align, Theme.HasCyrillic(text), wrap);
            var old = GUI.color;
            GUI.color = color ?? Theme.Text;
            GUI.Label(R(r), ScaleTags(text), style);
            GUI.color = old;
        }

        public static void Centered(Rect r, string text) => Label(r, text, 14f, null, TextAnchor.MiddleCenter);

        public static void Centered(Rect r, string text, float size, Color? color = null) => Label(r, text, size, color, TextAnchor.MiddleCenter);

        // Ширина надписи в точках окна.
        public static float TextWidth(string text, float size = 14f)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            try { return Theme.Style(Px(size), TextAnchor.UpperLeft, Theme.HasCyrillic(text)).CalcSize(new GUIContent(ScaleTags(text))).x / K; }
            catch { return System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "").Length * size * 0.52f; }
        }

        // ---------- Окна ----------

        // Окно в стиле игры с заголовком по центру (как «Сон» у меню отдыха). Возвращает y, с которого начинать содержимое.
        public static float Window(Rect r, string title, string corner = null, bool mark = true)
        {
            Theme.Apply();
            Theme.Window(R(r), K, mark);
            if (string.IsNullOrEmpty(title)) return r.y + 12f;
            float tw = Math.Max(200f, TextWidth(title, 22f) + 80f);
            Theme.Sprite(R(new Rect(r.center.x - tw / 2f, r.y - 4f, tw, 52f)), "backHeader", new Color(0.98f, 0.98f, 0.98f, 0.40f));
            Label(new Rect(r.x, r.y + 6f, r.width, 34f), title, 22f, Theme.White, TextAnchor.MiddleCenter);
            if (!string.IsNullOrEmpty(corner)) Label(new Rect(r.x, r.y + 10f, r.width - 18f, 20f), corner, 11f, Theme.Caption, TextAnchor.MiddleRight);
            return r.y + 44f;
        }

        // Окно старого вида (обмен, лодка): заголовок в полосе 24 точки сверху, содержимое с y + 26.
        public static void Panel(Rect r, string title, int layers = 3)
        {
            Theme.Apply();
            Theme.Window(R(r), K, false);
            if (!string.IsNullOrEmpty(title)) Label(new Rect(r.x, r.y + 2f, r.width, 22f), title, 15f, Theme.White, TextAnchor.MiddleCenter);
        }

        // Своя подложка (значок «Кооп», подсказка): окно без «пятна».
        public static void Backdrop(Rect r, int layers = 2)
        {
            Theme.Apply();
            if (Theme.HasSprites)
            {
                Theme.Sprite(R(r), "inv_statBack", new Color(0f, 0f, 0f, 0.62f), true, K * 0.5f);
                Theme.Sprite(R(r), "innerGlow2", new Color(0f, 0f, 0f, 0.24f), true, K);
            }
            else { Theme.Fill(R(r), Theme.PanelBg); Theme.Frame(R(r), Theme.Border); }
        }

        // Тёмная плашка под список или чат.
        public static void Slot(Rect r)
        {
            if (!Visible(r)) return;
            Theme.Slot(R(r), K);
        }

        // Разделитель.
        public static void Line(float x, float y, float w, float a = 0.5f)
        {
            if (!Visible(new Rect(x, y - 4f, w, 8f))) return;
            Theme.Line(R(new Rect(x, y - 4f, w, 8f)), a);
        }

        // Заголовок раздела: мелкие прописные серо-зелёные буквы и линия до края.
        public static void Section(float x, ref float cy, float w, string title)
        {
            cy += 4f;
            string t = title.ToUpperInvariant();
            Label(new Rect(x, cy, w, 18f), t, 12f, Theme.Caption, TextAnchor.MiddleLeft);
            float tw = TextWidth(t, 12f) + 10f;
            if (tw < w - 20f) Line(x + tw, cy + 10f, w - tw, 0.32f);
            cy += 22f;
        }

        // Строка «подпись … значение».
        public static void Row(float x, ref float cy, float w, string caption, string value, Color? valueColor = null, float gap = 4f)
        {
            float cwid = Math.Min(w * 0.42f, TextWidth(caption, 12f) + 12f);
            Label(new Rect(x, cy + 2f, cwid, 20f), caption, 12f, Theme.Caption, TextAnchor.MiddleLeft);
            float h = Math.Max(20f, TextHeight(value, w - cwid, 15f));
            Label(new Rect(x + cwid, cy, w - cwid, h + 2f), value, 15f, valueColor ?? Theme.White, h > 22f ? TextAnchor.UpperRight : TextAnchor.MiddleRight, true);
            cy += h + gap;
        }

        // ---------- Управление ----------

        public static bool Button(Rect r, string text) => Button(r, text, false);

        public static bool Button(Rect r, string text, bool active, float size = 14f, bool back = false, bool disabled = false)
        {
            if (!Visible(r)) return false;
            Theme.Apply();
            bool hover = !disabled && Hover(r);
            Theme.ButtonBack(R(r), hover, active, back);
            Label(r, text, size, Theme.ButtonText(hover, active, disabled), TextAnchor.MiddleCenter);
            return !disabled && TakeClick(r);
        }

        // Вкладка: выбранная подсвечена светлой полосой, как пункт главного меню игры.
        public static bool Tab(Rect r, bool selected, string text)
        {
            if (!Visible(r)) return false;
            Theme.Apply();
            bool hover = Hover(r);
            if (selected || hover)
            {
                var hr = new Rect(r.x + r.width * 0.06f, r.y + 2f, r.width * 0.88f, r.height - 4f);
                if (!Theme.Sprite(R(hr), "TLDHOME_MenuHighlight", new Color(0.98f, 0.98f, 0.98f, selected ? 0.32f : 0.14f)))
                    Theme.Fill(R(hr), selected ? Theme.Selected : Theme.ButtonHover);
            }
            Label(r, text, 16f, selected ? Theme.White : hover ? new Color(0.85f, 0.86f, 0.85f, 1f) : Theme.Dim, TextAnchor.MiddleCenter);
            return TakeClick(r);
        }

        // Галочка игры (checkboxA + checkbox_checkmarkA).
        public static bool Toggle(Rect r, bool value, string text)
        {
            if (!Visible(r)) return value;
            Theme.Apply();
            bool hover = Hover(r);
            text = (text ?? "").TrimStart();
            float bs = Math.Min(18f, r.height - 2f);
            var box = new Rect(r.x + 2f, r.y + (r.height - bs) / 2f, bs, bs);
            if (Theme.HasSprites)
            {
                Theme.Sprite(R(box), "checkboxA", new Color(0.98f, 0.98f, 0.98f, value ? 0.60f : hover ? 0.45f : 0.30f));
                if (value) Theme.Sprite(R(new Rect(box.x, box.y + 1f, bs, bs)), "checkbox_checkmarkA", new Color(1f, 1f, 1f, 0.95f));
            }
            else
            {
                Theme.Fill(R(box), Theme.FieldBg);
                Theme.Frame(R(box), hover ? Theme.Accent : Theme.Border);
                if (value) Theme.Fill(R(new Rect(box.x + 4f, box.y + 4f, bs - 8f, bs - 8f)), Theme.Accent);
            }
            Label(new Rect(r.x + bs + 9f, r.y, r.width - bs - 9f, r.height), Fit(text, r.width - bs - 9f), 14f,
                value ? new Color(0.90f, 0.90f, 0.88f, 1f) : hover ? new Color(0.80f, 0.81f, 0.80f, 1f) : Theme.Dim, TextAnchor.MiddleLeft);
            return TakeClick(r) ? !value : value;
        }

        // Выбор стрелками, как часы сна в меню игры: ‹ значение ›. Вернёт −1, 0 или +1.
        public static int Arrows(Rect r, string value, float size = 15f)
        {
            if (!Visible(r)) return 0;
            Theme.Apply();
            float a = Math.Min(r.height, 26f);
            var left = new Rect(r.x, r.y + (r.height - a) / 2f, a, a);
            var right = new Rect(r.xMax - a, left.y, a, a);
            int res = 0;
            for (int i = 0; i < 2; i++)
            {
                var ar = i == 0 ? left : right;
                bool hover = Hover(ar);
                var c = new Color(0.98f, 0.98f, 0.98f, hover ? 1f : 0.7f);
                if (!Theme.Sprite(R(ar), i == 0 ? "arrow_nav2" : "arrow_nav3", c))
                    Label(ar, i == 0 ? "‹" : "›", 18f, c, TextAnchor.MiddleCenter);
                if (TakeClick(ar)) res = i == 0 ? -1 : 1;
            }
            Label(new Rect(left.xMax, r.y, r.width - a * 2f, r.height), value, size, Theme.White, TextAnchor.MiddleCenter);
            return res;
        }

        // Карточка-кнопка (экран смерти): значок на красном пятне, заголовок и пояснение, как у пунктов игрового экрана смерти.
        public static bool Card(Rect r, string icon, string title, string body)
        {
            if (!Visible(r)) return false;
            Theme.Apply();
            bool hover = Hover(r);
            if (hover)
            {
                float g = 12f;
                Theme.Sprite(R(new Rect(r.x - g, r.y - g, r.width + g * 2f, r.height + g * 2f)), "clothing_slotSelected", new Color(0.98f, 0.98f, 0.98f, 0.18f), true, K * 0.5f);
                if (!Theme.Sprite(R(r), "inv_statBack", new Color(0f, 0f, 0f, 0.5f), true, K * 0.5f)) Theme.Fill(R(r), Theme.ButtonHover);
            }
            float a = hover ? 1f : 0.78f;
            var spot = new Rect(r.center.x - 50f, r.y + 6f, 100f, 100f);
            var ic = Theme.RedIcon;
            Theme.Sprite(R(spot), "bg_texture1", new Color(ic.r, ic.g, ic.b, 0.67f * a));
            var ir = new Rect(r.center.x - 32f, spot.center.y - 32f, 64f, 64f);
            if (!Theme.Sprite(R(ir), icon, hover ? new Color(0.95f, 0.90f, 0.88f, 1f) : new Color(ic.r, ic.g, ic.b, a)) && !Theme.HasSprites)
                Theme.Frame(R(ir), Theme.Border);
            Label(new Rect(r.x, spot.yMax + 2f, r.width, 22f), title, 16f, hover ? Theme.White : Theme.Red, TextAnchor.MiddleCenter);
            Label(new Rect(r.x + 8f, spot.yMax + 26f, r.width - 16f, r.yMax - spot.yMax - 28f), body, 12f, new Color(0.93f, 0.93f, 0.93f, 0.85f * a), TextAnchor.UpperCenter, true);
            return TakeClick(r);
        }

        // Цветной квадратик (выбор цвета ника). Выбранный — во всю яркость в белой рамке, остальные приглушены.
        public static bool Swatch(Rect r, Color c, bool selected)
        {
            if (!Visible(r)) return false;
            bool hover = Hover(r);
            if (selected || hover)
            {
                var g = new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f);
                var fc = new Color(0.98f, 0.98f, 0.98f, selected ? 0.95f : 0.45f);
                Theme.Frame(R(g), fc);
                if (selected) Theme.Frame(R(new Rect(g.x + 1f, g.y + 1f, g.width - 2f, g.height - 2f)), fc);
            }
            var col = selected ? new Color(c.r, c.g, c.b, 1f) : new Color(c.r * 0.8f, c.g * 0.8f, c.b * 0.8f, hover ? 0.9f : 0.6f);
            Theme.Fill(R(r), col);
            return TakeClick(r);
        }

        // Однострочное поле ввода. submitted = нажат Enter, пока поле в фокусе.
        public static string TextField(Rect r, string id, string value, int max) => TextField(r, id, value, max, out _);

        public static string TextField(Rect r, string id, string value, int max, out bool submitted)
        {
            submitted = false;
            value ??= "";
            if (TakeClick(r)) Focus = id;
            else if (_clickRaw && Focus == id && !r.Contains(M)) Focus = null;

            bool focused = Focus == id;
            if (focused) _focusDrawn = Time.frameCount;
            if (focused && _typed.Length > 0)
            {
                foreach (char c in _typed)
                {
                    if (c == '\b') { if (value.Length > 0) value = value.Substring(0, value.Length - 1); }
                    else if (c == '\n' || c == '\r') submitted = true;
                    else if (!char.IsControl(c) && value.Length < max) value += c;
                }
                _typed = "";      // OnGUI зовётся несколько раз за кадр, символы забираем один раз
            }

            if (Visible(r))
            {
                // Поле ввода как у переименования сохранения в игре: светлая заливка и ореол, когда в фокусе.
                if (Theme.HasSprites)
                {
                    Theme.Sprite(R(r), "inv_statBack", new Color(0.43f, 0.51f, 0.49f, focused ? 0.24f : 0.14f), true, K * 0.4f);
                    if (focused)
                    {
                        var g = new Rect(r.x - 10f, r.y - 8f, r.width + 20f, r.height + 16f);
                        Theme.Sprite(R(g), "clothing_slotSelected", new Color(0.43f, 0.51f, 0.49f, 0.45f), true, K * 0.3f);
                    }
                }
                else
                {
                    Theme.Fill(R(r), Theme.FieldBg);
                    Theme.Frame(R(r), focused ? Theme.Accent : Theme.Border);
                }
                bool caret = focused && (int)(Time.unscaledTime * 2f) % 2 == 0;
                string shown = Escape(value) + (caret ? "|" : "");
                Label(new Rect(r.x + 6f, r.y, r.width - 12f, r.height), shown, 14f, new Color(0.85f, 0.85f, 0.84f, 1f), TextAnchor.MiddleLeft);
            }
            return value;
        }

        // Escape нужен, чтобы ник вроде «<b>» не ломал разметку.
        private static string Escape(string s) => s.Replace("<", "<​");

        // Текст в несколько строк: рисует и сдвигает cy на его настоящую высоту.
        public static void Text(float x, ref float cy, float w, string text, float gap = 4f) => Text(x, ref cy, w, text, gap, 14f, null);

        public static void Text(float x, ref float cy, float w, string text, float gap, float size, Color? color, TextAnchor align = TextAnchor.UpperLeft)
        {
            float h = TextHeight(text, w, size);
            Label(new Rect(x, cy, w, h + 2f), text, size, color, align, true);
            cy += h + gap;
        }

        private static bool _calcBroken;

        // Высота текста в точках окна (с переносом по ширине w).
        public static float TextHeight(string text, float w, float size = 14f)
        {
            float line = size * 1.45f;
            if (string.IsNullOrEmpty(text)) return line;
            if (!_calcBroken)
            {
                try
                {
                    var style = Theme.Style(Px(size), TextAnchor.UpperLeft, Theme.HasCyrillic(text), true);
                    return Math.Max(line, style.CalcHeight(new GUIContent(ScaleTags(text)), w * K) / K);
                }
                catch { _calcBroken = true; }
            }
            string plain = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
            int lines = Math.Max(1, (int)Math.Ceiling(plain.Length * size * 0.52f / Math.Max(40f, w - 8f)));
            return lines * line;
        }

        // Одна строка: обрезаем с «…», чтобы не переносилась на соседнюю строку.
        public static string Fit(string text, float w, float size = 14f)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') >= 0) return text;
            if (TextWidth(text, size) <= w - 4f) return text;
            int lo = 1, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (TextWidth(text.Substring(0, mid) + "…", size) <= w - 4f) lo = mid; else hi = mid - 1;
            }
            return text.Substring(0, lo) + "…";
        }
    }
}
