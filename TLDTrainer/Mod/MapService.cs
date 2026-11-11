using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes;

namespace TLDTrainer
{
    public sealed class MapMarker
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public bool Done { get; set; }
        public string Kind { get; set; }

        // Имя значка на угольной карте игры (атлас NGUI), если место есть на карте игры.
        public string Sprite { get; set; }

        // Метка есть на карте самой игры (её значок или надпись).
        public bool Game { get; set; }

        // Значок карты игры, который игрок ещё не открыл (на карте игры его пока не видно).
        public bool Hidden { get; set; }
    }

    // Карта: снимок локации сверху, положение игрока, метки и телепорт.
    internal static class MapService
    {
        private const int MaxLoot = 3000;

        internal static string SceneName() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        // Улица или помещение. Локации из модов игра может не знать, поэтому если в сцене есть рельеф Unity,
        // или игрок не внутри помещения (по данным самой игры), считаем её улицей.
        internal static bool Outdoors()
        {
            try
            {
                if (GameManager.IsOutDoorsScene(SceneName())) return true;
            }
            catch
            {
                // проверим иначе
            }
            try
            {
                var terrains = Terrain.activeTerrains;
                if (terrains != null && terrains.Length > 0) return true;
            }
            catch
            {
                // нет данных о рельефе
            }
            try
            {
                var weather = GameManager.GetWeatherComponent();
                if (weather != null) return !weather.IsIndoorEnvironment();
            }
            catch
            {
                // неизвестно
            }
            return false;
        }

        public static object Position()
        {
            var t = GameManager.GetPlayerTransform();
            if (t == null) throw new InvalidOperationException("Игрок не найден");
            var cam = GameManager.GetMainCamera();
            float yaw = cam != null ? cam.transform.eulerAngles.y : t.eulerAngles.y;
            var p = t.position;
            return new { ok = true, x = p.x, y = p.y, z = p.z, yaw, scene = SceneName() };
        }

        // Ортографический снимок сверху. На улице — вся локация, в помещении — этаж вокруг игрока.
        public static object Render(bool force) => MapBuilder.Request(force);

        private static string _tracedScene;
        private static bool _trace;

        // При первом запросе меток в локации пишем в лог каждый этап — если игра упадёт, будет видно где.
        private static void Stage(string what)
        {
            if (_trace) MelonLoader.MelonLogger.Msg("Метки: " + what);
        }

        // Всё, кроме зверей, почти не меняется: собираем один раз на локацию (это тяжело — десятки тысяч объектов),
        // потом отдаём готовое. Пересобираем при смене локации, по кнопке «Обновить метки» и раз в 2 минуты.
        private static List<MapMarker> _static;
        private static string _staticScene;
        private static float _staticTime;

        public static List<MapMarker> Markers(bool refresh)
        {
            string scene = SceneName();
            if (refresh || _static == null || _staticScene != scene || Time.realtimeSinceStartup - _staticTime > 120f)
            {
                _static = StaticMarkers();
                _staticScene = scene;
                _staticTime = Time.realtimeSinceStartup;
            }
            var list = new List<MapMarker>(_static);
            Animals(list);
            return list;
        }

        private static void Animals(List<MapMarker> list)
        {
            // Живые животные
            var ais = UnityEngine.Object.FindObjectsOfType<BaseAi>();
            foreach (var ai in ais)
            {
                if (ai == null || !ai.gameObject.activeInHierarchy) continue;
                if (ai.m_CurrentMode == AiMode.Dead || ai.m_CurrentMode == AiMode.Disabled) continue;
                var (kind, name) = ai.m_AiSubType switch
                {
                    AiSubType.Wolf => ("wolf", "Волк"),
                    AiSubType.Bear => ("bear", "Медведь"),
                    AiSubType.Stag => ("deer", "Олень"),
                    AiSubType.Rabbit => ("rabbit", "Кролик"),
                    AiSubType.Moose => ("moose", "Лось"),
                    AiSubType.Cougar => ("cougar", "Пума"),
                    _ => ("bird", "Животное"),
                };
                list.Add(new MapMarker { Type = "animal", Kind = kind, Name = name, X = ai.transform.position.x, Z = ai.transform.position.z });
            }

        }

        private static List<MapMarker> StaticMarkers()
        {
            var list = new List<MapMarker>();
            string scene = SceneName();
            _trace = scene != _tracedScene;
            _tracedScene = scene;
            Stage("двери и переходы");

            // Здания, пещеры и переходы в другие локации (по сцене, в которую ведёт дверь/проход)
            var doors = LoadScene.m_LoadScenesList;
            var doorTargets = new List<(string, float, float)>();
            if (doors != null)
            {
                for (int i = 0; i < doors.Count; i++)
                {
                    var d = doors[i];
                    if (d == null) continue;
                    string target = d.m_SceneToLoad ?? "";
                    string shown = Loc(d.m_SceneLocationLocIDToShow);
                    if (string.IsNullOrWhiteSpace(shown) || shown == d.m_SceneLocationLocIDToShow) shown = SceneTitle(target);
                    var (type, kind, name) = Places.Classify(target, shown);
                    // Точка перехода — середина его зоны, а не опорная точка объекта (она бывает в стороне).
                    Vector3 dp = d.transform.position;
                    var dc = d.GetComponent<Collider>();
                    if (dc != null) dp = dc.bounds.center;
                    // Один переход часто состоит из нескольких зон рядом — оставляем одну метку.
                    bool dup = false;
                    foreach (var (t, x, z) in doorTargets)
                        if (t == target && (x - dp.x) * (x - dp.x) + (z - dp.z) * (z - dp.z) < 40f * 40f) dup = true;
                    if (dup) continue;
                    doorTargets.Add((target, dp.x, dp.z));
                    list.Add(new MapMarker { Type = type, Kind = kind, Name = name, X = dp.x, Z = dp.z });
                }
            }

            Stage("объекты карты игры");
            AddMapDetails(list);

            Stage("пещеры");
            // Помещения внутри самой локации (без загрузки отдельной сцены): здания, вагончики, пещеры.
            // Игра помечает их зонами IndoorSpaceTrigger; вид определяем по имени зоны.
            var spaces = UnityEngine.Object.FindObjectsOfType<IndoorSpaceTrigger>(true);
            foreach (var sp in spaces)
            {
                if (sp == null) continue;
                var go = sp.gameObject;
                Vector3 c = go.transform.position;
                var col = go.GetComponent<Collider>();
                if (col != null) c = col.bounds.center;
                string id = sp.m_TriggerID ?? "";
                string goName = go.name ?? "";
                string parentName = go.transform.parent != null ? go.transform.parent.name ?? "" : "";
                string shown = Loc(id);
                if (shown == id || (shown != null && shown.StartsWith("SCENE_", StringComparison.Ordinal))) shown = null;
                if (_trace) MelonLoader.MelonLogger.Msg($"  помещение: «{id}» / {goName} / {parentName} → {shown ?? "без названия"}");
                string names = $"{id} {goName} {parentName}";
                // Без подсказок в названии смотрим, что вокруг: скалы пещеры (TRN_Cave…) или постройка (STR_…).
                if (!Places.HasKindHint(names) && col != null) names += " " + Surroundings(col.bounds);
                var (type, kind, name) = Places.ClassifyIndoorSpace(names, shown);
                // Безымянные зоны без явного признака (контейнеры, вагоны, сараюшки) не показываем.
                if (type == null) continue;
                // Одно помещение часто состоит из нескольких зон, а здание уже может быть отмечено дверью или картой игры.
                if (Near(list, 0, list.Count, type, c, 25f)) continue;
                list.Add(new MapMarker { Type = type, Kind = kind, Name = name, X = c.x, Z = c.z });
            }

            Stage("туши");
            // Туши животных
            var bodies = UnityEngine.Object.FindObjectsOfType<BodyHarvest>();
            foreach (var b in bodies)
            {
                if (b == null || !b.gameObject.activeInHierarchy) continue;
                string name = null;
                try
                {
                    name = b.GetDisplayName();
                }
                catch
                {
                    // без имени
                }
                if (string.IsNullOrWhiteSpace(name)) name = "Туша";
                bool empty = false;
                try
                {
                    empty = !b.AreResourcesAvailable();
                }
                catch
                {
                    // неизвестно
                }
                Add(list, "carcass", name, b.transform.position, empty);
            }

            Stage("растения");
            // Растения для сбора
            Scan<Harvestable>(list, "plant", h => PlantName(h.gameObject.name), done: h => h.IsHarvested());

            Stage("разбираемое");
            // Ветки, мебель и прочее, что можно разобрать
            var breakdowns = BreakDown.m_BreakDownObjects;
            if (breakdowns != null)
            {
                for (int i = 0; i < breakdowns.Count; i++)
                {
                    var b = breakdowns[i];
                    if (b == null || !b.gameObject.activeInHierarchy) continue;
                    string name = null;
                    try
                    {
                        name = b.m_LocalizedDisplayName?.Text();
                    }
                    catch
                    {
                        // без имени
                    }
                    Add(list, "breakdown", string.IsNullOrWhiteSpace(name) ? "Можно разобрать" : name, b.transform.position, false);
                }
            }

            Stage("костры, кровати, верстаки");
            // Огонь, сон, мастерские, рыбалка, верёвки, каирны
            Scan<Fire>(list, "fire", f => FireName(f.gameObject.name));
            Scan<Bed>(list, "bed", _ => "Кровать");
            Scan<SnowShelter>(list, "bed", _ => "Снежное укрытие", _ => "shelter");
            Scan<WorkBench>(list, "workbench", _ => "Верстак");
            Scan<AmmoWorkBench>(list, "workbench", _ => "Станок для патронов");
            Scan<Forge>(list, "workbench", _ => "Кузница", _ => "forge");
            Scan<IceFishingHole>(list, "fishing", _ => "Лунка для рыбалки");
            Scan<RopeClimbPoint>(list, "rope", _ => "Верёвка для подъёма");
            Scan<Cairn>(list, "cairn", _ => "Каирн");

            Stage("контейнеры");
            // Контейнеры
            var containers = UnityEngine.Object.FindObjectsOfType<Container>();
            foreach (var c in containers)
            {
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                string name = null;
                try
                {
                    name = c.LocalizedDisplayName?.Text();
                }
                catch
                {
                    // без локализованного имени
                }
                if (string.IsNullOrWhiteSpace(name)) name = c.gameObject.name;
                Add(list, "container", name, c.transform.position, c.IsInspected());
            }

            Stage("саке и карточки FoodPack");
            FoodPackSpots.Add(list, scene);

            Stage("предметы");
            // Предметы, лежащие в мире
            var gear = UnityEngine.Object.FindObjectsOfType<GearItem>();
            int n = 0;
            foreach (var g in gear)
            {
                if (g == null || g.m_InPlayerInventory || !g.gameObject.activeInHierarchy) continue;
                if (++n > MaxLoot) break;
                Add(list, "loot", g.DisplayName, g.transform.position, false);
            }

            Stage($"готово, {list.Count} шт.");
            return list;
        }

        // Точки, которые игра сама показывает на своей карте (здания, пещеры, ориентиры) — в том числе ещё не открытые.
        private static void AddMapDetails(List<MapMarker> list)
        {
            int start = list.Count;
            var details = UnityEngine.Object.FindObjectsOfType<MapDetail>(true);
            foreach (var md in details)
            {
                if (md == null) continue;
                MapIcon.MapIconType iconType;
                string sprite, locId, custom;
                bool hidden;
                Vector3 p;
                try
                {
                    iconType = md.m_IconType;
                    sprite = md.m_SpriteName ?? "";
                    locId = md.m_LocID ?? "";
                    custom = md.m_CustomName;
                    p = md.GetWorldPosition();
                    // «Открыто» у игры хранится не в m_IsUnlocked (он ложный у всех) — пока показываем всё, что игра рисует значком.
                    hidden = false;
                    if (_trace) MelonLoader.MelonLogger.Msg($"  флаги: unlocked {md.m_IsUnlocked}, discovered {md.m_IsDiscovered}, surveyed {md.m_IsSurveyed}");
                }
                catch
                {
                    continue;
                }
                if (iconType == MapIcon.MapIconType.Area) continue;
                // DetailEntry — это сотни ресурсов (ветки, рогоз, саженцы, туши), которые игра показывает только в списке
                // после зарисовки углём. На карте они только мешают и грузят компьютер.
                if (iconType == MapIcon.MapIconType.DetailEntry) continue;
                string name = !string.IsNullOrWhiteSpace(custom) ? custom : Loc(locId);
                if (name == locId) name = null;
                if (_trace) MelonLoader.MelonLogger.Msg($"  на карте игры: {iconType} «{sprite}» «{locId}» → {name ?? "без названия"}{(hidden ? " (не открыто)" : "")}");
                // Надписи карты игры (названия мест) — отдельным слоем, как на угольной карте.
                if (iconType == MapIcon.MapIconType.Text)
                {
                    if (!string.IsNullOrWhiteSpace(name)) list.Add(new MapMarker { Type = "label", Kind = "label", Name = name, X = p.x, Z = p.z, Game = true });
                    continue;
                }
                var (type, kind, label) = Places.ClassifyMapDetail(sprite, locId, md.gameObject.name, name, iconType == MapIcon.MapIconType.TopIcon);
                if (type == null)
                {
                    if (string.IsNullOrEmpty(sprite)) continue;
                    (type, kind, label) = ("landmark", "landmark", name ?? "");
                }
                // Место уже отмечено по двери: берём название, значок и точку с карты игры, второй значок не нужен.
                int same = NearIndex(list, 0, start, type, p, 35f);
                if (same >= 0)
                {
                    var m = list[same];
                    if (!string.IsNullOrWhiteSpace(name)) m.Name = name;
                    m.Sprite = string.IsNullOrEmpty(sprite) ? null : sprite;
                    m.Game = true;
                    m.Hidden = hidden;
                    // Значок здания на карте игры стоит посередине здания — берём его; у переходов и пещер оставляем точку входа.
                    if (Group(m.Type) == "building")
                    {
                        m.X = p.x;
                        m.Z = p.z;
                    }
                    continue;
                }
                list.Add(new MapMarker { Type = type, Kind = kind, Name = label, X = p.x, Z = p.z, Sprite = string.IsNullOrEmpty(sprite) ? null : sprite, Game = true, Hidden = hidden });
            }
        }

        private static int NearIndex(List<MapMarker> list, int from, int to, string type, Vector3 p, float dist)
        {
            int best = -1;
            float bestD = dist * dist;
            for (int i = from; i < to; i++)
            {
                var m = list[i];
                if (Group(m.Type) != Group(type)) continue;
                float dx = m.X - p.x, dz = m.Z - p.z, d2 = dx * dx + dz * dz;
                if (d2 < bestD)
                {
                    bestD = d2;
                    best = i;
                }
            }
            return best;
        }

        private static readonly Dictionary<string, string> _sceneTitles = new();
        private static SceneNameMapping _sceneNames;
        private static bool _sceneNamesFailed;

        // Название локации, в которую ведёт дверь, как его пишет игра («Лагерь Офис», «Пещера»…), а не имя сцены.
        public static string SceneTitle(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return null;
            if (_sceneTitles.TryGetValue(scene, out var t)) return t;
            try
            {
                if (_sceneNames == null && !_sceneNamesFailed) _sceneNames = SceneNameMapping.GetRefOrLoad(null);
                string id = _sceneNames?.GetLocIDForScene(scene);
                t = Loc(id);
                if (t == id) t = null;
            }
            catch (Exception e)
            {
                _sceneNamesFailed = true;
                MelonLoader.MelonLogger.Warning("Карта: названия локаций недоступны: " + e.Message);
            }
            _sceneTitles[scene] = t;
            return t;
        }

        private static bool Near(List<MapMarker> list, int from, int to, string type, Vector3 p, float dist)
        {
            for (int i = from; i < to; i++)
            {
                var m = list[i];
                if (Group(m.Type) != Group(type)) continue;
                float dx = m.X - p.x, dz = m.Z - p.z;
                if (dx * dx + dz * dz < dist * dist) return true;
            }
            return false;
        }

        // Пещера на этой локации и вход в пещеру-локацию — одно и то же место на карте.
        private static string Group(string type) => type == "cavescene" ? "cave" : type;

        // Имена объектов в зоне помещения и их родителей: по ним понятно, пещера это или постройка.
        private static string Surroundings(Bounds b)
        {
            int cave = 0, built = 0;
            var sb = new System.Text.StringBuilder();
            try
            {
                var hits = Physics.OverlapBox(b.center, b.extents, Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits.Length && i < 60; i++)
                {
                    var h = hits[i];
                    if (h == null) continue;
                    var t = h.transform;
                    var names = new System.Text.StringBuilder();
                    for (int up = 0; up < 3 && t != null; up++, t = t.parent) names.Append(t.name).Append(' ');
                    string n = names.ToString();
                    if (n.IndexOf("Cave", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Mine", StringComparison.OrdinalIgnoreCase) >= 0) cave++;
                    else if (n.IndexOf("STR_", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Interior", StringComparison.OrdinalIgnoreCase) >= 0
                             || n.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) >= 0
                             || n.IndexOf("Roof", StringComparison.OrdinalIgnoreCase) >= 0) built++;
                    if (sb.Length < 300) sb.Append(n);
                }
            }
            catch
            {
                // без окружения
            }
            if (_trace) MelonLoader.MelonLogger.Msg($"    вокруг: пещера {cave}, постройка {built}: {sb}");
            // Возвращаем одно слово-подсказку: по большинству объектов вокруг.
            return cave > built ? "cave" : built > 0 ? "building" : "";
        }

        public static void Teleport(float x, float z)
        {
            var player = GameManager.GetPlayerTransform();
            if (player == null) throw new InvalidOperationException("Игрок не найден");
            bool outdoors = Outdoors();
            float y = player.position.y;

            // Ищем пол под точкой: на улице сверху вниз, в помещении — от уровня текущего этажа.
            if (outdoors)
                y = Ground(x, z, y);
            else if (Physics.Raycast(new Vector3(x, y + 2.0f, z), Vector3.down, out RaycastHit hit, 5000f,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                y = hit.point.y + 0.3f;

            var pos = new Vector3(x, y, z);
            Fly.NoFallDamageUntil = Time.unscaledTime + 4f;
            if (Fly.Active)
            {
                player.position = pos;
                return;
            }
            var pm = GameManager.GetPlayerManagerComponent();
            if (pm == null) throw new InvalidOperationException("Игрок не найден");
            pm.TeleportPlayer(pos, player.rotation);
        }

        // Земля на улице: самая верхняя поверхность под точкой, над которой есть место для игрока.
        // Ниже не ищем: внутри скал и холмов коллайдеры «пустые», и игрок оказывается под текстурами.
        // Кроны деревьев и ветки пропускаем, чтобы не встать на дерево.
        private static float Ground(float x, float z, float fallback)
        {
            var terrain = Terrain.activeTerrain;
            float? relief = null;
            if (terrain != null) relief = terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.GetPosition().y;
            float top = Math.Max(fallback, relief ?? fallback) + 1500f;
            var hits = Physics.RaycastAll(new Vector3(x, top, z), Vector3.down, 5000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var ys = new List<(float Y, string Name)>();
            for (int i = 0; hits != null && i < hits.Length; i++)
            {
                string name = "";
                try
                {
                    var t = hits[i].collider != null ? hits[i].collider.transform : null;
                    for (int up = 0; up < 2 && t != null; up++, t = t.parent) name += t.name + " ";
                }
                catch
                {
                    // без имени
                }
                ys.Add((hits[i].point.y, name));
            }
            ys.Sort((a, b) => b.Y.CompareTo(a.Y));
            foreach (var (hy, name) in ys)
            {
                if (name.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Branch", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("Pine", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Foliage", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                // Под рельефом не встаём никогда.
                if (relief.HasValue && hy < relief.Value - 0.5f) break;
                var feet = new Vector3(x, hy + 0.6f, z);
                var head = new Vector3(x, hy + 1.6f, z);
                if (Physics.CheckCapsule(feet, head, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                return hy + 0.3f;
            }
            if (ys.Count > 0) return Math.Max(ys[0].Y, relief ?? float.MinValue) + 0.3f;
            return (relief ?? fallback) + 0.3f;
        }

        private static void Scan<T>(List<MapMarker> list, string type, Func<T, string> name, Func<T, string> kind = null, Func<T, bool> done = null)
            where T : Component
        {
            T[] found;
            try
            {
                found = UnityEngine.Object.FindObjectsOfType<T>();
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Warning($"[карта] {typeof(T).Name}: {e.Message}");
                return;
            }
            foreach (var o in found)
            {
                if (o == null || !o.gameObject.activeInHierarchy) continue;
                try
                {
                    var p = o.transform.position;
                    list.Add(new MapMarker
                    {
                        Type = type,
                        Kind = kind?.Invoke(o) ?? type,
                        Name = name(o),
                        X = p.x,
                        Z = p.z,
                        Done = done != null && done(o),
                    });
                }
                catch
                {
                    // объект пропал во время обхода
                }
            }
        }

        private static string PlantName(string n)
        {
            n = n ?? "";
            if (n.Contains("RoseHip", StringComparison.OrdinalIgnoreCase)) return "Шиповник";
            if (n.Contains("Cattail", StringComparison.OrdinalIgnoreCase)) return "Рогоз";
            if (n.Contains("Reishi", StringComparison.OrdinalIgnoreCase)) return "Гриб рейши";
            if (n.Contains("OldMansBeard", StringComparison.OrdinalIgnoreCase) || n.Contains("Lichen", StringComparison.OrdinalIgnoreCase)) return "Бородач (лишайник)";
            if (n.Contains("Burdock", StringComparison.OrdinalIgnoreCase)) return "Лопух";
            if (n.Contains("Birch", StringComparison.OrdinalIgnoreCase)) return "Берёзовая кора";
            if (n.Contains("Acorn", StringComparison.OrdinalIgnoreCase)) return "Жёлуди";
            if (n.Contains("Mushroom", StringComparison.OrdinalIgnoreCase)) return "Грибы";
            return "Растение";
        }

        private static string FireName(string n)
        {
            n = n ?? "";
            if (n.Contains("Stove", StringComparison.OrdinalIgnoreCase)) return "Печь";
            if (n.Contains("Fireplace", StringComparison.OrdinalIgnoreCase)) return "Камин";
            if (n.Contains("Barrel", StringComparison.OrdinalIgnoreCase)) return "Бочка для огня";
            if (n.Contains("Forge", StringComparison.OrdinalIgnoreCase)) return "Кузница";
            return "Место для костра";
        }

        private static void Add(List<MapMarker> list, string type, string name, Vector3 p, bool done)
        {
            list.Add(new MapMarker { Type = type, Kind = type, Name = name ?? "", X = p.x, Z = p.z, Done = done });
        }

        internal static string Loc(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            try
            {
                return Localization.Get(key);
            }
            catch
            {
                return null;
            }
        }

    }
}
