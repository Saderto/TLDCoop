namespace SmokehouseBySeagull;

/// <summary>The window's parts drawn with the game's own interface pictures (NGUI Base Atlas) and fonts, by the recipe of the coop mod's
/// windows (Theme.cs is its copy): dark sliced panel with a light halo, menu buttons with a glow, the game's checkbox.
/// Laid out in the game's 1280×720 points and stretched to the screen's height, as the game's menus are.
/// This build of the game has IMGUI's Button and Toggle stripped, so buttons are pictures and labels, with the mouse read through Input
/// in Update (Capture) and checked against each button's rectangle in OnGUI.</summary>
internal static class Ui
{
    static bool click;
    static Vector2 mouse;
    static bool blocking;
    static float prevDisable, ourDisable;

    public static float K { get; private set; } = 1f;
    public static float SW => Screen.width / K;
    public static float SH => Screen.height / K;

    public static readonly Color Dim = Theme.Dim, Good = Theme.Good, Fire = new(.95f, .62f, .30f, 1f), Accent = new(.86f, .86f, .83f, .95f);

    static Rect R(Rect v) => new(v.x * K, v.y * K, v.width * K, v.height * K);
    static int Px(float size) => Mathf.Max(8, Mathf.RoundToInt(size * K));

    /// <summary>At the start of OnUpdate.</summary>
    public static void Capture()
    {
        var mp = Input.mousePosition;
        mouse = new Vector2(mp.x, Screen.height - mp.y);
        click = Input.GetMouseButtonDown(0);
    }

    /// <summary>While our window is open the game gets no keys or mouse (the same switch the coop mod's window uses).</summary>
    public static void BlockGameInput(bool want)
    {
        try
        {
            if (want)
            {
                if (!blocking) { prevDisable = InputManager.m_InputDisableTime; blocking = true; }
                ourDisable = InputManager.m_InputDisableTime = Math.Max(InputManager.m_InputDisableTime, Time.time + .25f);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (blocking)
            {
                blocking = false;
                if (InputManager.m_InputDisableTime <= ourDisable) InputManager.m_InputDisableTime = Math.Max(prevDisable, Time.time + .1f);
            }
        }
        catch { blocking = false; }
    }

    /// <summary>In OnGUI (styles exist only there).</summary>
    public static void Begin()
    {
        K = Mathf.Max(1f, Screen.height / 720f);
        Theme.Apply();
    }

    public static void DropClick() => click = false;

    static bool Hover(Rect r) => R(r).Contains(mouse);

    static bool TakeClick(Rect r)
    {
        if (!click || !R(r).Contains(mouse)) return false;
        click = false;
        return true;
    }

    static void Text(Rect r, string text, float size, Color color, TextAnchor align)
    {
        if (string.IsNullOrEmpty(text)) return;
        var style = Theme.Style(Px(size), align, Theme.HasCyrillic(text));
        var old = GUI.color;
        GUI.color = color;
        GUI.Label(R(r), text, style);
        GUI.color = old;
    }

    /// <summary>The window: the game's dialog back with the title in the header strip. Content starts 44 points below the top.</summary>
    public static void Panel(Rect r, string title)
    {
        Theme.Window(R(r), K);
        float tw = Math.Max(240f, title.Length * 13f + 80f);
        Theme.Sprite(R(new Rect(r.center.x - tw / 2f, r.y - 4f, tw, 52f)), "backHeader", new Color(.98f, .98f, .98f, .40f));
        Text(new Rect(r.x, r.y + 6f, r.width, 34f), title, 22f, Theme.White, TextAnchor.MiddleCenter);
    }

    /// <summary>A small caption in the game's grey-green capitals with a line to the edge.</summary>
    public static void Section(Rect r, string title)
    {
        string t = title.ToUpperInvariant();
        Text(r, t, 12f, Theme.Caption, TextAnchor.MiddleLeft);
        float tw = t.Length * 7.6f + 12f;
        if (tw < r.width - 20f) Theme.Line(R(new Rect(r.x + tw, r.center.y - 4f, r.width - tw, 8f)), .32f);
    }

    public static void Row(Rect r) => Theme.Slot(R(r), K);

    public static void Bar(Rect r, float part, Color c)
    {
        Theme.Fill(R(r), new Color(0f, 0f, 0f, .5f));
        Theme.Fill(R(new Rect(r.x, r.y, r.width * Mathf.Clamp01(part), r.height)), c);
    }

    public static void Icon(Rect r, Texture? t)
    {
        if (t == null) return;
        try { GUI.DrawTexture(R(r), t); } catch { }
    }

    public static bool Button(Rect r, string text, bool enabled = true, bool selected = false)
    {
        bool hot = enabled && Hover(r);
        Theme.ButtonBack(R(r), hot, selected);
        Text(r, text, 14f, Theme.ButtonText(hot, selected, !enabled), TextAnchor.MiddleCenter);
        return enabled && TakeClick(r);
    }

    public static bool Toggle(Rect r, bool value, string text)
    {
        bool hover = Hover(r);
        float bs = Math.Min(18f, r.height - 2f);
        var box = new Rect(r.x + 2f, r.y + (r.height - bs) / 2f, bs, bs);
        if (Theme.HasSprites)
        {
            Theme.Sprite(R(box), "checkboxA", new Color(.98f, .98f, .98f, value ? .60f : hover ? .45f : .30f));
            if (value) Theme.Sprite(R(new Rect(box.x, box.y + 1f, bs, bs)), "checkbox_checkmarkA", new Color(1f, 1f, 1f, .95f));
        }
        else
        {
            Theme.Fill(R(box), Theme.FieldBg);
            Theme.Frame(R(box), hover ? Theme.Accent : Theme.Border);
            if (value) Theme.Fill(R(new Rect(box.x + 4f, box.y + 4f, bs - 8f, bs - 8f)), Theme.Accent);
        }
        Text(new Rect(r.x + bs + 9f, r.y, r.width - bs - 9f, r.height), text, 14f, value ? new Color(.90f, .90f, .88f, 1f) : hover ? new Color(.80f, .81f, .80f, 1f) : Theme.Dim, TextAnchor.MiddleLeft);
        return TakeClick(r) ? !value : value;
    }

    public static string Colored(string text, Color c) =>
        $"<color=#{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}>{text}</color>";

    public static void Label(Rect r, string text) => Text(r, text, 14f, Color.white, TextAnchor.MiddleLeft);

    public static void Caption(Rect r, string text) => Text(r, text.ToUpperInvariant(), 12f, Theme.Caption, TextAnchor.MiddleLeft);

    public static void Centered(Rect r, string text) => Text(r, text, 14f, Color.white, TextAnchor.MiddleCenter);
}
