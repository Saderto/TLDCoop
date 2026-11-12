using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;
using TrainerByEMPEROR.Shared;

namespace TrainerByEMPEROR.App
{
    internal enum ModStatus
    {
        Missing,
        Outdated,
        Installed,
    }

    // Поиск игры, установка MelonLoader и встроенной части трейнера, запуск игры.
    internal static class GameInstaller
    {
        public const string MelonLoaderUrl = "https://github.com/LavaGang/MelonLoader/releases/download/v0.7.1/MelonLoader.x64.zip";
        private const string SteamAppId = "305620";
        private const string ModFileName = "TrainerByEMPEROR.dll";
        // names of the trainer's files before the mods were renamed to ...ByEMPEROR (11.2026): removed on install
        private static readonly string[] OldFiles = { "TLDTrainer.dll", "TLDTrainer.app.txt" };

        public static bool IsGameFolder(string dir) => !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "tld.exe"));

        public static string FindGame()
        {
            foreach (var dir in Candidates())
                if (IsGameFolder(dir)) return dir;
            return null;
        }

        private static IEnumerable<string> Candidates()
        {
            // Steam: основная папка и все библиотеки из libraryfolders.vdf
            var steamRoots = new List<string>();
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key?.GetValue("SteamPath") is string sp) steamRoots.Add(sp.Replace('/', '\\'));
            }
            catch
            {
                // нет Steam
            }
            steamRoots.Add(@"C:\Program Files (x86)\Steam");

            foreach (var root in steamRoots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                yield return Path.Combine(root, "steamapps", "common", "TheLongDark");
                string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf)) continue;
                string text;
                try
                {
                    text = File.ReadAllText(vdf);
                }
                catch
                {
                    continue;
                }
                foreach (Match m in Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\""))
                    yield return Path.Combine(m.Groups[1].Value.Replace(@"\\", @"\"), "steamapps", "common", "TheLongDark");
            }

            // Epic Games: манифесты установленных игр
            string manifests = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";
            if (Directory.Exists(manifests))
            {
                foreach (var file in Directory.GetFiles(manifests, "*.item"))
                {
                    string loc = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(file));
                        var r = doc.RootElement;
                        string name = r.TryGetProperty("DisplayName", out var dn) ? dn.GetString() : "";
                        if (name != null && name.Contains("Long Dark", StringComparison.OrdinalIgnoreCase) &&
                            r.TryGetProperty("InstallLocation", out var il))
                            loc = il.GetString();
                    }
                    catch
                    {
                        // чужой или испорченный манифест
                    }
                    if (loc != null) yield return loc;
                }
            }
            yield return @"C:\Program Files\Epic Games\TheLongDark";
        }

        public static bool MelonLoaderInstalled(string dir) =>
            File.Exists(Path.Combine(dir, "version.dll")) && Directory.Exists(Path.Combine(dir, "MelonLoader"));

        public static ModStatus GetModStatus(string dir)
        {
            string path = Path.Combine(dir, "Mods", ModFileName);
            if (!File.Exists(path)) return ModStatus.Missing;
            using var sha = SHA256.Create();
            var installed = sha.ComputeHash(File.ReadAllBytes(path));
            var embedded = sha.ComputeHash(EmbeddedMod());
            return installed.SequenceEqual(embedded) ? ModStatus.Installed : ModStatus.Outdated;
        }

        public static async Task Install(string dir, Action<string> log)
        {
            if (IsGameRunning()) throw new InvalidOperationException(L.T("Закройте игру перед установкой.", "Close the game before installing."));

            if (!MelonLoaderInstalled(dir))
            {
                log(L.T("Скачиваю MelonLoader 0.7.1…", "Downloading MelonLoader 0.7.1…"));
                string zip = Path.Combine(Path.GetTempPath(), "MelonLoader.x64.zip");
                using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                {
                    var bytes = await http.GetByteArrayAsync(MelonLoaderUrl);
                    await File.WriteAllBytesAsync(zip, bytes);
                }
                log(L.T("Распаковываю MelonLoader в папку игры…", "Unpacking MelonLoader into the game folder…"));
                await Task.Run(() => ZipFile.ExtractToDirectory(zip, dir, true));
                File.Delete(zip);
            }
            else
            {
                log(L.T("MelonLoader уже установлен.", "MelonLoader is already installed."));
            }

            string mods = Path.Combine(dir, "Mods");
            Directory.CreateDirectory(mods);
            await File.WriteAllBytesAsync(Path.Combine(mods, ModFileName), EmbeddedMod());
            RemoveOld(mods, log);
            log(L.T("Встроенная часть трейнера установлена в папку Mods.", "The in-game part of the trainer is installed into Mods."));
            log(L.T("Готово. Первый запуск игры после установки MelonLoader идёт дольше обычного (1–3 минуты).", "Done. The first game start after installing MelonLoader takes longer than usual (1–3 minutes)."));
        }

        public static void RemoveMod(string dir)
        {
            if (IsGameRunning()) throw new InvalidOperationException(L.T("Закройте игру перед удалением.", "Close the game before removing."));
            string path = Path.Combine(dir, "Mods", ModFileName);
            if (File.Exists(path)) File.Delete(path);
            RemoveOld(Path.Combine(dir, "Mods"), null);
        }

        private static void RemoveOld(string mods, Action<string> log)
        {
            foreach (string f in OldFiles)
            {
                string p = Path.Combine(mods, f);
                try
                {
                    if (!File.Exists(p)) continue;
                    File.Delete(p);
                    log?.Invoke(L.T("Удалён старый файл трейнера: Mods\\", "Removed an old trainer file: Mods\\") + f);
                }
                catch (Exception e) { log?.Invoke(L.T("Не удалось удалить Mods\\", "Could not delete Mods\\") + f + ": " + e.Message); }
            }
        }

        public static bool IsGameRunning() => Process.GetProcessesByName("tld").Length > 0;

        public static void Launch(string dir)
        {
            bool steam = dir.Contains("steamapps", StringComparison.OrdinalIgnoreCase);
            if (steam)
            {
                Process.Start(new ProcessStartInfo("steam://rungameid/" + SteamAppId) { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo(Path.Combine(dir, "tld.exe")) { WorkingDirectory = dir, UseShellExecute = true });
            }
        }

        private static byte[] EmbeddedMod()
        {
            using var s = typeof(GameInstaller).Assembly.GetManifestResourceStream("TrainerByEMPEROR.Mod.dll")
                          ?? throw new InvalidOperationException(L.T("Внутри трейнера нет файла модуля", "The module file is missing inside the trainer"));
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
