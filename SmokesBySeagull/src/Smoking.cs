using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SmokesBySeagull;

/// <summary>One smoke in the hand. The item is a copy of the game's torch, never lit: the arm holds it as a torch, the game's equip, hide and
/// switch logic all work, and there is no light, heat or wolf fear. In first person the torch model is hidden and ours sits on the torch handle,
/// with an ember, a wisp of smoke, a drag every 10-15 s (hand to the mouth by the weapon offsets) and smoke breathed out in front of the camera.
/// It ends burnt down, thrown away with H, or when anything else takes the hand; the effects go by the share smoked.</summary>
public static class Smoking
{
    class Session
    {
        public Def Lit = null!;
        public Def? From;
        public SmokeStats Stats = null!;
        public float EffectMul = 1f;
        public GearItem? Item;
        public float Smoked;           // seconds (real) or game hours, by Settings.RealTime
        public float Length;
        public float LastGameHour;
        public float StartedAt;        // unscaled time
        public bool Equipped;          // was in the hand at least once
        public float NextDrag, DragStart = -1f;
        public bool Exhaled, DragSounded;
        public bool Ending;
        public float EquippedAt;        // unscaled time it reached the hand
        public bool Resumed;            // came back with a scene change: the view settles first, a pipe goes straight back into the mouth
        public float OutroAt = -1f;     // a pipe being taken out of the mouth since (unscaled), before it ends
        public int EquipTries;          // equip calls made so far
        public float NextEquip;         // unscaled time of the next try
        public float LostAt = -1f;      // unscaled time the hand went empty (inventory, a menu): put back when it closes
        public float NextReequip;
        public Action? Refund;
        public float GoneAt = -1f;      // unscaled time the item vanished (a scene change)          // gives back what lighting took, should the smoke never reach the hand
    }

    static Session? s;
    /// <summary>The pipe being smoked: while it burns the lit pipe stands for it, so the pipe itself is not listed (no two pipes).</summary>
    public static GearItem? SourcePipe;
    public static bool IsSmokedPipe(GearItem? g) => s != null && !s.Ending && s.Lit.Smoke == SmokeType.Pipe && SourcePipe != null && g != null && g.Pointer == SourcePipe.Pointer;
    public static bool Active => s != null && !s.Ending;
    /// <summary>Our own call takes the smoke out of the hand (not the game's).</summary>
    public static bool OwnUnequip;
    /// <summary>The game would empty the hand while the smoke burns in it: kept for the inventory and other menus (taking it out and back
    /// made a hitch, a stow sound and the pipe leaving the mouth) and while a pipe is being taken out of the mouth (H).</summary>
    public static bool KeepInHand(string what)
    {
        if (OwnUnequip || s == null || s.Ending || !s.Equipped || s.Item == null) return false;
        var pm = GameManager.GetPlayerManagerComponent();
        if (pm == null || pm.m_ItemInHands == null || pm.m_ItemInHands.Pointer != s.Item.Pointer) return false;
        bool overlay = false, inv = false;
        try { overlay = InterfaceManager.IsOverlayActiveImmediate(); } catch { }
        try { var p = InterfaceManager.GetPanel<Panel_Inventory>(); inv = p != null && p.IsEnabled(); } catch { }
        // H: the game's own holster takes the hand before our tick sees the key; the pipe is taken out of the mouth by the hand first
        bool holster = false;
        try { holster = InputManager.GetHolsterPressed(pm); } catch { }
        if (holster && s.OutroAt < 0f) { Safe.Info("Holster key: the smoke is put away by our hand"); Use.Say(s.Lit.Smoke == SmokeType.Pipe ? "SMOKES_MSG_PIPE_OUT" : "SMOKES_MSG_THROWN"); End(true); }
        bool keep = s.OutroAt >= 0f || overlay || inv || Time.unscaledTime < InventoryAt + 1f;
        if (keep && (overlay || inv)) MenuKeptAt = Time.unscaledTime;
        if (keep || Time.unscaledTime >= nextKeepLog)
        {
            nextKeepLog = Time.unscaledTime + 2f;
            Safe.Info($"The game takes the smoke from the hand ({what}; overlay {overlay}, inventory {inv}, outro {s.OutroAt >= 0f}): {(keep ? "kept" : "let go")}");
        }
        return keep;
    }
    static float nextKeepLog;
    /// <summary>When a menu last tried to take the smoke from the hand (we kept it).</summary>
    public static float MenuKeptAt = -10f;
    /// <summary>The item the player picked to hold while a menu was open (inventory, wheel), and when.</summary>
    public static IntPtr ChosenInMenu;
    public static float ChosenInMenuAt = -10f;
    /// <summary>11-10: the player picked something to hold in the inventory or the item wheel (UI hooks only: 0.1.120 hooked
    /// PlayerManager.EquipItem itself, and a torch taken from a fire then broke in the hand, cause not proven).</summary>
    public static void PickedInMenu(GearItem? gi)
    {
        if (!Active || gi == null) return;
        try { ChosenInMenu = gi.Pointer; ChosenInMenuAt = Time.unscaledTime; Safe.Info($"Picked in a menu to hold: {gi.name}"); } catch { }
    }
    public static float InventoryAt = -10f;
    public static bool IsOurs(GearItem? g) => s != null && g != null && s.Item != null && g.Pointer == s.Item.Pointer;

