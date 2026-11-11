using Il2CppInterop.Runtime;

namespace SmokesBySeagull;

/// <summary>The hands are drawn by a camera that turns with the look inside the arms rig (no bone of the rig does): every frame, just before
/// that camera culls, its view of the eye bone is kept; the mouth poses then use the eye bone carried by the change of that view since the smoke
/// started, so whatever sits at the mouth stays put on screen (a frame late at most).</summary>
internal static class ViewLock
{
    static bool hooked, logged;
    static Camera.CameraCallback? cb;
    public static int Layer = -1;
    public static Transform? Eye;
    public static Transform? Prop;      // the smoke: checked at the draw against where it was put and where it sat on screen at the start
    public static Vector3 PropSetAt;    // world position of the smoke at the end of our LateUpdate this frame
    public static bool PropSet;
    static Matrix4x4 refCamProp;        // camera-from-smoke when the pose was taken
    static bool grabRefProp, haveRefProp;
    static float nextDrawLog;
    static Camera? cam;                 // the weapon camera that draws the hands
    static Matrix4x4 used;              // what Carry used this frame (to compare with what the draw then really had)
    static bool haveUsed;
    static float nextLagLog;
    static Matrix4x4 last;              // camera-from-eye at the last draw of the hands
    static bool haveLast;
    static Matrix4x4 refView;           // camera-from-eye when the smoke's pose was taken
    static bool haveRef;
    static CameraGlobalRT? rt;
    static Matrix4x4 refK;              // clip-from-eye by the game's own hands view-projection when the pose was taken
    public static bool ByGameView;      // that view-projection really shows the smoke: carry by it
    static float nextLog;

    public static void Hook()
    {
        try
        {
            // hooked again for every smoke: something else may have replaced the callback since (the checks stopped after the first smoke)
            cb ??= DelegateSupport.ConvertDelegate<Camera.CameraCallback>(new Action<Camera>(Pre));
            var cur = Camera.onPreCull;
            if (cur != null && hooked) { var rest = Il2CppSystem.Delegate.Remove(cur, cb); cur = rest == null ? null : rest.Cast<Camera.CameraCallback>(); }
            Camera.onPreCull = cur == null ? cb : Il2CppSystem.Delegate.Combine(cur, cb).Cast<Camera.CameraCallback>();
            if (!hooked) Safe.Info("View lock hooked");
            hooked = true;
        }
        catch (Exception e) { Safe.Log("View lock hook", e); }
    }

