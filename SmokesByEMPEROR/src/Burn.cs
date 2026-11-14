namespace SmokesByEMPEROR;

/// <summary>A cigarette or cigar mesh cut at the burning point: every vertex past the cut (towards the lit end, +z) is put on the cut,
/// on its own line along the smoke (same angle round the axis), with the texture coordinate it would have there; so the filter and the band
/// keep their size while the tobacco part gets shorter. The lit-end discs keep their texture (they become the burnt end).</summary>
internal sealed class Burn
{
    readonly Mesh mesh;
    readonly Vector3[] pos;
    readonly Vector2[] uv;
    readonly int[][] down;     // for each vertex: its neighbours along the length towards the mouth (same angle), nearest first
    readonly Vector3[] work;
    readonly Vector2[] workUv;
    float lastZ = float.NaN;
    public readonly float MinZ, MaxZ;
    readonly float[] rad;      // 11-14: how thick the smoke is along its length, per millimetre from MinZ (tapered cigarillos: the ember follows it)
    const float Bin = .001f;

    // 11-14 (user: "the burning still breaks now and then... let it not be an outer ring but real burning"): the burning band is the
    // smoke's own side wall drawn once more, a hair outside, with the band's picture laid on the last BandLen before the cut. It is cut
    // with the smoke, so it can neither stand off the surface nor sink into it, whatever the shape
    public Mesh? Overlay { get; private set; }
    public float BandLen = .004f;
    /// <summary>Where the smoke is cut now (NaN before the first cut).</summary>
    public float CutZ => lastZ;
    const float Lift = .00006f;
    int[] ovIdx = Array.Empty<int>();
    Vector3[] ovPos = Array.Empty<Vector3>();
    Vector2[] ovUv = Array.Empty<Vector2>();
    Vector2[] ovOut = Array.Empty<Vector2>();
    float[] ovU = Array.Empty<float>();
    readonly bool[] body;      // the vertex belongs to a part that runs along the smoke (the wrapper), not to an end disc or a band
    readonly bool anyBody;
    readonly bool[] endPart;   // the vertex belongs to the lit end's own disc (a short part within 3 mm of the end)
    // 11-14: the cut follows the burning in steps too small to see (was .3 mm: "it burns in jerks"), so the arrays handed to the
    // mesh are kept and filled in place instead of made anew at every step
    Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>? ilPos, ilOvPos;
    Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector2>? ilUv, ilOvUv;
    static bool? fast;

    /// <summary>Whether a kept array can be filled in one copy (its data lies where expected: tried once on a small one).</summary>
    static unsafe bool Fast()
    {
        if (fast is { } known) return known;
        bool ok = false;
        try
        {
            var a = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(2);
            a[0] = new Vector3(1.5f, -2.25f, 3.125f);
            a[1] = new Vector3(7f, 8f, 9f);
            var f = (float*)IntPtr.Add(a.Pointer, 4 * IntPtr.Size);
            ok = sizeof(Vector3) == 12 && sizeof(Vector2) == 8 && a.Length == 2
                 && f[0] == 1.5f && f[1] == -2.25f && f[2] == 3.125f && f[3] == 7f && f[4] == 8f && f[5] == 9f;
        }
        catch { ok = false; }
        fast = ok;
        if (!ok) Safe.Info("Burn: arrays are copied the slow way");
        return ok;
    }

    static unsafe void Fill(Vector3[] src, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3> dst)
    {
        long bytes = (long)src.Length * sizeof(Vector3);
        fixed (Vector3* from = src) Buffer.MemoryCopy(from, (void*)IntPtr.Add(dst.Pointer, 4 * IntPtr.Size), bytes, bytes);
    }

    static unsafe void Fill(Vector2[] src, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector2> dst)
    {
        long bytes = (long)src.Length * sizeof(Vector2);
        fixed (Vector2* from = src) Buffer.MemoryCopy(from, (void*)IntPtr.Add(dst.Pointer, 4 * IntPtr.Size), bytes, bytes);
    }

