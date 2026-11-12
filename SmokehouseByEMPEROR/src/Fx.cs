using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SmokehouseByEMPEROR;

/// <summary>A burning barrel: smoke out of its pipe and a glow at the firebox door. The game's particle modules are stripped from this build,
/// so the smoke is camera-facing soft quads of our own (as in SmokesByEMPEROR).</summary>
public static class Fx
{
    const string LightName = "SH_FireLight";
    static readonly Vector3 PipeTop = new(-.12f, 1.31f, -.05f), Door = new(0f, .25f, .40f), KettleTop = new(0f, .74f, 0f), KettleFire = new(0f, .14f, 0f);

    class Live { public BarrelState State = null!; public GearItem? Item; public Light? Light; public float Acc, FireAcc; public GameObject? Boil; public bool BoilTried; public uint BoilId; public HeatSource? Heat; public AudioSource? Own; }
    const string BoilEvent = "Play_BoilingLight";      // the game's own pot of boiling water (11-12 owner)
    static readonly List<Live> live = new();

    class Puff { public GameObject Go = null!; public Material Mat = null!; public Vector3 Vel; public float Age, Life, Size0, Grow, Alpha, White; public bool Fire; }
    static readonly List<Puff> puffs = new();
    static Material? smoke, flame;
    static Mesh? quad;

    static GearItem? FindItem(BarrelState b)
    {
        try
        {
            foreach (var c in Physics.OverlapSphere(b.Pos + Vector3.up * .5f, 1.2f))
            {
                var g = c != null ? c.GetComponentInParent<GearItem>() : null;
                if (g != null && Defs.IsStation(g, out var d) && Defs.KindOf(d) == (b.Kind ?? "") && !g.m_InPlayerInventory && Vector3.Distance(g.transform.position, b.Pos) < .9f) return g;
            }
        }
        catch (Exception e) { Safe.Log("Find barrel", e); }
        return null;
    }

