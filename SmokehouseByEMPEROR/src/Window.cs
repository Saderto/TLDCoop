namespace SmokehouseByEMPEROR;

/// <summary>The barrel's window: what hangs inside, the fire and its wood, and what in the pack can go in.</summary>
public static partial class Window
{
    static GearItem? item;
    static BarrelState? state;
    static GearItem? taking;
    static float takingUntil;
    /// <summary>This barrel is being taken through our button: the game's own pick-up must go on.</summary>
    public static bool IsTaking(GearItem g)
    {
        try { return taking != null && Time.unscaledTime < takingUntil && taking.Pointer == g.Pointer; }
        catch { return false; }
    }
    static bool salt;
    static int page;
    static int blockFrames;
    static float nextScan, openedAt;

    record RawEntry(GearItem G, Species S, string Label);
    record WoodEntry(GearItem G, string Label, int Count, float Hours);
    static readonly List<RawEntry> raws = new();
    static readonly List<WoodEntry> woods = new();
    static int saltCount;

    public static bool IsOpen => item != null && state != null;
    public static bool IsOpenOn(BarrelState b) => state == b;

    public static void Open(GearItem g)
    {
        item = g;
        state = Barrels.Ensure(g);
        page = 0;
        nextScan = 0f;
        openedAt = Time.unscaledTime;
        Safe.Debug("Window opened");
    }

    public static void Close()
    {
        if (item != null) blockFrames = 3;
        item = null;
        state = null;
    }

