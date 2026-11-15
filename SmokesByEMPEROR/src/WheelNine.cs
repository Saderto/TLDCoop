namespace SmokesByEMPEROR;

/// <summary>The action wheel's background picture with nine sector lines in place of eight (11-15). The game's own picture (a frame of
/// its opening animation: fog, a ring, eight lines) is re-laid by angle: each of the nine 40 degree sectors shows one of the game's
/// 45 degree sectors squeezed sideways, so every line stays the game's own line and meets its neighbour at the seam; the ninth sector
/// is a soft blend of the two it sits between. Checked outside the game on the picture saved by 0.1.160 before it was ever shown.</summary>
public static class WheelNine
{
    /// <summary>src: RGBA bytes of the frame as cut from the atlas, w x h, rows bottom-up; the frame sits in a full x full picture
    /// at padL from the left and padB from the bottom. Returns n x n RGBA, rows bottom-up. Sector middles at first, first + 40, ... degrees clockwise from the top.</summary>
    public static byte[] Remap(byte[] src, int w, int h, int padL, int padB, int full, int n, float first = 0f)
    {
        var dst = new byte[n * n * 4];
        float c = n * .5f, cf = full * .5f, scale = (float)full / n;
        // nothing of the frame lies farther from the middle than its farthest corner
        float far = 0f;
        foreach (var (x, y) in new[] { (padL, padB), (padL + w, padB), (padL, padB + h), (padL + w, padB + h) })
            far = Math.Max(far, MathF.Sqrt((x - cf) * (x - cf) + (y - cf) * (y - cf)));
        float farOut = far / scale + 1f;
        const float deg = 180f / MathF.PI, rad = MathF.PI / 180f;
        Span<float> px = stackalloc float[4];
        Span<float> p2 = stackalloc float[4];
        for (int oy = 0; oy < n; oy++)
        {
            float dy = oy + .5f - c;
            for (int ox = 0; ox < n; ox++)
            {
                float dx = ox + .5f - c;
                float r = MathF.Sqrt(dx * dx + dy * dy);
                if (r > farOut) continue;
                float th = MathF.Atan2(dx, dy) * deg;              // clockwise from the top
                th = ((th - first) % 360f + 360f) % 360f;
                float t = (th + 20f) % 360f;
                int k = (int)(t / 40f);
                if (k > 8) k = 8;
                float off = t - k * 40f - 20f;                     // -20..20 inside the sector
                float rf = r * scale;
                float a = off * (22.5f / 20f);
                if (k < 8) Sample(src, w, h, padL, padB, cf, rf, (45f * k + a) * rad, px);
                else
                {
                    Sample(src, w, h, padL, padB, cf, rf, a * rad, px);
                    Sample(src, w, h, padL, padB, cf, rf, (315f + a) * rad, p2);
                    float m = (off + 20f) / 40f;
                    m = m * m * (3f - 2f * m);
                    for (int i = 0; i < 4; i++) px[i] = px[i] * (1f - m) + p2[i] * m;
                }
                float al = px[3];
                if (al <= 0f) continue;
                int o = (oy * n + ox) * 4;
                dst[o] = (byte)Math.Clamp(px[0] * 255f / al + .5f, 0f, 255f);
                dst[o + 1] = (byte)Math.Clamp(px[1] * 255f / al + .5f, 0f, 255f);
                dst[o + 2] = (byte)Math.Clamp(px[2] * 255f / al + .5f, 0f, 255f);
                dst[o + 3] = (byte)Math.Clamp(al + .5f, 0f, 255f);
            }
        }
        return dst;
    }

    /// <summary>The frame at radius rf and angle phi (clockwise from the top) of the full picture: premultiplied colour, bilinear.</summary>
    static void Sample(byte[] src, int w, int h, int padL, int padB, float cf, float rf, float phi, Span<float> o)
    {
        float x = cf + rf * MathF.Sin(phi) - padL - .5f, y = cf + rf * MathF.Cos(phi) - padB - .5f;
        o[0] = o[1] = o[2] = o[3] = 0f;
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0;
        for (int j = 0; j < 2; j++)
            for (int i = 0; i < 2; i++)
            {
                int xi = x0 + i, yi = y0 + j;
                if (xi < 0 || yi < 0 || xi >= w || yi >= h) continue;
                float wgt = (i == 0 ? 1f - fx : fx) * (j == 0 ? 1f - fy : fy);
                int s = (yi * w + xi) * 4;
                float al = src[s + 3] * wgt;
                o[0] += src[s] * al / 255f; o[1] += src[s + 1] * al / 255f; o[2] += src[s + 2] * al / 255f; o[3] += al;
            }
    }

