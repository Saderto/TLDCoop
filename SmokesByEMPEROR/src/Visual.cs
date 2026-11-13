using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SmokesByEMPEROR;

/// <summary>What the player sees of a smoke: our model on the first-person torch handle, the ember and its light, a wisp, the hand coming to
/// the mouth (by the weapon's own offset springs) and smoke breathed out in front of the camera. Materials are borrowed from the torch's own effects.</summary>
public static class Visual
{
    static IntPtr fphFor;              // the torch hand rig our prop sits on
    static GameObject? prop, ember, halo;
    static Light? light;
    static Material? glowMat, smokeMat, propMat;
    static Mesh? quad, ownQuad;
    static Material? ownSmoke;
    static float keepNow = 1f;
    static Vector3 restLocal;
    static Quaternion restRot = Quaternion.identity;
    static bool haveRest, restOnly;
    /// <summary>11-09: after a scene change the view settles for a while (log: eye carried 0.75 m off, arm stretched out): the rest pose and view reference are taken again until then.</summary>
    public static float SettleUntil;
    /// <summary>Set when the smoke came back with a scene change: the hands camera lock from before is put back instead of taken anew.</summary>
    public static bool ResumeLock;
    // 11-09 evening (log 1943): the rest pose (the smoke in the hand, in the eye bone's space) was taken again after a scene change while
    // the pipe hand was already down, so the pipe in the teeth came out turned wrong (bowl to the camera). It is kept over the change now.
    static Vector3 savedRestLocal; static Quaternion savedRestRot = Quaternion.identity, savedPipeHand = Quaternion.identity;
    static bool savedRestHave, savedPipeHandSet, restFixed;
    static float savedRestAt;
    // a pipe is held in the fist turned the way it lies in the mouth (so going to the mouth it only moves, it does not turn in the air);
    // the old in-hand pose is still what the tuned mouth turns are measured from
    static Quaternion pipeHandFromEye = Quaternion.identity, heldRot0 = Quaternion.identity;
    static Vector3 heldPos0;
    static bool pipeHandSet, pipeHeldAsMouth;

    /// <summary>The smoke as it lies in the resting hand, in the eye bone's space: the start of every move to the mouth.</summary>
    static Vector3 shownLocal, savedShownLocal; static Quaternion shownRot = Quaternion.identity, savedShownRot = Quaternion.identity;

    static void KeepRest()
    {
        if (prop == null || eyeBone == null) return;
        ViewLock.Eye = eyeBone;
        ViewLock.Layer = prop.layer;
        ViewLock.Hook();
        ViewLock.TakeRef(pipeHeldAsMouth ? heldPos0 : prop.transform.position);
        if (restFixed) return;   // the rest from before the scene change stays
        Eye(out var ep, out var eq);
        var rp = pipeHeldAsMouth ? heldPos0 : prop.transform.position;
        var rr = pipeHeldAsMouth ? heldRot0 : prop.transform.rotation;
        // as it really lies in the hand: what the hand is held to at rest (18:16 user: with a cigarette the hand jerked for a frame when
        // taken out, because for the frames before the lock it was held to the pose without the wrist limit)
        shownLocal = Quaternion.Inverse(eq) * (rp - ep);
        shownRot = Quaternion.Inverse(eq) * rr;
        if (!pipeHeldAsMouth && Hands.Wrist != null && Quaternion.Angle(restFree, Quaternion.identity) > .01f)
        {
            // a cigarette: the rest pose as it would be with the wrist not limited (see RestHand)
            var wp = Hands.Wrist.position;
            rp = wp + restFree * (rp - wp);
            rr = restFree * rr;
        }
        restLocal = Quaternion.Inverse(eq) * (rp - ep);
        restRot = Quaternion.Inverse(eq) * rr;
        haveRest = true;
        pipeHandSet = false;   // the pipe's turn in the fist is taken again from this rest pose
    }

    static Camera? weaponCam;
    static float weaponCamLook;
    static bool eyeLogged;

    static Camera? WeaponCam()
    {
        if (weaponCam != null && weaponCam) return weaponCam;
        if (Time.unscaledTime < weaponCamLook) return null;
        weaponCamLook = Time.unscaledTime + 2f;
        try
        {
            var vp = GameManager.GetVpFPSCamera();
            Camera? wc = null;
            try { wc = vp != null ? vp.GetWeaponCamera() : null; } catch { }
            if (wc == null) try { wc = vp != null ? vp.m_WeaponCamera : null; } catch { }
            weaponCam = wc;
        }
        catch { }
        return weaponCam;
    }

    /// <summary>The eye the mouth poses are kept in: the arms rig's eye bone, but turned up and down with the view. The hands are drawn by the
    /// weapon camera, which sits 1.75 m above that bone and pitches with the look while the bone does not; so this eye is the bone carried
    /// round with the weapon camera, and whatever is kept in it stays put on screen.</summary>
    static void Eye(out Vector3 p, out Quaternion q)
    {
        p = eyeBone != null ? eyeBone.position : Vector3.zero;
        q = eyeBone != null ? eyeBone.rotation : Quaternion.identity;
        if (eyeBone == null || !Settings.FollowView.Value) return;
        ViewLock.Carry(eyeBone, out p, out q);
        // a view reference taken while the view was still settling (a scene change) carried the eye far off: take the rest again
        if (eyeBone.InverseTransformPoint(p).sqrMagnitude > .3f * .3f && Time.unscaledTime >= SettleUntil)
        {
            SettleUntil = Time.unscaledTime + .3f;
            Safe.Info($"View-carried eye {eyeBone.InverseTransformPoint(p):F2} m off the eye bone: rest pose taken again");
        }
        if (Time.unscaledTime > nextAnchorLog)
        {
            nextAnchorLog = Time.unscaledTime + 5f;
            var e = Quaternion.Inverse(eyeBone.rotation) * q;
            Safe.Info($"View-carried eye vs eye bone: turned {e.eulerAngles:F1}, moved {eyeBone.InverseTransformPoint(p):F3}");
        }
    }
    static float nextAnchorLog;

    // the arms' own frame (Camera_Weapon_Offset, parent of both shoulders; we never move it): the game turns it with the look, so the hands
    // stay put on screen; the eye bone does not. Mouth poses kept in it stay put on screen with the hands, frame for frame.
    static Transform? anchor;
    static Vector3 anchorPos;
    static Quaternion anchorRot = Quaternion.identity;
    static bool haveAnchor;

    static Transform? Anchor()
    {
        if (anchor != null && anchor) return anchor;
        anchor = null;
        if (prop == null) return null;
        for (var t = prop.transform.parent; t != null; t = t.parent)
            if (t.name == "Camera_Weapon_Offset") { anchor = t; break; }
        if (anchor != null && !eyeLogged) { eyeLogged = true; Safe.Info("Mouth poses kept in the arms frame Camera_Weapon_Offset"); }
        return anchor;
    }

    // ---- the whole arms frame turned with the view: the shoulder stays where it is in the rig while the view pitches about the hands
    // camera, so the arm could never reach a pose locked to the screen (looking up it was pulled into the camera). Camera_Weapon_Offset
    // (parent of both shoulders) is carried with the view each frame and put back first thing the next frame.
    static Transform? carried;
    static Vector3 carriedBasePos, carriedLeftPos;
    static Quaternion carriedBaseRot = Quaternion.identity, carriedLeftRot = Quaternion.identity;
    static bool carriedLeft, carriedLogged;

    static void UncarryArms()
    {
        if (carried == null || !carriedLeft) return;
        try
        {
            if (carried && carried.localPosition == carriedLeftPos && carried.localRotation == carriedLeftRot)
            {
                carried.localPosition = carriedBasePos;
                carried.localRotation = carriedBaseRot;
            }
        }
        catch { }
        carriedLeft = false;
    }

    static void CarryArms()
    {
        UncarryArms();
        if (eyeBone == null || !haveRest || !Settings.FollowView.Value || !Settings.CarryArms.Value || ViewLock.Locked) return;
        var a = Anchor();
        if (a == null) return;
        carried = a;
        carriedBasePos = a.localPosition;
        carriedBaseRot = a.localRotation;
        Eye(out var p, out var q);
        var inv = Quaternion.Inverse(eyeBone.rotation);
        a.SetPositionAndRotation(p + q * (inv * (a.position - eyeBone.position)), q * inv * a.rotation);
        carriedLeftPos = a.localPosition;
        carriedLeftRot = a.localRotation;
        carriedLeft = true;
        if (!carriedLogged) { carriedLogged = true; Safe.Info("Arms frame carried with the view (Camera_Weapon_Offset)"); }
    }

    static void KeepAnchor()
    {
        haveAnchor = false;
        var a = Anchor();
        if (a == null || eyeBone == null) return;
        anchorPos = a.InverseTransformPoint(eyeBone.position);
        anchorRot = Quaternion.Inverse(a.rotation) * eyeBone.rotation;
        haveAnchor = true;
    }

    static Vector3 EyeVector(Vector3 v)
    {
        Eye(out _, out var q);
        return q * v;
    }
    static Burn? burn;
    static float emberZ;
    static float propRadius = .0095f;   // the held smoke's thickness: a cigar's fingers open by it (tuned on the Cohibra, r .0095)               // the lit end, along the prop's z, at full length
    static Vector3 pipeBowl, pipeGrip;
    static readonly List<Vector3> pipeVerts = new();   // a sample of the pipe's points (its own space): nothing of it may sink into the fist
    static Material? tobaccoGlow;
    static Transform? gripFor;         // the torch handle in the palm: our prop is put in it every frame
    static GameObject? emberCap, emberBand;
    static Material? bandMat;

    // the weapon's own offsets, kept while ours are added for a drag
    static vp_FPSWeapon? posed;
    static Vector3 basePos, baseRot;
    static bool havePose;

    public static void Reset()
    {
        if (haveRest)
        {
            savedRestLocal = restLocal; savedRestRot = restRot; savedShownLocal = shownLocal; savedShownRot = shownRot; savedPipeHand = pipeHandFromEye; savedPipeHandSet = pipeHandSet;
            savedRestHave = true; savedRestAt = Time.unscaledTime;
        }
        restFixed = false;
        ResumeLock = false;
        upHave = false;
        faceHave = false;
        restDirHave = false;
        restFree = Quaternion.identity;
        Tune.Frozen = false;   // the hold key is for one smoke: the next starts in the hand
        UncarryArms();
        carried = null;
        haveRest = false;
        pipeHandSet = pipeHeldAsMouth = false;
        ViewLock.Clear();
        ViewLock.Unlock();
        Tune.Frozen = false;
        try
        {
            if (prop != null) UnityEngine.Object.Destroy(prop);
        }
        catch { }
        FilterStain.Clear();
        prop = ember = halo = emberCap = null;
        tobaccoGlow = null;
        emberBand = null;
        bandMat = null;
        flung = false;
        dumped = false;
        ClearMarkers();
        eyeBone = null;
        haveView = false;
        guardInit = false;
        for (int i = 0; i < 4; i++) guarded[i] = null;
        light = null;
        fphFor = IntPtr.Zero;
        Hands.Reset();
    }

    static FirstPersonWeapon? HandRig(GearItem item, out vp_FPSWeapon? weapon)
    {
        weapon = null;
        var fpi = item.m_FirstPersonItem;
        if (fpi == null) return null;
        if (!fpi.TryGetFPSWeapon(out var w) || w == null) return null;
        weapon = w;
        return w.m_FirstPersonWeaponRightHand;
    }

    static Transform? FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    public static void Update(GearItem item, Def lit, float share, float glow)
    {
        FirstPersonWeapon? rig;
        try
        {
            rig = HandRig(item, out _);
        }
        catch (Exception e)
        {
            Safe.Log("Hand rig", e);
            return;
        }
        if (rig == null) return;
        if (prop == null || fphFor != rig.Pointer) Build(rig, lit);
        if (prop == null) return;
        // the torch's own model and effects stay hidden: the game turns them back on as it likes
        foreach (var r in rig.GetComponentsInChildren<Renderer>(true))
            if (r != null && r.enabled && !IsOurs(r.transform)) r.enabled = false;
        foreach (var l in rig.GetComponentsInChildren<Light>(true))
            if (l != null && l.enabled && l != light) l.enabled = false;

        // shorter as it burns: a cigarette down to its filter, a cigar to about half
        // only the tobacco part burns: a cigarette stops at its filter, a cigar at its band; the mesh is cut there (filter and band keep their size)
        float keep = 1f;
        if (lit.Smoke != SmokeType.Pipe && burn != null)
        {
            float stop = burn.MinZ + (lit.Smoke == SmokeType.Cigarette ? (Smoking.From?.CigMesh == "cig_papirosa" ? .067f : Settings.CigaretteFilterStop.Value) : Settings.CigarBandStop.Value);
            float z = Mathf.Lerp(emberZ, Math.Min(stop, emberZ), Mathf.Clamp01(share));
            burn.Cut(z);
            keep = emberZ > 1e-4f ? z / emberZ : 1f;
        }
        if (lit.Smoke == SmokeType.Cigarette) FilterStain.Set(Smoking.StainShare);
        keepNow = keep;
        var tip = lit.Smoke == SmokeType.Pipe ? pipeBowl : new Vector3(0f, 0f, emberZ * keep);
        if (ember != null) ember.transform.localPosition = tip;

        // ember: a faint red glow, bright orange on a drag
        float flicker = .9f + .1f * Mathf.PerlinNoise(Time.time * 3f, 0f);
        float k = (.35f + .65f * glow) * flicker;
        var cam = GameManager.GetVpFPSCamera();
        var camT = cam != null ? cam.transform : null;
        if (halo != null)
        {
            // 11-13 (user: "an orange circle under the model"): the glow was a disc twice as wide as a cigarette, drawn round its end.
            // Now hardly wider than the smoke itself, fainter, and gone when the lit end is not turned to the eye
            float size = (lit.Smoke == SmokeType.Pipe ? .02f : Mathf.Clamp(propRadius * 2.5f, .008f, .024f)) * (1f + glow * .3f);
            halo.transform.localScale = new Vector3(size, size, size);
            if (haveView) halo.transform.rotation = Quaternion.LookRotation(view.transform.forward, view.transform.up);
            else if (camT != null) halo.transform.rotation = Quaternion.LookRotation(halo.transform.position - camT.position, camT.up);
            float seen = 1f;
            var hw = WeaponCam();
            if (lit.Smoke != SmokeType.Pipe && prop != null && hw != null && hw)
            {
                var eyeAt = (Vector3)hw.cameraToWorldMatrix.GetColumn(3);
                seen = Mathf.Clamp01(.35f + Vector3.Dot(prop.transform.forward, (eyeAt - halo.transform.position).normalized));
            }
            SetColor(halo, new Color(1f, .3f + .2f * glow, .06f, 1f) * k * (lit.Smoke == SmokeType.Pipe ? .45f : .3f * seen));
        }
        if (light != null) light.intensity = .05f + .35f * glow;
        if (emberCap != null && emberCap.GetComponent<MeshRenderer>() is { } ecr && ecr.sharedMaterial != null)
            ecr.sharedMaterial.color = new Color(.55f + .45f * k, .5f + .5f * k, .5f + .5f * k, 1f);
        if (bandMat != null) bandMat.color = new Color(.6f + .4f * k, .6f + .4f * k, .6f + .4f * k, 1f);
        if (tobaccoGlow != null) tobaccoGlow.color = new Color(1f, 1f, 1f, Mathf.Clamp01((.3f + .7f * glow) * flicker));
        if (ember != null) Wisp(ember.transform.position, ember.layer, glow);
    }