    public static void Update()
    {
        Ui.Capture();
        RemoteTick();
        if (!IsOpen)
        {
            if (blockFrames > 0) { blockFrames--; Ui.BlockGameInput(true); }
            else Ui.BlockGameInput(false);
            return;
        }
        bool gone = false;
        try { gone = item == null || item.gameObject == null || !item.gameObject.activeInHierarchy || item.m_InPlayerInventory; } catch { gone = true; }
        if (gone || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1) || GameManager.m_IsPaused)
        {
            Close();
            Ui.BlockGameInput(true);
            return;
        }
        Ui.BlockGameInput(true);
        // the click that opened the window is not a click on what appears under the cursor
        if (Time.unscaledTime - openedAt < .25f) Ui.DropClick();
        if (Time.unscaledTime >= nextScan) Scan();
    }

    // ---- the pack

    static bool IsWood(GearItem g)
    {
        var f = g.m_FuelSourceItem;
        if (f == null || f.m_IsTinder || f.m_BurnDurationHours <= 0f) return false;
        string n = Defs.Clean(g.name);
        if (n.IndexOf("Coal", StringComparison.OrdinalIgnoreCase) >= 0) return false;     // coal and charcoal: too hot, no smoke
        return n.IndexOf("Stick", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("wood", StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("log", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsSaltBag(GearItem g)
    {
        try { return g.m_PowderItem != null && Defs.Clean(g.name).StartsWith("GEAR_Salt", StringComparison.Ordinal); }
        catch { return false; }
    }

    static int Units(GearItem g) => g.m_StackableItem != null ? Math.Max(1, g.m_StackableItem.m_Units) : 1;

    static void Scan()
    {
        nextScan = Time.unscaledTime + .5f;
        raws.Clear();
        woods.Clear();
        saltCount = 0;
        var inv = GameManager.GetInventoryComponent();
        if (inv == null) return;
        var woodByName = new Dictionary<string, WoodEntry>();
        foreach (var o in inv.m_Items)
        {
            GearItem? g = null;
            try { g = o?.m_GearItem; } catch { }
            if (g == null) continue;
            string n = Defs.Clean(g.name);
            if (n == "GEAR_Salt") { saltCount += Units(g); continue; }
            // the 10 kg bag of the curing salt: a powder container, a pinch per piece
            if (IsSaltBag(g)) { saltCount += (int)Math.Floor(g.m_PowderItem.m_Weight.ToQuantity(1f) / Tune.SaltKg + .001f); continue; }
            var sp = Defs.ByRaw(n);
            if (sp != null && g.m_FoodItem != null && !g.IsWornOut())
            {
                float kg = g.GetItemWeightKG().ToQuantity(1f);
                raws.Add(new RawEntry(g, sp, $"{g.DisplayName}   {Texts.Get("KG", kg.ToString("0.00"))}   {(int)Math.Round(g.GetNormalizedCondition() * 100f)}%"));
                continue;
            }
            if (IsWood(g))
            {
                woodByName.TryGetValue(n, out var w);
                woodByName[n] = new WoodEntry(w?.G ?? g, g.DisplayName, (w?.Count ?? 0) + Units(g), g.m_FuelSourceItem.m_BurnDurationHours * Tune.FuelPerBurnHour);
            }
        }
        woods.AddRange(woodByName.Values.OrderBy(w => w.Hours));
    }

    static void TakeOne(GearItem g)
    {
        var inv = GameManager.GetInventoryComponent();
        if (g.m_StackableItem != null && g.m_StackableItem.m_Units > 1) inv.RemoveUnits(g, 1);
        else inv.DestroyGear(g.gameObject);
        nextScan = 0f;
    }

    static bool TakeSalt()
    {
        var inv = GameManager.GetInventoryComponent();
        foreach (var o in inv.m_Items)
        {
            var g = o?.m_GearItem;
            if (g == null || Defs.Clean(g.name) != "GEAR_Salt") continue;
            TakeOne(g);
            return true;
        }
        foreach (var o in inv.m_Items)
        {
            var g = o?.m_GearItem;
            if (g == null || !IsSaltBag(g) || g.m_PowderItem.m_Weight.ToQuantity(1f) < Tune.SaltKg - .001f) continue;
            g.m_PowderItem.RemovePowder(ItemWeight.FromKilograms(Tune.SaltKg));
            Safe.Debug($"Salt from the bag, {g.m_PowderItem.m_Weight.ToQuantity(1f):0.00} kg left");
            if (g.m_PowderItem.m_Weight.ToQuantity(1f) < .001f) inv.DestroyGear(g.gameObject);
            nextScan = 0f;
            return true;
        }
        return false;
    }

    // ---- what the buttons do

    static int CookingLevel()
    {
        try { return GameManager.GetSkillCooking().GetCurrentTierNumber() + 1; }
        catch { return 1; }
    }

    static void SetMode(BarrelState b, bool cold)
    {
        if (b.Cold == cold) return;
        if (b.Started) { Barrels.Say("MSG_MODE_BUSY"); return; }
        if (cold && CookingLevel() < Tune.ColdSkillLevel) { Barrels.Say("MSG_SKILL", Tune.ColdSkillLevel); return; }
        if (Remote) { Ask(b, new Op { Kind = "mode", Cold = cold }, null); return; }
        b.Cold = cold;
        Barrels.Changed(b);
    }

    static void PutIn(BarrelState b, RawEntry e)
    {
        int i = Array.FindIndex(b.Slots, s => s == null);
        if (i < 0) { Barrels.Say("MSG_FULL"); return; }
        var g = e.G;
        if (g == null || g.m_FoodItem == null) return;
        float kg = g.GetItemWeightKG().ToQuantity(1f), rawCal = g.m_FoodItem.m_CaloriesRemaining, ratio = 1f;
        try
        {
            // the smoked piece is as nourishing as the game's cooked one made of the same raw piece
            var cooked = g.m_Cookable != null ? g.m_Cookable.m_CookedPrefab : null;
            var cw = cooked != null ? cooked.GetComponent<FoodWeight>() : null;
            if (cw != null && g.m_FoodWeight != null && g.m_FoodWeight.m_CaloriesPerKG > 0f) ratio = cw.m_CaloriesPerKG / g.m_FoodWeight.m_CaloriesPerKG;
        }
        catch (Exception ex) { Safe.Log("Cooked ratio", ex); }
        var slot = new Slot { Raw = Defs.Clean(g.name), Species = e.S.Id, Kg = kg, RawCal = rawCal, Cal = rawCal * Mathf.Clamp(ratio, .5f, 2f), Cond = g.GetNormalizedCondition() };
        if (Remote) { slot.Salted = salt && saltCount > 0; Ask(b, new Op { Kind = "put", Piece = slot }, g); return; }
        if (salt && saltCount > 0) slot.Salted = TakeSalt();
        GameManager.GetInventoryComponent().DestroyGear(g.gameObject);
        b.Slots[i] = slot;
        nextScan = 0f;
        Barrels.Changed(b);
        Safe.Debug($"Put in: {slot.Raw} {kg:0.00} kg, {rawCal:0} kcal raw, {slot.Cal:0} smoked, salted {slot.Salted}");
    }

    static void TakeOut(BarrelState b, int i)
    {
        var s = b.Slots[i];
        if (s == null) return;
        if (Remote) { Ask(b, new Op { Kind = "take", Slot = i, Raw = s.Raw, Kg = s.Kg }, null); return; }
        if (!Give(s, b.Done(s), b.Cold)) return;
        b.Slots[i] = null;
        nextScan = 0f;
        Barrels.Changed(b);
    }

    /// <summary>The piece comes out of the barrel into the pack: smoked if it is done, raw as it went in otherwise.</summary>
    static bool Give(Slot s, bool done, bool cold)
    {
        var sp = Defs.SpeciesById(s.Species);
        GearItem? got;
        if (done && sp != null)
        {
            var def = Defs.ById(Defs.OutId(sp, !cold, s.Salted))!;
            got = Items.GiveFood(Defs.Prefix + def.Id, s.Kg * def.WeightFactor, s.Cal, Math.Min(1f, s.Cond + Tune.ConditionGain));
            if (got != null)
            {
                try { GameManager.GetSkillsManager().IncrementPointsAndNotify(SkillType.Cooking, 1, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox); }
                catch (Exception e) { Safe.Log("Cooking skill", e); }
            }
        }
        else got = Items.GiveFood(s.Raw, s.Kg, s.RawCal, s.Cond);
        nextScan = 0f;
        return got != null;
    }

    static GearItem? Starter()
    {
        GearItem? best = null;
        foreach (var o in GameManager.GetInventoryComponent().m_Items)
        {
            var g = o?.m_GearItem;
            var f = g != null ? g.m_FireStarterItem : null;
            if (f == null || f.m_RequiresSunLight || f.m_IsAccelerant || g.IsWornOut()) continue;
            if (g.name.StartsWith("GEAR_SeagullSmoke_Lit", StringComparison.Ordinal)) continue;
            // matches first, then what wears
            if (best == null || (f.m_ConsumeOnUse && !best.m_FireStarterItem.m_ConsumeOnUse)) best = g;
        }
        return best;
    }

    static void Light(BarrelState b)
    {
        if (b.Fuel <= 0f) { Barrels.Say("MSG_NO_FUEL"); return; }
        bool indoors = false;
        try { indoors = GameManager.GetWeatherComponent().IsIndoorEnvironment(); } catch { }
        if (indoors) { Barrels.Say("MSG_INDOORS"); return; }
        var st = Starter();
        if (st == null) { Barrels.Say("MSG_NO_STARTER"); return; }
        if (Remote) { Ask(b, new Op { Kind = "light" }, st); return; }
        Strike(st);
        b.Last = Barrels.Now;
        b.Lit = true;
        Barrels.Changed(b);
    }

    static void Strike(GearItem st)
    {
        var f = st.m_FireStarterItem;
        if (f.m_ConsumeOnUse) TakeOne(st);
        else st.CurrentHP = Math.Max(0f, st.CurrentHP - f.m_ConditionDegradeOnUse);
        try { if (!string.IsNullOrEmpty(f.m_OnUseSoundEvent)) GameAudioManager.PlaySound(f.m_OnUseSoundEvent, GameManager.GetPlayerObject()); } catch { }
    }

    static void PutOut(BarrelState b)
    {
        if (Remote) { Ask(b, new Op { Kind = "out" }, null); return; }
        b.Lit = false;
        Barrels.Changed(b);
    }

    static void AddWood(BarrelState b, WoodEntry w)
    {
        if (b.Fuel + w.Hours > Tune.FuelMaxHours + .01f) { Barrels.Say("MSG_FUEL_FULL"); return; }
        if (w.G == null) return;
        if (Remote) { Ask(b, new Op { Kind = "wood", Hours = w.Hours, Raw = Defs.Clean(w.G.name) }, w.G); return; }
        string wood = Defs.Clean(w.G.name);
        TakeOne(w.G);
        b.Fuel += w.Hours;
        (b.Logs ??= new()).Add(new Log { Name = wood, Hours = w.Hours, Full = w.Hours });
        Barrels.Changed(b);
    }

    /// <summary>Logs the fire has not touched go back into the pack.</summary>
    static void TakeWood(BarrelState b)
    {
        if (b.Lit) { Barrels.Say("MSG_WOOD_LIT"); return; }
        if (Remote) { Ask(b, new Op { Kind = "unwood" }, null); return; }
        var names = Barrels.TakeLogs(b);
        if (names.Count == 0) { Barrels.Say("MSG_WOOD_BURNT"); return; }
        GiveLogs(names);
        Barrels.Changed(b);
    }

    internal static void GiveLogs(List<string>? names)
    {
        if (names == null || names.Count == 0) return;
        var pm = GameManager.GetPlayerManagerComponent();
        foreach (var n in names)
        {
            try { pm.InstantiateItemInPlayerInventory(n, 1, 1f, PlayerManager.InventoryInstantiateFlags.None); }
            catch (Exception e) { Safe.Log("Give a log back", e); }
        }
        nextScan = 0f;
        Safe.Debug($"Wood back into the pack: {names.Count}");
    }

    static void PickUp(BarrelState b)
    {
        if (!b.Idle) { Barrels.Say("MSG_BUSY"); return; }
        var g = item;
        if (Remote) { Ask(b, new Op { Kind = "pickup" }, g); return; }
        GiveLogs(Barrels.TakeLogs(b));
        Barrels.State.Barrels.Remove(b);
        b.Fuel = 0f;
        b.Logs.Clear();
        Barrels.Changed(b);
        Close();
        Lift(g);
    }

    static void Lift(GearItem? g)
    {
        if (g == null) return;
        // straight into the pack, no inspect screen; the game's pick-up is let through only for this call
        taking = g;
        takingUntil = Time.unscaledTime + 1f;
        // AddItemToPlayerInventory, not ProcessPickupWithNoInspectScreen: that one also puts the item into the hands («Снаряжён»)
        try
        {
            var pm = GameManager.GetPlayerManagerComponent();
            pm.AddItemToPlayerInventory(g, true, false);
            Place.OutOfHands(pm, g);
            GameAudioManager.PlayGUIButtonClick();
        }
        catch (Exception e) { Safe.Log("Take the barrel", e); }
        try { if (g.m_InPlayerInventory) taking = null; } catch { }
    }

    // ---- drawing

    const float W = 640f, Pad = 22f, RowH = 28f;

    static bool Btn(Rect r, string text, bool enabled = true, bool selected = false) => Ui.Button(r, text, enabled && !Waiting, selected) && !Waiting;

    public static void Draw()
    {
        if (!IsOpen) return;
        var b = state!;
        Ui.Begin();
        const int perPage = 4;
        int pages = Math.Max(1, (raws.Count + perPage - 1) / perPage);
        if (page >= pages) page = 0;
        int shown = Math.Min(perPage, Math.Max(0, raws.Count - page * perPage));
        float h = 52f + 34f + 30f + Tune.Slots * (RowH + 3f) + 12f + 34f + 30f + 34f + 12f + 28f + Math.Max(1, shown) * (RowH + 3f) + 34f + 44f + 10f;
        float x = (Ui.SW - W) / 2f, y = Math.Max(14f, (Ui.SH - h) / 2f);
        Ui.Panel(new Rect(x, y, W, h), Texts.Get("TITLE") + (Waiting ? "   " + Texts.Get("WAIT_HOST") : ""));
        float lx = x + Pad, cw = W - Pad * 2f, cy = y + 52f;

        // the way of smoking
        if (Btn(new Rect(lx, cy, cw / 2f - 4f, 28f), Texts.Get("MODE_HOT"), true, !b.Cold)) SetMode(b, false);
        if (Btn(new Rect(lx + cw / 2f + 4f, cy, cw / 2f - 4f, 28f), Texts.Get("MODE_COLD"), true, b.Cold)) SetMode(b, true);
        cy += 34f;

        // the fire
        string fire = b.Lit ? Ui.Colored(Texts.Get("LIT"), Ui.Fire) : Texts.Get("UNLIT");
        string fuel = b.Fuel > .004f ? Texts.Get("FUEL", Texts.Time(b.Fuel)) : Texts.Get("NOFUEL");
        Ui.Label(new Rect(lx, cy, cw - 330f, 26f), fire + ".  " + fuel);
        if (Btn(new Rect(lx + cw - 322f, cy, 156f, 26f), Texts.Get("TAKE_WOOD"), !b.Lit && b.Logs != null && b.Logs.Any(l => l.Whole))) TakeWood(b);
        if (b.Lit)
        {
            if (Btn(new Rect(lx + cw - 160f, cy, 160f, 26f), Texts.Get("PUT_OUT"))) PutOut(b);
        }
        else if (Btn(new Rect(lx + cw - 160f, cy, 160f, 26f), Texts.Get("LIGHT"), b.Fuel > 0f)) Light(b);
        cy += 30f;

        // what hangs inside
        for (int i = 0; i < Tune.Slots; i++)
        {
            var r = new Rect(lx, cy, cw, RowH);
            Ui.Row(r);
            var s = b.Slots[i];
            if (s == null) Ui.Label(new Rect(lx + 10f, cy, 200f, RowH), Ui.Colored($"{i + 1}.  {Texts.Get("EMPTY")}", Ui.Dim));
            else
            {
                var sp = Defs.SpeciesById(s.Species);
                bool done = b.Done(s);
                var def = sp != null ? Defs.ById(Defs.OutId(sp, !b.Cold, false)) : null;
                if (def != null) Ui.Icon(new Rect(lx + 4f, cy + 1f, RowH - 2f, RowH - 2f), Assets.Texture(def.Icon, compress: false));
                string name = sp == null ? s.Raw : Texts.Russian ? sp.Ru : char.ToUpperInvariant(sp.En[0]) + sp.En.Substring(1);
                if (s.Salted) name += ", " + Texts.Get("SALTED");
                Ui.Label(new Rect(lx + RowH + 8f, cy, 250f, RowH), name + "   " + Ui.Colored(Texts.Get("KG", s.Kg.ToString("0.00")), Ui.Dim));
                Ui.Bar(new Rect(lx + 300f, cy + RowH - 6f, 170f, 3f), s.Hours / b.Need, done ? Ui.Good : Ui.Accent);
                Ui.Label(new Rect(lx + 300f, cy - 2f, 170f, RowH), done ? Ui.Colored(Texts.Get("DONE"), Ui.Good) : Texts.Get("LEFT", Texts.Time(b.Need - s.Hours)));
                if (Btn(new Rect(lx + cw - 112f, cy + 2f, 110f, RowH - 4f), Texts.Get(done ? "TAKE" : "TAKE_RAW"))) TakeOut(b, i);
            }
            cy += RowH + 3f;
        }
        cy += 12f;

        // wood
        Ui.Label(new Rect(lx, cy, cw, 26f), woods.Count > 0 ? Texts.Get("ADD_WOOD") : Ui.Colored(Texts.Get("NO_WOOD_INV"), Ui.Dim));
        cy += 30f;
        float bx = lx;
        int nw = Math.Min(3, woods.Count);
        for (int i = 0; i < nw; i++)
        {
            var w = woods[i];
            float bw = (cw - (nw - 1) * 6f) / Math.Max(nw, 2);
            if (Btn(new Rect(bx, cy, bw, 28f), $"{w.Label} ×{w.Count}  (+{Texts.Time(w.Hours)})")) AddWood(b, w);
            bx += bw + 6f;
        }
        cy += 34f + 12f;

        // the pack
        Ui.Label(new Rect(lx, cy, 300f, 26f), raws.Count > 0 ? Texts.Get("PUT_IN") : Ui.Colored(Texts.Get("NO_RAW"), Ui.Dim));
        if (pages > 1 && Btn(new Rect(lx + cw - 150f, cy, 150f, 24f), Texts.Get("MORE", $"{page + 1}/{pages}"))) page = (page + 1) % pages;
        cy += 28f;
        for (int k = 0; k < Math.Max(1, shown); k++)
        {
            if (k < shown)
            {
                var e = raws[page * perPage + k];
                if (Btn(new Rect(lx, cy, cw, RowH), e.Label)) { PutIn(b, e); break; }
            }
            cy += RowH + 3f;
        }
        salt = Ui.Toggle(new Rect(lx, cy, cw, 28f), salt && saltCount > 0, Texts.Get("SALT", saltCount));
        cy += 34f;

        if (Btn(new Rect(lx, cy, 200f, 32f), Texts.Get("PICK_UP"), b.Idle)) { PickUp(b); return; }
        if (Ui.Button(new Rect(lx + cw - 200f, cy, 200f, 32f), Texts.Get("CLOSE"))) Close();
    }
}
