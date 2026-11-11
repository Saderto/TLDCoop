using System.Reflection;
namespace SmokesBySeagull;

/// <summary>«Табак» / «Tobacco» in the action wheel: a ninth sector of the main wheel (the eight game sectors are moved closer to make
/// room) that opens a wheel of our items; our items leave the first aid wheel. Our sector is a visual copy of a game sector: hover and click
/// are handled here (the game's own sector arrays stay at eight).</summary>
[HarmonyPatch]
internal static class Radial
{
    static GameObject? ours;
    static UISprite? oursBg, oursGlow;
    static UITexture? oursTex;
    static Panel_ActionsRadial? panel;
    static readonly List<Saved> saved = new();
    static Saved? oursSaved;
    static readonly List<(RadialMenuArm arm, float angle)> wheel = new();   // the nine sectors by angle, ours = arm null
    static float oursAngle;
    static bool nine, hovered, dumped;
    static Vector3 center;
    static Texture2D? icon;

    static bool Ours(GearItem? gi) => Defs.Is(gi, out var d) && d.Kind != Kind.Lighter && d.Kind != Kind.Lit && d.Kind != Kind.Refill;

    static List<GearItem> OurItems()
    {
        var list = new List<GearItem>();
        try
        {
            var seen = new HashSet<string>();
            foreach (var o in GameManager.GetInventoryComponent().m_Items)
            {
                GearItem? g = o;
                if (g == null || !Ours(g)) continue;
                if (seen.Add(g.name)) list.Add(g);
            }
        }
        catch (Exception e) { Safe.Log("Tobacco wheel items", e); }
        return list;
    }

