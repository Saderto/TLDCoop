using System.Collections.Generic;
namespace SmokesByEMPEROR;

/// <summary>The right hand while smoking: gloves off (bare hands drawn), index and middle fingers opened from the torch fist so a
/// cigarette or cigar sits between them. Run after the game's animation each frame; everything is put back when the smoke ends.</summary>
public static class Hands
{
    static readonly List<GameObject> hiddenGloves = new();
    static GameObject? shownHands;
    static bool loggedClothing;

    /// <summary>Gloves and mittens hidden, the bare hands shown; sleeves stay.</summary>
    public static void Bare()
    {
        try
        {
            var pa = GameManager.GetPlayerAnimationComponent();
            if (pa == null) return;
            var worn = pa.m_ActiveHandsClothing;
            if (worn != null)
            {
                if (!loggedClothing)
                {
                    loggedClothing = true;
                    var names = new List<string>();
                    foreach (var go in worn) if (go != null) names.Add(go.name + (go.activeSelf ? "" : " (off)"));
                    Safe.Info($"Hand clothing: {string.Join(", ", names)}; hands mesh {pa.m_HandsMesh?.name ?? "none"} active {pa.m_HandsMesh?.activeSelf}");
                }
                foreach (var go in worn)
                {
                    if (go == null || !go.activeSelf || !IsGlove(go.name)) continue;
                    go.SetActive(false);
                    hiddenGloves.Add(go);
                }
            }
            // the bare arms mesh must really draw before the mittens go: in 0.1.10 hiding them left no hand at all
            bool bareOk = false;
            // the arms mesh the finger bones drive (found under the rig): switched on if it is off, used only if it is a real mesh
            var bare = BareMesh;
            // the game switched the bare arms off again since the last frame: it hides them itself, so the mittens stay
            if (bare != null && bareTried && !bareFought && (!bare.enabled || !bare.gameObject.activeInHierarchy))
            {
                bareFought = true;
                Safe.Info("Bare hands: the game hides them again every frame; mittens kept");
                foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
                hiddenRenderers.Clear();
            }
            if (bareFought) bare = null;
            if (bare != null && bare.sharedMesh != null)
            {
                bareTried = true;
                var go = bare.gameObject;
                for (var t = go.transform; t != null && t.name != "NEW_FPHand_Rig"; t = t.parent)
                    if (!t.gameObject.activeSelf) { t.gameObject.SetActive(true); activatedBare.Add(t.gameObject); }
                if (!bare.enabled) { bare.enabled = true; enabledBare.Add(bare); }
                bareOk = bare.sharedMesh.vertexCount > 300;
                if (!loggedRenderers) rendererNames.Add($"bare {Path(bare.transform)} verts {bare.sharedMesh.vertexCount} layer {go.layer}");
            }
            if (pa.m_HandsMesh != null && !loggedRenderers) rendererNames.Add($"hands mesh {Path(pa.m_HandsMesh.transform)}");
            if (Wrist != null)
            {
                var root = Wrist;
                while (root.parent != null && root.name != "NEW_FPHand_Rig") root = root.parent;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    bool on = r.enabled && r.gameObject.activeInHierarchy;
                    if (!loggedRenderers && on) rendererNames.Add($"{Path(r.transform)}{(r is SkinnedMeshRenderer sm && sm.sharedMesh != null ? " verts " + sm.sharedMesh.vertexCount : "")} layer {r.gameObject.layer}");
                    if (!bareOk || !on || !IsGloveRenderer(r.name)) continue;
                    r.enabled = false;
                    hiddenRenderers.Add(r);
                }
            }
            if (!loggedRenderers)
            {
                loggedRenderers = true;
                Safe.Info($"Arms drawn: {string.Join("; ", rendererNames)}; bare hands {(bareOk ? "shown, mittens hidden" : "not usable, mittens kept")}");
                rendererNames.Clear();
            }
        }
        catch (Exception e)
        {
            Safe.Log("Bare hands", e);
        }
    }

    static readonly List<Renderer> hiddenRenderers = new();
    static readonly List<Renderer> enabledBare = new();
    static readonly List<GameObject> activatedBare = new();
    static SkinnedMeshRenderer? BareMesh;
    static bool bareTried, bareFought;

    static string Path(Transform t)
    {
        var n = t.name;
        for (var p = t.parent; p != null && n.Length < 160; p = p.parent) { n = p.name + "/" + n; if (p.name == "NEW_FPHand_Rig") break; }
        return n;
    }
    static readonly List<string> rendererNames = new();
    static bool loggedRenderers;

    static bool IsGloveRenderer(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("glove") || n.Contains("mitt") || n.Contains("gauntlet");
    }

    static bool IsGlove(string n)
    {
        n = n.ToLowerInvariant();
        return n.Contains("glove") || n.Contains("mitt") || n.Contains("gauntlet") || n.Contains("hand");
    }

    // ---- gloves and mittens really taken off for the smoke (the arms rig has no bare hand under them) and put back on after
    static readonly List<(GearItem gi, ClothingLayer layer)> takenOff = new();

    public static void TakeOffGloves()
    {
        try
        {
            var pm = GameManager.GetPlayerManagerComponent();
            if (pm == null) return;
            for (int l = 0; l < (int)ClothingLayer.NumLayers; l++)
            {
                var gi = pm.GetClothingInSlot(ClothingRegion.Hands, (ClothingLayer)l);
                if (gi == null) continue;
                pm.TakeOffClothingItem(gi);
                takenOff.Add((gi, (ClothingLayer)l));
                Safe.Info($"Gloves off for the smoke: {gi.name} (layer {(ClothingLayer)l})");
            }
        }
        catch (Exception e)
        {
            Safe.Log("Gloves off", e);
        }
    }

    public static void PutBackGloves()
    {
        if (takenOff.Count == 0) return;
        try
        {
            var pm = GameManager.GetPlayerManagerComponent();
            if (pm != null)
                foreach (var (gi, layer) in takenOff)
                {
                    if (gi == null || !gi || pm.IsWearingClothingItem(gi)) continue;
                    // only if it is still with the player and its place on the hands is free
                    if (!gi.m_InPlayerInventory || pm.GetClothingInSlot(ClothingRegion.Hands, layer) != null) continue;
                    pm.PutOnClothingItem(gi, layer);
                    Safe.Info($"Gloves back on: {gi.name}");
                }
        }
        catch (Exception e)
        {
            Safe.Log("Gloves back on", e);
        }
        takenOff.Clear();
    }

    public static void Restore()
    {
        PutBackGloves();
        try
        {
            foreach (var go in hiddenGloves) if (go != null) go.SetActive(true);
            foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
            foreach (var r in enabledBare) if (r != null) r.enabled = false;
            foreach (var g in activatedBare) if (g != null) g.SetActive(false);
            if (shownHands != null) shownHands.SetActive(false);
        }
        catch (Exception e)
        {
            Safe.Log("Gloves back", e);
        }
        hiddenGloves.Clear();
        hiddenRenderers.Clear();
        enabledBare.Clear();
        activatedBare.Clear();
        bareTried = bareFought = false;
        loggedRenderers = false;
        shownHands = null;
        loggedClothing = false;
    }

    // ---- fingers

    static IntPtr palmFor;
    static Transform? palm;
    static readonly List<(Transform bone, Quaternion rest, float weight)> fingers = new();
    static Transform? index1, index2, middle1, middle2, ring1, thumb3, thumbTip;
    static readonly Transform?[] indexChain = new Transform?[4], middleChain = new Transform?[4];
    public static Transform? Wrist, Elbow, Shoulder;

    /// <summary>The palm the torch rig hangs on: FPH_Torch(Clone) &lt; right_prop_point &lt; RightPalm.</summary>
    public static Transform? Palm(Transform torchRig)
    {
        var p = torchRig.parent?.parent;
        if (p == null) return null;
        if (palm != null && palmFor == p.Pointer && palm) return palm;
        palmFor = p.Pointer;
        palm = p;
        Find(p);
        return palm;
    }

    static Transform? Child(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = Child(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>The finger bones and their rest (bind) rotations, from the skinned mesh that uses them.</summary>
    static void Find(Transform p)
    {
        fingers.Clear();
        index1 = Child(p, "Right_HandIndex1");
        index2 = Child(p, "Right_HandIndex2");
        middle1 = Child(p, "Right_HandMiddle1");
        middle2 = Child(p, "Right_HandMiddle2");
        ring1 = Child(p, "Right_HandRing1");
        thumb3 = Child(p, "Right_HandThumb3");
        thumbTip = Child(p, "Right_HandThumb4") ?? thumb3;
        thumbChain[0] = Child(p, "Right_HandThumb1");
        thumbChain[1] = Child(p, "Right_HandThumb2");
        thumbChain[2] = thumb3;
        thumbCarpal = Child(p, "RightThumbCarpal");
        pinky1 = Child(p, "Right_HandPinky1") ?? Like(p, "pinky1") ?? Like(p, "little1");
        pinkyTip = pinky1 == null ? null : (Child(p, "Right_HandPinky4") ?? Child(p, "Right_HandPinky3") ?? Like(p, "pinky3") ?? Like(p, "little3") ?? Deepest(pinky1));
        if (!pinkyLogged) { pinkyLogged = true; Safe.Info($"Pinky bones: {pinky1?.name ?? "none"} .. {pinkyTip?.name ?? "none"}"); }
        for (int i = 0; i < 4; i++)
        {
            indexChain[i] = Child(p, "Right_HandIndex" + (i + 1));
            middleChain[i] = Child(p, "Right_HandMiddle" + (i + 1));
        }
        // the arm the hand hangs on: RightPalm < RightWristJoint < RightElbowJoint < RightShoulderJoint
        Wrist = p.parent;
        Elbow = Wrist?.parent;
        Shoulder = Elbow?.parent;
        Safe.Info($"Arm: {Shoulder?.name} > {Elbow?.name} > {Wrist?.name} > {p.name}; tips {indexChain[3]?.name}, {middleChain[3]?.name}");
        SkinnedMeshRenderer? smr = null;
        int palmIdx = -1;
        Transform root = p;
        while (root.parent != null) root = root.parent;
        foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var bones = r.bones;
            if (bones == null || r.sharedMesh == null) continue;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] != null && bones[i].Pointer == p.Pointer)
                {
                    smr = r;
                    palmIdx = i;
                    break;
                }
            if (smr != null) break;
        }
        // bare hands: any mesh skinned to the palm that is not a glove or mitten, the biggest one
        BareMesh = null;
        var cands = new List<string>();
        foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var bones = r.bones;
            if (bones == null || r.sharedMesh == null) continue;
            bool uses = false;
            for (int i = 0; i < bones.Length && !uses; i++) uses = bones[i] != null && bones[i].Pointer == p.Pointer;
            if (!uses) continue;
            cands.Add($"{r.name} {r.sharedMesh.vertexCount}{(r.enabled && r.gameObject.activeInHierarchy ? "" : " off")}");
            var ln = r.name.ToLowerInvariant();
            if (IsGloveRenderer(r.name) || !(ln.Contains("arm") || ln.Contains("hand") || ln.Contains("cube") || ln.Contains("skin"))) continue;
            if (BareMesh == null || r.sharedMesh.vertexCount > BareMesh.sharedMesh.vertexCount) BareMesh = r;
        }
        Safe.Info($"Meshes on the palm: {string.Join(", ", cands)}; bare = {BareMesh?.name ?? "none"}");
        if (smr == null)
        {
            Safe.Info($"Fingers: no skinned mesh uses {p.name}; the fist stays");
            return;
        }
        var bs = smr.bones;
        var bind = smr.sharedMesh.bindposes;
        int IndexOf(Transform t)
        {
            for (int i = 0; i < bs.Length; i++) if (bs[i] != null && bs[i].Pointer == t.Pointer) return i;
            return -1;
        }
        void Chain(string first, float w)
        {
            var b = Child(p, first);
            while (b != null)
            {
                int bi = IndexOf(b), pi = b.parent != null ? IndexOf(b.parent) : -1;
                if (bi >= 0 && pi >= 0 && bi < bind.Length && pi < bind.Length)
                {
                    var m = bind[pi] * bind[bi].inverse;
                    var rest = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
                    fingers.Add((b, rest, w));
                }
                b = b.childCount > 0 ? b.GetChild(0) : null;
            }
        }
        Chain("Right_HandIndex1", Settings.FingerOpen.Value);
        Chain("Right_HandMiddle1", Settings.FingerOpen.Value);
        Chain("RightThumbCarpal", Settings.FingerOpen.Value * .4f);
        Safe.Info($"Fingers: {fingers.Count} bones opened from {smr.name} (palm bone {palmIdx})");
    }

    /// <summary>Index and middle eased from the fist towards their rest, every frame after the animation.</summary>
    public static void Open(float scale = 1f)
    {
        foreach (var (bone, rest, w) in fingers)
            if (bone != null) bone.localRotation = Quaternion.Slerp(bone.localRotation, rest, w * scale);
    }

    static readonly Transform?[] thumbChain = new Transform?[3];

    /// <summary>The thumb laid round the smoke: each joint turned (at most maxDeg) so the tip goes to the nearest point of the line a..b.</summary>
    public static void ThumbTo(Vector3 a, Vector3 b, float maxDeg, float radius = 0f)
    {
        if (thumbTip == null || maxDeg <= 0f) return;
        foreach (var j in thumbChain)
        {
            if (j == null) continue;
            var tip = thumbTip.position;
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(tip - a, ab) / ab.sqrMagnitude) : 0f;
            var target = a + ab * t;
            // onto the surface of the smoke, not its axis (on the axis the thumb went into a thick cigar's end)
            if (radius > 0f)
            {
                var off = Vector3.ProjectOnPlane(tip - target, ab);
                if (off.sqrMagnitude > 1e-8f) target += off.normalized * radius;
            }
            var from = tip - j.position;
            var to = target - j.position;
            if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) continue;
            var r = Quaternion.FromToRotation(from, to);
            r.ToAngleAxis(out float ang, out var axis);
            if (ang > 180f) ang -= 360f;
            j.rotation = Quaternion.AngleAxis(Mathf.Clamp(ang, -maxDeg, maxDeg), axis) * j.rotation;
        }
    }

    /// <summary>Only the thumb's tip moved by <paramref name="lift"/> (world), each thumb joint turning at most maxDeg towards it.</summary>
    public static void LiftThumbTip(Vector3 lift, float maxDeg)
    {
        if (thumbTip == null || maxDeg <= 0f || lift.sqrMagnitude < 1e-10f) return;
        var target = thumbTip.position + lift;
        foreach (var j in thumbChain)
        {
            if (j == null) continue;
            var from = thumbTip.position - j.position;
            var to = target - j.position;
            if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) continue;
            Quaternion.FromToRotation(from, to).ToAngleAxis(out float ang, out var axis);
            if (ang > 180f) ang -= 360f;
            j.rotation = Quaternion.AngleAxis(Mathf.Clamp(ang, -maxDeg, maxDeg), axis) * j.rotation;
        }
    }

    /// <summary>The thumb's tip turned towards the index finger's tip by up to <paramref name="dist"/> metres (11-09: it pointed away).</summary>
    public static void ThumbTipToIndex(float dist, float maxDeg)
    {
        var i1 = indexChain[0] ?? index1; var i2 = indexChain[1] ?? index2;
        if (thumbTip == null || i1 == null || i2 == null || dist <= 0f) return;
        var d = Vector3.Lerp(i1.position, i2.position, .6f) - thumbTip.position;
        if (d.sqrMagnitude < 1e-8f) return;
        LiftThumbTip(d.normalized * Math.Min(dist, d.magnitude * .6f), maxDeg);
    }

    /// <summary>Only the thumb's last joint turned (up to maxDeg) so its tip points at the index finger's tip (11-09 evening: moving the
    /// whole thumb by a centimetre did not show).</summary>
    public static void TurnThumbTipToIndex(float maxDeg)
    {
        // 11-09 evening: aimed not at the index tip but at the middle of the index finger, nearer its base (between joints 1 and 2)
        var i1 = indexChain[0] ?? index1; var i2 = indexChain[1] ?? index2;
        var j = thumb3;
        if (j == null || thumbTip == null || i1 == null || i2 == null || maxDeg <= 0f || thumbTip == j) return;
        var aim = Vector3.Lerp(i1.position, i2.position, .6f);
        var from = thumbTip.position - j.position;
        var to = aim - j.position;
        if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) return;
        Quaternion.FromToRotation(from, to).ToAngleAxis(out float ang, out var axis);
        if (ang > 180f) ang -= 360f;
        j.rotation = Quaternion.AngleAxis(Mathf.Clamp(ang, -maxDeg, maxDeg), axis) * j.rotation;
    }

    static Transform? pinky1, pinkyTip;
    static bool pinkyLogged;

    static Transform? Like(Transform t, string part)
    {
        if (t.name.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0 && t.name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) < 0) return t;
        for (int i = 0; i < t.childCount; i++) { var r = Like(t.GetChild(i), part); if (r != null) return r; }
        return null;
    }

    static Transform Deepest(Transform t) { while (t.childCount > 0) t = t.GetChild(0); return t; }

    /// <summary>The little finger turned at its base so its tip moves by <paramref name="move"/> (world), at most maxDeg.</summary>
    public static void MovePinky(Vector3 move, float maxDeg)
    {
        if (pinky1 == null || pinkyTip == null || pinkyTip == pinky1 || move.sqrMagnitude < 1e-10f) return;
        var from = pinkyTip.position - pinky1.position;
        var to = from + move;
        if (from.sqrMagnitude < 1e-8f) return;
        Quaternion.FromToRotation(from, to).ToAngleAxis(out float ang, out var axis);
        if (ang > 180f) ang -= 360f;
        pinky1.rotation = Quaternion.AngleAxis(Mathf.Clamp(ang, -maxDeg, maxDeg), axis) * pinky1.rotation;
    }

    static Transform? thumbCarpal;

    static void Closest(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out Vector3 c1, out Vector3 c2)
    {
        var d1 = q1 - p1; var d2 = q2 - p2; var r = p1 - p2;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        float sN, t;
        if (a < 1e-10f) { sN = 0f; t = e > 1e-10f ? Mathf.Clamp01(f / e) : 0f; }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e < 1e-10f) { t = 0f; sN = Mathf.Clamp01(-c / a); }
            else
            {
                float b = Vector3.Dot(d1, d2), den = a * e - b * b;
                sN = den > 1e-10f ? Mathf.Clamp01((b * f - c * e) / den) : 0f;
                t = (b * sN + f) / e;
                if (t < 0f) { t = 0f; sN = Mathf.Clamp01(-c / a); }
                else if (t > 1f) { t = 1f; sN = Mathf.Clamp01((b - c) / a); }
            }
        }
        c1 = p1 + d1 * sN; c2 = p2 + d2 * t;
    }

    /// <summary>The thumb bent round a rod a..b (the pipe stem): from the base joint out, every thumb bone whose segment comes closer than
    /// clearance to the rod is turned at its joint so that point moves out to the clearance, on the side where the thumb's root is.</summary>
    public static void ThumbAround(Vector3 a, Vector3 b, float clearance)
    {
        if (thumbTip == null || clearance <= 0f) return;
        var bones = new List<Transform>();
        if (thumbCarpal != null) bones.Add(thumbCarpal);
        foreach (var j in thumbChain) if (j != null) bones.Add(j);
        if (bones.Count == 0) return;
        bones.Add(thumbTip);
        var ab = b - a;
        // the side to stay on: where the root of the thumb is, seen across the rod
        var root = bones[0].position;
        float tr = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(root - a, ab) / ab.sqrMagnitude) : 0f;
        var side = Vector3.ProjectOnPlane(root - (a + ab * tr), ab.normalized);
        if (side.sqrMagnitude < 1e-8f) return;
        side.Normalize();
        for (int pass = 0; pass < 3; pass++)
            for (int i = 0; i + 1 < bones.Count; i++)
            {
                var j = bones[i]; var c = bones[i + 1];
                if (j == c) continue;
                Closest(j.position, c.position, a, b, out var pb, out var pr);
                var off = pb - pr;
                float along = Vector3.Dot(off, side);
                var perp = Vector3.ProjectOnPlane(off, ab.normalized);
                if (perp.magnitude >= clearance && along > 0f) continue;
                // move the closest point of this bone out to the clearance on the root side
                var target = pb + side * (clearance - along);
                var from = pb - j.position; var to = target - j.position;
                if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) continue;
                j.rotation = Quaternion.FromToRotation(from, to) * j.rotation;
            }
    }

    /// <summary>The thumb laid over a stem (polyline pts, radius clear incl. the thumb's own thickness): every thumb bone that dips into
    /// the stem or passes under it is turned at its joint so it goes over the top (up = the pipe's up), from the base out.</summary>
    public static void ThumbOver(Vector3[] pts, Vector3 up, float clear)
    {
        if (thumbTip == null || clear <= 0f || pts.Length < 2) return;
        var bones = new List<Transform>();
        if (thumbCarpal != null) bones.Add(thumbCarpal);
        foreach (var j in thumbChain) if (j != null) bones.Add(j);
        if (bones.Count == 0) return;
        bones.Add(thumbTip);
        for (int pass = 0; pass < 4; pass++)
            for (int i = 0; i + 1 < bones.Count; i++)
            {
                var j = bones[i]; var c = bones[i + 1];
                if (j == c) continue;
                // the closest stem segment to this bone
                float best = float.MaxValue; Vector3 pb = default, pr = default, sd = default;
                for (int k = 0; k + 1 < pts.Length; k++)
                {
                    Closest(j.position, c.position, pts[k], pts[k + 1], out var b1, out var r1);
                    float d = (b1 - r1).sqrMagnitude;
                    if (d < best) { best = d; pb = b1; pr = r1; sd = (pts[k + 1] - pts[k]).normalized; }
                }
                var off = Vector3.ProjectOnPlane(pb - pr, sd);
                var u = Vector3.ProjectOnPlane(up, sd).normalized;
                if (u.sqrMagnitude < .5f) continue;
                float h = Vector3.Dot(off, u);
                var side = off - u * h;
                if (side.magnitude >= clear) continue;           // beside the stem, clear of it
                float need = Mathf.Sqrt(Mathf.Max(0f, clear * clear - side.sqrMagnitude));
                if (h >= need) continue;                          // already over it
                var target = pr + side + u * need;
                var from = pb - j.position; var to = target - j.position;
                if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) continue;
                var rot = Quaternion.FromToRotation(from, to);
                rot.ToAngleAxis(out float ang, out var axis);
                if (ang > 180f) ang -= 360f;
                j.rotation = Quaternion.AngleAxis(Mathf.Clamp(ang, -50f, 50f), axis) * j.rotation;
            }
    }

    /// <summary>The thumb's lower part (base joint) turned so its middle joint moves by lift metres along up (the whole thumb follows).</summary>
    public static void LiftThumbBase(Vector3 up, float lift)
    {
        var j0 = thumbChain[0]; var j1 = thumbChain[1];
        if (j0 == null || j1 == null || Mathf.Abs(lift) < 1e-5f) return;
        var from = j1.position - j0.position;
        var to = from + up.normalized * lift;
        if (from.sqrMagnitude < 1e-8f) return;
        j0.rotation = Quaternion.FromToRotation(from, to) * j0.rotation;
    }

    /// <summary>Index and middle spread apart at the knuckles (degrees each), so a thick cigar fits between them.</summary>
    public static void Spread(float deg)
    {
        float sign = deg < 0f ? -1f : 1f;   // negative: index and middle closer together (a slim cigarette)
        deg = Mathf.Abs(deg);
        if (deg <= 0f || palm == null || indexChain[0] == null || middleChain[0] == null || indexChain[3] == null || middleChain[3] == null || ring1 == null) return;
        var side = (indexChain[0]!.position - ring1.position).normalized;
        var fingers = (middleChain[0]!.position - palm.position).normalized;
        var back = Vector3.Cross(side, fingers).normalized;
        void Turn(Transform k, Transform tip, float toward)
        {
            // pick the turn that moves the tip towards (+1) or away from (-1) the thumb side
            var r1 = Quaternion.AngleAxis(deg, back);
            var t1 = k.position + r1 * (tip.position - k.position);
            float d1 = Vector3.Dot(t1 - tip.position, side) * toward;
            k.rotation = (d1 >= 0f ? r1 : Quaternion.AngleAxis(-deg, back)) * k.rotation;
        }
        Turn(indexChain[0]!, indexChain[3]!, sign);
        Turn(middleChain[0]!, middleChain[3]!, -sign);
    }

    /// <summary>A cigar pinched like a pen held up: across the pads of the index and middle fingertips, the thumb under it; it lies along
    /// the knuckle line, the lit end out on the little-finger side, tilted down a little towards the palm.</summary>
    public static bool Pinch(float towardThumb, out Vector3 pos, out Vector3 dir, out Vector3 up)
    {
        pos = dir = up = Vector3.zero;
        if (indexChain[3] == null || middleChain[3] == null || ring1 == null || palm == null) return false;
        var tips = (Along(indexChain, 2.6f) + Along(middleChain, 2.6f)) * .5f;
        var thumb = thumbTip != null ? thumbTip.position : tips;
        pos = Vector3.Lerp(tips, thumb, Mathf.Clamp01(towardThumb) * .8f + .1f);
        var fingers = (middleChain[0]!.position - palm.position).normalized;
        var side = (indexChain[0]!.position - ring1.position).normalized;
        var back = Vector3.Cross(side, fingers).normalized;
        dir = (-side * .9f + fingers * .15f - back * .25f).normalized;
        up = back;
        return true;
    }

    /// <summary>A point along a finger, t in joints: 0 = knuckle (…1), 1 = …2, 2 = …3, 3 = the tip (…4).</summary>
    static Vector3 Along(Transform?[] chain, float t)
    {
        int i = Mathf.Clamp(Mathf.FloorToInt(t), 0, 2);
        var a = chain[i];
        var b = chain[i + 1] ?? a;
        if (a == null) return Vector3.zero;
        return Vector3.LerpUnclamped(a.position, b!.position, t - i);
    }

    /// <summary>Where a cigarette sits between index and middle, near the fingertips (grip in joints, see Along), and the way its lit end
    /// points: across the fingers, out of the back of the hand, leaning a little towards the tips. viewFwd decides the side once.</summary>
    public static bool Between(float grip, out Vector3 pos, out Vector3 dir, out Vector3 along)
    {
        pos = dir = along = Vector3.zero;
        if (indexChain[0] == null || indexChain[2] == null || middleChain[0] == null || middleChain[2] == null || ring1 == null || palm == null) return false;
        var a = Along(indexChain, grip);
        var b = Along(middleChain, grip);
        pos = (a + b) * .5f;
        var i0 = Along(indexChain, grip - .5f);
        var m0 = Along(middleChain, grip - .5f);
        along = (pos - (i0 + m0) * .5f).normalized;
        var side = (indexChain[0]!.position - ring1.position).normalized;
        // the back of the right hand: side (towards the thumb) x fingers
        var back = Vector3.Cross(side, along).normalized;
        // out over the middle finger, lit end away from the thumb and up from the back of the hand, a little towards the tips
        var d = back * Settings.Vec(Settings.CigInFingers).x - side * Settings.Vec(Settings.CigInFingers).y + along * Settings.Vec(Settings.CigInFingers).z;
        dir = d.normalized * (Settings.FlipCigarette.Value ? -1f : 1f);
        pos += back * .004f;
        return true;
    }

    /// <summary>The hand's frame now: fingers (palm to middle knuckles) and the back of the hand.</summary>
    public static bool Frame(out Vector3 fingers, out Vector3 back)
    {
        fingers = back = Vector3.zero;
        if (palm == null || middleChain[0] == null || indexChain[0] == null || ring1 == null) return false;
        fingers = (middleChain[0]!.position - palm.position).normalized;
        var side = (indexChain[0]!.position - ring1.position).normalized;
        back = Vector3.Cross(side, fingers).normalized;
        return fingers.sqrMagnitude > .5f && back.sqrMagnitude > .5f;
    }

    public static void Reset()
    {
        palm = null;
        palmFor = IntPtr.Zero;
        fingers.Clear();
        Wrist = Elbow = Shoulder = null;
        pinky1 = pinkyTip = null;
    }
}