    /// <summary>The smoke's radius at z (uncut shape).</summary>
    public float RadiusAt(float z)
    {
        if (rad.Length == 0) return 0f;
        float f = Mathf.Clamp((z - MinZ) / Bin, 0f, rad.Length - 1);
        int i = (int)f;
        int j = Math.Min(i + 1, rad.Length - 1);
        return Mathf.Lerp(rad[i], rad[j], f - i);
    }

    /// <summary>The largest radius between two places along the smoke.</summary>
    public float RadiusMax(float z0, float z1)
    {
        float m = 0f;
        for (float z = z0; z <= z1; z += Bin) m = Math.Max(m, RadiusAt(z));
        return m;
    }

    public void Drop()
    {
        try { if (Overlay != null) UnityEngine.Object.Destroy(Overlay); } catch { }
        Overlay = null;
    }

    /// <param name="doubled">the mesh is drawn on both sides (TwoSided): its second half repeats the first, turned round</param>
    public Burn(Mesh m, bool doubled = false, float bandLen = .004f)
    {
        BandLen = bandLen;
        mesh = m;
        pos = m.vertices.ToArray();
        uv = m.uv.ToArray();
        if (uv.Length != pos.Length) uv = new Vector2[pos.Length];
        work = (Vector3[])pos.Clone();
        workUv = (Vector2[])uv.Clone();
        MinZ = pos.Length > 0 ? pos.Min(p => p.z) : 0f;
        MaxZ = pos.Length > 0 ? pos.Max(p => p.z) : 0f;
        int bins = Math.Max(1, (int)Mathf.Ceil((MaxZ - MinZ) / Bin) + 1);
        rad = new float[bins];
        // side walls only: the rings of the end discs (normals along the axis) are not the smoke's thickness
        var nrm = m.normals;
        bool haveN = nrm != null && nrm.Length == pos.Length;
        for (int pass = 0; pass < 2; pass++)
        {
            bool any = false;
            for (int k = 0; k < pos.Length; k++)
            {
                var p = pos[k];
                if (pass == 0 && haveN && Mathf.Abs(nrm![k].z) > .7f) continue;
                int i = Mathf.Clamp((int)Mathf.Round((p.z - MinZ) / Bin), 0, bins - 1);
                rad[i] = Math.Max(rad[i], new Vector2(p.x, p.y).magnitude);
                any = true;
            }
            if (any) break;
        }
        // empty millimetres (long straight stretches have no vertices): from their neighbours
        int last = -1;
        for (int i = 0; i < bins; i++)
        {
            if (rad[i] <= 0f) continue;
            if (last < 0) for (int k = 0; k < i; k++) rad[k] = rad[i];
            else for (int k = last + 1; k < i; k++) rad[k] = Mathf.Lerp(rad[last], rad[i], (k - last) / (float)(i - last));
            last = i;
        }
        if (last >= 0) for (int k = last + 1; k < bins; k++) rad[k] = rad[last];
        var nb = new HashSet<int>[pos.Length];
        for (int i = 0; i < nb.Length; i++) nb[i] = new HashSet<int>();
        var tris = m.triangles;
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            int a = tris[t], b = tris[t + 1], c = tris[t + 2];
            nb[a].Add(b); nb[a].Add(c); nb[b].Add(a); nb[b].Add(c); nb[c].Add(a); nb[c].Add(b);
        }
        down = new int[pos.Length][];
        for (int i = 0; i < pos.Length; i++)
        {
            var p = pos[i];
            float r = new Vector2(p.x, p.y).magnitude;
            var list = new List<int>();
            foreach (var j in nb[i])
            {
                var q = pos[j];
                if (q.z >= p.z - 1e-5f) continue;
                float rq = new Vector2(q.x, q.y).magnitude;
                if (r < 1e-5f || rq < 1e-5f) continue;
                // same angle round the axis
                float dot = (p.x * q.x + p.y * q.y) / (r * rq);
                if (dot > .9995f) list.Add(j);
            }
            down[i] = list.OrderByDescending(j => pos[j].z).ToArray();
        }

