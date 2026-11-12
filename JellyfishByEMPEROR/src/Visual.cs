namespace JellyfishByEMPEROR;

/// <summary>The see-through parts: glass, water and the jellyfish itself, drawn with the game's «Sprites/Default» (alpha-blended, both sides,
/// unlit: a jellyfish glows on its own). Glass and water take their look from their textures (clear glass with glints and denser edges,
/// sea water with its surface) and are dimmed in the dark so they don't shine. Every live jellyfish swims: the bell pulses, it rises and sinks
/// a little and turns. In an aquarium the jellyfish poured into it drift through the whole tank; a dead one lies on the sand.</summary>
public static class Visual
{
    public class Live
    {
        public GearItem G = null!;
        public Def Def = null!;
        public Transform? Jelly;
        public Renderer? Water;
        public Light? Light;
        public float Phase;
    }

    class TankVis
    {
        public GearItem G = null!;
        public string Sig = "";
        public readonly List<Transform> Jellies = new();
        public Light? Light;
    }

    public static readonly List<Live> Lives = new();
    static readonly List<TankVis> tanks = new();
    static readonly Dictionary<string, Material> mats = new();
    static Material? glass, water, tankGlass, tankWater;
    static float light = 1f, nextLook;
    static readonly System.Random rnd = new();

    static Shader? Sh() => Shader.Find("Sprites/Default") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");

    public static Material? Mat(string texture, int queue)
    {
        if (mats.TryGetValue(texture, out var m) && m != null) return m;
        var sh = Sh();
        if (sh == null) { Safe.Log("Shader", new Exception("Sprites/Default not found")); return null; }
        var t = Assets.Texture("textures/" + texture + ".png");
        m = new Material(sh) { mainTexture = t, renderQueue = queue, name = "JF_" + texture, hideFlags = HideFlags.HideAndDontSave };
        return mats[texture] = m;
    }

    /// <summary>Glass or water: its own material (not in the jellyfish list: it is dimmed by another rule).</summary>
    static Material? See(string texture, int queue)
    {
        var sh = Sh();
        if (sh == null) return null;
        return new Material(sh) { mainTexture = Assets.Texture("textures/" + texture + ".png", compress: false), renderQueue = queue, name = "JF_" + texture, hideFlags = HideFlags.HideAndDontSave };
    }

