using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SmokesByEMPEROR;

/// <summary>Textures from PNG and meshes from the .fpmesh files written by tools/obj_to_fpmesh.py. Each is loaded once.</summary>
public static class Assets
{
    static readonly Dictionary<string, Texture2D> textures = new();
    static readonly Dictionary<string, Mesh> meshes = new();

    /// <summary>compress: an item's surface, made a GPU-compressed texture (DXT1, or DXT5 where the PNG has alpha) with no copy kept in memory. Uncompressed and readable it took about 4 to 8 times the video memory,
    /// and the game then lowered its own textures' detail to stay in budget. Icons stay as they were: the interface draws them.</summary>
    /// <param name="mipmaps">False for an atlas of small tiles (the cookware's): its smaller copies run the tiles into each other, a drink's pale water
    /// coming out red and orange at its edge in hard-edged bands.</param>
    public static Texture2D? Texture(string relativePath, bool compress = true, bool mipmaps = true)
    {
        if (textures.TryGetValue(relativePath, out var known))
            return known;
        var path = Path.Combine(Mod.Root, relativePath);
        if (!File.Exists(path))
        {
            MelonLogger.Error("Missing texture: " + path);
            return null;
        }
        var bytes = File.ReadAllBytes(path);
        // The PNG's colour type: 2 is RGB without alpha; 4, 6 and a palette may have it.
        bool alpha = bytes.Length <= 25 || bytes[25] != 2;
        var t = new Texture2D(2, 2, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, mipmaps);
        ImageConversion.LoadImage(t, bytes, false);
        if (compress && !alpha && t.format != TextureFormat.RGB24)
        {
            // Loaded with alpha after all: as RGB it compresses to DXT1, half of DXT5.
            var rgb = new Texture2D(t.width, t.height, TextureFormat.RGB24, mipmaps);
            rgb.SetPixels32(t.GetPixels32());
            rgb.Apply(mipmaps);
            UnityEngine.Object.Destroy(t);
            t = rgb;
        }
        t.name = Path.GetFileNameWithoutExtension(path);
        t.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
        t.anisoLevel = 8;
        t.mipMapBias = -.5f;
        t.hideFlags = HideFlags.HideAndDontSave;
        if (compress)
        {
            try
            {
                t.Compress(true);
                t.Apply(false, true);
            }
            catch (Exception e)
            {
                Safe.Log("Compressing " + relativePath, e);
            }
        }
        if (textures.Count == 0) MelonLogger.Msg($"Texture {t.name}: {t.width}x{t.height} {t.format}, readable {t.isReadable}");
        return textures[relativePath] = t;
    }

