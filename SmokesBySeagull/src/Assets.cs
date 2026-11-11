using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SmokesBySeagull;

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
