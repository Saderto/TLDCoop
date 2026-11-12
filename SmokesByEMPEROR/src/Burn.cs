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

    public Burn(Mesh m)
    {
        mesh = m;
        pos = m.vertices.ToArray();
        uv = m.uv.ToArray();
        if (uv.Length != pos.Length) uv = new Vector2[pos.Length];
        work = (Vector3[])pos.Clone();
        workUv = (Vector2[])uv.Clone();
        MinZ = pos.Length > 0 ? pos.Min(p => p.z) : 0f;
        MaxZ = pos.Length > 0 ? pos.Max(p => p.z) : 0f;
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
    }

    /// <summary>Cuts the smoke at z (prop/model space): nothing of it past z.</summary>
    public void Cut(float z)
    {
        if (!float.IsNaN(lastZ) && Mathf.Abs(z - lastZ) < .0003f) return;
        lastZ = z;
        try
        {
            for (int i = 0; i < pos.Length; i++)
            {
                var p = pos[i];
                if (p.z <= z) { work[i] = p; workUv[i] = uv[i]; continue; }
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
                    work[i] = new Vector3(p.x, p.y, z);
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
            mesh.vertices = work;
            mesh.uv = workUv;
            mesh.uv2 = workUv;
            mesh.RecalculateBounds();
        }
        catch (Exception e) { Safe.Log("Burn cut", e); }
    }
}
