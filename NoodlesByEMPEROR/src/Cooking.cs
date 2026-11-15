using UnityEngine.AddressableAssets;

namespace NoodlesByEMPEROR;

/// <summary>The pot and our noodles.
/// 1. Handing out the cooked item. The game (disassembled 11-14: MaybeSpawnGearItemForInspectMode and PickUpCookedGearItem) makes it with
///    PlayerManager.InstantiateItemAtPlayersFeet(cookedPrefab.m_GearItemData.m_PrefabReference, units); that overload first asks the reference
///    RuntimeKeyIsValid(), ours is not a real addressable, so it returned null: no model in the inspect view and a NullReference in
///    AddItemToPlayerInventory on taking (0.3.0 and 0.5.0 tests). FeetPatch sends our references to the by-name overload, the rest is the game's own flow.
/// 2. Noodles seen in the pot while they cook: the pot's food surface is repainted with the finished bowl's broth and noodles.</summary>
[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.InstantiateItemAtPlayersFeet), new[] { typeof(AssetReferenceGearItem), typeof(int) })]
internal static class FeetPatch
{
    static bool Prefix(PlayerManager __instance, AssetReferenceGearItem assetReference, int numUnits, ref GearItem __result)
    {
        try
        {
            if (!Items.FromReference(assetReference, out var name)) return true;
            __result = __instance.InstantiateItemAtPlayersFeet(name, numUnits);
            Safe.Info($"Pot gives {name} x{numUnits}: {(__result != null ? "made" : "FAILED")}");
            return false;
        }
        catch (Exception e) { Safe.Log("Feet", e); return true; }
    }
}

[HarmonyPatch]
internal static class PotPatches
{
    static bool Ours(CookingPotItem pot, out Def raw, out Def hot)
    {
        raw = hot = null!;
        if (pot == null) return false;
        var g = pot.m_GearItemBeingCooked;
        if (g == null || !Defs.Is(g, out raw) || raw.Kind != Kind.Noodle) return false;
        hot = Defs.ById(raw.Brewed)!;
        return hot != null;
    }

    // ---------------------------------------------------------------- noodles seen in the pot

    sealed class Paint { public string Id = ""; public Material Liquid = null!; public GameObject Disc = null!; public MeshFilter Filter = null!; public Bounds Fit; public int Logged; }
    static readonly Dictionary<IntPtr, Paint> painted = new();
    static bool loggedVisual;

