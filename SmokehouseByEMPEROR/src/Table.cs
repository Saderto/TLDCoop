namespace SmokehouseByEMPEROR;

/// <summary>The sausage table: meat, a gut and salt make a raw sausage of one of the kinds (Defs.Sausages); the barrel then smokes it.
/// Nothing is kept in the table, so there is nothing to share in coop but the table itself, which is an ordinary item.</summary>
public static partial class Window
{
    static float HaveOf(Part p) => Defs.RawsOf(p).Sum(r => meatKg.TryGetValue(r, out float kg) ? kg : 0f);

    static bool CanMake(SausKind k) => k.Parts.All(p => HaveOf(p) >= p.Kg - .001f);

    static Job? JobOf(SausKind k) => Barrels.Jobs.FirstOrDefault(j => j.Kind == k.Id);
    static OnExitDelegate? workDone;      // kept: the game holds only the native side of it
    static GearItem? reopen;
    static int reopenIn;

    /// <summary>«Сделать»: the meat, the gut and the salt go into the sausage at once, then the work takes its time. «Продолжить»: only the rest of the work.</summary>
    static void MakeSausage(SausKind k)
    {
        var job = JobOf(k);
        if (job == null)
        {
            if (gutCount <= 0) { Barrels.Say("T_NEED_GUT"); return; }
            if (saltCount <= 0) { Barrels.Say("MSG_NEED_SALT"); return; }
            var sets = k.Parts.Select(p => (p, raws: (ICollection<string>)Defs.RawsOf(p).ToList())).ToList();
            foreach (var (p, set) in sets)
                if (!Portion(set, p.Kg, false, out _, out _, out _)) { Barrels.Say("T_NEED_MEAT"); return; }
            if (!TakeNamed("GEAR_Gut", "GEAR_GutDried")) { Barrels.Say("T_NEED_GUT"); return; }
            if (!TakeSalt()) { Barrels.Say("MSG_NEED_SALT"); return; }
            float kg = 0f, cal = 0f, cond = 0f;
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
            job = new Job { Kind = k.Id, Kg = kg, Cal = cal, Cond = cond };
            Barrels.Jobs.Add(job);
            Barrels.SaveJobs();
            Safe.Debug($"Sausage {k.Id} begun: {kg:0.00} kg, {cal:0} kcal");
        }
        Work(k, job);
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
                        var got = Items.GiveFood(Defs.Prefix + k.RawId, job.Kg + Tune.SausageSkin, job.Cal, job.Cond);
                        if (got == null) MelonLogger.Error("Smokehouse: the raw sausage could not be created");
                    }
                    else Barrels.Say("T_PAUSED", (int)Math.Round(job.Done * 100f));
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

        foreach (var k in kinds)
        {
            Ui.Row(new Rect(lx, cy, cw, rh));
            Ui.Icon(new Rect(lx + 4f, cy + 3f, rh - 6f, rh - 6f), Assets.Texture("icons/" + k.RawId + ".png", compress: false));
            Ui.Fit(new Rect(lx + rh + 6f, cy - 1f, cw - rh - 176f, 24f), k.Name + ":   " + Recipe(k));
            Ui.Fit(new Rect(lx + rh + 6f, cy + 20f, cw - rh - 176f, 24f), Ui.Colored(Texts.Russian ? k.RuFx : k.EnFx, Ui.Dim));
            var job = JobOf(k);
            bool can = job != null || (CanMake(k) && gutCount > 0 && saltCount > 0);
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
