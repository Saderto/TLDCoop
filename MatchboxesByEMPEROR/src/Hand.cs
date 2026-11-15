using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace MatchboxesByEMPEROR;

/// <summary>0.2.0 (the owner, 11-14): the match and the box in the hands follow the box they came from, the striking animation included.
/// The game's first-person rig (bundle research 11-14): FPH_Match holds the skinned WoodenMatchMesh / UsedWoodenMatchMesh (bones origin,
/// MatchStick_handle, MatchBox, MatchBoxOffset, all bind poses identity; the stick runs along +Y from −0.014 to +0.049, head on top) and
/// origin/FX_Hook/FX with MatchFlame and the lights LightIndoor/LightOutdoor/LightHand/LightTip; FPH_Matchbox holds the skinned box
/// «Matches» (110 × 21 × 51 mm, bottom at y 0). While one of our boxes is in the hands, those three meshes are swapped for ours
/// (built here, skinned to the same bones), the flame and lights are scaled by the kind; with anything else in the hands all is put back.
/// 0.3.0: sticks grow behind the fingers, the box sits where the game's strike lands (see BoxMesh), the burnt stick chars as it burns.
/// A friend in co-op still sees the game's plain match (the owner: leave it).</summary>
internal static class Hand
{
    // ---- by kind (my numbers, 11-14)
    static float Light(string k) => k switch { "Storm" => 1.35f, "Long" => 1.15f, "Book" => .75f, _ => 1f };
    static float Flame(string k) => k switch { "Storm" => 1.3f, "Long" => 1.15f, "Book" => .8f, _ => 1f };
    static float Jitter(string k) => k switch { "Book" => .22f, "Storm" => .05f, _ => 0f };
    // How much longer than the game's stick, metres. The extra length goes BEHIND the fingers: the head stays where the game's head is,
    // because the striking animation drags that point along the box (0.2.1 in game: a stick grown at the head went through the box).
    // 0.3.2: the fireplace match is 22 mm longer, not 47: at its true 11 cm the head ended in the other hand (the owner, 11-14).
    static float Extra(string k) => k == "Long" ? .022f : k == "Storm" ? .006f : 0f;
    // A fireplace match is then pushed forward through the fingers once it burns, so its whole length shows and the flame goes with it.
    static float Slide(string k) => k == "Long" ? .022f : 0f;
    static Color Tint(string k) => k switch { "Long" => new Color(1f, .93f, .82f), "Storm" => new Color(1f, 1f, .96f), "Book" => new Color(1f, .95f, .9f), _ => Color.white };

    static Color C(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f, 1f);

    /// <summary>The head's colour: by series for the plain and souvenir boxes, fixed for the special kinds.</summary>
    static Color Head(Def d) => d.Kind.Id switch
    {
        "Storm" => C(74, 96, 58),
        "Long" => C(150, 34, 30),
        "Book" => d.Series.Id == "Japan" ? C(236, 232, 220) : C(60, 110, 170),
        "Souvenir" => (d.Label.N % 5) switch { 0 => C(200, 40, 40), 1 => C(40, 130, 70), 2 => C(50, 90, 180), 3 => C(225, 170, 40), _ => C(120, 60, 150) },
        _ => d.Series.Id switch
        {
            "Beasts" => C(110, 52, 36), "Fire" => C(196, 36, 30), "Coast" => C(40, 72, 130), "Sea" => C(36, 120, 110),
            "Winter" => C(232, 232, 228), "Space" => C(226, 178, 52), _ => C(34, 30, 30),
        },
    };

    // ---- what we found in the scene
    static SkinnedMeshRenderer? stick, used, box;
    static Mesh? stick0, used0, box0;
    static Texture? stickTex0, usedTex0, boxTex0;
    static Transform? hook, fx, handle;
    static Vector3 handle0;
    static ParticleSystem? flame;
    static float flameSize0 = -1f;
    static readonly List<(Light l, float i0, float r0, Color c0)> lights = new();
    static Vector3 hook0, fx0;
    static float nextFind;
    static string applied = "";
    static bool wasBurning;
    static float igniteAt = -100f;
    static readonly Dictionary<IntPtr, float> lastI = new();
    static float lastHookY = float.NaN;

    static bool Alive(UnityEngine.Object? o) => o != null && o;

    static bool InScene(Component c)
    {
        try { return c.gameObject.scene.IsValid(); } catch { return false; }
    }