    static void Pre(Camera c)
    {
        try
        {
            if (Layer < 0 || Eye == null || !Eye || c == null) return;
            if ((c.cullingMask & (1 << Layer)) == 0) return;
            if (Locked && lockCam != null && c.Pointer == lockCam.Pointer)
            {
                if (haveLate && Time.unscaledTime > nextPostLog && Prop != null && Prop && Frame != null && Frame && B != null)
                {
                    nextPostLog = Time.unscaledTime + 2f;
                    var b = B;
                    var dB = (Vector3)(b.localToWorldMatrix.GetColumn(3) - lateB.GetColumn(3));
                    var dF = (Vector3)(Frame.localToWorldMatrix.GetColumn(3) - lateFrame.GetColumn(3));
                    var pInB = (Vector3)(b.worldToLocalMatrix * Prop.localToWorldMatrix).GetColumn(3) - (Vector3)(lateB.inverse * lateProp).GetColumn(3);
                    var bInF = (Vector3)(Frame.worldToLocalMatrix * b.localToWorldMatrix).GetColumn(3) - (Vector3)(lateFrame.inverse * lateB).GetColumn(3);
                    Safe.Info($"After our pose, before the draw: {b.name} moved {dB.magnitude:F3} m, frame {dF.magnitude:F3} m; {b.name} in frame {bInF.magnitude:F3} m; smoke in {b.name} {pInB.magnitude:F3} m");
                }
                Apply(c);
            }
            last = c.worldToCameraMatrix * Eye.localToWorldMatrix;
            haveLast = true;
            cam = c;
            if (Prop != null && Prop)
            {
                var cp = c.worldToCameraMatrix * Prop.localToWorldMatrix;
                if (grabRefProp) { refCamProp = cp; haveRefProp = true; grabRefProp = false; }
                if (haveRefProp && Time.unscaledTime > nextDrawLog)
                {
                    nextDrawLog = Time.unscaledTime + 2f;
                    var r = (Vector3)refCamProp.GetColumn(3);
                    var n = (Vector3)cp.GetColumn(3);
                    var e = Eye.InverseTransformDirection(c.transform.forward);
                    Safe.Info($"Draw check: smoke in camera now {n:F3} vs start {r:F3} (off {(n - r).magnitude:F3} m); moved after our pose {(PropSet ? (Prop.position - PropSetAt).magnitude : -1f):F4} m; camera look in eye {e:F2}; camera at eye {Eye.InverseTransformPoint(c.transform.position):F3}");
                }
            }
            if (haveUsed && Time.unscaledTime > nextLagLog)
            {
                nextLagLog = Time.unscaledTime + 5f;
                var a = used.inverse * last;
                Safe.Info($"View lock lag check: drawn vs used this frame turned {Quaternion.Angle(Quaternion.identity, Quaternion.LookRotation((Vector3)a.GetColumn(2), (Vector3)a.GetColumn(1))):F2} deg, moved {((Vector3)a.GetColumn(3)).magnitude:F4} m");
            }
            if (!logged)
            {
                logged = true;
                Safe.Info($"View lock: hands drawn by {c.name}; camera-from-eye column y {(Vector3)last.GetColumn(1):F3} z {(Vector3)last.GetColumn(2):F3} t {(Vector3)last.GetColumn(3):F3}");
            }
        }
        catch { }
    }

    // ---- the lock: the hands camera is put, at the very moment of the draw, where it stood relative to the smoke when the smoke lay at
    // rest in the hand. Whatever moves the hand and the smoke (look up/down, walking, the game's own arm motion, even after our LateUpdate)
    // moves the camera with it, so the picture of the hand stays exactly the same; only our own moves (the drag to the mouth, the pipe going
    // into the teeth) are kept, because they are taken out first: the camera follows the smoke's "virtual rest" pose = smoke now * our move^-1.
    public static bool Locked;
    public static Transform? Frame;     // the arms frame (Camera_Weapon_Offset): our moves are measured in it
    public static Transform? Bone;      // what the camera rides: the right clavicle (above our arm IK), else the smoke
    static Transform? B => Bone != null && Bone ? Bone : Prop;
    static Matrix4x4 lateB, lateProp, lateFrame;
    static bool haveLate;
    static float nextPostLog;
    static Camera? lockCam;
    static Matrix4x4 restToCam;         // camera-to-world in the resting smoke's space, taken once
    static Matrix4x4 restInFrame;       // the resting smoke in the arms frame
    static Matrix4x4 ourMove = Matrix4x4.identity; // the smoke's local move by our poses this frame (rest -> now)
    static float nextLockLog, lockedAt;
    const float BlendIn = .5f;

