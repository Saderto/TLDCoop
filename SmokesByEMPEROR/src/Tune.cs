namespace SmokesByEMPEROR;

/// <summary>Live tuning of the pose at the mouth with the numpad, while smoking: Num0 holds the cigarette/cigar at the mouth (again: lets go);
/// Num4/Num6, Num8/Num2, Num9/Num3 move it along the three axes; Num7/Num1, Num+/Num-, Num*/Num/ turn it. Every change is saved to the
/// settings file and shown on screen and in the log, so the found pose becomes the default.</summary>
internal static class Tune
{
    public static bool Frozen;
    const float Step = .01f, TurnStep = 5f;

    /// <summary>With the hands view locked: where the mouth end sits on that view (x, y 0..1 and distance m), per kind, per pipe.</summary>
    public static MelonPreferences_Entry<string> OnScreen(Def d)
    {
        if (d.Smoke != SmokeType.Pipe) return d.Smoke == SmokeType.Cigar ? Settings.CigarInView : Settings.CigInView;
        // the churchwarden's first values were broken (its long stem across the view): new keys, set from the billiard's tuned pose
        string key = d.Id + "InTeethOnScreen" + (d.Id.Contains("Churchwarden") ? "3" : "");
        if (pipeEntries.TryGetValue(key, out var e)) return e;
        var c = MelonPreferences.GetCategory("SmokesBySeagull") ?? MelonPreferences.CreateCategory("SmokesBySeagull");
        e = c.GetEntry<string>(key) ?? c.CreateEntry(key, d.Id.Contains("Corncob") ? "0.50 -0.03 0.27" : d.Id.Contains("Bent") ? "0.49 0.00 0.25" : d.Id.Contains("Churchwarden") ? "0.52 -0.05 0.09" : d.Id.Contains("Billiard") ? "0.50 -0.02 0.25" : Settings.PipeInView.Value, "Эта трубка в зубах: мундштук на экране x y (0..1) и расстояние, м / this pipe in the teeth: mouthpiece on screen x y and distance m");
        pipeEntries[key] = e;
        return e;
    }

    public static MelonPreferences_Entry<string> Offset(Def d) => d.Smoke == SmokeType.Pipe ? PipeEntry(d, true) : d.Smoke == SmokeType.Cigar ? Settings.CigarOffset : Settings.CigOffset;
    public static MelonPreferences_Entry<string> Turn(Def d) => d.Smoke == SmokeType.Pipe ? PipeEntry(d, false) : d.Smoke == SmokeType.Cigar ? Settings.CigarTurn : Settings.CigTurn;

    // every pipe has its own place in the mouth (their shapes differ): entries "<LitPipeX>MouthOffset"/"…MouthTurn", made on first use
    static readonly Dictionary<string, MelonPreferences_Entry<string>> pipeEntries = new();

    static MelonPreferences_Entry<string> PipeEntry(Def d, bool offset)
    {
        string key = d.Id + (offset ? "MouthOffset" : "MouthTurn") + (d.Id.Contains("Churchwarden") ? "3" : "");
        if (pipeEntries.TryGetValue(key, out var e)) return e;
        var c = MelonPreferences.GetCategory("SmokesBySeagull") ?? MelonPreferences.CreateCategory("SmokesBySeagull");
        string def = Settings.PipeDefault(d.Id, offset) ?? (offset ? Settings.PipeOffset.Value : Settings.PipeTurnAt.Value);
        if (d.Id.Contains("Churchwarden") && !offset) def = "35 -125 -60";   // the user's own tuning 11-13 (keys ...3)
        if (d.Id.Contains("Churchwarden") && offset) def = "-0.04 0.37 -0.43";
        e = c.GetEntry<string>(key) ?? c.CreateEntry(key, def, (offset ? "Эта трубка во рту: сдвиг, м / this pipe in the mouth: offset, m" : "Эта трубка во рту: поворот, градусы / this pipe in the mouth: turn, degrees"));
        pipeEntries[key] = e;
        return e;
    }