    /// <summary>After the game's animation each frame: fingers opened, the smoke put in the hand where it is now; on a drag the arm bends
    /// (shoulder and elbow, two-bone) so the filter or mouthpiece comes to the mouth, then the smoke is put in the hand again.</summary>
    // a pipe: brought to the mouth by the hand, left in the mouth with the hands down, taken out again at the end
    // taken out: the pipe is shown in the hand a moment, then raised to the mouth; put away: the hand comes up, takes it out back to the
    // hold in sight, then goes down out of view with it
    const float PipeRest = .6f, PipeAtMouthAt = 1.7f, PipeOutUp = .6f, PipeOutShown = 1.3f;
    public const float PipeHandsDown = 2.5f;
    public const float PipeOutroTime = 2.4f;

    // 11-13 (user screenshots: an orange dot on the ground under the player while smoking): the lit item itself, which stays in the
    // world at the player's feet while held, carries a point light (Items.EmberName, made for the friend's view in coop, plus the
    // torch's own). In the player's own hands they are switched off; the friend's copy is another object and keeps its light
    static IntPtr lightsFor;
    static Light[]? ownLights;
    static float ownFxNext;

    static void OwnItemLights(GearItem? item)
    {
        try
        {
            if (item == null) return;
            if (lightsFor != item.Pointer || ownLights == null)
            {
                lightsFor = item.Pointer;
                ownLights = item.GetComponentsInChildren<Light>(true).ToArray();
                Safe.Info($"Lit item's own lights switched off in the player's hands: {ownLights.Length}");
            }
            foreach (var l in ownLights) if (l != null && l && l.enabled) l.enabled = false;
            // and the torch's own flame effects, wherever the game keeps them on the item
            if (Time.unscaledTime >= ownFxNext)
            {
                ownFxNext = Time.unscaledTime + .5f;
                foreach (var r in item.GetComponentsInChildren<ParticleSystemRenderer>(true)) if (r != null && r.enabled && !IsOurs(r.transform)) r.enabled = false;
            }
        }
        catch (Exception e) { Safe.Log("Own item lights", e); }
    }

    public static void Late(GearItem item, Def lit, float drag, float since, float outro)
    {
        FirstPersonWeapon? rig = null;
        OwnItemLights(item);
        lateSince = since;
        lateDrag = drag; lateOutro = outro;
        faceOk = drag <= 0f && outro < 0f && since >= .4f && !Tune.Frozen;
        try
        {
            rig = HandRig(item, out var weapon);
            viewWeapon = weapon;
            if (rig != null && Hands.Palm(rig.transform) != null && lit.Smoke != SmokeType.Pipe) { Hands.Open(1f); if (lit.Smoke == SmokeType.Cigar) Hands.Spread(Settings.CigarSpread.Value * Mathf.Clamp(propRadius / .0095f, .6f, 1.5f));
                // 11-09: slim cigarettes hung in the air between the fingers: the fingers close in by how much thinner than a regular one (r 3.9 mm)
                else if (propRadius < .0037f && Settings.SlimSqueeze.Value > 0f) Hands.Spread(-Settings.SlimSqueeze.Value * Mathf.Clamp01((.0039f - propRadius) / .0013f));;
                if (lit.Smoke == SmokeType.Cigarette) Hands.BendIndex(Settings.CigIndexBend.Value); };
        }
        catch (Exception e)
        {
            Safe.Log("Fingers", e);
        }
        try
        {
            RestoreArms(rig);
            if (eyeBone != null) { ViewLock.Eye = eyeBone; ViewLock.Frame = Anchor(); }
            // 11-09 evening (log 1831): after a scene change our pose stopped for 2 s while the game's camera swung from looking at the
            // ground to straight on; the view reference was the old one, so the arms were carried 2 m off and the lock was taken there
            // (huge hand at the camera). Now when the carried eye is far off, nothing is carried this frame and the rest is taken again.
            // a smoke back from a scene change: the rest pose from before it, not one taken in the settling view
            if (ResumeLock && !haveRest && savedRestHave && Time.unscaledTime - savedRestAt < 120f)
            {
                restLocal = savedRestLocal; restRot = savedRestRot; shownLocal = savedShownLocal; shownRot = savedShownRot; pipeHandFromEye = savedPipeHand; pipeHandSet = savedPipeHandSet;
                haveRest = true; restFixed = true; savedRestHave = false;
                Safe.Info("Rest pose kept from before the scene change");
            }
            bool retake = false;
            if (haveRest && eyeBone != null && !ViewLock.Locked && Settings.FollowView.Value && ViewLock.Carry(eyeBone, out var cp, out _)
                && eyeBone.InverseTransformPoint(cp).sqrMagnitude > .3f * .3f)
            {
                retake = true;
                ViewLock.Clear();
                SettleUntil = Math.Max(SettleUntil, Time.unscaledTime + .5f);
                Safe.Info($"View-carried eye {eyeBone.InverseTransformPoint(cp):F2} m off: reference dropped, rest pose taken again");
            }
            CarryArms();
            GuardBones();
            if (Hands.Wrist != null) wristBase = Hands.Wrist.localRotation;
            Hold(lit);
            if (rig == null || prop == null) return;
            Tune.Keys(lit);
            // 11-09 evening (log 1901): after a scene change the lock re-taken in the settling view put the hand elsewhere (camera in smoke
            // space (-0.26, 0.41, 0.04) / (-0.36, 0.32, -0.32) instead of about (-0.29, 0.31, -0.13)): the lock from before is put back at once
            if (ResumeLock && haveRest && !ViewLock.Locked && outro < 0f && drag <= 0f)
            {
                ViewLock.Prop = prop.transform;
                Transform? clav = null;
                for (var t = prop.transform.parent; t != null; t = t.parent) if (t.name == "RightClavJoint") { clav = t; break; }
                ViewLock.Bone = clav;
                if (ViewLock.Relock(WeaponCam())) { ResumeLock = false; SettleUntil = Time.unscaledTime; }
            }
            bool settling = lit.Smoke == SmokeType.Pipe && outro < 0f && since >= PipeHandsDown && Time.unscaledTime < SettleUntil;
            if (lit.Smoke == SmokeType.Pipe && (since < .3f || retake || settling)) KeepRest();
            // a pipe back from a scene change: unseen until the view has settled, then straight in the mouth
            if (lit.Smoke == SmokeType.Pipe && outro < 0f && prop.activeSelf == settling) prop.SetActive(!settling);
            if (haveRest && !retake && !ViewLock.Locked && Time.unscaledTime >= SettleUntil && since >= (lit.Smoke == SmokeType.Pipe ? .32f : 1.02f) && drag <= 0f && outro < 0f)
            {
                ViewLock.Prop = prop.transform;
                Transform? clav = null;
                for (var t = prop.transform.parent; t != null; t = t.parent) if (t.name == "RightClavJoint") { clav = t; break; }
                ViewLock.Bone = clav;
                ResumeLock = false;
                ViewLock.Anchor(WeaponCam(), lit.Smoke == SmokeType.Pipe ? null : (Vector2)Settings.Vec(Settings.RestOnScreen));
            }
            Markers(since);
            if (!dumped) { Dump(rig); MelonLoader.MelonCoroutines.Start(CamProbe()); }
            if (Settings.PoseOff.Value) return;
            if (lit.Smoke == SmokeType.Pipe)
            {
                Pipe(rig, lit, since, outro);
                return;
            }
            restCigar = lit.Smoke == SmokeType.Cigar;
            RestHand();
            Hold(lit);
            if (!haveRest || retake || (drag <= 0f && outro < 0f && (since < 1f || (lit.Smoke != SmokeType.Pipe && Time.unscaledTime < SettleUntil)))) KeepRest();
            if (Tune.Frozen && outro < 0f) drag = 1f;
            if (outro >= 0f)
            {
                Flick(rig, lit, outro);
                return;
            }
            if (drag > 0f) Drag(rig, lit, drag);
            else if (haveRest && since >= 1f && Settings.FollowView.Value && !ViewLock.Locked)
            {
                // at rest too the hand keeps its place on screen when looking up and down
                restOnly = true;
                try { Reach(rig, lit, 1f); } finally { restOnly = false; }
                Hold(lit);
            }
            // 11-09 evening (user, cigar screenshot): the thumb with a cigarette must lie exactly as with a cigar: same pose for both
            if ((lit.Smoke == SmokeType.Cigar || lit.Smoke == SmokeType.Cigarette) && prop != null && Settings.CigarThumb.Value > 0f)
                // along the body only: from a little in from the cap (the thumb went into the end of long cigars) to the ember
            {
                // as before (from the cap to the ember), only the thumb's tip aimed a little higher on screen, off the cap end
                var a = prop.transform.position;
                var b = prop.transform.TransformPoint(0f, 0f, emberZ * keepNow);
                var lift = Vector3.zero;
                var hc = HandsCamera(prop.layer);
                if (hc != null)
                {
                    var up = Vector3.ProjectOnPlane(ScreenUp(hc), b - a);
                    if (up.sqrMagnitude > 1e-6f) lift = up.normalized * Settings.CigarThumbLift.Value;
                }
                Hands.ThumbTo(a + lift, b + lift, Settings.CigarThumb.Value);
                // 11-09: the thumb should stand up off the cigar, not lie over it: its tip goes higher on screen
                // 11-09 evening: on a drag the standing thumb came into the camera: it stands less and goes a little down then
                if (hc != null && lift.sqrMagnitude > 0f && Settings.CigarThumbStand.Value > 0f)
                {
                    float d01 = Mathf.Clamp01(drag);
                    var by = lift.normalized * (Settings.CigarThumbStand.Value * (1f - d01) - Settings.CigarThumbDragDown.Value * d01);
                    if (by.sqrMagnitude > 1e-10f) Hands.LiftThumbTip(by, 40f);
                }
            }
            else if (lit.Smoke == SmokeType.Cigarette && prop != null && Settings.CigThumbLift.Value > 0f)
            {
                // 11-09: the thumb's tip lay over the cigarette (over its end when short): only the tip goes a little higher on screen
                var hc = HandsCamera(prop.layer);
                if (hc != null)
                {
                    var up = Vector3.ProjectOnPlane(ScreenUp(hc), prop.transform.forward);
                    if (up.sqrMagnitude > 1e-6f) Hands.LiftThumbTip(up.normalized * Settings.CigThumbLift.Value, 35f);
                }
                // 11-09 evening: the tip turned in towards the index finger (it pointed away from it)
                if (Settings.CigThumbToIndex.Value > 0f) Hands.ThumbTipToIndex(Settings.CigThumbToIndex.Value, 25f);
                if (Settings.CigThumbTipTurn.Value > 0f) Hands.TurnThumbTipToIndex(Settings.CigThumbTipTurn.Value);
            }
            if (lit.Smoke == SmokeType.Cigar && prop != null && Settings.CigarPinkyDown.Value > 0f)
            {
                // 11-13 (user): with a cigar too the little finger a little lower
                var hc = HandsCamera(prop.layer);
                if (hc != null) Hands.MovePinky(-ScreenUp(hc) * Settings.CigarPinkyDown.Value, 25f);
            }
            if (lit.Smoke == SmokeType.Cigarette && prop != null && Settings.CigPinkyDown.Value > 0f)
            {
                // 11-09: the little finger a little lower on screen
                var hc = HandsCamera(prop.layer);
                if (hc != null) Hands.MovePinky(-ScreenUp(hc) * Settings.CigPinkyDown.Value, 25f);
            }

        }
        catch (Exception e)
        {
            Safe.Log("Hold", e);
        }
        finally
        {
            try { SmoothRest(lit.Smoke != SmokeType.Pipe && drag <= 0f && outro < 0f && since >= 1.02f); } catch (Exception e) { Safe.Log("Smooth", e); }
            KeepBones();
            if (lit.Smoke == SmokeType.Cigarette) { faceFree = faceOk; FaceName(); }
            if (prop != null) { ViewLock.Prop = prop.transform; ViewLock.PropSetAt = prop.transform.position; ViewLock.PropSet = true; ViewLock.AfterPose(); }
        }
    }

    static Vector3 printDir = Vector3.down;
    static bool faceLogged, faceFree, faceHave, faceOk;
    static float faceRoll;

    /// <summary>Which side of the held cigarette carries the print (u = .5 of the paper): the mean normal of the body vertices there.</summary>
    static void PrintSide(Mesh mesh)
    {
        try
        {
            var uv = mesh.uv; var n = mesh.normals;
            var sum = Vector3.zero;
            for (int i = 0; i < uv.Length && i < n.Length; i++)
                if (Mathf.Abs(uv[i].x - .5f) < .03f && Mathf.Abs(n[i].z) < .3f) sum += n[i];
            sum.z = 0f;
            printDir = sum.sqrMagnitude > 1e-6f ? sum.normalized : Vector3.down;
            Safe.Info($"Cigarette print side {printDir:F2}");
        }
        catch (Exception e) { Safe.Log("Cigarette print side", e); }
    }

