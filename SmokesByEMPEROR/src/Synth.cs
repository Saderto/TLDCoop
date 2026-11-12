using System.Runtime.InteropServices;

namespace SmokesByEMPEROR;

/// <summary>Sounds made in code and played straight through Windows (winmm PlaySound): TLD switches Unity audio off and Wwise only plays
/// the game's own banks, so a new sound cannot go through either. Short one-shots only (a new one cuts the previous).</summary>
public static class Synth
{
    const int Rate = 44100;
    public static readonly string[] Names = { "cut1", "cut2", "cut3", "knife1", "knife2" };
    static readonly Dictionary<string, IntPtr> made = new();

    [DllImport("winmm.dll", SetLastError = true)]
    static extern bool PlaySound(IntPtr sound, IntPtr module, uint flags);
    const uint SND_ASYNC = 0x1, SND_NODEFAULT = 0x2, SND_MEMORY = 0x4;

    // waveOut: our own output line (PlaySound said "playing" yet nothing was heard in the game)
    [StructLayout(LayoutKind.Sequential)]
    struct WaveFormat { public short tag, channels; public int rate, bytesPerSec; public short align, bits, extra; }
    [StructLayout(LayoutKind.Sequential)]
    struct WaveHdr { public IntPtr data; public int length, recorded; public IntPtr user; public int flags, loops; public IntPtr next, reserved; }
    [DllImport("winmm.dll")] static extern int waveOutGetNumDevs();
    [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr h, int dev, ref WaveFormat fmt, IntPtr cb, IntPtr inst, int flags);
    [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr h);
    [DllImport("winmm.dll")] static extern int waveOutSetVolume(IntPtr h, uint vol);
    static IntPtr wout, hdrMem;
    static bool hdrPrepared;