    public static Mesh? LoadMesh(string relativePath)
    {
        if (meshes.TryGetValue(relativePath, out var known))
            return known;
        var path = Path.Combine(Mod.Root, relativePath);
        if (!File.Exists(path))
        {
            MelonLogger.Error("Missing mesh: " + path);
            return null;
        }
        using var r = new BinaryReader(File.OpenRead(path));
        int nv = r.ReadInt32(), ni = r.ReadInt32();
        var v = new Il2CppStructArray<Vector3>(nv);
        var n = new Il2CppStructArray<Vector3>(nv);
        var u = new Il2CppStructArray<Vector2>(nv);
        for (int i = 0; i < nv; i++) v[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        for (int i = 0; i < nv; i++) n[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        for (int i = 0; i < nv; i++) u[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
        var t = new Il2CppStructArray<int>(ni);
        for (int i = 0; i < ni; i++) t[i] = r.ReadInt32();
        // 11-13 (user: a dot or hole in the filter of nearly every cigarette): the end discs of the cigarette meshes stop 0.5 mm short of
        // the axis, which left a 1 mm hole in both ends. The innermost ring is drawn together onto the axis
        if (Path.GetFileName(path).StartsWith("cig_", StringComparison.OrdinalIgnoreCase) && nv > 0)
        {
            Vector3 lo = v[0], hi = v[0];
            for (int i = 1; i < nv; i++) { lo = Vector3.Min(lo, v[i]); hi = Vector3.Max(hi, v[i]); }
            var size = hi - lo; var mid = (lo + hi) * .5f;
            int ax = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            for (int i = 0; i < nv; i++)
            {
                var d = v[i] - mid; d[ax] = 0f;
                if (d.sqrMagnitude < .0008f * .0008f) { var q = v[i]; q -= d; v[i] = q; }
            }
        }
        // 11-14 (user: "the cigars have a see-through texture at the tip"): in every cigar mesh the foot (the end that is lit) is a part of
        // its own whose faces look into the cigar, so from outside it was not drawn and one saw into the hollow body. A flat part at
        // an end of the mesh that faces the middle is turned round here (so it needs no new mesh files)
        if (Path.GetFileName(path).StartsWith("cigar", StringComparison.OrdinalIgnoreCase) && nv > 0 && ni >= 3)
        {
            try { FaceEndsOut(v, n, t, nv, ni); }
            catch (Exception e) { MelonLogger.Warning("Mesh ends " + path + ": " + e.Message); }
        }
        var m = new Mesh { name = Path.GetFileNameWithoutExtension(path), indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.vertices = v;
        m.normals = n;
        m.uv = u;
        // every channel a shader might read, with plain values (a cooking material can read the second uv set or the vertex colours)
        m.uv2 = u;
        var white = new Il2CppStructArray<Color32>(nv);
        for (int i = 0; i < nv; i++) white[i] = new Color32(255, 255, 255, 255);
        m.colors32 = white;
        m.triangles = t;
        m.RecalculateBounds();
        m.hideFlags = HideFlags.HideAndDontSave;
        return meshes[relativePath] = m;
    }

    /// <summary>Turns round the flat parts at the ends of the mesh's long axis that face its middle (triangle order and normals).</summary>
    static void FaceEndsOut(Il2CppStructArray<Vector3> v, Il2CppStructArray<Vector3> n, Il2CppStructArray<int> t, int nv, int ni)
    {
        var pos = new Vector3[nv];
        for (int i = 0; i < nv; i++) pos[i] = v[i];
        var tri = new int[ni];
        for (int i = 0; i < ni; i++) tri[i] = t[i];
        Vector3 lo = pos[0], hi = pos[0];
        for (int i = 1; i < nv; i++) { lo = Vector3.Min(lo, pos[i]); hi = Vector3.Max(hi, pos[i]); }
        var size = hi - lo; var mid = (lo + hi) * .5f;
        int ax = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        var par = new int[nv];
        for (int i = 0; i < nv; i++) par[i] = i;
        int Find(int x)
        {
            while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; }
            return x;
        }
        for (int i = 0; i + 2 < ni; i += 3)
        {
            int ra = Find(tri[i]);
            par[Find(tri[i + 1])] = ra;
            par[Find(tri[i + 2])] = ra;
        }
        // per part: the sum of its faces (area × direction), its area, where it is
        var sum = new Dictionary<int, (Vector3 n, float area, Vector3 at, int count)>();
        for (int i = 0; i + 2 < ni; i += 3)
        {
            Vector3 a = pos[tri[i]], b = pos[tri[i + 1]], c = pos[tri[i + 2]];
            var fn = Vector3.Cross(b - a, c - a) * .5f;
            int r = Find(tri[i]);
            sum.TryGetValue(r, out var s);
            sum[r] = (s.n + fn, s.area + fn.magnitude, s.at + a + b + c, s.count + 3);
        }
        var turn = new HashSet<int>();
        foreach (var kv in sum)
        {
            var s = kv.Value;
            float nl = s.n.magnitude;
            if (s.area < 1e-12f || nl / s.area < .8f) continue;          // not flat
            float off = (s.at / s.count - mid)[ax];
            if (Mathf.Abs(off) < .4f * size[ax]) continue;                // not at an end
            if (s.n[ax] / nl * Mathf.Sign(off) > -.8f) continue;          // faces out already
            turn.Add(kv.Key);
        }
        if (turn.Count == 0) return;
        for (int i = 0; i + 2 < ni; i += 3)
        {
            if (!turn.Contains(Find(tri[i]))) continue;
            (t[i + 1], t[i + 2]) = (tri[i + 2], tri[i + 1]);
        }
        for (int i = 0; i < nv; i++)
            if (turn.Contains(Find(i))) n[i] = -n[i];
    }
}