    /// <summary>The brand is printed once (u = .5, the held mesh's local -Y): the cigarette is rolled about its own axis so that side
    /// faces the camera and the name reads straight (a round stick looks the same whichever way it is rolled).</summary>
    static void FaceName()
    {
        try
        {
            if (prop == null || !Settings.CigNameToView.Value) return;
            // during a drag or the flick the roll found at rest is kept (fixed in the fingers): following the camera there made it jump
            if (!faceFree)
            {
                if (faceHave) prop.transform.rotation = Quaternion.AngleAxis(faceRoll, prop.transform.forward) * prop.transform.rotation;
                return;
            }
            var hv = HandsCamera(prop.layer);
            if (hv == null) return;
            var axis = prop.transform.forward;
            // the camera that really draws the hands (eye-local 0,1.75,0); the hands-view basis sits at the eye bone, behind the smoke
            // used only when its projection really puts the smoke on screen (the game draws the hands with its own matrices)
            var wc = WeaponCam();
            bool wcOk = false;
            if (wc != null)
            {
                var vp = wc.WorldToViewportPoint(prop.transform.position);
                wcOk = vp.z > 0f && vp.x > -.2f && vp.x < 1.2f && vp.y > -.2f && vp.y < 1.2f;
            }
            var eye = wcOk ? (Vector3)wc!.cameraToWorldMatrix.GetColumn(3) : hv.transform.position;
            if (!faceLogged)
            {
                faceLogged = true;
                Safe.Info($"Cigarette name to view: weapon camera {(wc != null)} usable {wcOk}, camera-smoke {(eye - prop.transform.position).magnitude:0.000} m, eye bone-smoke {(hv.transform.position - prop.transform.position).magnitude:0.000} m");
            }
            var toCam = Vector3.ProjectOnPlane(eye - prop.transform.position, axis);
            if (toCam.sqrMagnitude < 1e-8f) toCam = Vector3.ProjectOnPlane(-hv.transform.forward, axis);
            if (toCam.sqrMagnitude < 1e-8f) return;
            // turn about the axis so the printed side's normal points at the view
            var now = Vector3.ProjectOnPlane(prop.transform.TransformDirection(printDir), axis);
            if (now.sqrMagnitude < 1e-8f) return;
            float want = Vector3.SignedAngle(now, toCam, axis) + Settings.CigNameRoll.Value;
            faceRoll = faceHave ? Mathf.LerpAngle(faceRoll, want, Mathf.Clamp01(Time.deltaTime * 6f)) : want;
            faceHave = true;
            prop.transform.rotation = Quaternion.AngleAxis(faceRoll, axis) * prop.transform.rotation;
        }
        catch (Exception e) { Safe.Log("Cigarette name to view", e); }
    }

    // ---- the arm bones: if the game's animation does not write them every frame, the drag's bend would stay; put back to the pose
    // the animation had when the smoke started whenever they still hold exactly what we left last frame
    static readonly Transform?[] guarded = new Transform?[4];
    static readonly Quaternion[] guardRest = new Quaternion[4], guardLeft = new Quaternion[4];
    static bool guardInit, guardLogged;
    static int guardHits;

    static void GuardBones()
    {
        var w = Hands.Wrist;
        if (w == null) return;
        if (!guardInit || guarded[2] != w)
        {
            guarded[0] = Hands.Shoulder; guarded[1] = Hands.Elbow; guarded[2] = w; guarded[3] = w.childCount > 0 ? w.GetChild(0) : null;
            guardInit = true;
            for (int i = 0; i < 4; i++) if (guarded[i] != null) guardRest[i] = guardLeft[i] = guarded[i]!.localRotation;
        }
        for (int i = 0; i < 4; i++)
        {
            var b = guarded[i];
            if (b == null) continue;
            // still exactly what we left last frame: the animation did not write it, so take back only our own change (the pose it had
            // before our posing last frame); otherwise this frame's animated pose is the base
            if (Quaternion.Angle(b.localRotation, guardLeft[i]) < .001f)
            {
                b.localRotation = guardRest[i];
                guardHits++;
            }
            guardRest[i] = b.localRotation;
        }
        if (!guardLogged && guardHits > 30)
        {
            guardLogged = true;
            Safe.Info("Arm bones are not animated every frame: our pose is taken back each frame");
        }
    }

    // ---- the resting hand trembled: each frame's pose (the game's arm animation, our turns of the wrist) differs a little from the last.
    // At rest the arm bones' final turns are eased from frame to frame, so only slow moves come through
    static readonly Quaternion[] smooth = new Quaternion[3];
    static readonly Transform?[] smoothBone = new Transform?[3];
    static bool smoothLogged;

    static void SmoothRest(bool atRest)
    {
        float rate = Settings.RestSmooth.Value;
        float k = 1f - Mathf.Exp(-Mathf.Clamp(Time.unscaledDeltaTime, .001f, .1f) * Math.Max(rate, .01f));
        for (int i = 0; i < 3; i++)
        {
            var b = i == 0 ? Hands.Shoulder : i == 1 ? Hands.Elbow : Hands.Wrist;
            if (b == null || !b) { smoothBone[i] = null; continue; }
            var cur = b.localRotation;
            if (!atRest || rate <= 0f || smoothBone[i] != b || Quaternion.Angle(smooth[i], cur) > 25f) { smooth[i] = cur; smoothBone[i] = b; continue; }
            smooth[i] = Quaternion.Slerp(smooth[i], cur, k);
            b.localRotation = smooth[i];
        }
        if (atRest && rate > 0f && !smoothLogged) { smoothLogged = true; Safe.Info($"Resting arm eased frame to frame (rate {rate:0.#}/s)"); }
    }

    static void KeepBones()
    {
        for (int i = 0; i < 4; i++) if (guarded[i] != null) guardLeft[i] = guarded[i]!.localRotation;
    }

    // ---- calibration dots: where points around the hands' eye bone really show on screen (the user screenshots them)
    static readonly List<GameObject> markers = new();
    static readonly List<Vector3> markerPts = new();
    static bool markersLogged;

