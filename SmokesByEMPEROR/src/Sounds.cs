namespace SmokesByEMPEROR;

/// <summary>Our own sounds, made in code (no recordings): the cigar cutter's snip, the drag (paper and tobacco crackling, a soft draw)
/// and the exhale (a long breath of air). Played through a Unity AudioSource next to the player; volume by the SoundVolume setting.</summary>
public static class Sounds
{
    static AudioSource? src;
    static AudioClip? snip, drag, dragPipe, exhale;
    static bool logged;

    const int Rate = 44100;

    static AudioClip Make(string name, float seconds, Func<float, float, System.Random, float> f, int seed)
    {
        int n = (int)(seconds * Rate);
        var data = new float[n];
        var rnd = new System.Random(seed);
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, seconds, rnd), -1f, 1f);
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>(data), 0);
        clip.hideFlags = HideFlags.HideAndDontSave;
        return clip;
    }

    // one-pole low/high pass state per clip
    static float lp, hp, hpIn;

    static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

    static void Build()
    {
        if (snip != null) return;
        // snip: two sharp metallic clicks (blades meeting) and a short dry tear of leaf
        lp = 0f;
        snip = Make("smokes_snip", .22f, (t, len, r) =>
        {
            float v = 0f;
            foreach (var at in new[] { .01f, .045f })
            {
                float d = t - at;
                if (d >= 0f && d < .03f) v += Mathf.Exp(-d * 260f) * (Mathf.Sin(2f * Mathf.PI * 3900f * d) * .6f + Noise(r) * .5f);
            }
            float tear = t - .05f;
            if (tear > 0f) v += Noise(r) * .25f * Mathf.Exp(-tear * 30f);
            return v * .9f;
        }, 11);
        // drag: soft draw of air (low-passed noise swelling) with sparse crackles of burning paper/tobacco
        lp = 0f;
        drag = Make("smokes_drag", 1.1f, (t, len, r) =>
        {
            float env = Mathf.SmoothStep(0f, 1f, t / .25f) * Mathf.SmoothStep(0f, 1f, (len - t) / .3f);
            lp += (Noise(r) - lp) * .06f;
            float air = lp * 1.6f * env;
            float crack = r.NextDouble() < .0016 ? Noise(r) * .9f : 0f;
            return air * .55f + crack * env;
        }, 23);
        // pipe: the same draw, deeper and with fewer crackles, little lip pops
        lp = 0f;
        dragPipe = Make("smokes_drag_pipe", 1.1f, (t, len, r) =>
        {
            float env = Mathf.SmoothStep(0f, 1f, t / .2f) * Mathf.SmoothStep(0f, 1f, (len - t) / .3f);
            lp += (Noise(r) - lp) * .035f;
            float pop = 0f;
            foreach (var at in new[] { .15f, .5f, .85f })
            {
                float d = t - at;
                if (d >= 0f && d < .04f) pop += Mathf.Exp(-d * 120f) * Mathf.Sin(2f * Mathf.PI * 180f * d) * .5f;
            }
            float crack = r.NextDouble() < .0005 ? Noise(r) * .6f : 0f;
            return lp * 2f * env * .5f + pop + crack * env;
        }, 37);
        // exhale: a long breath: band of noise, quick rise, slow fall
        lp = 0f; hp = 0f; hpIn = 0f;
        exhale = Make("smokes_exhale", 1.7f, (t, len, r) =>
        {
            float env = Mathf.SmoothStep(0f, 1f, t / .12f) * Mathf.Pow(Mathf.Clamp01(1f - t / len), 1.6f);
            float x = Noise(r);
            lp += (x - lp) * .18f;
            hp = .97f * (hp + lp - hpIn);
            hpIn = lp;
            return hp * 1.4f * env * .6f;
        }, 41);
    }

    static AudioSource? Source()
    {
        if (src != null && src) return src;
        var go = new GameObject("SmokesSounds");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        // the game plays its sound through Wwise; Unity audio needs a listener somewhere
        var listener = UnityEngine.Object.FindObjectOfType<AudioListener>();
        if (listener == null) go.AddComponent<AudioListener>();
        if (!logged)
        {
            logged = true;
            Safe.Info($"Sounds: Unity audio listener {(listener != null ? "found on " + listener.name : "added")}, output rate {AudioSettings.outputSampleRate}");
        }
        return src;
    }

    static void Play(Func<AudioClip?> clip, float vol)
    {
        if (!Settings.Sounds.Value) return;
        try
        {
            Build();
            var s = Source();
            var c = clip();
            if (s == null || c == null) return;
            s.PlayOneShot(c, vol * Settings.SoundVolume.Value);
        }
        catch (Exception e)
        {
            Safe.Log("Sound", e);
        }
    }

    // TLD switches Unity audio off (output rate 0), so the game's own Wwise events are used; Unity clips only if Unity audio runs
    static bool Wwise(string ev)
    {
        if (!Settings.Sounds.Value) return true;
        try
        {
            var who = GameManager.GetPlayerObject();
            if (who == null) return false;
            uint id = GameAudioManager.PlaySound(ev, who);
            if (!wwiseLogged.Contains(ev)) { wwiseLogged.Add(ev); Safe.Info($"Sound {ev}: playing id {id}"); }
            return id != 0;
        }
        catch (Exception e) { Safe.Log("Wwise sound " + ev, e); return false; }
    }
    static readonly HashSet<string> wwiseLogged = new();

    // the cut: the game's own sounds named in the settings (several play together); Ctrl+Shift+F10 plays the candidates one by one
    public static void Snip(bool knife = false)
    {
        if (Sfx.Play("cigar_cut", 1f, knife ? .8f : 1f)) return;
        bool any = false;
        foreach (var ev in (knife ? Settings.KnifeCutSounds.Value : Settings.CutterSounds.Value).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            any |= !Synth.Is(ev) && Wwise(ev);
        if (!any) Play(() => snip, 1f);
    }

    static int candidate = -1;

    /// <summary>Test key: our cut sounds one by one, the name on screen and in the log.</summary>
    public static void NextCandidate()
    {
        candidate = (candidate + 1) % Synth.Names.Length;
        var name = Synth.Names[candidate];
        bool ok = Synth.Play(name, Settings.SoundVolume.Value);
        string msg = $"{candidate + 1}/{Synth.Names.Length}: {name}";
        Safe.Info($"Cut sound candidate {msg} ({(ok ? "played" : "failed")})");
        try { HUDMessage.AddMessage(msg, 2.5f, true, true); } catch { }
    }
    /// <summary>A drag: the ember crackles (cigarette / cigar and pipe) and a breath in. Recorded sounds when Unity audio runs, else the game's own.</summary>
    public static void Drag(SmokeType t)
    {
        bool ember = Sfx.PlayEmber(t == SmokeType.Cigarette ? "cig_drag" : "cigar_drag", t == SmokeType.Cigarette ? .8f : .9f);
        bool breath = Sfx.Play(Voiced("inhale", Female()), .55f);
        if (!ember && !breath && !Wwise("PLAY_RESPIRATORCANISTER_TIMELINE_REPLACE_VOINHALE")) Play(() => t == SmokeType.Pipe ? dragPipe : drag, .7f);
    }
    public static void Exhale() { Sfx.StopEmber(); if (!Sfx.Play(Voiced("exhale", Female()), .7f) && !Wwise("PLAY_RESPIRATORCANISTER_TIMELINE_REPLACE_VOEXHALE")) Play(() => exhale, .6f); }
    public static void PipeFill() => Sfx.Play("pipe_fill", .9f);
    /// <summary>Another player's drag and breath out, from their figure.</summary>
    public static void RemoteDrag(Vector3 at, bool pipe) { Sfx.PlayAt(pipe ? "cigar_drag" : "cig_drag", at, .8f); }
    public static void RemoteExhale(Vector3 at, bool female) { Sfx.PlayAt(Voiced("exhale", female), at, .7f); }

    /// <summary>The player's character: the game's voice (Mackenzie / Astrid, or what the coop mod set for the chosen figure).</summary>
    static bool Female()
    {
        try { return PlayerManager.m_VoicePersona == VoicePersona.Female; } catch { return false; }
    }

    /// <summary>A breath in the character's voice: "inhale_f" for a woman when we have it, else the male one.</summary>
    static string Voiced(string name, bool female) => female && Sfx.Has(name + "_f") ? name + "_f" : name;
}
