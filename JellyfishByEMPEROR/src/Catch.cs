using System.Globalization;

namespace JellyfishByEMPEROR;

/// <summary>A hole just broken through the sea ice may bring a jellyfish up (the owner, 11-12: not on a hook, only when the hole is opened;
/// it stays 3 to 7 seconds, then sinks). It is seen in the hole; a small window offers the jar or letting it go (keys 1, 2).
/// In coop everyone in the place sees it, a player near the hole gets the window too, and the host decides who took it first.</summary>
[HarmonyPatch]
public static class Catch
{
    static string id = "";
    static Species? sp;
    static Colour? col;
    static GameObject? jelly;
    static Vector3 at;
    static float started, until, sinkingSince = -1f;
    static bool window;
    /// <summary>Holes whose jellyfish is gone (taken, let go or sunk): the host refuses a late claim on them.</summary>
    static readonly HashSet<string> ended = new();
    static readonly System.Random rnd = new();
    public static bool Showing => sp != null && window;

    static MelonPreferences_Entry<float> chance = null!;
    static MelonPreferences_Entry<bool> anyWater = null!;

    public static void Prefs(MelonPreferences_Category cat)
    {
        chance = cat.CreateEntry("Chance", 30f, "Шанс медузы в новой лунке на море, % / Chance of a jellyfish in a new sea hole, %");
        anyWater = cat.CreateEntry("AnyWater", false, "Медузы и в озёрах / Jellyfish in lakes too");
    }

    static IceFishingHole? hole;