    /// <summary>Two layers, neither depends on the uv layout of the game's food surface (it differs: the can showed our picture centred,
    /// the pot and the skillet did not, 0.6.0 test; and the game keeps its own mesh there):
    /// a flat round disc of our own, a child of that surface, takes its place with pots/Id.jpg (broth with the noodles in the middle);
    /// the game's surface is not drawn meanwhile (forceRenderingOff) and has the plain broth colour in case it is.</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.UpdateVisuals))]
    static void AfterVisuals(CookingPotItem __instance)
    {
        try
        {
            painted.TryGetValue(__instance.Pointer, out var p);
            if (p != null && (p.Disc == null || p.Liquid == null)) { painted.Remove(__instance.Pointer); p = null; }
            MeshRenderer? r = null;
            bool show = Ours(__instance, out var raw, out var hot) && __instance.GetCookingState() != CookingPotItem.CookingState.Ruined && (r = Surface(__instance)) != null;
            if (!show)
            {
                if (p != null)
                {
                    if (p.Disc.activeSelf) p.Disc.SetActive(false);
                    var g = __instance.m_GrubMeshRenderer;
                    if (g != null && g.forceRenderingOff) g.forceRenderingOff = false;   // the game's own surface is the game's again
                }
                return;
            }
            if (p != null && p.Id != raw.Id)
            {
                UnityEngine.Object.Destroy(p.Disc); painted.Remove(__instance.Pointer); p = null;
            }
            if (p == null)
            {
                var mf = Filter(__instance, r!);
                var tex = Assets.Texture("pots/" + raw.Id + ".jpg", compress: true);
                if (tex == null || r.sharedMaterial == null || mf == null || mf.sharedMesh == null) return;
                if (mf.transform != r.transform) Safe.Info($"Pot visual: the surface's mesh is on {mf.name}, its renderer on {r.name}");
                tex.wrapMode = TextureWrapMode.Clamp;
                var liquid = Clean(r.sharedMaterial, Plain(raw.Broth));
                // The picture must NOT go on the liquid's shader: the disc was centred on the vessel to the millimetre (0.6.2 log: surface and pot
                // share one centre) and the picture on it still lay shifted, the same way as on the game's own surface in 0.6.0. That shader moves
                // the uv itself (flow / distortion maps, which we empty). So the disc takes the plain shader of our own pack, the item being cooked.
                Material? own = null;
                try
                {
                    var cooked = __instance.m_GearItemBeingCooked;
                    var cr = cooked != null ? cooked.GetComponentInChildren<MeshRenderer>(true) : null;
                    if (cr != null) own = cr.sharedMaterial;
                }
                catch (Exception e) { Safe.Log("Pot picture shader", e); }
                var top = Clean(own != null ? own : r.sharedMaterial, tex);
                if (own != null && top.HasProperty("_Color")) top.color = new Color(.74f, .74f, .74f, 1f);
                try { Safe.Info($"Pot visual: liquid shader maps: {string.Join(", ", r.sharedMaterial.GetTexturePropertyNames())}; picture on {top.shader?.name}"); } catch { }
                var go = new GameObject("NoodlesInPot");
                go.layer = r.gameObject.layer;
                go.transform.SetParent(mf.transform, false);
                var df = go.AddComponent<MeshFilter>();
                df.sharedMesh = Disc(mf.sharedMesh.bounds, mf.transform);
                var dr = go.AddComponent<MeshRenderer>();
                dr.sharedMaterial = top;
                dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                try
                {
                    foreach (var o in __instance.GetComponentsInChildren<MeshRenderer>(true))
                        if (o != null && o.gameObject != go)
                            Safe.Info($"Pot part {__instance.name}/{o.name}: centre {mf.transform.InverseTransformPoint(o.bounds.center)} size {o.bounds.size} on {o.enabled}");
                }
                catch (Exception e) { Safe.Log("Pot parts", e); }
                p = new Paint { Id = raw.Id, Liquid = liquid, Disc = go, Filter = df, Fit = mf.sharedMesh.bounds };
                painted[__instance.Pointer] = p;
                {
                    Safe.Info($"Pot visual: {raw.Id} on {r.name}, mesh {mf.sharedMesh.name}, shader {top.shader?.name}, bounds {mf.sharedMesh.bounds.center} / {mf.sharedMesh.bounds.size}, scale {r.transform.lossyScale}");
                }
            }
            // the game changes the surface's mesh as the cooking goes (0.6.1 test: the disc, fitted once to the first mesh, lay off centre): the disc follows it
            var nf = Filter(__instance, r!);
            var now = nf != null ? nf.sharedMesh : null;
            if (now != null && (now.bounds.center != p.Fit.center || now.bounds.size != p.Fit.size))
            {
                var old = p.Filter.sharedMesh;
                p.Fit = now.bounds;
                if (p.Disc.transform.parent != nf!.transform) p.Disc.transform.SetParent(nf.transform, false);
                p.Filter.sharedMesh = Disc(now.bounds, nf.transform);
                if (old != null) UnityEngine.Object.Destroy(old);
                if (p.Logged++ < 6) Safe.Info($"Pot visual: {raw.Id} refit to {now.name}, bounds {now.bounds.center} / {now.bounds.size}, at {nf.transform.localPosition} scale {nf.transform.lossyScale}");
            }
            // the game puts its own material back on every refresh: ours goes on again
            if (r!.sharedMaterial == null || r.sharedMaterial.Pointer != p.Liquid.Pointer) r.sharedMaterial = p.Liquid;
            if (!p.Disc.activeSelf) p.Disc.SetActive(true);
            // ours is the whole surface: the game's one is not drawn (its enabled flag stays the game's)
            if (!r.forceRenderingOff) r.forceRenderingOff = true;
        }
        catch (Exception e) { Safe.Log("Pot visuals", e); }
    }

    static MeshFilter? Filter(CookingPotItem pot, MeshRenderer r)
    {
        var f = pot.m_GrubMeshFilter;
        return f != null ? f : r.GetComponent<MeshFilter>();
    }

