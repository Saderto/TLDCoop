namespace SmokesByEMPEROR;

/// <summary>How fast the world's clock runs against the real one. 11-15 (the owner: waiting through the wheel, or any action that
/// skips time, left the smoke burning at its own pace): while the game hurries its time (waiting, sleep, harvesting, crafting...)
/// the smoke burns along with it. Measured, not asked of the game: hours played against real seconds in windows of a quarter second
/// (frame by frame the game's hour counter is too coarse), at the usual pace 1 game hour is 300 real seconds.</summary>
internal static class TimeFlow
{
    const float SecondsPerHour = 300f;
    const float Window = .25f;
    const float FastRate = 3f;

    static float lastHours = -1f, winReal, winHours, lastWait = -10f;
    static bool logged;

    /// <summary>The world runs much faster than usual now.</summary>
    public static bool Fast { get; private set; }
    /// <summary>Set for one frame when a window closes: real seconds the world got ahead of the clock in it (0 at the usual pace).</summary>
    public static float ExtraSeconds { get; private set; }
    /// <summary>Game minutes gone by in the present run of fast time.</summary>
    public static float FastMinutes { get; private set; }

    public static void Reset()
    {
        lastHours = -1f; winReal = winHours = 0f; Fast = false; ExtraSeconds = 0f; FastMinutes = 0f; lastWait = -10f;
    }

    public static void Tick()
    {
        ExtraSeconds = 0f;
        if (Fast) _ = Waiting;
        float now = Habit.Now, dt = Time.unscaledDeltaTime;
        // a load, another save: the counter jumps, nothing is measured across it
        if (lastHours < 0f || now < lastHours || now - lastHours > 12f || dt <= 0f || dt > 2f) { lastHours = now; winReal = winHours = 0f; return; }
        winHours += now - lastHours;
        winReal += dt;
        lastHours = now;
        if (winReal < Window) return;
        float worth = winHours * SecondsPerHour;
        bool fast = worth > winReal * FastRate;
        if (fast)
        {
            ExtraSeconds = worth - winReal;
            FastMinutes += winHours * 60f;
            if (!logged) { logged = true; Safe.Debug($"World time runs fast: {worth / winReal:0.#} times the usual pace"); }
        }
        else { FastMinutes = 0f; logged = false; }
        Fast = fast;
        winReal = winHours = 0f;
    }

    /// <summary>Time hurried by waiting or sleeping (not by an action of the hands).</summary>
    public static bool Waiting
    {
        get
        {
            bool w = false;
            try { var p = GameManager.GetPassTime(); if (p != null && p.IsPassingTime()) w = true; } catch { }
            if (!w) try { var r = GameManager.GetRestComponent(); if (r != null && r.IsSleeping()) w = true; } catch { }
            if (!w) try { if (InterfaceManager.IsPanelEnabled<Panel_Rest>()) w = true; } catch { }
            if (w) lastWait = Time.unscaledTime;
            return w;
        }
    }

    /// <summary>Waiting or sleeping now, or a moment ago: the measure of the pace lags a window behind the end of a wait, and the
    /// wait's own panel may still be closing. That tail is not «an action that takes time».</summary>
    public static bool WaitingLately => Waiting || Time.unscaledTime - lastWait < 1.5f;
}
