namespace JellyfishByEMPEROR;

/// <summary>What the pack holds of ours: empty jars, jars with a live jellyfish.</summary>
public static class Pack
{
    public static IEnumerable<(GearItem g, Def d)> All()
    {
        var inv = GameManager.GetInventoryComponent();
        if (inv == null) yield break;
        foreach (var o in inv.m_Items)
        {
            GearItem? g = null;
            try { g = o?.m_GearItem; } catch { }
            if (g != null && Defs.Is(g, out var d)) yield return (g, d);
        }
    }

    public static GearItem? EmptyJar() => All().Where(x => x.d.Kind == Kind.Jar).Select(x => x.g).FirstOrDefault();
    public static int JarCount() => All().Count(x => x.d.Kind == Kind.Jar);
    public static List<(GearItem g, Def d)> LiveJars() => All().Where(x => x.d.Kind == Kind.Live && x.g.CurrentHP > .001f).ToList();

    public static void Remove(GearItem g) => GameManager.GetInventoryComponent().DestroyGear(g.gameObject);

    public static void GiveJar() =>
        GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory(Defs.Prefix + "Jar", 1, 1f, PlayerManager.InventoryInstantiateFlags.None);

    /// <summary>A jar with this jellyfish, its condition in % as it was.</summary>
    public static void GiveLive(Species s, Colour c, float hp)
    {
        var g = GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory(Defs.Prefix + Defs.LiveId(s, c), 1, Mathf.Clamp(hp / 100f, .01f, 1f), PlayerManager.InventoryInstantiateFlags.None);
        try { if (g != null) g.CurrentHP = Items.MaxHP(g) * Mathf.Clamp(hp / 100f, .01f, 1f); } catch (Exception e) { Safe.Log("Live condition", e); }
    }

    public static float Hp(GearItem g) => Mathf.Clamp(g.CurrentHP / Items.MaxHP(g) * 100f, 0f, 100f);
}

/// <summary>The aquarium's window: the jellyfish in it (each back into a jar, a dead one removed), the live jars in the pack to pour in,
/// and taking the aquarium itself when it is empty. A friend in the host's world asks the host and changes nothing until the answer.</summary>
public static class TankWindow
{
    static GearItem? item;
    static GearItem? taking;
    static float takingUntil;
    static int blockFrames;

    public static bool IsOpen => item != null;

    public static bool IsTaking(GearItem g)
    {
        try { return taking != null && Time.unscaledTime < takingUntil && taking.Pointer == g.Pointer; }
        catch { return false; }
    }

    public static void Open(GearItem g)
    {
        item = g;
        GameAudioManager.PlayGUIButtonClick();
    }

    public static void Close()
    {
        if (item != null) blockFrames = 3;
        item = null;
    }

    static TankState? State => item != null ? Tanks.Of(item) : null;