    static MeshRenderer? Part(Transform parent, string name, string mesh, Material? mat, int layer)
    {
        var m = Assets.LoadMesh("meshes/" + mesh + ".fpmesh");
        if (m == null || mat == null) return null;
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    public static void Attach(GearItem g, Def def)
    {
        try
        {
            var t = g.transform; int layer = g.gameObject.layer;
            for (int i = t.childCount - 1; i >= 0; i--)
                if (t.GetChild(i).name.StartsWith("JF_", StringComparison.Ordinal)) UnityEngine.Object.DestroyImmediate(t.GetChild(i).gameObject);
            if (def.Kind == Kind.Aquarium)
            {
                tankGlass ??= See("AquaGlass", 3003); tankWater ??= See("AquaWater", 3002);
                Part(t, "JF_Glass", "AquaGlass", tankGlass, layer);
                Part(t, "JF_Water", "AquaWater", tankWater, layer);
                tanks.RemoveAll(x => x.G == null || x.G.Pointer == g.Pointer);
                tanks.Add(new TankVis { G = g });
                return;
            }
            glass ??= See("JarGlass", 3003); water ??= See("JarWater", 3002);
            Part(t, "JF_Glass", "JarGlass", glass, layer);
            // an empty jar is just glass; the water comes with the jellyfish
            if (def.Kind != Kind.Live || def.Species == null || def.Colour == null) return;
            var wr = Part(t, "JF_Water", "JarWater", water, layer);
            var jr = Part(t, "JF_Jelly", "Jelly_" + def.Species.Id, Mat("Jelly_" + def.Species.Id + "_" + def.Colour.Id, 3000), layer);
            if (jr == null) return;
            jr.transform.localPosition = new Vector3(0f, def.Species.Y, 0f);
            jr.transform.localScale = Vector3.one * def.Species.JarK;
            var l = Glow(jr.transform, def.Colour.Glow, .8f);
            Lives.RemoveAll(x => x.G == null || x.G.Pointer == g.Pointer);
            Lives.Add(new Live { G = g, Def = def, Jelly = jr.transform, Water = wr, Light = l, Phase = (float)rnd.NextDouble() * 20f });
        }
        catch (Exception e) { Safe.Log("Visual " + def.Id, e); }
    }

    static Light Glow(Transform parent, Color c, float range)
    {
        var lgo = new GameObject("JF_Light");
        lgo.transform.SetParent(parent, false);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = 0f; l.shadows = LightShadows.None; l.enabled = false;
        return l;
    }

    public static bool Dead(GearItem g) => g.CurrentHP <= 0.001f;

    static bool Standing(GearItem? g)
    {
        try { return g != null && !g.m_InPlayerInventory && g.gameObject.activeInHierarchy; } catch { return false; }
    }

    /// <summary>How bright the place is: glass and water are lit by the world, our shader is not. The nearest few jellyfish light their surroundings.</summary>
    static void Look()
    {
        nextLook = Time.unscaledTime + 1f;
        bool indoors = false, night = false;
        try { indoors = GameManager.GetWeatherComponent().IsIndoorEnvironment(); } catch { }
        try { night = GameManager.GetTimeOfDayComponent().IsNight(); } catch { }
        light = indoors ? (night ? .3f : .55f) : night ? .25f : 1f;
        foreach (var m in new[] { glass, water, tankGlass, tankWater }) if (m != null) m.color = new Color(light, light, light, 1f);
        float j = Math.Max(.75f, light);
        foreach (var m in mats.Values) if (m != null) m.color = new Color(j, j, j, 1f);
        var me = GameManager.GetPlayerTransform()?.position ?? Vector3.zero;
        float glow = indoors ? .55f : night ? .8f : 0f;
        var lit = new List<(float d, Light l, float k)>();
        foreach (var l in Lives)
            if (l.Light != null && Standing(l.G) && !Dead(l.G)) lit.Add(((l.G.transform.position - me).sqrMagnitude, l.Light, 1f));
        foreach (var t in tanks)
            if (t.Light != null && Standing(t.G) && (Tanks.Of(t.G)?.Fish.Any(f => !f.Dead) ?? false)) lit.Add(((t.G.transform.position - me).sqrMagnitude, t.Light, 1.6f));
        var on = new HashSet<Light>(lit.OrderBy(x => x.d).Take(6).Select(x => x.l));
        foreach (var x in lit) x.l.intensity = glow * x.k;
        foreach (var l in Lives) if (l.Light != null) l.Light.enabled = glow > 0f && on.Contains(l.Light);
        foreach (var t in tanks) if (t.Light != null) t.Light.enabled = glow > 0f && on.Contains(t.Light);
    }

    public static void Frame()
    {
        Lives.RemoveAll(x => x.G == null || x.Jelly == null);
        tanks.RemoveAll(x => x.G == null);
        if (Lives.Count == 0 && tanks.Count == 0) return;
        if (Time.unscaledTime >= nextLook) Look();
        float time = Time.time;
        foreach (var l in Lives)
        {
            if (!Standing(l.G)) continue;
            var j = l.Jelly!;
            var sp = l.Def.Species!;
            float k = sp.JarK;
            // the owner (11-12): a dead jellyfish is simply gone, the jar stands empty
            bool dead = Dead(l.G);
            if (j.gameObject.activeSelf == dead) j.gameObject.SetActive(!dead);
            if (l.Water != null && l.Water.enabled == dead) l.Water.enabled = !dead;
            if (dead) continue;
            float t = time * .9f + l.Phase;
            float s = Mathf.Sin(t * 2.9f);
            j.localPosition = new Vector3(0f, sp.Y + Mathf.Sin(t * .7f) * .004f, 0f);
            j.localRotation = Quaternion.Euler(0f, t * 7f, 0f);
            j.localScale = new Vector3(k * (1f + .06f * s), k * (1f - .05f * s), k * (1f + .06f * s));
        }
        foreach (var v in tanks)
        {
            if (!Standing(v.G)) continue;
            var st = Tanks.Of(v.G);
            var fish = st?.Fish ?? new List<Fish>();
            string sig = string.Join(",", fish.Select(f => f.Sp + "_" + f.Col));
            if (sig != v.Sig) Rebuild(v, fish, sig);
            for (int i = 0; i < v.Jellies.Count && i < fish.Count; i++) Swim(v.Jellies[i], fish[i], i, time);
        }
    }

    const float Floor = .051f, Surface = .387f, T = 2.2f;

    static void Rebuild(TankVis v, List<Fish> fish, string sig)
    {
        v.Sig = sig;
        foreach (var j in v.Jellies) if (j != null) UnityEngine.Object.Destroy(j.gameObject);
        v.Jellies.Clear();
        int layer = v.G.gameObject.layer;
        foreach (var f in fish)
        {
            if (f.Species is not { } s || f.Colour is not { } c) continue;
            var r = Part(v.G.transform, "JF_Fish", "Jelly_" + s.Id, Mat("Jelly_" + s.Id + "_" + c.Id, 3000), layer);
            if (r != null) v.Jellies.Add(r.transform);
        }
        var first = fish.FirstOrDefault(f => f.Colour != null);
        if (v.Light == null && first != null)
        {
            var lgo = new GameObject("JF_Light") { layer = layer };
            lgo.transform.SetParent(v.G.transform, false);
            lgo.transform.localPosition = new Vector3(0f, .22f, 0f);
            v.Light = lgo.AddComponent<Light>();
            v.Light.type = LightType.Point; v.Light.range = 1.6f; v.Light.shadows = LightShadows.None; v.Light.intensity = 0f; v.Light.enabled = false;
        }
        if (v.Light != null && first?.Colour != null) v.Light.color = first.Colour.Glow;
        nextLook = 0f;
    }

    /// <summary>Through the whole tank, each on its own slow path; the size and height worked out from how the species fills a jar.</summary>
    static void Swim(Transform j, Fish f, int i, float time)
    {
        var s = f.Species;
        if (s == null || j == null) return;
        float k = s.JarK * T;
        float ph = i * 2.1f + (s.Id.Length * .7f);
        if (f.Dead)
        {
            j.localPosition = new Vector3(-.16f + .16f * i, Floor + .006f, (i % 2 == 0 ? .04f : -.05f));
            j.localRotation = Quaternion.Euler(0f, ph * 40f, 0f);
            j.localScale = new Vector3(k * 1.1f, k * .3f, k * 1.1f);
            return;
        }
        float lo = Floor + .01f + (s.Y - .008f) * T, hi = Surface - .01f - (.126f - s.Y) * T;
        if (hi < lo) lo = hi = (lo + hi) / 2f;
        float t = time * .9f + ph * 3f;
        float sn = Mathf.Sin(t * 2.9f);
        j.localPosition = new Vector3(Mathf.Sin(t * .11f + ph) * .17f, Mathf.Lerp(lo, hi, .5f + .5f * Mathf.Sin(t * .17f + ph)), Mathf.Sin(t * .13f + ph * 1.7f) * .05f);
        j.localRotation = Quaternion.Euler(0f, t * 9f, 0f);
        j.localScale = new Vector3(k * (1f + .06f * sn), k * (1f - .05f * sn), k * (1f + .06f * sn));
    }

    public static void Clear()
    {
        Lives.Clear();
        tanks.Clear();
    }
}
