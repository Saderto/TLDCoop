using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MelonLoader;
using MelonLoader.Utils;

namespace TrainerByEMPEROR
{
    // Имена ресурсов игры, прочитанные прямо из файлов каталога на диске (основная игра и дополнения).
    // Перебирать каталог внутри игры нельзя: в нём сотни тысяч ключей, игра на этом зависает.
    internal static class GameCatalog
    {
        private static Task<List<string>> _keys;
        private static readonly HashSet<string> _modGear = new(StringComparer.Ordinal);

        public static Task<List<string>> Keys => _keys ??= Task.Run(LoadAll);

        // Предметы (префабы GEAR_*), которые добавляют моды ModComponent. Готово, когда готов Keys.
        public static IReadOnlyCollection<string> ModGear => _modGear;

        private static List<string> LoadAll()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in Files())
            {
                try
                {
                    int before = set.Count;
                    Parse(file, set, null);
                    MelonLogger.Msg($"Каталог: {Path.GetFileName(file)} — ключей {set.Count - before}");
                }
                catch (Exception e)
                {
                    MelonLogger.Warning($"Каталог {file}: {e.Message}");
                }
            }
            foreach (var file in ModFiles())
            {
                try
                {
                    var gear = new HashSet<string>(StringComparer.Ordinal);
                    Parse(file, set, gear);
                    _modGear.UnionWith(gear);
                    MelonLogger.Msg($"Каталог мода: {Path.GetFileName(file)} — предметов {gear.Count}");
                }
                catch (Exception e)
                {
                    MelonLogger.Warning($"Каталог {file}: {e.Message}");
                }
            }
            return set.ToList();
        }

        // ModComponent распаковывает моды (*.modcomponent) в Mods/_ModComponentTemp, у каждого свой каталог.
        private static IEnumerable<string> ModFiles()
        {
            string dir = Path.Combine(MelonEnvironment.ModsDirectory, "_ModComponentTemp");
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            return Directory.EnumerateFiles(dir, "catalog*.json", SearchOption.AllDirectories);
        }

        private static IEnumerable<string> Files()
        {
            string root = MelonEnvironment.GameRootDirectory;
            string main = Path.Combine(root, "tld_Data", "StreamingAssets", "aa", "catalog.json");
            if (File.Exists(main)) yield return main;
            string dlc = Path.Combine(root, "tld_dlc");
            if (!Directory.Exists(dlc)) yield break;
            foreach (var f in Directory.EnumerateFiles(dlc, "*.json", SearchOption.AllDirectories))
            {
                long len;
                try
                {
                    len = new FileInfo(f).Length;
                }
                catch
                {
                    continue;
                }
                if (len > 100_000) yield return f;
            }
        }

        // Формат каталога Addressables: ключи лежат в base64-строке m_KeyDataString, начало каждого — в m_BucketDataString.
        private static void Parse(string path, HashSet<string> set, HashSet<string> gearPrefabs)
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
            var rootEl = doc.RootElement;
            if (!rootEl.TryGetProperty("m_KeyDataString", out var kd) || !rootEl.TryGetProperty("m_BucketDataString", out var bd)) return;
            byte[] keys = Convert.FromBase64String(kd.GetString());
            byte[] buckets = Convert.FromBase64String(bd.GetString());

            // Для модов отмечаем ключи, за которыми стоит префаб (GameObject): это и есть предметы.
            byte[] entryData = null;
            int gameObjectType = -1;
            if (gearPrefabs != null && rootEl.TryGetProperty("m_EntryDataString", out var ed))
            {
                entryData = Convert.FromBase64String(ed.GetString());
                JsonElement types;
                if (rootEl.TryGetProperty("m_resourceTypes", out types) || rootEl.TryGetProperty("m_ResourceTypes", out types))
                {
                    int ti = 0;
                    foreach (var t in types.EnumerateArray())
                    {
                        if (t.TryGetProperty("m_ClassName", out var cn) && cn.GetString() == "UnityEngine.GameObject") gameObjectType = ti;
                        ti++;
                    }
                }
            }
            int count = BitConverter.ToInt32(buckets, 0);
            int off = 4;
            for (int i = 0; i < count && off + 8 <= buckets.Length; i++)
            {
                int dataOffset = BitConverter.ToInt32(buckets, off);
                int entries = BitConverter.ToInt32(buckets, off + 4);
                int first = off + 8;
                off += 8 + 4 * entries;
                string key = ReadString(keys, dataOffset);
                if (key == null) continue;
                set.Add(key);
                if (entryData == null || gameObjectType < 0 || !key.StartsWith("GEAR_", StringComparison.Ordinal) || key.IndexOfAny(new[] { '/', '.', ' ' }) >= 0) continue;
                for (int e = 0; e < entries; e++)
                {
                    int entry = BitConverter.ToInt32(buckets, first + 4 * e);
                    int typeAt = 4 + entry * 28 + 24;
                    if (typeAt + 4 <= entryData.Length && BitConverter.ToInt32(entryData, typeAt) == gameObjectType)
                    {
                        gearPrefabs.Add(key);
                        break;
                    }
                }
            }
        }

        private static string ReadString(byte[] b, int off)
        {
            if (off < 0 || off + 5 > b.Length) return null;
            byte type = b[off];
            if (type != 0 && type != 1) return null;
            int len = BitConverter.ToInt32(b, off + 1);
            if (len <= 0 || off + 5 + len > b.Length) return null;
            return type == 0 ? Encoding.ASCII.GetString(b, off + 5, len) : Encoding.Unicode.GetString(b, off + 5, len);
        }
    }
}
