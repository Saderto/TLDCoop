using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MelonLoader;
using MelonLoader.Utils;

namespace TrainerByEMPEROR
{
    // Предметы мода SmokesByEMPEROR (сигареты, сигары, трубки, табак, зажигалки, заправки). Мод клонирует обычные предметы,
    // в каталогах игры их нет. Код предмета — GEAR_SeagullSmoke_<Id>; список берём из иконок мода (у каждого предмета своя,
    // у горящих сигарет и эффектов их нет), русские названия — из его text_*_ru.json.
    internal static class Smokes
    {
        public const string Prefix = "GEAR_SeagullSmoke_";
        private static List<ItemInfo> _items = new();
        private static DateTime _stamp;

        private static string Group(string id)
        {
            if (id == "LighterFluid" || id == "ButaneCan") return "Табак: заправки для зажигалок";
            if (id.StartsWith("Lighter", StringComparison.Ordinal)) return "Табак: зажигалки";
            if (id.StartsWith("Pack", StringComparison.Ordinal)) return "Табак: сигареты";
            if (id.StartsWith("Tube", StringComparison.Ordinal) || id.StartsWith("Cigar", StringComparison.Ordinal) || id.StartsWith("Box", StringComparison.Ordinal)) return "Табак: сигары";
            if (id.StartsWith("Pipe", StringComparison.Ordinal) || id.StartsWith("Pouch", StringComparison.Ordinal)) return "Табак: трубки и табак";
            return "Табак: прочее";
        }

        public static List<ItemInfo> Items()
        {
            string root = Path.Combine(MelonEnvironment.ModsDirectory, "SmokesByEMPEROR");
            string icons = Path.Combine(root, "icons"), data = Path.Combine(root, "data");
            try
            {
                if (!Directory.Exists(icons)) return _items = new List<ItemInfo>();
                var stamp = Directory.GetLastWriteTimeUtc(icons);
                if (_items.Count > 0 && stamp == _stamp) return _items;
                _stamp = stamp;

                var names = new Dictionary<string, string>();
                if (Directory.Exists(data))
                    foreach (var f in Directory.GetFiles(data, "text*_ru.json"))
                    {
                        try
                        {
                            var d = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(f));
                            if (d != null) foreach (var kv in d) names[kv.Key] = kv.Value;
                        }
                        catch { /* битый файл текстов — просто без названий */ }
                    }

                var list = new List<ItemInfo>();
                foreach (var f in Directory.GetFiles(icons, "*.png"))
                {
                    string id = Path.GetFileNameWithoutExtension(f);
                    if (id.StartsWith("Lit", StringComparison.Ordinal) || id.StartsWith("hud_", StringComparison.Ordinal) || id.StartsWith("fx_", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!names.TryGetValue($"SMOKES_{id}_NAME", out var name) || string.IsNullOrWhiteSpace(name)) continue;
                    list.Add(new ItemInfo { Id = Prefix + id, Name = name, Group = Group(id) });
                }
                list.Sort((a, b) => string.CompareOrdinal(a.Group + a.Name, b.Group + b.Name));
                MelonLogger.Msg($"Предметы: SmokesByEMPEROR — {list.Count} шт.");
                return _items = list;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Предметы: не удалось прочитать список SmokesByEMPEROR: " + e.Message);
                return _items;
            }
        }
    }
}