    // our items out of the first aid wheel
    [HarmonyPostfix, HarmonyPatch(typeof(Panel_ActionsRadial), nameof(Panel_ActionsRadial.GetFirstAidItemsInInventory))]
    static void NoTobaccoInFirstAid(Il2CppSystem.Collections.Generic.List<GearItem> __result)
    {
        if (!Settings.SmokesTab.Value || __result == null) return;
        try
        {
            for (int i = __result.Count - 1; i >= 0; i--)
                if (Ours(__result[i])) __result.RemoveAt(i);
        }
        catch (Exception e) { Safe.Log("First aid wheel", e); }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Panel_ActionsRadial), nameof(Panel_ActionsRadial.ShowPrimaryRadial))]
    static void Primary(Panel_ActionsRadial __instance)
    {
        if (!Settings.SmokesTab.Value) return;
        try
        {
            panel = __instance;
            Build(__instance);
            Nine(true);
        }
        catch (Exception e) { Safe.Log("Tobacco wheel sector", e); }
    }

    // every other wheel: back to eight sectors
    static IEnumerable<MethodBase> Others() =>
        typeof(Panel_ActionsRadial).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.StartsWith("Show") && m.Name.EndsWith("Radial") && m.Name != nameof(Panel_ActionsRadial.ShowPrimaryRadial) && m.GetParameters().Length == 0)
            .Concat(new MethodBase[] { AccessTools.Method(typeof(Panel_ActionsRadial), nameof(Panel_ActionsRadial.ShowGearRadial)) })
            .Where(m => m != null)
            .Cast<MethodBase>();

    [HarmonyPatch]
    static class OtherWheels
    {
        static IEnumerable<MethodBase> TargetMethods() => Others();
        static void Prefix()
        {
            try { Nine(false); } catch (Exception e) { Safe.Log("Tobacco wheel restore", e); }
        }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Panel_ActionsRadial), nameof(Panel_ActionsRadial.Enable), new[] { typeof(bool) })]
    static void Enabled(bool enable)
    {
        if (!enable) { hovered = false; try { Nine(false); } catch { } }
    }

    static void Build(Panel_ActionsRadial p)
    {
        if (ours != null && ours) return;
        saved.Clear();
        var arms = p.m_RadialArms;
        if (arms == null || arms.Length < 8) { Safe.Info($"Tobacco wheel: {arms?.Length ?? 0} arms, not built"); return; }
        RadialMenuArm? centerArm = null, src = null;
        foreach (var a in arms)
        {
            if (a == null) continue;
            if (a.m_IsCenterButton) centerArm = a; else src ??= a;
        }
        if (src == null) return;
        center = centerArm != null ? centerArm.transform.localPosition : Vector3.zero;
        if (!dumped)
        {
            dumped = true;
            var sb = new System.Text.StringBuilder("Action wheel arms:");
            foreach (var a in arms)
            {
                if (a == null) continue;
                var t = a.transform;
                sb.Append($"\n  {a.name} center {a.m_IsCenterButton} parent {t.parent?.name} local {t.localPosition:F1} rot {t.localEulerAngles.z:F1} scale {t.localScale:F2}");
                if (a.m_BG != null) sb.Append($" | bg {a.m_BG.spriteName} {a.m_BG.width}x{a.m_BG.height} at {a.m_BG.transform.localPosition:F1} rot {a.m_BG.transform.localEulerAngles.z:F1} pivot {a.m_BG.pivot}");
                if (a.m_Sprite != null) sb.Append($" | icon {a.m_Sprite.spriteName} at {a.m_Sprite.transform.localPosition:F1}");
                if (a.m_GlowSprite != null) sb.Append($" | glow {a.m_GlowSprite.spriteName} {a.m_GlowSprite.width}x{a.m_GlowSprite.height} rot {a.m_GlowSprite.transform.localEulerAngles.z:F1}");
                if (a.m_RadialInfo != null) sb.Append($" | info {a.m_RadialInfo.m_RadialElement} {a.m_RadialInfo.m_SpriteName}");
                var cols = a.GetComponentsInChildren<Collider>(true);
                sb.Append($" | colliders {cols.Length}");
            }
            sb.Append($"\n  center radius {p.m_CenterButtonRadius}");
            Safe.Info(sb.ToString());
        }
        foreach (var a in arms)
            if (a != null && !a.m_IsCenterButton) saved.Add(Save(a));
        oursSaved = Save(src);
        FindDividers(src.transform.parent, arms);
        ours = UnityEngine.Object.Instantiate(src.gameObject, src.transform.parent);
        ours.name = "RadialArm_SmokesTobacco";
        var copy = ours.GetComponent<RadialMenuArm>();
        if (copy != null)
        {
            oursBg = copy.m_BG;
            oursGlow = copy.m_GlowSprite;
            oursTex = copy.m_Texture;
            if (copy.m_Sprite != null) copy.m_Sprite.enabled = false;
            if (copy.m_StackLabel != null) copy.m_StackLabel.enabled = false;
            if (copy.m_NotificationSprite != null) copy.m_NotificationSprite.enabled = false;
            if (copy.m_NewItemHighlightAnim != null) copy.m_NewItemHighlightAnim.gameObject.SetActive(false);
            UnityEngine.Object.DestroyImmediate(copy);
        }
        foreach (var c in ours.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
        foreach (var b in ours.GetComponentsInChildren<UIButton>(true)) UnityEngine.Object.Destroy(b);
        icon ??= Assets.Texture("icons/radial_tobacco.png", compress: false, mipmaps: false);
        if (oursTex != null && icon != null)
        {
            oursTex.mainTexture = icon;
            oursTex.enabled = true;
            oursTex.gameObject.SetActive(true);
            oursTex.color = Color.white;
        }
        ours.SetActive(false);
        Safe.Info($"Tobacco wheel sector made from {src.name}; icon {(icon != null)}, texture {(oursTex != null)}");
    }

    // the wheel's background picture has the eight sector lines drawn into it: a copy with nine lines is made once and shown instead
    static UISprite? backSprite;
    static UITexture? back9;
    static float armReach;   // world radius the sector wedges reach to
    static float iconReach;  // world radius of the sector icons (mean)

    static void FindDividers(Transform? root, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<RadialMenuArm> arms)
    {
        backSprite = null;
        if (root == null) return;
        var t = root.Find("RadialBackground");
        backSprite = t != null ? t.GetComponent<UISprite>() : null;
        if (backSprite == null) { Safe.Info("Action wheel: no RadialBackground sprite"); return; }
        armReach = 0f;
        try
        {
            var c0 = backSprite.transform.position;
            foreach (var a in arms)
            {
                if (a == null || a.m_BG == null) continue;
                var bg = a.m_BG;
                float r = Vector3.Distance(bg.transform.position, c0) + bg.width * .5f * Mathf.Abs(bg.transform.lossyScale.x);
                if (r > armReach) armReach = r;
            }
            float sum = 0f; int n = 0;
            foreach (var a in arms)
            {
                if (a == null || a.m_IsCenterButton || a.m_Sprite == null) continue;
                sum += Vector3.Distance(a.m_Sprite.transform.position, c0); n++;
            }
            iconReach = n > 0 ? sum / n : 0f;
            Safe.Info($"Action wheel: icons at {iconReach:F3} world ({n} icons)");
            Safe.Info($"Action wheel: arms reach {armReach:F3} world, background sprite {backSprite.width}x{backSprite.height} scale {backSprite.transform.lossyScale.x:F5}");
        }
        catch (Exception e) { Safe.Log("Action wheel reach", e); }
        // 11-10: no background of ours (user), the game's stays
    }

    static void MakeBack9(UISprite sp)
    {
        Texture? tex = null;
        UISpriteData? data = null;
        UIAtlas? atlas = null;
        try { atlas = sp.atlas; } catch { }
        try { tex = sp.mainTexture; } catch { }
        if (tex == null) try { tex = atlas != null ? atlas.texture : null; } catch { }
        if (tex == null) try { tex = sp.material != null ? sp.material.mainTexture : null; } catch { }
        if (tex == null) try { tex = atlas != null && atlas.spriteMaterial != null ? atlas.spriteMaterial.mainTexture : null; } catch { }
        try { data = sp.GetAtlasSprite(); } catch { }
        if (data == null) try { data = atlas != null ? atlas.GetSprite(sp.spriteName) : null; } catch { }
        if (data == null && atlas != null)
        {
            try { var rep = atlas.replacement; if (rep != null) data = rep.GetSprite(sp.spriteName); } catch { }
            if (data == null)
                try { foreach (var d in atlas.spriteList) if (d != null && d.name == sp.spriteName) { data = d; break; } } catch { }
        }
        // 11-09 evening: TLD's "Base Atlas" is a UISDAtlas (SD/HD swap); the sprite list lives in the atlas it wraps, whose texture is
        // the _hd one the sprite is drawn with (log 1943: no data in the outer atlas, texture Base Atlas_hd 4096)
        if (data == null && atlas != null)
        {
            var seen = new List<string>();
            var cands = new List<UIAtlas>();
            void AddC(UIAtlas? a) { if (a != null && !cands.Exists(x => x.Pointer == a.Pointer)) cands.Add(a); }
            AddC(atlas);
            try { AddC(atlas.replacement); } catch { }
            try { var sd = atlas.TryCast<UISDAtlas>(); if (sd != null) seen.Add($"SD atlas, reduction {sd.m_ReductionFactor}"); } catch { }
            foreach (var a in cands)
            {
                int n1 = -1, n2 = -1, n3 = -1;
                try { var l = a.mSprites; if (l != null) { n1 = l.Count; foreach (var d in l) if (d != null && d.name == sp.spriteName) { data = d; break; } } } catch { }
                try { var l = a.spriteList; if (l != null) { n2 = l.Count; if (data == null) foreach (var d in l) if (d != null && d.name == sp.spriteName) { data = d; break; } } } catch { }
                try
                {
                    var l = a.spriteList;
                    if (l != null)
                    {
                        var hits = new List<string>();
                        foreach (var d in l)
                        {
                            if (d == null || d.name == null) continue;
                            var nm = d.name.ToLowerInvariant();
                            if (data == null && nm == sp.spriteName.ToLowerInvariant()) data = d;
                            if (nm.Contains("radial") || nm.Contains("wheel")) hits.Add($"{d.name} {d.width}x{d.height}");
                        }
                        if (hits.Count > 0) seen.Add($"{a.name} radial sprites: " + string.Join(", ", hits.Take(40)));
                    }
                }
                catch { }
                Rect legacy = default; bool haveLegacy = false;
                try
                {
                    var l = a.sprites;
                    if (l != null)
                    {
                        n3 = l.Count;
                        foreach (var o in l) if (o != null && o.name == sp.spriteName) { legacy = o.outer; haveLegacy = true; break; }
                    }
                }
                catch { }
                Texture? at = null;
                try { at = a.texture; } catch { }
                seen.Add($"{a.name} ({a.GetIl2CppType().Name}) sprites {n1}/{n2}/{n3} texture {(at != null ? at.name + " " + at.width : "none")}");
                if (data == null && haveLegacy && at != null)
                {
                    bool texCoords = false;
                    try { texCoords = a.mCoordinates == UIAtlas.Coordinates.TexCoords; } catch { }
                    if (texCoords) legacy = new Rect(legacy.x * at.width, legacy.y * at.height, legacy.width * at.width, legacy.height * at.height);
                    data = new UISpriteData();
                    data.SetRect(Mathf.RoundToInt(legacy.x), Mathf.RoundToInt(legacy.y), Mathf.RoundToInt(legacy.width), Mathf.RoundToInt(legacy.height));
                }
                if (data != null)
                {
                    if (at != null) tex = at;
                    seen.Add($"found '{sp.spriteName}' in {a.name}: {data.x},{data.y} {data.width}x{data.height}");
                    break;
                }
            }
            Safe.Info("Action wheel background atlases: " + string.Join("; ", seen));
        }
        // the rect in texture pixels, bottom-left origin: from the sprite data, else from the UVs the sprite itself draws with
        RectInt rect = default;
        bool haveRect = false;
        if (tex != null && data != null)
        {
            rect = new RectInt(data.x, tex.height - data.y - data.height, data.width, data.height);
            haveRect = rect.x >= 0 && rect.y >= 0 && rect.xMax <= tex.width && rect.yMax <= tex.height && rect.width > 8 && rect.height > 8;
            if (!haveRect) Safe.Info($"Action wheel background: sprite rect {data.x},{data.y} {data.width}x{data.height} outside texture {tex.width}x{tex.height}");
        }
        if (tex != null && !haveRect)
        {
            try
            {
                // what the panel really drew it with (log 1642: the atlas had no data for the sprite and OnFill gave nothing)
                BetterList<Vector2>? uvs = null;
                try { var g = sp.geometry; if (g != null && g.uvs != null && g.uvs.size > 0) uvs = g.uvs; } catch { }
                try { var dc = sp.drawCall; if (dc != null && dc.mTexture != null) tex = dc.mTexture; } catch { }
                if (uvs == null)
                {
                    uvs = new BetterList<Vector2>();
                    var verts = new BetterList<Vector3>();
                    var cols = new BetterList<Color32>();
                    try { sp.OnFill(verts, uvs, cols); } catch (Exception e) { Safe.Log("Action wheel background fill", e); }
                }
                Safe.Info($"Action wheel background: {uvs.size} UVs, texture {(tex != null ? tex.name + " " + tex.width + "x" + tex.height : "none")}");
                float u0 = 1f, v0 = 1f, u1 = 0f, v1 = 0f;
                for (int i = 0; i < uvs.size; i++)
                {
                    var uv = uvs[i];
                    u0 = Math.Min(u0, uv.x); v0 = Math.Min(v0, uv.y); u1 = Math.Max(u1, uv.x); v1 = Math.Max(v1, uv.y);
                }
                if (uvs.size > 0 && u1 > u0 && v1 > v0)
                {
                    int x0 = Mathf.RoundToInt(u0 * tex.width), y0 = Mathf.RoundToInt(v0 * tex.height);
                    rect = new RectInt(x0, y0, Mathf.RoundToInt(u1 * tex.width) - x0, Mathf.RoundToInt(v1 * tex.height) - y0);
                    haveRect = rect.width > 8 && rect.height > 8;
                    Safe.Info($"Action wheel background: rect from the sprite's own UVs {rect.x},{rect.y} {rect.width}x{rect.height}");
                }
            }
            catch (Exception e) { Safe.Log("Action wheel background UVs", e); }
        }
        if (tex == null || !haveRect)
        {
            Safe.Info($"Action wheel background: no atlas data (sprite '{sp.spriteName}', atlas {(atlas != null ? atlas.name : "none")}, texture {(tex != null ? tex.name : "none")}, rect {(data != null ? "yes" : "none")}): drawn by the mod");
            // 11-09: the drawn one once filled the whole (much larger) sprite rect = a giant disc. Now sized to where the wedges reach.
            if (armReach > 0f) DrawnBack9(sp);
            return;
        }
        int w = rect.width, h = rect.height;
        // the atlas region read through a render texture (the atlas itself is not readable)
        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;
        var src = new Texture2D(w, h, TextureFormat.RGBA32, false);
        src.ReadPixels(new Rect(rect.x, rect.y, w, h), 0, 0);
        src.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var px = src.GetPixels().ToArray();
        var outPx = new Color[px.Length];
        float cx = (w - 1) * .5f, cy = (h - 1) * .5f;
        const float half = 4f;            // half width of a line, degrees
        Color At(float r, float angDeg)
        {
            float a = angDeg * Mathf.Deg2Rad;
            int x = Mathf.Clamp(Mathf.RoundToInt(cx + Mathf.Sin(a) * r), 0, w - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(cy + Mathf.Cos(a) * r), 0, h - 1);
            return px[y * w + x];
        }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x - cx, dy = y - cy;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = (Mathf.Atan2(dx, dy) * Mathf.Rad2Deg + 360f) % 360f;   // clockwise from the top
                // new lines at 20 + 40k, old ones at 22.5 + 45k
                float rel9 = ((ang - 20f) % 40f + 40f) % 40f;                    // 0..40 from the last new line
                int k9 = Mathf.FloorToInt(((ang - 20f) % 360f + 360f) % 360f / 40f);
                Color c;
                if (rel9 < half || rel9 > 40f - half)
                {
                    float off = rel9 < half ? rel9 : rel9 - 40f;
                    c = At(r, 22.5f + off);                                      // an old line copied here
                }
                else
                {
                    // inside a new sector: the matching part of the old sector nearest by angle, lines left out
                    float mid9 = 40f + 40f * k9;
                    int k8 = Mathf.RoundToInt(mid9 / 45f);
                    float s8 = 45f * k8 - 22.5f + half;
                    c = At(r, s8 + (rel9 - half) / (40f - 2f * half) * (45f - 2f * half));
                }
                outPx[y * w + x] = c;
            }
        var t9 = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        t9.SetPixels(new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color>(outPx));
        t9.Apply();
        UnityEngine.Object.Destroy(src);
        var go = new GameObject("RadialBackground9");
        go.layer = sp.gameObject.layer;
        go.transform.SetParent(sp.transform.parent, false);
        go.transform.localPosition = sp.transform.localPosition;
        go.transform.localRotation = sp.transform.localRotation;
        go.transform.localScale = sp.transform.localScale;
        back9 = go.AddComponent<UITexture>();
        back9.mainTexture = t9;
        back9.width = sp.width;
        back9.height = sp.height;
        back9.depth = sp.depth;
        back9.color = sp.color;
        go.SetActive(false);
        Safe.Info($"Action wheel background with nine lines made ({w}x{h} from atlas {tex.width}x{tex.height})");
    }

    /// <summary>The game's picture could not be read: a wheel background of our own in its style (a soft light disc, a darker inner
    /// ring, nine thin lines), so the nine sectors are split evenly.</summary>
    static void DrawnBack9(UISprite sp)
    {
        // 11-10: the old disc (sized from the wedges, capped at the 800 sprite) came out far too big and lumpy. Now: a clean round
        // fog disc sized from the icon ring (fog to 1.9 x icon radius), no noise in its outline, nine thin lines only outside the ring.
        int w = 512, h = 512;
        var px = new Color[w * h];
        float c = (w - 1) * .5f;
        const float ring = 1.32f / 1.9f;     // the game's ring edge, of our outer radius
        const float solid = 1.45f / 1.9f;    // fog full up to here, then fades out to the edge
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x - c, dy = y - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / c;          // 0..1
                float ang = (Mathf.Atan2(dx, dy) * Mathf.Rad2Deg + 360f) % 360f;
                float n = (Mathf.PerlinNoise(x * .03f, y * .03f) - .5f) * .08f;
                float f = Mathf.Clamp01((1f - r) / (1f - solid));
                float a = f * f * (3f - 2f * f) * .62f;
                float v = .86f + n;
                float rel = ((ang - 20f) % 40f + 40f) % 40f;
                float dl = Math.Min(rel, 40f - rel) * Mathf.Deg2Rad * r * c;   // distance to the nearest line in pixels
                if (r > ring && dl < 1.6f)
                {
                    float k = dl / 1.6f;
                    float fade = Mathf.Clamp01((r - ring) / .04f) * Mathf.Clamp01((1f - r) / .25f);
                    v = Mathf.Lerp(v, v * (.35f + .65f * k), fade);
                    a = Mathf.Max(a, (1f - k) * .45f * fade);
                }
                px[y * w + x] = new Color(v, v, v, a);
            }
        var t9 = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        t9.SetPixels(new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color>(px));
        t9.Apply();
        var go = new GameObject("RadialBackground9");
        go.layer = sp.gameObject.layer;
        go.transform.SetParent(sp.transform.parent, false);
        go.transform.localPosition = sp.transform.localPosition;
        go.transform.localRotation = sp.transform.localRotation;
        go.transform.localScale = sp.transform.localScale;
        back9 = go.AddComponent<UITexture>();
        back9.mainTexture = t9;
        float sc = Mathf.Abs(go.transform.lossyScale.x);
        float reach = iconReach > 0f ? iconReach * 1.9f : armReach * .75f;
        int size = sc > 1e-6f ? Mathf.RoundToInt(2f * reach / sc) : sp.width / 2;
        size = Math.Min(size, Math.Max(sp.width, sp.height) * 3 / 4);
        back9.width = size;
        back9.height = size;
        back9.depth = sp.depth;
        back9.color = sp.color;
        go.SetActive(false);
        Safe.Info($"Action wheel background drawn by the mod: {size} units (sprite {sp.width}x{sp.height}, icons {iconReach:F3}, arms reach {armReach:F3})");
    }

    static int back9Tries;

    static void Dividers(bool on)
    {
        // 11-10 user: our disc was always crooked → the game's own wheel background stays, nothing of ours is drawn
        if (backSprite != null && backSprite) backSprite.enabled = true;
        if (back9 != null && back9) back9.gameObject.SetActive(false);
        return;
#pragma warning disable CS0162
        if (on && back9 == null && backSprite != null && backSprite && back9Tries++ < 4)
            try { MakeBack9(backSprite); } catch (Exception e) { Safe.Log("Action wheel background", e); }
        if (backSprite == null || !backSprite || back9 == null || !back9) return;
        back9.gameObject.SetActive(on);
        backSprite.enabled = !on;
        if (on) back9.color = backSprite.color;
    }