    static bool Under(Transform? t, string part)
    {
        for (; t != null; t = t.parent) if (t.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    static Transform? Deep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++) { var r = Deep(t.GetChild(i), name); if (r != null) return r; }
        return null;
    }

    static void Find()
    {
        if (Alive(stick) && Alive(box) && Alive(hook)) return;
        if (Time.unscaledTime < nextFind) return;
        nextFind = Time.unscaledTime + .5f;
        bool hadStick = Alive(stick), hadBox = Alive(box);
        foreach (var r in Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>())
        {
            if (r == null || !InScene(r)) continue;
            string n = r.gameObject.name;
            if (!hadStick && n == "WoodenMatchMesh" && Under(r.transform, "FPH_Match")) { stick = r; stick0 = r.sharedMesh; stickTex0 = r.sharedMaterial != null ? r.sharedMaterial.mainTexture : null; }
            else if (!hadStick && n == "UsedWoodenMatchMesh" && Under(r.transform, "FPH_Match")) { used = r; used0 = r.sharedMesh; usedTex0 = r.sharedMaterial != null ? r.sharedMaterial.mainTexture : null; }
            else if (!hadBox && n == "Matches" && Under(r.transform, "FPH_Matchbox")) { box = r; box0 = r.sharedMesh; boxTex0 = r.sharedMaterial != null ? r.sharedMaterial.mainTexture : null; }
        }
        if (!hadStick && Alive(stick))
        {
            applied = "";
            var root = stick!.transform;
            while (root.parent != null && !root.name.StartsWith("FPH_Match", StringComparison.Ordinal)) root = root.parent;
            hook = Deep(root, "FX_Hook");
            try { var bs = stick.bones; handle = bs != null && bs.Length > 1 ? bs[1] : null; if (handle != null) handle0 = handle.localPosition; } catch { handle = null; }
            fx = hook != null ? Deep(hook, "FX") : null;
            lights.Clear(); lastI.Clear(); lastHookY = float.NaN;
            flame = null;
            if (hook != null)
            {
                hook0 = hook.localPosition;
                if (fx != null) fx0 = fx.localScale;
                foreach (var l in hook.GetComponentsInChildren<Light>(true)) if (l != null) lights.Add((l, l.intensity, l.range, l.color));
                foreach (var p in hook.GetComponentsInChildren<ParticleSystem>(true)) if (p != null && p.gameObject.name == "MatchFlame") { flame = p; try { flameSize0 = p.main.startSizeMultiplier; } catch { flameSize0 = -1f; } }
            }
            Safe.Info($"Hand: match rig found under {root.name}: stick {stick0?.name}, used {(Alive(used) ? used0?.name : "none")}, hook {(hook != null)} at {hook0:F3}, {lights.Count} lights, flame {(flame != null)}");
        }
        if (!hadBox && Alive(box)) { applied = ""; Safe.Info($"Hand: box rig found, mesh {box0?.name}, {box!.bones?.Length ?? 0} bones"); }
    }

    public static void LateTick()
    {
        GearItem? g = null;
        try { g = GameManager.GetPlayerManagerComponent()?.m_ItemInHands; } catch { }
        Def? def = Defs.Is(g, out var d) ? d : null;
        if (def == null && applied == "") return;
        Find();
        string want = def?.Id ?? "";
        if (want != applied) Apply(def);
        if (def != null) Frame(def, g!.m_MatchesItem);
    }