    static void Apply(Camera c)
    {
        var b = B;
        if (b == null || !b) return;
        var virt = b.localToWorldMatrix * ourMove.inverse;
        var locked = virt * restToCam;   // camera-to-world
        float w = Mathf.Clamp01((Time.unscaledTime - lockedAt) / BlendIn);
        if (w < 1f)
        {
            // eased in, so the hand does not jump when the lock takes over from the game's own view
            w = w * w * (3f - 2f * w);
            c.ResetWorldToCameraMatrix();
            var game = c.worldToCameraMatrix.inverse;
            var flip = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));
            var a = game * flip; var bb = locked * flip;
            var p = Vector3.Lerp(a.GetColumn(3), bb.GetColumn(3), w);
            var q = Quaternion.Slerp(Quaternion.LookRotation(a.GetColumn(2), a.GetColumn(1)), Quaternion.LookRotation(bb.GetColumn(2), bb.GetColumn(1)), w);
            locked = Matrix4x4.TRS(p, q, Vector3.one) * flip;
        }
        c.worldToCameraMatrix = locked.inverse;
        lockCam = c;
    }

    /// <summary>Called once, when the smoke has settled at rest in the hand: the camera as the game has it now is kept against the smoke.</summary>
    public static void Anchor(Camera? c, Vector2? restOnScreen = null)
    {
        if (Locked || !Settings.LockCamera.Value || c == null || !c || Prop == null || !Prop || Frame == null || !Frame) return;
        try
        {
            c.ResetWorldToCameraMatrix();
            var b = B!;
            var camToWorld = c.worldToCameraMatrix.inverse;
            if (restOnScreen.HasValue)
            {
                // the held smoke always at the same place on the view, however the head was turned when the lock was taken
                var pm = c.projectionMatrix;
                var p = c.worldToCameraMatrix.MultiplyPoint3x4(Prop.position);
                var v = restOnScreen.Value;
                var want = new Vector3((v.x * 2f - 1f) / pm.m00, (v.y * 2f - 1f) / pm.m11, -1f).normalized;
                if (p.sqrMagnitude > 1e-6f && p.z < 0f)
                {
                    float was = .5f + .5f * pm.m00 * p.x / -p.z, wasY = .5f + .5f * pm.m11 * p.y / -p.z;
                    camToWorld = camToWorld * Matrix4x4.Rotate(Quaternion.FromToRotation(want, p.normalized));
                    Safe.Info($"Hands view turned so the held smoke sits at ({v.x:0.00}, {v.y:0.00}) on screen (was {was:0.00}, {wasY:0.00})");
                }
            }
            restToCam = b.worldToLocalMatrix * camToWorld;
            restInFrame = Frame.worldToLocalMatrix * b.localToWorldMatrix;
            ourMove = Matrix4x4.identity;
            lockCam = c;
            cam = c;
            Locked = true;
            lockedAt = Time.unscaledTime;
            Safe.Info($"Hands camera locked to {b.name}: camera in smoke space at {(Vector3)restToCam.GetColumn(3):F3}");
        }
        catch (Exception e) { Safe.Log("Hands camera lock", e); }
    }

    /// <summary>End of our LateUpdate: how far our own poses have moved the smoke from rest (in the arms frame), then the camera for it.</summary>
    public static void AfterPose()
    {
        if (!Locked || Prop == null || !Prop || Frame == null || !Frame || lockCam == null || !lockCam) return;
        try
        {
            var b = B!;
            ourMove = restInFrame.inverse * Frame.worldToLocalMatrix * b.localToWorldMatrix;
            lateB = b.localToWorldMatrix; lateProp = Prop.localToWorldMatrix; lateFrame = Frame.localToWorldMatrix; haveLate = true;
            Apply(lockCam);
            if (Time.unscaledTime > nextLockLog)
            {
                nextLockLog = Time.unscaledTime + 3f;
                var s = lockCam.projectionMatrix * lockCam.worldToCameraMatrix * new Vector4(Prop.position.x, Prop.position.y, Prop.position.z, 1f);
                Safe.Info($"Camera lock check: smoke on screen ({(s.x / s.w + 1f) / 2f:F3}, {(s.y / s.w + 1f) / 2f:F3}), our move {(Vector3)ourMove.GetColumn(3):F3}");
            }
        }
        catch (Exception e) { Safe.Log("Hands camera lock", e); }
    }

    /// <summary>A point given on the locked hands view (x, y 0..1 and distance in m) in the world, as the view stands this frame.</summary>
    public static bool OnView(Vector3 v, out Vector3 world)
    {
        world = Vector3.zero;
        if (!Locked || lockCam == null || !lockCam || Frame == null || !Frame) return false;
        var camToWorld = Frame.localToWorldMatrix * restInFrame * restToCam;
        var pm = lockCam.projectionMatrix;
        float d = Mathf.Max(.05f, v.z);
        var local = new Vector3((v.x * 2f - 1f) * d / pm.m00, (v.y * 2f - 1f) * d / pm.m11, -d);
        world = camToWorld.MultiplyPoint3x4(local);
        return true;
    }

    /// <summary>The locked hands view's axes in the world this frame.</summary>
    public static bool Axes(out Vector3 right, out Vector3 up, out Vector3 fwd)
    {
        right = Vector3.right; up = Vector3.up; fwd = Vector3.forward;
        if (!Locked || lockCam == null || !lockCam || Frame == null || !Frame) return false;
        var m = Frame.localToWorldMatrix * restInFrame * restToCam;
        right = ((Vector3)m.GetColumn(0)).normalized;
        up = ((Vector3)m.GetColumn(1)).normalized;
        fwd = -((Vector3)m.GetColumn(2)).normalized;
        return true;
    }

    static Matrix4x4 savedRestToCam, savedRestInFrame;
    static float savedAt;
    static bool haveSaved;

    /// <summary>The lock as it was before the scene change, put back on the new camera (true when done).</summary>
    public static bool Relock(Camera? c)
    {
        if (Locked || !haveSaved || Time.unscaledTime - savedAt > 120f || !Settings.LockCamera.Value || c == null || !c || Prop == null || !Prop || Frame == null || !Frame) return false;
        try
        {
            c.ResetWorldToCameraMatrix();
            restToCam = savedRestToCam;
            restInFrame = savedRestInFrame;
            ourMove = Matrix4x4.identity;
            lockCam = c;
            cam = c;
            Locked = true;
            lockedAt = Time.unscaledTime;
            haveSaved = false;
            Safe.Info($"Hands camera locked again as before the scene change: camera in smoke space at {(Vector3)restToCam.GetColumn(3):F3}");
            return true;
        }
        catch (Exception e) { Safe.Log("Hands camera relock", e); return false; }
    }

    public static void Unlock()
    {
        haveLate = false;
        if (!Locked) return;
        // kept for a scene change: the same smoke comes back and is locked the same way again (re-taking the lock while the new scene
        // settled put the hand in another place: log 1901)
        savedRestToCam = restToCam; savedRestInFrame = restInFrame; savedAt = Time.unscaledTime; haveSaved = true;
        Locked = false;
        try { if (lockCam != null && lockCam) lockCam.ResetWorldToCameraMatrix(); } catch { }
        lockCam = null;
        Safe.Info("Hands camera unlocked");
    }

    /// <summary>The pose is taken now: later changes of the view are carried over from here.</summary>
    /// <summary>The weapon camera's view of the eye bone as it is right now (this frame), else as last drawn.</summary>
    static bool Now(out Matrix4x4 m)
    {
        m = last;
        if (cam != null && cam && Eye != null && Eye)
        {
            m = cam.worldToCameraMatrix * Eye.localToWorldMatrix;
            return true;
        }
        return haveLast;
    }

    public static void TakeRef(Vector3 smoke)
    {
        haveRef = Now(out refView);
        grabRefProp = true;
        ByGameView = false;
        try
        {
            if (rt == null || !rt) rt = UnityEngine.Object.FindObjectOfType<CameraGlobalRT>();
            if (rt == null || Eye == null) { Safe.Info("Hands view-projection: CameraGlobalRT not found"); return; }
            var vp = rt.m_PreviousViewProjection_Weapon;
            var v = vp * new Vector4(smoke.x, smoke.y, smoke.z, 1f);
            var ndc = v.w != 0f ? new Vector3(v.x / v.w, v.y / v.w, v.z / v.w) : Vector3.positiveInfinity;
            ByGameView = v.w > 0f && Mathf.Abs(ndc.x) < 1.2f && Mathf.Abs(ndc.y) < 1.2f;
            refK = vp * Eye.localToWorldMatrix;
            Safe.Info($"Hands view-projection from the game: smoke at screen ({(ndc.x + 1f) / 2f:F2}, {(ndc.y + 1f) / 2f:F2}), w {v.w:F3} -> {(ByGameView ? "carry by it" : "not usable, turn about the pivot")}; VP row0 {(Vector4)vp.GetRow(0):F3} row1 {(Vector4)vp.GetRow(1):F3} row3 {(Vector4)vp.GetRow(3):F3}");
            var c = (Matrix4x4)last;
            Safe.Info($"Weapon camera at the same time: camera-from-eye col y {(Vector3)c.GetColumn(1):F3} z {(Vector3)c.GetColumn(2):F3} t {(Vector3)c.GetColumn(3):F3}");
        }
        catch (Exception e) { Safe.Log("Hands view-projection", e); }
    }

    /// <summary>The eye carried by the change of the game's own hands view-projection since the reference: K(now)^-1 * K(ref).</summary>
    static bool CarryByGame(Transform eye, out Vector3 p, out Quaternion q)
    {
        p = eye.position;
        q = eye.rotation;
        if (rt == null || !rt) return false;
        var k = rt.m_PreviousViewProjection_Weapon * eye.localToWorldMatrix;
        var d = k.inverse * refK;
        var f = (Vector3)d.GetColumn(2);
        var u = (Vector3)d.GetColumn(1);
        if (f.sqrMagnitude < 1e-6f || u.sqrMagnitude < 1e-6f || float.IsNaN(f.x)) return false;
        float w = d.m33 != 0f ? d.m33 : 1f;
        var t = (Vector3)d.GetColumn(3) / w;
        var turn = Quaternion.LookRotation(f, u);
        float a = Settings.FollowAmount.Value;
        turn = Quaternion.SlerpUnclamped(Quaternion.identity, turn, a);
        q = eye.rotation * turn;
        p = eye.TransformPoint(t * a);
        if (Time.unscaledTime > nextLog)
        {
            nextLog = Time.unscaledTime + 5f;
            Safe.Info($"Carry by game view: turned {turn.eulerAngles:F1}, moved {t:F3}, row3 {(Vector4)d.GetRow(3):F4}");
        }
        return true;
    }

    public static void Clear() { haveRef = false; }

    /// <summary>The eye bone moved by the view's change since the reference.</summary>
    public static bool Carry(Transform eye, out Vector3 p, out Quaternion q)
    {
        p = eye.position;
        q = eye.rotation;
        if (Locked) return true;   // the camera rides the smoke itself: nothing to carry
        if (false && ByGameView && Settings.GameView.Value && CarryByGame(eye, out p, out q))
        {
            // the game's matrix must turn when the weapon camera turns; if it stays still it is not updated every frame
            if (haveRef && haveLast)
            {
                float cam = Quaternion.Angle(Quaternion.identity, Quaternion.LookRotation((Vector3)(last.inverse * refView).GetColumn(2), (Vector3)(last.inverse * refView).GetColumn(1)));
                float game = Quaternion.Angle(eye.rotation, q);
                if (cam > 4f && game < .5f)
                {
                    ByGameView = false;
                    Safe.Info($"Hands view-projection does not follow the look (camera turned {cam:F1}, game matrix {game:F1}): turn about the pivot instead");
                }
                else return true;
            }
            else return true;
            p = eye.position; q = eye.rotation;
        }
        if (!haveRef || !haveLast) return false;
                // the full change of the weapon camera's view of the eye bone (it turns about the camera itself, 1.75 m above the bone):
        // the same as the game's own hands view-projection gives, checked in the logs
        if (!Now(out var now)) return false;
        used = now; haveUsed = true;
        var d = now.inverse * refView;
        var f = (Vector3)d.GetColumn(2);
        var u = (Vector3)d.GetColumn(1);
        if (f.sqrMagnitude < 1e-6f || u.sqrMagnitude < 1e-6f || float.IsNaN(f.x)) return false;
        // the whole change of the camera's view (it pitches about a neck point, so it also moves): taken this frame, not a frame late,
        // so the walking bob is cancelled at once instead of shaking the smoke
        float a = Settings.FollowAmount.Value;
        var turn = Quaternion.SlerpUnclamped(Quaternion.identity, Quaternion.LookRotation(f, u), a);
        q = eye.rotation * turn;
        p = eye.TransformPoint((Vector3)d.GetColumn(3) * a);
        return true;
    }
}
