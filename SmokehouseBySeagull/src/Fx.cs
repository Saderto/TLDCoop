using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SmokehouseBySeagull;

/// <summary>A burning barrel: smoke out of its pipe and a glow at the firebox door. The game's particle modules are stripped from this build,
/// so the smoke is camera-facing soft quads of our own (as in SmokesBySeagull).</summary>
public static class Fx
{
    const string LightName = "SH_FireLight";
    static readonly Vector3 PipeTop = new(-.12f, 1.31f, -.05f), Door = new(0f, .25f, .40f);

    class Live { public BarrelState State = null!; public GearItem? Item; public Light? Light; public float Acc; }
    static readonly List<Live> live = new();

    class Puff { public GameObject Go = null!; public Material Mat = null!; public Vector3 Vel; public float Age, Life, Size0, Grow, Alpha; }
    static readonly List<Puff> puffs = new();
    static Material? smoke;
    static Mesh? quad;

    static GearItem? FindItem(BarrelState b)
    {
        try
        {
            foreach (var c in Physics.OverlapSphere(b.Pos + Vector3.up * .5f, 1.2f))
            {
                var g = c != null ? c.GetComponentInParent<GearItem>() : null;
                if (g != null && Defs.IsBarrel(g) && !g.m_InPlayerInventory && Vector3.Distance(g.transform.position, b.Pos) < .9f) return g;
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
                    go.transform.localPosition = Door;
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
        }
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
        }
        if (quad == null)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad = q.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(q);
        }
    }

    static void Spawn(Vector3 pos, Vector3 vel, float life, float size, float grow, float alpha)
    {
        if (smoke == null || quad == null || puffs.Count > 160) return;
        var go = new GameObject("SH_Smoke");
        go.transform.position = pos;
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var r = go.AddComponent<MeshRenderer>();
        var m = new Material(smoke);
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        puffs.Add(new Puff { Go = go, Mat = m, Vel = vel, Life = life, Size0 = size, Grow = grow, Alpha = alpha });
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
                top = l.Item != null ? l.Item.transform.TransformPoint(PipeTop) : l.State.Pos + Vector3.up * 1.3f;
                if (l.Light != null) l.Light.intensity = 1f + .35f * Mathf.PerlinNoise(Time.time * 3.1f, l.State.X) + .15f * Mathf.PerlinNoise(Time.time * 11f, l.State.Z);
            }
            catch { l.Item = null; l.Light = null; continue; }
            if ((top - camT.position).sqrMagnitude > 70f * 70f) continue;
            EnsureSmoke();
            l.Acc += dt * 7f;
            while (l.Acc >= 1f)
            {
                l.Acc -= 1f;
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
            p.Vel = Vector3.Lerp(p.Vel, new Vector3(wind.x, .22f, wind.z), Math.Min(1f, .5f * dt));
            p.Go.transform.position += p.Vel * dt;
            float size = p.Size0 * (1f + p.Grow * t);
            p.Go.transform.localScale = new Vector3(size, size, size);
            p.Go.transform.rotation = Quaternion.LookRotation(p.Go.transform.position - camT.position, camT.up);
            float a = p.Alpha * (t < .12f ? t / .12f : 1f - (t - .12f) / .88f);
            var c = new Color(.72f, .72f, .74f, a);
            if (p.Mat.HasProperty("_Color")) p.Mat.SetColor("_Color", c);
            if (p.Mat.HasProperty("_TintColor")) p.Mat.SetColor("_TintColor", c * new Color(a, a, a, 1f));
        }
    }

    public static void Clear()
    {
        foreach (var p in puffs) { try { if (p.Go != null) UnityEngine.Object.Destroy(p.Go); } catch { } }
        puffs.Clear();
        live.Clear();
    }
}