    /// <summary>Twice a second: which barrels of this scene burn.</summary>
    public static void Tick(string scene)
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var l = live[i];
            if (l.State.Lit && l.State.Scene == scene && Barrels.State.Barrels.Contains(l.State)) continue;
            try { if (l.Light != null) UnityEngine.Object.Destroy(l.Light.gameObject); } catch { }
            Hush(l);
            live.RemoveAt(i);
        }
        foreach (var b in Barrels.State.Barrels)
        {
            if (!b.Lit || b.Scene != scene) continue;
            var l = live.FirstOrDefault(x => x.State == b);
            if (l == null) live.Add(l = new Live { State = b });
            if (l.Item == null) l.Item = FindItem(b);
            if (l.Item != null && l.Light == null)
            {
                try
                {
                    var old = l.Item.transform.Find(LightName);
                    var go = old != null ? old.gameObject : new GameObject(LightName);
                    go.transform.SetParent(l.Item.transform, false);
                    go.transform.localPosition = b.Kettle ? KettleFire : Door;
                    var li = go.GetComponent<Light>() ?? go.AddComponent<Light>();
                    li.type = LightType.Point;
                    li.color = new Color(1f, .42f, .12f);
                    li.range = 2.2f;
                    li.intensity = 1.1f;
                    li.shadows = LightShadows.None;
                    l.Light = li;
                }
                catch (Exception e) { Safe.Log("Fire light", e); }
            }
            // the kettle is heard boiling while something is in it
            if (b.Kettle && l.Item != null)
            {
                bool full = b.Slots.Any(s => s != null);
                if (!full) Hush(l);
                else if (l.Boil == null && !l.BoilTried)
                {
                    l.BoilTried = true;
                    try
                    {
                        var go = new GameObject("SH_Boil");
                        go.transform.SetParent(l.Item.transform, false);
                        go.transform.localPosition = new Vector3(0f, .6f, 0f);
                        if (Boil.Available)
                        {
                            l.Own = Boil.Start(go);
                            l.Boil = go;
                            continue;
                        }
                        // 0.4.11 played it with PlaySound: an id came back and nothing was heard (11-12 owner). A sound of a place goes through Play3DSound, as the game's pot does
                        go.transform.position = l.Item.transform.position + Vector3.up * .6f;
                        // 0.4.12: Play3DSound gave an id too and still nothing was heard (11-12 owner). The game's cooking sounds are switch containers:
                        // with no cooking status and surface set on the object they play nothing
                        try
                        {
                            var r1 = AkSoundEngine.SetSwitch("CookingStatusSwitch", "CookingStatus_InProgress", go);
                            var r2 = AkSoundEngine.SetSwitch("CookingSurface", "CookingSurface_Stove", go);
                            MelonLogger.Msg($"Kettle boiling switches: {r1}, {r2}");
                        }
                        catch (Exception e) { Safe.Log("Boil switches", e); }
                        // 0.4.13: switches set, an id came back, silence still (11-12 owner). The bank mixes three loops (low, medium, high boil) by the game
                        // parameter CookingState, which is 0 on a new object: nothing to hear. A full boil is asked for, before and after the start
                        try { MelonLogger.Msg($"Kettle boiling parameter: {AkSoundEngine.SetRTPCValue("CookingState", Tune.BoilLevel, go)}"); } catch (Exception e) { Safe.Log("Boil parameter", e); }
                        uint id = GameAudioManager.Play3DSound(BoilEvent, go);
                        try { AkSoundEngine.SetRTPCValue("CookingState", Tune.BoilLevel, go); } catch { }
                        l.BoilId = id;
                        MelonLogger.Msg($"Kettle boiling sound {BoilEvent}: 3D, playing id {id} at {go.transform.position}");
                        l.Boil = go;
                    }
                    catch (Exception e) { Safe.Log("Boil sound", e); }
                }
            }
        }
    }

    /// <summary>11-12 owner: a burning kettle warms the air near it by 6 degrees, a smokehouse by 3. Full within 2.5 m, none past 6 m; the warmest one counts.</summary>
    public static float Warmth(Vector3 at)
    {
        float best = 0f;
        foreach (var l in live)
        {
            if (!l.State.Lit) continue;
            Vector3 p;
            try { p = l.Item != null ? l.Item.transform.position : l.State.Pos; } catch { continue; }
            float d = Vector3.Distance(at, p), warm = l.State.Kettle ? Tune.KettleWarm : Tune.BarrelWarm;
            best = Mathf.Max(best, warm * Mathf.Clamp01(1f - (d - 2.5f) / 3.5f));
        }
        return best;
    }
    static readonly HashSet<string> heatLogged = new();
    static float nextHeatLog;
    static int boilStep;
    static readonly float[] BoilLevels = { 100f, 0f, .25f, .5f, 1f, 2f, 3f, 5f, 10f, 50f };

    /// <summary>Test key: the next strength of the boiling sound on every burning kettle, its number on the screen.</summary>
    public static void NextBoilLevel()
    {
        boilStep = (boilStep + 1) % BoilLevels.Length;
        float v = BoilLevels[boilStep]; int n = 0;
        foreach (var l in live)
            if (l.Boil != null) { try { AkSoundEngine.SetRTPCValue("CookingState", v, l.Boil); n++; } catch (Exception e) { Safe.Log("Boil level", e); } }
        string msg = $"CookingState = {v} ({n})";
        MelonLogger.Msg("Boil test: " + msg);
        try { HUDMessage.AddMessage(msg, 2.5f, true, true); } catch { }
    }

    static void Hush(Live l)
    {
        if (l.Own != null && l.Boil != null) { Boil.FadeOut(l.Own, l.Boil); l.Own = null; l.Boil = null; l.BoilTried = false; return; }
        l.Own = null;
        if (l.Boil == null) { l.BoilTried = false; return; }
        try { if (l.BoilId != 0) { uint id = l.BoilId; GameAudioManager.StopPlayingID(ref id, 300); } } catch { }
        l.BoilId = 0;
        try { GameAudioManager.StopAllSoundsFromGameObject(l.Boil); } catch (Exception e) { Safe.Log("Boil sound stop", e); }
        try { UnityEngine.Object.Destroy(l.Boil); } catch { }
        l.Boil = null; l.BoilTried = false;
    }

    static void EnsureSmoke()
    {
        if (smoke == null)
        {
            var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Particles/Standard Unlit");
            if (sh != null)
            {
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                        a = a * a * (3f - 2f * a);
                        a *= .7f + .3f * Mathf.PerlinNoise(x * .15f, y * .15f);
                        px[y * n + x] = new Color(1f, 1f, 1f, a);
                    }
                tex.SetPixels(new Il2CppStructArray<Color>(px));
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;
                smoke = new Material(sh) { mainTexture = tex };
                smoke.hideFlags = HideFlags.HideAndDontSave;
            }
            Safe.Info("Smoke material: " + (smoke != null ? smoke.shader.name : "none"));
            if (sh != null)
            {
                // a tongue of flame: wide and bright below, a thin wavering tip above
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float v = (y + .5f) / n, dx = (x + .5f) / n * 2f - 1f;
                        float w = Mathf.Lerp(.62f, .04f, Mathf.Pow(v, .8f)) * Mathf.Clamp01(v * 9f + .25f);
                        dx += .12f * Mathf.Sin(v * 7f) * v;
                        float a = Mathf.Clamp01(1f - Mathf.Abs(dx) / Mathf.Max(.02f, w));
                        a = a * a * (3f - 2f * a) * Mathf.Clamp01((1f - v) * 5f);
                        px[y * n + x] = new Color(1f, 1f, 1f, a);
                    }
                tex.SetPixels(new Il2CppStructArray<Color>(px));
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;
                flame = new Material(sh) { mainTexture = tex };
                flame.hideFlags = HideFlags.HideAndDontSave;
            }
        }
        if (quad == null)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad = q.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(q);
        }
    }

    static void Spawn(Vector3 pos, Vector3 vel, float life, float size, float grow, float alpha, float white = .72f, bool fire = false)
    {
        if (smoke == null || quad == null || puffs.Count > (fire ? 300 : 200)) return;
        var go = new GameObject("SH_Smoke");
        go.transform.position = pos;
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var r = go.AddComponent<MeshRenderer>();
        var m = new Material(fire && flame != null ? flame : smoke);
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        puffs.Add(new Puff { Go = go, Mat = m, Vel = vel, Life = life, Size0 = size, Grow = grow, Alpha = alpha, White = white, Fire = fire });
    }

    /// <summary>Every frame.</summary>
    public static void Frame()
    {
        if (live.Count == 0 && puffs.Count == 0) return;
        float dt = Time.deltaTime;
        var cam = GameManager.GetMainCamera();
        var camT = cam != null ? cam.transform : null;
        if (camT == null || dt <= 0f) return;
        Vector3 wind = Vector3.zero;
        try
        {
            var w = GameManager.GetWindComponent();
            if (w != null) wind = w.GetWindDirection() * (w.GetSpeedMPH() * .03f);
            wind.y = 0f;
            if (wind.magnitude > .9f) wind = wind.normalized * .9f;
        }
        catch { }
        foreach (var l in live)
        {
            Vector3 top;
            try
            {
                top = l.Item != null ? l.Item.transform.TransformPoint(l.State.Kettle ? KettleTop : PipeTop) : l.State.Pos + Vector3.up * (l.State.Kettle ? .74f : 1.3f);
                if (l.Light != null) l.Light.intensity = 1f + .35f * Mathf.PerlinNoise(Time.time * 3.1f, l.State.X) + .15f * Mathf.PerlinNoise(Time.time * 11f, l.State.Z);
            }
            catch { l.Item = null; l.Light = null; continue; }
            if ((top - camT.position).sqrMagnitude > 70f * 70f) continue;
            if (l.Own != null) { try { Boil.Volume(l.Own, camT.position); } catch { } }
            EnsureSmoke();
            l.Acc += dt * (l.State.Kettle ? 4f : 7f);
            while (l.Acc >= 1f)
            {
                l.Acc -= 1f;
                if (l.State.Kettle)
                {
                    // 11-12 owner: a real fire under the kettle. Tongues of flame between the stones, licking its belly.
                    if (l.Item != null)
                    {
                        l.FireAcc += 5f;
                        while (l.FireAcc >= 1f)
                        {
                            l.FireAcc -= 1f;
                            var f = UnityEngine.Random.insideUnitCircle * .2f;
                            var at = l.Item.transform.TransformPoint(new Vector3(f.x, .05f + UnityEngine.Random.value * .05f, f.y));
                            Spawn(at, new Vector3(f.x * .25f, .5f + UnityEngine.Random.value * .35f, f.y * .25f) + wind * .15f, .45f + UnityEngine.Random.value * .3f,
                                .17f + UnityEngine.Random.value * .09f, -.45f, .9f, 1f, true);
                        }
                    }
                    // steam off the water: wide, pale, soon gone
                    // 11-12 owner: the steam went through the shut kettle. It comes out from under the lid's edge, all round, and rises past it
                    float sa = UnityEngine.Random.value * Mathf.PI * 2f;
                    var edge = new Vector3(Mathf.Cos(sa), 0f, Mathf.Sin(sa));
                    var from = l.Item != null ? l.Item.transform.TransformPoint(edge * .385f + Vector3.up * .70f) : top + edge * .385f;
                    Spawn(from, edge * (.10f + UnityEngine.Random.value * .08f) + Vector3.up * (.30f + UnityEngine.Random.value * .2f) + wind * .2f, 2.4f, .09f, 6f, .10f, .92f);
                    continue;
                }
                var jitter = UnityEngine.Random.insideUnitSphere * .05f;
                Spawn(top + jitter, new Vector3(jitter.x * 2f, .55f + UnityEngine.Random.value * .25f, jitter.z * 2f) + wind * .3f, 5.5f, .16f, 7f, l.State.Cold ? .16f : .24f);
            }
        }
        for (int i = puffs.Count - 1; i >= 0; i--)
        {
            var p = puffs[i];
            if (p.Go == null) { puffs.RemoveAt(i); continue; }
            p.Age += dt;
            float t = p.Age / p.Life;
            if (t >= 1f)
            {
                UnityEngine.Object.Destroy(p.Go);
                UnityEngine.Object.Destroy(p.Mat);
                puffs.RemoveAt(i);
                continue;
            }
            if (p.Fire)
            {
                p.Go.transform.position += p.Vel * dt;
                float fs = p.Size0 * (1f + p.Grow * t);
                p.Go.transform.localScale = new Vector3(fs * (.85f + .25f * Mathf.Sin(p.Age * 31f + p.Size0 * 90f)), fs * 1.9f, fs);
                // upright, turned to the camera round the vertical only
                var to = p.Go.transform.position - camT.position; to.y = 0f;
                if (to.sqrMagnitude > 1e-4f) p.Go.transform.rotation = Quaternion.LookRotation(to, Vector3.up);
                float fa = p.Alpha * (t < .15f ? t / .15f : 1f - (t - .15f) / .85f);
                var hot = new Color(1f, .9f, .45f); var mid = new Color(1f, .5f, .1f); var end = new Color(.75f, .14f, .03f);
                var fc = t < .4f ? Color.Lerp(hot, mid, t / .4f) : Color.Lerp(mid, end, (t - .4f) / .6f);
                fc.a = fa;
                if (p.Mat.HasProperty("_Color")) p.Mat.SetColor("_Color", fc);
                if (p.Mat.HasProperty("_TintColor")) p.Mat.SetColor("_TintColor", fc);
                continue;
            }
            p.Vel = Vector3.Lerp(p.Vel, new Vector3(wind.x, .22f, wind.z), Math.Min(1f, .5f * dt));
            p.Go.transform.position += p.Vel * dt;
            float size = p.Size0 * (1f + p.Grow * t);
            p.Go.transform.localScale = new Vector3(size, size, size);
            p.Go.transform.rotation = Quaternion.LookRotation(p.Go.transform.position - camT.position, camT.up);
            float a = p.Alpha * (t < .12f ? t / .12f : 1f - (t - .12f) / .88f);
            var c = new Color(p.White, p.White, p.White + .02f, a);
            if (p.Mat.HasProperty("_Color")) p.Mat.SetColor("_Color", c);
            if (p.Mat.HasProperty("_TintColor")) p.Mat.SetColor("_TintColor", c * new Color(a, a, a, 1f));
        }
    }

    public static void Clear()
    {
        foreach (var p in puffs) { try { if (p.Go != null) UnityEngine.Object.Destroy(p.Go); } catch { } }
        puffs.Clear();
        foreach (var l in live) Hush(l);
        live.Clear();
    }
}