    const float DragUp = .55f, DragHold = 1.1f, DragDown = .6f;

    /// <summary>What the smoke came from (a variety pack gives the cigarette its look and numbers).</summary>
    public static Def? From => s?.From;

    public static void Start(Def lit, float effectMul, string how, Action? refund, Def? from = null)
    {
        var pm = GameManager.GetPlayerManagerComponent();
        // standing up first: no smoking crouched
        try
        {
            if (pm.PlayerIsCrouched()) InputManager.ExecuteCrouchAction();
        }
        catch (Exception e)
        {
            Safe.Log("Stand up", e);
        }
        var template = Items.Spawn(lit.Id);
        if (template == null)
        {
            Safe.Info($"Lighting {lit.Id}: the lit item could not be made");
            refund?.Invoke();
            Use.Say("SMOKES_MSG_FAILED");
            return;
        }
        var gi = pm.InstantiateItemInPlayerInventoryFromPrefab(template, 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
        UnityEngine.Object.Destroy(template.gameObject);
        if (gi == null)
        {
            Safe.Info($"Lighting {lit.Id}: the lit item did not reach the pack");
            refund?.Invoke();
            Use.Say("SMOKES_MSG_FAILED");
            return;
        }
        var stats = Defs.Stats(lit.Smoke, from ?? (lit.Smoke == SmokeType.Cigar ? lit : null));
        s = new Session
        {
            Lit = lit,
            From = from,
            Stats = stats,
            EffectMul = effectMul,
            Item = gi,
            Length = Settings.RealTime.Value ? stats.RealMinutes * 60f : stats.GameMinutes / 60f,
            LastGameHour = Habit.Now,
            StartedAt = Time.unscaledTime,
            NextDrag = Time.time + 2.5f,
            NextEquip = Time.unscaledTime + .2f,
            Refund = refund,
        };
        Visual.Reset();
        Use.Say("SMOKES_MSG_LIT_" + how);
        Safe.Info($"Smoking {lit.Id} ({gi.name}): {s.Length:0.###} {(Settings.RealTime.Value ? "s" : "game h")}, effect x{effectMul}");
    }

    static float brokenHandSince = -1f, nextHandCheck;
    /// <summary>11-10 friend log: the hand held a destroyed item (a torch taken from a fire; its GearItem gone, the model still in
    /// the hand): it could be neither dropped nor put away and no smoke could get in. After 2 s of that the hand is emptied.</summary>
    public static void HealHand()
    {
        if (Time.unscaledTime < nextHandCheck) return;
        nextHandCheck = Time.unscaledTime + 1f;
        var pm = GameManager.GetPlayerManagerComponent();
        if (pm == null) return;
        var h = pm.m_ItemInHands;
        bool broken = h != null && !h;
        if (!broken) { brokenHandSince = -1f; return; }
        if (brokenHandSince < 0f) { brokenHandSince = Time.unscaledTime; return; }
        if (Time.unscaledTime - brokenHandSince < 2f) return;
        brokenHandSince = -1f;
        Safe.Info("The hand holds a destroyed item: emptied");
        try { OwnUnequip = true; pm.UnequipImmediate(false); } catch (Exception e) { Safe.Log("Empty a broken hand", e); } finally { OwnUnequip = false; }
        try { if (pm.m_ItemInHands != null && !pm.m_ItemInHands) pm.m_ItemInHands = null; } catch (Exception e) { Safe.Log("Clear a broken hand", e); }
    }

    static string HandName(PlayerManager pm)
    {
        try { var h = pm.m_ItemInHands; return h == null ? "empty" : !h ? "destroyed item" : h.name; } catch { return "unknown"; }
    }

    /// <summary>Into the hand once the inventory and other panels are gone and the hands are free; tried a few times, then given up with the smoke given back.</summary>
    static bool TryEquip(PlayerManager pm)
    {
        if (Time.unscaledTime < s!.NextEquip) return true;
        bool overlay = false, ready = true, can = true;
        try { overlay = InterfaceManager.IsOverlayActiveImmediate(); } catch { }
        try { ready = pm.IsReadyToEquip(); } catch { }
        try { can = pm.ItemCanEquipInHands(s.Item); } catch { }
        if (overlay || !ready)
        {
            s.NextEquip = Time.unscaledTime + .25f;
            if (Time.unscaledTime - s.StartedAt < 20f) return true;
            Safe.Info($"Equip: still waiting after 20 s (overlay {overlay}, ready {ready})");
        }
        if (s.EquipTries >= 4)
        {
            Safe.Info($"Equip: {s.EquipTries} tries, never in the hand (can equip {can}, hand {HandName(pm)})");
            return false;
        }
        s.EquipTries++;
        s.NextEquip = Time.unscaledTime + 1.2f;
        try
        {
            Safe.Info($"Equip try {s.EquipTries}: can equip {can}, hand {HandName(pm)}");
            pm.EquipItem(s.Item, false);
        }
        catch (Exception e)
        {
            Safe.Log("Equip the smoke", e);
        }
        return true;
    }

    public static float Share => s == null || s.Length <= 0f ? 0f : Math.Clamp(s.Smoked / s.Length, 0f, 1f);

    /// <summary>Time in the hand for the pose; a pipe brought back by a scene change skips its way to the mouth (it was in the mouth already).</summary>
    static float Since()
    {
        float since = Time.unscaledTime - s!.EquippedAt;
        // 11-09: jumping straight to «in the mouth» skipped the poses taken on the way (the pipe hung huge before the eyes): the way is run 10x fast
        return since;
    }

    public static void Tick()
    {
        TidyOrphans();
        if (s == null) return;
        var pm = GameManager.GetPlayerManagerComponent();
        // between scenes there is no player: the smoke waits for its item to come back with the new scene (TidyOrphans)
        if (pm == null) return;
        if (s.Ending)
        {
            Finish();
            return;
        }
        var hand = pm.m_ItemInHands;
        bool inHand = s.Item != null && hand != null && hand.Pointer == s.Item.Pointer;
        if (inHand && !s.Equipped)
        {
            s.Equipped = true;
            s.EquippedAt = Time.unscaledTime + (s.Resumed ? 1.5f : 0f);   // after a scene change: the rest pose is taken again while the view settles
            if (s.Resumed && s.Lit.Smoke == SmokeType.Pipe)
            {
                // 11-09 evening: a pipe brought back by a scene change is in the mouth at once, no way up again: hidden while the view settles
                // (the rest pose is taken each frame meanwhile), then shown straight in the mouth with the hand already down
                s.EquippedAt = Time.unscaledTime - Visual.PipeHandsDown - .05f;
                Visual.SettleUntil = Math.Max(Visual.SettleUntil, Time.unscaledTime + 1.5f);
            }
            Safe.Info("The smoke is in the hand");
            if (Settings.BareHands.Value) Hands.TakeOffGloves();
        }
        bool gone = s.Item == null;
        try { gone = gone || !s.Item!.gameObject; } catch { gone = true; }
        if (gone)
        {
            // a scene change re-makes the inventory: the lit item comes back as a new object; wait for it a while before giving up
            if (s.GoneAt < 0f) { s.GoneAt = Time.unscaledTime; Safe.Info("The smoke item is gone: waiting for it to come back with the scene"); }
            if (Time.unscaledTime - s.GoneAt < 30f) return;
            Safe.Info("The smoke item did not come back");
            s.Item = null;
            End(false);
            return;
        }
        s.GoneAt = -1f;
        if (!s.Equipped)
        {
            if (!TryEquip(pm))
            {
                // never got into the hand: nothing smoked, what it took comes back
                s.Refund?.Invoke();
                s.Refund = null;
                Use.Say("SMOKES_MSG_FAILED");
                End(false);
            }
            return;
        }
        // lost from the hand: switched to something else, dropped, put away by the game (a car, a rope, a struggle)
        if (!inHand && hand == null && s.Item != null && s.Item.gameObject && s.OutroAt < 0f)
        {
            // the game empties the hand for the inventory and other menus: wait, then put the smoke back in the hand
            bool overlay = false;
            try { overlay = InterfaceManager.IsOverlayActiveImmediate(); } catch { }
            if (s.LostAt < 0f) { s.LostAt = Time.unscaledTime; Safe.Info($"The hand went empty (overlay {overlay}): the smoke waits"); }
            if (overlay) { s.LostAt = Time.unscaledTime; return; }
            if (Time.unscaledTime - s.LostAt < 6f)
            {
                if (Time.unscaledTime >= s.NextReequip)
                {
                    s.NextReequip = Time.unscaledTime + 1f;
                    try
                    {
                        bool ready = true;
                        try { ready = pm.IsReadyToEquip(); } catch { }
                        if (ready) { Safe.Info("The smoke goes back into the hand"); pm.EquipItem(s.Item, false); }
                    }
                    catch (Exception e) { Safe.Log("Back into the hand", e); }
                }
                return;
            }
        }
        // 11-10 logs: after the inventory the game puts its «last item» back in the hand (the matches we were lit with, a revolver held
        // before), and the smoke was thrown away. Something showing up in the hand right after a menu that the player did not pick in
        // that menu is the game's doing: the smoke goes back instead. Picked in the menu (a lamp from the inventory) → the smoke ends.
        bool pickedInMenu = hand != null && ChosenInMenu == hand.Pointer && Time.unscaledTime - ChosenInMenuAt < 30f;
        if (!inHand && hand != null && s.Item != null && s.Item.gameObject && s.OutroAt < 0f && !pickedInMenu
            && s.LostAt >= 0f && Time.unscaledTime - s.LostAt < 6f && s.LostAt - MenuKeptAt < 4f)
        {
            if (s.LostAt < 0f) s.LostAt = Time.unscaledTime;
            if (Time.unscaledTime >= s.NextReequip)
            {
                s.NextReequip = Time.unscaledTime + 1f;
                Safe.Info($"The game put {hand.name} in the hand after the menu (not picked there): the smoke goes back into the hand");
                try { pm.EquipItem(s.Item, false); } catch (Exception e) { Safe.Log("Back into the hand", e); }
            }
            return;
        }
        if (inHand && s.LostAt >= 0f) { s.LostAt = -1f; Safe.Info("The smoke is back in the hand"); }
        if (!inHand)
        {
            Safe.Info($"The smoke left the hand ({(hand == null ? "empty" : !hand ? "destroyed item" : hand.name)}): thrown away");
            End(false);
            return;
        }
        // a pipe being taken out of the mouth: the hand's animation, then the end
        if (s.OutroAt >= 0f)
        {
            if (Time.unscaledTime - s.OutroAt >= (s.Lit.Smoke == SmokeType.Pipe ? Visual.PipeOutroTime : Visual.FlickTime)) End(true);
            return;
        }
        // burning: real seconds while the game runs, or game time
        if (Settings.RealTime.Value)
        {
            if (!GameManager.m_IsPaused && Time.timeScale > 0f) s.Smoked += Time.unscaledDeltaTime;
        }
        else
        {
            float now = Habit.Now;
            if (now > s.LastGameHour) s.Smoked += now - s.LastGameHour;
            s.LastGameHour = now;
        }
        if (Share >= 1f)
        {
            Use.Say(s.Lit.Smoke == SmokeType.Pipe ? "SMOKES_MSG_PIPE_DONE" : "SMOKES_MSG_DONE");
            End(true);
            return;
        }
        try
        {
            if (InputManager.GetHolsterPressed(pm))
            {
                Use.Say(s.Lit.Smoke == SmokeType.Pipe ? "SMOKES_MSG_PIPE_OUT" : "SMOKES_MSG_THROWN");
                End(true);
                return;
            }
        }
        catch (Exception e)
        {
            Safe.Log("H key", e);
        }
        // drags
        if (s.DragStart < 0f && Time.time >= s.NextDrag && !GameManager.m_IsPaused)
        {
            s.DragStart = Time.time;
            s.Exhaled = false;
            s.DragSounded = false;
        }
        if (s.DragStart >= 0f)
        {
            float t = Time.time - s.DragStart;
            if (!s.DragSounded && t >= DragUp * .7f)
            {
                s.DragSounded = true;
                Sounds.Drag(s.Lit.Smoke);
            }
            if (!s.Exhaled && t >= DragUp + DragHold)
            {
                s.Exhaled = true;
                Sounds.Exhale();
                Visual.Exhale(s.Lit.Smoke);
            }
            if (t >= DragUp + DragHold + DragDown)
            {
                s.DragStart = -1f;
                s.NextDrag = Time.time + UnityEngine.Random.Range(Settings.DragMinSeconds.Value, Math.Max(Settings.DragMinSeconds.Value, Settings.DragMaxSeconds.Value));
            }
        }
        // what is left shows on the hand's bar and as the item's condition
        try
        {
            var item = s.Item!;
            float left = 1f - Share;
            if (item.m_TorchItem is { } torch) torch.m_ElapsedBurnMinutes = torch.m_BurnLifetimeMinutes * Share * .999f;
            float max = item.m_GearItemData != null && item.m_GearItemData.m_MaxHP > 0f ? item.m_GearItemData.m_MaxHP : 100f;
            item.CurrentHP = Math.Max(max * .01f, max * left);
        }
        catch (Exception e)
        {
            Safe.Log("Burn bar", e);
        }
        Visual.Update(s.Item!, s.Lit, Share, DragGlow());
    }

    /// <summary>What a coop friend should see: 0 the smoke in the hand, 1 the hand at the mouth (a drag), 2 a pipe in the teeth, hand away.</summary>
    public static int CoopPhase()
    {
        if (s == null || s.Ending || s.Item == null || !s.Equipped) return 0;
        if (s.Lit.Smoke == SmokeType.Pipe) return Visual.PipePhase;
        return DragAmount() > .45f ? 1 : 0;
    }

    /// <summary>0 at rest, 1 at the top of a drag.</summary>
    static float DragAmount()
    {
        if (s == null || s.DragStart < 0f) return 0f;
        float t = Time.time - s.DragStart;
        if (t < DragUp) return Smooth(t / DragUp);
        if (t < DragUp + DragHold) return 1f;
        return 1f - Smooth(Math.Min(1f, (t - DragUp - DragHold) / DragDown));
    }

    static float DragGlow()
    {
        if (s == null || s.DragStart < 0f) return 0f;
        float t = Time.time - s.DragStart - DragUp * .6f;
        return t < 0f || t > DragHold + .3f ? 0f : (float)Math.Sin(Math.PI * t / (DragHold + .3f));
    }

    static float Smooth(float x) => x * x * (3f - 2f * x);

    public static void LateTick()
    {
        if (s == null || s.Ending || s.Item == null || !s.Equipped) return;
        if (Settings.BareHands.Value) Hands.Bare();
        Visual.Late(s.Item, s.Lit, s.Lit.Smoke == SmokeType.Pipe ? 0f : DragAmount(), Since(), s.OutroAt >= 0f ? Time.unscaledTime - s.OutroAt : -1f);
    }

    /// <summary>finished: smoked to the end, or thrown away by the player; either way the effects count by the share smoked.</summary>
    public static void End(bool byPlayer)
    {
        Sfx.StopEmber(.1f);
        if (s == null || s.Ending) return;
        // a pipe in the mouth is first taken out by the hand; a cigarette or cigar flicked away
        if (byPlayer && s.Equipped && s.OutroAt < 0f && s.Item != null)
        {
            s.OutroAt = Time.unscaledTime;
            s.DragStart = -1f;
            return;
        }
        s.Ending = true;
        float share = Share;
        try
        {
            Habit.Smoked(s.Stats, share * s.EffectMul, s.Lit.Id);
        }
        catch (Exception e)
        {
            Safe.Log("Smoke effects", e);
        }
        Visual.Pose(s.Item, 0f);
        Visual.Throw(s.Lit);
        Visual.Reset();
        Hands.Restore();
        var pm = GameManager.GetPlayerManagerComponent();
        try
        {
            if (byPlayer && pm != null && IsOurs(pm.m_ItemInHands))
            {
                OwnUnequip = true;
                // after our own put-away (flick / pipe out) the hand is already out of view: gone at once, no torch holster animation
                // (that brought the hand back up with a jerk and pressed it down)
                try { if (s.OutroAt >= 0f) pm.UnequipImmediate(false); else pm.UnequipItemInHands(); } finally { OwnUnequip = false; }
            }
        }
        catch (Exception e)
        {
            Safe.Log("Put the smoke down", e);
        }
    }

    /// <summary>The item goes once it has left the hand.</summary>
    static void Finish()
    {
        var pm = GameManager.GetPlayerManagerComponent();
        var item = s!.Item;
        if (item != null && pm != null && pm.m_ItemInHands != null && pm.m_ItemInHands.Pointer == item.Pointer)
        {
            // still being put away: a moment for the game's own animation, then at once
            if (!finishWaitStarted)
            {
                finishWaitStarted = true;
                finishWaitUntil = Time.unscaledTime + 1.5f;
            }
            if (Time.unscaledTime < finishWaitUntil) return;
            OwnUnequip = true;
            try { pm.UnequipImmediate(false); }
            catch (Exception e) { Safe.Log("Unequip at once", e); }
            finally { OwnUnequip = false; }
        }
        finishWaitStarted = false;
        Destroy(item);
        s = null;
    }

    static bool finishWaitStarted;
    static float finishWaitUntil;

    static void Destroy(GearItem? item)
    {
        if (item == null) return;
        try
        {
            var inv = GameManager.GetInventoryComponent();
            if (inv != null && item.m_InPlayerInventory) inv.DestroyGear(item.gameObject);
            else UnityEngine.Object.Destroy(item.gameObject);
        }
        catch (Exception e)
        {
            Safe.Log("Destroy the smoke", e);
        }
    }

    /// <summary>A lit smoke dropped from the pack: a cigarette or cigar is thrown away; a pipe goes out and the pipe itself is dropped
    /// in its place (the lit stand-in could not be picked up).</summary>
    public static void Dropped(GearItem lit)
    {
        bool ours = IsOurs(lit);
        GearItem? pipe = ours && s!.Lit.Smoke == SmokeType.Pipe ? SourcePipe : null;
        Safe.Info($"A lit smoke was dropped ({lit.name}, ours {ours}, pipe {(pipe != null ? pipe.name : "none")})");
        if (ours) { s!.Item = null; End(false); }
        try { UnityEngine.Object.Destroy(lit.gameObject); } catch (Exception e) { Safe.Log("Dropped lit smoke", e); }
        if (pipe != null) MelonLoader.MelonCoroutines.Start(DropPipe(pipe));
    }

    static System.Collections.IEnumerator DropPipe(GearItem pipe)
    {
        yield return null;
        yield return null;
        try
        {
            if (pipe != null && pipe && pipe.m_InPlayerInventory) { pipe.Drop(1, true, true, true); Safe.Info("The pipe went out and lies where it was dropped"); }
        }
        catch (Exception e) { Safe.Log("Drop the pipe", e); }
    }

    // ---- lit items brought back by a load: a scene change while smoking re-makes the inventory; the smoke goes on. Any other is thrown away.

    static readonly List<(GearItem g, float at)> orphans = new();

    public static void Orphan(GearItem g) => orphans.Add((g, Time.unscaledTime));

    static void TidyOrphans()
    {
        if (orphans.Count == 0) return;
        var list = orphans.ToList();
        orphans.Clear();
        foreach (var (g, at) in list)
        {
            if (g == null) continue;
            bool itemGone = s?.Item == null;
            try { itemGone = itemGone || !s!.Item!.gameObject; } catch { itemGone = true; }
            if (s != null && !s.Ending && Defs.Is(g, out var d) && d.Id == s.Lit.Id)
            {
                bool same = false, inPack = false;
                try { same = !itemGone && s.Item!.Pointer == g.Pointer; } catch { }
                try { inPack = g.m_InPlayerInventory; } catch { }
                if (same) continue;
                // 11-09: opening the inventory makes a copy of the lit item (not in the pack) while ours is still alive;
                // adopting it put the smoke out. Only a copy in the pack (a scene change) or one with ours gone takes over.
                if (!itemGone && !inPack)
                {
                    if (Time.unscaledTime - at < 5f) orphans.Add((g, at));
                    else Safe.Debug("A copy of the lit smoke (not in the pack): left alone");
                    continue;
                }
                s.Item = g;
                s.Equipped = false;
                s.EquipTries = 0;
                s.StartedAt = Time.unscaledTime;
                s.NextEquip = Time.unscaledTime + 1f;
                s.GoneAt = -1f;
                s.LostAt = -1f;
                Visual.Reset();
                Visual.SettleUntil = Time.unscaledTime + 3f;
                Visual.ResumeLock = true;
                s.Resumed = true;
                // log 1638: the reloaded item came while the old one still existed, and was thrown away; the new one always wins
                Safe.Info($"The smoke came back with the scene: going on (old item gone {itemGone})");
                continue;
            }
            Safe.Debug("A lit smoke from a save: thrown away");
            Destroy(g);
        }
    }
}

[HarmonyPatch(typeof(GearItem), nameof(GearItem.Drop))]
internal static class LitDropPatch
{
    static void Postfix(GearItem __instance, GearItem __result)
    {
        try
        {
            var g = __result ?? __instance;
            if (g != null && Defs.Is(g, out var d) && d.Kind == Kind.Lit) Smoking.Dropped(g);
        }
        catch (Exception e) { Safe.Log("Lit drop", e); }
    }
}

/// <summary>No running, no crouching, no swinging with a smoke in the hand; H is ours.</summary>
[HarmonyPatch]
internal static class SmokingRules
{
    [HarmonyPostfix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.PlayerCanSprint))]
    static void NoSprint(ref bool __result)
    {
        if (Smoking.Active) __result = false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(InputManager), nameof(InputManager.CanExecuteCrouchAction))]
    static bool NoCrouch(ref bool __result)
    {
        if (!Smoking.Active) return true;
        __result = false;
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(InputManager), nameof(InputManager.ProcessFireAction))]
    static bool NoSwing() => !Smoking.Active || !Smoking.IsOurs(GameManager.GetPlayerManagerComponent()?.m_ItemInHands);

    [HarmonyPrefix, HarmonyPatch(typeof(InputManager), nameof(InputManager.ExecuteAltFire))]
    static bool NoAltFire() => !Smoking.Active || !Smoking.IsOurs(GameManager.GetPlayerManagerComponent()?.m_ItemInHands);

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.UnequipItemInHands))]
    static bool KeepA() => !Smoking.KeepInHand("unequip");

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.UnequipItemInHandsSkipAnimation))]
    static bool KeepB() => !Smoking.KeepInHand("unequip at once");

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.UnequipImmediate))]
    static bool KeepC() => !Smoking.KeepInHand("unequip immediate");

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.StowItem))]
    static bool KeepD(ref bool __result)
    {
        if (!Smoking.KeepInHand("stow")) return true;
        __result = false;
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Panel_Inventory), nameof(Panel_Inventory.Enable), new[] { typeof(bool) })]
    static void InventoryOpens(bool enable)
    {
        if (enable) Smoking.InventoryAt = Time.unscaledTime;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.CanHolsterItemInHand))]
    static void NoHolster(ref bool __result)
    {
        if (Smoking.Active && Smoking.IsOurs(GameManager.GetPlayerManagerComponent()?.m_ItemInHands)) __result = false;
    }

    /// <summary>A smoke is a torch copy only for its hand rig: animals must not see a lit torch in a smoker's hand
    /// (wolves keep away from one).
    /// Runs first, so the coop mod's own postfix (a friend's real torch, sent over the net) still has the last word.</summary>
    [HarmonyPostfix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(AiTarget), nameof(AiTarget.HoldingLitTorch))]
    static void NoTorchForAnimals(AiTarget __instance, ref bool __result)
    {
        try
        {
            if (!__result || __instance == null || __instance.m_BaseAi != null || __instance.m_Npc != null) return;
            if (Smoking.IsOurs(GameManager.GetPlayerManagerComponent()?.m_ItemInHands)) __result = false;
        }
        catch { }
    }

    /// <summary>The «light torch» prompt and panel never come up for ours.</summary>
    [HarmonyPostfix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.PlayerHoldingTorchThatCanBeLit))]
    static void NotATorch(ref bool __result)
    {
        if (Smoking.Active && Smoking.IsOurs(GameManager.GetPlayerManagerComponent()?.m_ItemInHands)) __result = false;
    }
}
