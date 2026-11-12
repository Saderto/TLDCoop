namespace SmokesByEMPEROR;

/// <summary>11-10: collectible lighters. Each is its own item with its charges kept as condition; an empty one stays and is refilled from
/// lighter fluid (petrol kinds) or a butane can (gas kinds). The collection is per world, in the smoking state file (Habit), and only
/// grows: the first time a design comes into the pack, «New lighter in the collection: N of 29».</summary>
public static partial class Lighters
{
    public const int FluidUnits = 600, ButaneUnits = 900;

    public static int Total => All.Length;

    public static bool QuietGive;   // the give-all key: one line at the end, not 29

    public static int Found => Habit.State.Lighters.Count(id => Array.Exists(All, d => d.Id == id));

    public static bool Has(string id) => Habit.State.Lighters.Contains(id);

    /// <summary>Called when one of ours comes into the pack.</summary>
    public static void Collected(Def def, bool quiet)
    {
        if (def.Kind != Kind.Lighter || Has(def.Id)) return;
        Habit.State.Lighters.Add(def.Id);
        Safe.Info($"Lighters: {def.Id} collected ({Found} of {Total}){(quiet ? " quietly" : "")}");
        if (!quiet) Use.Say("SMOKES_MSG_NEW_LIGHTER", Found, Total);
    }

    /// <summary>Lighters already in the pack count, quietly (a save from before the collection, or a pack restored before the state file).</summary>
    public static void SyncFromPack()
    {
        try
        {
            var inv = GameManager.GetInventoryComponent();
            if (inv == null) return;
            foreach (var o in inv.m_Items)
                if (o?.m_GearItem is { } gi && Defs.Is(gi, out var d) && d.Kind == Kind.Lighter) Collected(d, true);
        }
        catch (Exception e) { Safe.Log("Lighters from the pack", e); }
    }

    public static System.Collections.IEnumerator SyncLater()
    {
        float until = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < until) yield return null;
        SyncFromPack();
    }

    /// <summary>«Refill» on the fluid tin or the butane can: the emptiest lighter of that fuel in the pack is topped up.</summary>
    public static bool Refill(GearItem can, Def canDef)
    {
        var inv = GameManager.GetInventoryComponent();
        if (inv == null) return false;
        GearItem? best = null;
        Def? bestDef = null;
        float bestFill = 2f;
        bool any = false;
        foreach (var o in inv.m_Items)
        {
            var gi = o?.m_GearItem;
            if (gi == null || !Defs.Is(gi, out var d) || d.Kind != Kind.Lighter || d.Fuel != canDef.Fuel) continue;
            any = true;
            float fill = Items.Units(gi) / (float)d.Units;
            if (fill < bestFill && Items.Units(gi) < d.Units) { bestFill = fill; best = gi; bestDef = d; }
        }
        if (!any) { Use.Error("SMOKES_MSG_NO_LIGHTER_FUEL"); return false; }
        if (best == null || bestDef == null) { Use.Error("SMOKES_MSG_LIGHTERS_FULL"); return false; }
        int need = bestDef.Units - Items.Units(best);
        int have = Items.Units(can);
        int give = Math.Min(need, have);
        if (give <= 0) return false;
        SetUnits(best, bestDef, Items.Units(best) + give);
        if (give >= have) inv.DestroyGear(can.gameObject);
        else SetUnits(can, canDef, have - give);
        Use.Say("SMOKES_MSG_REFILLED", Texts.Get("SMOKES_" + bestDef.Id + "_NAME"), give);
        Safe.Info($"Lighters: {bestDef.Id} refilled +{give} from {canDef.Id} ({have - give} left)");
        try { GameAudioManager.PlaySound("Play_SndInvLiquidPour", GameManager.GetPlayerObject()); } catch { }
        return true;
    }

    public static void SetUnits(GearItem g, Def d, int units)
    {
        float max = Items.MaxHP(g);
        g.CurrentHP = units <= 0 ? 0f : Math.Min(max, max * units / d.Units + .001f);
    }

    /// <summary>A lighter lying in the world or a container is never full: 15–80% of its charges.</summary>
    public static void RandomFill(GearItem g, Def d)
    {
        if (d.Kind != Kind.Lighter && d.Kind != Kind.Refill) return;
        float f = d.Kind == Kind.Lighter ? UnityEngine.Random.Range(.15f, .8f) : UnityEngine.Random.Range(.3f, 1f);
        SetUnits(g, d, Math.Max(1, (int)(d.Units * f)));
    }
}

/// <summary>A lighter of ours coming into the pack counts for the collection. Quiet while a save loads (the pack fills from the save).</summary>
[HarmonyPatch(typeof(Inventory), nameof(Inventory.AddGear))]
internal static class CollectPatch
{
    static void Postfix(GearItem gi)
    {
        try
        {
            if (!Defs.Is(gi, out var def) || def.Kind != Kind.Lighter) return;
            bool quiet = Lighters.QuietGive || GameManager.IsMainMenuActive() || SaveGameSystem.IsRestoreInProgress() || Time.timeSinceLevelLoad < 8f;
            Lighters.Collected(def, quiet);
        }
        catch (Exception e) { Safe.Log("Lighter collection", e); }
    }
}
