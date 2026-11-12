using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SmokehouseByEMPEROR;

/// <summary>The kettle's boiling, made in code and played through Unity audio (11-12 owner: four tries with the game's own Wwise boiling event gave an id
/// and silence each time). Bubbles are short rising tones, as a bubble in water rings; under them a low rumble; all of it dulled, as under a lid.
/// Unity audio is off in the plain game and on with the SmokesAudioOn plugin; without it Fx falls back to the game's event.</summary>
public static class Boil
{
    static AudioClip? clip;
    static readonly Dictionary<int, float> gain = new();     // 11-12 owner: it began and ended at once. Each source swells and dies away
    static bool said;

    public static bool Available
    {
        get
        {
            int rate = 0;
            try { rate = AudioSettings.outputSampleRate; } catch { }
            if (!said) { said = true; MelonLogger.Msg($"Boiling sound: Unity audio output rate {rate} ({(rate > 0 ? "own sound" : "off, the game's event is tried")})"); }
            return rate > 0;
        }
    }

    static AudioClip Clip()
    {
        if (clip != null) return clip;
        const int rate = 22050, seconds = 8, n = rate * seconds;
        var rnd = new System.Random(7);
        var d = new float[n];
        // bubbles
        for (int k = 0; k < seconds * 34; k++)
        {
            int start = rnd.Next(n);
            float f0 = 150f * Mathf.Pow(5.5f, (float)rnd.NextDouble());
            int dur = (int)(rate * (.03f + .07f * (float)rnd.NextDouble()));
            float amp = (.10f + .30f * (float)rnd.NextDouble()) * Mathf.Min(1.4f, Mathf.Sqrt(260f / f0));
            float rise = .5f + .9f * (float)rnd.NextDouble(), phase = 0f;
            for (int i = 0; i < dur; i++)
            {
                float t = i / (float)dur;
                phase += 2f * Mathf.PI * f0 * (1f + rise * t) / rate;
                float env = Mathf.Min(1f, i / (.003f * rate)) * Mathf.Exp(-5f * t);
                d[(start + i) % n] += amp * env * Mathf.Sin(phase);
            }
        }
        // a rolling rumble and a little hiss; the filters run round the loop twice so that its ends meet
        var w = new float[n];
        for (int i = 0; i < n; i++) w[i] = (float)rnd.NextDouble() * 2f - 1f;
        float low = 0f, low2 = 0f;
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++)
            {
                low += .012f * (w[i] - low);
                low2 += .012f * (low - low2);      // twice through: no hiss is left of the noise (11-12 owner), only a low roll
                if (pass == 1) d[i] += low2 * 2.2f * (.7f + .3f * Mathf.Sin(2f * Mathf.PI * 3f * i / n));
            }
        float lid = 0f, peak = 0f;
        var o = new float[n];
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++)
            {
                lid += .22f * (d[i] - lid);
                if (pass == 1) { o[i] = lid; peak = Mathf.Max(peak, Mathf.Abs(lid)); }
            }
        if (peak > 0f) for (int i = 0; i < n; i++) o[i] *= .85f / peak;
        clip = AudioClip.Create("sh_boil", n, 1, rate, false);
        clip.SetData(new Il2CppStructArray<float>(o), 0);
        clip.hideFlags = HideFlags.HideAndDontSave;
        return clip;
    }

    public static AudioSource? Start(GameObject on)
    {
        try
        {
            if (UnityEngine.Object.FindObjectOfType<AudioListener>() == null)
            {
                var cam = GameManager.GetMainCamera();
                if (cam != null) cam.gameObject.AddComponent<AudioListener>();
            }
            var s = on.GetComponent<AudioSource>() ?? on.AddComponent<AudioSource>();
            s.clip = Clip();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;       // the loudness by distance is ours (Volume): where the game keeps its listener is not known
            s.volume = 0f;
            s.time = UnityEngine.Random.value * 7f;
            s.Play();
            MelonLogger.Msg($"Kettle boiling: own sound started, playing {s.isPlaying}");
            return s;
        }
        catch (Exception e) { Safe.Log("Own boiling sound", e); return null; }
    }

    /// <summary>Every frame: full within 1.5 m, gone at 13 m; silent while the game stands still.</summary>
    public static void Volume(AudioSource s, Vector3 ears)
    {
        float game = 1f;
        try
        {
            var st = Il2CppTLD.SaveState.BaseStateSingleton<Il2CppTLD.SaveState.SettingsState>.Instance;
            if (st != null) game = st.m_MasterVolume * st.m_SoundVolume;
        }
        catch { }
        float far = Mathf.Clamp01(1f - (Vector3.Distance(ears, s.transform.position) - 1.5f) / 11.5f);
        int id = s.GetInstanceID();
        gain.TryGetValue(id, out float g);
        g = Mathf.MoveTowards(g, 1f, Time.unscaledDeltaTime / 2.5f);
        gain[id] = g;
        s.volume = Time.timeScale <= 0f ? 0f : Tune.BoilVolume * game * far * far * g * g;
    }

    /// <summary>The fire is out: the boiling dies away in a couple of seconds, then the object goes.</summary>
    public static void FadeOut(AudioSource s, GameObject go)
    {
        try { MelonLoader.MelonCoroutines.Start(Fade(s, go)); }
        catch { try { UnityEngine.Object.Destroy(go); } catch { } }
    }

    static System.Collections.IEnumerator Fade(AudioSource s, GameObject go)
    {
        float v0 = 0f, t = 0f;
        try { v0 = s.volume; go.transform.SetParent(null, true); } catch { }
        while (t < 2.5f && s)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / 2.5f);
            s.volume = v0 * k * k;
            yield return null;
        }
        try { if (go) UnityEngine.Object.Destroy(go); } catch { }
    }
}
