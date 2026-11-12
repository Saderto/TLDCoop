using System;
using Il2Cpp;

namespace TrainerByEMPEROR
{
    // Определяет, что находится за дверью или проходом: здание (и какого типа), пещера или другая локация.
    internal static class Places
    {
        private static readonly (string Key, string Name)[] Regions =
        {
            ("LakeRegion", "Таинственное озеро"),
            ("CoastalRegion", "Береговое шоссе"),
            ("RuralRegion", "Тихая долина"),
            ("CrashMountainRegion", "Лес Тимбервольф"),
            ("MountainTownRegion", "Милтон"),
            ("WhalingStationRegion", "Унылый мыс"),
            ("MarshRegion", "Скорбная топь"),
            ("RavineTransitionZone", "Овраг"),
            ("HighwayTransitionZone", "Скорбная дорога"),
            ("TracksRegion", "Пути"),
            ("RiverValleyRegion", "Долина Хашман"),
            ("CanneryRegion", "Бледный берег"),
            ("AshCanyonRegion", "Пепельный каньон"),
            ("BlackrockRegion", "Чёрный камень"),
            ("AirfieldRegion", "Затерянное поле"),
            ("MiningRegion", "Зона заражения"),
            ("HubRegion", "Транзит"),
            ("MountainPassRegion", "Перевал"),
            ("LongRailTransitionZone", "Долгая дорога"),
            ("CrumblingHighwayRegion", "Разрушенное шоссе"),
            ("DamRiverTransitionZone", "Плотина"),
        };

        private static readonly (string Key, string Kind, string Name)[] Buildings =
        {
            ("Church", "church", "Церковь"),
            ("Lighthouse", "lighthouse", "Маяк"),
            ("Barn", "barn", "Амбар"),
            ("Farm", "house", "Ферма"),
            ("Lodge", "lodge", "Охотничий домик"),
            ("HuntingLodge", "lodge", "Охотничий домик"),
            ("Cabin", "cabin", "Хижина"),
            ("Hut", "cabin", "Хижина"),
            ("Shack", "cabin", "Лачуга"),
            ("Trailer", "trailer", "Трейлер"),
            ("Store", "store", "Магазин"),
            ("Shop", "store", "Магазин"),
            ("Grocery", "store", "Магазин"),
            ("Bank", "store", "Банк"),
            ("PostOffice", "store", "Почта"),
            ("Office", "office", "Офис"),
            ("GasStation", "store", "Заправка"),
            ("Garage", "garage", "Гараж"),
            ("Prison", "factory", "Тюрьма"),
            ("Dam", "factory", "Плотина"),
            ("Factory", "factory", "Завод"),
            ("Cannery", "factory", "Консервный завод"),
            ("Mill", "factory", "Лесопилка"),
            ("Warehouse", "factory", "Склад"),
            ("Station", "factory", "Станция"),
            ("School", "office", "Школа"),
            ("Lookout", "lighthouse", "Вышка"),
            ("Tower", "lighthouse", "Вышка"),
            ("Bunker", "cave", "Бункер"),
            ("Hangar", "garage", "Ангар"),
            ("Mansion", "house", "Особняк"),
            ("House", "house", "Дом"),
            ("Home", "house", "Дом"),
            ("Apartment", "house", "Квартира"),
            ("Motel", "house", "Мотель"),
            ("Hotel", "house", "Гостиница"),
        };

