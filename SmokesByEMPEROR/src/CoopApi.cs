namespace SmokesByEMPEROR;

/// <summary>For the coop mod (CoopByEMPEROR), found by reflection; no hard link either way.
/// LocalPhase: what a friend should see of our smoking (0 in the hand, 1 the hand at the mouth, 2 a pipe in the teeth, hand away).
/// HeldLook: the coop mod has made a copy of one of our lit items for a figure's hand; it gets the held model (origin = the mouth end,
/// +Z toward the lit end) and the ember at the lit end; returns the model's length along Z, or 0 when it is not one of ours.
/// RemotePhase: a figure's phase changed (the breath out follows a drag).</summary>
public static class CoopApi
{
    public static int LocalPhase()
    {
        try { return Smoking.CoopPhase(); } catch { return 0; }
    }

    public static float HeldLook(GameObject copy, string prefab)
    {
        try
        {
            if (copy == null || prefab == null || !prefab.StartsWith(Defs.Prefix, StringComparison.Ordinal)) return 0f;
            var def = Defs.ById(prefab.Substring(Defs.Prefix.Length));
            if (def == null || def.Kind != Kind.Lit || def.HeldMesh == null) return 0f;
            var mesh = Assets.LoadMesh("meshes/" + def.HeldMesh + ".fpmesh");
            if (mesh == null) return 0f;
            foreach (var mf in copy.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.GetComponent<MeshRenderer>() == null) continue;
                mf.sharedMesh = mesh;
                if (mf.transform != copy.transform)
                {
                    mf.transform.localPosition = Vector3.zero;
                    mf.transform.localRotation = Quaternion.identity;
                    mf.transform.localScale = Vector3.one;
                }
            }
            var b = mesh.bounds;
            var tip = def.Smoke == SmokeType.Pipe ? new Vector3(b.center.x, b.max.y, b.max.z - .02f) : new Vector3(0f, 0f, b.max.z);
            foreach (var l in copy.GetComponentsInChildren<Light>(true))
                if (l != null && l.gameObject.name == Items.EmberName)
                {
                    var t = l.transform;
                    var holder = copy.GetComponentInChildren<MeshFilter>(true)?.transform ?? copy.transform;
                    t.position = holder.TransformPoint(tip);
                }
            Safe.Info($"Coop: {prefab} in a friend's hand as {def.HeldMesh}, length {b.max.z:0.000} m");
            return b.max.z;
        }
        catch (Exception e) { Safe.Log("Coop held look", e); return 0f; }
    }

    /// <summary>The figure holding this copy is a woman (true) or a man: its breath sounds follow.</summary>
    public static void RemoteVoice(GameObject copy, bool female)
    {
        try { if (copy != null) Visual.RemoteFemale[copy.Pointer] = female; } catch { }
    }

    public static void RemotePhase(GameObject copy, int phase)
    {
        try { Visual.RemotePhase(copy, phase); } catch { }
    }
}
