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

namespace TLDTrainer.App
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
        private const string ModFileName = "TLDTrainer.dll";

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
            if (IsGameRunning()) throw new InvalidOperationException("Закройте игру перед установкой.");

            if (!MelonLoaderInstalled(dir))
            {
                log("Скачиваю MelonLoader 0.7.1…");
                string zip = Path.Combine(Path.GetTempPath(), "MelonLoader.x64.zip");
                using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                {
                    var bytes = await http.GetByteArrayAsync(MelonLoaderUrl);
                    await File.WriteAllBytesAsync(zip, bytes);
                }
                log("Распаковываю MelonLoader в папку игры…");
                await Task.Run(() => ZipFile.ExtractToDirectory(zip, dir, true));
                File.Delete(zip);
            }
            else
            {
                log("MelonLoader уже установлен.");
            }

            string mods = Path.Combine(dir, "Mods");
            Directory.CreateDirectory(mods);
            await File.WriteAllBytesAsync(Path.Combine(mods, ModFileName), EmbeddedMod());
            log("Встроенная часть трейнера установлена в папку Mods.");
            log("Готово. Первый запуск игры после установки MelonLoader идёт дольше обычного (1–3 минуты).");
        }

        public static void RemoveMod(string dir)
        {
            if (IsGameRunning()) throw new InvalidOperationException("Закройте игру перед удалением.");
            string path = Path.Combine(dir, "Mods", ModFileName);
            if (File.Exists(path)) File.Delete(path);
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
            using var s = typeof(GameInstaller).Assembly.GetManifestResourceStream("TLDTrainer.Mod.dll")
                          ?? throw new InvalidOperationException("Внутри трейнера нет файла модуля");
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
