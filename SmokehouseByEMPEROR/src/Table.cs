namespace SmokehouseByEMPEROR;

/// <summary>The sausage table: meat, a gut and salt make a raw sausage of one of the kinds (Defs.Sausages); the barrel then smokes it.
/// Nothing is kept in the table, so there is nothing to share in coop but the table itself, which is an ordinary item.</summary>
public static partial class Window
{
    static float HaveOf(Part p) => Defs.RawsOf(p).Sum(r => meatKg.TryGetValue(r, out float kg) ? kg : 0f);

    static bool CanMake(SausKind k) => k.Parts.All(p => HaveOf(p) >= p.Kg - .001f);

    static Job? JobOf(SausKind k) => Barrels.Jobs.FirstOrDefault(j => j.Kind == k.Id);

    /// <summary>Is this kind's unfinished sausage in the pack.</summary>
    static bool HasWip(SausKind k)
    {
        var inv = GameManager.GetInventoryComponent();
        if (inv == null) return false;
        string name = Defs.Prefix + k.WipId;
        foreach (var o in inv.m_Items)
        {
            var g = o?.m_GearItem;
            if (g != null && Defs.Clean(g.name) == name) return true;
        }
        return false;
    }

    /// <summary>The work that can go on: a record from before 0.4.19 (no item yet), or one whose unfinished sausage is at hand.
    /// A record whose item is not in the pack is not offered: the sausage was left somewhere, a new one is made from new meat.</summary>
    static Job? Going(SausKind k)
    {
        var job = JobOf(k);
        return job != null && (!job.Item || HasWip(k)) ? job : null;
    }

    static readonly Dictionary<string, (Job? job, float until)> goingCache = new();
    static Job? GoingCached(SausKind k)
    {
        float now = Time.realtimeSinceStartup;
        if (goingCache.TryGetValue(k.Id, out var c) && c.until > now) return c.job;
        var j = Going(k);
        goingCache[k.Id] = (j, now + .5f);
        return j;
    }
    static OnExitDelegate? workDone;      // kept: the game holds only the native side of it
    static GearItem? reopen;
    static int reopenIn;

    /// <summary>«Сделать» (11-12 owner, 0.4.20): the meat, the gut and the salt are only checked here and go into the sausage when the work is done to the end;
    /// work broken off costs nothing. «Продолжить» is left for the sausages begun before 0.4.20, whose meat was taken at the start.</summary>
    static void MakeSausage(SausKind k)
    {
        goingCache.Clear();
        var job = Going(k);
        if (job != null) { Work(k, job); return; }
        if (CookingLevel() < k.Level) { Barrels.Say("T_NEED_LEVEL", k.Level); return; }
        if (gutCount <= 0) { Barrels.Say("T_NEED_GUT"); return; }
        if (saltCount <= 0) { Barrels.Say("MSG_NEED_SALT"); return; }
        foreach (var p in k.Parts)
            if (!Portion(Defs.RawsOf(p).ToList(), p.Kg, false, out _, out _, out _)) { Barrels.Say("T_NEED_MEAT"); return; }
        Fresh(k);
    }

    /// <summary>Everything a sausage is made of leaves the pack; what the sausage will be.</summary>
    static bool TakeParts(SausKind k, out float kg, out float cal, out float cond)
    {
        kg = cal = cond = 0f;
        var sets = k.Parts.Select(p => (p, raws: (ICollection<string>)Defs.RawsOf(p).ToList())).ToList();
        foreach (var (p, set) in sets)
            if (!Portion(set, p.Kg, false, out _, out _, out _)) { Barrels.Say("T_NEED_MEAT"); return false; }
        if (!TakeNamed("GEAR_Gut", "GEAR_GutDried")) { Barrels.Say("T_NEED_GUT"); return false; }
        if (!TakeSalt()) { Barrels.Say("MSG_NEED_SALT"); return false; }
        foreach (var (p, set) in sets)
        {
            Portion(set, p.Kg, true, out float c, out float cn, out float ratio);
            // as nourishing as the same meat smoked in pieces: the sausage is not fried, so the cooked value is set here
            cal += c * Mathf.Clamp(ratio, .5f, 2f);
            cond += cn * p.Kg;
            kg += p.Kg;
        }
        cond = kg > 0f ? cond / kg : 1f;
        if (kg > Tune.SausageMeatKg + .001f)
        {
            // «Рыбная» takes half a kilo of every fish, yet comes out a sausage of the usual size: the rest is the recipe's price (11-12 owner)
            cal *= Tune.SausageMeatKg / kg; kg = Tune.SausageMeatKg;
        }
        return true;
    }

