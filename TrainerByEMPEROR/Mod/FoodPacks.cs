using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MelonLoader;
using MelonLoader.Utils;
using TrainerByEMPEROR.Shared;

namespace TrainerByEMPEROR
{
    // Предметы мода FoodPackByEMPEROR. Мод создаёт их прямо в игре, клонируя обычные предметы, поэтому
    // в каталогах игры их нет. Список и русские названия берём из файлов мода (Mods/FoodPackByEMPEROR/data).
    internal static class FoodPacks
    {
        private static List<ItemInfo> _items = new();
        private static DateTime _stamp;

        private static readonly Dictionary<string, string> Groups = new()
        {
            ["drink"] = "FoodPack: напитки",
            ["recipe"] = "FoodPack: блюда",
            ["can"] = "FoodPack: консервы",
            ["snack"] = "FoodPack: перекусы",
            ["pack"] = "FoodPack: упаковки",
            ["card"] = "FoodPack: карточки рецептов",
            ["cream"] = "FoodPack: ингредиенты",
            ["crafted"] = "FoodPack: блюда",
        };

        public static List<ItemInfo> Items()
        {
            string dir = Path.Combine(MelonEnvironment.ModsDirectory, "FoodPackByEMPEROR", "data");
            string itemsPath = Path.Combine(dir, "items.json");
            try
            {
                if (!File.Exists(itemsPath)) return _items = new List<ItemInfo>();
                var stamp = File.GetLastWriteTimeUtc(itemsPath);
                if (stamp == _stamp) return _items;
                _stamp = stamp;

                var names = new Dictionary<string, string>();
                string textPath = Path.Combine(dir, "text_ru.json");
                if (File.Exists(textPath))
                    names = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(textPath)) ?? names;

                var list = new List<ItemInfo>();
                using var doc = JsonDocument.Parse(File.ReadAllText(itemsPath));
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    if (!e.TryGetProperty("Id", out var idEl) || idEl.ValueKind != JsonValueKind.String) continue;
                    string id = idEl.GetString();
                    string cat = e.TryGetProperty("Category", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : "";
                    string name = names.TryGetValue($"SEAGULL_{id}_NAME", out var n) && !string.IsNullOrWhiteSpace(n) ? n : id.Replace('_', ' ');
                    if (id.EndsWith("_P", StringComparison.Ordinal) && cat == "recipe") name += L.T(" (порция)", " (portion)");
                    list.Add(new ItemInfo
                    {
                        Id = "GEAR_Seagull_" + id,
                        Name = name,
                        Group = Groups.TryGetValue(cat, out var g) ? g : "FoodPack",
                    });
                }
                MelonLogger.Msg($"Предметы: FoodPack — {list.Count} шт.");
                return _items = list;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Предметы: не удалось прочитать список FoodPack: " + e.Message);
                return _items;
            }
        }
    }
}

namespace TrainerByEMPEROR
{
    // Саке и карточки рецептов FoodPack (место из spots.json по зерну мира), а также виски O'Kerrigan и сливки — там, где лежат.
    internal static class FoodPackSpots
    {
        private sealed class Spot
        {
            public string Item;
            public List<(string Scene, float X, float Z)> Options = new();
        }

        private static List<Spot> _spots;
        private static System.DateTime _stamp;

