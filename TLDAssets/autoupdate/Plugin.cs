using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MelonLoader;
using MelonLoader.Utils;

[assembly: MelonInfo(typeof(AutoUpdateByEMPEROR.Plugin), "AutoUpdateByEMPEROR", "1.0.0", "EMPEROR")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace AutoUpdateByEMPEROR;

/// <summary>Updates EMPEROR's mods when the game starts, before MelonLoader loads them (plugins start first, so the mods'
/// dll files are not in use yet). It does what the launcher's "Install / update" does, for the mods the launcher installed:
/// reads the mod list from GitHub, downloads the new zip of each mod whose version changed, checks it against SHA256SUMS.txt
/// of the release and unpacks it the way the launcher does (same file list in Mods/EMPERORMods.json). Libraries a new version
/// needs and does not find are installed too. Offline, or when anything goes wrong, the game starts with the mods as they are.
/// The launcher puts this plugin into Plugins when "Update automatically" is on and removes it when it is off.</summary>
public sealed class Plugin : MelonPlugin
{
    const string CatalogUrl = "https://raw.githubusercontent.com/Saderto/TLDCoop/main/TLDAssets/catalog.json";
    const string Manifest = "Mods/EMPERORMods.json";
    static readonly string[] SkipParts = { "saves/", "logs/" };

    string game = "";
    HttpClient http = null!, httpLong = null!;

    public override void OnPreInitialization()
    {
        try
        {
            game = MelonEnvironment.GameRootDirectory;
            http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = TimeSpan.FromSeconds(8) };
            httpLong = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = TimeSpan.FromMinutes(10) };
            foreach (var h in new[] { http, httpLong }) h.DefaultRequestHeaders.UserAgent.ParseAdd("EMPERORModsAutoUpdate/1.0.0");
            Run();
        }
        catch (Exception e) { LoggerInstance.Warning("Auto update skipped: " + e.Message); }
    }

    void Run()
    {
        var manPath = Path.Combine(game, Manifest);
        if (!File.Exists(manPath)) { LoggerInstance.Msg("No launcher record (Mods/EMPERORMods.json): nothing to update"); return; }
        var man = JsonNode.Parse(File.ReadAllText(manPath))!.AsObject();

        JsonObject cat;
        try { cat = JsonNode.Parse(Get(CatalogUrl))!.AsObject(); }
        catch (Exception e) { LoggerInstance.Msg("No connection to GitHub, mods left as they are (" + e.Message + ")"); return; }

        var todo = new List<JsonObject>();
        foreach (var n in cat["mods"]!.AsArray())
        {
            var mod = n!.AsObject();
            var rec = man[Str(mod, "id")] as JsonObject;
            if (rec == null || !File.Exists(Path.Combine(game, Str(mod, "detect")))) continue;    // not installed by the launcher
            if (Str(rec, "version") == Str(mod, "version")) continue;
            var old = (mod["legacy"] as JsonObject)?["dll"]?.GetValue<string>();
            if (old != null && File.Exists(Path.Combine(game, old)))
            { LoggerInstance.Msg($"{Str(mod, "id")}: still has its old name, update it in the launcher"); continue; }
            todo.Add(mod);
        }
        if (todo.Count == 0) { LoggerInstance.Msg("Mods are up to date"); return; }

        var libs = cat["libs"] as JsonObject ?? new JsonObject();
        var sums = Sums(cat);
        var tmp = Path.Combine(Path.GetTempPath(), "emperor_autoupdate_" + Environment.ProcessId);
        Directory.CreateDirectory(tmp);
        var built = new List<string>();
        try
        {
            foreach (var mod in todo)
            {
                var id = Str(mod, "id");
                var from = Str((JsonObject)man[id]!, "version");
                try
                {
                    foreach (var need in mod["needs"]?.AsArray() ?? new JsonArray())
                    {
                        var ln = need!.GetValue<string>();
                        if (libs[ln] is not JsonObject lib || lib["manual"] != null) continue;
                        if (File.Exists(Path.Combine(game, Str(lib, "path")))) continue;
                        InstallLib(cat, ln, lib, man, sums, tmp);
                    }
                    LoggerInstance.Msg($"{id}: {from} -> {Str(mod, "version")}, downloading {Size(mod)}…");
                    var z = Path.Combine(tmp, Str(mod, "zip"));
                    Download(Urls(cat, mod, Str(mod, "zip")), z);
                    Verify(sums, mod, Str(mod, "zip"), z);
                    var n = InstallZip(mod, z, man, Str(mod, "version"));
                    Save(man, manPath);
                    File.Delete(z);
                    LoggerInstance.Msg($"{id}: updated to {Str(mod, "version")} ({n} files)");
                    if (mod["assets"]?.GetValue<bool>() == true) built.Add(id);
                }
                catch (Exception e) { LoggerInstance.Warning($"{id}: not updated, the old version stays ({e.Message})"); }
            }
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
        if (built.Count > 0) BuildAssets();
    }

    // ---------------- the launcher's logic (launcher_core.py), kept the same ----------------

    void InstallLib(JsonObject cat, string name, JsonObject lib, JsonObject man, Dictionary<string, string> sums, string tmp)
    {
        if (lib["zip"] != null)
        {
            LoggerInstance.Msg($"library {name}: downloading…");
            var z = Path.Combine(tmp, Str(lib, "zip"));
            Download(Urls(cat, lib, Str(lib, "zip")), z);
            Verify(sums, lib, Str(lib, "zip"), z);
            var m = new JsonObject { ["id"] = name, ["zip"] = Str(lib, "zip") };
            InstallZip(m, z, man, lib["version"]?.GetValue<string>() ?? Str(lib, "note"));
            ((JsonObject)man[name]!)["lib"] = true;
            File.Delete(z);
        }
        else
        {
            var repo = Str(lib, "repo"); var asset = Str(lib, "asset"); var tag = lib["tag"]?.GetValue<string>();
            var url = tag != null ? $"https://github.com/{repo}/releases/download/{tag}/{asset}" : $"https://github.com/{repo}/releases/latest/download/{asset}";
            LoggerInstance.Msg($"library {name}: {url}");
            var dst = Path.Combine(game, Str(lib, "path"));
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            var t = Path.Combine(tmp, asset);
            DownloadOne(url, t);
            var version = tag ?? TagByRedirect(url) ?? "";
            File.Move(t, dst, true);
            man[name] = new JsonObject
            {
                ["version"] = version, ["files"] = new JsonArray(Str(lib, "path")), ["lib"] = true,
                ["time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };
        }
        LoggerInstance.Msg($"library {name}: installed");
    }

    /// <summary>Extracts a zip into the game like launcher_core.install_zip; returns the number of files.</summary>
    int InstallZip(JsonObject mod, string zpath, JsonObject man, string version)
    {
        var keep = new HashSet<string>((mod["keep"] as JsonArray)?.Select(x => x!.GetValue<string>()) ?? Array.Empty<string>());
        var files = new List<string>();
        using (var z = ZipFile.OpenRead(zpath))
        {
            // write everything to .new first: a file in use stops the update before anything is replaced
            var plan = new List<(ZipArchiveEntry e, string rel, string dst)>();
            foreach (var e in z.Entries)
            {
                var rel = TargetOf(mod, e.FullName);
                if (rel == null) continue;
                var dst = Path.Combine(game, rel.Replace('/', Path.DirectorySeparatorChar));
                if (keep.Contains(rel) && File.Exists(dst)) continue;
                plan.Add((e, rel, dst));
            }
            try
            {
                foreach (var (e, _, dst) in plan)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    e.ExtractToFile(dst + ".new", true);
                }
            }
            catch
            {
                foreach (var (_, _, dst) in plan) try { File.Delete(dst + ".new"); } catch { }
                throw;
            }
            foreach (var (_, rel, dst) in plan)
            {
                File.Move(dst + ".new", dst, true);
                if (!keep.Contains(rel)) files.Add(rel);
            }
        }
        var id = Str(mod, "id");
        var old = (man[id] as JsonObject)?["files"] as JsonArray;
        if (old != null)
            foreach (var r in old.Select(x => x!.GetValue<string>()).Except(files))
                try { var p = Path.Combine(game, r.Replace('/', Path.DirectorySeparatorChar)); if (File.Exists(p)) File.Delete(p); } catch { }
        var rec = new JsonObject
        {
            ["version"] = version,
            ["files"] = new JsonArray(files.OrderBy(x => x, StringComparer.Ordinal).Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
            ["time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        };
        if (man[id] is JsonObject prev && prev["lib"] != null) rec["lib"] = true;
        man[id] = rec;
        return files.Count;
    }

    static string? TargetOf(JsonObject mod, string name)
    {
        var n = name.Replace('\\', '/');
        if (n.EndsWith("/")) return null;
        var strip = mod["strip"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(strip))
        {
            if (!n.StartsWith(strip)) return null;
            n = n.Substring(strip.Length);
            if (!(n.StartsWith("Mods/") || n.StartsWith("Plugins/") || n.StartsWith("UserLibs/")))
                n = (mod["other_to"]?.GetValue<string>() ?? "") + n;
        }
        if (n.StartsWith("/") || n.Split('/').Contains("..") || n.Contains(':')) return null;
        if (SkipParts.Any(s => n.Contains(s))) return null;
        return n;
    }

    static List<string> Urls(JsonObject cat, JsonObject item, string asset)
    {
        var repo = Str(cat, "repo");
        var tag = item["tag"]?.GetValue<string>() ?? cat["tag"]?.GetValue<string>();
        var u = new List<string>();
        if (tag != null) u.Add($"https://github.com/{repo}/releases/download/{tag}/{asset}");
        u.Add($"https://github.com/{repo}/releases/latest/download/{asset}");
        return u;
    }

    Dictionary<string, string> Sums(JsonObject cat)
    {
        var d = new Dictionary<string, string>();
        try
        {
            foreach (var line in Get(Urls(cat, new JsonObject(), "SHA256SUMS.txt")[0]).Split('\n'))
            {
                var p = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length == 2 && p[0].Length == 64) d[p[1].TrimStart('*')] = p[0].ToLowerInvariant();
            }
        }
        catch { }
        return d;
    }

    void Verify(Dictionary<string, string> sums, JsonObject item, string name, string path)
    {
        if (item["tag"] != null) return;                         // pinned to another release: its sums are not in ours
        if (!sums.TryGetValue(name, out var want)) { LoggerInstance.Msg($"  no checksum for {name}, check skipped"); return; }
        using var f = File.OpenRead(path);
        using var sha = SHA256.Create();
        var got = Convert.ToHexString(sha.ComputeHash(f)).ToLowerInvariant();
        if (got != want) throw new Exception($"SHA-256 of {name} does not match the release");
    }

    void Download(List<string> urls, string dest)
    {
        Exception? last = null;
        foreach (var u in urls)
        {
            try { DownloadOne(u, dest); return; }
            catch (Exception e) { last = e; LoggerInstance.Msg($"  {u}: {e.Message}"); }
        }
        throw new Exception("download failed: " + last?.Message);
    }

    /// <summary>The tag behind …/releases/latest/download/asset (GitHub answers with a redirect to …/download/TAG/asset).</summary>
    string? TagByRedirect(string url)
    {
        try
        {
            using var h = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(8) };
            h.DefaultRequestHeaders.UserAgent.ParseAdd("EMPERORModsAutoUpdate/1.0.0");
            using var r = h.Send(new HttpRequestMessage(HttpMethod.Head, url));
            var path = r.Headers.Location?.ToString() ?? "";
            var i = path.IndexOf("/releases/download/", StringComparison.Ordinal);
            return i >= 0 ? path.Substring(i + 19).Split('/')[0] : null;
        }
        catch { return null; }
    }

    void DownloadOne(string url, string dest)
    {
        using var r = httpLong.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        r.EnsureSuccessStatusCode();
        long total = r.Content.Headers.ContentLength ?? 0, done = 0; int shown = 0;
        var part = dest + ".part";
        using (var src = r.Content.ReadAsStream())
        using (var f = File.Create(part))
        {
            var buf = new byte[1 << 18]; int k;
            while ((k = src.Read(buf, 0, buf.Length)) > 0)
            {
                f.Write(buf, 0, k); done += k;
                if (total > 20_000_000 && done * 4 / total > shown) { shown = (int)(done * 4 / total); if (shown < 4) LoggerInstance.Msg($"  {shown * 25}%"); }
            }
        }
        File.Move(part, dest, true);
    }

    string Get(string url) => http.GetStringAsync(url).GetAwaiter().GetResult();

    /// <summary>Story files (episode 5) for the co-op mod, shotgun, stim and lamp: TLDAssets.exe builds only what is missing
    /// or differs from the checked build, so after most updates it finishes at once.</summary>
    void BuildAssets()
    {
        var exe = Path.Combine(game, "Mods", "TLDAssets.exe");
        if (!File.Exists(exe)) return;
        try
        {
            LoggerInstance.Msg("Checking the story files (TLDAssets)…");
            var p = Process.Start(new ProcessStartInfo(exe, $"--game \"{game}\" --no-pause") { UseShellExecute = false, CreateNoWindow = true });
            if (p == null) return;
            if (!p.WaitForExit(300_000)) { LoggerInstance.Warning("TLDAssets is still working, the game goes on"); return; }
            LoggerInstance.Msg(p.ExitCode == 0 ? "Story files ready" : $"TLDAssets finished with code {p.ExitCode}, see Mods/TLDAssets.log");
        }
        catch (Exception e) { LoggerInstance.Warning("TLDAssets: " + e.Message); }
    }

    static void Save(JsonObject man, string path)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, man.ToJsonString(new JsonSerializerOptions
        { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        File.Move(tmp, path, true);
    }

    static string Str(JsonObject o, string k) => o[k]?.GetValue<string>() ?? "";
    static string Size(JsonObject mod)
    {
        var s = mod["size"]?.GetValue<long>() ?? 0;
        return s >= 1048576 ? $"{s / 1048576.0:0.0} MB" : s > 0 ? $"{s / 1024} KB" : "";
    }
}
