using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SnacksByEMPEROR;

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
        var bytes = FromPack(relativePath);
        if (bytes == null && !File.Exists(path))
        {
            MelonLogger.Error("Missing texture: " + path);
            return null;
        }
        bytes ??= File.ReadAllBytes(path);
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
        t.mipMapBias = compress ? -.5f : 0f;
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

    /// <summary>All art travels in a few files, assets1.pak, assets2.pak…: int count, then per file a name, an offset and a length; the bytes follow.
    /// A file missing from them is looked for loose in the folder.</summary>
    static Dictionary<string, (string file, long at, int size)>? pack;

    static byte[]? FromPack(string relativePath)
    {
        try
        {
            if (pack == null)
            {
                pack = new(StringComparer.OrdinalIgnoreCase);
                var paks = Directory.Exists(Mod.Root) ? Directory.GetFiles(Mod.Root, "assets*.pak") : Array.Empty<string>();
                foreach (var pak in paks)
                {
                    using var r = new BinaryReader(File.OpenRead(pak));
                    int n = r.ReadInt32();
                    for (int i = 0; i < n; i++)
                    {
                        string name = System.Text.Encoding.UTF8.GetString(r.ReadBytes(r.ReadUInt16()));
                        long at = r.ReadInt64(); int size = r.ReadInt32();
                        pack[name] = (pak, at, size);
                    }
                }
                MelonLogger.Msg($"Art packs: {paks.Length}, files: {pack.Count}");
            }
            if (!pack.TryGetValue(relativePath.Replace('\\', '/'), out var e)) return null;
            using var f = File.OpenRead(e.file);
            f.Seek(e.at, SeekOrigin.Begin);
            var bytes = new byte[e.size];
            int got = 0;
            while (got < e.size) { int k = f.Read(bytes, got, e.size - got); if (k <= 0) break; got += k; }
            return bytes;
        }
        catch (Exception ex) { Safe.Log("Pack " + relativePath, ex); return null; }
    }

    /// <summary>inside: every face also drawn from behind (the barrel is seen from inside when the camera comes close or it is looked into).</summary>
    public static Mesh? LoadMesh(string relativePath, bool inside = false)
    {
        if (meshes.TryGetValue(relativePath, out var known))
            return known;
        var path = Path.Combine(Mod.Root, relativePath);
        var packed = FromPack(relativePath);
        if (packed == null && !File.Exists(path))
        {
            MelonLogger.Error("Missing mesh: " + path);
            return null;
        }
        using var r = new BinaryReader(packed != null ? new MemoryStream(packed) : File.OpenRead(path));
        int nv = r.ReadInt32(), ni = r.ReadInt32();
        int fv = nv, fi = ni;
        if (inside) { nv *= 2; ni *= 2; }
        var v = new Il2CppStructArray<Vector3>(nv);
        var n = new Il2CppStructArray<Vector3>(nv);
        var u = new Il2CppStructArray<Vector2>(nv);
        for (int i = 0; i < fv; i++) v[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        for (int i = 0; i < fv; i++) n[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        for (int i = 0; i < fv; i++) u[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
        var t = new Il2CppStructArray<int>(ni);
        for (int i = 0; i < fi; i++) t[i] = r.ReadInt32();
        if (inside)
        {
            // the same faces turned inside out: own vertices with the normal reversed, so the inside is lit as an inside
            for (int i = 0; i < fv; i++)
            {
                var a = v[i]; var b = n[i];
                v[fv + i] = a;
                n[fv + i] = new Vector3(-b.x, -b.y, -b.z);
                u[fv + i] = u[i];
            }
            for (int i = 0; i + 2 < fi; i += 3)
            {
                t[fi + i] = t[i] + fv;
                t[fi + i + 1] = t[i + 2] + fv;
                t[fi + i + 2] = t[i + 1] + fv;
            }
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
}
