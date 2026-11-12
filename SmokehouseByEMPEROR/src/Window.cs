namespace SmokehouseByEMPEROR;

/// <summary>The window of a barrel or a kettle: what is inside, the fire and its wood, and what in the pack can go in. The sausage table's window is in Table.cs.</summary>
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
    /// <summary>11-12 owner: salt is told in its weight, not in portions.</summary>
    static string SaltKgText => (saltCount * Tune.SaltKg).ToString("0.0");
    static bool salt;
    static int page;
    static int blockFrames;
    static float nextScan, openedAt;

    record RawEntry(GearItem G, Species S, string Label, float Total = 0f);
    record WoodEntry(GearItem G, string Label, int Count, float Hours);
    static readonly List<RawEntry> raws = new();
    static readonly List<WoodEntry> woods = new();
    static int saltCount, canCount, gutCount;
    static float waterL;
    static bool table;
    static readonly Dictionary<string, float> meatKg = new();
    static string Kind => state?.Kind ?? "";

    public static bool IsOpen => item != null && (state != null || table);
    public static bool IsOpenOn(BarrelState b) => state == b;

    public static void Open(GearItem g)
    {
        item = g;
        table = Defs.Is(g, out var def) && def.Station == Defs.Table;
        state = table ? null : Barrels.Ensure(g);
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
        table = false;
    }

    public static void Update()
    {
        Ui.Capture();
        RemoteTick();
        TableTick();
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
        meatKg.Clear();
        saltCount = canCount = gutCount = 0;
        waterL = Water();
        string kind = Kind;
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
            if (n == Defs.Can) { canCount += Units(g); continue; }
            if (n == "GEAR_Gut" || n == "GEAR_GutDried") { gutCount += Units(g); continue; }
            // the 10 kg bag of the curing salt: a powder container, a pinch per piece
            if (IsSaltBag(g)) { saltCount += (int)Math.Floor(g.m_PowderItem.m_Weight.ToQuantity(1f) / Tune.SaltKg + .001f); continue; }
            var sp = Defs.ByRaw(n);
            if (sp != null && g.m_FoodItem != null && !g.IsWornOut())
            {
                float kg = g.GetItemWeightKG().ToQuantity(1f);
                if (table)
                {
                    // the table: how much of each raw meat the pack holds, the recipes count on it
                    if (Defs.ForSausage(sp)) meatKg[n] = (meatKg.TryGetValue(n, out float have) ? have : 0f) + kg;
                    continue;
                }
                if (!Defs.Takes(kind, sp)) continue;
                if (kind == Defs.Kettle)
                {
                    // the kettle: one line per kind of meat, a can takes its portion of whatever pieces there are
                    float portion = Tune.CanMeatKg;
                    int at = raws.FindIndex(r => r.S == sp && r.Total > 0f && Defs.Clean(r.G.name) == n);
                    float total = kg + (at >= 0 ? raws[at].Total : 0f);
                    string name = Texts.Russian ? sp.Ru : char.ToUpperInvariant(sp.En[0]) + sp.En.Substring(1);
                    var entry = new RawEntry(at >= 0 ? raws[at].G : g, sp, $"{name}   {Texts.Get("K_PORTION", portion.ToString("0.00"), total.ToString("0.00"))}", total);
                    if (at >= 0) raws[at] = entry; else raws.Add(entry);
                    continue;
                }
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

    static bool TakeNamed(params string[] names)
    {
        var inv = GameManager.GetInventoryComponent();
        foreach (var name in names)
            foreach (var o in inv.m_Items)
            {
                var g = o?.m_GearItem;
                if (g == null || Defs.Clean(g.name) != name) continue;
                TakeOne(g);
                return true;
            }
        return false;
    }

    /// <summary>Drinkable water in the pack, litres.</summary>
    static float Water()
    {
        try { return GameManager.GetPlayerManagerComponent().GetTotalLiters(LiquidType.GetPotableWater()).ToQuantity(1f); }
        catch (Exception e) { Safe.Log("Water count", e); }
        try { return GameManager.GetInventoryComponent().GetPotableWaterSupply().m_WaterSupply.m_VolumeInLiters.ToQuantity(1f); }
        catch { return 0f; }
    }

    static void TakeWater()
    {
        try { GameManager.GetPlayerManagerComponent().DeductWaterFromInventory(ItemLiquidVolume.FromLiters(Tune.CanWater), LiquidQuality.Potable); }
        catch (Exception e) { Safe.Log("Water for the can", e); }
        nextScan = 0f;
    }

    /// <summary>A portion of raw meat or fish of one kind out of the pack, the lightest pieces first. take false: only what it would be.
    /// False if the pack holds less than the portion.</summary>
    static bool Portion(string raw, float kg, bool take, out float cal, out float cond, out float ratio) => Portion(new[] { raw }, kg, take, out cal, out cond, out ratio);

    /// <summary>The same out of any of several kinds (a recipe's «заяц или куропатка», any fish). ratio: cooked calories to raw ones, by weight of what was taken.</summary>
    static bool Portion(ICollection<string> raws, float kg, bool take, out float cal, out float cond, out float ratio)
    {
        cal = 0f; cond = 1f; ratio = 1f;
        var inv = GameManager.GetInventoryComponent();
        var pieces = new List<(GearItem g, float kg)>();
        foreach (var o in inv.m_Items)
        {
            var g = o?.m_GearItem;
            if (g == null || g.m_FoodItem == null || !raws.Contains(Defs.Clean(g.name)) || g.IsWornOut()) continue;
            pieces.Add((g, g.GetItemWeightKG().ToQuantity(1f)));
        }
        if (pieces.Sum(p => p.kg) < kg - .001f) return false;
        float left = kg, condSum = 0f, ratioSum = 0f;
        foreach (var (g, w) in pieces.OrderBy(p => p.kg))
        {
            if (left <= .0005f) break;
            float part = Math.Min(left, w), all = g.m_FoodItem.m_CaloriesRemaining, c = w > 0f ? all * part / w : 0f;
            cal += c;
            condSum += g.GetNormalizedCondition() * part;
            left -= part;
            float one = 1f;
            try
            {
                var cooked = g.m_Cookable != null ? g.m_Cookable.m_CookedPrefab : null;
                var cw = cooked != null ? cooked.GetComponent<FoodWeight>() : null;
                if (cw != null && g.m_FoodWeight != null && g.m_FoodWeight.m_CaloriesPerKG > 0f) one = cw.m_CaloriesPerKG / g.m_FoodWeight.m_CaloriesPerKG;
            }
            catch (Exception ex) { Safe.Log("Cooked ratio", ex); }
            ratioSum += one * c;
            if (!take) continue;
            if (w - part < .02f) inv.DestroyGear(g.gameObject);
            else
            {
                // what is left of the piece: lighter, with its share of the calories
                g.m_FoodItem.m_CaloriesRemaining = all - c;
                g.WeightKG = ItemWeight.FromKilograms(w - part);
            }
        }
        cond = condSum / kg;
        if (cal > 0f) ratio = ratioSum / cal;
        if (take) nextScan = 0f;
        return true;
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
        bool sausage = Defs.IsSausage(e.S), can = b.Kettle && !sausage;
        if (can)
        {
            if (!Portion(slot.Raw, Tune.CanMeatKg, false, out float pc, out float pcond, out float pr)) { Barrels.Say("MSG_NEED_MEAT", Tune.CanMeatKg.ToString("0.00")); return; }
            slot.Kg = Tune.CanMeatKg; slot.RawCal = pc; slot.Cal = pc * Mathf.Clamp(pr, .5f, 2f); slot.Cond = pcond;
            // a can: an empty tin, a quarter litre of water and salt go in with the piece
            if (canCount <= 0) { Barrels.Say("MSG_NEED_CAN"); return; }
            if (waterL < Tune.CanWater - .001f) { Barrels.Say("MSG_NEED_WATER"); return; }
            if (saltCount <= 0) { Barrels.Say("MSG_NEED_SALT"); return; }
            slot.Can = slot.Salted = true;
        }
        else slot.Salted = !sausage && salt && saltCount > 0;
        if (Remote) { Ask(b, new Op { Kind = "put", Piece = slot }, can ? null : g); return; }
        if (can) { TakeNamed(Defs.Can); TakeWater(); }
        if (slot.Salted) slot.Salted = TakeSalt() || can;
        if (can) Portion(slot.Raw, Tune.CanMeatKg, true, out _, out _, out _);
        else GameManager.GetInventoryComponent().DestroyGear(g.gameObject);
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
        if (!Give(s, b.Done(s), b.Cold, b.Kind)) return;
        b.Slots[i] = null;
        nextScan = 0f;
        Barrels.Changed(b);
    }

    /// <summary>The piece comes out into the pack: smoked, canned or boiled if it is done, raw as it went in otherwise (an unused tin comes back with it).</summary>
    static bool Give(Slot s, bool done, bool cold, string? kind)
    {
        var sp = Defs.SpeciesById(s.Species);
        var def = done && sp != null ? Defs.Result(kind ?? "", sp, cold, s.Salted) : null;
        GearItem? got;
        if (def != null)
        {
            got = Items.GiveFood(Defs.Prefix + def.Id, s.Kg * def.Weight + def.Tare, s.Cal, Math.Min(1f, s.Cond + Tune.ConditionGain));
            if (got != null)
            {
                try { GameManager.GetSkillsManager().IncrementPointsAndNotify(SkillType.Cooking, 1, SkillsManager.PointAssignmentMode.AssignOnlyInSandbox); }
                catch (Exception e) { Safe.Log("Cooking skill", e); }
            }
        }
        else
        {
            got = Items.GiveFood(s.Raw, s.Kg, s.RawCal, s.Cond);
            if (got != null && s.Can)
            {
                try { GameManager.GetPlayerManagerComponent().InstantiateItemInPlayerInventory(Defs.Can, 1, 1f, PlayerManager.InventoryInstantiateFlags.None); }
                catch (Exception e) { Safe.Log("Tin back", e); }
            }
        }
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
        if (indoors) { Barrels.Say(b.Kettle ? "K_MSG_INDOORS" : "MSG_INDOORS"); return; }     // 11-12 owner: the kettle too is lit outdoors only
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
        if (!b.Idle) { Barrels.Say(b.Kettle ? "K_MSG_BUSY" : "MSG_BUSY"); return; }
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

    internal static void Lift(GearItem? g)
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
    const float CanRow = 46f;
    static readonly (string id, string need)[] CanKinds = { ("CanDeer", "K_M_DEER"), ("CanMoose", "K_M_MOOSE"), ("CanBear", "K_M_BEAR"), ("CanFish", "K_M_FISH"), ("CanPate", "K_M_PATE") };

    static bool Btn(Rect r, string text, bool enabled = true, bool selected = false) => Ui.Button(r, text, enabled && !Waiting, selected) && !Waiting;

    const float RecW = 430f;

    /// <summary>11-12 owner: the smokehouse shows its recipes too, so that the player sees at once what smoking takes.</summary>
    static void Recipes(Rect r, BarrelState b)
    {
        Ui.Panel(r, Texts.Get("R_TITLE"));
        float lx = r.x + 16f, cw = r.width - 32f, cy = r.y + 52f;
        float meat = 0f, fish = 0f; int saus = 0;
        foreach (var e in raws)
        {
            float kg = 0f;
            try { kg = e.G != null ? e.G.GetItemWeightKG().ToQuantity(1f) : 0f; } catch { }
            if (Defs.IsSausage(e.S)) saus++; else if (e.S.Fish) fish += kg; else meat += kg;
        }
        float fuel = b.Fuel;
        string Have(bool ok, string t) => Ui.Colored(t, ok ? Ui.Dim : Ui.Fire);
        void Row(string icon, string name, string need, string have, bool ok)
        {
            const float rh = 62f;
            Ui.Row(new Rect(lx, cy, cw, rh));
            Ui.Icon(new Rect(lx + 4f, cy + 6f, 50f, 50f), Assets.Texture("icons/" + icon + ".png", compress: false));
            Ui.Fit(new Rect(lx + 62f, cy, cw - 66f, 22f), name);
            Ui.Fit(new Rect(lx + 62f, cy + 20f, cw - 66f, 22f), need);
            Ui.Fit(new Rect(lx + 62f, cy + 40f, cw - 66f, 22f), Have(ok, have));
            cy += rh + 3f;
        }
        Row(b.Cold ? "ColdDeer" : "HotDeer", Texts.Get("R_MEAT"), Texts.Get("R_MEAT_NEED"), Texts.Get("K_HAVE", meat.ToString("0.00")), meat > 0f);
        Row(b.Cold ? "ColdCoho" : "HotCoho", Texts.Get("R_FISH"), Texts.Get("R_FISH_NEED"), Texts.Get("K_HAVE", fish.ToString("0.00")), fish > 0f);
        Row(b.Cold ? "SausDry" : "SausHot", Texts.Get("R_SAUS"), Texts.Get("R_SAUS_NEED"), Texts.Get("R_HAVE_N", saus), saus > 0);
        cy += 8f;
        Ui.Caption(new Rect(lx, cy, cw, 22f), Texts.Get("R_ALSO"));
        cy += 24f;
        float need = b.Cold ? Tune.ColdHours : Tune.HotHours;
        foreach (var line in new[]
        {
            (fuel >= need - .01f ? "" : "!") + Texts.Get("R_WOOD", need.ToString("0"), Texts.Time(fuel)),
            Texts.Get("R_SALT", SaltKgText),
            Texts.Get("R_SLOTS", Tune.Slots),
        })
        {
            bool warn = line.StartsWith("!");
            Ui.Fit(new Rect(lx, cy, cw, 22f), warn ? Ui.Colored(line.Substring(1), Ui.Fire) : line);
            cy += 23f;
        }
        cy += 8f;
        Ui.Caption(new Rect(lx, cy, cw, 22f), Texts.Get("R_WAYS"));
        cy += 24f;
        foreach (var k in new[] { "R_HOT", "R_HOT2", "R_COLD", "R_COLD2" })
        {
            bool mine = (k.StartsWith("R_COLD")) == b.Cold;
            Ui.Fit(new Rect(lx, cy, cw, 22f), mine ? Texts.Get(k) : Ui.Colored(Texts.Get(k), Ui.Dim));
            cy += 23f;
        }
    }

    public static void Draw()
    {
        if (!IsOpen) return;
        if (table) { DrawTable(); return; }
        var b = state!;
        bool kt = b.Kettle;
        Ui.Begin();
        const int perPage = 4;
        int pages = Math.Max(1, (raws.Count + perPage - 1) / perPage);
        if (page >= pages) page = 0;
        int shown = Math.Min(perPage, Math.Max(0, raws.Count - page * perPage));
        float h = 52f + 34f + 30f + Tune.Slots * (RowH + 3f) + 12f + 34f + 30f + 34f + 12f + 28f + (kt ? CanKinds.Length * (CanRow + 3f) - 28f : Math.Max(1, shown) * (RowH + 3f) + 34f) + 44f + 10f;
        float x = kt ? (Ui.SW - W) / 2f : Math.Max(8f, (Ui.SW - W - RecW - 10f) / 2f), y = Math.Max(14f, (Ui.SH - h) / 2f);
        if (!kt) Recipes(new Rect(x + W + 10f, y, RecW, h), b);
        Ui.Panel(new Rect(x, y, W, h), Texts.Get(kt ? "K_TITLE" : "TITLE") + (Waiting ? "   " + Texts.Get("WAIT_HOST") : ""));
        float lx = x + Pad, cw = W - Pad * 2f, cy = y + 52f;

        // the way of smoking
        if (kt)
        {
            // what one can takes, and how much of it the pack holds
            string Have(bool ok, string t) => ok ? t : Ui.Colored(t, Ui.Fire);
            Ui.Label(new Rect(lx, cy, cw, 28f), Texts.Get("K_NEED") + "  " + Have(canCount > 0, Texts.Get("K_CANS", canCount)) + ",  "
                + Have(waterL >= Tune.CanWater - .001f, Texts.Get("K_WATER", waterL.ToString("0.00"))) + ",  " + Have(saltCount > 0, Texts.Get("K_SALT", SaltKgText)));
        }
        else
        {
            if (Btn(new Rect(lx, cy, cw / 2f - 4f, 28f), Texts.Get("MODE_HOT"), true, !b.Cold)) SetMode(b, false);
            if (Btn(new Rect(lx + cw / 2f + 4f, cy, cw / 2f - 4f, 28f), Texts.Get("MODE_COLD"), true, b.Cold)) SetMode(b, true);
        }
        cy += 34f;

        // the fire
        string fire = b.Lit ? Ui.Colored(Texts.Get(kt ? "K_LIT" : "LIT"), Ui.Fire) : Texts.Get("UNLIT");
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
                var def = sp != null ? Defs.Result(b.Kind, sp, b.Cold, false) : null;
                float need = b.NeedOf(s);
                if (def != null) Ui.Icon(new Rect(lx + 4f, cy + 1f, RowH - 2f, RowH - 2f), Assets.Texture(def.Icon, compress: false));
                string name = sp == null ? s.Raw : Texts.Russian ? sp.Ru : char.ToUpperInvariant(sp.En[0]) + sp.En.Substring(1);
                if (s.Salted && !kt) name += ", " + Texts.Get("SALTED");
                Ui.Label(new Rect(lx + RowH + 8f, cy, 250f, RowH), name + "   " + Ui.Colored(Texts.Get("KG", s.Kg.ToString("0.00")), Ui.Dim));
                Ui.Bar(new Rect(lx + 300f, cy + RowH - 6f, 170f, 3f), s.Hours / need, done ? Ui.Good : Ui.Accent);
                Ui.Label(new Rect(lx + 300f, cy - 2f, 170f, RowH), done ? Ui.Colored(Texts.Get("DONE"), Ui.Good) : Texts.Get("LEFT", Texts.Time(need - s.Hours)));
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
            if (i >= woods.Count) break;
            var w = woods[i];
            float bw = (cw - (nw - 1) * 6f) / Math.Max(nw, 2);
            if (Btn(new Rect(bx, cy, bw, 28f), $"{w.Label} ×{w.Count}  (+{Texts.Time(w.Hours)})")) { AddWood(b, w); break; }
            bx += bw + 6f;
        }
        cy += 34f + 12f;

        if (kt)
        {
            // the recipes, as at the sausage table (11-12 owner): what each can is made of, and a button to set one to boil
            Ui.Label(new Rect(lx, cy, cw, 26f), Texts.Get("K_PUT_IN"));
            cy += 28f;
            bool room = b.Slots.Any(s => s == null);
            foreach (var (id, need) in CanKinds)
            {
                RawEntry? pick = null; float have = 0f;
                foreach (var e in raws)
                {
                    if (Defs.CanOf(e.S) != id) continue;
                    have += e.Total;
                    if (pick == null && e.Total >= Tune.CanMeatKg - .001f) pick = e;
                }
                Ui.Row(new Rect(lx, cy, cw, CanRow));
                Ui.Icon(new Rect(lx + 4f, cy + 3f, CanRow - 6f, CanRow - 6f), Assets.Texture("icons/" + id + ".png", compress: false));
                string meat = Texts.Get(need) + " " + Texts.Get("KG", Tune.CanMeatKg.ToString("0.00"));
                Ui.Fit(new Rect(lx + CanRow + 6f, cy - 1f, cw - CanRow - 136f, 24f), Texts.Get(id + "_NAME") + ":   " + (pick != null ? meat : Ui.Colored(meat, Ui.Fire)));
                Ui.Fit(new Rect(lx + CanRow + 6f, cy + 20f, cw - CanRow - 136f, 24f), Ui.Colored(Texts.Get("K_HAVE", have.ToString("0.00")) + "  " + Texts.Get("K_ROW"), Ui.Dim));
                bool can = pick != null && room && canCount > 0 && saltCount > 0 && waterL >= Tune.CanWater - .001f;
                if (Btn(new Rect(lx + cw - 124f, cy + 8f, 120f, CanRow - 16f), Texts.Get("K_DO"), can)) { PutIn(b, pick!); break; }
                cy += CanRow + 3f;
            }
            cy += 6f;
            if (Btn(new Rect(lx, cy, 200f, 32f), Texts.Get("K_PICK_UP"), b.Idle)) { PickUp(b); return; }
            if (Ui.Button(new Rect(lx + cw - 200f, cy, 200f, 32f), Texts.Get("CLOSE"))) Close();
            return;
        }

        // the pack
        Ui.Label(new Rect(lx, cy, 300f, 26f), raws.Count > 0 ? Texts.Get(kt ? "K_PUT_IN" : "PUT_IN") : Ui.Colored(Texts.Get(kt ? "K_NO_RAW" : "NO_RAW"), Ui.Dim));
        if (pages > 1 && Btn(new Rect(lx + cw - 150f, cy, 150f, 24f), Texts.Get("MORE", $"{page + 1}/{pages}"))) page = (page + 1) % pages;
        cy += 28f;
        for (int k = 0; k < Math.Max(1, shown); k++)
        {
            if (k < shown)
            {
                if (page * perPage + k >= raws.Count) break;
                var e = raws[page * perPage + k];
                if (Btn(new Rect(lx, cy, cw, RowH), e.Label)) { PutIn(b, e); break; }
            }
            cy += RowH + 3f;
        }
        if (!kt) salt = Ui.Toggle(new Rect(lx, cy, cw, 28f), salt && saltCount > 0, Texts.Get("SALT", SaltKgText));
        cy += 34f;

        if (Btn(new Rect(lx, cy, 200f, 32f), Texts.Get(kt ? "K_PICK_UP" : "PICK_UP"), b.Idle)) { PickUp(b); return; }
        if (Ui.Button(new Rect(lx + cw - 200f, cy, 200f, 32f), Texts.Get("CLOSE"))) Close();
    }
}
