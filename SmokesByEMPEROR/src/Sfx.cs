namespace SmokesByEMPEROR;

/// <summary>Recorded sounds (CC0, Mods/SmokesByEMPEROR/sounds/&lt;name&gt;_&lt;n&gt;.wav, 16-bit PCM) played through Unity audio.
/// TLD plays its own sound through Wwise and keeps Unity audio off; the AudioCore plugin turns it back on. Without it (output rate 0)
/// every call returns false and the caller falls back to the game's Wwise sounds.
/// Volume: our setting × the game's master × effects volume. Remote figures' sounds are 3D at the figure.</summary>
public static class Sfx
{
    static readonly Dictionary<string, List<AudioClip>> clips = new();
    static bool loaded, loggedRate;
    static AudioSource? local;

    public static bool Available
    {
        get
        {
            int rate = 0;
            try { rate = AudioSettings.outputSampleRate; } catch { }
            if (!loggedRate) { loggedRate = true; Safe.Info($"Sfx: Unity audio output rate {rate} ({(rate > 0 ? "on (AudioCore)" : "off: using Wwise sounds")})"); }
            return rate > 0;
        }
    }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            var dir = Path.Combine(Mod.Root, "sounds");
            if (!Directory.Exists(dir)) { Safe.Info("Sfx: no sounds folder"); return; }
            foreach (var f in Directory.GetFiles(dir, "*.wav"))
            {
                var stem = Path.GetFileNameWithoutExtension(f);
                int us = stem.LastIndexOf('_');
                string name = us > 0 && int.TryParse(stem.Substring(us + 1), out _) ? stem.Substring(0, us) : stem;
                var c = Wav(f, stem);
                if (c == null) continue;
                if (!clips.TryGetValue(name, out var list)) clips[name] = list = new();
                list.Add(c);
            }
            Safe.Info($"Sfx: {clips.Count} sounds loaded ({string.Join(", ", clips.Select(kv => kv.Key + "×" + kv.Value.Count))})");
        }
        catch (Exception e) { Safe.Log("Sfx load", e); }
    }

    /// <summary>A RIFF/WAVE file, PCM 16-bit, mono or stereo (stereo is mixed down).</summary>
    static AudioClip? Wav(string path, string name)
    {
        try
        {
            var b = File.ReadAllBytes(path);
            int ch = 1, rate = 44100, bits = 16, dataAt = -1, dataLen = 0;
            for (int p = 12; p + 8 <= b.Length;)
            {
                string id = System.Text.Encoding.ASCII.GetString(b, p, 4);
                int len = BitConverter.ToInt32(b, p + 4);
                if (id == "fmt ") { ch = BitConverter.ToInt16(b, p + 10); rate = BitConverter.ToInt32(b, p + 12); bits = BitConverter.ToInt16(b, p + 22); }
                else if (id == "data") { dataAt = p + 8; dataLen = Math.Min(len, b.Length - dataAt); break; }
                p += 8 + len + (len & 1);
            }
            if (dataAt < 0 || bits != 16 || ch < 1) { Safe.Info($"Sfx: {name} skipped (need 16-bit PCM wav)"); return null; }
            int frames = dataLen / (2 * ch);
            var data = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                float s = 0f;
                for (int c = 0; c < ch; c++) s += BitConverter.ToInt16(b, dataAt + (i * ch + c) * 2) / 32768f;
                data[i] = s / ch;
            }
            var clip = AudioClip.Create("smokes_" + name, frames, 1, rate, false);
            clip.SetData(new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>(data), 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }
        catch (Exception e) { Safe.Log("Sfx wav " + name, e); return null; }
    }

    static float GameVolume()
    {
        try
        {
            var st = Il2CppTLD.SaveState.BaseStateSingleton<Il2CppTLD.SaveState.SettingsState>.Instance;
            if (st != null) return st.m_MasterVolume * st.m_SoundVolume;
        }
        catch { }
        return 1f;
    }

    static AudioClip? Pick(string name)
    {
        Load();
        return clips.TryGetValue(name, out var l) && l.Count > 0 ? l[UnityEngine.Random.Range(0, l.Count)] : null;
    }

    public static bool Has(string name) { Load(); return clips.ContainsKey(name); }

    static void EnsureListener()
    {
        if (UnityEngine.Object.FindObjectOfType<AudioListener>() != null) return;
        var cam = GameManager.GetMainCamera();
        if (cam != null) cam.gameObject.AddComponent<AudioListener>();
    }

    /// <summary>The smoker's own sound, at the ears.</summary>
    public static bool Play(string name, float vol = 1f, float pitch = 1f)
    {
        if (!Settings.Sounds.Value) return true;
        try
        {
            if (!Available) return false;
            var c = Pick(name);
            if (c == null) return false;
            EnsureListener();
            if (local == null || !local)
            {
                var go = new GameObject("SmokesSfx");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                local = go.AddComponent<AudioSource>();
                local.playOnAwake = false;
                local.spatialBlend = 0f;
            }
            local.pitch = pitch * UnityEngine.Random.Range(.95f, 1.05f);
            local.PlayOneShot(c, vol * Settings.SoundVolume.Value * GameVolume());
            return true;
        }
        catch (Exception e) { Safe.Log("Sfx " + name, e); return false; }
    }

    static AudioSource? emberSrc;
    static int emberGen;

    /// <summary>The ember's crackle during a drag: on its own source, so it can be stopped when the drag ends.</summary>
    public static bool PlayEmber(string name, float vol = 1f)
    {
        if (!Settings.Sounds.Value) return true;
        try
        {
            if (!Available) return false;
            var c = Pick(name);
            if (c == null) return false;
            EnsureListener();
            if (emberSrc == null || !emberSrc)
            {
                var go = new GameObject("SmokesSfxEmber");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                emberSrc = go.AddComponent<AudioSource>();
                emberSrc.playOnAwake = false;
                emberSrc.spatialBlend = 0f;
            }
            emberGen++;
            emberSrc.Stop();
            emberSrc.clip = c;
            emberSrc.pitch = UnityEngine.Random.Range(.95f, 1.05f);
            emberSrc.volume = vol * Settings.SoundVolume.Value * GameVolume();
            emberSrc.Play();
            return true;
        }
        catch (Exception e) { Safe.Log("Sfx ember " + name, e); return false; }
    }

    /// <summary>The drag is over: the crackle fades out quickly.</summary>
    public static void StopEmber(float fade = .2f)
    {
        try
        {
            if (emberSrc == null || !emberSrc || !emberSrc.isPlaying) return;
            MelonLoader.MelonCoroutines.Start(Fade(emberSrc, fade, ++emberGen));
        }
        catch { }
    }

    static System.Collections.IEnumerator Fade(AudioSource s, float time, int gen)
    {
        float v0 = s.volume, t = 0f;
        while (t < time && s && gen == emberGen)
        {
            t += Time.unscaledDeltaTime;
            s.volume = v0 * (1f - Mathf.Clamp01(t / time));
            yield return null;
        }
        if (s && gen == emberGen) s.Stop();
    }

    /// <summary>Another player's sound, from their figure (heard up to ~12 m).</summary>
    public static bool PlayAt(string name, Vector3 at, float vol = 1f)
    {
        if (!Settings.Sounds.Value) return true;
        try
        {
            if (!Available) return false;
            var c = Pick(name);
            if (c == null) return false;
            EnsureListener();
            var go = new GameObject("SmokesSfx3D");
            go.transform.position = at;
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1f;
            s.maxDistance = 12f;
            s.pitch = UnityEngine.Random.Range(.95f, 1.05f);
            s.PlayOneShot(c, vol * Settings.SoundVolume.Value * GameVolume());
            UnityEngine.Object.Destroy(go, c.length + .5f);
            return true;
        }
        catch (Exception e) { Safe.Log("Sfx 3D " + name, e); return false; }
    }
}