        private static List<Spot> Load()
        {
            string dir = System.IO.Path.Combine(MelonEnvironment.ModsDirectory, "FoodPackByEMPEROR", "data");
            string path = System.IO.Path.Combine(dir, "spots.json");
            if (!System.IO.File.Exists(path)) return _spots = new List<Spot>();
            var stamp = System.IO.File.GetLastWriteTimeUtc(path);
            if (_spots != null && stamp == _stamp) return _spots;
            _stamp = stamp;
            var list = new List<Spot>();
            using (var doc = JsonDocument.Parse(System.IO.File.ReadAllText(path)))
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    var s = new Spot { Item = e.GetProperty("Item").GetString() };
                    foreach (var o in e.GetProperty("Options").EnumerateArray())
                        s.Options.Add((o.GetProperty("Scene").GetString(), (float)o.GetProperty("X").GetDouble(), (float)o.GetProperty("Z").GetDouble()));
                    list.Add(s);
                }
            // Ручные правки мест (замена Options[Option]).
            string over = System.IO.Path.Combine(dir, "spots_override.json");
            if (System.IO.File.Exists(over))
            {
                using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(over));
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    var s = list.Find(x => x.Item == e.GetProperty("Item").GetString());
                    int i = e.GetProperty("Option").GetInt32();
                    if (s == null || i < 0 || i >= s.Options.Count) continue;
                    s.Options[i] = (e.GetProperty("Scene").GetString(), (float)e.GetProperty("X").GetDouble(), (float)e.GetProperty("Z").GetDouble());
                }
            }
            return _spots = list;
        }

        // Тот же выбор места, что и в моде: FNV-1a от "<зерно мира>|<предмет>".
        private static int Chosen(int seed, string item, int count)
        {
            uint h = 2166136261;
            foreach (char c in seed.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + item)
                h = unchecked((h ^ c) * 16777619);
            return (int)(h % (uint)count);
        }

        private static bool SameScene(string scene, string spotScene) =>
            string.Equals(scene, spotScene, StringComparison.OrdinalIgnoreCase) ||
            (scene != null && scene.StartsWith(spotScene + "_", StringComparison.OrdinalIgnoreCase));

        public static void Add(List<MapMarker> list, string scene)
        {
            List<Spot> spots;
            try
            {
                spots = Load();
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Карта: места FoodPack не прочитаны: " + e.Message);
                return;
            }
            var names = new Dictionary<string, string>();
            foreach (var i in FoodPacks.Items()) names[i.Id] = i.Name;

            // Что сейчас лежит в локации (не в инвентаре и не в контейнере).
            var live = new List<(string Id, UnityEngine.Vector3 P)>();
            foreach (var g in UnityEngine.Object.FindObjectsOfType<Il2Cpp.GearItem>())
            {
                if (g == null || g.m_InPlayerInventory || g.m_InsideContainer || !g.gameObject.activeInHierarchy) continue;
                string n = g.gameObject.name ?? "";
                if (!n.StartsWith("GEAR_Seagull_Sake_", StringComparison.Ordinal) && !n.StartsWith("GEAR_Seagull_Card_", StringComparison.Ordinal)
                    && !n.StartsWith("GEAR_Seagull_IrishWhiskey", StringComparison.Ordinal) && !n.StartsWith("GEAR_Seagull_WhippedCream", StringComparison.Ordinal)) continue;
                int cut = n.IndexOfAny(new[] { '(', ' ' });
                live.Add((cut > 0 ? n.Substring(0, cut) : n, g.transform.position));
            }

            int seed = 0;
            bool haveSeed = false;
            try
            {
                var t = Il2Cpp.GameManager.m_SceneTransitionData;
                if (t != null)
                {
                    seed = t.m_GameRandomSeed;
                    haveSeed = true;
                }
            }
            catch
            {
                // без зерна — покажем только то, что лежит
            }

            var shown = new HashSet<string>();
            foreach (var (id, p) in live)
            {
                shown.Add(id);
                list.Add(new MapMarker { Type = "foodpack", Kind = Kind(id), Name = names.TryGetValue(id, out var nm) ? nm : id, X = p.x, Z = p.z });
            }
            if (!haveSeed) return;
            foreach (var s in spots)
            {
                if (s.Options.Count == 0) continue;
                string id = "GEAR_Seagull_" + s.Item;
                if (shown.Contains(id)) continue;
                var o = s.Options[Chosen(seed, s.Item, s.Options.Count)];
                if (!SameScene(scene, o.Scene)) continue;
                // Место в этой локации, но предмета там нет — значит, уже подобран.
                string name = (names.TryGetValue(id, out var nm) ? nm : s.Item) + L.T(" (уже подобрано)", " (already picked up)");
                list.Add(new MapMarker { Type = "foodpack", Kind = Kind(id), Name = name, X = o.X, Z = o.Z, Done = true });
            }
        }

        private static string Kind(string id) =>
            id.StartsWith("GEAR_Seagull_Card_", StringComparison.Ordinal) ? "card" :
            id.StartsWith("GEAR_Seagull_IrishWhiskey", StringComparison.Ordinal) ? "whiskey" :
            id.StartsWith("GEAR_Seagull_WhippedCream", StringComparison.Ordinal) ? "cream" : "sake";
    }
}