    [HarmonyPrefix, HarmonyPatch(typeof(Panel_IceFishingHoleClear), nameof(Panel_IceFishingHoleClear.OnBreakIceComplete))]
    static void Before(Panel_IceFishingHoleClear __instance)
    {
        try { hole = __instance.m_IceFishingHole; } catch { hole = null; }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Panel_IceFishingHoleClear), nameof(Panel_IceFishingHoleClear.OnBreakIceComplete))]
    static void After(bool success, bool playerCancel)
    {
        try
        {
            if (!success || playerCancel || hole == null) return;
            string scene = GameManager.m_ActiveScene ?? "";
            bool coast = Defs.CoastScenes.Contains(scene);
            float roll = Defs.Rand(0f, 100f);
            Safe.Info($"Hole opened in {scene}: coast {coast}, roll {roll:0} of {chance.Value:0}");
            if (!coast && !anyWater.Value) return;
            if (roll >= chance.Value) return;
            Start(hole.transform.position);
        }
        catch (Exception e) { Safe.Log("Hole opened", e); }
    }

    static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
    static float P(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    /// <summary>A jellyfish rises in a hole this player just opened: shown here and told to the others.</summary>
    public static void Start(Vector3 holeAt)
    {
        bool night = false;
        try { night = GameManager.GetTimeOfDayComponent().IsNight(); } catch { }
        var (s, c) = Defs.Roll(night);
        float secs = Defs.Rand(Tune.ShowMin, Tune.ShowMax);
        string newId = $"{rnd.Next():x}{rnd.Next():x}";
        Show(newId, holeAt, s, c, secs, true);
        try { CoopApi.Send?.Invoke(string.Join("|", "hole", newId, GameManager.m_ActiveScene ?? "", F(holeAt.x), F(holeAt.y), F(holeAt.z), s.Id, c.Id, F(secs))); }
        catch (Exception e) { Safe.Log("Coop send hole", e); }
    }

    /// <summary>Another player's hole: "id|scene|x|y|z|sp|col|seconds". Seen if we are in the same place; the window too if we stand near it.</summary>
    public static void Remote(string rest)
    {
        var a = rest.Split('|');
        if (a.Length < 8) return;
        var s = Defs.SpeciesById(a[5]);
        var c = s != null ? Defs.ColourOf(s, a[6]) : null;
        if (s == null || c == null || ended.Contains(a[0])) return;
        if (!string.Equals(a[1], GameManager.m_ActiveScene ?? "", StringComparison.OrdinalIgnoreCase)) return;
        var p = new Vector3(P(a[2]), P(a[3]), P(a[4]));
        bool near = false;
        try { near = Vector3.Distance(GameManager.GetPlayerTransform().position, p) < 6f; } catch { }
        Show(a[0], p, s, c, P(a[7]), near);
    }

    /// <summary>Another player took or let go the jellyfish of that hole: "id|taken" or "id|let".</summary>
    public static void RemoteEnd(string rest)
    {
        var a = rest.Split('|');
        ended.Add(a[0]);
        if (a[0] != id || sp == null) return;
        if (window) Say(a.Length > 1 && a[1] == "taken" ? "MSG_GONE" : "MSG_SANK");
        Sink();
    }

    /// <summary>At the host: a friend wants the jellyfish of that hole. The first to ask gets it.</summary>
    public static string HostClaim(string holeId)
    {
        if (holeId.Length == 0 || ended.Contains(holeId)) return "";
        ended.Add(holeId);
        try { CoopApi.Send?.Invoke("holeend|" + holeId + "|taken"); } catch (Exception e) { Safe.Log("Coop send end", e); }
        if (holeId == id && sp != null)
        {
            if (window) Say("MSG_GONE");
            Sink();
        }
        return "ok";
    }

    static void Show(string newId, Vector3 holeAt, Species s, Colour c, float secs, bool withWindow)
    {
        id = newId; sp = s; col = c; window = withWindow;
        at = holeAt;
        started = Time.time;
        until = started + secs;
        sinkingSince = -1f;
        Safe.Info($"Jellyfish in the hole: {s.Id} {c.Id}, {secs:0.0} s{(withWindow ? "" : ", seen only")}");
        try
        {
            if (jelly != null) UnityEngine.Object.Destroy(jelly);
            jelly = null;
            var mesh = Assets.LoadMesh("meshes/Jelly_" + s.Id + ".fpmesh");
            var mat = Visual.Mat("Jelly_" + s.Id + "_" + c.Id, 3000);
            if (mesh != null && mat != null)
            {
                jelly = new GameObject("JF_HoleJelly");
                jelly.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = jelly.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var l = new GameObject("JF_Light").AddComponent<Light>();
                l.transform.SetParent(jelly.transform, false);
                l.type = LightType.Point; l.color = c.Glow; l.range = 1.2f; l.intensity = .7f; l.shadows = LightShadows.None;
                jelly.transform.position = at + Vector3.down * .3f;
                jelly.transform.localScale = Vector3.one * Tune.HoleScale;
            }
        }
        catch (Exception e) { Safe.Log("Hole jellyfish", e); }
    }

    public static void Say(string key, params object[] args)
    {
        try { HUDMessage.AddMessage(Texts.Get(key, args), false, true); }
        catch (Exception e) { Safe.Log("HUD message", e); }
    }

    static void TakeJar()
    {
        if (sp == null || col == null || Pack.EmptyJar() == null || JellyfishByEMPEROR.Remote.Waiting) return;
        var s = sp; var c = col; string hid = id;
        if (JellyfishByEMPEROR.Remote.On)
        {
            JellyfishByEMPEROR.Remote.Ask("claim|" + JellyfishByEMPEROR.Remote.NewKey() + "|" + hid, a =>
            {
                ended.Add(hid);
                if (a != "ok") { Say("MSG_GONE"); if (id == hid) Sink(); return; }
                Give(s, c);
                if (id == hid) End();
            });
            return;
        }
        if (ended.Contains(hid)) { Say("MSG_GONE"); Sink(); return; }
        ended.Add(hid);
        try { CoopApi.Send?.Invoke("holeend|" + hid + "|taken"); } catch (Exception e) { Safe.Log("Coop send end", e); }
        Give(s, c);
        End();
    }

    static void Give(Species s, Colour c)
    {
        var jar = Pack.EmptyJar();
        if (jar == null) return;
        Pack.Remove(jar);
        Pack.GiveLive(s, c, 100f);
        Say("MSG_JAR", Texts.Name(s, c));
        Safe.Info($"Caught in a jar: {s.Id} {c.Id}");
    }

    static void LetGo()
    {
        Safe.Info("Let go");
        ended.Add(id);
        try { CoopApi.Send?.Invoke("holeend|" + id + "|let"); } catch (Exception e) { Safe.Log("Coop send end", e); }
        Sink();
    }

    static void Sink()
    {
        if (sinkingSince < 0f) sinkingSince = Time.time;
        sp = null; col = null;
        if (window) Ui.BlockGameInput(false);
        window = false;
    }

    static void End()
    {
        if (jelly != null) UnityEngine.Object.Destroy(jelly);
        jelly = null; sp = null; col = null; sinkingSince = -1f;
        if (window) Ui.BlockGameInput(false);
        window = false;
        GameAudioManager.PlayGUIButtonClick();
    }

    public static void Update()
    {
        // the jellyfish's own motion: rising up into the hole, pulsing, sinking away
        if (jelly != null)
        {
            float t = Time.time;
            float s = Mathf.Sin(t * 2.6f);
            float y;
            if (sinkingSince >= 0f)
            {
                float k = (t - sinkingSince) / 1.8f;
                y = Mathf.Lerp(-.05f, -.6f, k);
                if (k >= 1f) { UnityEngine.Object.Destroy(jelly); jelly = null; sinkingSince = -1f; return; }
            }
            else y = Mathf.Lerp(-.3f, -.05f, Mathf.Clamp01((t - started) / .8f)) + Mathf.Sin(t * .9f) * .01f;
            jelly.transform.position = at + Vector3.up * y;
            jelly.transform.rotation = Quaternion.Euler(0f, t * 10f, 0f);
            float k2 = Tune.HoleScale;
            jelly.transform.localScale = new Vector3(k2 * (1f + .06f * s), k2 * (1f - .05f * s), k2 * (1f + .06f * s));
        }
        if (sp == null) return;
        if (Time.time >= until)
        {
            if (window) Say("MSG_SANK");
            Safe.Info("The jellyfish sank");
            ended.Add(id);
            Sink();
            return;
        }
        if (!window) return;
        Ui.BlockGameInput(true);
        if (Input.GetKeyDown(KeyCode.Alpha1)) TakeJar();
        else if (Input.GetKeyDown(KeyCode.Alpha2)) LetGo();
    }

    public static void Draw()
    {
        if (sp == null || col == null || !window) return;
        Ui.Begin();
        const float W = 420f, H = 214f;
        var r = new Rect((Ui.SW - W) / 2f, Ui.SH - H - 70f, W, H);
        Ui.Panel(r, Texts.Get("CATCH_TITLE"));
        float y = r.y + 50f;
        Ui.Icon(new Rect(r.x + 22f, y, 64f, 64f), Assets.Texture("icons/" + Defs.LiveId(sp, col) + ".png", compress: false));
        Ui.Label(new Rect(r.x + 98f, y + 6f, W - 120f, 26f), Texts.Name(sp, col));
        float left = Mathf.Clamp01((until - Time.time) / Math.Max(.1f, until - started));
        Ui.Bar(new Rect(r.x + 98f, y + 40f, W - 120f, 8f), left, new Color(.55f, .75f, .95f, .9f));
        y += 80f;
        int jars = Pack.JarCount();
        bool wait = JellyfishByEMPEROR.Remote.Waiting;
        float bw = (W - 52f) / 2f;
        if (Ui.Button(new Rect(r.x + 22f, y, bw, 30f), wait ? Texts.Get("TANK_WAIT") : jars > 0 ? Texts.Get("CATCH_JAR", jars) : Texts.Get("CATCH_NOJAR"), jars > 0 && !wait)) TakeJar();
        if (sp != null && Ui.Button(new Rect(r.x + 30f + bw, y, bw, 30f), Texts.Get("CATCH_LET"), !wait)) LetGo();
        Ui.Caption(new Rect(r.x + 22f, r.yMax - 30f, W - 44f, 20f), Texts.Get("CATCH_KEYS"));
    }
}