        // Возвращает (тип метки, вид значка, подпись).
        public static (string Type, string Kind, string Name) Classify(string target, string shownName)
        {
            string t = target ?? "";

            foreach (var (key, name) in Regions)
                if (t.StartsWith(key, StringComparison.OrdinalIgnoreCase) && IsOutdoors(t))
                    return ("exit", "exit", "Переход: " + (string.IsNullOrWhiteSpace(shownName) ? name : shownName));

            if (IsOutdoors(t))
                return ("exit", "exit", "Переход: " + (string.IsNullOrWhiteSpace(shownName) ? Pretty(t) : shownName));

            // Пещеры и шахты, которые загружаются отдельной локацией
            if (t.IndexOf("Cave", StringComparison.OrdinalIgnoreCase) >= 0)
                return ("cavescene", "cavescene", "Вход в пещеру-локацию" + (string.IsNullOrWhiteSpace(shownName) ? "" : ": " + shownName));
            if (t.IndexOf("Mine", StringComparison.OrdinalIgnoreCase) >= 0)
                return ("cavescene", "mine", "Вход в шахту-локацию" + (string.IsNullOrWhiteSpace(shownName) ? "" : ": " + shownName));

            foreach (var (key, kind, name) in Buildings)
                if (t.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return ("building", kind, string.IsNullOrWhiteSpace(shownName) ? name : shownName);

            return ("building", "house", string.IsNullOrWhiteSpace(shownName) ? "Здание" : shownName);
        }

        // Помещение внутри локации: пещера, шахта или здание (по ключевым словам в именах зоны).
        public static (string Type, string Kind, string Name) ClassifyIndoorSpace(string names, string shownName)
        {
            string n = names ?? "";
            bool has(string w) => n.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0;
            if (has("cave") || has("grotto") || has("crevice"))
                return ("cave", "cave", string.IsNullOrWhiteSpace(shownName) ? "Пещера (на этой локации)" : shownName);
            if (has("mine") && !has("mineral"))
                return ("cave", "mine", string.IsNullOrWhiteSpace(shownName) ? "Шахта (на этой локации)" : shownName);
            foreach (var (key, kind, name) in Buildings)
                if (has(key))
                    return ("building", kind, string.IsNullOrWhiteSpace(shownName) ? name : shownName);
            // Без названия от игры и без явного признака здания это обычно контейнер, вагон или навес — не здание.
            if (string.IsNullOrWhiteSpace(shownName)) return (null, null, null);
            if (has("trailer") || has("cabin") || has("hut") || has("shed"))
                return ("building", "trailer", shownName);
            return ("building", "house", shownName);
        }

        public static bool HasKindHint(string names)
        {
            string n = names ?? "";
            bool has(string w) => n.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0;
            if (has("cave") || has("grotto") || has("crevice") || (has("mine") && !has("mineral"))) return true;
            foreach (var (key, _, _) in Buildings)
                if (has(key)) return true;
            return has("trailer") || has("cabin") || has("shed") || has("hut");
        }

        // Точка с карты игры: вид по имени значка (спрайта) и объекта. null — не показываем.
        public static (string Type, string Kind, string Name) ClassifyMapDetail(string sprite, string locId, string objName, string shown, bool topIcon)
        {
            string n = $"{sprite} {locId} {objName}";
            bool has(string w) => n.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0;
            if (has("cave"))
                return ("cave", "cave", string.IsNullOrWhiteSpace(shown) ? "Пещера (на этой локации)" : shown);
            if (has("mine") && !has("mineral"))
                return ("cave", "mine", string.IsNullOrWhiteSpace(shown) ? "Шахта" : shown);
            foreach (var (key, kind, name) in Buildings)
                if (has(key))
                    return ("building", kind, string.IsNullOrWhiteSpace(shown) ? name : shown);
            if (has("trailer") || has("cabin") || has("hut") || has("shed") || has("house") || has("building"))
                return ("building", "house", string.IsNullOrWhiteSpace(shown) ? "Постройка" : shown);
            if (!topIcon && string.IsNullOrWhiteSpace(shown)) return (null, null, null);
            return ("landmark", "landmark", string.IsNullOrWhiteSpace(shown) ? "Ориентир" : shown);
        }

        private static bool IsOutdoors(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return false;
            try
            {
                return GameManager.IsOutDoorsScene(scene);
            }
            catch
            {
                return scene.EndsWith("Region", StringComparison.OrdinalIgnoreCase) || scene.EndsWith("Zone", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string Pretty(string scene) => string.IsNullOrEmpty(scene) ? "другая локация" : scene.Replace("_", " ");
    }
}