        // the parts of the mesh (joined by triangles) and how far each runs along the smoke
        var par = new int[pos.Length];
        for (int i = 0; i < par.Length; i++) par[i] = i;
        int Find(int x)
        {
            while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; }
            return x;
        }
        for (int t = 0; t + 2 < tris.Length; t += 3)
        {
            int ra = Find(tris[t]);
            par[Find(tris[t + 1])] = ra;
            par[Find(tris[t + 2])] = ra;
        }
        var lo = new float[pos.Length];
        var hi = new float[pos.Length];
        for (int i = 0; i < pos.Length; i++) { lo[i] = float.MaxValue; hi[i] = float.MinValue; }
        for (int i = 0; i < pos.Length; i++)
        {
            int r = Find(i);
            lo[r] = Math.Min(lo[r], pos[i].z);
            hi[r] = Math.Max(hi[r], pos[i].z);
        }
        float need = (MaxZ - MinZ) * .3f;
        body = new bool[pos.Length];
        endPart = new bool[pos.Length];
        for (int i = 0; i < pos.Length; i++)
        {
            int r = Find(i);
            body[i] = hi[r] - lo[r] >= need;
            anyBody |= body[i];
            endPart[i] = !body[i] && lo[r] >= MaxZ - .003f;
        }
        try
        {
            // the side wall: triangles of the long parts that do not face along the axis (the first half only when the mesh is doubled)
            int end = doubled ? tris.Length / 2 : tris.Length;
            var map = new Dictionary<int, int>();
            var idx = new List<int>();
            var ot = new List<int>();
            for (int t = 0; t + 2 < end; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                if (!body[a]) continue;
                var fn = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]);
                float l = fn.magnitude;
                if (l < 1e-13f || Mathf.Abs(fn.z) / l > .85f) continue;
                for (int k = 0; k < 3; k++)
                {
                    int v = tris[t + k];
                    if (!map.TryGetValue(v, out int o)) { o = idx.Count; map[v] = o; idx.Add(v); }
                    ot.Add(o);
                }
            }
            if (ot.Count >= 18)
            {
                ovIdx = idx.ToArray();
                ovPos = new Vector3[ovIdx.Length];
                ovUv = new Vector2[ovIdx.Length];
                ovOut = new Vector2[ovIdx.Length];
                ovU = new float[ovIdx.Length];
                for (int k = 0; k < ovIdx.Length; k++)
                {
                    var p = pos[ovIdx[k]];
                    float r = new Vector2(p.x, p.y).magnitude;
                    ovOut[k] = r > 1e-5f ? new Vector2(p.x / r * Lift, p.y / r * Lift) : Vector2.zero;
                    // round the smoke and back (mirrored): no seam where the angle wraps
                    ovU[k] = r > 1e-5f ? Mathf.Abs(Mathf.Atan2(p.y, p.x)) / Mathf.PI : 0f;
                    ovPos[k] = new Vector3(p.x + ovOut[k].x, p.y + ovOut[k].y, p.z);
                    ovUv[k] = new Vector2(ovU[k], -1f);   // nothing of the band shows until the first cut
                }
                var om = new Mesh { name = m.name + "_burning", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                om.vertices = ovPos;
                om.uv = ovUv;
                om.triangles = ot.ToArray();
                om.RecalculateBounds();
                om.MarkDynamic();
                om.hideFlags = HideFlags.HideAndDontSave;
                Overlay = om;
            }
        }
        catch (Exception e) { Safe.Log("Burn overlay", e); Overlay = null; }
    }

    readonly List<(float a, float r)> cross = new();

    /// <summary>Where every line along the smoke crosses z (uncut shape), by angle round the axis, in <see cref="cross"/>; how many.
    /// 0 when the mesh gives too few lines there or a side is missing.</summary>
    int Crossings(float z)
    {
        cross.Clear();
        z = Mathf.Clamp(z, MinZ, MaxZ);
        for (int k = 0; k < pos.Length; k++)
        {
            if (down[k].Length == 0) continue;
            if (anyBody && !body[k]) continue;   // the wrapper only: an end disc's rim or a band over it is not the outline
            var pa = pos[k];
            if (pa.z < z) continue;
            var pb = pos[down[k][0]];
            if (pb.z > z) continue;
            float t = Mathf.Abs(pa.z - pb.z) > 1e-6f ? (z - pb.z) / (pa.z - pb.z) : 1f;
            var q = Vector3.Lerp(pb, pa, t);
            float m = new Vector2(q.x, q.y).magnitude;
            if (m < 1e-5f) continue;
            cross.Add((Mathf.Atan2(q.y, q.x), m));
        }
        if (cross.Count < 6) return 0;
        cross.Sort((x, y) => x.a.CompareTo(y.a));
        // the same line twice (the mesh is drawn on both sides): one crossing, the outer
        int w = 0;
        for (int i = 1; i < cross.Count; i++)
        {
            if (cross[i].a - cross[w].a < .01f) { if (cross[i].r > cross[w].r) cross[w] = (cross[w].a, cross[i].r); }
            else cross[++w] = cross[i];
        }
        int c = w + 1;
        if (c < 6) return 0;
        float gap = 0f;
        for (int i = 0; i < c; i++) gap = Math.Max(gap, i + 1 < c ? cross[i + 1].a - cross[i].a : cross[0].a + Mathf.PI * 2f - cross[i].a);
        if (gap > .7f) return 0;   // a side missing (a frayed foot: its lines end at different places)
        return c;
    }

    /// <summary>The smoke's real outline at z (uncut shape): the very points where its lines cross z, in order round the axis; these
    /// are the points the cut mesh ends in, so a cap through them closes the cut exactly. False when there is no whole outline at z.</summary>
    public bool Outline(float z, List<Vector2> pts)
    {
        pts.Clear();
        int c = Crossings(z);
        if (c < 6) return false;
        for (int i = 0; i < c; i++) pts.Add(new Vector2(Mathf.Cos(cross[i].a) * cross[i].r, Mathf.Sin(cross[i].a) * cross[i].r));
        return true;
    }

    /// <summary>The outline's radius in direction a (-π..π), between the crossings on either side; after Crossings gave c of them.</summary>
    float OutlineAt(float a, int c)
    {
        int hi = 0;
        while (hi < c && cross[hi].a < a) hi++;
        var p1 = cross[hi % c]; var p0 = cross[(hi + c - 1) % c];
        float a0 = p0.a, a1 = p1.a;
        if (hi == 0) a0 -= Mathf.PI * 2f;
        if (hi == c) a1 += Mathf.PI * 2f;
        float t = a1 - a0 > 1e-5f ? (a - a0) / (a1 - a0) : 0f;
        return Mathf.Lerp(p0.r, p1.r, Mathf.Clamp01(t));
    }

    /// <summary>A point of the lit end's own disc kept inside the outline (11-14, user: "past the burning of the Toskanello its brown
    /// tip shows, just a little": the disc is round, the leaf round it lumpy, so it stood out where the leaf is low).</summary>
    Vector3 Inside(Vector3 p, int c)
    {
        float r = new Vector2(p.x, p.y).magnitude;
        if (c < 6 || r < 1e-5f) return p;
        float lim = OutlineAt(Mathf.Atan2(p.y, p.x), c) * .985f;
        if (r <= lim) return p;
        float k = lim / r;
        return new Vector3(p.x * k, p.y * k, p.z);
    }

    /// <summary>The outline at z as the radius at n even directions round the axis, between the crossings on either side (for the
    /// outer band of a mesh that has no side wall of its own to draw the burning on).</summary>
    public bool Section(float z, float[] r)
    {
        int n = r.Length;
        int c = Crossings(z);
        if (c < 6) return false;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            if (a > Mathf.PI) a -= Mathf.PI * 2f;
            r[i] = OutlineAt(a, c);
        }
        return true;
    }

    /// <summary>Cuts the smoke at z (prop/model space): nothing of it past z.</summary>
    public void Cut(float z)
    {
        if (!float.IsNaN(lastZ) && Mathf.Abs(z - lastZ) < .00002f) return;
        lastZ = z;
        try
        {
            int oc = Crossings(z);   // the outline at the cut (0 when there is no whole one: a frayed foot)
            for (int i = 0; i < pos.Length; i++)
            {
                var p = pos[i];
                if (p.z <= z) { work[i] = endPart[i] ? Inside(p, oc) : p; workUv[i] = uv[i]; continue; }
                // walk towards the mouth along the smoke until below the cut
                int a = i, guard = 0;
                int b = -1;
                while (guard++ < 64)
                {
                    var d = down[a];
                    if (d.Length == 0) break;
                    int nxt = d[0];
                    if (pos[nxt].z <= z) { b = nxt; break; }
                    a = nxt;
                }
                if (b < 0)
                {
                    // a disc of the lit end (or the like): onto the cut, its own texture kept
                    // 11-14: as wide as the smoke is at the cut (a tapered one's end disc stood out past the body)
                    float r0 = RadiusAt(p.z), r1 = RadiusAt(z);
                    float sc = r0 > 1e-5f && r1 > 1e-5f ? Mathf.Clamp(r1 / r0, .4f, 1.6f) : 1f;
                    work[i] = Inside(new Vector3(p.x * sc, p.y * sc, z), oc);
                    workUv[i] = uv[i];
                    continue;
                }
                var pa = pos[a];
                var pb = pos[b];
                float k = Mathf.Abs(pa.z - pb.z) > 1e-6f ? (z - pb.z) / (pa.z - pb.z) : 0f;
                var at = Vector3.Lerp(pb, pa, k);
                work[i] = new Vector3(at.x, at.y, z);
                workUv[i] = Vector2.Lerp(uv[b], uv[a], k);
            }
            bool quick = Fast();
            if (quick)
            {
                ilPos ??= new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(work.Length);
                ilUv ??= new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector2>(workUv.Length);
                Fill(work, ilPos);
                Fill(workUv, ilUv);
                mesh.vertices = ilPos;
                mesh.uv = ilUv;
                mesh.uv2 = ilUv;
            }
            else
            {
                mesh.vertices = work;
                mesh.uv = workUv;
                mesh.uv2 = workUv;
            }
            mesh.RecalculateBounds();
            if (Overlay != null)
            {
                // the band's picture runs from v = 0 at BandLen before the cut to v = 1 at the cut; further back it is below 0 (clear)
                float z0 = z - BandLen;
                for (int k = 0; k < ovIdx.Length; k++)
                {
                    var w = work[ovIdx[k]];
                    var o = ovOut[k];
                    ovPos[k] = new Vector3(w.x + o.x, w.y + o.y, w.z);
                    ovUv[k] = new Vector2(ovU[k], (w.z - z0) / BandLen);
                }
                if (quick)
                {
                    ilOvPos ??= new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(ovPos.Length);
                    ilOvUv ??= new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector2>(ovUv.Length);
                    Fill(ovPos, ilOvPos);
                    Fill(ovUv, ilOvUv);
                    Overlay.vertices = ilOvPos;
                    Overlay.uv = ilOvUv;
                }
                else
                {
                    Overlay.vertices = ovPos;
                    Overlay.uv = ovUv;
                }
                Overlay.RecalculateBounds();
            }
        }
        catch (Exception e) { Safe.Log("Burn cut", e); }
    }
}
