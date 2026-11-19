using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using MelonLoader.Utils;
using TrainerByEMPEROR.Shared;

namespace TrainerByEMPEROR
{
    // Предметы остальных модов *ByEMPEROR (снеки, лапша, чай и кофе, медузы, спичечные коробки, коптильня и др.).
    // Эти моды создают предметы в игре сами, в каталогах игры их нет. У всех один устрой: класс Defs с кодом
    // Prefix и списком All (у каждого элемента Id), а названия лежат в словарях ru/en класса Texts
    // под ключом <ПРЕФИКС_><Id>_NAME. Читаем это отражением, поэтому новые предметы этих модов появятся сами.
    internal static class ModItems
    {
        // Мод (имя сборки) -> группа в списке (по-русски, это же ключ; перевод — в окне трейнера).
        private static readonly Dictionary<string, string> Groups = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SnacksByEMPEROR"] = "Снеки",
            ["NoodlesByEMPEROR"] = "Лапша",
            ["TeaCoffeeByEMPEROR"] = "Чай и кофе",
            ["JellyfishByEMPEROR"] = "Медузы",
            ["MatchboxesByEMPEROR"] = "Спичечные коробки",
            ["SmokehouseByEMPEROR"] = "Коптильня и колбасы",
        };

        // Моды, у которых предметы читаются отдельно (FoodPack, табак) или их нет.
        private static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase)
        {
            "TrainerByEMPEROR", "FoodPackByEMPEROR", "SmokesByEMPEROR", "SmokesAudioOnByEMPEROR", "FoodBalanceByEMPEROR",
        };

        // Моды с одним-несколькими предметами без общего устроя: коды и названия известны.
        private static readonly (string Dll, string Id, string Ru, string En, string Group)[] Fixed =
        {
            ("MarkersByEMPEROR", "GEAR_SeagullMarker_TrailMarker", "Метка тропы", "Trail marker", "Метки"),
        };

        private static readonly HashSet<string> _ids = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _prefixes = new(StringComparer.OrdinalIgnoreCase);

        // Предмет одного из этих модов: выдаём так же, как сами моды (не консольной командой).
        public static bool IsModItem(string gear)
        {
            if (_ids.Contains(gear)) return true;
            foreach (var p in _prefixes)
                if (gear.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static List<ItemInfo> Items()
        {
            var list = new List<ItemInfo>();
            var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name;
                try
                {
                    name = asm.GetName().Name ?? "";
                }
                catch
                {
                    continue;
                }
                if (!name.EndsWith("ByEMPEROR", StringComparison.OrdinalIgnoreCase)) continue;
                loaded.Add(name);
                if (Skip.Contains(name)) continue;
                try
                {
                    int before = list.Count;
                    Read(asm, name, list);
                    if (list.Count > before && !_logged.Contains(name))
                    {
                        _logged.Add(name);
                        MelonLogger.Msg($"Предметы: {name} — {list.Count - before} шт.");
                    }
                }
                catch (Exception e)
                {
                    if (_logged.Add(name + "!")) MelonLogger.Warning($"Предметы: не удалось прочитать {name}: {e.Message}");
                }
            }

            foreach (var f in Fixed)
            {
                if (!loaded.Contains(f.Dll) && !File.Exists(Path.Combine(MelonEnvironment.ModsDirectory, f.Dll + ".dll"))) continue;
                _ids.Add(f.Id);
                list.Add(new ItemInfo { Id = f.Id, Name = L.En ? f.En : f.Ru, Group = f.Group });
            }
            return list;
        }

        private static readonly HashSet<string> _logged = new(StringComparer.OrdinalIgnoreCase);

        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static void Read(Assembly asm, string mod, List<ItemInfo> list)
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }
            var defs = types.FirstOrDefault(t => t.Name == "Defs" && t.GetField("Prefix", Static)?.FieldType == typeof(string));
            if (defs == null) return;
            string prefix = defs.GetField("Prefix", Static).GetValue(null) as string;
            if (string.IsNullOrEmpty(prefix)) return;
            object all = defs.GetField("All", Static)?.GetValue(null) ?? defs.GetProperty("All", Static)?.GetValue(null);
            if (all is not IEnumerable items) return;

            var texts = types.FirstOrDefault(t => t.Name == "Texts" && t.Namespace == defs.Namespace) ?? types.FirstOrDefault(t => t.Name == "Texts");
            var ru = texts?.GetField("ru", Static)?.GetValue(null) as Dictionary<string, string>;
            var en = texts?.GetField("en", Static)?.GetValue(null) as Dictionary<string, string>;
            var table = (L.En ? en : ru) ?? ru ?? en;
            string keyPrefix = KeyPrefix(table);
            string group = Groups.TryGetValue(mod, out var g) ? g : mod.Replace("ByEMPEROR", "");

            _prefixes.Add(prefix);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in items)
            {
                if (d == null) continue;
                string id = Member(d, "Id") as string;
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                if (Member(d, "Wip") is true) continue; // недостроенные постройки коптильни выдавать незачем
                string title = null;
                if (table != null && keyPrefix != null) table.TryGetValue(keyPrefix + id + "_NAME", out title);
                if (string.IsNullOrWhiteSpace(title)) title = Pretty(id);
                list.Add(new ItemInfo { Id = prefix + id, Name = title, Group = group });
            }
        }

        private static object Member(object o, string name)
        {
            var t = o.GetType();
            try
            {
                var f = t.GetField(name, Inst);
                if (f != null) return f.GetValue(o);
                var p = t.GetProperty(name, Inst);
                if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(o);
            }
            catch
            {
                // свойство бросило исключение — у этого предмета нет кода
            }
            return null;
        }

        // Все ключи текстов мода начинаются одинаково («SNACKS_», «TEACOFFEE_»…): берём это начало у ключа названия.
        private static string KeyPrefix(Dictionary<string, string> table)
        {
            if (table == null) return null;
            foreach (var k in table.Keys)
            {
                if (!k.EndsWith("_NAME", StringComparison.Ordinal)) continue;
                int i = k.IndexOf('_');
                if (i > 0) return k.Substring(0, i + 1);
            }
            return null;
        }

        private static string Pretty(string id)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (c == '_') c = ' ';
                else if (i > 0 && char.IsUpper(c) && (char.IsLower(id[i - 1]) || char.IsDigit(id[i - 1]))) sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }
    }
}
