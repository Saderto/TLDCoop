namespace JellyfishByEMPEROR;

/// <summary>A live jellyfish's condition is the jar's own condition (saved by the game); one in an aquarium has it in the aquarium's state.
/// The owner (11-12): where its water is below −5° it dies, from full condition in 90 minutes; in the warmth it comes back slowly.
/// In the pack the body keeps it some degrees warmer. Dead, a standing jar shows it lying on the bottom; in the pack (or picked up) only the
/// empty jar is left. A dead one in an aquarium lies on the sand until it is removed in the aquarium's window.</summary>
public static class Life
{
    static float lastHours = -1f, next;

    static float Hours()
    {
        try { return GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused(); }
        catch { return -1f; }
    }

    public static float TempAt(Vector3 pos)
    {
        float t = GameManager.GetWeatherComponent().GetCurrentTemperatureWithoutHeatSources();
        try { t += GameManager.GetHeatSourceManagerComponent().GetTemperatureIncrease(pos); } catch { }
        return t;
    }

    public static float TempAt(GearItem g) =>
        g.m_InPlayerInventory ? GameManager.GetWeatherComponent().GetCurrentTemperature() + Tune.PackWarmth : TempAt(g.transform.position);

    /// <summary>Condition in % after dh hours at temperature t.</summary>
    static float Step(float pct, float t, float dh)
    {
        if (t < Tune.FreezeAt) pct -= Tune.LossPerHour * dh;
        else if (t > 2f) pct += Tune.GainPerHour * dh;
        return Mathf.Clamp(pct, 0f, 100f);
    }

    public static void Tick()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + 1f;
        float h = Hours();
        if (h < 0f || GameManager.GetPlayerManagerComponent() == null) return;
        if (lastHours < 0f || h < lastHours || h - lastHours > 200f) { lastHours = h; return; }
        float dh = h - lastHours;
        lastHours = h;
        if (dh <= 0f) return;
        foreach (var l in Visual.Lives.ToArray())
        {
            var g = l.G;
            if (g == null) continue;
            try
            {
                float max = Items.MaxHP(g);
                if (g.CurrentHP <= 0.001f)
                {
                    if (g.m_InPlayerInventory) Die(g);
                    continue;
                }
                float t = TempAt(g);
                float pct = Step(g.CurrentHP / max * 100f, t, dh);
                g.CurrentHP = pct / 100f * max;
                if (pct <= 0.001f)
                {
                    g.CurrentHP = 0f;
                    Catch.Say("MSG_DIED", Texts.Name(l.Def.Species!, l.Def.Colour!));
                    Safe.Info($"{l.Def.Id} died at {t:0.0} C");
                    if (g.m_InPlayerInventory) Die(g);
                }
            }
            catch (Exception e) { Safe.Log("Life", e); }
        }
        // the aquariums of this place (one somewhere else is not there to be measured: it waits, as the place itself does)
        try
        {
            string scene = Tanks.SceneName;
            foreach (var t in Tanks.State.Tanks.ToArray())
            {
                if (t.Scene != scene) continue;
                float temp = TempAt(t.Pos);
                bool died = false;
                for (int i = t.Fish.Count - 1; i >= 0; i--)
                {
                    var f = t.Fish[i];
                    if (!f.Dead) f.Hp = Step(f.Hp, temp, dh);
                    if (!f.Dead) continue;
                    // the owner (11-12): a dead one simply disappears, with a word that it died
                    t.Fish.RemoveAt(i);
                    died = true;
                    Safe.Info($"{f.Sp} died in the aquarium at {temp:0.0} C");
                    if (f.Species is { } s && f.Colour is { } c) Catch.Say("MSG_DIED_TANK", Texts.Name(s, c));
                }
                if (died) Tanks.Changed(t);
            }
        }
        catch (Exception e) { Safe.Log("Aquarium life", e); }
    }

    /// <summary>In the pack (or picked up dead): only the empty jar is left, quietly (the death was told when it happened).</summary>
    static void Die(GearItem g)
    {
        Visual.Lives.RemoveAll(x => x.G == null || x.G.Pointer == g.Pointer);
        Pack.Remove(g);
        Pack.GiveJar();
    }

    /// <summary>The hover text of a standing jar or aquarium: its name, how it is, and a warning while its water freezes.</summary>
    public static string Hover(GearItem g, string text)
    {
        if (!Defs.Is(g, out var def)) return text;
        if (def.Kind == Kind.Aquarium)
        {
            int n = Tanks.CountOf(g);
            string a = text + "\n" + Texts.Get("HOVER_TANK", n, Tune.TankMax);
            try { if (n > 0 && TempAt(g) < Tune.FreezeAt) a += "  " + Texts.Get("HOVER_COLD"); } catch { }
            return a;
        }
        if (def.Kind != Kind.Live) return text;
        float max = Items.MaxHP(g);
        int pct = (int)Math.Round(g.CurrentHP / max * 100f);
        if (pct <= 0) return Texts.Get("Jar_NAME");
        string s = text + "\n" + Texts.Get("HOVER_COND", pct);
        try { if (TempAt(g) < Tune.FreezeAt) s += "  " + Texts.Get("HOVER_COLD"); } catch { }
        return s;
    }
}

[HarmonyPatch]
internal static class HoverPatch
{
    [HarmonyPostfix, HarmonyPatch(typeof(GearItem), nameof(GearItem.GetHoverText))]
    static void Hover(GearItem __instance, ref string __result)
    {
        try
        {
            if (__instance == null || __instance.m_InPlayerInventory) return;
            __result = Life.Hover(__instance, __result);
        }
        catch (Exception e) { Safe.Log("Hover", e); }
    }
}