    static Material Clean(Material src, Texture tex)
    {
        var m = new Material(src);
        m.hideFlags = HideFlags.HideAndDontSave;
        m.mainTexture = tex;
        foreach (var n in m.GetTexturePropertyNames())
        {
            var t = m.GetTexture(n);
            if (t != null && t.Pointer != tex.Pointer) m.SetTexture(n, null);
            m.SetTextureScale(n, Vector2.one); m.SetTextureOffset(n, Vector2.zero);
        }
        if (m.HasProperty("_Color")) m.color = new Color(.8f, .8f, .8f, 1f);
        return m;
    }

    static readonly Dictionary<string, Texture2D> plain = new();

    /// <summary>A small one-colour texture, "#rrggbb".</summary>
    static Texture2D Plain(string hex)
    {
        if (plain.TryGetValue(hex, out var t) && t != null) return t;
        byte C(int i) => hex.Length >= 7 ? Convert.ToByte(hex.Substring(i, 2), 16) : (byte)160;
        var c = new Color32(C(1), C(3), C(5), 255);
        t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        t.hideFlags = HideFlags.HideAndDontSave;
        for (int x = 0; x < 4; x++) for (int y = 0; y < 4; y++) t.SetPixel(x, y, c);
        t.Apply(false, true);
        plain[hex] = t;
        return t;
    }

    /// <summary>A flat round disc in place of the game's food surface: same centre and width, its thin axis is "up", at the middle of its thickness,
    /// both faces, the whole picture on it. (0.6.1 had it a millimetre above the top of the game's surface, which is about 1 cm thick and not flat:
    /// the disc hung over the liquid and looked shifted from any side view.)</summary>
    static Mesh Disc(Bounds b, Transform tr)
    {
        var s = b.size; int up = s.x <= s.y && s.x <= s.z ? 0 : (s.y <= s.z ? 1 : 2);
        int a1 = (up + 1) % 3, a2 = (up + 2) % 3;
        float rad = Mathf.Min(s[a1], s[a2]) * .5f * .985f;
        var axis = Vector3.zero; axis[up] = 1f;
        float sign = Vector3.Dot(tr.TransformDirection(axis), Vector3.up) >= 0f ? 1f : -1f;
        float h = b.center[up];
        const int N = 48;
        var v = new Vector3[N + 1]; var uv = new Vector2[N + 1]; var nr = new Vector3[N + 1]; var col = new Color[N + 1]; var tan = new Vector4[N + 1];
        Vector3 P(float x, float y) { var q = b.center; q[up] = h; q[a1] += x * rad; q[a2] += y * rad; return q; }
        var t1 = Vector3.zero; t1[a1] = 1f;
        v[0] = P(0, 0); uv[0] = new Vector2(.5f, .5f);
        for (int i = 0; i < N; i++)
        {
            float an = i * Mathf.PI * 2f / N, x = Mathf.Cos(an), y = Mathf.Sin(an);
            v[i + 1] = P(x, y); uv[i + 1] = new Vector2(.5f + x * .5f, .5f + y * .5f);
        }
        for (int i = 0; i <= N; i++) { nr[i] = axis * sign; col[i] = Color.white; tan[i] = new Vector4(t1.x, t1.y, t1.z, 1f); }
        var tri = new int[N * 6];
        for (int i = 0; i < N; i++)
        {
            int p1 = 1 + i, p2 = 1 + (i + 1) % N;
            tri[i * 6] = 0; tri[i * 6 + 1] = p1; tri[i * 6 + 2] = p2;
            tri[i * 6 + 3] = 0; tri[i * 6 + 4] = p2; tri[i * 6 + 5] = p1;
        }
        var mesh = new Mesh { name = "NoodlesDisc" };
        mesh.hideFlags = HideFlags.HideAndDontSave;
        mesh.vertices = v; mesh.uv = uv; mesh.normals = nr; mesh.colors = col; mesh.tangents = tan; mesh.triangles = tri;
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>The part of the pot that shows what is cooking: the food surface if it is on, else the liquid.</summary>
    static MeshRenderer? Surface(CookingPotItem pot)
    {
        var g = pot.m_GrubMeshRenderer;
        if (g != null && g.enabled && g.gameObject.activeInHierarchy) return g;
        return null;
    }
}