    // ---- in the game ----
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (byte[] px, int n)> ready = new();
    static readonly Dictionary<string, Texture2D> textures = new();
    static bool prepared;
    static string last = "";
    public static int Frames => ready.Count;

    /// <summary>Once: every frame of the background's animation is cut from the atlas here (main thread) and re-laid on worker threads;
    /// a frame that is not ready yet is simply shown the game's way.</summary>
    public static void Prepare(UISprite sp, float first)
    {
        if (prepared) return;
        prepared = true;
        try
        {
            var atlas = sp.atlas;
            var tex = sp.mainTexture;
            if (atlas == null || tex == null) { Safe.Info("Wheel nine: no atlas or texture"); return; }
            string name = sp.spriteName ?? "";
            int cut = name.LastIndexOf('_');
            string prefix = cut > 0 ? name.Substring(0, cut + 1) : name;
            var frames = new List<(string name, int x, int y, int w, int h, int pl, int pb, int full)>();
            foreach (var d in atlas.spriteList)
            {
                if (d == null || d.name == null || !d.name.StartsWith(prefix, StringComparison.Ordinal) || d.width <= 0 || d.height <= 0) continue;
                int fw = d.paddingLeft + d.width + d.paddingRight, fh = d.paddingBottom + d.height + d.paddingTop;
                if (fw != fh) { Safe.Info($"Wheel nine: frame {d.name} is not square ({fw}x{fh}), left as it is"); continue; }
                frames.Add((d.name, d.x, tex.height - d.y - d.height, d.width, d.height, d.paddingLeft, d.paddingBottom, fw));
            }
            if (frames.Count == 0) { Safe.Info($"Wheel nine: no frames named {prefix}* in {atlas.name}"); return; }
            frames.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            last = frames[^1].name;
            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var jobs = new List<(string name, byte[] src, int w, int h, int pl, int pb, int full)>();
            int big = 0;
            foreach (var f in frames) big = Math.Max(big, f.full);
            foreach (var f in frames)
            {
                var img = new Texture2D(f.w, f.h, TextureFormat.RGBA32, false);
                img.ReadPixels(new Rect(f.x, f.y, f.w, f.h), 0, 0);
                img.Apply();
                byte[] bytes = img.GetRawTextureData();
                UnityEngine.Object.Destroy(img);
                if (bytes.Length != f.w * f.h * 4) { Safe.Info($"Wheel nine: frame {f.name} read {bytes.Length} bytes for {f.w}x{f.h}"); continue; }
                jobs.Add((f.name, bytes, f.w, f.h, f.pl, f.pb, f.full));
            }
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Safe.Info($"Wheel nine: {jobs.Count} frames {prefix}* cut from {atlas.name}, the last is {last}, sectors from {first:F1} deg, out {big} px");
            // the still picture first and sharp, the moving ones at half size (each is on screen for a thirtieth of a second)
            jobs.Reverse();
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    System.Threading.Tasks.Parallel.ForEach(jobs, j =>
                    {
                        int n = Math.Max(j.full, big);   // the moving frames lie in the atlas at half the size of the still one: re-laid straight at the still one's size, so no sharpness is lost twice (user 11-15: soft for the first second)
                        ready[j.name] = (Remap(j.src, j.w, j.h, j.pl, j.pb, j.full, n, first), n);
                    });
                }
                catch (Exception e) { MelonLoader.MelonLogger.Error("Wheel nine frames: " + e); }
            });
        }
        catch (Exception e) { Safe.Log("Wheel nine prepare", e); }
    }

    /// <summary>The nine-line picture for a frame of the game's animation, or null while it is not ready.</summary>
    public static Texture2D? For(string? frame)
    {
        if (string.IsNullOrEmpty(frame)) return null;
        if (textures.TryGetValue(frame, out var t) && t != null) return t;
        if (!ready.TryGetValue(frame, out var r)) return null;
        try
        {
            t = new Texture2D(r.n, r.n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            t.LoadRawTextureData((Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>)r.px);
            t.Apply(false, true);
            textures[frame] = t;
            ready.TryRemove(frame, out _);
            return t;
        }
        catch (Exception e) { Safe.Log("Wheel nine texture", e); return null; }
    }
}