    public static void Keys(Def lit)
    {
        if (!Settings.TuneKeys.Value) return;
        try
        {
            Flush();
            if (!announced) { announced = true; Safe.Info("Tune keys active"); }
            if ((K(KeyCode.Keypad0) || K(KeyCode.End)) && lit.Smoke != SmokeType.Pipe)
            {
                Frozen = !Frozen;
                Show(lit, Frozen ? "SMOKES_TUNE_HOLD" : "SMOKES_TUNE_FREE");
            }
            if (K(KeyCode.L) && lit.Smoke == SmokeType.Cigarette)
            {
                Settings.CigNameRoll.Value = (Settings.CigNameRoll.Value + 30f) % 360f;
                MelonPreferences.Save();
                Safe.Info("Tune cigarette name roll " + Settings.CigNameRoll.Value);
                pending = "ROLL " + Settings.CigNameRoll.Value.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            }
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift && (K(KeyCode.Equals) || K(KeyCode.Minus)))
            {
                Settings.ViewPivot.Value = Mathf.Round((Settings.ViewPivot.Value + (K(KeyCode.Equals) ? .02f : -.02f)) * 50f) / 50f;
                MelonPreferences.Save();
                Safe.Info("Tune pivot " + Settings.ViewPivot.Value);
                pending = Texts.Get("SMOKES_TUNE_PIVOT") + " " + Settings.ViewPivot.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                Flush();
            }
            else if (K(KeyCode.Equals) || K(KeyCode.Minus))
            {
                Settings.FollowAmount.Value = Mathf.Round((Settings.FollowAmount.Value + (K(KeyCode.Equals) ? .25f : -.25f)) * 4f) / 4f;
                MelonPreferences.Save();
                Safe.Info("Tune follow " + Settings.FollowAmount.Value);
                pending = Texts.Get("SMOKES_TUNE_FOLLOW") + " " + Settings.FollowAmount.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                Flush();
            }
            var d = Vector3.zero;
            var r = Vector3.zero;
            if (K(KeyCode.Keypad4) || K(KeyCode.LeftArrow)) d.x -= Step;
            if (K(KeyCode.Keypad6) || K(KeyCode.RightArrow)) d.x += Step;
            // the hands are seen from the eye bone looking along its +Y with +Z up: arrows up/down = Z (up/down on screen),
            // PgUp/PgDn = Y (away from / towards the eye)
            if (K(KeyCode.Keypad8) || K(KeyCode.UpArrow)) d.z += Step;
            if (K(KeyCode.Keypad2) || K(KeyCode.DownArrow)) d.z -= Step;
            if (K(KeyCode.Keypad9) || K(KeyCode.PageUp)) d.y += Step;
            if (K(KeyCode.Keypad3) || K(KeyCode.PageDown)) d.y -= Step;
            if (K(KeyCode.Keypad7) || K(KeyCode.LeftBracket)) r.x += TurnStep;
            if (K(KeyCode.Keypad1) || K(KeyCode.RightBracket)) r.x -= TurnStep;
            if (K(KeyCode.KeypadPlus) || K(KeyCode.Semicolon)) r.y += TurnStep;
            if (K(KeyCode.KeypadMinus) || K(KeyCode.Quote)) r.y -= TurnStep;
            if (K(KeyCode.KeypadMultiply) || K(KeyCode.Comma)) r.z += TurnStep;
            if (K(KeyCode.KeypadDivide) || K(KeyCode.Period)) r.z -= TurnStep;
            if (d == Vector3.zero && r == Vector3.zero) return;
            if (ViewLock.Locked)
            {
                // on the locked view: left/right and up/down move it on the screen, PgUp/PgDn farther/nearer
                var sc = OnScreen(lit);
                sc.Value = Fmt(Settings.Vec(sc) + new Vector3(d.x, d.z, d.y), "0.00");
                var tt = Turn(lit);
                tt.Value = Fmt(Settings.Vec(tt) + r, "0");
                MelonPreferences.Save();
                Show(lit, null);
                return;
            }
            var o = Offset(lit);
            var t = Turn(lit);
            o.Value = Fmt(Settings.Vec(o) + d, "0.00");
            t.Value = Fmt(Settings.Vec(t) + r, "0");
            MelonPreferences.Save();
            Show(lit, null);
        }
        catch (Exception e) { Safe.Log("Tune keys", e); }
    }

    static bool announced;
    static bool K(KeyCode k) => Input.GetKeyDown(k);

    static string Fmt(Vector3 v, string f) => string.Join(" ", new[] { v.x, v.y, v.z }.Select(x => x.ToString(f, System.Globalization.CultureInfo.InvariantCulture)));

    static void Show(Def lit, string? key)
    {
        string line = $"{(lit.Smoke == SmokeType.Pipe ? lit.Id : lit.Smoke.ToString())}: {(ViewLock.Locked ? OnScreen(lit).Value : Offset(lit).Value)} | {Turn(lit).Value}";
        Safe.Info("Tune " + line + (key != null ? " " + key : ""));
        pending = (key != null ? Texts.Get(key) + " " : "") + line;
        Flush();
    }

    // the game queues its screen messages one after another: a new one only when the last has gone, each shown briefly, so the line
    // on screen is always the latest values and never a long tail of old ones
    static string? pending;
    static float shownUntil;
    const float ShowFor = .6f;

    static void Flush()
    {
        if (pending == null || Time.unscaledTime < shownUntil) return;
        try { HUDMessage.AddMessage(pending, ShowFor, true, true); } catch { }
        shownUntil = Time.unscaledTime + ShowFor + .15f;
        pending = null;
    }
}
