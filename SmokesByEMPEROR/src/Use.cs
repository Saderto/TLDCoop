namespace SmokesByEMPEROR;

/// <summary>What «use» does on each of ours: light a cigarette from a pack, open a tube, cut or light a cigar, fill and light a pipe.
/// Caught at the inventory's general use call, before the game would treat our packs as painkillers.</summary>
[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.UseInventoryItem), new[] { typeof(GearItem), typeof(bool) })]
internal static class UsePatch
{
    [HarmonyPriority(Priority.First)]
    static bool Prefix(GearItem gi, ref bool __result)
    {
        if (!Defs.Is(gi, out var def) || def.Kind == Kind.Lighter) return true;
        __result = false;
        try
        {
            __result = Use.Do(gi, def);
        }
        catch (Exception e)
        {
            Safe.Log("Use " + def.Id, e);
        }
        return false;
    }
}

/// <summary>Should the first aid panel hand one of ours on anyway: the same action, never a pill.</summary>
[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.UseFirstAidItem))]
internal static class FirstAidUsePatch
{
    [HarmonyPriority(Priority.First)]
    static bool Prefix(GearItem gi, ref bool __result)
    {
        if (!Defs.Is(gi, out var def)) return true;
        __result = false;
        try
        {
            __result = Use.Do(gi, def);
        }
        catch (Exception e)
        {
            Safe.Log("First aid use " + def.Id, e);
        }
        return false;
    }
}

public static class Use
{
    static readonly string[] Knives = { "GEAR_Knife", "GEAR_KnifeImprovised", "GEAR_KnifeScrapMetal", "GEAR_JeremiahKnife", "GEAR_KnifeCougarClaw" };

    public static void Say(string key, params object[] args)
    {
        try
        {
            HUDMessage.AddMessage(Texts.Get(key, args), false, true);
        }
        catch (Exception e)
        {
            Safe.Log("HUD message", e);
        }
    }

    public static void Error(string key)
    {
        Safe.Info("Use: " + key);
        try { GameAudioManager.PlayGUIError(); }
        catch { }
        Say(key);
    }

    public static bool Do(GearItem gi, Def def)
    {
        Safe.Info($"Use {def.Id} ({def.Kind}), units {Items.Units(gi)}");
        switch (def.Kind)
        {
            case Kind.Pack:
                return Light(def, () => Items.TakeOne(gi), 1f);
            case Kind.CigarCut:
            {
                // a torn one smokes for half the effect
                float mul = Torn.Contains(gi.GetInstanceID()) ? .5f : 1f;
                return Light(def, () => Items.TakeOne(gi), mul);
            }
            case Kind.Cigar:
                return Cut(gi, def);
            case Kind.Cutter:
            {
                var cigar = Items.Find(d => d.Kind == Kind.Cigar);
                if (cigar == null)
                {
                    Error("SMOKES_MSG_NO_CIGAR");
                    return false;
                }
                return Cut(cigar, Defs.TryGet(cigar.name, out var cd) ? cd : def);
            }
            case Kind.Refill:
                return Lighters.Refill(gi, def);
            case Kind.Box:
            {
                Items.TakeOne(gi);
                Items.Give(def.Next!);
                Say("SMOKES_MSG_BOX_TAKEN");
                return true;
            }
            case Kind.Tube:
            {
                Items.TakeOne(gi);
                Items.Give(def.Next!);
                Say("SMOKES_MSG_TUBE_OPENED");
                return true;
            }
            case Kind.Pipe:
                return LightPipe(def, gi);
            case Kind.Pouch:
            {
                var pipe = Items.Find(d => d.Kind == Kind.Pipe);
                if (pipe == null || !Defs.Is(pipe, out var pd))
                {
                    Error("SMOKES_MSG_NO_PIPE");
                    return false;
                }
                return LightPipe(pd, pipe, gi);
            }
            case Kind.Lit:
                return false;
        }
        return false;
    }

    /// <summary>The tobacco last filled with: lighting the pipe itself takes it again if it is in the pack (a pouch's own «fill» picks it).</summary>
    static string? lastPouch;

    // the pipe item is hidden from the pack only once it really burns (set before, a pipe without tobacco vanished from the list)
    static bool LightPipe(Def pipe, GearItem pipeItem, GearItem? pouch = null)
    {
        if (pouch == null && lastPouch != null) pouch = Items.Find(d => d.Id == lastPouch);
        pouch ??= Items.Find(d => d.Kind == Kind.Pouch);
        if (pouch == null || !Defs.Is(pouch, out var pdef))
        {
            Error("SMOKES_MSG_NO_TOBACCO");
            return false;
        }
        var chosen = pouch;
        if (!Light(pipe, () => Items.TakeOne(chosen), 1f, pdef)) return false;
        Smoking.SourcePipe = pipeItem;
        lastPouch = pdef.Id;
        Sounds.PipeFill();
        try { Say("SMOKES_MSG_TOBACCO_USED", Texts.Get("SMOKES_" + pdef.Id + "_NAME")); } catch { }
        return true;
    }