    public static void Update()
    {
        if (item == null)
        {
            if (blockFrames > 0) { blockFrames--; Ui.BlockGameInput(true); }
            return;
        }
        bool gone = true;
        try
        {
            gone = item.gameObject == null || item.m_InPlayerInventory || !item.gameObject.activeInHierarchy
                || Vector3.Distance(GameManager.GetPlayerTransform().position, item.transform.position) > 4f;
        }
        catch { }
        if (gone || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1) || GameManager.m_IsPaused)
        {
            Close();
            Ui.BlockGameInput(true);
            return;
        }
        Ui.BlockGameInput(true);
    }

    // ---- actions

    static string Name(Fish f) => f.Species is { } s && f.Colour is { } c ? Texts.Name(s, c) : f.Sp;

    static void Pour(GearItem jar, Def d)
    {
        if (item == null || d.Species == null || d.Colour == null) return;
        var sp = d.Species; var col = d.Colour;
        float hp = Pack.Hp(jar);
        var t = State;
        if (t != null && t.Fish.Count >= Tune.TankMax) { Catch.Say("MSG_FULL"); return; }
        if (Remote.On)
        {
            string op = Tanks.OpAt("pour", Tanks.SceneName, item.transform.position, sp.Id, col.Id, hp.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            Remote.Ask(op, a =>
            {
                if (a != "ok") { Catch.Say("MSG_OTHER"); return; }
                try { if (jar != null && jar.m_InPlayerInventory) Pack.Remove(jar); } catch { }
                Pack.GiveJar();
                Catch.Say("MSG_POURED", Texts.Name(sp, col));
            });
            return;
        }
        var p = item.transform.position;
        var ts = Tanks.Find(Tanks.SceneName, p);
        if (ts == null) { ts = new TankState { Scene = Tanks.SceneName, X = p.x, Y = p.y, Z = p.z }; Tanks.State.Tanks.Add(ts); }
        ts.Fish.Add(new Fish { Sp = sp.Id, Col = col.Id, Hp = hp });
        Pack.Remove(jar);
        Pack.GiveJar();
        Tanks.Changed(ts);
        Catch.Say("MSG_POURED", Texts.Name(sp, col));
        Safe.Info($"Poured into the aquarium: {sp.Id} {col.Id} {hp:0}%");
    }

    static void Take(TankState t, int i, bool remove)
    {
        var f = t.Fish[i];
        var sp = f.Species; var col = f.Colour;
        if (!remove && (sp == null || col == null)) return;
        if (!remove && Pack.EmptyJar() == null) return;
        if (Remote.On)
        {
            Remote.Ask(Tanks.Op(remove ? "remove" : "take", t, i.ToString(), f.Sp, f.Col), a =>
            {
                if (!a.StartsWith("ok", StringComparison.Ordinal)) { Catch.Say("MSG_OTHER"); return; }
                if (remove) return;
                float hp = f.Hp;
                var parts = a.Split('|');
                if (parts.Length > 1) float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out hp);
                var jar = Pack.EmptyJar();
                if (jar != null) Pack.Remove(jar);
                Pack.GiveLive(sp!, col!, hp);
                Catch.Say("MSG_JAR", Texts.Name(sp!, col!));
            });
            return;
        }
        t.Fish.RemoveAt(i);
        if (!remove)
        {
            var jar = Pack.EmptyJar();
            if (jar != null) Pack.Remove(jar);
            Pack.GiveLive(sp!, col!, f.Hp);
            Catch.Say("MSG_JAR", Texts.Name(sp!, col!));
        }
        Tanks.Changed(t);
        GameAudioManager.PlayGUIButtonClick();
    }

    static void Lift()
    {
        var g = item;
        if (g == null) return;
        if (Tanks.CountOf(g) > 0) { Catch.Say("MSG_BUSY"); return; }
        Close();
        taking = g;
        takingUntil = Time.unscaledTime + 1f;
        // AddItemToPlayerInventory, not ProcessPickupWithNoInspectScreen: that one also puts the item into the hands
        try
        {
            var pm = GameManager.GetPlayerManagerComponent();
            pm.AddItemToPlayerInventory(g, true, false);
            Place.OutOfHands(pm, g);
            GameAudioManager.PlayGUIButtonClick();
        }
        catch (Exception e) { Safe.Log("Take the aquarium", e); }
        try { if (g.m_InPlayerInventory) taking = null; } catch { }
    }

    // ---- drawing

    const float W = 520f, Pad = 22f, RowH = 46f;

    static bool Btn(Rect r, string text, bool enabled = true) => Ui.Button(r, text, enabled && !Remote.Waiting) && !Remote.Waiting;

    public static void Draw()
    {
        if (item == null) return;
        Ui.Begin();
        var t = State;
        var fish = t?.Fish ?? new List<Fish>();
        var jars = Pack.LiveJars();
        int emptyJars = Pack.JarCount();
        bool room = fish.Count < Tune.TankMax;
        int pourRows = room ? Math.Max(1, Math.Min(jars.Count, 5)) : 0;
        float h = 60f + 24f + Math.Max(1, fish.Count) * (RowH + 6f) + (room ? 34f + pourRows * (RowH + 6f) : 0f) + 60f;
        var r = new Rect((Ui.SW - W) / 2f, (Ui.SH - h) / 2f, W, h);
        Ui.Panel(r, Texts.Get("TANK_TITLE"));
        float y = r.y + 50f;
        Ui.Caption(new Rect(r.x + Pad, y, W - 2 * Pad, 20f), Texts.Get("TANK_COUNT", fish.Count, Tune.TankMax));
        y += 26f;
        if (fish.Count == 0)
        {
            Ui.Label(new Rect(r.x + Pad, y + 8f, W - 2 * Pad, 30f), Texts.Get("TANK_EMPTY"));
            y += RowH + 6f;
        }
        for (int i = 0; i < fish.Count && t != null; i++)
        {
            var f = fish[i];
            var row = new Rect(r.x + Pad, y, W - 2 * Pad, RowH);
            Ui.Row(row);
            if (f.Species is { } s && f.Colour is { } c) Ui.Icon(new Rect(row.x + 4f, row.y + 3f, 40f, 40f), Assets.Texture("icons/" + Defs.LiveId(s, c) + ".png", compress: false));
            Ui.Fit(new Rect(row.x + 52f, row.y + 2f, row.width - 200f, 24f), Name(f));
            Ui.Caption(new Rect(row.x + 52f, row.y + 24f, row.width - 200f, 18f), f.Dead ? Texts.Get("DEAD") : Texts.Get("HOVER_COND", Mathf.RoundToInt(f.Hp)));
            var b = new Rect(row.xMax - 138f, row.y + 8f, 130f, 30f);
            if (f.Dead) { if (Btn(b, Texts.Get("TANK_REMOVE"))) { Take(t, i, true); break; } }
            else if (Btn(b, emptyJars > 0 ? Texts.Get("TANK_TAKE") : Texts.Get("TANK_NOJAR"), emptyJars > 0)) { Take(t, i, false); break; }
            y += RowH + 6f;
        }
        if (room)
        {
            Ui.Caption(new Rect(r.x + Pad, y + 6f, W - 2 * Pad, 20f), Texts.Get("TANK_POUR"));
            y += 34f;
            if (jars.Count == 0)
            {
                Ui.Label(new Rect(r.x + Pad, y + 8f, W - 2 * Pad, 30f), Texts.Get("TANK_NONE"));
                y += RowH + 6f;
            }
            foreach (var (g, d) in jars.Take(5))
            {
                var row = new Rect(r.x + Pad, y, W - 2 * Pad, RowH);
                Ui.Row(row);
                Ui.Icon(new Rect(row.x + 4f, row.y + 3f, 40f, 40f), Assets.Texture(d.Icon, compress: false));
                if (d.Species != null && d.Colour != null) Ui.Fit(new Rect(row.x + 52f, row.y + 2f, row.width - 200f, 24f), Texts.Name(d.Species, d.Colour));
                Ui.Caption(new Rect(row.x + 52f, row.y + 24f, row.width - 200f, 18f), Texts.Get("HOVER_COND", Mathf.RoundToInt(Pack.Hp(g))));
                if (Btn(new Rect(row.xMax - 138f, row.y + 8f, 130f, 30f), Texts.Get("TANK_POUR_BTN"))) { Pour(g, d); break; }
                y += RowH + 6f;
            }
        }
        y = r.yMax - 48f;
        if (Remote.Waiting) Ui.Caption(new Rect(r.x + Pad, y - 22f, W - 2 * Pad, 18f), Texts.Get("TANK_WAIT"));
        float bw = (W - 2 * Pad - 10f) / 2f;
        if (Btn(new Rect(r.x + Pad, y, bw, 32f), Texts.Get("TANK_LIFT"), fish.Count == 0)) Lift();
        if (Ui.Button(new Rect(r.x + Pad + bw + 10f, y, bw, 32f), Texts.Get("TANK_CLOSE"))) Close();
    }
}