    static void Markers(float since)
    {
        bool want = Settings.Markers.Value && since < 25f && eyeBone != null && prop != null;
        if (!want)
        {
            ClearMarkers();
            return;
        }
        if (markers.Count > 0)
        {
            // kept at their eye-bone points; hung under the hand rig because the game draws only the rig's own children
            for (int i = 0; i < markers.Count && i < markerPts.Count; i++)
                if (markers[i] != null) markers[i].transform.position = eyeBone!.TransformPoint(markerPts[i]);
            return;
        }
        var pts = new (Vector3 p, Color c, string n)[]
        {
            (new Vector3(0f, 1f, 0f), Color.red, "red (0,1,0)"),
            (new Vector3(0f, 1f, .25f), Color.green, "green (0,1,+.25)"),
            (new Vector3(0f, 1f, -.25f), Color.yellow, "yellow (0,1,-.25)"),
            (new Vector3(.25f, 1f, 0f), Color.blue, "blue (+.25,1,0)"),
            (new Vector3(-.25f, 1f, 0f), Color.white, "white (-.25,1,0)"),
            (new Vector3(0f, .5f, 0f), Color.magenta, "magenta (0,.5,0)"),
            (new Vector3(0f, 0f, .5f), Color.cyan, "cyan (0,0,.5)"),
        };
        var sh = Shader.Find("Sprites/Default");
        foreach (var (p, c, n) in pts)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            go.name = "SmokesMarker";
            go.layer = prop!.layer;
            go.transform.SetParent(prop!.transform.parent, false);
            go.transform.position = eyeBone!.TransformPoint(p);
            markerPts.Add(p);
            go.transform.localScale = Vector3.one * (.025f * Math.Max(.3f, p.magnitude));
            var r = go.GetComponent<MeshRenderer>();
            if (r != null && sh != null) r.material = new Material(sh) { color = c };
            markers.Add(go);
        }
        if (!markersLogged)
        {
            markersLogged = true;
            Safe.Info("Calibration dots on the eye bone (x,y,z local): " + string.Join(", ", Array.ConvertAll(pts, x => x.n)));
        }
    }

    static void ClearMarkers()
    {
        if (markers.Count == 0) return;
        foreach (var m in markers) try { if (m != null) UnityEngine.Object.Destroy(m); } catch { }
        markers.Clear();
        markerPts.Clear();
    }

    static bool dumped;

    /// <summary>Once per smoke, after the frame is drawn: every camera (also disabled ones, the weapon camera first) with its matrices as
    /// they were used, and where the smoke is in its viewport; the hands' real view comes from this.</summary>
    static System.Collections.IEnumerator CamProbe()
    {
        yield return new WaitForEndOfFrame();
        try
        {
            if (prop == null) yield break;
            var at = prop.transform.position;
            var list = new List<Camera>();
            var vp = GameManager.GetVpFPSCamera();
            Camera? wc = null;
            try { wc = vp != null ? vp.GetWeaponCamera() : null; } catch { }
            if (wc == null) try { wc = vp != null ? vp.m_WeaponCamera : null; } catch { }
            if (wc != null) list.Add(wc);
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Camera>()) if (c != null && (wc == null || c.Pointer != wc.Pointer)) list.Add(c);
            string M(Matrix4x4 m) => $"[{m.m00:F3} {m.m01:F3} {m.m02:F3} {m.m03:F3} | {m.m10:F3} {m.m11:F3} {m.m12:F3} {m.m13:F3} | {m.m20:F3} {m.m21:F3} {m.m22:F3} {m.m23:F3} | {m.m30:F3} {m.m31:F3} {m.m32:F3} {m.m33:F3}]";
            foreach (var c in list)
            {
                var t = c.transform;
                string eye = eyeBone != null ? $"eye-local pos {eyeBone.InverseTransformPoint(t.position):F3} fwd {eyeBone.InverseTransformDirection(t.forward):F3} up {eyeBone.InverseTransformDirection(t.up):F3}" : "no eye bone";
                Safe.Info($"Cam probe {(c == wc ? "WEAPON " : "")}{c.name}: enabled {c.enabled} active {c.gameObject.activeInHierarchy} fov {c.fieldOfView:0.0} near {c.nearClipPlane:0.000} mask {c.cullingMask:X} depth {c.depth}; {eye}; smoke viewport {c.WorldToViewportPoint(at):F3}");
                Safe.Info($"Cam probe {c.name}: view {M(c.worldToCameraMatrix)} proj {M(c.projectionMatrix)}");
            }
        }
        catch (Exception e)
        {
            Safe.Log("Cam probe", e);
        }
    }

    /// <summary>Once per smoke: where the hand, the smoke and the arms rig's own camera bone are, seen from the player camera.</summary>
    static void Dump(FirstPersonWeapon rig)
    {
        dumped = true;
        try
        {
            var cam = GameManager.GetVpFPSCamera();
            if (cam == null) return;
            var ct = cam.transform;
            string P(Transform? t) => t == null ? "-" : ct.InverseTransformPoint(t.position).ToString("F3");
            string D(Vector3 v) => ct.InverseTransformDirection(v).ToString("F2");
            var arms = ArmsRoot(rig.transform);
            Transform? camBone = null;
            foreach (var t in arms.GetComponentsInChildren<Transform>(true))
                if (t.name == "camera" || t.name == "camera_FOV") { camBone = t; if (t.name == "camera") break; }
            var c = cam.GetComponent<Camera>() ?? Camera.main;
            string vp = prop != null && c != null ? c.WorldToViewportPoint(prop.transform.position).ToString("F2") : "-";
            Safe.Info($"Pose dump: camera {cam.name} pos {ct.position:F2} fwd {ct.forward:F2} lossyScale arms {arms.lossyScale:F2}");
            Safe.Info($"Pose dump: smoke {P(prop?.transform)} (viewport by {c?.name} {vp}), palm {P(Hands.Wrist != null ? Hands.Wrist.GetChild(0) : null)}, wrist {P(Hands.Wrist)}, elbow {P(Hands.Elbow)}, shoulder {P(Hands.Shoulder)}");
            Safe.Info($"Pose dump: arms {arms.name} at {P(arms)} fwd {D(arms.forward)} up {D(arms.up)}; rig bone {(camBone != null ? camBone.name : "none")} at {P(camBone)} fwd {(camBone != null ? D(camBone.forward) : "-")} up {(camBone != null ? D(camBone.up) : "-")} right {(camBone != null ? D(camBone.right) : "-")}");
            if (Hands.Frame(out var f, out var bk)) Safe.Info($"Pose dump: fingers {D(f)}, back of hand {D(bk)}");
            foreach (var cc in Camera.allCameras)
                if (cc != null) Safe.Info($"Pose dump: camera {cc.name} at {ct.InverseTransformPoint(cc.transform.position):F3} fwd {D(cc.transform.forward)} fov {cc.fieldOfView:0} mask {cc.cullingMask:X}");
        }
        catch (Exception e)
        {
            Safe.Log("Pose dump", e);
        }
    }

    /// <summary>The resting hand turned at the wrist from the torch grip to a smoker's: fingers forward and in, back of the hand up.</summary>
    static Vector3 restDirF, restDirB, restDirKey;
    static bool restDirHave;

    static bool restCigar;
    static Quaternion restFree = Quaternion.identity;   // from the wrist as it lies (limited) to where it would lie without the limit

    static void RestHand()
    {
        var w = Hands.Wrist;
        if (w == null || prop == null || !Hands.Frame(out var f, out var bk)) return;
        var cam = HandsCamera(prop.layer);
        if (cam == null) return;
        var ct = cam.transform;
        var F = InView(ct, Settings.Vec(Settings.RestFingers));
        var B = InView(ct, Settings.Vec(Settings.RestBack));
        // the eye bone sways with the walk (secondary physics) while the arms do not: directions taken from it every frame shook the hand.
        // they are taken once and kept in the arms frame (Camera_Weapon_Offset), which the hands sit in
        var a = Anchor();
        if (a != null)
        {
            var key = Settings.Vec(Settings.RestFingers) * 7f + Settings.Vec(Settings.RestBack) * 13f;
            if (!restDirHave || key != restDirKey)
            {
                restDirF = a.InverseTransformDirection(F);
                restDirB = a.InverseTransformDirection(B);
                restDirKey = key;
                restDirHave = true;
            }
            F = a.TransformDirection(restDirF);
            B = a.TransformDirection(restDirB);
        }
        var q = Quaternion.LookRotation(F, B) * Quaternion.Inverse(Quaternion.LookRotation(f, bk));
        float turn = Mathf.Clamp01(Settings.RestHandTurn.Value);
        // the wrist may not bend far from the forearm: past ~30° the hand mesh opens and the empty sleeve shows.
        // only the extra bend (beyond what the game's own pose has) is limited; the rest of the turn is given up
        var elbow = Hands.Elbow ?? w.parent;
        // cigars only: with cigarettes the limit kept the hand in the torch pose (the cigarette stood up out of the fist, the drag broke)
        // 11-13 (user): the resting hand with a cigarette lies as with a cigar, so the same limit holds for it too. But only at rest:
        // it comes in after the rest pose is kept (1 s) and goes in the first third of a drag, so the kept rest pose and the mouth poses
        // made from it are as they were (on 11-09 the limit on all the time broke the cigarette's drag)
        // 18:05 user: no turning in and out at all: the hand with a cigarette is as with a cigar from the moment it is taken out.
        // The rest pose that the mouth poses are made from is still kept as it would be without the limit (restFree, used in KeepRest),
        // so the cigarette comes to the mouth exactly as before; the way there starts from the hand as it lies now
        float like = restCigar || Settings.CigRestLikeCigar.Value ? 1f : 0f;
        restFree = Quaternion.identity;
        if (elbow != null && like > 0f)
        {
            float free = turn;
            var arm = (w.position - elbow.position).normalized;
            float own = Vector3.Angle(arm, f), max = Math.Max(own, Settings.RestWristBend.Value);
            if (Vector3.Angle(arm, Quaternion.Slerp(Quaternion.identity, q, turn) * f) > max)
            {
                float lo = 0f, hi = turn;
                for (int i = 0; i < 10; i++)
                {
                    float mid = (lo + hi) * .5f;
                    if (Vector3.Angle(arm, Quaternion.Slerp(Quaternion.identity, q, mid) * f) > max) hi = mid; else lo = mid;
                }
                turn = Mathf.Lerp(free, lo, like);
                if (!restCigar) restFree = Quaternion.Slerp(Quaternion.identity, q, free) * Quaternion.Inverse(Quaternion.Slerp(Quaternion.identity, q, turn));
            }
        }
        w.rotation = Quaternion.Slerp(Quaternion.identity, q, turn) * w.rotation;
    }

    // throwing a cigarette or cigar away: a flick of the hand forward, let go at its top
    public const float FlickTime = 1.35f;
    const float FlickRelease = .32f;
    static bool flung;

    static void Flick(FirstPersonWeapon rig, Def lit, float t)
    {
        var w = Hands.Wrist;
        var cam = HandsCamera(prop!.layer);
        if (cam == null) return;
        var ct = cam.transform;
        // wind up back and up a little, then out forward fast, then the hand drops
        // after the release the swing eases out fully and the hand sinks out of view, so the end (back to the game's own pose) is unseen
        float after = t < FlickRelease ? 0f : S01((t - FlickRelease) / (FlickTime - .08f - FlickRelease));
        float swing = t < FlickRelease ? Mathf.Sin(t / FlickRelease * Mathf.PI * .5f) : 1f - after;
        float cock = t < FlickRelease * .5f ? S01(t / (FlickRelease * .5f)) : 1f - S01((t - FlickRelease * .5f) / (FlickRelease * .5f));
        // 11-09 evening: a cigar's hand rests nearer the camera; the full flick brought the arm into it and the empty shoulder showed:
        // with a cigar the swing is smaller, the wrist turns less and the hand goes forward and a little down instead of up
        bool cigar = lit.Smoke == SmokeType.Cigar;
        float k = cigar ? Settings.CigarFlickScale.Value : 1f;
        if (w != null)
        {
            // the wrist cocks back, then snaps forward
            var turn = Quaternion.AngleAxis((-35f * cock + 30f * swing) * k, ct.right);
            w.rotation = turn * w.rotation;
        }
        MoveArms(rig, ct.forward * (.12f * swing * k) + ct.up * (cigar ? -.03f * swing : .04f * swing - .03f * cock) + ct.right * (.03f * swing * k));
        if (after > 0f) HandAway(rig, HandDown(cam) * after);
        LimitWrist(Settings.RestWristBend.Value);
        Hold(lit);
        if (t >= FlickRelease && !flung)
        {
            flung = true;
            Throw(lit);
            prop.SetActive(false);
        }
    }

    static Quaternion wristBase = Quaternion.identity;   // the wrist's own (game) turn this frame, before our poses

    /// <summary>The wrist may not bend from the forearm further than the game's own pose or maxDeg, whichever is more (past that the hand
    /// folds back unnaturally and the empty sleeve shows): our extra turn is given up as far as needed.</summary>
    static void LimitWrist(float maxDeg)
    {
        var w = Hands.Wrist;
        var elbow = Hands.Elbow ?? w?.parent;
        if (w == null || elbow == null || w.parent == null || !Hands.Frame(out var f, out _)) return;
        var arm = (w.position - elbow.position).normalized;
        var fLocal = Quaternion.Inverse(w.rotation) * f;
        var cur = w.localRotation;
        Vector3 F(Quaternion local) => w.parent.rotation * (local * fLocal);
        float max = Math.Max(Vector3.Angle(arm, F(wristBase)), maxDeg);
        if (Vector3.Angle(arm, F(cur)) <= max) return;
        float lo = 0f, hi = 1f;
        for (int i = 0; i < 10; i++)
        {
            float mid = (lo + hi) * .5f;
            if (Vector3.Angle(arm, F(Quaternion.Slerp(wristBase, cur, mid))) > max) hi = mid; else lo = mid;
        }
        w.localRotation = Quaternion.Slerp(wristBase, cur, lo);
    }

    static Vector3 HandDown(HandsView cam) =>
        eyeBone != null && Settings.ByOffset.Value ? EyeVector(Settings.Vec(Settings.PipeHandAway)) : -cam.transform.up * .45f - cam.transform.forward * .1f;

    /// <summary>The empty hand moved by "by" with the arm bones (the arms rig root does not move visibly: the game puts it back).</summary>
    static void HandAway(FirstPersonWeapon rig, Vector3 by)
    {
        var w = Hands.Wrist;
        if (w == null || Hands.Elbow == null || Hands.Shoulder == null) { MoveArms(rig, by); return; }
        if (by.sqrMagnitude < 1e-8f) return;
        var hint = eyeBone != null ? EyeVector(new Vector3(0f, -1f, 0f)) : -w.up;
        var target = w.position + by;
        TwoBone(Hands.Shoulder, Hands.Elbow, w, target, hint);
        var miss = target - w.position;
        if (miss.sqrMagnitude > .0001f) MoveArms(rig, miss);
    }

    /// <summary>A copy of the mesh drawn from both sides (every triangle also turned the other way, on its own vertices with the normals
    /// turned round): the held smoke is seen from whatever side, whatever the rig's mirroring does to the winding.</summary>
    static Mesh TwoSided(Mesh src)
    {
        var v = src.vertices.ToArray();
        var n = src.normals.ToArray();
        var u = src.uv.ToArray();
        var t = src.triangles.ToArray();
        int nv = v.Length;
        if (n.Length != nv) n = new Vector3[nv];
        if (u.Length != nv) u = new Vector2[nv];
        var v2 = new Vector3[nv * 2];
        var n2 = new Vector3[nv * 2];
        var u2 = new Vector2[nv * 2];
        for (int i = 0; i < nv; i++)
        {
            v2[i] = v2[i + nv] = v[i];
            n2[i] = n[i];
            n2[i + nv] = -n[i];
            u2[i] = u2[i + nv] = u[i];
        }
        var t2 = new int[t.Length * 2];
        for (int i = 0; i + 2 < t.Length; i += 3)
        {
            t2[i] = t[i]; t2[i + 1] = t[i + 1]; t2[i + 2] = t[i + 2];
            t2[t.Length + i] = t[i] + nv; t2[t.Length + i + 1] = t[i + 2] + nv; t2[t.Length + i + 2] = t[i + 1] + nv;
        }
        var m = new Mesh { name = src.name + "_2s", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.vertices = v2;
        m.normals = n2;
        m.uv = u2;
        m.uv2 = u2;
        var white = new Color32[nv * 2];
        for (int i = 0; i < white.Length; i++) white[i] = new Color32(255, 255, 255, 255);
        m.colors32 = white;
        m.triangles = t2;
        m.RecalculateBounds();
        return m;
    }

    static float S01(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

    /// <summary>For the coop figure: 0 the pipe in the hand, 1 the hand holds it at the mouth, 2 in the teeth with the hand away.</summary>
    public static int PipePhase;

    static void Pipe(FirstPersonWeapon rig, Def lit, float since, float outro)
    {
        PipePhase = PipePhaseAt(since, outro);
        var cam = HandsCamera(prop!.layer);
        if (cam == null) return;
        // the empty hand goes down out of view (lower eye-bone Y = lower on screen, as the tests showed)
        // right and down, away from the pipe in the teeth (it used to go back toward the face, through the bowl)
        var down = HandDown(cam);
        if (outro < 0f)
        {
            if (since < PipeAtMouthAt)
            {
                // in the hand, to the mouth
                float k = S01((since - PipeRest) / (PipeAtMouthAt - PipeRest));
                if (k > 0f)
                {
                    Reach(rig, lit, k, Vector3.zero, S01(k * 1.8f), Settings.PipeWristTurn.Value);
                    Hold(lit);
                }
                PipeThumb();
                return;
            }
            // in the mouth; the hand lets go and goes down out of view
            float low = S01((since - PipeAtMouthAt) / (PipeHandsDown - PipeAtMouthAt));
            if (low < 1f) Reach(rig, lit, 1f - low, Vector3.zero, -1f, Settings.PipeWristTurn.Value);
            HandAway(rig, down * low);
            InMouth(lit);
            return;
        }
        if (outro < PipeOutUp)
        {
            // the hand comes up to the pipe in the mouth
            float up = S01(outro / PipeOutUp);
            Reach(rig, lit, up, Vector3.zero, S01(outro / PipeOutUp * 1.8f), Settings.PipeWristTurn.Value);
            HandAway(rig, down * (1f - up));
            InMouth(lit);
            return;
        }
        if (outro < PipeOutShown)
        {
            // out of the mouth, straight first, back to the hold in sight
            float o = S01((outro - PipeOutUp) / (PipeOutShown - PipeOutUp));
            Reach(rig, lit, 1f - o, Vector3.zero, 1f - S01((o - .35f) / .65f), Settings.PipeWristTurn.Value);
            Hold(lit);
            PipeThumb();
            return;
        }
        // then the hand with the pipe goes down out of view (put away, not dropped in sight)
        float t = S01((outro - PipeOutShown) / (PipeOutroTime - .1f - PipeOutShown));
        HandAway(rig, down * t);
        Hold(lit);
        PipeThumb();
    }

    static int PipePhaseAt(float since, float outro)
    {
        if (outro < 0f)
        {
            if (since < PipeAtMouthAt) return since > PipeRest + (PipeAtMouthAt - PipeRest) * .6f ? 1 : 0;
            return since < (PipeAtMouthAt + PipeHandsDown) * .5f ? 1 : 2;
        }
        if (outro < PipeOutUp) return outro > PipeOutUp * .5f ? 1 : 2;
        if (outro < PipeOutShown) return outro < (PipeOutUp + PipeOutShown) * .5f ? 1 : 0;
        return 0;
    }

    /// <summary>Pipe in the fist: the thumb raised over the stem (it went through the stem), its tip onto a line just above the stem.</summary>
    static void PipeThumb()
    {
        if (prop == null || Settings.PipeThumb.Value <= 0f) return;
        float up = Settings.PipeThumbAbove.Value / Mathf.Max(1e-4f, Settings.PropScale.Value);
        Hands.LiftThumbBase(prop.transform.up, Settings.PipeThumbBaseLift.Value);
        // the thumb lies OVER the stem (0.1.67): the stem's real centre line (curved on bent pipes), the thumb pushed up out of it
        if (pipeStem.Count >= 2)
        {
            var pts = new Vector3[pipeStem.Count];
            for (int i = 0; i < pts.Length; i++) pts[i] = prop.transform.TransformPoint(pipeStem[i]);
            float r = prop.transform.TransformVector(new Vector3(0f, pipeStemR, 0f)).magnitude;
            Hands.ThumbOver(pts, prop.transform.up, r + Settings.PipeThumbOverGap.Value);
        }
        else
            Hands.ThumbAround(prop.transform.TransformPoint(0f, 0f, 0f), prop.transform.TransformPoint(0f, 0f, pipeBowl.z * .9f), Settings.PipeThumbClearance.Value);
    }

    /// <summary>The pipe from where the fingers hold it toward its place in the mouth (it turns in the fingers, the wrist does not spin).</summary>
    static void ToMouth(Def lit, float move, float turn)
    {
        var cam = HandsCamera(prop!.layer);
        if (cam == null) return;
        MouthPose(cam, lit, prop.transform.position, out var mp, out var mr);
        prop.transform.position = Vector3.Lerp(prop.transform.position, mp, S01((move - .5f) / .5f));
        prop.transform.rotation = Quaternion.Slerp(prop.transform.rotation, mr, turn);
    }

    /// <summary>Where the mouth end must be and how the smoke lies there, in the hands camera's view.</summary>
    static void MouthPose(HandsView cam, Def lit, Vector3 restAt, out Vector3 pos, out Quaternion rot)
    {
        var ct = cam.transform;
        bool pipe = lit.Smoke == SmokeType.Pipe;
        if (restOnly && haveRest && eyeBone != null)
        {
            Eye(out var rp, out var rq);
            pos = rp + rq * shownLocal;
            rot = rq * shownRot;
            return;
        }
        if (Settings.ByOffset.Value && haveRest && eyeBone != null)
        {
            // the pose in the hand, moved and turned by the tuned amounts (eye bone axes): no projection guesses
            Eye(out var ep, out var eq);
            pos = ep + eq * (restLocal + Settings.Vec(Tune.Offset(lit)));
            rot = eq * (Quaternion.Euler(Settings.Vec(Tune.Turn(lit))) * restRot);
            // with the hands view locked, the mouth end goes to a fixed place on that view, so the drag always looks the same and stays in sight
            if (ViewLock.OnView(Settings.Vec(Tune.OnScreen(lit)), out var onView))
            {
                pos = onView;
                // a cigarette or cigar points away from the face, a little up, so only its far part shows from the bottom edge
                if (lit.Smoke == SmokeType.Cigar && ViewLock.Axes(out var vr, out var vu, out var vf))
                {
                    var a = Settings.Vec(Settings.CigarAim) * Mathf.Deg2Rad;
                    var want = (vf * Mathf.Cos(a.x) * Mathf.Cos(a.y) + vu * Mathf.Sin(a.x) + vr * Mathf.Sin(a.y)).normalized;
                    rot = Quaternion.FromToRotation(rot * Vector3.forward, want) * rot;
                }
            }
            return;
        }
        if (pipe)
        {
            var m = Settings.Vec(Settings.PipeMouthAt);
            pos = cam.ViewportToWorldPoint(new Vector3(m.x, m.y, Math.Max(.08f, m.z)));
        }
        else
        {
            var m = Settings.Vec(Settings.MouthAt);
            float depth = Math.Max(.08f, Vector3.Dot(restAt - ct.position, ct.forward) * m.z);
            pos = cam.ViewportToWorldPoint(new Vector3(m.x, m.y, depth));
        }
        var fwd = InView(ct, Settings.Vec(pipe ? Settings.PipeMouthDir : Settings.CigMouthDir));
        var up = pipe ? ct.up : InView(ct, Settings.Vec(Settings.CigMouthUp));
        rot = Quaternion.LookRotation(fwd, up);
    }

    /// <summary>The pipe held in the teeth: fixed in the view, swaying with the head.</summary>
    static void InMouth(Def lit)
    {
        var cam = HandsCamera(prop!.layer);
        if (cam == null) return;
        MouthPose(cam, lit, prop.transform.position, out var pos, out var rot);
        prop.transform.rotation = rot;
        prop.transform.position = pos;
    }

    /// <summary>The view the hands are drawn from: no camera in TLD draws the hands' layer (they are drawn by the game itself with the
    /// weapon's own field of view), so this is the player camera's transform with that field of view.</summary>
    /// <summary>An eye: position and axes.</summary>
    sealed class Basis
    {
        public Vector3 position, forward = Vector3.forward, up = Vector3.up, right = Vector3.right;
    }

    /// <summary>The view the hands are drawn from. TLD keeps the first-person arms rig far from the player (hundreds of metres) and draws it
    /// from the rig's own "camera" bone with the weapon's field of view; so mouth, view directions and billboards are taken from that bone.</summary>
    sealed class HandsView
    {
        public Basis transform = new();
        public float fov = 30f;
        public Camera? cam;

        public Vector3 ViewportToWorldPoint(Vector3 v)
        {
            if (cam != null && cam) return cam.ViewportToWorldPoint(v);
            float t = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            var d = transform.forward + transform.right * ((v.x - .5f) * 2f * t * aspect) + transform.up * ((v.y - .5f) * 2f * t);
            return transform.position + d * v.z;
        }
    }

    static readonly HandsView view = new();
    static float viewLogged = -100f;
    static vp_FPSWeapon? viewWeapon;
    static Transform? eyeBone;
    static IntPtr eyeFor;
    static int axF = 4, axU = 2;        // which of the bone's ±x ±y ±z is forward / up (0..5: +x -x +y -y +z -z)
    static bool haveView;

    static Vector3 Axis(Transform t, int i)
    {
        var v = (i >> 1) == 0 ? t.right : (i >> 1) == 1 ? t.up : t.forward;
        return (i & 1) == 0 ? v : -v;
    }

    static int viewMode;               // 0 not chosen, 1 the rig's Unity camera (its own matrices), 2 the eye bone looking along its +Y
    static Camera? rigCam;

    static Vector3 Project(Basis b, float fov, Vector3 p)
    {
        var d = p - b.position;
        float z = Vector3.Dot(d, b.forward);
        float t = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        if (Mathf.Abs(z) < 1e-4f) z = 1e-4f;
        return new Vector3(.5f + Vector3.Dot(d, b.right) / z / (2f * t * aspect), .5f + Vector3.Dot(d, b.up) / z / (2f * t), z);
    }

    /// <summary>The rig's eye bone looks along its +Y with +Z up (fitted to screenshots: the resting hand at the lower right),
    /// pitched by the EyePitch setting; screen right is its +X.</summary>
    static void EyeAxes(Transform e, Basis b)
    {
        // the hands' camera stands 1.75 m above the bone (along its +Y) and looks back down its -Y, +Z up: the arm then runs from the
        // eye out to the hand (shoulder near, hand far), as the weapon camera's matrices say
        // (HandsEyeHeight > 0 tries that; 0 = the fitted view from the bone itself, which the user liked: 0.1.18)
        float a = Settings.EyePitch.Value * Mathf.Deg2Rad;
        b.right = e.right;
        if (Settings.EyeHeight.Value > 0f)
        {
            b.position = e.TransformPoint(new Vector3(0f, Settings.EyeHeight.Value, 0f));
            b.forward = (-e.up * Mathf.Cos(a) - e.forward * Mathf.Sin(a)).normalized;
            b.up = (-e.up * Mathf.Sin(a) + e.forward * Mathf.Cos(a)).normalized;
            return;
        }
        b.position = e.position;
        b.forward = (e.up * Mathf.Cos(a) - e.forward * Mathf.Sin(a)).normalized;
        b.up = (e.up * Mathf.Sin(a) + e.forward * Mathf.Cos(a)).normalized;
    }

    static bool OnScreen(Vector3 v) => v.z > 0f && v.x > -.25f && v.x < 1.25f && v.y > -.25f && v.y < 1.25f;

    /// <summary>Up on the screen: the weapon camera's own up (11-09: the eye-bone basis' up moved the thumb towards the viewer, not up).</summary>
    static Vector3 ScreenUp(HandsView hc)
    {
        var wc = WeaponCam();
        var up = wc != null && wc ? wc.transform.up : hc.transform.up;
        // 11-13: as the view stands when levelled (lit while looking down, "up" leaned forward and the thumb went the wrong way)
        if (wc != null && wc) { ViewLock.LevelTurn(wc, out var lev); up = lev * up; }
        // 11-09 evening: taken from the camera every frame, the little finger (and the thumb) went up and down with the look;
        // now taken once at rest, after the view has settled, and kept fixed in the hand
        var w = Hands.Wrist;
        if (w == null) return up;
        if (!upHave)
        {
            if (lateSince < 1.02f || Time.unscaledTime < SettleUntil) return up;
            upInWrist = w.InverseTransformDirection(up);
            upHave = true;
        }
        return w.TransformDirection(upInWrist);
    }
    static Vector3 upInWrist;
    static bool upHave;
    static float lateSince, lateDrag, lateOutro = -1f;

    static HandsView? HandsCamera(int layer)
    {
        if (prop == null) return null;
        var root = prop.transform;
        while (root.parent != null) root = root.parent;
        if (eyeBone == null || !eyeBone || eyeFor != root.Pointer)
        {
            eyeBone = null;
            eyeFor = root.Pointer;
            viewMode = 0;
            for (var a = prop.transform.parent; a != null && eyeBone == null; a = a.parent)
                eyeBone = a.Find("GAME_DATA/Origin/Camera_Offset/Camera_SecondaryPhysics/camera") ?? a.Find("Origin/Camera_Offset/Camera_SecondaryPhysics/camera");
        }
        float wfov = Settings.HandsFov.Value;
        if (wfov <= 1f)
        {
            wfov = 30f;
            try { if (viewWeapon != null && viewWeapon.RenderingFieldOfView > 1f) wfov = viewWeapon.RenderingFieldOfView; } catch { }
        }
        var b = view.transform;
        if (viewMode == 0)
        {
            // the Unity camera standing at the rig's eye, if any; the hands are drawn through its (possibly overridden) matrices
            rigCam = null;
            if (eyeBone != null)
                foreach (var c in Camera.allCameras)
                    if (c != null && (c.transform.position - eyeBone.position).sqrMagnitude < .01f) { rigCam = c; break; }
            var at = prop.transform.position;
            var w = Hands.Wrist != null ? Hands.Wrist.position : at;
            Vector3 vc = rigCam != null ? rigCam.WorldToViewportPoint(at) : new Vector3(-9f, -9f, -9f);
            Vector3 vw = rigCam != null ? rigCam.WorldToViewportPoint(w) : vc;
            var bb = new Basis();
            if (eyeBone != null)
            {
                EyeAxes(eyeBone, bb);
            }
            var vb = Project(bb, wfov, at);
            var vbw = Project(bb, wfov, w);
            viewMode = 2;
            if (Settings.HandsViewMode.Value is 1 or 2) viewMode = Settings.HandsViewMode.Value;
            string M(Matrix4x4 m) => $"[{m.m00:F2} {m.m01:F2} {m.m02:F2} {m.m03:F2} | {m.m10:F2} {m.m11:F2} {m.m12:F2} {m.m13:F2} | {m.m20:F2} {m.m21:F2} {m.m22:F2} {m.m23:F2}]";
            Safe.Info($"Hands view chosen: {(viewMode == 1 ? "rig camera " + rigCam!.name : "eye bone +Y")}; smoke on screen by camera {vc:F2} wrist {vw:F2}; by bone {vb:F2} wrist {vbw:F2}; fov weapon {wfov:0} camera {(rigCam != null ? rigCam.fieldOfView.ToString("0") : "-")}");
            if (rigCam != null) Safe.Info($"Rig camera matrices: view {M(rigCam.worldToCameraMatrix)} proj {M(rigCam.projectionMatrix)}; transform fwd {rigCam.transform.forward:F2}");
            if (eyeBone != null) Safe.Info($"Eye bone local: smoke {eyeBone.InverseTransformPoint(at):F3} wrist {eyeBone.InverseTransformPoint(w):F3} elbow {(Hands.Elbow != null ? eyeBone.InverseTransformPoint(Hands.Elbow.position).ToString("F3") : "-")}");
        }
        if (viewMode == 1 && rigCam != null && rigCam)
        {
            var m = rigCam.cameraToWorldMatrix;
            b.position = m.MultiplyPoint(Vector3.zero);
            b.forward = m.MultiplyVector(Vector3.back).normalized;
            b.up = m.MultiplyVector(Vector3.up).normalized;
            b.right = m.MultiplyVector(Vector3.right).normalized;
            view.cam = rigCam;
            view.fov = rigCam.fieldOfView;
        }
        else if (eyeBone != null)
        {
            EyeAxes(eyeBone, b);
            view.cam = null;
            view.fov = wfov;
        }
        else
        {
            var cam = GameManager.GetVpFPSCamera();
            if (cam == null) return null;
            b.position = cam.transform.position;
            b.forward = cam.transform.forward;
            b.up = cam.transform.up;
            b.right = cam.transform.right;
            view.cam = null;
            view.fov = wfov;
        }
        haveView = true;
        if (Time.unscaledTime - viewLogged > 60f)
        {
            viewLogged = Time.unscaledTime;
            var rel = prop.transform.position - b.position;
            Safe.Info($"Hands view: mode {viewMode}, smoke at right {Vector3.Dot(rel, b.right):0.000} up {Vector3.Dot(rel, b.up):0.000} fwd {Vector3.Dot(rel, b.forward):0.000}");
        }
        return view;
    }

    static Vector3 InView(Basis ct, Vector3 v) => (ct.right * v.x + ct.up * v.y + ct.forward * v.z).normalized;

    /// <summary>The hand to the mouth by amount 0..1: where the mouth end (filter, cigar cap, mouthpiece = the prop's origin) must go and how the
    /// smoke must lie there; the wrist pose that gives it; the shoulder and elbow turned to reach it; whatever is out of reach made up by moving
    /// the whole arms rig (put back next frame).</summary>
    /// <summary>A drag of a cigarette or cigar: the hand comes up only to below the chin (out of view, the elbow hardly bent), then the smoke
    /// alone rises from there to the lips; so the player sees the smoke come up to the mouth, not the fingers.</summary>
    static void Drag(FirstPersonWeapon rig, Def lit, float amount)
    {
        var below = Settings.Vec(Settings.DragHandBelow);
        if (below.sqrMagnitude < 1e-6f || !Settings.ByOffset.Value || !haveRest || eyeBone == null)
        {
            Reach(rig, lit, amount);
            Hold(lit);
            return;
        }
        float split = Mathf.Clamp(Settings.DragSplit.Value, .1f, .9f);
        Reach(rig, lit, S01(amount / split), below);
        Hold(lit);
        float up = S01((amount - split) / (1f - split));
        if (up <= 0f) return;
        var cam = HandsCamera(prop!.layer);
        if (cam == null) return;
        MouthPose(cam, lit, prop.transform.position, out var mp, out var mr);
        prop.transform.position = Vector3.Lerp(prop.transform.position, mp, up);
        prop.transform.rotation = Quaternion.Slerp(prop.transform.rotation, mr, up);
    }

    static void Reach(FirstPersonWeapon rig, Def lit, float amount) => Reach(rig, lit, amount, Vector3.zero);

    static void Reach(FirstPersonWeapon rig, Def lit, float amount, Vector3 shift, float turnAmount = -1f, float maxWristTurn = 0f)
    {
        if (turnAmount < 0f) turnAmount = amount;
        var w = Hands.Wrist;
        var cam = HandsCamera(prop!.layer);
        if (cam == null) return;
        var ct = cam.transform;
        var p0 = prop.transform.position;
        var r0 = prop.transform.rotation;

        MouthPose(cam, lit, p0, out var target, out var rT);
        if (shift.sqrMagnitude > 0f) target += EyeVector(shift);

        if (w == null || Hands.Elbow == null || Hands.Shoulder == null)
        {
            MoveArms(rig, (target - p0) * amount);
            return;
        }
        // the smoke as seen from the wrist stays as it is; the wrist goes where the smoke must be
        var wr0 = w.rotation;
        var wp0 = w.position;
        var relRot = Quaternion.Inverse(wr0) * r0;
        var relPos = Quaternion.Inverse(wr0) * (p0 - wp0);
        var wrT = rT * Quaternion.Inverse(relRot);
        var wpT = target - wrT * relPos;
        if (maxWristTurn > 0f)
        {
            // a pipe turns in the fingers; the wrist itself only so far (a big turn spun the hand round)
            float ang = Quaternion.Angle(wr0, wrT);
            if (ang > maxWristTurn) wrT = Quaternion.Slerp(wr0, wrT, maxWristTurn / ang);
        }
        var wr = Quaternion.Slerp(wr0, wrT, turnAmount);
        var wp = Vector3.Lerp(wp0, wpT, amount);

        // with the view locked the elbow is turned down under the hand, so the forearm rises from below the screen instead of crossing it
        if (ViewLock.Axes(out var vr, out var vu, out _))
            TwoBone(Hands.Shoulder, Hands.Elbow, w, wp, -vu + vr * .25f, Settings.ElbowDown.Value * amount);
        else
            TwoBone(Hands.Shoulder, Hands.Elbow, w, wp, -ct.up + ct.right * .6f);
        w.rotation = wr;
        var miss = wp - w.position;
        if (miss.sqrMagnitude > .00002f) MoveArms(rig, miss);
        if (Time.unscaledTime > nextPoseLog)
        {
            nextPoseLog = Time.unscaledTime + 30f;
            Safe.Info($"Drag reach: to {target - ct.position}, miss {miss.magnitude:0.000} m, amount {amount:0.00}");
        }
    }

    /// <summary>Shoulder and elbow turned so the wrist reaches t, the elbow bending the way it already bends (or towards hint).</summary>
    static void TwoBone(Transform sh, Transform el, Transform wr, Vector3 t, Vector3 hint, float toHint = 0f)
    {
        var s = sh.position;
        var e = el.position;
        var w = wr.position;
        float a = (e - s).magnitude, b = (w - e).magnitude;
        if (a < 1e-4f || b < 1e-4f) return;
        var st = t - s;
        float d = Mathf.Clamp(st.magnitude, Math.Abs(a - b) + 1e-3f, a + b - 1e-3f);
        var dir = st.sqrMagnitude > 1e-8f ? st.normalized : (w - s).normalized;
        var pole = Vector3.ProjectOnPlane(e - s, dir);
        if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(hint, dir);
        pole.Normalize();
        if (toHint > 0f)
        {
            var hp = Vector3.ProjectOnPlane(hint, dir);
            if (hp.sqrMagnitude > 1e-6f) pole = Vector3.Slerp(pole, hp.normalized, Mathf.Clamp01(toHint)).normalized;
        }
        float cos = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
        var eNew = s + dir * (a * cos) + pole * (a * Mathf.Sqrt(1f - cos * cos));
        sh.rotation = Quaternion.FromToRotation(e - s, eNew - s) * sh.rotation;
        var e2 = el.position;
        el.rotation = Quaternion.FromToRotation(wr.position - e2, s + dir * d - e2) * el.rotation;
    }

    // the whole arms rig moved for what the arm cannot reach; put back at the start of every frame
    static Transform? movedArms;
    static Vector3 armsRest;

    static void MoveArms(FirstPersonWeapon rig, Vector3 by)
    {
        // the arms only: the rig root also carries the hands' own camera, moving it breaks the view
        Transform t = ArmsRoot(rig.transform);
        for (var a = rig.transform.parent; a != null; a = a.parent)
            if (a.name == "Camera_Weapon_Offset" || a.name == "Shoulder_Joint") { t = a; if (a.name == "Shoulder_Joint") break; }
        if (movedArms == null)
        {
            movedArms = t;
            armsRest = t.localPosition;
        }
        t.position += by;
    }

    static void RestoreArms(FirstPersonWeapon? rig)
    {
        if (movedArms == null) return;
        try
        {
            if (movedArms) movedArms.localPosition = armsRest;
        }
        catch { }
        movedArms = null;
    }

    static void Hold(Def lit)
    {
        if (prop == null || gripFor == null) return;
        var cam = HandsCamera(prop.layer);
        if (cam == null) return;
        var ct = cam.transform;
        if (lit.Smoke != SmokeType.Pipe)
        {
            if (lit.Smoke == SmokeType.Cigar && Settings.CigarPinchOn.Value && Hands.Pinch(Settings.CigarGrip.Value, out var cp, out var cd, out var cu))
            {
                prop.transform.rotation = Quaternion.LookRotation(cd, cu);
                // held about a third of the way from the cap (mouth end)
                prop.transform.position = cp - cd * (emberZ * .3f * Settings.PropScale.Value);
                return;
            }
            float grip = lit.Smoke == SmokeType.Cigar ? Settings.CigarJoint.Value : Settings.CigaretteGrip.Value;
            if (Hands.Between(grip, out var bp, out var bd, out var bu))
            {
                prop.transform.rotation = Quaternion.LookRotation(bd, bu);
                // a third of it on the palm side (filter), the lit end out past the back of the fingers
                // long cigars: no more than 3 cm out on the palm side, else the cap ran into the thumb
                float back = lit.Smoke == SmokeType.Cigar ? emberZ * .2f : Math.Max(emberZ * .25f, Settings.CigFilterOut.Value);   // 11-09: short ones showed too little filter behind the fingers
                prop.transform.position = bp - bd * (back * Settings.PropScale.Value);
                return;
            }
        }
        var axis = gripFor.up;
        bool heldPipeById = lit.Id.Contains("Corncob");   // the corn cob sat right with the old rule: kept
        if (lit.Smoke == SmokeType.Pipe)
        {
            // the bowl upright in the fist, the stem out over the thumb towards the face
            var f = Vector3.ProjectOnPlane(ct.forward, axis);
            if (f.sqrMagnitude < 1e-4f) f = Vector3.ProjectOnPlane(gripFor.forward, axis);
            // the long-stemmed pipes: the stem turned across, to the left of the face (pointing back at the face it went into the shoulder)
            f = Quaternion.AngleAxis(Settings.PipeTurn.Value + (heldPipeById ? 0f : Settings.PipeHeldTurn.Value), axis) * f.normalized;
            var rot = Quaternion.LookRotation(f, axis);
            prop.transform.rotation = rot;
            if (heldPipeById && Settings.PipeOverFist.Value <= 0f)
            {
                var stem = gripFor.position + axis * Settings.PipeStemUp.Value;
                prop.transform.position = stem - rot * (new Vector3(0f, 0f, pipeBowl.z) * Settings.PropScale.Value);
            }
            else
                // the fist under the bowl's foot (bent, billiard and churchwarden bowls hang below the stem)
                prop.transform.position = gripFor.position - rot * (pipeGrip * Settings.PropScale.Value);
            heldRot0 = prop.transform.rotation;
            heldPos0 = prop.transform.position;
            pipeHeldAsMouth = false;
            if (haveRest && eyeBone != null && Settings.ByOffset.Value && Settings.PipeHeldAsMouth.Value)
            {
                // turned in the fist as it will lie in the mouth (the tuned turn), fixed to the fist from now on
                Eye(out _, out var eq);
                if (!pipeHandSet)
                {
                    pipeHandFromEye = Quaternion.Inverse(gripFor.rotation) * eq;
                    pipeHandSet = true;
                }
                var mr = gripFor.rotation * pipeHandFromEye * (Quaternion.Euler(Settings.Vec(Tune.Turn(lit))) * restRot);
                // stood upright in the fist (bowl up along the fist, its foot in the curled fingers, the stem out over the thumb), turned
                // round that axis the way it lies in the mouth: so on the way up only the small tilt is left for the wrist
                mr = Quaternion.FromToRotation(mr * Vector3.up, axis) * mr;
                prop.transform.rotation = mr;
                float sc = Settings.PropScale.Value;
                var at = gripFor.position - mr * (pipeGrip * sc);
                // turned as in the mouth, the stem or the bowl's side can dip below the bowl's foot: lifted so the lowest point of the
                // pipe stays over the fist (the bowl foot is the grip only while the pipe stands upright)
                if (pipeVerts.Count > 0)
                {
                    float low = float.MaxValue;
                    foreach (var v in pipeVerts) low = Math.Min(low, Vector3.Dot(at + mr * (v * sc) - gripFor.position, axis));
                    float lift = Settings.PipeOverFist.Value - low;
                    if (lift > 0f) at += axis * lift;
                }
                prop.transform.position = at;
                pipeHeldAsMouth = true;
            }
        }
        else
        {
            var p = gripFor.position + axis * Settings.Vec(Settings.PropPos).y;
            var w = Settings.Vec(Settings.PropRot);
            var dir = (axis * w.x + ct.forward * w.y + ct.right * w.z).normalized;
            var rot = Quaternion.LookRotation(dir, -ct.forward);
            prop.transform.rotation = rot;
            prop.transform.position = p - dir * (emberZ * .2f * Settings.PropScale.Value);
        }
    }

    static bool IsOurs(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            if (p.name == "SmokesProp") return true;
        return false;
    }

    static void SetColor(GameObject go, Color c)
    {
        var r = go.GetComponent<MeshRenderer>();
        if (r == null || r.material == null) return;
        var m = r.material;
        if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    static void Build(FirstPersonWeapon rig, Def lit)
    {
        // 11-09 evening (log 1922): the new rig after a scene change came through here and Reset dropped ResumeLock, so the old lock was never put back
        bool resume = ResumeLock;
        Reset();
        ResumeLock = resume;
        fphFor = rig.Pointer;
        var handle = FindDeep(rig.transform, "torch_handle") ?? FindDeep(rig.transform, "origin") ?? rig.transform;
        Borrow(rig);
        var mesh = Assets.LoadMesh("meshes/" + (Smoking.From?.CigMesh is { } cm && lit.Smoke == SmokeType.Cigarette ? cm + "_held" : lit.HeldMesh) + ".fpmesh");
        if (mesh == null) return;
        if (lit.Smoke == SmokeType.Cigarette) PrintSide(mesh);
        int layer = handle.gameObject.layer;
        foreach (var r in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            layer = r.gameObject.layer;
            break;
        }

        prop = new GameObject("SmokesProp");
        prop.layer = layer;
        prop.transform.SetParent(handle, false);
        bool pipe = lit.Smoke == SmokeType.Pipe;
        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        gripFor = handle;
        prop.transform.localScale = Vector3.one * Settings.PropScale.Value;

        var model = new GameObject("Model");
        model.layer = layer;
        model.transform.SetParent(prop.transform, false);
        burn = null;
        keepNow = 1f;
        if (mesh != null)
        {
            mesh = TwoSided(mesh);
            if (!pipe) burn = new Burn(mesh);
        }
        model.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = model.AddComponent<MeshRenderer>();
        if (propMat != null)
        {
            var m = new Material(propMat);
            string texPath = Def.TexPath(Smoking.From?.CigTexture ?? lit.Texture);
            Texture? tex = null;
            // 11-13: a filter cigarette gets its own copy of the texture, whose mouth end disc goes brown as it is smoked (FilterStain)
            if (lit.Smoke == SmokeType.Cigarette && Settings.FilterStain.Value > 0f && Smoking.From?.CigMesh is { } fm && fm != "cig_nofilter")
                tex = FilterStain.Make(texPath);
            else FilterStain.Clear();
            tex ??= Assets.Texture(texPath);
            if (tex != null) m.mainTexture = tex;
            mr.sharedMaterial = m;
        }
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var b = mesh.bounds;
        emberZ = b.max.z;
        propRadius = Mathf.Max(b.extents.x, b.extents.y);
        pipeBowl = new Vector3(0f, b.max.y - .004f, b.max.z - .016f);
        if (pipe) Tobacco(mesh, layer);

        ember = new GameObject("Ember");
        ember.layer = layer;
        ember.transform.SetParent(prop.transform, false);
        if (glowMat != null && quad != null)
        {
            halo = new GameObject("Glow");
            halo.layer = layer;
            halo.transform.SetParent(ember.transform, false);
            halo.AddComponent<MeshFilter>().sharedMesh = quad;
            var hr = halo.AddComponent<MeshRenderer>();
            hr.sharedMaterial = new Material(glowMat);
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // the burning end itself: a glowing disc with an ash rim, unlit so it shows as glowing, seen from both sides
        if (!pipe)
        {
            float rr = Math.Max(b.extents.x, b.extents.y) * .98f;
            emberCap = new GameObject("EmberCap");
            emberCap.layer = layer;
            emberCap.transform.SetParent(ember.transform, false);
            // the end's real outline (a box-pressed cigar is square: a round cap and band sat inside it, the burning end hardly showed)
            var prof = Profile(mesh, b, rr);
            emberCap.AddComponent<MeshFilter>().sharedMesh = Disc(prof, 1.03f);   // as wide as the band: a narrower cap left a ring of gap at the end
            var er = emberCap.AddComponent<MeshRenderer>();
            er.sharedMaterial = EmberMat();
            er.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // the burning band seen from the side: char line, glowing ring, a little grey ash past it
            float band = lit.Smoke == SmokeType.Cigarette ? .004f : .007f;
            emberBand = new GameObject("EmberBand");
            emberBand.layer = layer;
            emberBand.transform.SetParent(ember.transform, false);
            emberBand.transform.localPosition = new Vector3(0f, 0f, -band * .85f);
            emberBand.AddComponent<MeshFilter>().sharedMesh = Tube(prof, 1.03f, band);
            var br = emberBand.AddComponent<MeshRenderer>();
            bandMat = BandMat();
            br.sharedMaterial = bandMat;
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            emberCap.transform.localPosition = new Vector3(0f, 0f, band * .15f);
        }
// the faint light of the ember on the hand (a setting)
        if (Settings.EmberLight.Value)
        {
                var lgo = new GameObject("EmberLight");
            lgo.layer = layer;
            lgo.transform.SetParent(ember.transform, false);
            light = lgo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .45f, .15f);
            light.range = .12f;
            light.intensity = .08f;
            light.shadows = LightShadows.None;
        }

        Safe.Debug($"Prop on {handle.name} (rig {rig.name}, parent {rig.transform.parent?.name}), layer {layer}, material {(propMat != null ? propMat.shader?.name : "none")}, glow {(glowMat != null)}, smoke {(smokeMat != null)}");
    }

    static readonly List<Vector3> pipeStem = new();
    static float pipeStemR = .004f;

    /// <summary>The stem's real centre line (bent pipes curve up to the bowl): slices across z from the mouthpiece to the bowl's wall,
    /// each slice's centre from its vertices; the thickest slice gives the stem radius.</summary>
    static void StemLine(Vector3[] vs, Bounds b, Vector3 bowl, float inner)
    {
        pipeStem.Clear();
        pipeStemR = .004f;
        float z0 = b.min.z, z1 = bowl.z - inner - .007f;
        if (z1 <= z0 + .005f) return;
        const int N = 10;
        float half = (z1 - z0) / N * .6f;
        var rs = new List<float>();
        for (int k = 0; k <= N; k++)
        {
            float z = Mathf.Lerp(z0 + half * .5f, z1, k / (float)N);
            float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
            int n = 0;
            for (int i = 0; i < vs.Length; i++)
            {
                var v = vs[i];
                if (Mathf.Abs(v.z - z) > half) continue;
                n++;
                if (v.x < x0) x0 = v.x; if (v.x > x1) x1 = v.x;
                if (v.y < y0) y0 = v.y; if (v.y > y1) y1 = v.y;
            }
            if (n < 3) continue;
            pipeStem.Add(new Vector3((x0 + x1) * .5f, (y0 + y1) * .5f, z));
            rs.Add(Mathf.Max(x1 - x0, y1 - y0) * .5f);
        }
        // slices that reach into the bowl are far thicker than the stem: dropped
        var sorted = new List<float>(rs); sorted.Sort();
        float med = sorted.Count > 0 ? sorted[sorted.Count / 2] : 0f;
        float rmax = 0f;
        for (int k = rs.Count - 1; k >= 0; k--)
            if (rs[k] > med * 1.8f) { rs.RemoveAt(k); pipeStem.RemoveAt(k); }
            else rmax = Mathf.Max(rmax, rs[k]);
        if (rmax > .001f && rmax < .02f) pipeStemR = rmax;
        Safe.Info($"Pipe stem: {pipeStem.Count} points, radius {pipeStemR:0.0000}, from {(pipeStem.Count > 0 ? pipeStem[0] : Vector3.zero):F3} to {(pipeStem.Count > 0 ? pipeStem[pipeStem.Count - 1] : Vector3.zero):F3}");
    }

    /// <summary>Tobacco in the bowl: a shredded brown disc just under the rim, and over it smouldering spots that glow up on a drag.
    /// Centre and size from the rim of the bowl itself (its top vertices).</summary>
    static void Tobacco(Mesh mesh, int layer)
    {
        try
        {
            var b = mesh.bounds;
            var vs = mesh.vertices;
            float top = b.max.y;
            Vector3 c = Vector3.zero;
            int n = 0;
            for (int i = 0; i < vs.Length; i++)
                if (vs[i].y > top - .0015f) { c += vs[i]; n++; }
            if (n == 0) return;
            c /= n;
            float inner = float.MaxValue;
            for (int i = 0; i < vs.Length; i++)
                if (vs[i].y > top - .003f)
                {
                    float d = new Vector2(vs[i].x - c.x, vs[i].z - c.z).magnitude;
                    if (d < inner) inner = d;
                }
            if (inner > .02f || inner < .004f) inner = .009f;
            var at = new Vector3(c.x, top - .0055f, c.z);
            pipeBowl = at;
            // the bowl's foot: lowest point near the bowl's axis; the fist closes round the middle of the bowl
            float bottom = top;
            float rr = Mathf.Max(inner * 2.2f, .02f);
            for (int i = 0; i < vs.Length; i++)
                if (new Vector2(vs[i].x - c.x, vs[i].z - c.z).magnitude < rr && vs[i].y < bottom) bottom = vs[i].y;
            // the bowl rests on the closed fist (its foot just over the fingers): inside the fist it went through them
            pipeGrip = new Vector3(c.x, bottom - Settings.PipeOverFist.Value, c.z);
            StemLine(vs, b, c, inner);
            pipeVerts.Clear();
            int step = Math.Max(1, vs.Length / 400);
            for (int i = 0; i < vs.Length; i += step) pipeVerts.Add(vs[i]);
            Safe.Info($"Pipe bowl: rim {top:F3}, foot {bottom:F3}, grip at {pipeGrip:F3}");
            var sh = Shader.Find("Sprites/Default");
            if (sh == null) return;
            var leaf = new Material(sh) { mainTexture = TobaccoTex(false) };
            var hot = new Material(sh) { mainTexture = TobaccoTex(true) };
            tobaccoGlow = hot;
            for (int k = 0; k < 2; k++)
            {
                var go = new GameObject(k == 0 ? "Tobacco" : "TobaccoEmber");
                go.layer = layer;
                go.transform.SetParent(prop!.transform, false);
                go.transform.localPosition = at + Vector3.up * (k * .0003f);
                go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = Disc(inner * 1.02f);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = k == 0 ? leaf : hot;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Safe.Info($"Tobacco in the bowl at {at}, radius {inner:0.0000}");
        }
        catch (Exception e)
        {
            Safe.Log("Tobacco", e);
        }
    }

    static Texture2D? leafTex, hotTex;
    static Color leafTint;

    static Texture2D TobaccoTex(bool hot)
    {
        var tint = Smoking.From?.Tobacco ?? new Color(.42f, .24f, .1f);
        if (hot && hotTex != null) return hotTex;
        if (!hot && leafTex != null && leafTint == tint) return leafTex;
        if (!hot) leafTint = tint;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                // shreds: stretched noise in a few directions
                float s1 = Mathf.PerlinNoise(x * .55f, y * .12f), s2 = Mathf.PerlinNoise(x * .1f + 7f, y * .6f + 3f);
                float shred = Mathf.Max(s1, s2);
                Color c;
                if (!hot)
                {
                    c = Color.Lerp(new Color(tint.r * .38f, tint.g * .33f, tint.b * .35f), tint, shred);
                    // charred towards the middle where it burns
                    c = Color.Lerp(c, new Color(.1f, .08f, .07f), Mathf.Clamp01(.8f - r) * .7f * Mathf.PerlinNoise(x * .3f + 11f, y * .3f));
                    c.a = r <= 1f ? 1f : 0f;
                }
                else
                {
                    float spot = Mathf.Clamp01((Mathf.PerlinNoise(x * .35f + 23f, y * .35f + 5f) - .48f) * 4f) * Mathf.Clamp01(1.1f - r);
                    c = Color.Lerp(new Color(.9f, .2f, .03f), new Color(1f, .7f, .25f), spot);
                    c.a = r <= 1f ? spot : 0f;
                }
                px[y * n + x] = c;
            }
        tex.SetPixels(new Il2CppStructArray<Color>(px));
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        if (hot) hotTex = tex; else leafTex = tex;
        return tex;
    }

    static Material? emberMat;

    static Material? EmberMat()
    {
        if (emberMat != null) return emberMat;
        var sh = Shader.Find("Sprites/Default");
        if (sh == null) return glowMat;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float noise = Mathf.PerlinNoise(x * .3f, y * .3f);
                // glowing core, darker red, then grey ash at the rim
                Color hot = Color.Lerp(new Color(1f, .62f, .2f), new Color(.85f, .18f, .04f), Mathf.Clamp01(r * 1.3f + noise * .3f - .2f));
                Color ash = Color.Lerp(new Color(.16f, .14f, .13f), new Color(.3f, .28f, .26f), noise);   // 11-09: light ash at the rim read as a gap ring
                float t = Mathf.Clamp01((r - .8f + noise * .1f) / .15f);
                var c = Color.Lerp(hot, ash, t);
                c.a = r <= 1f ? 1f : 0f;
                px[y * n + x] = c;
            }
        tex.SetPixels(new Il2CppStructArray<Color>(px));
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        emberMat = new Material(sh) { mainTexture = tex };
        emberMat.hideFlags = HideFlags.HideAndDontSave;
        return emberMat;
    }

    const int ProfSeg = 24;

    /// <summary>The smoke's outline near its lit end: per direction round the axis, how far the surface reaches (a circle for round ones,
    /// a rounded square for box-pressed cigars).</summary>
    static float[] Profile(Mesh? mesh, Bounds b, float fallback)
    {
        var r = new float[ProfSeg];
        if (mesh == null) { for (int i = 0; i < ProfSeg; i++) r[i] = fallback; return r; }
        var vs = mesh.vertices;
        float z1 = b.max.z - .003f, z0 = b.max.z - .03f;
        var c = Vector2.zero;   // the ember sits on the axis
        for (int k = 0; k < vs.Length; k++)
        {
            var v = vs[k];
            if (v.z < z0 || v.z > z1) continue;
            var d = new Vector2(v.x, v.y) - c;
            float m = d.magnitude;
            if (m < 1e-5f) continue;
            float a = Mathf.Atan2(d.y, d.x);
            int i = ((int)Mathf.Round(a / (Mathf.PI * 2f) * ProfSeg) % ProfSeg + ProfSeg) % ProfSeg;
            if (m > r[i]) r[i] = m;
        }
        int got = 0;
        for (int i = 0; i < ProfSeg; i++) if (r[i] > 0f) got++;
        if (got < ProfSeg / 3) { for (int i = 0; i < ProfSeg; i++) r[i] = fallback; return r; }
        for (int pass = 0; pass < ProfSeg; pass++)
            for (int i = 0; i < ProfSeg; i++)
                if (r[i] <= 0f)
                {
                    float l = r[(i + ProfSeg - 1) % ProfSeg], n = r[(i + 1) % ProfSeg];
                    if (l > 0f || n > 0f) r[i] = l > 0f && n > 0f ? (l + n) * .5f : Math.Max(l, n);
                }
        return r;
    }

    static Vector3 ProfPoint(float[] prof, int i, float scale)
    {
        float a = i * Mathf.PI * 2f / ProfSeg;
        float r = prof[i % ProfSeg] * scale;
        return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
    }

    /// <summary>A tube following the outline, along +Z from 0 to len, both sides drawn.</summary>
    static Mesh Tube(float[] prof, float scale, float len)
    {
        const int seg = ProfSeg;
        var v = new Vector3[(seg + 1) * 2];
        var uv = new Vector2[(seg + 1) * 2];
        for (int i = 0; i <= seg; i++)
        {
            var c = ProfPoint(prof, i, scale);
            v[i * 2] = c;
            v[i * 2 + 1] = c + new Vector3(0f, 0f, len);
            uv[i * 2] = new Vector2((float)i / seg, 0f);
            uv[i * 2 + 1] = new Vector2((float)i / seg, 1f);
        }
        var t = new int[seg * 12];
        for (int i = 0; i < seg; i++)
        {
            int a0 = i * 2, a1 = i * 2 + 1, b0 = i * 2 + 2, b1 = i * 2 + 3, o = i * 12;
            t[o] = a0; t[o + 1] = b0; t[o + 2] = a1;
            t[o + 3] = a1; t[o + 4] = b0; t[o + 5] = b1;
            t[o + 6] = a0; t[o + 7] = a1; t[o + 8] = b0;
            t[o + 9] = a1; t[o + 10] = b1; t[o + 11] = b0;
        }
        var m = new Mesh();
        m.vertices = new Il2CppStructArray<Vector3>(v);
        m.uv = new Il2CppStructArray<Vector2>(uv);
        m.triangles = new Il2CppStructArray<int>(t);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>A flat cap filling the outline, both sides drawn, UV 0..1 over its widest reach.</summary>
    static Mesh Disc(float[] prof, float scale)
    {
        const int seg = ProfSeg;
        float max = 1e-5f;
        for (int i = 0; i < seg; i++) max = Math.Max(max, prof[i] * scale);
        var v = new Vector3[seg + 1];
        var uv = new Vector2[seg + 1];
        v[0] = Vector3.zero;
        uv[0] = new Vector2(.5f, .5f);
        for (int i = 0; i < seg; i++)
        {
            v[i + 1] = ProfPoint(prof, i, scale);
            uv[i + 1] = new Vector2(.5f + v[i + 1].x / max * .5f, .5f + v[i + 1].y / max * .5f);
        }
        var t = new int[seg * 6];
        for (int i = 0; i < seg; i++)
        {
            int a = i + 1, b = (i + 1) % seg + 1;
            t[i * 6] = 0; t[i * 6 + 1] = a; t[i * 6 + 2] = b;
            t[i * 6 + 3] = 0; t[i * 6 + 4] = b; t[i * 6 + 5] = a;
        }
        var m = new Mesh();
        m.vertices = new Il2CppStructArray<Vector3>(v);
        m.uv = new Il2CppStructArray<Vector2>(uv);
        m.triangles = new Il2CppStructArray<int>(t);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>An open tube along +Z from 0 to len, both sides drawn, v = 0..1 along it.</summary>
    static Mesh Tube(float r, float len)
    {
        const int seg = 20;
        var v = new Vector3[(seg + 1) * 2];
        var uv = new Vector2[(seg + 1) * 2];
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            var c = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            v[i * 2] = c;
            v[i * 2 + 1] = c + new Vector3(0f, 0f, len);
            uv[i * 2] = new Vector2((float)i / seg, 0f);
            uv[i * 2 + 1] = new Vector2((float)i / seg, 1f);
        }
        var t = new int[seg * 12];
        for (int i = 0; i < seg; i++)
        {
            int a0 = i * 2, a1 = i * 2 + 1, b0 = i * 2 + 2, b1 = i * 2 + 3, o = i * 12;
            t[o] = a0; t[o + 1] = b0; t[o + 2] = a1;
            t[o + 3] = a1; t[o + 4] = b0; t[o + 5] = b1;
            t[o + 6] = a0; t[o + 7] = a1; t[o + 8] = b0;
            t[o + 9] = a1; t[o + 10] = b1; t[o + 11] = b0;
        }
        var m = new Mesh();
        m.vertices = new Il2CppStructArray<Vector3>(v);
        m.uv = new Il2CppStructArray<Vector2>(uv);
        m.triangles = new Il2CppStructArray<int>(t);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static Texture2D? bandTex;

    static Material? BandMat()
    {
        var sh = Shader.Find("Sprites/Default");
        if (sh == null) return null;
        if (bandTex == null)
        {
            const int w = 64, h = 32;
            bandTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = (y + .5f) / h;
                    float noise = Mathf.PerlinNoise(x * .35f, y * .5f);
                    Color c;
                    if (v < .18f) c = Color.Lerp(new Color(.12f, .08f, .05f), new Color(.3f, .15f, .06f), v / .18f);      // char line on the paper
                    else if (v < .7f) c = Color.Lerp(new Color(1f, .35f, .06f), new Color(1f, .68f, .25f), noise);       // glowing ring
                    else c = Color.Lerp(new Color(.2f, .18f, .16f), new Color(.34f, .32f, .3f), noise);                     // dark ash (light grey read as a gap)
                    if (v >= .62f && v < .78f) c = Color.Lerp(c, new Color(.85f, .3f, .08f), .5f * noise);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            bandTex.SetPixels(new Il2CppStructArray<Color>(px));
            bandTex.Apply();
            bandTex.hideFlags = HideFlags.HideAndDontSave;
        }
        var m = new Material(sh) { mainTexture = bandTex };
        return m;
    }

    /// <summary>A round disc facing +Z and -Z (both sides drawn), UV 0..1 over it.</summary>
    static Mesh Disc(float r)
    {
        const int seg = 24;
        var v = new Vector3[seg + 1];
        var uv = new Vector2[seg + 1];
        v[0] = Vector3.zero;
        uv[0] = new Vector2(.5f, .5f);
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            v[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            uv[i + 1] = new Vector2(.5f + Mathf.Cos(a) * .5f, .5f + Mathf.Sin(a) * .5f);
        }
        var t = new int[seg * 6];
        for (int i = 0; i < seg; i++)
        {
            int a = i + 1, b = (i + 1) % seg + 1;
            t[i * 6] = 0; t[i * 6 + 1] = a; t[i * 6 + 2] = b;
            t[i * 6 + 3] = 0; t[i * 6 + 4] = b; t[i * 6 + 5] = a;
        }
        var m = new Mesh();
        m.vertices = new Il2CppStructArray<Vector3>(v);
        m.uv = new Il2CppStructArray<Vector2>(uv);
        m.triangles = new Il2CppStructArray<int>(t);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>The torch rig's material for our model, its glow and smoke for the ember and the smoke.</summary>
    static void Borrow(FirstPersonWeapon rig)
    {
        foreach (var r in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (propMat == null && r.sharedMaterial != null) propMat = r.sharedMaterial;
        foreach (var r in rig.GetComponentsInChildren<MeshRenderer>(true))
            if (glowMat == null && r.gameObject.name == "RadialGradient" && r.sharedMaterial != null)
            {
                glowMat = r.sharedMaterial;
                quad = r.GetComponent<MeshFilter>()?.sharedMesh;
            }
        foreach (var r in rig.GetComponentsInChildren<ParticleSystemRenderer>(true))
            if (smokeMat == null && r.gameObject.name == "Smoke" && r.sharedMaterial != null) smokeMat = r.sharedMaterial;
        EnsureOwnSmoke();
        if (quad == null)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad = q.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(q);
        }
    }

    static void EnsureOwnSmoke()
    {
        // smoke: an alpha-blended soft round puff of our own (the torch's smoke material came out as a solid white sheet)
        if (ownSmoke == null)
        {
            var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Particles/Standard Unlit");
            if (sh != null)
            {
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - r);
                        a = a * a * (3f - 2f * a);
                        a *= .75f + .25f * Mathf.PerlinNoise(x * .15f, y * .15f);
                        px[y * n + x] = new Color(1f, 1f, 1f, a);
                    }
                tex.SetPixels(new Il2CppStructArray<Color>(px));
                tex.Apply();
                tex.hideFlags = HideFlags.HideAndDontSave;
                ownSmoke = new Material(sh) { mainTexture = tex };
                ownSmoke.hideFlags = HideFlags.HideAndDontSave;
            }
            Safe.Info("Smoke material: " + (ownSmoke != null ? ownSmoke.shader.name : "none, the torch's"));
        }
        if (ownQuad == null)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ownQuad = q.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(q);
        }
    }

    /// <summary>A breath of smoke out of the mouth, in front of the camera, drifting forward and up and spreading; bigger after a cigar or pipe.</summary>
    public static void Exhale(SmokeType type)
    {
        exhaleLeft = type == SmokeType.Cigarette ? 1.1f : 1.5f;
        exhaleBig = type == SmokeType.Cigarette ? 1f : 1.4f;
    }

    static float exhaleLeft, exhaleBig, exhaleAcc, wispAcc;

    /// <summary>Puffs of smoke, each a camera-facing quad with the torch's smoke material, moved here every frame
    /// (the game's particle modules for colour and size over lifetime are stripped from its build).</summary>
    class Puff
    {
        public GameObject Go = null!;
        public Material Mat = null!;
        public Vector3 Vel;
        public float Age, Life, Size0, Grow, Alpha, Rise;
    }

    static readonly List<Puff> puffs = new();

    static void Spawn(Vector3 pos, Vector3 vel, int layer, float life, float size, float grow, float alpha, float rise)
    {
        var baseMat = ownSmoke ?? smokeMat;
        var mesh = ownSmoke != null ? ownQuad : quad;
        if (baseMat == null || mesh == null || puffs.Count > 220) return;
        var go = new GameObject("SmokesPuff");
        go.layer = layer;
        go.transform.position = pos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        var m = new Material(baseMat);
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.transform.localScale = Vector3.one * size;
        puffs.Add(new Puff { Go = go, Mat = m, Vel = vel, Life = life * UnityEngine.Random.Range(.8f, 1.2f), Size0 = size, Grow = grow, Alpha = alpha, Rise = rise });
    }

    // ---- a coop friend smoking: the coop mod shows a copy of the lit item in the figure's hand ("Held_" + its name); our lit items carry an
    // ember light ("SeagullSmokeEmber", kept by the copy), so its tip glows there; here thin smoke rises from it and now and then a breath goes up
    static readonly List<(Light l, float next, float breath)> remotes = new();
    static float remoteScan;

    // phases sent by the coop mod (CoopApi.RemotePhase): when it sends them, a breath goes out after each real drag instead of at random
    static readonly Dictionary<IntPtr, (int phase, float since)> remotePhase = new();
    static bool remoteLinked;
    const float CigarRadius = .009f;   // our cigar meshes are 16–21 mm thick
    /// <summary>A figure's voice (sent by the coop mod through CoopApi.RemoteVoice): a woman breathes out in a female voice.</summary>
    public static readonly Dictionary<IntPtr, bool> RemoteFemale = new();

    public static void RemotePhase(GameObject copy, int phase)
    {
        if (copy == null) return;
        remoteLinked = true;
        var k = copy.Pointer;
        if (remotePhase.TryGetValue(k, out var was) && was.phase == phase) return;
        remotePhase[k] = (phase, Time.time);
        if (phase == 1 && was.phase != 1) Sounds.RemoteDrag(copy.transform.position, false);
        // the hand comes down from the mouth (or a pipe is taken out): breathe out from the face
        if (was.phase == 1 && phase != 1 || was.phase == 2 && phase == 0)
        {
            Light? l = null;
            foreach (var x in copy.GetComponentsInChildren<Light>(true)) if (x != null && x.gameObject.name == Items.EmberName) { l = x; break; }
            var at = (l != null ? l.transform.position : copy.transform.position) + Vector3.up * (phase == 2 ? .05f : .25f);
            Sounds.RemoteExhale(at, RemoteFemale.TryGetValue(k, out var fem) && fem);
            EnsureOwnSmoke();
            for (int k2 = 0; k2 < 16; k2++)
                Spawn(at + UnityEngine.Random.insideUnitSphere * .03f, (Vector3.up * .22f + UnityEngine.Random.insideUnitSphere * .2f), 0, 2.6f, .05f, 6f, .13f, .05f);
        }
    }

    static bool IsRemoteCopy(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            if (p.name.StartsWith("Held_", StringComparison.Ordinal)) return true;
        return false;
    }

    static void Remote(float dt)
    {
        try
        {
            remoteScan -= dt;
            if (remoteScan <= 0f)
            {
                remoteScan = 1f;
                var found = new List<(Light, float, float)>();
                foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>())
                {
                    if (l == null || l.gameObject.name != Items.EmberName || !l.gameObject.activeInHierarchy || !IsRemoteCopy(l.transform)) continue;
                    var old = remotes.FirstOrDefault(r => r.l != null && r.l.Pointer == l.Pointer);
                    found.Add(old.l != null ? old : (l, 0f, UnityEngine.Random.Range(3f, 6f)));
                }
                remotes.Clear();
                remotes.AddRange(found);
                if (remotes.Count > 0) EnsureOwnSmoke();
            }
            for (int i = 0; i < remotes.Count; i++)
            {
                var (l, next, breath) = remotes[i];
                if (l == null || !l.gameObject.activeInHierarchy) continue;
                var at = l.transform.position;
                // the ember breathes: brighter now and then
                l.enabled = true;   // the coop copy switches an unlit torch's lights off
                l.intensity = .5f + .25f * Mathf.PerlinNoise(Time.time * 1.7f, i * 3.1f);
                next -= dt;
                if (next <= 0f)
                {
                    next = .18f;
                    Spawn(at, Vector3.up * .05f + UnityEngine.Random.insideUnitSphere * .02f, 0, 2.2f, .015f, 6f, .08f, .06f);
                }
                breath -= dt;
                if (breath <= 0f && !remoteLinked)
                {
                    // a drag and a breath out: a bigger cloud rising from above the hand, where the face is
                    breath = UnityEngine.Random.Range(Settings.DragMinSeconds.Value, Settings.DragMaxSeconds.Value) + 1.5f;
                    var face = at + Vector3.up * .35f;
                    for (int k = 0; k < 14; k++)
                        Spawn(face + UnityEngine.Random.insideUnitSphere * .03f, (Vector3.up * .25f + UnityEngine.Random.insideUnitSphere * .2f), 0, 2.6f, .05f, 6f, .13f, .05f);
                }
                remotes[i] = (l, next, breath);
            }
        }
        catch (Exception e) { Safe.Log("Remote smoke", e); remoteScan = 5f; }
    }

    /// <summary>Every frame, smoking or not: what was breathed out drifts on after the smoke is gone.</summary>
    public static void TickPuffs()
    {
        float dt = Time.deltaTime;
        TickButts(dt);
        Remote(dt);
        var cam = GameManager.GetVpFPSCamera();
        var camT = cam != null ? cam.transform : null;
        if (exhaleLeft > 0f && camT != null && (smokeMat != null || ownSmoke != null))
        {
            exhaleLeft -= dt;
            exhaleAcc += dt * 26f * exhaleBig;
            while (exhaleAcc >= 1f)
            {
                exhaleAcc -= 1f;
                var dir = (camT.forward + camT.up * .12f + UnityEngine.Random.insideUnitSphere * .25f).normalized;
                Spawn(camT.position + camT.forward * .25f - camT.up * .06f, dir * .45f * exhaleBig, 0, 2.6f, .04f * exhaleBig, 7f, .16f, .05f);
            }
        }
        for (int i = puffs.Count - 1; i >= 0; i--)
        {
            var p = puffs[i];
            if (p.Go == null)
            {
                puffs.RemoveAt(i);
                continue;
            }
            p.Age += dt;
            float t = p.Age / p.Life;
            if (t >= 1f)
            {
                UnityEngine.Object.Destroy(p.Go);
                UnityEngine.Object.Destroy(p.Mat);
                puffs.RemoveAt(i);
                continue;
            }
            p.Vel *= 1f - Math.Min(1f, 1.4f * dt);
            p.Vel += Vector3.up * p.Rise * dt;
            p.Go.transform.position += p.Vel * dt;
            float size = p.Size0 * (1f + p.Grow * t);
            p.Go.transform.localScale = new Vector3(size, size, size);
            if (p.Go.layer != 0 && haveView) p.Go.transform.rotation = Quaternion.LookRotation(p.Go.transform.position - view.transform.position, view.transform.up);
            else if (camT != null) p.Go.transform.rotation = Quaternion.LookRotation(p.Go.transform.position - camT.position, camT.up);
            float a = p.Alpha * (t < .15f ? t / .15f : 1f - (t - .15f) / .85f);
            var c = new Color(.78f, .78f, .8f, a);
            if (p.Mat.HasProperty("_Color")) p.Mat.SetColor("_Color", c);
            if (p.Mat.HasProperty("_TintColor")) p.Mat.SetColor("_TintColor", c * new Color(a, a, a, 1f));
        }
    }

    /// <summary>A thin rising thread from the ember: many tiny faint puffs that rise, sway a little and widen as they go.</summary>
    static void Wisp(Vector3 at, int layer, float glow, float strength = 1f)
    {
        wispAcc += Time.deltaTime * (glow > .3f ? 8f : 24f) * strength;
        while (wispAcc >= 1f)
        {
            wispAcc -= 1f;
            float sway = (Mathf.PerlinNoise(Time.time * .7f, 3f) - .5f) * .03f;
            var v = new Vector3(sway + UnityEngine.Random.Range(-.003f, .003f), UnityEngine.Random.Range(.09f, .13f), UnityEngine.Random.Range(-.003f, .003f));
            Spawn(at + Vector3.up * .004f, v, layer, 1.8f, .006f, 4f, .09f, .02f);
        }
    }

    static float nextPoseLog;

    static IntPtr armsFor;
    static Transform? armsRoot;

    /// <summary>The arms the torch is held by: the torch rig hangs on the palm (right_prop_point), so it is the animated arms above it
    /// that must move. The nearest ancestor with an Animator, other than the torch's own.</summary>
    static Transform ArmsRoot(Transform torchRig)
    {
        if (armsRoot != null && armsFor == torchRig.Pointer && armsRoot) return armsRoot;
        Transform found = torchRig;
        var chain = new List<string>();
        for (var p = torchRig.parent; p != null; p = p.parent)
        {
            chain.Add(p.name);
            if (p.GetComponent<Animator>() != null)
            {
                found = p;
                break;
            }
        }
        armsFor = torchRig.Pointer;
        armsRoot = found;
        Safe.Info($"Arms for the drag: {found.name} (up from the torch: {string.Join(" < ", chain)})");
        return found;
    }

    class Butt
    {
        public GameObject Go = null!;
        public Transform? Tip;
        public Light? Light;
        public Material? Cap;
        public float Age;
    }

    static readonly List<Butt> butts = new();

    /// <summary>Like a spent match: the cigarette or cigar flicked out of the hand forward, falling with physics, its end glowing out over a
    /// few seconds, gone after a while. Our own visual only, nothing in the world's save.</summary>
    public static void Throw(Def lit)
    {
        try
        {
            if (prop == null || lit.Smoke == SmokeType.Pipe || flung && !prop.activeSelf) return;
            var model = prop.transform.Find("Model");
            var mf = model != null ? model.GetComponent<MeshFilter>() : null;
            var mr = model != null ? model.GetComponent<MeshRenderer>() : null;
            var cam = GameManager.GetVpFPSCamera();
            if (mf == null || mf.sharedMesh == null || mr == null || cam == null) return;
            var ct = cam.transform;
            float keep = keepNow;
            float scale = Settings.PropScale.Value;

            var go = new GameObject("SmokesButt");
            go.layer = 0;
            go.transform.position = ct.position + ct.forward * .5f + ct.right * .14f - ct.up * .18f;
            go.transform.rotation = Quaternion.LookRotation(ct.forward + ct.right * .4f, ct.up);
            var vis = new GameObject("Model");
            vis.layer = 0;
            vis.transform.SetParent(go.transform, false);
            vis.transform.localScale = new Vector3(scale, scale, scale);
            vis.AddComponent<MeshFilter>().sharedMesh = UnityEngine.Object.Instantiate(mf.sharedMesh);
            var r = vis.AddComponent<MeshRenderer>();
            r.sharedMaterial = mr.sharedMaterial;

            var bnd = mf.sharedMesh.bounds;
            var col = go.AddComponent<BoxCollider>();
            float len = bnd.size.z * scale;
            col.size = new Vector3(Math.Max(.012f, bnd.size.x * scale), Math.Max(.012f, bnd.size.y * scale), Math.Max(.02f, len));
            col.center = new Vector3(0f, 0f, len * .5f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = .02f;
            rb.drag = .2f;
            rb.angularDrag = .5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.velocity = (ct.forward * .85f + ct.up * .45f + ct.right * .2f).normalized * Settings.ThrowSpeed.Value;
            rb.angularVelocity = ct.right * UnityEngine.Random.Range(6f, 12f) + UnityEngine.Random.insideUnitSphere * 3f;
            try
            {
                var player = GameManager.GetPlayerObject();
                var cc = player != null ? player.GetComponent<CharacterController>() : null;
                if (cc != null) Physics.IgnoreCollision(col, cc);
            }
            catch { }

            var b = new Butt { Go = go };
            var tip = new GameObject("Tip");
            tip.layer = 0;
            tip.transform.SetParent(go.transform, false);
            tip.transform.localPosition = new Vector3(0f, 0f, emberZ * keep * scale);
            b.Tip = tip.transform;
            var em = EmberMat();
            if (em != null)
            {
                var cap = new GameObject("EmberCap");
                cap.layer = 0;
                cap.transform.SetParent(tip.transform, false);
                cap.AddComponent<MeshFilter>().sharedMesh = Disc(Profile(mf.sharedMesh, bnd, Math.Max(bnd.extents.x, bnd.extents.y)), .98f * scale);
                var cr = cap.AddComponent<MeshRenderer>();
                b.Cap = new Material(em);
                cr.sharedMaterial = b.Cap;
                cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var lgo = new GameObject("Light");
            lgo.layer = 0;
            lgo.transform.SetParent(tip.transform, false);
            b.Light = lgo.AddComponent<Light>();
            b.Light.type = LightType.Point;
            b.Light.color = new Color(1f, .45f, .15f);
            b.Light.range = .35f;
            b.Light.intensity = .5f;
            b.Light.shadows = LightShadows.None;
            butts.Add(b);
            Safe.Info($"Thrown: {lit.Id}, {keep:0.00} left");
        }
        catch (Exception e)
        {
            Safe.Log("Throw", e);
        }
    }

    static void TickButts(float dt)
    {
        for (int i = butts.Count - 1; i >= 0; i--)
        {
            var b = butts[i];
            b.Age += dt;
            if (b.Go == null || !b.Go || b.Age > 20f)
            {
                try
                {
                    if (b.Go != null && b.Go) UnityEngine.Object.Destroy(b.Go);
                    if (b.Cap != null) UnityEngine.Object.Destroy(b.Cap);
                }
                catch { }
                butts.RemoveAt(i);
                continue;
            }
            float glow = Mathf.Clamp01(1f - b.Age / 5f);
            if (b.Light != null) b.Light.intensity = .5f * glow * (.85f + .15f * Mathf.PerlinNoise(Time.time * 4f, i));
            if (b.Cap != null)
            {
                float k = .25f + .75f * glow;
                b.Cap.color = new Color(k, k * .8f + .2f * (1f - glow), k * .8f + .2f * (1f - glow), 1f);
            }
            if (glow > 0f && b.Tip != null) Wisp(b.Tip.position, 0, 0f, glow);
        }
    }

    /// <summary>Everything we moved put back (end of a smoke).</summary>
    public static void Pose(GearItem? item, float amount)
    {
        RestoreArms(null);
    }
}