    /// <summary>Cut ends of cigars cut with a knife that tore the leaf, by instance: they smoke for half the effect. Not kept over a save.</summary>
    static readonly HashSet<int> Torn = new();

    static bool Cut(GearItem cigar, Def def)
    {
        var cutter = Items.Find(d => d.Kind == Kind.Cutter);
        bool byKnife = cutter == null, torn = false;
        if (cutter != null)
        {
            float max = cutter.m_GearItemData != null && cutter.m_GearItemData.m_MaxHP > 0f ? cutter.m_GearItemData.m_MaxHP : 100f;
            cutter.CurrentHP = Math.Max(0f, cutter.CurrentHP - max * Settings.CutterWearPct.Value / 100f);
            if (cutter.CurrentHP <= 0f) GameManager.GetInventoryComponent().DestroyGear(cutter.gameObject);
        }
        else
        {
            var inv = GameManager.GetInventoryComponent();
            GearItem? knife = null;
            foreach (var k in Knives)
            {
                knife = inv.GetBestGearItemWithName(k);
                if (knife != null) break;
            }
            if (knife == null)
            {
                Error("SMOKES_MSG_NO_CUTTER");
                return false;
            }
            torn = UnityEngine.Random.value < Settings.KnifeTearChance.Value;
        }
        Items.TakeOne(cigar);
        var cut = Items.Spawn(def.Next!);
        if (cut == null) return false;
        var pm = GameManager.GetPlayerManagerComponent();
        var given = pm.InstantiateItemInPlayerInventoryFromPrefab(cut, 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
        UnityEngine.Object.Destroy(cut.gameObject);
        if (torn && given != null) Torn.Add(given.GetInstanceID());
        Sounds.Snip(byKnife);
        Say(torn ? "SMOKES_MSG_CUT_TORN" : "SMOKES_MSG_CUT");
        return true;
    }

    /// <summary>Lights def's smoke from what is at hand: a burning fire close by, a lit torch, flare, lamp or match in the hand, the Zappo, or matches.
    /// take() removes the cigarette, cigar or portion of tobacco once lighting is sure.</summary>
    static bool Light(Def def, Func<Action?> take, float effectMul, Def? from = null)
    {
        if (Smoking.Active)
        {
            Error("SMOKES_MSG_ALREADY");
            return false;
        }
        var lit = Defs.ById(def.Lit ?? "");
        if (lit == null)
        {
            Safe.Info($"Light {def.Id}: no lit item '{def.Lit}'");
            return false;
        }
        var pm = GameManager.GetPlayerManagerComponent();
        string? how = null;
        try
        {
            var hand = pm.m_ItemInHands;
            if (hand != null && (hand.IsLitTorch() || hand.IsLitFlare() || hand.IsLitLamp() || hand.IsLitMatch())) how = "HAND";
        }
        catch (Exception e)
        {
            Safe.Log("Lit thing in hand", e);
        }
        if (how == null)
        {
            try
            {
                var fm = GameManager.GetFireManagerComponent();
                var pos = GameManager.GetPlayerTransform().position;
                var fire = fm != null ? fm.GetClosestFire(pos) : null;
                if (fire != null && fire.IsBurning() && (fire.transform.position - pos).magnitude < 2.5f) how = "FIRE";
            }
            catch (Exception e)
            {
                Safe.Log("Fire nearby", e);
            }
        }
        var inv = GameManager.GetInventoryComponent();
        if (how == null)
        {
            var lighter = Items.Find(d => d.Kind == Kind.Lighter);
            if (lighter != null)
            {
                Items.TakeOne(lighter);
                how = "LIGHTER";
            }
        }
        if (how == null)
        {
            var matches = inv.GetBestMatches();
            if (matches != null && Items.Units(matches) > 0)
            {
                Items.TakeOne(matches);
                how = "MATCH";
            }
        }
        if (how == null)
        {
            Error("SMOKES_MSG_NO_FIRE");
            return false;
        }
        var refund = take();
        Safe.Info($"Lighting {lit.Id} from {how}");
        // the inventory and other panels close so the hand can come up
        try
        {
            InterfaceManager.GetPanel<Panel_Inventory>()?.Enable(false);
        }
        catch (Exception e)
        {
            Safe.Log("Closing the inventory", e);
        }
        Smoking.Start(lit, effectMul, how, refund, from ?? def);
        return true;
    }
}