    static bool WaveOut(IntPtr pcm, int bytes, out string why)
    {
        why = "";
        int r;
        if (wout == IntPtr.Zero)
        {
            var f = new WaveFormat { tag = 1, channels = 1, rate = Rate, bytesPerSec = Rate * 2, align = 2, bits = 16 };
            r = waveOutOpen(out wout, -1, ref f, IntPtr.Zero, IntPtr.Zero, 0);
            if (r != 0) { wout = IntPtr.Zero; why = $"waveOutOpen {r}, devices {waveOutGetNumDevs()}"; return false; }
            waveOutSetVolume(wout, 0xFFFFFFFF);
            hdrMem = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHdr>());
        }
        int hs = Marshal.SizeOf<WaveHdr>();
        waveOutReset(wout);   // a new cut stops the last one
        if (hdrPrepared) { waveOutUnprepareHeader(wout, hdrMem, hs); hdrPrepared = false; }
        Marshal.StructureToPtr(new WaveHdr { data = pcm, length = bytes }, hdrMem, false);
        r = waveOutPrepareHeader(wout, hdrMem, hs);
        if (r != 0) { why = $"waveOutPrepareHeader {r}"; return false; }
        hdrPrepared = true;
        r = waveOutWrite(wout, hdrMem, hs);
        if (r != 0) { why = $"waveOutWrite {r}"; return false; }
        return true;
    }

    public static bool Is(string name) => Array.IndexOf(Names, name) >= 0;

    // The game's own process plays no sound but its Wwise one (PlaySound said "playing" and was silent, waveOut refused the device),
    // while the same file double-clicked is heard: so a small hidden helper process (PowerShell's SoundPlayer) plays our files,
    // started once and fed file names through its input.
    static System.Diagnostics.Process? helper;
    static bool helperFailed;
    const string HelperScript = "$p=@{}; while(($l=[Console]::In.ReadLine()) -ne $null){ try { if(-not $p.ContainsKey($l)){ $s=New-Object System.Media.SoundPlayer $l; $s.Load(); $p[$l]=$s }; $p[$l].Play() } catch {} }";

    public static void Warm()
    {
        try { Helper(); } catch (Exception e) { Safe.Log("Sound helper", e); }
    }

    static System.Diagnostics.Process? Helper()
    {
        if (helper != null && !helper.HasExited) return helper;
        if (helperFailed) return null;
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"" + HelperScript.Replace("\"", "\\\"") + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
            };
            helper = System.Diagnostics.Process.Start(psi);
            Safe.Info($"Sound helper started: {(helper != null ? "pid " + helper.Id : "no process")}");
            return helper;
        }
        catch (Exception e)
        {
            helperFailed = true;
            Safe.Log("Sound helper start", e);
            return null;
        }
    }

    static string? FileFor(string name, float volume)
    {
        string dir = Path.Combine(Mod.Root, "sounds");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"{name}_{(int)Math.Round(volume * 100)}.wav");
        if (!File.Exists(path)) File.WriteAllBytes(path, Wav(Make(name), volume));
        return path;
    }

    public static bool Play(string name, float volume = 1f)
    {
        try
        {
            if (!Is(name)) return false;
            volume = Mathf.Clamp(volume, 0f, 2f);
            var h = Helper();
            if (h != null)
            {
                var file = FileFor(name, volume);
                h.StandardInput.WriteLine(file);
                h.StandardInput.Flush();
                Safe.Info($"Synth sound {name}: sent to the helper ({file})");
                return true;
            }
            string key = name + "@" + volume.ToString("0.00");
            if (!made.TryGetValue(key, out var mem))
            {
                var wav = Wav(Make(name), volume);
                mem = Marshal.AllocHGlobal(wav.Length);   // kept for good: PlaySound reads it while playing
                Marshal.Copy(wav, 0, mem, wav.Length);
                made[key] = mem;
            }
            int bytes = Marshal.ReadInt32(mem, 40);
            bool ok = WaveOut(mem + 44, bytes, out var why);
            string how = "waveOut";
            if (!ok)
            {
                how = why + " -> PlaySound";
                ok = PlaySound(mem, IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
            }
            Safe.Info($"Synth sound {name}: {(ok ? "playing" : "failed")} ({how}, {bytes} bytes)");
            return ok;
        }
        catch (Exception e) { Safe.Log("Synth sound " + name, e); return false; }
    }
    static readonly HashSet<string> logged = new();

    // ---- building blocks ----
    sealed class Biquad
    {
        float b0, b1, b2, a1, a2, x1, x2, y1, y2;
        public Biquad(float freq, float q)
        {
            // band-pass, constant peak gain
            double w = 2 * Math.PI * freq / Rate, al = Math.Sin(w) / (2 * q), a0 = 1 + al;
            b0 = (float)(al / a0); b1 = 0; b2 = (float)(-al / a0);
            a1 = (float)(-2 * Math.Cos(w) / a0); a2 = (float)((1 - al) / a0);
        }
        public float Run(float x)
        {
            float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            return y;
        }
    }

    static float Env(float t, float at, float attack, float decay) => t < at ? 0f : t < at + attack ? (t - at) / attack : Mathf.Exp(-(t - at - attack) / decay);

    /// <summary>Metal ringing: inharmonic partials, each fading at its own speed.</summary>
    static float Ring(float t, float at, float[] f, float tau, float gain)
    {
        float d = t - at;
        if (d < 0f || d > tau * 8f) return 0f;
        float v = 0f;
        for (int i = 0; i < f.Length; i++)
            v += Mathf.Sin(2f * Mathf.PI * f[i] * d + i) * Mathf.Exp(-d / (tau / (1f + i * .45f))) / (1f + i * .6f);
        return v * gain * Mathf.Clamp01(d * 4000f);
    }

    /// <summary>The blade through the cigar's end: dry leaf crushed and torn (band noise with crackling grains).</summary>
    static float Slice(float t, float from, float to, Biquad bp, System.Random r, float grains, float gain)
    {
        if (t < from || t > to + .02f) return 0f;
        float k = (t - from) / (to - from);
        float env = t > to ? Mathf.Exp(-(t - to) / .004f) : Mathf.SmoothStep(0f, 1f, k * 3f) * (.6f + .4f * k);
        float x = (float)(r.NextDouble() * 2 - 1);
        if (r.NextDouble() < grains) x *= 9f;   // fibres snapping
        return bp.Run(x) * env * gain;
    }

    static float[] Make(string name)
    {
        var r = new System.Random(name.GetHashCode() & 0xffff);
        float len = name.StartsWith("knife") ? .55f : .32f;
        int n = (int)(len * Rate);
        var s = new float[n];
        switch (name)
        {
            case "cut1":
            {
                // small double-blade guillotine: a quick crisp slice, then the blades meet with a bright click
                var bp = new Biquad(3800f, 1.1f);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    s[i] = Slice(t, .004f, .045f, bp, r, .02f, .9f)
                         + Ring(t, .046f, new[] { 3150f, 4870f, 7240f, 9630f }, .018f, .55f)
                         + Ring(t, .001f, new[] { 5200f, 8100f }, .004f, .25f);
                }
                break;
            }
            case "cut2":
            {
                // heavier cutter: a fuller, slower crunch through a thick cigar and a lower "chack"
                var bp = new Biquad(2300f, .9f);
                var bp2 = new Biquad(900f, .8f);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float body = t > .07f ? Mathf.Sin(2f * Mathf.PI * 210f * (t - .07f)) * Mathf.Exp(-(t - .07f) / .02f) * .3f : 0f;
                    s[i] = Slice(t, .005f, .068f, bp, r, .03f, .8f)
                         + Slice(t, .005f, .068f, bp2, r, .0f, .5f)
                         + Ring(t, .069f, new[] { 1950f, 3420f, 5100f, 7400f }, .022f, .5f) + body;
                }
                break;
            }
            case "cut3":
            {
                // scissors-style: two short shears with a light tick each
                var bp = new Biquad(4500f, 1.3f);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    s[i] = Slice(t, .004f, .028f, bp, r, .025f, .75f)
                         + Ring(t, .029f, new[] { 4100f, 6300f, 8800f }, .010f, .4f)
                         + Slice(t, .085f, .118f, bp, r, .025f, .85f)
                         + Ring(t, .119f, new[] { 3900f, 6050f, 8600f }, .014f, .5f);
                }
                break;
            }
            case "knife1":
            {
                // a sharp knife pressed through: slow fibrous crunching, then the cap drops off with a soft tick
                var bp = new Biquad(2600f, 1f);
                var lo = new Biquad(700f, .9f);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float press = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .36f));
                    float x = (float)(r.NextDouble() * 2 - 1);
                    if (r.NextDouble() < .006) x *= 14f;
                    float drop = Ring(t, .39f, new[] { 1700f, 2900f }, .008f, .25f);
                    s[i] = (bp.Run(x) * .7f + lo.Run(x) * .35f) * press * press + drop;
                }
                break;
            }
            default: // knife2
            {
                // two quick saw strokes of a blade, crunchy
                var bp = new Biquad(3100f, 1f);
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float a = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / .16f)), b = Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - .2f) / .2f));
                    float x = (float)(r.NextDouble() * 2 - 1);
                    if (r.NextDouble() < .01) x *= 10f;
                    s[i] = bp.Run(x) * (a * a * .7f + b * b * .9f) + Ring(t, .41f, new[] { 1600f, 2750f }, .008f, .2f);
                }
                break;
            }
        }
        // soft start/end, normalise to a modest peak
        float peak = 1e-4f;
        foreach (var v in s) peak = Math.Max(peak, Math.Abs(v));
        int fade = Rate / 200;
        for (int i = 0; i < n; i++)
        {
            float g = Math.Min(1f, Math.Min(i / (float)fade, (n - 1 - i) / (float)fade));
            s[i] = (float)Math.Tanh(s[i] / peak * 3.0) / (float)Math.Tanh(3.0) * .9f * g;
        }
        return s;
    }

    static byte[] Wav(float[] s, float volume)
    {
        int bytes = s.Length * 2;
        var ms = new MemoryStream(44 + bytes);
        var w = new BinaryWriter(ms);
        w.Write("RIFF"u8.ToArray()); w.Write(36 + bytes); w.Write("WAVE"u8.ToArray());
        w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(bytes);
        foreach (var v in s) w.Write((short)Mathf.Clamp(v * volume * 32767f, -32767f, 32767f));
        w.Flush();
        return ms.ToArray();
    }
}
