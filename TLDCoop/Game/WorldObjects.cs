using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Состояние объектов мира: контейнеры, двери и шкафы (OpenClose), разобранная мебель (BreakDown),
    // сорванные растения (Harvestable), костры и печки (Fire).
    // У каждого такого объекта игры есть Serialize/Deserialize — тем же текстом игра пишет его
    // в сохранение. Кто изменил объект, тот рассылает его текст, остальные применяют.
    // Объект узнаётся по его GUID из сохранения (ObjectGuid), а если GUID нет — по имени и месту.
    //
    // Контейнер с ещё не выпавшим лутом: лут случайный, поэтому выигрывает тот, кто открыл первым,
    // остальные получают его содержимое при закрытии.
    public static class WorldObjects
    {
        public enum Kind : byte { Container = 1, Door = 2, BreakDown = 3, Harvest = 4, Fire = 5, Spawner = 6, Region = 7, Shelter = 8, Rope = 9, Lock = 10, IceHoles = 11, BigCarry = 12, Safe = 13, Heater = 14, FireClock = 15, Carcass = 16, Elevator = 17, Water = 18, Decoration = 19, Lantern = 20 }

        private static bool _applying;

        // Для локаций, которые сейчас не загружены: последнее состояние каждого объекта.
        private static readonly Dictionary<string, Dictionary<string, byte[]>> PendingByScene = new();
        // Контейнер открыт у нас — его обновление ждёт закрытия.
        private static readonly Dictionary<string, byte[]> WaitingForClose = new();
        private static readonly Dictionary<string, float> WaitingSince = new();   // когда пришла чужая версия (от открытия ящика у нас)
        internal static string SilentClose;
        internal static bool OpenedUninspected;   // открыли необысканным: при закрытии шлём в любом случае (другие узнают «обыскан»)   // ящик закрываем по отказу хоста («занято»): своё не шлём
        private static float _openedAt;
        internal static Container OpenContainer;

        private static bool Active => Session.Mode != Mode.Off && !_applying && GameApi.InGame() && Session.InSharedWorld;

        // ---------- Опознание объекта ----------

        private static int _listCleared;
        public static int ListCleared => _listCleared;

        internal static string Contents(Container c)
        {
            try
            {
                var items = new Il2CppSystem.Collections.Generic.List<GearItem>();
                c.GetItems(items);
                var names = new List<string>();
                foreach (var gi in items) if (gi != null) names.Add(ItemSync.PrefabName(gi).Replace("GEAR_", ""));
                // Ещё не созданное (игра создаст при обыске) — в скобках, чтобы «пусто» не путало.
                var g = c.m_GearToInstantiate;
                if (g != null && g.Count > 0)
                {
                    var later = new List<string>();
                    foreach (var n in g) if (!string.IsNullOrEmpty(n)) later.Add(n.Replace("GEAR_", ""));
                    if (later.Count > 0) names.Add("(при обыске: " + string.Join(", ", later) + ")");
                }
                return names.Count == 0 ? "пусто" : string.Join(", ", names);
            }
            catch { return "?"; }
        }

        public static string IdOf(Component c)
        {
            if (c == null) return "";
            // Палки и перья у туш раскладываются спавнерами, которые игра создаёт сама (у туши, у дерева),
            // и GUID у них у каждого игрока свой. Место у них одно и то же, по нему и узнаём.
            if (c.TryCast<RadialObjectSpawner>() != null) return PlaceId(c);
            try
            {
                for (var t = c.transform; t != null; t = t.parent)
                {
                    string g = ObjectGuid.MaybeGetGuidFromGameObject(t.gameObject);
                    if (!string.IsNullOrEmpty(g)) return g;
                    if (t != c.transform) break;   // только сам объект и его родитель
                }
            }
            catch { }
            return PlaceId(c);
        }

        internal static string PlaceId(Component c)
        {
            var p = c.transform.position;
            return $"@{c.gameObject.name}|{Mathf.RoundToInt(p.x * 10)}|{Mathf.RoundToInt(p.y * 10)}|{Mathf.RoundToInt(p.z * 10)}";
        }

        // Поиск по id: объектов одного типа в локации сотни, поэтому строим словарь один раз
        // на локацию и тип, а при промахе перестраиваем (объект мог появиться позже).
        private static readonly Dictionary<Type, Dictionary<string, Component>> Cache = new();
        private static string _cacheScene = "";

        internal static void ForgetCache(Type t) => Cache.Remove(t);

        internal static Container FindContainer(string id) => Find<Container>(id);

        internal static T Find<T>(string id) where T : Component
        {
            string scene = ItemSync.SceneKey();
            if (scene != _cacheScene) { Cache.Clear(); _cacheScene = scene; }
            if (Cache.TryGetValue(typeof(T), out var map) && map.TryGetValue(id, out var hit) && hit != null) return (T)hit;
            map = new Dictionary<string, Component>();
            // Спавнеры палок вдали от игрока игра выключает, а состояние им нужно и тогда: ищем и выключенные.
            // Сорванные растения (с TinyTweaks) выключены, а отрастить их надо.
            // Место появления зверей у второго игрока может быть выключено — включить его как у хозяина и надо.
            bool inactive = typeof(T) == typeof(RadialObjectSpawner) || typeof(T) == typeof(Harvestable) || typeof(T) == typeof(SpawnRegion);
            foreach (var c in UnityEngine.Object.FindObjectsOfType<T>(inactive))
                if (c != null) map[IdOf(c)] = c;
            Cache[typeof(T)] = map;
            return map.TryGetValue(id, out hit) ? (T)hit : null;
        }

        // ---------- Свои изменения ----------

        // Состояние объекта в любой локации (не обязательно текущей): растения хоста (Game/PlantRegrow.cs).
        internal static byte[] BuildFor(string scene, Kind kind, string id, string state)
        {
            var w = Msg.New(MsgType.ObjectState);
            w.Write(scene ?? "");
            w.Write((byte)kind);
            w.Write(id ?? "");
            w.Write(state ?? "");
            w.Write(-1);
            return Msg.Done(w);
        }

        // Состояние объекта одному игроку (починка расхождения, Game/SyncCheck.cs): то же сообщение, что при изменении.
        internal static byte[] Build(Kind kind, Component c, string state)
        {
            if (kind == Kind.Container && c != null) state = WithUncreated(ItemSync.SceneKey(), IdOf(c), state);
            var w = Msg.New(MsgType.ObjectState);
            w.Write(ItemSync.SceneKey());
            w.Write((byte)kind);
            w.Write(IdOf(c));
            w.Write(state ?? "");
            w.Write(Probe(kind, c));
            if (kind == Kind.Container) { bool insp = false; try { insp = ((Container)c).m_Inspected; } catch { } w.Write(insp); }
            return Msg.Done(w);
        }

        private static readonly Dictionary<string, (string state, float at)> LastDoor = new();

        public static byte[] Send(Kind kind, Component c, string state, string id = null)
        {
            if (!Active || (id == null && c == null)) return null;
            try
            {
                // Дверь: Open/Close и Interact срабатывают на одно действие — второе такое же событие не шлём.
                if (kind == Kind.Door)
                {
                    string did = id ?? IdOf(c);
                    if (LastDoor.TryGetValue(did, out var ld) && ld.state == state && Time.unscaledTime - ld.at < 1.5f) return null;
                    LastDoor[did] = (state, Time.unscaledTime);
                }
                if (kind == Kind.Container) state = WithUncreated(ItemSync.SceneKey(), id ?? IdOf(c), state);
                var w = Msg.New(MsgType.ObjectState);
                w.Write(ItemSync.SceneKey());
                w.Write((byte)kind);
                w.Write(id ?? IdOf(c));
                w.Write(state ?? "");
                w.Write(Probe(kind, c));
                // Контейнер: обыскан ли он у отправителя (дописано в конец — старые версии это просто не читают).
                if (kind == Kind.Container && c != null) { bool insp = false; try { insp = ((Container)c).m_Inspected; } catch { } w.Write(insp); }
                var raw = Msg.Done(w);
                Session.SendGameEvent(raw);
                if (kind == Kind.Harvest) PlantRegrow.Note(ItemSync.SceneKey(), id ?? IdOf(c), state);
                MelonLogger.Msg($"[объекты] отправил {KindName(kind)} {id ?? IdOf(c)} в {ItemSync.SceneKey()}" + (kind == Kind.Container ? ": " + Contents((Container)c) : ""));
                return raw;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[объекты] {kind}: {e.Message}");
                return null;
            }
        }

        // Короткая проверка состояния, чтобы получатель сравнил его со своим:
        // число вещей в контейнере, открыта ли дверь, разобран ли объект, горит ли костёр.
        private static int Probe(Kind kind, Component c)
        {
            if (c == null) return kind == Kind.BreakDown ? 1 : -1;   // разобранный объект может быть уничтожен
            try
            {
                switch (kind)
                {
                    case Kind.Container:
                        var items = new Il2CppSystem.Collections.Generic.List<GearItem>();
                        ((Container)c).GetItems(items);
                        return items.Count;
                    case Kind.Door: return ((OpenClose)c).IsOpen() ? 1 : 0;
                    case Kind.BreakDown: return c.gameObject.activeInHierarchy ? 0 : 1;
                    case Kind.Harvest: return ((Harvestable)c).IsHarvested() ? 1 : 0;
                    case Kind.Fire: return ((Fire)c).IsBurning() ? 1 : 0;
                    case Kind.Rope: return ((RopeAnchorPoint)c).m_RopeDeployed ? 1 : 0;
                    case Kind.Spawner:
                    {
                        // Полученная раскладка сначала лежит в очереди (m_PendingSpawns), выкладывается позже.
                        var sp = (RadialObjectSpawner)c;
                        return (sp.m_Spawns?.Count ?? 0) + (sp.m_PendingSpawns?.Count ?? 0);
                    }
                    case Kind.Water:   // сколько воды, в десятых литра
                        return Mathf.RoundToInt(((WaterSource)c).m_CurrentLiters.m_Units * 10f / Il2CppTLD.IntBackedUnit.ItemLiquidVolume.Liter.m_Units);
                }
            }
            catch { }
            return -1;
        }

        internal static int ProbeOf(Kind kind, Component c) => Probe(kind, c);
        internal static string DescribeProbe(Kind kind, int v) => Describe(kind, v);

        // Проверка бота у хоста: то же состояние у себя по id.
        internal static int ProbeById(Kind kind, string id, out bool found)
        {
            Component c = kind switch
            {
                Kind.Container => Find<Container>(id),
                Kind.Door => Find<OpenClose>(id),
                Kind.BreakDown => Find<BreakDown>(id),
                Kind.Harvest => Find<Harvestable>(id),
                Kind.Fire => Find<Fire>(id),
                Kind.Rope => Find<RopeAnchorPoint>(id),
                Kind.Water => Find<WaterSource>(id),
                _ => null,
            };
            found = c != null;
            return c == null ? -1 : Probe(kind, c);
        }

        private static string Describe(Kind kind, int v) => kind switch
        {
            Kind.Container => $"в контейнере {v} вещ.",
            Kind.Door => v == 1 ? "открыто" : "закрыто",
            Kind.BreakDown => v == 1 ? "разобрано" : "не разобрано",
            Kind.Harvest => v == 1 ? "сорвано" : "не сорвано",
            Kind.Fire => v == 1 ? "горит" : "не горит",
            Kind.Water => $"{v / 10f:0.#} л",
            Kind.Rope => v == 1 ? "трос висит" : "троса нет",
            Kind.Spawner => $"{v} шт.",
            Kind.Region => $"животных {v}",
            _ => v < 0 ? "?" : v.ToString(),
        };

        private static string KindName(Kind kind) => kind switch
        {
            Kind.Container => "контейнер",
            Kind.Door => "дверь/шкаф",
            Kind.BreakDown => "разборка",
            Kind.Harvest => "растение",
            Kind.Fire => "костёр",
            Kind.Spawner => "палки/камни у дерева",
            Kind.Region => "место появления животных",
            Kind.Shelter => "снежное укрытие",
            Kind.Rope => "трос",
            Kind.Lock => "замок",
            Kind.IceHoles => "лунки",
            Kind.BigCarry => "волокуша",
            Kind.Safe => "сейф",
            Kind.Heater => "обогреватель",
            Kind.FireClock => "таймер костра",
            Kind.Carcass => "туша",
            Kind.Elevator => "лифт",
            Kind.Water => "вода в бачке",
            Kind.Decoration => "починенное украшение",
            Kind.Lantern => "настенный фонарь",
            _ => kind.ToString(),
        };

        // ---------- Вещи ящика, которые у нас не создаются ----------
        // Вещь из DLC или мода, которых у нас нет (FoodPackBySeagull на префабе DLC и т. п.), игра у нас из состояния ящика
        // не создаёт. Раньше наше следующее состояние этого ящика (закрыли, взяли что-то, рюкзак смерти) уходило без неё,
        // и у всех она пропадала. Теперь помним такие вещи, как они пришли (их запись из состояния), и дописываем их
        // в каждое своё состояние этого ящика. Забываем, когда придёт новое чужое состояние ящика.
        private class Uncreated
        {
            public List<(string prefab, string json)> Items = new();   // записи из m_SerializedItems
            public List<string> Names = new();                          // ещё не созданные имена (m_GearToInstantiate)
        }

        private static readonly Dictionary<string, Uncreated> UncreatedBy = new();

        private static string UKey(string scene, string id) => scene + "|" + id;

        // Container.Deserialize (Patch_ContainerDeserialize): чужое состояние (Apply, рюкзак смерти) главнее записанного;
        // загрузка локации из своего сохранения только добавляет (в своём сохранении вещи уже может не быть).
        internal static void NoteUncreated(Container c, string text)
        {
            if (c == null || string.IsNullOrEmpty(text)) return;
            bool authoritative = _applying;
            List<(string, string)> lost = null;
            if (text.Contains("\"m_PrefabName\""))
            {
                var entries = ItemEntries(text);
                var items = new Il2CppSystem.Collections.Generic.List<GearItem>();
                c.GetItems(items);
                if (items.Count < entries.Count)
                    foreach (var e in entries) if (!ItemSync.Creatable(e.prefab)) (lost ??= new()).Add(e);
            }
            if (!authoritative && lost == null) return;
            string id = IdOf(c), key = UKey(ItemSync.SceneKey(), id);
            if (authoritative)
            {
                // Имена создаются при обыске: проверяем сразу, если этот ящик сейчас открывают (ответ хоста на вопрос).
                bool opening = (OpenContainer != null && OpenContainer.Pointer == c.Pointer) || ContainerLoot.WaitingFor(id);
                var names = opening ? UncreatableNames(c) : null;
                if (lost == null && names == null) { UncreatedBy.Remove(key); return; }
                UncreatedBy[key] = new Uncreated { Items = lost ?? new(), Names = names ?? new() };
            }
            else if (lost != null)
            {
                if (!UncreatedBy.TryGetValue(key, out var u)) UncreatedBy[key] = u = new Uncreated();
                u.Items = lost;
            }
            if (lost != null) MelonLogger.Msg($"[объекты] контейнер {id}: у меня не создались {lost.Count} вещ. ({string.Join(", ", lost.ConvertAll(e => e.Item1))}), в свои состояния ящика допишу их как есть");
        }

        // Открываем ящик (Container.BeginContainerOpen): имена, которые игра у нас не создаст при обыске, запоминаем до обыска.
        internal static void NoteUncreatedNames(Container c)
        {
            if (c == null || Session.Mode == Mode.Off || !Session.InSharedWorld) return;
            try
            {
                var names = UncreatableNames(c);
                if (names == null) return;
                string id = IdOf(c), key = UKey(ItemSync.SceneKey(), id);
                if (!UncreatedBy.TryGetValue(key, out var u)) UncreatedBy[key] = u = new Uncreated();
                u.Names = names;
                MelonLogger.Msg($"[объекты] контейнер {id}: у меня не создадутся {names.Count} вещ. ({string.Join(", ", names)}), в свои состояния ящика допишу их");
            }
            catch (Exception e) { MelonLogger.Warning("[объекты] несоздаваемые вещи: " + e.Message); }
        }

        private static List<string> UncreatableNames(Container c)
        {
            List<string> bad = null;
            var g = c.m_GearToInstantiate;
            if (g == null || g.Count == 0) return null;
            foreach (var n in g) if (!string.IsNullOrEmpty(n) && !ItemSync.Creatable(n)) (bad ??= new()).Add(n);
            return bad;
        }

        internal static int UncreatedCount(string scene, string id) => UncreatedBy.TryGetValue(UKey(scene, id), out var u) ? u.Items.Count : 0;

        internal static IEnumerable<string> UncreatedPrefabs(string scene, string id)
        {
            if (UncreatedBy.TryGetValue(UKey(scene, id), out var u))
                foreach (var e in u.Items) yield return e.prefab;
        }

        internal static void ForgetUncreated(string scene, string id) => UncreatedBy.Remove(UKey(scene, id));

        // Своё состояние ящика + вещи, которые у нас не создались (тех, что в нём уже есть под тем же именем, не дублируем).
        internal static string WithUncreated(string scene, string id, string state)
        {
            if (string.IsNullOrEmpty(state) || id == null || !UncreatedBy.TryGetValue(UKey(scene, id), out var u)) return state;
            try
            {
                if (u.Items.Count > 0)
                {
                    var have = new Dictionary<string, int>();
                    foreach (var e in ItemEntries(state)) have[e.prefab] = have.TryGetValue(e.prefab, out int n) ? n + 1 : 1;
                    var add = new List<string>();
                    foreach (var e in u.Items)
                        if (have.TryGetValue(e.prefab, out int n) && n > 0) have[e.prefab] = n - 1;
                        else add.Add(e.json);
                    state = InsertInto(state, "m_SerializedItems", add);
                }
                if (u.Names.Count > 0)
                {
                    var have = new Dictionary<string, int>();
                    foreach (var el in ArrayElements(state, "m_GearToInstantiateSerialized"))
                    {
                        string n0 = Unquote(el);
                        if (n0 != null) have[n0] = have.TryGetValue(n0, out int n) ? n + 1 : 1;
                    }
                    var add = new List<string>();
                    foreach (var name in u.Names)
                        if (have.TryGetValue(name, out int n) && n > 0) have[name] = n - 1;
                        else add.Add("\"" + name.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"");
                    state = InsertInto(state, "m_GearToInstantiateSerialized", add);
                }
            }
            catch (Exception e) { MelonLogger.Warning("[объекты] дописать несоздаваемые вещи: " + e.Message); }
            return state;
        }

        // Записи вещей из текста Container.Serialize: имя префаба и вся запись как есть.
        private static readonly System.Text.RegularExpressions.Regex PrefabField =
            new("\"m_PrefabName\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");

        private static List<(string prefab, string json)> ItemEntries(string state)
        {
            var list = new List<(string, string)>();
            foreach (var el in ArrayElements(state, "m_SerializedItems"))
            {
                if (el.Length == 0 || el[0] != '{') continue;
                // Вложенный текст вещи (m_SerializedGear) экранирован: его \"m_PrefabName\" сюда не попадает.
                var m = PrefabField.Match(el);
                if (m.Success) list.Add((m.Groups[1].Value, el));
            }
            return list;
        }

        private static string Unquote(string el) => el.Length >= 2 && el[0] == '"' && el[el.Length - 1] == '"' ? el.Substring(1, el.Length - 2) : null;

        // Элементы JSON-массива "field":[...] верхнего уровня текста (с учётом строк и вложенности).
        private static List<string> ArrayElements(string s, string field)
        {
            var list = new List<string>();
            int i = ArrayStart(s, field);
            if (i < 0 || s[i] != '[') return list;
            int depth = 0, start = -1;
            bool str = false, esc = false;
            for (i++; i < s.Length; i++)
            {
                char ch = s[i];
                if (str)
                {
                    if (esc) esc = false;
                    else if (ch == '\\') esc = true;
                    else if (ch == '"') str = false;
                    continue;
                }
                if (ch == '"') { if (depth == 0 && start < 0) start = i; str = true; continue; }
                if (ch == '{' || ch == '[') { if (depth == 0 && start < 0) start = i; depth++; continue; }
                if (ch == '}' || ch == ']')
                {
                    if (depth == 0) { if (start >= 0) list.Add(s.Substring(start, i - start).Trim()); break; }
                    depth--;
                    continue;
                }
                if (depth == 0 && ch == ',') { if (start >= 0) list.Add(s.Substring(start, i - start).Trim()); start = -1; continue; }
                if (depth == 0 && start < 0 && !char.IsWhiteSpace(ch)) start = i;
            }
            return list;
        }

        // Позиция значения поля верхнего уровня ('[' или 'n' от null), -1 — нет такого поля.
        private static int ArrayStart(string s, string field)
        {
            int k = s.IndexOf("\"" + field + "\"", StringComparison.Ordinal);
            if (k < 0) return -1;
            int i = k + field.Length + 2;
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i >= s.Length || s[i] != ':') return -1;
            for (i++; i < s.Length && char.IsWhiteSpace(s[i]); i++) { }
            return i < s.Length ? i : -1;
        }

        private static string InsertInto(string s, string field, List<string> add)
        {
            if (add.Count == 0) return s;
            int i = ArrayStart(s, field);
            if (i < 0) { MelonLogger.Warning($"[объекты] в состоянии ящика нет {field}, вещи не дописаны"); return s; }
            string joined = string.Join(",", add);
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) return s.Substring(0, i) + "[" + joined + "]" + s.Substring(i + 4);
            if (s[i] != '[') return s;
            int j = i + 1;
            while (j < s.Length && char.IsWhiteSpace(s[j])) j++;
            bool empty = j < s.Length && s[j] == ']';
            return s.Substring(0, i + 1) + joined + (empty ? "" : ",") + s.Substring(i + 1);
        }

        // ---------- Чужие изменения ----------

        public static void Receive(byte[] raw)
        {
            if (Session.InOwnWorld) return;   // в своём мире состояния мира хоста не применяем
            string scene, id;
            Kind kind;
            using (var r = Msg.Read(raw, out _))
            {
                scene = r.ReadString();
                kind = (Kind)r.ReadByte();
                id = r.ReadString();
            }
            if (kind == Kind.Harvest) { try { PlantRegrow.Note(scene, id, StateIn(raw)); } catch { } }
            try { SyncCheck.TouchObj(kind, id); } catch { }
            if (kind == Kind.Container && DeathPacks.IsPackId(id))
            {
                // Рюкзак на месте смерти: запоминаем содержимое и тогда, когда сам рюкзак у нас ещё не появился.
                try { DeathPacks.NoteContents(id, StateIn(raw)); } catch { }
            }
            bool now = GameApi.InGame() && ItemSync.Settled && scene == ItemSync.SceneKey();
            // Палки и камни у деревьев друг подбирает на ходу постоянно — в лог не пишем, лог от них разрастался.
            if (kind != Kind.Spawner && kind != Kind.Region)
                MelonLogger.Msg($"[объекты] получил {KindName(kind)} {id} в {scene}: " + (now ? "применяю" : $"откладываю (у меня {ItemSync.SceneKey()}, готово={ItemSync.Settled})"));
            if (now) Apply(raw);
            else Defer(scene, (byte)kind + id, raw);
            // Хост: ящик, разборка, растение или вода в бачке друга применены прямо сейчас, а в сохранение попадут только при
            // следующем сохранении игры — при вылете до него они применятся снова (Game/PlayerProgress.cs).
            if (now && Session.Mode == Mode.Host && (kind == Kind.Container || kind == Kind.BreakDown || kind == Kind.Harvest || kind == Kind.Water))
                PlayerProgress.NoteFriendApplied(raw);
        }

        // Отложенное состояние объекта по id (из любой локации) — забрать, чтобы его не применили второй раз.
        public static string TakePendingState(Kind kind, string id)
        {
            string key = (byte)kind + id;
            foreach (var d in PendingByScene.Values)
                if (d.TryGetValue(key, out var raw)) { d.Remove(key); PlayerProgress.NoteApplied(new[] { raw }); return StateIn(raw); }
            return null;
        }

        public static List<(string id, string state)> TakePendingStates(string scene, Kind kind)
        {
            var got = new List<(string, string)>();
            if (!PendingByScene.TryGetValue(scene, out var d)) return got;
            foreach (var key in new List<string>(d.Keys))
            {
                using var r = Msg.Read(d[key], out _);
                r.ReadString();
                if ((Kind)r.ReadByte() != kind) continue;
                string id = r.ReadString();
                got.Add((id, r.ReadString()));
                PlayerProgress.NoteApplied(new[] { d[key] });
                d.Remove(key);
            }
            return got;
        }

        private static string StateIn(byte[] raw)
        {
            using var r = Msg.Read(raw, out _);
            r.ReadString();
            r.ReadByte();
            r.ReadString();
            return r.ReadString();
        }

        private static void Defer(string scene, string key, byte[] raw)
        {
            if (!PendingByScene.TryGetValue(scene, out var d)) PendingByScene[scene] = d = new Dictionary<string, byte[]>();
            d[key] = raw;   // нужно только последнее состояние
        }

        private static void Apply(byte[] raw)
        {
            _applying = true;
            Kind kind = 0;
            string id = "";
            try
            {
                using var r = Msg.Read(raw, out _);
                r.ReadString();
                kind = (Kind)r.ReadByte();
                id = r.ReadString();
                string state = r.ReadString();
                int expect = r.BaseStream.Position < r.BaseStream.Length ? r.ReadInt32() : -1;
                bool inspected = kind == Kind.Container && r.BaseStream.Position < r.BaseStream.Length && r.ReadBoolean();
                Component found = null;
                bool deferred = false;
                switch (kind)
                {
                    case Kind.Container:
                    {
                        var c = Find<Container>(id);
                        found = c;
                        if (c == null) break;
                        // Открытый у нас контейнер ждёт закрытия. Кроме того, о котором мы сами спросили хоста
                        // (Game/ContainerLoot.cs): его содержимое приходит, пока идёт обыск, — кладём сразу.
                        bool asked = ContainerLoot.WaitingFor(id);
                        if (!asked && OpenContainer != null && OpenContainer.Pointer == c.Pointer) { WaitingForClose[id] = raw; WaitingSince[id] = Time.unscaledTime - _openedAt; deferred = true; break; }
                        // Вещь из этого ящика сейчас на экране осмотра «Возьми / Не трогай» (обыск без окна ящика): ждём решения.
                        if (!asked && _afterSearch != null && _afterSearch.Pointer == c.Pointer) { _afterSearchPending = raw; deferred = true; break; }
                        // Свои квестовые вещи в контейнере оставляем, чужие не берём.
                        var keep = new List<GearItem>();
                        var had = new Il2CppSystem.Collections.Generic.List<GearItem>();
                        c.GetItems(had);
                        foreach (var gi in had) if (ItemSync.IsPersonal(gi)) keep.Add(gi);
                        foreach (var gi in keep) c.RemoveGear(gi, false);
                        c.DestroyAllGear();
                        // Список ещё не созданных вещей (m_GearToInstantiate) Deserialize не заменяет, а дописывает
                        // (InsertRange). Хост ответил на вопрос «пусто, но в списке шоколадка», потом хост обыскал ящик и
                        // прислал уже созданную шоколадку — а у друга в списке оставалась прежняя, и при обыске игра
                        // создавала вторую. Пришедшее состояние — всё содержимое ящика: старый список очищаем.
                        try { int left = c.m_GearToInstantiate?.Count ?? 0; if (left > 0) { c.m_GearToInstantiate.Clear(); _listCleared++; } } catch { }
                        c.Deserialize(state, new Il2CppSystem.Collections.Generic.List<GearItem>());
                        // Содержимое пришло от другого — своё игра раскладывать уже не должна. Иначе при первом обыске
                        // она докладывала свой случайный лут поверх (у друга были перчатки, у хоста при обыске — ботинки).
                        try { c.m_NotPopulated = false; c.m_RolledSpawnChance = true; } catch { }
                        // Кто-то уже обыскал — для всех он обысканный, второй раз обыскивать не нужно.
                        if (inspected) { try { if (!c.m_Inspected) c.MarkAsInspected(); } catch { } }
                        var got = new Il2CppSystem.Collections.Generic.List<GearItem>();
                        c.GetItems(got);
                        foreach (var gi in got) if (ItemSync.IsPersonal(gi)) c.DestroyGear(gi);
                        foreach (var gi in keep) if (gi != null) c.AddGear(gi);
                        // Что лежит после применения — сверять с «отправил» в логе другого (вещи из модов лута
                        // могут не создаться у того, у кого такого мода нет).
                        ContainerLoot.Answered(id, c);
                        // Ответ хоста пришёл в уже открытое окно: что лежало «при открытии» — теперь это.
                        if (OpenContainer != null && OpenContainer.Pointer == c.Pointer) NoteOpenedItems(c);
                        MelonLogger.Msg($"[объекты] контейнер {id} теперь: {Contents(c)}");
                        break;
                    }
                    case Kind.Door:
                    {
                        var oc = Find<OpenClose>(id);
                        found = oc;
                        if (oc == null) break;
                        bool open = state == "1";
                        if (oc.IsOpen() != open)
                        {
                            if (open) oc.Open(false);
                            else oc.Close(false);
                        }
                        break;
                    }
                    case Kind.BreakDown:
                    {
                        var bd = Find<BreakDown>(id);
                        found = bd;
                        if (bd != null && bd.gameObject.activeInHierarchy) bd.DoBreakDown(false);
                        break;
                    }
                    case Kind.Harvest:
                    {
                        var h = Find<Harvestable>(id);
                        found = h;
                        if (h != null && state != null && state.StartsWith(PlantRegrow.Mark, StringComparison.Ordinal)) PlantRegrow.ApplyRegrow(h);   // отросло по таймеру хоста
                        else if (h != null && !h.IsHarvested()) h.Deserialize(state);
                        break;
                    }
                    case Kind.Spawner:
                    {
                        var sp = Find<RadialObjectSpawner>(id);
                        found = sp;
                        RadialSync.OnApplied(ItemSync.SceneKey(), id);
                        if (sp == null) { MelonLogger.Msg("[палки] не нашёл спавнер " + id + ": " + RadialSync.Nearest(id)); break; }
                        sp.ReleaseSpawnedObjectsToPool();
                        sp.Deserialize(state);
                        break;
                    }
                    case Kind.Region:
                    {
                        // Состояние места появления животных от хозяина животных локации: включено ли оно и объекты над ним,
                        // кто убит, когда появятся снова (Game/AnimalSync.RegionSync.cs).
                        var sr = Find<SpawnRegion>(id);
                        found = sr;
                        if (sr != null) AnimalSync.ApplyRegionLive(sr, state);
                        break;
                    }
                    case Kind.Fire:
                        found = ApplyFire(id, state);
                        break;
                    case Kind.Carcass:
                        // Туша, оставшаяся у друга в локации без хоста (Game/AnimalSync.Carry.cs).
                        AnimalSync.ApplyCarcass(id, state);
                        deferred = true;   // проверке бота сравнивать нечего
                        break;
                    default:
                        found = WorldExtra.Apply(kind, id, state);
                        break;
                }
                if (Bot.Active && !deferred && kind != Kind.Region && kind != Kind.IceHoles && kind != Kind.Shelter && kind != Kind.BigCarry) Bot.Report(kind != Kind.Fire && kind != Kind.Harvest && kind != Kind.Spawner, found == null
                    ? $"не нашёл у себя: {KindName(kind)}"
                    : Compare(kind, expect, Probe(kind, found)));
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[объекты] применить {kind} {id}: {e.Message}");
                if (Bot.Active) Bot.Report(true, $"ошибка ({KindName(kind)}): {e.Message}");
            }
            finally
            {
                _applying = false;
            }
        }

        // ---------- Костры на открытой местности ----------
        // Такой костёр игрок кладёт сам, у остальных его нет. Поэтому к состоянию костра добавляем, где он
        // лежит и как выглядит, а получатель, не найдя его у себя, создаёт такой же (с тем же GUID).

        private const char Sep = '\u0002';

        private class CampInfo { public string Name; public Vector3 Pos; public Quaternion Rot; public string Camp = ""; }

        internal static Fire ApplyFire(string id, string state)
        {
            var camp = ParseCampfire(ref state);
            var f = Find<Fire>(id);
            if (f == null && camp != null) f = CreateCampfire(id, camp);
            if (f == null) { MelonLogger.Msg("[объекты] костёр не найден: " + id); return null; }
            if (camp != null && f.m_Campfire != null && camp.Camp.Length > 0) { try { f.m_Campfire.Deserialize(camp.Camp); } catch { } }
            float before = Patch_ClaimTorch.MinutesLeft(f);
            f.Deserialize(state);
            float after = Patch_ClaimTorch.MinutesLeft(f);
            string heat = "";
            try
            {
                var hs = f.m_HeatSource;
                if (hs != null)
                {
                    // Друзья: «печка грела на 0 градусов». Горит, дрова есть, а тепло выключено — включаем, как игра при розжиге.
                    if (f.IsBurning() && after > 0f && !hs.m_TurnedOn)
                    {
                        hs.TurnOn();
                        MelonLogger.Msg($"[костёр] {id}: горит, а тепло было выключено — включил (макс. +{hs.m_MaxTempIncrease:0.#}°)");
                    }
                    heat = $", тепло +{hs.m_TempIncrease:0.#}° из +{hs.m_MaxTempIncrease:0.#}°{(hs.m_TurnedOn ? "" : " (выкл.)")}, стадия {f.m_FireState}";
                }
            }
            catch { }
            if (Mathf.Abs(after - before) >= 1f) MelonLogger.Msg($"[костёр] {id}: оставалось {before:0.#} мин, по присланному {after:0.#} мин{heat}");
            return f;
        }

        public static string FireState(Fire f)
        {
            string fire = f.Serialize();
            if (f.m_Campfire == null) return fire;
            var t = f.transform;
            var p = t.position;
            var q = t.rotation;
            string camp = "";
            try { camp = f.m_Campfire.Serialize() ?? ""; } catch { }
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            return string.Join(Sep, "CF", f.gameObject.name,
                p.x.ToString("R", ic), p.y.ToString("R", ic), p.z.ToString("R", ic),
                q.x.ToString("R", ic), q.y.ToString("R", ic), q.z.ToString("R", ic), q.w.ToString("R", ic),
                camp, fire);
        }

        private static CampInfo ParseCampfire(ref string state)
        {
            if (state == null || !state.StartsWith("CF" + Sep)) return null;
            var parts = state.Split(Sep);
            if (parts.Length < 11) return null;
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            float F(int i) => float.Parse(parts[i], ic);
            state = parts[10];
            return new CampInfo
            {
                Name = parts[1],
                Pos = new Vector3(F(2), F(3), F(4)),
                Rot = new Quaternion(F(5), F(6), F(7), F(8)),
                Camp = parts[9],
            };
        }

        private static Fire CreateCampfire(string id, CampInfo info)
        {
            try
            {
                var fm = GameManager.GetFireManagerComponent();
                var f = fm != null ? fm.InstantiateCampFire() : null;
                if (f == null) { MelonLogger.Msg("[объекты] не смог создать костёр"); return null; }
                f.transform.SetPositionAndRotation(info.Pos, info.Rot);
                f.gameObject.name = info.Name;
                if (!id.StartsWith("@")) ObjectGuid.MaybeAttachObjectGuidAddOrReplace(f.gameObject, id);
                Cache.Remove(typeof(Fire));
                MelonLogger.Msg($"[объекты] создал костёр, который разложил другой игрок: {id}");
                return f;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[объекты] создать костёр: " + e.Message);
                return null;
            }
        }

        private static string Compare(Kind kind, int expect, int mine)
        {
            if (expect < 0) return $"{KindName(kind)}: у меня {Describe(kind, mine)}";
            return expect == mine
                ? $"{KindName(kind)}: у меня так же, {Describe(kind, mine)}"
                : $"{KindName(kind)}: РАСХОЖДЕНИЕ, у тебя {Describe(kind, expect)}, у меня {Describe(kind, mine)}";
        }

        // Каждый кадр, после того как ItemSync применил свои отложенные события.
        private static string _appliedScene = "";

        public static void Tick()
        {
            if (Session.Mode == Mode.Off || !GameApi.InGame() || !ItemSync.Settled || Session.InOwnWorld) { if (!GameApi.InGame()) _appliedScene = ""; return; }
            string scene = ItemSync.SceneKey();
            if (scene != _appliedScene)
            {
                _appliedScene = scene;
                OpenContainer = null;
                WaitingForClose.Clear();
                _afterSearch = null;
                _afterSearchPending = null;
                WorldExtra.BeforePending(scene);
                DeathPacks.SpawnScene(scene);   // рюкзаки на месте смерти — до их отложенного содержимого
                if (PendingByScene.TryGetValue(scene, out var d))
                {
                    PendingByScene.Remove(scene);
                    ApplyingPending = true;
                    try { foreach (var raw in d.Values) Apply(raw); }
                    finally { ApplyingPending = false; }
                    PlayerProgress.NoteApplied(d.Values);
                    MelonLogger.Msg($"[объекты] {scene}: применено отложенных состояний {d.Count}");
                }
                WorldExtra.AfterPending(scene);
            }
            else WorldExtra.AfterPending(scene);   // замки, выпавшие позже (новая часть локации)
            TickLiveContainer();
            TickAfterSearch();
        }

        internal static bool ApplyingPending { get; private set; }

        public static void ClearPending(string scene) => PendingByScene.Remove(scene);

        // Открытый контейнер: пока его обыскивают (у остальных он «Занято»), изменения уходят каждые 2 секунды,
        // а не только при закрытии. Если игра упадёт посреди обыска, у остальных останется почти то же.
        private static IntPtr _liveContainer;
        private static string _liveState = "";
        private static float _nextLive;

        private static void TickLiveContainer()
        {
            var c = OpenContainer;
            if (c == null) { _liveContainer = IntPtr.Zero; return; }
            float now = Time.unscaledTime;
            if (c.Pointer != _liveContainer)
            {
                _liveContainer = c.Pointer;
                try { _liveState = c.Serialize() ?? ""; } catch { _liveState = ""; }
                _nextLive = now + 2f;
                return;
            }
            if (now < _nextLive) return;
            _nextLive = now + 2f;
            string id = IdOf(c);
            if (WaitingForClose.ContainsKey(id)) return;
            string st;
            try { st = c.Serialize() ?? ""; } catch { return; }
            if (st == _liveState) return;
            _liveState = st;
            Send(Kind.Container, c, st);
        }

        public static void OnContainerClosed(Container c)
        {
            OpenContainer = null;
            if (c == null) return;
            try { QuestItems.OnContainerClosed(c); } catch { }
            string id = IdOf(c);
            bool silent = SilentClose == id;
            SilentClose = null;
            string now = "";
            try { now = c.Serialize() ?? ""; } catch { }
            // Мы что-то взяли или положили: состояние отличается от того, что уже знают другие.
            bool changed = OpenedUninspected || c.Pointer != _liveContainer || now != _liveState;
            OpenedUninspected = false;
            _liveContainer = IntPtr.Zero;
            if (WaitingForClose.TryGetValue(id, out var raw))
            {
                WaitingForClose.Remove(id);
                float since = WaitingSince.TryGetValue(id, out var s) ? s : 99f;
                WaitingSince.Remove(id);
                // Пока ящик был открыт у нас, пришла чужая версия. Она главнее, если мы ничего не трогали, если хост
                // отдал ящик другому, или если она пришла в первые 2 с (прежний обыск закончился, пока мы открывали).
                // Иначе это устаревшее (ответ хоста на чужой вопрос, закрытие после отказа) — шлём своё.
                if (silent || !changed)
                {
                    Apply(raw);
                    return;
                }
                if (since < 2f)
                {
                    // Мы при этом что-то взяли или положили: кладём чужую версию и повторяем поверх неё своё, итог —
                    // всем. Раньше наше молча пропадало: взятая вещь оставалась и у нас в рюкзаке, и в ящике у всех.
                    if (!ReplayOurs(c, raw)) { Apply(raw); return; }
                    try { now = c.Serialize() ?? ""; } catch { now = ""; }
                    if (now.Length > 0) Send(Kind.Container, c, now);
                    return;
                }
                MelonLogger.Msg($"[объекты] контейнер {id}: чужая версия пришла во время нашего обыска, оставляю свою");
            }
            if (silent) { MelonLogger.Msg($"[объекты] контейнер {id} закрыт по отказу хоста, не отправляю"); return; }
            // m_NotPopulated здесь не смотрим: в 2.59 игра раскладывает ящик при загрузке локации и этот флаг не
            // снимает никогда, из-за него обысканные ящики никому не уходили. Неразложенный список вещей
            // (закрыли до конца обыска) Serialize несёт сам, «пусто» другим не уйдёт.
            if (!changed && raw == null) { MelonLogger.Msg($"[объекты] контейнер {id} закрыт без изменений, не отправляю"); return; }
            Send(Kind.Container, c, now);
        }

        // Хост: все отложенные состояния (для друга, который только что получил мир).
        public static IEnumerable<byte[]> AllPending()
        {
            foreach (var d in PendingByScene.Values)
                foreach (var raw in d.Values) yield return raw;
        }

        public static void Reset()
        {
            PendingByScene.Clear();
            WaitingForClose.Clear();
            WaitingSince.Clear();
            SilentClose = null;
            OpenContainer = null;
            OpenedItems.Clear();
            UncreatedBy.Clear();
            _appliedScene = "";
            _liveContainer = IntPtr.Zero;
            WorldExtra.Reset();
        }

        internal static bool Applying { get => _applying; set => _applying = value; }

        // Обыск ящика, в котором одна вещь: игра не открывает окно ящика, а сразу показывает вещь на экране осмотра
        // «Возьми / Не трогай» (Container.ShowItemsAfterSearch → EnterInspectGearMode). Закрытия окна ящика тогда нет,
        // и взятое так никому не уходило: друг забрал вяленое мясо, а у хоста оно лежало в ящике (лут «двоился»).
        // Запоминаем ящик и его содержимое до решения, а когда осмотр закончился — рассылаем, если что-то изменилось.
        private static Container _afterSearch;
        private static string _afterSearchState;
        private static byte[] _afterSearchPending;
        private static float _afterSearchAt, _afterSearchIdle;
        private static int _afterSearchSent;
        public static int AfterSearchSent => _afterSearchSent;

        internal static void OnShowItemsAfterSearch(Container c)
        {
            if (c == null || Session.Mode == Mode.Off || Session.InOwnWorld) return;
            try
            {
                var items = new Il2CppSystem.Collections.Generic.List<GearItem>();
                c.GetItems(items);
                if (items.Count == 0) return;   // пустой ящик: показывать нечего
                _afterSearch = c;
                _afterSearchAt = Time.unscaledTime;
                _afterSearchIdle = 0f;
                _afterSearchSawInspect = false;
                _afterSearchPending = null;
                try { _afterSearchState = c.Serialize() ?? ""; } catch { _afterSearchState = ""; }
                NoteOpenedItems(c);
                MelonLogger.Msg($"[объекты] ящик {IdOf(c)}: после обыска вещь показана на осмотре (без окна ящика), жду решения: {Contents(c)}");
            }
            catch (Exception e) { MelonLogger.Warning("[объекты] обыск без окна: " + e.Message); }
        }

        // Пока решение не принято: в 0.19.14 конец осмотра ловился по IsInspectModeActive, но после ShowItemsAfterSearch
        // осмотр включается не сразу — через 0.6 с ящик считался «ничего не взял», и взятое потом не уходило (лут
        // по-прежнему двоился). Теперь решение ловим по самим кнопкам игры: OnPickupFromContainerInspection («Возьми»)
        // и OnPutBackFromContainerInspection («Не трогай»). Здесь — только запасной выход: осмотр был и закончился
        // (выход без кнопки), или прошло 2 минуты.
        private static bool _afterSearchSawInspect;

        private static void TickAfterSearch()
        {
            var c = _afterSearch;
            if (c == null) return;
            // Открылось обычное окно ящика — его закрытие разошлёт всё само.
            if (OpenContainer != null && OpenContainer.Pointer == c.Pointer) { _afterSearch = null; _afterSearchPending = null; return; }
            bool inspecting = false;
            try { var pm = GameManager.GetPlayerManagerComponent(); inspecting = pm != null && pm.IsInspectModeActive(); } catch { }
            float now = Time.unscaledTime;
            if (inspecting) { _afterSearchSawInspect = true; _afterSearchIdle = 0f; return; }
            bool timeout = now - _afterSearchAt > 120f;
            // Осмотр так и не включился (ящик пуст — игра только пишет «пусто»): не держим чужие версии ящика.
            if (!_afterSearchSawInspect && now - _afterSearchAt > 5f) { FinishAfterSearch(c, false, "осмотра не было"); return; }
            if (!_afterSearchSawInspect && !timeout) return;
            if (!timeout)
            {
                if (_afterSearchIdle == 0f) { _afterSearchIdle = now; return; }
                if (now - _afterSearchIdle < 1f) return;
            }
            FinishAfterSearch(c, false, timeout ? "осмотр так и не закончился за 2 мин" : "осмотр закончился без кнопки");
        }

        // Кнопка на экране осмотра вещи из ящика. c — ящик, из которого вещь (PlayerManager.m_Container).
        internal static void OnContainerInspectChoice(Container c, bool took)
        {
            if (Session.Mode == Mode.Off || Session.InOwnWorld) return;
            if (c == null) c = _afterSearch;
            if (c == null) return;
            FinishAfterSearch(c, took, took ? "«Возьми»" : "«Не трогай»");
        }

        private static void FinishAfterSearch(Container c, bool took, string why)
        {
            bool ours = _afterSearch != null && _afterSearch.Pointer == c.Pointer;
            string before = ours ? _afterSearchState : null;
            var pending = ours ? _afterSearchPending : null;
            _afterSearch = null;
            _afterSearchPending = null;
            _afterSearchSawInspect = false;
            _afterSearchIdle = 0f;
            try
            {
                if (c == null || c.WasCollected) return;
                string id = IdOf(c);
                string state = "";
                try { state = c.Serialize() ?? ""; } catch { }
                bool changed = took || before == null || state != before;
                if (!changed)
                {
                    MelonLogger.Msg($"[объекты] ящик {id}: {why}, с экрана осмотра ничего не взял");
                    if (pending != null) Apply(pending);
                    return;
                }
                if (pending != null)
                {
                    // Пока решали, пришла чужая версия ящика: кладём её и повторяем поверх наше взятое.
                    if (!ReplayOurs(c, pending)) { Apply(pending); return; }
                    try { state = c.Serialize() ?? ""; } catch { state = ""; }
                    if (state.Length == 0) return;
                }
                Send(Kind.Container, c, state);
                _afterSearchSent++;
                MelonLogger.Msg($"[объекты] ящик {id}: {why} на экране осмотра после обыска, разослал: {Contents(c)}");
            }
            catch (Exception e) { MelonLogger.Warning("[объекты] обыск без окна: " + e.Message); }
        }

        // Окно ящика открылось: запоминаем его содержимое сразу, без ожидания кадра (с ним сравниваем при закрытии).
        internal static void NoteOpened()
        {
            _openedAt = Time.unscaledTime;
            var c = OpenContainer;
            if (c == null) { OpenedItems.Clear(); return; }
            _liveContainer = c.Pointer;
            try { _liveState = c.Serialize() ?? ""; } catch { _liveState = ""; }
            _nextLive = _openedAt + 2f;
            NoteOpenedItems(c);
        }

        // Вещи в ящике при открытии: по ним при закрытии видно, что мы взяли и что положили.
        private static readonly List<(IntPtr ptr, string prefab, int units)> OpenedItems = new();

        private static int UnitsOf(GearItem gi) => gi.m_StackableItem != null ? Math.Max(1, gi.m_StackableItem.m_Units) : 1;

        private static void NoteOpenedItems(Container c)
        {
            OpenedItems.Clear();
            try
            {
                var items = new Il2CppSystem.Collections.Generic.List<GearItem>();
                c.GetItems(items);
                foreach (var gi in items) if (gi != null) OpenedItems.Add((gi.Pointer, ItemSync.PrefabName(gi), UnitsOf(gi)));
            }
            catch { OpenedItems.Clear(); }
        }

        // Чужая версия ящика пришла рано, а мы в нём что-то меняли: кладём её и повторяем поверх наше —
        // взятое (по имени вещи) убираем, положенное оставляем. Часть пачки (3 патрона из 10, спички в чужую
        // пачку) — по числу штук в пачке, оставшейся в ящике. false — повторять нечего или не вышло.
        private static bool ReplayOurs(Container c, byte[] raw)
        {
            var put = new List<GearItem>();
            var taken = new List<string>();
            var less = new List<(string prefab, int n)>();
            var more = new List<(string prefab, int n, string json)>();
            try
            {
                var cur = new Il2CppSystem.Collections.Generic.List<GearItem>();
                c.GetItems(cur);
                var had = new Dictionary<IntPtr, int>();
                foreach (var o in OpenedItems) had[o.ptr] = o.units;
                var now = new HashSet<IntPtr>();
                foreach (var gi in cur)
                {
                    if (gi == null) continue;
                    now.Add(gi.Pointer);
                    if (!had.TryGetValue(gi.Pointer, out int was)) { put.Add(gi); continue; }
                    int u = UnitsOf(gi);
                    if (u < was) less.Add((ItemSync.PrefabName(gi), was - u));
                    else if (u > was) more.Add((ItemSync.PrefabName(gi), u - was, gi.SerializeToString() ?? ""));
                }
                foreach (var o in OpenedItems) if (!now.Contains(o.ptr)) taken.Add(o.prefab);
                if (put.Count == 0 && taken.Count == 0 && less.Count == 0 && more.Count == 0) return false;
                foreach (var gi in put) c.RemoveGear(gi, false);   // иначе Apply уничтожит их вместе со старым содержимым
            }
            catch (Exception e) { MelonLogger.Warning("[объекты] своё поверх чужого: " + e.Message); return false; }
            Apply(raw);
            _applying = true;
            try
            {
                var got = new Il2CppSystem.Collections.Generic.List<GearItem>();
                c.GetItems(got);
                var left = new List<GearItem>();
                foreach (var gi in got) if (gi != null) left.Add(gi);
                foreach (string prefab in taken)
                {
                    int i = left.FindIndex(gi => gi != null && ItemSync.PrefabName(gi) == prefab);
                    if (i < 0) continue;   // у другого этой вещи уже нет (взял и он)
                    c.DestroyGear(left[i]);
                    left.RemoveAt(i);
                }
                foreach (var (prefab, n) in less)
                {
                    int i = left.FindIndex(gi => gi != null && gi.m_StackableItem != null && ItemSync.PrefabName(gi) == prefab);
                    if (i < 0) continue;
                    var st = left[i].m_StackableItem;
                    if (st.m_Units > n) st.m_Units -= n;
                    else { c.DestroyGear(left[i]); left.RemoveAt(i); }
                }
                foreach (var (prefab, n, json) in more)
                {
                    var stack = left.Find(gi => gi != null && gi.m_StackableItem != null && ItemSync.PrefabName(gi) == prefab);
                    if (stack != null) { stack.m_StackableItem.m_Units += n; continue; }
                    // У другого такой пачки нет: кладём наши штуки отдельной пачкой.
                    if (json.Length == 0) continue;
                    var gi2 = ItemSync.Spawn(prefab, json, null, false, true);
                    if (gi2 == null) continue;
                    if (gi2.m_StackableItem != null) gi2.m_StackableItem.m_Units = n;
                    c.AddGear(gi2);
                }
                foreach (var gi in put) if (gi != null) c.AddGear(gi);
                MelonLogger.Msg($"[объекты] контейнер {IdOf(c)}: чужая версия пришла во время нашего обыска, повторил поверх своё (взял {taken.Count}+{less.Count} частью, положил {put.Count}+{more.Count} в пачки)");
            }
            catch (Exception e) { MelonLogger.Warning("[объекты] своё поверх чужого: " + e.Message); }
            finally { _applying = false; }
            return true;
        }

        internal static void ApplyNow(byte[] raw) => Apply(raw);
        internal static RadialObjectSpawner FindSpawner(string id) => Find<RadialObjectSpawner>(id);

        // Сцена, вид и id из сообщения ObjectState.
        internal static (string scene, Kind kind, string id) Header(byte[] raw)
        {
            using var r = Msg.Read(raw, out _);
            return (r.ReadString(), (Kind)r.ReadByte(), r.ReadString());
        }
    }

    // ---------- Патчи ----------

    [HarmonyPatch(typeof(Panel_Container), nameof(Panel_Container.Enable),
        new[] { typeof(bool), typeof(bool), typeof(Il2CppSystem.Action) })]
    internal static class Patch_ContainerPanel
    {
        private static void Prefix(Panel_Container __instance, bool enable)
        {
            try
            {
                if (enable) return;
                // На закрытии m_Container может быть уже очищен: берём тот, что запомнили при открытии.
                var c = WorldObjects.OpenContainer;
                if (c == null) return;   // окно и так было закрыто
                MelonLogger.Msg("[объекты] окно контейнера закрывается: " + WorldObjects.IdOf(c));
                WorldObjects.OnContainerClosed(c);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[объекты] контейнер: " + e.Message);
            }
        }

        private static void Postfix(Panel_Container __instance, bool enable)
        {
            if (!enable) return;
            WorldObjects.OpenContainer = __instance.m_Container;
            WorldObjects.NoteOpened();
            try { QuestItems.OnContainerOpened(__instance.m_Container); } catch { }
            MelonLogger.Msg("[объекты] окно контейнера открыто: " + (__instance.m_Container != null ? WorldObjects.IdOf(__instance.m_Container) : "контейнер неизвестен"));
        }
    }

    // Что из состояния ящика у нас не создалось (вещи DLC и модов, которых у нас нет): WorldObjects.NoteUncreated.
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.OnPickupFromContainerInspection))]
    internal static class Patch_ContainerInspectTake
    {
        private static void Prefix(PlayerManager __instance, out Container __state) { __state = null; try { __state = __instance.m_Container; } catch { } }
        private static void Postfix(Container __state) { try { WorldObjects.OnContainerInspectChoice(__state, true); } catch { } }
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.OnPutBackFromContainerInspection))]
    internal static class Patch_ContainerInspectPutBack
    {
        private static void Prefix(PlayerManager __instance, out Container __state) { __state = null; try { __state = __instance.m_Container; } catch { } }
        private static void Postfix(Container __state) { try { WorldObjects.OnContainerInspectChoice(__state, false); } catch { } }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.ShowItemsAfterSearch))]
    internal static class Patch_ContainerItemsAfterSearch
    {
        private static void Prefix(Container __instance)
        {
            try { WorldObjects.OnShowItemsAfterSearch(__instance); } catch { }
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.Deserialize))]
    internal static class Patch_ContainerDeserialize
    {
        private static void Postfix(Container __instance, string text)
        {
            if (Session.Mode == Mode.Off) return;
            try { WorldObjects.NoteUncreated(__instance, text); }
            catch (Exception e) { MelonLogger.Warning("[объекты] несоздаваемые вещи: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(OpenClose), nameof(OpenClose.Open), new[] { typeof(bool), typeof(bool) })]
    internal static class Patch_DoorOpen
    {
        private static void Postfix(OpenClose __instance, bool fromLink)
        {
            if (!fromLink) WorldObjects.Send(WorldObjects.Kind.Door, __instance, "1");
        }
    }

    // Шкафы и двери открываются взаимодействием; после него отправляем, что получилось.
    [HarmonyPatch(typeof(OpenClose), nameof(OpenClose.PerformInteraction))]
    internal static class Patch_DoorInteract
    {
        private static void Postfix(OpenClose __instance)
        {
            MelonLogger.Msg("[объекты] взаимодействие с дверью/шкафом, открыто=" + __instance.IsOpen());
            WorldObjects.Send(WorldObjects.Kind.Door, __instance, __instance.IsOpen() ? "1" : "0");
        }
    }

    [HarmonyPatch(typeof(OpenClose), nameof(OpenClose.Close), new[] { typeof(bool), typeof(bool) })]
    internal static class Patch_DoorClose
    {
        private static void Postfix(OpenClose __instance, bool fromLink)
        {
            if (!fromLink) WorldObjects.Send(WorldObjects.Kind.Door, __instance, "0");
        }
    }

    [HarmonyPatch(typeof(BreakDown), nameof(BreakDown.DoBreakDown))]
    internal static class Patch_BreakDown
    {
        // id запоминаем до разборки: после неё объекта может уже не быть.
        private static void Prefix(BreakDown __instance, out string __state)
        {
            __state = null;
            try { if (!WorldObjects.Applying) __state = WorldObjects.IdOf(__instance); } catch { }
        }

        private static void Postfix(BreakDown __instance, string __state)
        {
            if (__state != null) WorldObjects.Send(WorldObjects.Kind.BreakDown, __instance, "", __state);
        }
    }

    [HarmonyPatch(typeof(Harvestable), nameof(Harvestable.Harvest))]
    internal static class Patch_Harvest
    {
        private static void Postfix(Harvestable __instance) => WorldObjects.Send(WorldObjects.Kind.Harvest, __instance, __instance.Serialize());
    }

    [HarmonyPatch(typeof(Fire), nameof(Fire.AddFuel))]
    internal static class Patch_FireFuel
    {
        private static void Postfix(Fire __instance) => WorldObjects.Send(WorldObjects.Kind.Fire, __instance, WorldObjects.FireState(__instance));
    }

    // Игрок разжёг костёр: игра идёт через Fire.PlayerBeginCreate → ExitFireStarting(success), а не через StartFireLit.
    // Раньше разжигание не отправлялось совсем — у друга костёр появлялся только с минутным таймером хозяина локации.
    [HarmonyPatch(typeof(Fire), nameof(Fire.ExitFireStarting))]
    internal static class Patch_FireStarted
    {
        private static void Postfix(Fire __instance, bool success)
        {
            if (!success || WorldObjects.Applying) return;
            try { if (__instance.IsBurning()) WorldObjects.Send(WorldObjects.Kind.Fire, __instance, WorldObjects.FireState(__instance)); } catch { }
        }
    }

    [HarmonyPatch(typeof(Fire), nameof(Fire.StartFireLit))]
    internal static class Patch_FireLit
    {
        private static void Postfix(Fire __instance) => WorldObjects.Send(WorldObjects.Kind.Fire, __instance, WorldObjects.FireState(__instance));
    }

    [HarmonyPatch(typeof(Fire), nameof(Fire.TurnOff))]
    internal static class Patch_FireOff
    {
        private static void Postfix(Fire __instance) => WorldObjects.Send(WorldObjects.Kind.Fire, __instance, WorldObjects.FireState(__instance));
    }
}
