using MelonLoader;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(SmokesAudioOnByEMPEROR.Plugin), "SmokesAudioOnByEMPEROR", "1.0.1", "EMPEROR")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace SmokesAudioOnByEMPEROR;

/// <summary>The game ships with Unity's own audio switched off (AudioManager m_DisableAudio in globalgamemanagers): all its sounds go
/// through Wwise. SmokesByEMPEROR plays its own wav files through Unity, so before Unity starts this clears that one flag (one byte),
/// keeping a copy of the original file. AudioCore did the same but no longer starts with MelonLoader 0.7 (AssetsTools.NET 3).</summary>
public sealed class Plugin : MelonPlugin
{
    // AudioManager's m_DisableAudio sits after two empty strings (spatializer, ambisonic decoder); the flag is followed by
    // m_VirtualizeEffects (1, aligned) and m_RequestedDSPBufferSize (1024)
    static readonly byte[] Before = { 0x00, 0x02, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    static readonly byte[] After = { 0x01, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00 };
    const long Known = 62296;   // game 2.60 (Unity 6000.0.60f1)

    public override void OnPreInitialization()
    {
        try
        {
            var dir = MelonEnvironment.UnityGameDataDirectory;
            var path = Path.Combine(dir, "globalgamemanagers");
            if (!File.Exists(path)) { LoggerInstance.Warning("globalgamemanagers not found: " + path); return; }
            var data = File.ReadAllBytes(path);
            long at = Matches(data, Known) ? Known : -1;
            if (at < 0)
            {
                int found = 0;
                for (long i = Before.Length; i + 1 + After.Length <= data.Length; i++)
                    if (Matches(data, i)) { found++; at = i; if (found > 1) break; }
                if (found != 1) { LoggerInstance.Warning($"Unity audio flag not found safely ({found} places): left as is"); return; }
            }
            if (data[at] == 0) { LoggerInstance.Msg($"Unity audio already on (flag at {at})"); return; }
            var bak = path + ".smokes_bak";
            if (!File.Exists(bak)) File.Copy(path, bak);
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            {
                fs.Seek(at, SeekOrigin.Begin);
                fs.WriteByte(0);
            }
            LoggerInstance.Msg($"Unity audio switched on (flag at {at}); original kept as globalgamemanagers.smokes_bak");
        }
        catch (Exception e) { LoggerInstance.Error("Could not switch Unity audio on: " + e.Message); }
    }

    static bool Matches(byte[] d, long at)
    {
        if (at - Before.Length < 0 || at + 1 + After.Length > d.Length) return false;
        if (d[at] > 1) return false;
        for (int i = 0; i < Before.Length; i++) if (d[at - Before.Length + i] != Before[i]) return false;
        for (int i = 0; i < After.Length; i++) if (d[at + 1 + i] != After[i]) return false;
        return true;
    }
}