    static void Finish(SausKind k)
    {
        if (!TakeParts(k, out float kg, out float cal, out float cond)) return;
        var got = Items.GiveFood(Defs.Prefix + k.RawId, kg + Tune.SausageSkin, cal, cond);
        if (got == null) MelonLogger.Error("Smokehouse: the raw sausage could not be created");
        Safe.Debug($"Sausage {k.Id} made: {kg:0.00} kg, {cal:0} kcal");
    }

    /// <summary>A new sausage: the whole work in one go, nothing is spent until it is over.</summary>
    static void Fresh(SausKind k)
    {
        var table = item;
        try
        {
            var panel = InterfaceManager.GetPanel<Panel_GenericProgressBar>();
            workDone = (OnExitDelegate)new Action<bool, bool, float>((success, cancelled, progress) =>
            {
                try
                {
                    if (success) Finish(k);
                    else Barrels.Say("T_BROKEN");
                    Safe.Debug($"Sausage {k.Id}: work over, success {success}, cancelled {cancelled}, progress {progress:0.00}");
                }
                catch (Exception e) { Safe.Log("Sausage work end", e); }
                reopen = table; reopenIn = 3;
            });
            Close();
            Ui.BlockGameInput(false);
            panel.Launch(Texts.Get("T_WORK", k.Name), Math.Max(.6f, Tune.SausageSeconds), Tune.SausageMinutes, 0f, true, workDone);
        }
        catch (Exception e)
        {
            Safe.Log("Sausage work", e);
            // no progress bar in this build: the sausage is simply made
            Finish(k);
        }
    }

