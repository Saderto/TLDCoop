using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;

namespace TrainerByEMPEROR
{
    /// <summary>The mods were renamed from "...BySeagull"/"TLD..." to "...ByEMPEROR" (11.2026). Once, on the first start of
    /// the new name, the player's files are moved over from the old folder of the mod, and the old copy of the dll (if
    /// it is still in Mods) is put aside, so the two copies are not loaded together next time.
    /// Settings (MelonPreferences), saves and UserData keep their old names and need nothing.</summary>
    internal static class Legacy
    {
        /// <param name="oldName">old folder/dll name in Mods</param>
        /// <param name="newName">new folder name in Mods</param>
        /// <param name="renames">pairs "old relative path", "new relative path" (files renamed together with the mod)</param>
        internal static void Move(MelonLogger.Instance log, string oldName, string newName, params string[] renames)
        {
            try
            {
                string mods = MelonEnvironment.ModsDirectory;
                string from = Path.Combine(mods, oldName), to = Path.Combine(mods, newName);
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i + 1 < renames.Length; i += 2) map[renames[i]] = renames[i + 1];
                if (Directory.Exists(from))
                {
                    int moved = 0, dropped = 0;
                    foreach (string f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                    {
                        string rel = Path.GetRelativePath(from, f).Replace('\\', '/');
                        if (map.TryGetValue(rel, out string r))
                        {
                            if (string.IsNullOrEmpty(r)) { File.Delete(f); dropped++; continue; }
                            rel = r;
                        }
                        string dst = Path.Combine(to, rel);
                        if (File.Exists(dst)) { File.Delete(f); dropped++; continue; }   // the new version brought its own copy
                        Directory.CreateDirectory(Path.GetDirectoryName(dst));
                        File.Move(f, dst);
                        moved++;
                    }
                    foreach (string d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                        if (Directory.Exists(d) && Directory.GetFileSystemEntries(d).Length == 0) TryRemoveTree(d);
                    TryRemoveTree(from);
                    log.Msg($"Mods\\{oldName} -> Mods\\{newName}: moved {moved}, old copies removed {dropped}");
                }
                PutAside(log, Path.Combine(mods, oldName + ".dll"));
            }
            catch (Exception e) { log.Warning("old folder " + oldName + ": " + e.Message); }
        }

        /// <summary>An old file of the mod (dll, plugin) is renamed to *.old so it is not loaded next time.</summary>
        internal static void PutAside(MelonLogger.Instance log, string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                string to = path + ".old";
                if (File.Exists(to)) File.Delete(to);
                File.Move(path, to);
                log.Warning($"{Path.GetFileName(path)} (old name of this mod) renamed to {Path.GetFileName(to)}: restart the game / старый файл мода отключён, перезапустите игру");
            }
            catch (Exception e)
            {
                log.Error($"Delete the old {Path.GetFileName(path)} from Mods: both copies are loaded / удалите старый {Path.GetFileName(path)} из Mods ({e.Message})");
            }
        }

        static void TryRemoveTree(string d)
        {
            try
            {
                foreach (string s in Directory.GetDirectories(d)) TryRemoveTree(s);
                if (Directory.GetFileSystemEntries(d).Length == 0) Directory.Delete(d);
            }
            catch { }
        }
    }
}