    static void Apply(Def? def)
    {
        try
        {
            if (Alive(stick))
            {
                stick!.sharedMesh = def != null ? StickMesh(def.Kind.Id) : stick0;
                stick.material.mainTexture = def != null ? Palette(def) : stickTex0;
                stick.updateWhenOffscreen = true;
            }
            if (Alive(used))
            {
                used!.sharedMesh = def != null ? StickMesh(def.Kind.Id) : used0;
                used.material.mainTexture = def != null ? Palette(def) : usedTex0;
                used.updateWhenOffscreen = true;
            }
            if (Alive(box))
            {
                var tex = def != null ? Assets.Texture(def.Texture) : null;
                var mesh = def != null && tex != null ? BoxMesh(def.Kind.Id) : null;
                box!.sharedMesh = mesh ?? box0;
                box.material.mainTexture = mesh != null ? tex : boxTex0;
                box.updateWhenOffscreen = true;
            }
            if (def == null)
            {
                // everything of the game's back as it was
                foreach (var (l, i0, r0, c0) in lights)
                    if (Alive(l)) { l.intensity = i0; l.range = r0; l.color = c0; }
                lastI.Clear();
                if (Alive(hook)) hook!.localPosition = hook0;
                if (Alive(fx)) fx!.localScale = fx0;
                FlameSize(1f);
                if (Alive(handle)) handle!.localPosition = handle0;
                lastHookY = float.NaN;
            }
            Safe.Info($"Hand: {(def != null ? def.Id + " (" + def.Kind.Id + ")" : "the game's own match")} in the hands; stick {Alive(stick)}, box {Alive(box)}");
        }
        catch (Exception e) { Safe.Log("Hand apply", e); }
        applied = def?.Id ?? "";
        wasBurning = false;
    }

    static void Frame(Def def, MatchesItem? mi)
    {
        try
        {
            string k = def.Kind.Id;
            bool burning = false;
            float p = 0f;
            // before the match is lit the game's progress is 0/0: not a number (0.2.1 in game: that number went into the flame's
            // position, and the flame with its lights was gone for the storm and fireplace matches)
            try { if (mi != null) { burning = mi.IsBurning(); float raw = burning ? mi.GetBurnProgress() : 0f; p = float.IsFinite(raw) ? Mathf.Clamp01(raw) : 0f; } } catch { }
            if (burning && !wasBurning)
            {
                igniteAt = Time.time;
                // storm matches flare up: a burst of sparks from the flame
                if (k == "Storm" && Alive(flame)) { try { flame!.Emit(30); } catch { } }
            }
            wasBurning = burning;

            float m = Light(k);
            float since = Time.time - igniteAt;
            if (k == "Storm" && since < .8f) m *= 1f + 1.6f * (1f - since / .8f);
            float j = Jitter(k);
            if (j > 0f) m *= 1f + j * (Mathf.PerlinNoise(Time.time * 9f, .37f) - .5f) * 2f;
            var tint = Tint(k);
            foreach (var (l, i0, r0, c0) in lights)
            {
                if (!Alive(l)) continue;
                // the game may rewrite the brightness as the flame flickers: what it wrote is the base, ours is that times the kind
                float cur = l.intensity;
                float b = lastI.TryGetValue(l.Pointer, out var mine) && Mathf.Abs(cur - mine) < .0005f ? baseI[l.Pointer] : cur;
                baseI[l.Pointer] = b;
                float set = b * m;
                l.intensity = set;
                lastI[l.Pointer] = set;
                l.range = r0 * Mathf.Sqrt(Light(k));
                l.color = new Color(c0.r * tint.r, c0.g * tint.g, c0.b * tint.b, c0.a);
            }
            // the flame picture: bigger by the kind, a storm match flares at the strike
            FlameSize(Flame(k) * (k == "Storm" && since < 1.2f ? 1f + .5f * (1f - since / 1.2f) : 1f));
            // the burnt stick the game shows while the match burns: charred further down as it burns
            // 0.3.3 (the owner: still in jerks): one mesh, the burnt part is a line in its texture moved every frame (Char)
            Char(k, burning ? BurnLine(k, p) : 1f);
            // the fireplace match comes forward through the fingers after the strike; the flame's hook goes with it.
            // The game itself only writes the flame's own x (MatchesItem.UpdateFXTransform), never these two, so plain values are safe.
            float slideMax = Slide(k);
            if (slideMax > 0f)
            {
                float t = burning ? Mathf.Clamp01((since - .45f) / .6f) : 0f;
                float sl = slideMax * t * t * (3f - 2f * t);
                if (!float.IsFinite(sl)) sl = 0f;
                if (Alive(handle)) handle!.localPosition = handle0 + new Vector3(0f, sl, 0f);
            }
            // 0.3.2 (the owner: only the top of the head burns, the flame does not go down). The game's flame is a picture that rises
            // from its source, drawn about 12 mm above it, and the source sits where the game's short stick needs it. So the source
            // is put by us every frame: the flame stands on the burning line of OUR stick, the head at first, then down with the char.
            if (burning && Alive(flame) && Alive(hook) && Alive(handle))
            {
                var want = handle!.TransformPoint(new Vector3(0f, BurnLine(k, p), 0f)) - Vector3.up * (-Settings.FlameRaise.Value * .001f);
                var d = want - flame!.transform.position;
                if (float.IsFinite(d.x) && float.IsFinite(d.y) && float.IsFinite(d.z) && d.sqrMagnitude < .09f) hook!.position += d;
            }
            else if (!burning && Alive(hook)) hook!.localPosition = hook0;
            if (slideMax > 0f)
            {
                float sl = Alive(handle) ? handle!.localPosition.y - handle0.y : 0f;
                if (burning && Time.time - lastSay > 2f) { lastSay = Time.time; Safe.Debug($"Hand: {def.Id} burning {p:0.00}, slide {sl * 1000f:0} mm, hook {(Alive(hook) ? hook!.localPosition.ToString("F3") : "none")}, fx active {(Alive(fx) && fx!.gameObject.activeInHierarchy)}"); }
            }
            else if (burning && Time.time - lastSay > 2f) { lastSay = Time.time; Safe.Debug($"Hand: {def.Id} burning {p:0.00}, hook {(Alive(hook) ? hook!.localPosition.ToString("F3") : "none")}, fx active {(Alive(fx) && fx!.gameObject.activeInHierarchy)}, lights {lights.Count}"); }
        }
        catch (Exception e) { Safe.Log("Hand frame", e); }
    }

    static readonly Dictionary<IntPtr, float> baseI = new();
    static float lastSay;

    // ---- our meshes, in the rig's bind space

    static readonly Dictionary<string, Mesh> sticks = new();
    static readonly Dictionary<string, Mesh> boxes = new();
    static readonly Dictionary<string, Texture2D> palettes = new();

    const float CharK = 20f;      // texture v per metre of stick: one texel is .8 mm

    /// <summary>Two columns (stick, head), bottom half as new, top half burnt, a glowing line between. The mesh's v is the height
    /// above the burning line (Char), so the line creeps down the stick without steps.</summary>
    static Texture2D Palette(Def d)
    {
        var head = Head(d);
        string key = d.Kind.Id + "|" + ColorUtility.ToHtmlStringRGB(head);
        if (palettes.TryGetValue(key, out var t) && t != null) return t;
        var body = d.Kind.Id == "Book" ? C(205, 196, 176) : C(222, 190, 132);
        t = new Texture2D(8, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 8; x++)
            {
                bool hd = x >= 4;
                Color c = y < 29 ? (hd ? head : body)
                    : y == 29 ? (hd ? head : C(120, 84, 48))                 // scorched just under the fire
                    : y < 32 ? (hd ? C(255, 190, 90) : C(230, 120, 40))      // the glowing line
                    : (hd ? C(70, 66, 62) : C(36, 30, 26));
                t.SetPixel(x, y, c);
            }
        t.Apply(false);
        return palettes[key] = t;
    }

    sealed class Builder
    {
        public readonly List<Vector3> V = new(), N = new();
        public readonly List<Vector2> U = new();
        public readonly List<int> T = new();

        int Add(Vector3 p, Vector3 n, int col) { V.Add(p); N.Add(n); U.Add(new Vector2(col == 0 ? .25f : .75f, 0f)); return V.Count - 1; }

        void Tri(int a, int b, int c, Vector3 outward)
        {
            // the game's faces are clockwise from outside: (b−a)×(c−a) points at the viewer
            if (Vector3.Dot(Vector3.Cross(V[b] - V[a], V[c] - V[a]), outward) < 0f) (b, c) = (c, b);
            T.Add(a); T.Add(b); T.Add(c);
        }

        static Vector3 P(float y, float rx, float rz, int j, int n, float turn)
        {
            float a = (j + turn) * Mathf.PI * 2f / n;
            return new Vector3(rx * Mathf.Cos(a), y, rz * Mathf.Sin(a));
        }

        /// <summary>A band between two rings (y, half-width on x, half-width on z), one colour.</summary>
        public void Band(float y0, float rx0, float rz0, float y1, float rx1, float rz1, int col, int n, float turn)
        {
            for (int j = 0; j < n; j++)
            {
                Vector3 a = P(y0, rx0, rz0, j, n, turn), b = P(y0, rx0, rz0, j + 1, n, turn), c = P(y1, rx1, rz1, j + 1, n, turn), d = P(y1, rx1, rz1, j, n, turn);
                var mid = (a + b + c + d) / 4f;
                var o = new Vector3(mid.x, 0f, mid.z).normalized;
                if (o == Vector3.zero) o = Vector3.up;
                int ia = Add(a, o, col), ib = Add(b, o, col), ic = Add(c, o, col), id = Add(d, o, col);
                Tri(ia, ib, ic, o); Tri(ia, ic, id, o);
            }
        }

        public void Cap(float y, float rx, float rz, int col, int n, float turn, bool up)
        {
            var o = up ? Vector3.up : Vector3.down;
            int c = Add(new Vector3(0f, y, 0f), o, col);
            for (int j = 0; j < n; j++)
            {
                int a = Add(P(y, rx, rz, j, n, turn), o, col), b = Add(P(y, rx, rz, j + 1, n, turn), o, col);
                Tri(c, a, b, o);
            }
        }

        public Mesh Make(string name, int bone)
        {
            var m = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            m.vertices = new Il2CppStructArray<Vector3>(V.ToArray());
            m.normals = new Il2CppStructArray<Vector3>(N.ToArray());
            m.uv = new Il2CppStructArray<Vector2>(U.ToArray());
            m.triangles = new Il2CppStructArray<int>(T.ToArray());
            Skin(m, V.Count, bone);
            m.RecalculateBounds();
            return m;
        }
    }

    static void Skin(Mesh m, int count, int bone)
    {
        var w = new Il2CppStructArray<BoneWeight>(count);
        for (int i = 0; i < count; i++) w[i] = new BoneWeight { boneIndex0 = bone, weight0 = 1f };
        m.boneWeights = w;
        var bp = new Il2CppStructArray<Matrix4x4>(4);
        for (int i = 0; i < 4; i++) bp[i] = Matrix4x4.identity;
        m.bindposes = bp;
    }

    /// <summary>The stick of a kind: tail at −0.0141 as the game's, head top at 0.0492 + what the kind adds. burnt: the charred one shown after it goes out.</summary>
    // the charred part grows in steps of about half a millimetre: to the eye it creeps down the stick (the owner 11-14: not in jumps)
    const int CharSteps = 60;

    static void FlameSize(float k)
    {
        if (flameSize0 <= 0f || !Alive(flame)) return;
        try { var main = flame!.main; main.startSizeMultiplier = flameSize0 * k; } catch { }
    }

    static float HeadLen(string kind) => kind == "Book" ? .0065f : kind == "Storm" ? .026f : .0085f;

    /// <summary>Where the stick burns at this moment, along the stick. 0.3.3 (the owner): it starts at the very top of the head; a
    /// plain head flares up at once and the fire then walks down the wood, a storm match's long coated head burns all the way; and it
    /// stops 26 mm short of where the game's stick starts, above the fingers (it used to end inside them).</summary>
    static float BurnLine(string kind, float p)
    {
        const float top = .0492f;
        float h0 = top - HeadLen(kind), end = .018f - Slide(kind);
        if (kind == "Storm") return Mathf.Lerp(top - .001f, end, p);
        return p < .08f ? Mathf.Lerp(top - .001f, h0, p / .08f) : Mathf.Lerp(h0, end, (p - .08f) / .92f);
    }

    static readonly Dictionary<string, (Mesh m, Il2CppStructArray<Vector2> uv, float[] y, float last)> live = new();

    /// <summary>Moves the burnt line of the kind's stick to this height (1 = far above the stick: not burnt at all).</summary>
    static void Char(string kind, float burnY)
    {
        if (!live.TryGetValue(kind, out var e) || e.m == null || !e.m) return;
        if (Mathf.Abs(e.last - burnY) < .00002f) return;
        for (int i = 0; i < e.y.Length; i++) { var u = e.uv[i]; u.y = (e.y[i] - burnY) * CharK + .5f; e.uv[i] = u; }
        e.m.uv = e.uv;
        live[kind] = (e.m, e.uv, e.y, burnY);
    }

    static Mesh StickMesh(string kind)
    {
        if (sticks.TryGetValue(kind, out var have) && have != null) return have;
        float tail = -.0141f - Extra(kind);
        const float top = .0492f;
        var b = new Builder();
        float h0 = top - HeadLen(kind);
        if (kind == "Book")
        {
            // a flat paper match torn from the book
            float rx = .0017f, rz = .00045f;
            b.Cap(tail, rx, rz, 0, 4, .5f, false);
            b.Band(tail, rx, rz, h0, rx, rz, 0, 4, .5f);
            b.Band(h0, rx, rz, h0 + .002f, rx * 1.25f, rz * 2.4f, 1, 8, 0f);
            b.Band(h0 + .002f, rx * 1.25f, rz * 2.4f, top - .0012f, rx * 1.1f, rz * 2.2f, 1, 8, 0f);
            b.Band(top - .0012f, rx * 1.1f, rz * 2.2f, top, rx * .4f, rz, 1, 8, 0f);
            b.Cap(top, rx * .4f, rz, 1, 8, 0f, true);
        }
        else
        {
            bool storm = kind == "Storm";
            float r = storm ? .00145f : .0011f;                 // the stick: square, a corner toward the viewer
            float hr = storm ? .0026f : .0023f;
            b.Cap(tail, r, r, 0, 4, .5f, false);
            b.Band(tail, r, r, h0, r, r, 0, 4, .5f);
            if (storm)
            {
                b.Band(h0, r * 1.2f, r * 1.2f, h0 + .003f, hr * .8f, hr * .8f, 1, 8, 0f);
                b.Band(h0 + .003f, hr * .8f, hr * .8f, top - .008f, hr * .85f, hr * .85f, 1, 8, 0f);
                b.Band(top - .008f, hr * .85f, hr * .85f, top - .004f, hr, hr, 1, 8, 0f);
            }
            else b.Band(h0, r, r, top - .004f, hr, hr, 1, 8, 0f);
            b.Band(top - .004f, hr, hr, top - .0012f, hr * .85f, hr * .85f, 1, 8, 0f);
            b.Band(top - .0012f, hr * .85f, hr * .85f, top, hr * .35f, hr * .35f, 1, 8, 0f);
            b.Cap(top, hr * .35f, hr * .35f, 1, 8, 0f, true);
        }
        var m = b.Make("EMB_Stick_" + kind, 1);                  // bone 1: MatchStick_handle
        try { m.MarkDynamic(); } catch { }
        var ys = new float[b.V.Count];
        for (int i = 0; i < ys.Length; i++) ys[i] = b.V[i].y;
        live[kind] = (m, new Il2CppStructArray<Vector2>(b.U.ToArray()), ys, float.NaN);
        sticks[kind] = m;
        Char(kind, 1f);
        return m;
    }

    /// <summary>The kind's box for the left hand, placed for the game's striking animation. Read from the game's clips (11-14): the
    /// match head rests against the long side z = −25.5 mm of the game's box (110 × 21 × 51 mm, bottom at y 0) at x ≈ −49, y ≈ 4, then
    /// is dragged along that side to x ≈ −13, rising to y ≈ 19. So ours is as wide as the game's (the same two striker sides), starts at
    /// x = −51 where the head rests, a thin one is lifted to the game's top edge, and none is thicker than the game's (0.2.1 in game:
    /// a box shrunk around the middle left the head in the air and the stick went through a thicker box).</summary>
    static Mesh? BoxMesh(string kind)
    {
        if (boxes.TryGetValue(kind, out var have) && have != null) return have;
        var src = Assets.LoadMesh("meshes/" + kind + ".fpmesh");
        if (src == null) return null;
        var bd = src.bounds;
        float L = Mathf.Max(bd.size.x, .001f), H = Mathf.Max(bd.size.y, .001f), W = Mathf.Max(bd.size.z, .001f);
        float s = .051f / W;
        float sx = Mathf.Min(s * 1.1f, .110f / L);            // up to a tenth longer than its true shape, to reach the hand that holds it
        float sy = Mathf.Min(s, .021f / H);
        float Lh = L * sx, Hh = H * sy;
        float left = Lh > .104f ? -Lh / 2f + .0007f : -.051f;
        float bottom = kind == "Book" ? .017f - Hh : Hh < .021f ? .021f - Hh : 0f;
        var v = src.vertices;
        var nv = new Il2CppStructArray<Vector3>(v.Length);
        for (int i = 0; i < v.Length; i++)
            nv[i] = new Vector3((v[i].x - bd.min.x) * sx + left, (v[i].y - bd.min.y) * sy + bottom, (v[i].z - bd.center.z) * s);
        var m = new Mesh { name = "EMB_HandBox_" + kind, hideFlags = HideFlags.HideAndDontSave };
        m.vertices = nv;
        m.normals = src.normals;
        m.uv = src.uv;
        m.triangles = src.triangles;
        Skin(m, v.Length, 2);                                    // bone 2: MatchBox, as the game's box (read from its mesh 11-14)
        m.RecalculateBounds();
        Safe.Info($"Hand: box of kind {kind} for the hand: {Lh * 1000f:0} × {Hh * 1000f:0} × {W * s * 1000f:0} mm, from x {left * 1000f:0}, bottom at {bottom * 1000f:0}");
        return boxes[kind] = m;
    }
}
