using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SmokesByEMPEROR;

/// <summary>11-13 (user: white filters should go brown as the cigarette is smoked, "only the place the breath comes from, the circle at
/// the filter on the smoker's side"): the held cigarette gets its own readable copy of its texture, and only the mouth end disc of it
/// (the 128 x 128 block at the bottom left: image rows 896.., x 0..128) is stained by the share smoked: tan from the middle outwards, brown
/// in the middle by the end. The paper outside is not touched. A dark end (black acetate, bare tobacco) is left alone.</summary>
internal static class FilterStain
{
    const int W = 128;
    static Texture2D? tex;
    static Color[]? clean;
    static Il2CppStructArray<Color>? work;
    static float[]? noise;
    static int step = -1;

    public static Texture2D? Make(string relativePath)
    {
        Clear();
        try
        {
            var path = Path.Combine(Mod.Root, relativePath);
            if (!File.Exists(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGB24, true);
            ImageConversion.LoadImage(t, File.ReadAllBytes(path), false);
            if (t.width < W * 2 || t.height < W * 2) { UnityEngine.Object.Destroy(t); return null; }
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 8;
            t.mipMapBias = -.5f;
            t.hideFlags = HideFlags.HideAndDontSave;
            var px = t.GetPixels(0, 0, W, W);
            var c = new Color[W * W];
            float bright = 0f;
            int n = 0;
            for (int i = 0; i < c.Length; i++)
            {
                c[i] = px[i];
                int x = i % W - W / 2, y = i / W - W / 2;
                if (x * x + y * y < 40 * 40) { bright += Mathf.Min(c[i].r, Mathf.Min(c[i].g, c[i].b)); n++; }
            }
            if (n == 0 || bright / n < .6f) { UnityEngine.Object.Destroy(t); return null; }
            noise ??= MakeNoise();
            clean = c;
            work = new Il2CppStructArray<Color>(W * W);
            tex = t;
            step = -1;
            return t;
        }
        catch (Exception e)
        {
            Safe.Log("Filter stain texture", e);
            Clear();
            return null;
        }
    }

    static float[] MakeNoise()
    {
        var a = new float[W * W];
        for (int y = 0; y < W; y++)
            for (int x = 0; x < W; x++)
                a[y * W + x] = Mathf.PerlinNoise(x * .07f + 3.1f, y * .07f + 7.7f) * .7f + Mathf.PerlinNoise(x * .31f + 11f, y * .31f + 5f) * .3f - .5f;
        return a;
    }

    public static void Set(float share)
    {
        if (tex == null || clean == null || work == null || noise == null) return;
        int s = Mathf.RoundToInt(Mathf.Clamp01(share) * 120f);
        if (s == step) return;
        step = s;
        try
        {
            float k = s / 120f * Mathf.Clamp(Settings.FilterStain.Value, 0f, 2f);
            var tan = new Color(.84f, .64f, .36f);
            var brown = new Color(.55f, .36f, .17f);
            const float R = 61f;
            for (int y = 0; y < W; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    float dx = x - 63.5f, dy = y - 63.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / R;
                    // 11-13 (user: it showed only by drag 12-14, should begin after about 5): the stain covers the disc early (0.1.134, slower: the middle
                    // from the second or third drag, all of it by about three quarters), and gets deeper, tan to brown, all the way; nothing on the paper rim
                    float cover = Mathf.Clamp01((k - .1f) * 2.6f - d * .7f + noise[i] * .3f * Mathf.Min(1f, k * 3f)) * Mathf.Clamp01((.96f - d) * 12f);
                    var deep = Color.Lerp(tan, brown, Mathf.Clamp01(k * 1.1f) * (1f - .35f * d));
                    var m = Color.Lerp(Color.white, deep, cover);
                    var c = clean[i];
                    work[i] = new Color(c.r * m.r, c.g * m.g, c.b * m.b, 1f);
                }
            tex.SetPixels(0, 0, W, W, work);
            tex.Apply(true, false);
        }
        catch (Exception e)
        {
            Safe.Log("Filter stain", e);
        }
    }

    public static void Clear()
    {
        try
        {
            if (tex != null) UnityEngine.Object.Destroy(tex);
        }
        catch { }
        tex = null;
        clean = null;
        work = null;
        step = -1;
    }
}
