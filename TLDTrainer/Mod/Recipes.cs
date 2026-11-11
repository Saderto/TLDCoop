using System;
using System.Collections.Generic;
using Il2CppTLD.Cooking;
using MelonLoader;

namespace TLDTrainer
{
    public sealed class RecipeInfo
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Ingredients { get; set; }
        public int Skill { get; set; }
        public string Dish { get; set; }
        public bool Unlocked { get; set; }
        public bool Cooked { get; set; }
    }

    // Рецепты готовки (рагу егеря, пирог ловчего и т. д.) — книга рецептов в сохранении.
    internal static class Recipes
    {
        private static bool _logged;
        private static bool _extraLogged;

        private static List<RecipeData> All()
        {
            var book = RecipeBook.Instance;
            if (book == null) throw new InvalidOperationException("Загрузите сохранение");
            var all = book.AllRecipes;
            if (all == null || all.Count == 0) throw new InvalidOperationException("Игра ещё загружает рецепты, попробуйте через пару секунд");
            var list = new List<RecipeData>();
            var seen = new HashSet<IntPtr>();
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && seen.Add(all[i].Pointer)) list.Add(all[i]);
            // Рецепты из модов (например, FoodPack) могут быть созданы в игре, но не попасть в общий список книги.
            try
            {
                var extra = new List<RecipeData>();
                var found = UnityEngine.Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<RecipeData>());
                for (int i = 0; found != null && i < found.Length; i++)
                {
                    var r = found[i]?.TryCast<RecipeData>();
                    if (r != null && seen.Add(r.Pointer)) extra.Add(r);
                }
                extra.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                list.AddRange(extra);
                if (extra.Count > 0 && !_extraLogged)
                {
                    _extraLogged = true;
                    MelonLogger.Msg($"Рецепты: вне книги найдено ещё {extra.Count}");
                }
            }
            catch (Exception e)
            {
                if (!_extraLogged) MelonLogger.Warning("Рецепты: поиск рецептов модов не удался: " + e.Message);
                _extraLogged = true;
            }
            return list;
        }

        public static List<RecipeInfo> List()
        {
            var book = RecipeBook.Instance;
            var all = All();
            var cooked = new HashSet<string>();
            try
            {
                var c = book.m_GameState?.m_CookedRecipes;
                for (int i = 0; c != null && i < c.Count; i++) cooked.Add(c[i]);
            }
            catch
            {
                // без отметок «готовили»
            }
            bool log = !_logged;
            if (log) MelonLogger.Msg($"Рецепты: всего {all.Count}");
            var list = new List<RecipeInfo>();
            for (int i = 0; i < all.Count; i++)
            {
                var r = all[i];
                if (r == null) continue;
                if (log) MelonLogger.Msg($"  рецепт {i}: {r.name}");
                string name = Text(r.m_RecipeName) ?? r.name;
                string dish = Dish(r);
                if (log) MelonLogger.Msg($"    блюдо: {dish ?? "не найдено"}");
                list.Add(new RecipeInfo
                {
                    Index = i,
                    Name = name,
                    Description = Text(r.m_RecipeDescription) ?? "",
                    Ingredients = Ingredients(r),
                    Skill = r.m_RequiredSkillLevel,
                    Dish = dish,
                    Unlocked = book.IsRecipeUnlocked(r),
                    Cooked = cooked.Contains(r.name),
                });
            }
            _logged = true;
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return list;
        }

        public static void Unlock(int index)
        {
            var all = All();
            if (index < 0 || index >= all.Count) throw new ArgumentOutOfRangeException(nameof(index));
            RecipeBook.Instance.UnlockRecipe(all[index]);
            MelonLogger.Msg($"Рецепт открыт: {all[index].name}");
        }

        public static void UnlockAll()
        {
            var book = RecipeBook.Instance;
            foreach (var r in All())
                if (r != null && !book.IsRecipeUnlocked(r)) book.UnlockRecipe(r);
            MelonLogger.Msg("Открыты все рецепты");
        }

        // Готовое блюдо: описание предмета у рецепта или результат его чертежа.
        private static string Dish(RecipeData r)
        {
            string fromData(Il2CppTLD.Gear.GearItemData d)
            {
                if (d == null || string.IsNullOrEmpty(d.name)) return null;
                return d.name.StartsWith("GearItemData_", StringComparison.Ordinal) ? "GEAR_" + d.name.Substring("GearItemData_".Length) : d.name;
            }
            try
            {
                var id = fromData(r.m_CookedGearItemData);
                if (id != null) return id;
            }
            catch
            {
                // дальше
            }
            try
            {
                var id = fromData(r.GetCookedGearItemData());
                if (id != null) return id;
            }
            catch
            {
                // дальше
            }
            try
            {
                var g = r.m_DishBlueprint?.m_CraftedResultGear;
                if (g != null && !string.IsNullOrEmpty(g.name)) return g.name;
            }
            catch
            {
                // дальше
            }
            // Рецепты FoodPack: RECIPE_Seagull_<Id>, блюдо GEAR_Seagull_<Dish> из recipes.json мода.
            const string seagull = "RECIPE_Seagull_";
            if (r.name != null && r.name.StartsWith(seagull, StringComparison.Ordinal))
            {
                string id = r.name.Substring(seagull.Length);
                return "GEAR_Seagull_" + (SeagullDishes().TryGetValue(id, out var dish) ? dish : id);
            }
            return null;
        }

        private static Dictionary<string, string> _seagullDishes;

        private static Dictionary<string, string> SeagullDishes()
        {
            if (_seagullDishes != null) return _seagullDishes;
            _seagullDishes = new Dictionary<string, string>();
            try
            {
                string path = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.ModsDirectory, "FoodPackBySeagull", "data", "recipes.json");
                if (!System.IO.File.Exists(path)) return _seagullDishes;
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
                foreach (var e in doc.RootElement.EnumerateArray())
                    if (e.TryGetProperty("Id", out var id) && e.TryGetProperty("Dish", out var dish))
                        _seagullDishes[id.GetString()] = dish.GetString();
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Рецепты: recipes.json FoodPack не прочитан: " + e.Message);
            }
            return _seagullDishes;
        }

        private static string Ingredients(RecipeData r)
        {
            var parts = new List<string>();
            try
            {
                var bp = r.m_DishBlueprint;
                if (bp == null) return "";
                var gear = bp.m_RequiredGear;
                for (int i = 0; gear != null && i < gear.Length; i++)
                {
                    var g = gear[i];
                    if (g?.m_Item == null) continue;
                    string n;
                    try
                    {
                        n = g.m_Item.DisplayName;
                    }
                    catch
                    {
                        n = g.m_Item.name;
                    }
                    parts.Add(g.m_Count > 1 ? $"{n} ×{g.m_Count}" : n);
                }
                var liquids = bp.m_RequiredLiquid;
                for (int i = 0; liquids != null && i < liquids.Length; i++)
                    if (liquids[i]?.m_Liquid != null) parts.Add(Text(liquids[i].m_Liquid.m_LiquidName) ?? liquids[i].m_Liquid.name);
                var powders = bp.m_RequiredPowder;
                for (int i = 0; powders != null && i < powders.Length; i++)
                    if (powders[i]?.m_Powder != null) parts.Add(Text(powders[i].m_Powder.m_PowderName) ?? powders[i].m_Powder.name);
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"Рецепт {r.name}: ингредиенты не прочитались: {e.Message}");
            }
            return string.Join(", ", parts);
        }

        private static string Text(Il2Cpp.LocalizedString s)
        {
            if (s == null) return null;
            try
            {
                var t = s.Text();
                return string.IsNullOrWhiteSpace(t) ? null : t;
            }
            catch
            {
                return null;
            }
        }
    }
}