    /// <summary>The game's own progress bar: the player stands and works, the clock runs, the work can be broken off.</summary>
    static void Work(SausKind k, Job job)
    {
        var table = item;
        float left = Mathf.Clamp01(1f - job.Done);
        try
        {
            var panel = InterfaceManager.GetPanel<Panel_GenericProgressBar>();
            workDone = (OnExitDelegate)new Action<bool, bool, float>((success, cancelled, progress) =>
            {
                try
                {
                    job.Done = success ? 1f : Mathf.Clamp01(job.Done + left * Mathf.Clamp01(progress));
                    if (job.Done >= .999f)
                    {
                        Barrels.Jobs.Remove(job);
                        TakeNamed(Defs.Prefix + k.WipId);
                        var got = Items.GiveFood(Defs.Prefix + k.RawId, job.Kg + Tune.SausageSkin, job.Cal, job.Cond);
                        if (got == null) MelonLogger.Error("Smokehouse: the raw sausage could not be created");
                    }
                    else
                    {
                        // 11-12 owner: as with a station left unfinished, the sausage goes to the pack under another name
                        if (!HasWip(k))
                        {
                            var w = GameManager.GetPlayerManagerComponent()?.InstantiateItemInPlayerInventory(Defs.Prefix + k.WipId, 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
                            if (w == null) MelonLogger.Error("Smokehouse: the unfinished sausage could not be created");
                            else job.Item = true;
                        }
                        else job.Item = true;
                        Barrels.Say("T_PAUSED", (int)Math.Round(job.Done * 100f));
                    }
                    goingCache.Clear();
                    Barrels.SaveJobs();
                    Safe.Debug($"Sausage {k.Id}: work over, done {job.Done:0.00}, success {success}, cancelled {cancelled}, progress {progress:0.00}");
                }
                catch (Exception e) { Safe.Log("Sausage work end", e); }
                reopen = table; reopenIn = 3;
            });
            Close();
            Ui.BlockGameInput(false);
            panel.Launch(Texts.Get("T_WORK", k.Name), Math.Max(.6f, Tune.SausageSeconds * left), Tune.SausageMinutes * left, 0f, true, workDone);
        }
        catch (Exception e)
        {
            Safe.Log("Sausage work", e);
            // no progress bar in this build: the sausage is not lost, it is simply made
            job.Done = 1f; Barrels.Jobs.Remove(job); Barrels.SaveJobs();
            TakeNamed(Defs.Prefix + k.WipId);
            Items.GiveFood(Defs.Prefix + k.RawId, job.Kg + Tune.SausageSkin, job.Cal, job.Cond);
        }
    }

    /// <summary>The table's window comes back when the work is over or broken off.</summary>
    static void TableTick()
    {
        if (reopen == null || --reopenIn > 0) return;
        var g = reopen; reopen = null;
        try { if (g != null && g.gameObject != null && g.gameObject.activeInHierarchy && !g.m_InPlayerInventory && !IsOpen) Open(g); } catch { }
    }

    static string Recipe(SausKind k)
    {
        string Have(bool ok, string t) => ok ? t : Ui.Colored(t, Ui.Fire);
        if (k.Parts.Length > 4) return Have(CanMake(k), Texts.Get("T_ALLFISH", k.Parts[0].Kg.ToString("0.00")));
        return string.Join(" + ", k.Parts.Select(p => Have(HaveOf(p) >= p.Kg - .001f,
            (Texts.Russian ? Defs.GroupNames[p.Group].Ru : Defs.GroupNames[p.Group].En) + " " + Texts.Get("KG", p.Kg.ToString("0.00")))));
    }

    static void DrawTable()
    {
        Ui.Begin();
        const int perPage = 8;
        int pages = (Defs.Sausages.Length + perPage - 1) / perPage;
        if (page >= pages) page = 0;
        var kinds = Defs.Sausages.Skip(page * perPage).Take(perPage).ToArray();
        const float rh = 46f; float tw = Math.Min(1080f, Ui.SW - 40f);
        float h = 52f + 30f + 30f + perPage * (rh + 3f) + 10f + 30f + 44f + 10f;
        float x = (Ui.SW - tw) / 2f, y = Math.Max(14f, (Ui.SH - h) / 2f);
        Ui.Panel(new Rect(x, y, tw, h), Texts.Get("T_TITLE"));
        float lx = x + Pad, cw = tw - Pad * 2f, cy = y + 52f;

        string Have(bool ok, string t) => ok ? t : Ui.Colored(t, Ui.Fire);
        Ui.Label(new Rect(lx, cy, cw, 26f), Texts.Get("T_NEED") + "  " + Have(gutCount > 0, Texts.Get("T_GUTS", gutCount)) + ",  " + Have(saltCount > 0, Texts.Get("K_SALT", SaltKgText)));
        cy += 30f;
        Ui.Label(new Rect(lx, cy, cw, 26f), Ui.Colored(Texts.Get("T_NEXT"), Ui.Dim));
        cy += 30f;

        int level = CookingLevel();
        foreach (var k in kinds)
        {
            Ui.Row(new Rect(lx, cy, cw, rh));
            Ui.Icon(new Rect(lx + 4f, cy + 3f, rh - 6f, rh - 6f), Assets.Texture("icons/" + k.RawId + ".png", compress: false));
            Ui.Fit(new Rect(lx + rh + 6f, cy - 1f, cw - rh - 176f, 24f), k.Name + ":   " + Recipe(k));
            bool skilled = level >= k.Level;
            Ui.Fit(new Rect(lx + rh + 6f, cy + 20f, cw - rh - 176f, 24f), Ui.Colored(Texts.Get("T_LEVEL", k.Level), skilled ? Ui.Dim : Ui.Fire) + Ui.Colored(".  " + (Texts.Russian ? k.RuFx : k.EnFx), Ui.Dim));
            var job = GoingCached(k);
            bool can = job != null || (skilled && CanMake(k) && gutCount > 0 && saltCount > 0);
            if (Ui.Button(new Rect(lx + cw - 154f, cy + 8f, 150f, rh - 16f), job != null ? Texts.Get("T_CONTINUE", (int)Math.Round(job.Done * 100f)) : Texts.Get("T_DO"), can)) { MakeSausage(k); return; }
            cy += rh + 3f;
        }
        cy += (perPage - kinds.Length) * (rh + 3f) + 10f;

        if (Ui.Button(new Rect(lx, cy, 200f, 32f), Texts.Get("T_PICK_UP")))
        {
            var g = item;
            Close();
            Lift(g);
            return;
        }
        if (Ui.Button(new Rect(lx + (cw - 200f) / 2f, cy, 200f, 32f), Texts.Get("MORE", $"{page + 1}/{pages}"))) page = (page + 1) % pages;
        if (Ui.Button(new Rect(lx + cw - 200f, cy, 200f, 32f), Texts.Get("CLOSE"))) Close();
    }
}
