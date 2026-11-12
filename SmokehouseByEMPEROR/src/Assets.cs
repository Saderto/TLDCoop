using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SmokehouseByEMPEROR;

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

    static Dictionary<string, (string File, long At, int Size)>? pack;
    const string PackName = "sausages*.pak";

    /// <summary>The sausages' textures and icons come in one file (0.4.1: 144 small files became one): a count, then name, size and bytes of each.
    /// What the pack holds wins over a loose file of the same name.</summary>
    static byte[]? FromPack(string relativePath)
    {
        try
        {
            if (pack == null)
            {
                pack = new(StringComparer.OrdinalIgnoreCase);
                foreach (var file in Directory.GetFiles(Mod.Root, PackName))
                {
                    using var r = new BinaryReader(File.OpenRead(file));
                    int n = r.ReadInt32();
                    for (int i = 0; i < n; i++)
                    {
                        string name = r.ReadString();
                        int size = r.ReadInt32();
                        pack[name] = (file, r.BaseStream.Position, size);
                        r.BaseStream.Seek(size, SeekOrigin.Current);
                    }
                }
                MelonLogger.Msg($"Smokehouse pack: {pack.Count} files");
            }
            if (!pack.TryGetValue(relativePath.Replace('\\', '/'), out var e)) return null;
            using var f = File.OpenRead(e.File);
            f.Seek(e.At, SeekOrigin.Begin);
            var bytes = new byte[e.Size];
            int got = 0;
            while (got < e.Size) { int k = f.Read(bytes, got, e.Size - got); if (k <= 0) break; got += k; }
            return got == e.Size ? bytes : null;
        }
        catch (Exception e) { Safe.Log("Pack " + relativePath, e); return null; }
    }

    /// <summary>inside: every face also drawn from behind (the barrel is seen from inside when the camera comes close or it is looked into).</summary>
    public static Mesh? LoadMesh(string relativePath, bool inside = false)
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