#pragma warning restore CS0162
    static float Angle(Vector3 local) => Mathf.Atan2(local.x - center.x, local.y - center.y) * Mathf.Rad2Deg;   // clockwise from the top

    /// <summary>Where a sector is seen: its wedge (or icon) in the arms' parent space; the arm roots themselves may all sit at the centre.</summary>
    static float SeenAngle(RadialMenuArm a)
    {
        var t = a.transform;
        var vis = a.m_BG != null ? a.m_BG.transform : a.m_Sprite != null ? a.m_Sprite.transform : t;
        var p = t.parent != null ? t.parent.InverseTransformPoint(vis.position) : vis.position;
        return Angle(p);
    }

    /// <summary>Nine sectors (ours after the first aid one) or the game's eight. A sector is turned round the centre as a whole; its icon and
    /// labels are turned back so they stay upright; its wedge gets narrower.</summary>
    static void Nine(bool on)
    {
        if (ours == null || !ours || saved.Count < 8) return;
        if (!on)
        {
            if (!nine) { ours.SetActive(false); return; }
            foreach (var s in saved) Turn(s, 0f, 1f, s.T);
            ours.SetActive(false);
            Dividers(false);
            nine = false;
            hovered = false;
            wheel.Clear();
            return;
        }
        var order = saved.OrderBy(x => (x.Ang + 360f + 1f) % 360f).ToList();
        int after = order.FindIndex(x =>
        {
            var info = x.T.GetComponent<RadialMenuArm>()?.m_RadialInfo;
            var n = (info?.m_SpriteName ?? "") + " " + (info != null ? info.m_RadialElement.ToString() : "");
            return n.IndexOf("aid", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("medic", StringComparison.OrdinalIgnoreCase) >= 0;
        });
        if (after < 0) after = order.Count - 1;
        float start = order[0].Ang;
        float step = 360f / 9f;
        float shrink = 8f / 9f;
        wheel.Clear();
        int k = 0;
        for (int i = 0; i < order.Count; i++)
        {
            float ang = start + step * k;
            Turn(order[i], Mathf.DeltaAngle(order[i].Ang, ang), shrink, order[i].T);
            wheel.Add((order[i].T.GetComponent<RadialMenuArm>(), ang));
            k++;
            if (i == after)
            {
                oursAngle = start + step * k;
                ours.SetActive(true);
                Turn(oursSaved!, Mathf.DeltaAngle(oursSaved!.Ang, oursAngle), shrink, ours.transform);
                wheel.Add((null!, oursAngle));
                k++;
            }
        }
        nine = true;
        Dividers(true);
    }

    sealed class Saved
    {
        public Transform T = null!;
        public Vector3 Pos;
        public Quaternion Rot;
        public float Ang;
        public UISprite? Bg, Glow;
        public int BgW, GlowW;
        public readonly List<(string path, Quaternion rot)> Upright = new();
    }

    static Saved Save(RadialMenuArm a)
    {
        var t = a.transform;
        var s = new Saved { T = t, Pos = t.localPosition, Rot = t.localRotation, Ang = SeenAngle(a), Bg = a.m_BG, Glow = a.m_GlowSprite };
        s.BgW = a.m_BG != null ? a.m_BG.width : 0;
        s.GlowW = a.m_GlowSprite != null ? a.m_GlowSprite.width : 0;
        void Up(Component? c)
        {
            if (c == null || c.transform == t) return;
            s.Upright.Add((PathIn(t, c.transform), c.transform.localRotation));
        }
        Up(a.m_Sprite); Up(a.m_Texture); Up(a.m_StackLabel); Up(a.m_CenterLabel); Up(a.m_NotificationSprite);
        return s;
    }

    static string PathIn(Transform root, Transform t)
    {
        var parts = new List<string>();
        for (var x = t; x != null && x != root; x = x.parent) parts.Insert(0, x.name);
        return string.Join("/", parts);
    }

    /// <summary>The saved sector's pose turned by d degrees clockwise round the centre, applied to target (the sector itself or our copy).</summary>
    static void Turn(Saved s, float d, float shrink, Transform target)
    {
        var q = Quaternion.Euler(0f, 0f, -d);
        target.localPosition = center + q * (s.Pos - center);
        target.localRotation = q * s.Rot;
        foreach (var (path, rot) in s.Upright)
        {
            var c = target.Find(path);
            if (c != null) c.localRotation = Quaternion.Inverse(q) * rot;
        }
        var bg = target == s.T ? s.Bg : oursBg;
        var glow = target == s.T ? s.Glow : oursGlow;
        if (bg != null && s.BgW > 0) bg.width = Mathf.RoundToInt(s.BgW * shrink);
        if (glow != null && s.GlowW > 0) glow.width = Mathf.RoundToInt(s.GlowW * shrink);
    }

    /// <summary>The sector under the mouse among the nine: the game's own (its index) or ours (-1 to the game, handled here).</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Panel_ActionsRadial), nameof(Panel_ActionsRadial.MousePosToIndex))]
    static void Mouse(Panel_ActionsRadial __instance, ref int __result)
    {
        hovered = false;
        if (!nine || wheel.Count != 9 || ours == null || !ours) return;
        try
        {
            var arms = __instance.m_RadialArms;
            if (__result >= 0 && __result < arms.Length && arms[__result] != null && arms[__result].m_IsCenterButton) return;
            if (__result < 0) return;   // the centre / dead zone
            var cam = UICamera.mainCamera ?? UICamera.currentCamera;
            if (cam == null) return;
            var parent = ours.transform.parent;
            var c = cam.WorldToScreenPoint(parent != null ? parent.TransformPoint(center) : center);
            var m = Input.mousePosition - c;
            float ang = Mathf.Atan2(m.x, m.y) * Mathf.Rad2Deg;
            int best = 0;
            float bestD = 999f;
            for (int i = 0; i < wheel.Count; i++)
            {
                float dd = Mathf.Abs(Mathf.DeltaAngle(ang, wheel[i].angle));
                if (dd < bestD) { bestD = dd; best = i; }
            }
            var arm = wheel[best].arm;
            if (arm == null)
            {
                hovered = true;
                __result = -1;
                return;
            }
            for (int i = 0; i < arms.Length; i++)
                if (arms[i] != null && arms[i].Pointer == arm.Pointer) { __result = i; return; }
        }
        catch (Exception e) { Safe.Log("Tobacco wheel mouse", e); }
    }

    /// <summary>An item of ours picked in the game's item wheel: our own use only. The game's own use after it (when ours refused, e.g. a
    /// pipe without tobacco) made the pipe vanish from the pack.</summary>
    [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(Panel_ActionsRadial), nameof(Panel_ActionsRadial.UseItem))]
    static bool WheelUse(Panel_ActionsRadial __instance, GearItem gi)
    {
        if (!Defs.Is(gi, out var def) || def.Kind == Kind.Lighter) { Smoking.PickedInMenu(gi); return true; }
        try { Safe.Info("Item wheel: " + def.Id); Use.Do(gi, def); }
        catch (Exception e) { Safe.Log("Item wheel use " + def.Id, e); }
        try { __instance.Enable(false); } catch { }
        return false;
    }

    static float nextClick;

    /// <summary>Our sector's hover look, its name and the click.</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Panel_ActionsRadial), nameof(Panel_ActionsRadial.Update))]
    static void Tick(Panel_ActionsRadial __instance)
    {
        if (!nine || ours == null || !ours) return;
        try
        {
            if (back9 != null && back9 && backSprite != null && backSprite) back9.color = backSprite.color;
            if (oursBg != null) oursBg.color = hovered ? __instance.m_SectionColorSelected : __instance.m_SectionColorUnselected;
            if (oursGlow != null) oursGlow.enabled = hovered;
            if (hovered && __instance.m_SegmentLabel != null) __instance.m_SegmentLabel.text = Texts.Get("SMOKES_TAB").ToUpperInvariant();
            if (hovered && Input.GetMouseButtonDown(0) && Time.unscaledTime > nextClick)
            {
                nextClick = Time.unscaledTime + .3f;
                Open(__instance);
            }
        }
        catch (Exception e) { Safe.Log("Tobacco wheel tick", e); }
    }

    static void Open(Panel_ActionsRadial p)
    {
        var items = OurItems();
        Safe.Info($"Tobacco wheel opened: {items.Count} items");
        var list = new Il2CppSystem.Collections.Generic.List<GearItem>();
        foreach (var g in items) list.Add(g);
        p.ShowGearRadial(list, (Il2CppSystem.Action)new Action(() =>
        {
            try { HUDMessage.AddMessage(Texts.Get("SMOKES_TAB_EMPTY"), false, false); } catch { }
        }));
    }
}
