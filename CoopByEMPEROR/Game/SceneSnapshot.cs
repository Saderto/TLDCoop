using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Что лежит на земле в локации. Когда в локацию заходят впервые (её нет в сохранении),
    // игра раскладывает случайный лут, и у каждого он свой. Поэтому раскладку решает один:
    //  - друг зашёл в локацию — спрашивает хоста (SceneJoin);
    //  - хост в этой локации — присылает свою раскладку, друг выравнивается под неё;
    //  - хоста там нет, но локация есть в его сохранении — у друга она та же (он загрузился
    //    из этого сохранения, а изменения приходят по ходу игры), ничего не делаем;
    //  - хоста там нет, но кто-то из друзей уже присылал раскладку — хост присылает её
    //    и события в этой локации, пришедшие после неё;
    //  - никто ещё не был — друг первый, он отправляет свою раскладку хосту (хост хранит её
    //    и сам выравнивается под неё, когда придёт в эту локацию).
    // Выравнивание: свои предметы, которых нет в раскладке, убираются, недостающие создаются.
    // Совпавшие (то же имя, то же место) не трогаются, если у них то же содержимое (штуки, жидкость, порошок);
    // иначе своя вещь заменяется вещью из раскладки.
    public static class SceneSnapshot
    {
        private const float Match = 0.3f;   // метры
        // На больших картах игра держит загруженными только вещи вокруг игрока, поэтому
        // там сравниваются лишь вещи рядом и с тем, кто прислал раскладку, и с тем, кто её получил.
        private const float OutdoorRadius = 40f;

        // Хост: локации, которые есть в его сохранении (ключи m_Dict файла сохранения).
        private static readonly HashSet<string> SavedScenes = new();

        public static void RefreshSavedScenes(byte[] file)
        {
            try
            {
                var outer = SaveJson.Split(Encoding.UTF8.GetString(Lzf.Decompress(file)));
                SavedScenes.Clear();
                foreach (var kv in SaveJson.Split(SaveJson.Get(outer, "m_Dict"))) SavedScenes.Add(kv.Key);
                var scenes = SavedScenes.Where(k => k != "boot" && k != "global" && k != "info" && k != "sandbox_global" && k != "screenshot").ToList();
                MelonLogger.Msg($"[локация] в сохранении хоста локаций: {scenes.Count} ({string.Join(", ", scenes.Take(12))}{(scenes.Count > 12 ? ", …" : "")})");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[локация] список локаций сохранения: " + e.Message);
            }
        }

        private static bool Outdoors()
        {
            try { return GameManager.IsOutDoorsScene(GameManager.m_ActiveScene); } catch { return false; }
        }

        private static Vector3 MyPos()
        {
            var t = GameManager.GetPlayerTransform();
            return t != null ? t.position : Vector3.zero;
        }

        // Хост: раскладки локаций, где его сейчас нет (присланы друзьями).
        private static readonly Dictionary<string, byte[]> Stored = new();
        private static string _askedFor = "";

        // Хост: своя раскладка локации в момент ухода из неё (пока подключены друзья). Нужна другу, в чьей копии мира
        // этой локации нет: хост зашёл туда впервые уже после его входа, и игра друга разложила бы там свой лут.
        private static readonly Dictionary<string, byte[]> HostLayouts = new();
        private static readonly List<string> HostLayoutOrder = new();
        private const int MaxHostLayouts = 300;   // одна 6-12 КБ; вытеснение — только в очень долгой сессии
        // Хост: локации, где он был за эту сессию (и вытесненные из HostLayouts) — раскладку первого друга там не берём.
        private static readonly HashSet<string> HostBeen = new();

        // Хост: локации, сохранение которых подставили от друга (Game/SceneHandoff.cs). Раскладка и кусочки на тот
        // момент — для друга, в чьей копии мира этой локации нет (иначе ему ответили бы «ты первый»).
        private static readonly Dictionary<string, (byte origin, byte[] layout, List<byte[]> pieces)> Handed = new();
        private static int _leftFrame = -1;

        // Хост: друг спросил раскладку, пока хост ещё грузится в эту же локацию — ответим, когда устоимся.
        private static readonly List<(Peer peer, string scene, Vector3 at)> Waiting = new();

        // Друг: локации, которые есть в его копии мира (из присланного сохранения) или где он уже был с тех пор.
        private static readonly HashSet<string> ClientKnown = new();
        private static string _firstIn = "";      // здесь мы были первыми (хоста не было) — когда хост придёт, спросим ещё раз
        private static float _hostHereSince = -1f;

        public static void ClientCopyLoaded(string json)
        {
            ClientKnown.Clear();
            try
            {
                var outer = SaveJson.Split(json);
                foreach (var kv in SaveJson.Split(SaveJson.Get(outer, "m_Dict"))) ClientKnown.Add(kv.Key);
                MelonLogger.Msg($"[локация] в моей копии мира хоста локаций: {ClientKnown.Count}");
            }
            catch (Exception e) { MelonLogger.Warning("[локация] список локаций копии: " + e.Message); }
        }

        // Хост уходит из локации: запоминаем, что в ней лежит.
        internal static void HostLeaving()
        {
            if (Session.Mode != Mode.Host || Session.Players.Count == 0 || !GameApi.InGame() || Time.frameCount == _leftFrame) return;
            _leftFrame = Time.frameCount;
            string scene = ItemSync.AppliedScene;
            if (string.IsNullOrEmpty(scene) || scene != ItemSync.SceneKey() && scene != GameApi.Scene()) { if (string.IsNullOrEmpty(scene)) return; }
            try
            {
                var packed = Build();
                HostLayouts[scene] = packed;
                HostBeen.Add(scene);
                HostLayoutOrder.Remove(scene);
                HostLayoutOrder.Add(scene);
                while (HostLayoutOrder.Count > MaxHostLayouts) { HostLayouts.Remove(HostLayoutOrder[0]); HostLayoutOrder.RemoveAt(0); }
                ItemSync.ClearPending(scene);   // всё, что было в локации до ухода, уже в раскладке
                MelonLogger.Msg($"[локация] {scene}: ухожу, запомнил свою раскладку ({packed.Length / 1024} КБ)");
            }
            catch (Exception e) { MelonLogger.Warning("[локация] раскладка при уходе: " + e.Message); }
        }

        private struct Entry
        {
            public string Prefab, Json;
            public Vector3 Pos;
            public Quaternion Rot;
            // Что готовится в котелке или кружке-подставке (0.19.35): без этого выравнивание ставило пустой котелок,
            // а пустая подставка тут же исчезала сама, и проверка каждые 20 с снова находила расхождение.
            public string FoodPrefab, FoodJson, FoodGuid;
            // Сетевой номер вещи у приславшего (0.19.39): только в живой раскладке (сверка, ответ на вход в локацию),
            // в хранимых его нет — номера живут одну игру.
            public uint Id;
        }

        // ---------- Сборка и разбор ----------

        private static List<GearItem> LooseItems()
        {
            var list = new List<GearItem>();
            GearItem inHands = null;
            try { inHands = GameManager.GetPlayerManagerComponent()?.m_ItemInHands; } catch { }
            // Вещь на экране осмотра висит перед камерой — её не берём. Но только пока осмотр идёт: ItemSync.Inspecting
            // остаётся и после «Оставить», и такая вещь (уже снова на земле) выпадала из раскладки — у друга при
            // выравнивании под хоста появлялась вторая такая же (двойной кролик в 0.19.12), а у хоста она пропадала
            // из его раскладки, и друг терял свою.
            GearItem inspected = null;
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                if (pm != null && pm.IsInspectModeActive()) inspected = pm.GearItemBeingInspected() ?? ItemSync.Inspecting;
            }
            catch { }
            // Палки, перья и прочее, что разложили спавнеры (RadialObjectSpawner), сверяет RadialSync.
            // Здесь их не трогаем: копия такой вещи не принадлежала бы спавнеру и не исчезала бы вместе с ним.
            var radial = RadialSync.SpawnedObjects();
            foreach (var gi in UnityEngine.Object.FindObjectsOfType<GearItem>())
            {
                if (gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer || !gi.gameObject.activeInHierarchy) continue;
                if (radial.Contains(gi.gameObject.Pointer) || ItemSync.IsPersonal(gi) || FlareShots.IsFiredRound(gi)) continue;
                if (ItemSync.InPot(gi)) continue;   // еда в котелке есть только у готовящего: её несёт замена котелка
                if (AnimalSync.InAnimal(gi)) continue;   // стрела в звере: её ставит AnimalSync в кость зверя
                if (inHands != null && inHands.Pointer == gi.Pointer) continue;
                if (inspected != null && inspected.Pointer == gi.Pointer) continue;
                list.Add(gi);
            }
            return list;
        }

        // center — для кого раскладка: на большой карте в неё идут только вещи вокруг этой точки.
        private static byte[] Build(Vector3? center = null)
        {
            var ms = new MemoryStream();
            int n = 0;
            using (var z = new DeflateStream(ms, System.IO.Compression.CompressionLevel.Fastest, true))
            using (var w = new BinaryWriter(z, Encoding.UTF8))
            {
                var items = LooseItems();
                var origin = center ?? MyPos();
                bool outdoors = Outdoors();
                if (outdoors && center.HasValue)
                    items = items.Where(gi => Vector3.Distance(gi.transform.position, origin) <= OutdoorRadius + 5f).ToList();
                w.Write(outdoors);
                w.Write(origin.x); w.Write(origin.y); w.Write(origin.z);
                w.Write(items.Count);
                foreach (var gi in items)
                {
                    var t = gi.transform;
                    w.Write(ItemSync.PrefabName(gi));
                    w.Write(t.position.x); w.Write(t.position.y); w.Write(t.position.z);
                    w.Write(t.rotation.x); w.Write(t.rotation.y); w.Write(t.rotation.z); w.Write(t.rotation.w);
                    string json = "";
                    try { json = gi.SerializeToString() ?? ""; } catch { }
                    w.Write(json);
                    n++;
                }
                // Еда в котелках — отдельным хвостом после всех вещей: старые сборки его просто не читают,
                // а сохранённые раньше раскладки (без хвоста) новые читают как прежде.
                var foods = new List<(int Index, GearItem Food)>();
                for (int i = 0; i < items.Count; i++)
                {
                    try { var f = items[i].GetComponent<CookingPotItem>()?.m_GearItemBeingCooked; if (f != null) foods.Add((i, f)); } catch { }
                }
                w.Write(foods.Count);
                foreach (var (index, food) in foods)
                {
                    w.Write(index);
                    w.Write(ItemSync.PrefabName(food));
                    string fj = "";
                    try { fj = food.SerializeToString() ?? ""; } catch { }
                    w.Write(fj);
                    w.Write(ObjectGuid.MaybeGetGuidFromGameObject(food.gameObject) ?? "");
                }
                // Номера вещей — ещё одним хвостом, только в живой раскладке (center задан): по ним после выравнивания
                // подбор находит ту же вещь у другого, а не ближайшую похожую в 0,5 м.
                if (center.HasValue)
                {
                    w.Write(items.Count);
                    foreach (var gi in items)
                    {
                        uint id = 0;
                        try { id = ItemSync.IdForLayout(gi); } catch { }
                        w.Write(id);
                    }
                }
            }
            return ms.ToArray();
        }

        private static List<Entry> Parse(byte[] packed, out bool outdoors, out Vector3 origin)
        {
            var list = new List<Entry>();
            using var z = new DeflateStream(new MemoryStream(packed), CompressionMode.Decompress);
            using var r = new BinaryReader(z, Encoding.UTF8);
            outdoors = r.ReadBoolean();
            origin = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var e = new Entry { Prefab = r.ReadString() };
                e.Pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                e.Rot = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                e.Json = r.ReadString();
                list.Add(e);
            }
            try
            {
                int k = r.ReadInt32();
                for (int i = 0; i < k; i++)
                {
                    int index = r.ReadInt32();
                    string fp = r.ReadString(), fj = r.ReadString(), fg = r.ReadString();
                    if (index < 0 || index >= list.Count) continue;
                    var e = list[index];
                    e.FoodPrefab = fp; e.FoodJson = fj; e.FoodGuid = fg;
                    list[index] = e;
                }
                int ids = r.ReadInt32();
                if (ids == list.Count)
                    for (int i = 0; i < ids; i++)
                    {
                        var e = list[i];
                        e.Id = r.ReadUInt32();
                        list[i] = e;
                    }
            }
            catch (EndOfStreamException) { }   // раскладка старой сборки или хранимая: еды в котелках или номеров в ней нет
            return list;
        }

        // Выровнять свою локацию под раскладку. Возвращает (убрано, создано).
        private static (int removed, int added) Align(byte[] packed)
        {
            var remote = Parse(packed, out bool outdoors, out var origin);
            var mine = LooseItems();
            if (outdoors)
            {
                var me = MyPos();
                bool Near(Vector3 p) => Vector3.Distance(p, origin) <= OutdoorRadius && Vector3.Distance(p, me) <= OutdoorRadius;
                remote = remote.Where(e => Near(e.Pos)).ToList();
                mine = mine.Where(gi => Near(gi.transform.position)).ToList();
            }
            var used = new bool[remote.Count];
            var extra = new List<GearItem>();
            foreach (var gi in mine)
            {
                string name = ItemSync.PrefabName(gi);
                var p = gi.transform.position;
                int best = -1;
                float bestD = Match;
                for (int i = 0; i < remote.Count; i++)
                {
                    if (used[i] || remote[i].Prefab != name) continue;
                    float d = Vector3.Distance(remote[i].Pos, p);
                    if (d <= bestD) { bestD = d; best = i; }
                }
                // То же место, но другое содержимое (моды бросают число штук в коробке своим генератором) — заменяем.
                if (best >= 0 && SameContents(remote[best].Json, gi)) { used[best] = true; ItemSync.AdoptId(gi, remote[best].Id); }
                else extra.Add(gi);
            }
            int removed = 0, added = 0, cancelled = 0;
            string sceneKey = ItemSync.SceneKey();
            ItemSync.Applying = true;
            try
            {
                foreach (var gi in extra)
                {
                    ItemSync.Forget(gi);
                    FlareShots.Silence(gi.gameObject);
                    UnityEngine.Object.Destroy(gi.gameObject);
                    removed++;
                }
                for (int i = 0; i < remote.Count; i++)
                {
                    if (used[i]) continue;
                    var e = remote[i];
                    // Я сам недавно поднял такую вещь рядом, а у другого подбор её не нашёл: не кладу себе вторую, а убираю у него.
                    if (ItemSync.TakeMyRecentPick(sceneKey, e.Prefab, e.Pos))
                    {
                        ItemSync.SendPickAt(sceneKey, e.Prefab, e.Pos);
                        cancelled++;
                        MelonLogger.Msg($"[локация] {e.Prefab} ({e.Pos.x:0.0} {e.Pos.y:0.0} {e.Pos.z:0.0}): я её уже поднял, у другого подбор не дошёл — убираю у него, себе не кладу");
                        continue;
                    }
                    try
                    {
                        var gi = ItemSync.Spawn(e.Prefab, e.Json, null, false, false);
                        if (gi == null) continue;
                        gi.transform.SetPositionAndRotation(e.Pos, e.Rot);
                        gi.gameObject.SetActive(true);
                        ItemSync.AdoptId(gi, e.Id);
                        if (!string.IsNullOrEmpty(e.FoodPrefab) && !string.IsNullOrEmpty(e.FoodGuid)) ItemSync.SpawnPotFood(gi, e.FoodPrefab, e.FoodJson, e.FoodGuid);
                        ItemSync.AttachToPlacePoint(gi);
                        added++;
                        Watch.Add((gi, e.Prefab, e.Pos, e.Rot, Time.unscaledTime, 0));
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[локация] не создался {e.Prefab}: {ex.Message}");
                    }
                }
            }
            finally
            {
                ItemSync.Applying = false;
            }
            return (removed, added);
        }

        // Содержимое вещи, которое игра или моды могут разложить у каждого по-своему: число штук, жидкость, вода, порошок.
        // Состояние (износ) не сравниваем: оно идёт со временем, и раскладка, пролежавшая у хоста, заменяла бы всё подряд.
        private static readonly string[] ContentFields = { "m_StackableItemSerialized", "m_LiquidItemSerialized", "m_WaterSupplySerialized", "m_PowderItemSerialized" };

        private static bool SameContents(string remoteJson, GearItem gi)
        {
            if (string.IsNullOrEmpty(remoteJson)) return true;
            try
            {
                if (gi.m_StackableItem == null && gi.m_LiquidItem == null && gi.m_WaterSupply == null && gi.m_PowderItem == null) return true;
                string mine = gi.SerializeToString();
                if (string.IsNullOrEmpty(mine)) return true;
                var a = SaveJson.Split(remoteJson);
                var b = SaveJson.Split(mine);
                foreach (var f in ContentFields)
                    if (SaveJson.Get(a, f) != SaveJson.Get(b, f)) return false;
                return true;
            }
            catch { return true; }
        }

        private static byte[] Message(MsgType type, string scene, byte origin = 0, byte[] packed = null)
        {
            var w = Msg.New(type);
            w.Write(scene);
            if (type == MsgType.SceneState)
            {
                w.Write(origin);
                w.Write(packed.Length);
                w.Write(packed);
            }
            if (type == MsgType.SceneJoin)
            {
                var me = MyPos();
                w.Write(me.x); w.Write(me.y); w.Write(me.z);
                w.Write(ClientKnown.Contains(scene));   // локация есть в моей копии мира (или я в ней уже был)
            }
            return Msg.Done(w);
        }

        // ---------- Друг ----------

        // Каждый кадр: локация загрузилась и устоялась — спрашиваем хоста.
        // На большой карте выравниваем только вещи вокруг друга (OutdoorRadius), поэтому, пока хост в той же
        // локации, повторяем запрос, когда друг отошёл от места прошлого выравнивания.
        private const float Reask = 25f;
        private static Vector3 _askedAt;
        private static float _nextReask;

        // Добавленные при выравнивании вещи: через 3 с смотрим, на месте ли они. Книга «Растопка» на втором этаже
        // CampOffice у друга добавлялась при каждом выравнивании и всё равно числилась у него пропавшей (0.19.15).
        // Если вещь пропала, выключена или уехала дальше метра — пишем в лог и один раз ставим обратно.
        private static readonly List<(GearItem gi, string prefab, Vector3 pos, Quaternion rot, float at, int tries)> Watch = new();
        private static int _watchMoved, _watchGone;

        private static void TickWatch()
        {
            if (Watch.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = Watch.Count - 1; i >= 0; i--)
            {
                var w = Watch[i];
                if (now - w.at < 1f) continue;
                Watch.RemoveAt(i);
                try
                {
                    if (w.gi == null || w.gi.WasCollected) { _watchGone++; MelonLogger.Msg($"[локация] добавленная вещь {w.prefab} ({w.pos.x:0.0} {w.pos.y:0.0} {w.pos.z:0.0}) через 1 с уничтожена"); continue; }
                    if (w.gi.m_InPlayerInventory || w.gi.m_InsideContainer) continue;   // подобрал или положил игрок
                    if (!w.gi.gameObject.activeInHierarchy)
                    {
                        // 0.19.16: книга «Кулинария» в Конторе лагеря у друга выключалась и без броска шанса появления.
                        // Выключают вещи и компоненты «только для режима игры/сложности» (DisableObjectForGameMode,
                        // DisableObjectForXPMode, EnableObjectFor…): у копии из префаба они есть, у вещи на сцене хоста — нет.
                        // Вещь у хоста лежит, значит должна лежать и у нас: убираем такие компоненты и включаем обратно.
                        _watchGone++;
                        var names = new List<string>();
                        int removedComps = 0;
                        foreach (var comp in w.gi.GetComponentsInChildren<Component>(true))
                        {
                            if (comp == null) continue;
                            string tn = comp.GetIl2CppType().Name;
                            if (!names.Contains(tn)) names.Add(tn);
                            if (tn.StartsWith("DisableObjectFor") || tn.StartsWith("EnableObjectFor")) { UnityEngine.Object.Destroy(comp); removedComps++; }
                        }
                        string tags = "";
                        try { var moi = w.gi.GetComponent<MissionObjectIdentifier>(); if (moi != null) tags = $"; метки «{moi.m_ActivityTags}» ({moi.m_NumTags}), без имени {moi.m_IsBlankId}"; } catch { }
                        names.Add(tags);
                        bool again = w.tries < 2;
                        MelonLogger.Msg($"[локация] добавленная вещь {w.prefab} ({w.pos.x:0.0} {w.pos.y:0.0} {w.pos.z:0.0}) через 1 с выключена (родитель {(w.gi.transform.parent != null ? w.gi.transform.parent.name : "нет")}); компоненты: {string.Join(", ", names)}; убрал «только для режима»: {removedComps}" + (again ? ", включаю обратно" : ", больше не включаю"));
                        if (again)
                        {
                            w.gi.gameObject.SetActive(true);
                            Watch.Add((w.gi, w.prefab, w.pos, w.rot, now, w.tries + 1));
                        }
                        continue;
                    }
                    var p = w.gi.transform.position;
                    float d = Vector3.Distance(p, w.pos);
                    if (d <= 1f) continue;
                    _watchMoved++;
                    var rb = w.gi.GetComponent<Rigidbody>();
                    MelonLogger.Msg($"[локация] добавленная вещь {w.prefab} уехала на {d:0.0} м: ({w.pos.x:0.0} {w.pos.y:0.0} {w.pos.z:0.0}) → ({p.x:0.0} {p.y:0.0} {p.z:0.0}), тело {(rb == null ? "нет" : rb.isKinematic ? "неподвижное" : "с физикой")}, ставлю обратно");
                    if (rb != null && !rb.isKinematic) { rb.velocity = Vector3.zero; rb.isKinematic = true; }
                    w.gi.transform.SetPositionAndRotation(w.pos, w.rot);
                }
                catch (Exception e) { MelonLogger.Warning("[локация] проверка добавленных: " + e.Message); }
            }
        }

        public static void Tick()
        {
            TickWatch();
            if (Session.Mode != Mode.Client || !Session.InHostWorld || !GameApi.InGame() || !ItemSync.Settled) { if (!GameApi.InGame()) _askedFor = ""; return; }
            string scene = ItemSync.SceneKey();
            // Мы были здесь первыми, а теперь пришёл хост: когда он устоится (5 с), спрашиваем раскладку ещё раз —
            // его локация могла разложиться по-своему раньше, чем дошла наша раскладка.
            bool hostHere = Session.Players.TryGetValue(0, out var h0) && h0.Scene == GameApi.Scene() && Time.unscaledTime - h0.LastState < 5f;
            if (!hostHere) _hostHereSince = -1f;
            else if (_hostHereSince < 0f) _hostHereSince = Time.unscaledTime;
            if (scene == _askedFor && _firstIn == scene && hostHere && Time.unscaledTime - _hostHereSince > 5f)
            {
                _firstIn = "";
                _askedFor = "";
                MelonLogger.Msg($"[локация] {scene}: хост пришёл сюда после меня, спрашиваю раскладку ещё раз");
            }
            if (scene == _askedFor)
            {
                if (Time.unscaledTime < _nextReask) return;
                _nextReask = Time.unscaledTime + 5f;
                if (!Outdoors() || !Session.Players.TryGetValue(0, out var host) || host.Scene != GameApi.Scene()) return;
                if (Vector3.Distance(MyPos(), _askedAt) < Reask) return;
            }
            _askedFor = scene;
            _askedAt = MyPos();
            _nextReask = Time.unscaledTime + 5f;
            Session.SendGameEvent(Message(MsgType.SceneJoin, scene));
        }

        // После переподключения: спросить раскладку своей локации заново (за время обрыва могло что-то поменяться).
        public static void AskAgain() => _askedFor = "";

        // Последняя локация, о раскладке которой хост уже ответил.
        public static string AnsweredScene { get; internal set; } = "";

        public static void ClientReceive(MsgType type, BinaryReader r)
        {
            string scene = r.ReadString();
            if (!GameApi.InGame() || !ItemSync.Settled || scene != ItemSync.SceneKey()) return;
            AnsweredScene = scene;   // экран можно открывать (Game/LoadVeil.cs)
            ClientKnown.Add(scene);   // после этого ответа локация у нас та же, что у всех
            if (type == MsgType.SceneSaved)
            {
                MelonLogger.Msg($"[локация] {scene}: есть в сохранении хоста, раскладка и так совпадает");
                return;
            }
            if (type == MsgType.SceneFirst)
            {
                // У меня нет DLC или модов хоста (бот): в моей раскладке нет их вещей, хост под неё стёр бы свои.
                // Не отправляем — хост, когда придёт, разложит сам, а мы тогда спросим ещё раз.
                if (ContentCheck.MissingHere.Count > 0)
                {
                    _firstIn = scene;
                    MelonLogger.Msg($"[локация] {scene}: я здесь первый, но раскладку хосту не отправляю — у меня нет DLC/модов хоста");
                    return;
                }
                var packed = Build();
                Session.SendGameEvent(Message(MsgType.SceneState, scene, 2, packed));
                _firstIn = scene;
                MelonLogger.Msg($"[локация] {scene}: я здесь первый, отправил раскладку хосту ({packed.Length / 1024} КБ)");
                if (Bot.Active) Bot.Report(true, "в этой локации я первый, раскладку решаю я");
                return;
            }
            byte origin = r.ReadByte();
            var data = r.ReadBytes(r.ReadInt32());
            var (removed, added) = Align(data);
            MelonLogger.Msg($"[локация] {scene}: выровнял под {(origin == 0 ? "хоста" : "первого посетителя")}: убрано {removed}, добавлено {added}");
            if (Bot.Active && removed + added > 0) Bot.Report(true, $"выровнял предметы на земле: убрал {removed}, добавил {added}");
        }

        // ---------- Хост ----------

        public static void HostReceive(Peer peer, MsgType type, BinaryReader r)
        {
            string scene = r.ReadString();
            if (type == MsgType.SceneJoin)
            {
                var at = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                bool inCopy = r.BaseStream.Position < r.BaseStream.Length ? r.ReadBoolean() : SavedScenes.Contains(scene);
                if (GameApi.InGame() && ItemSync.Settled && scene == ItemSync.SceneKey())
                {
                    // Раскладка вокруг друга, где бы он ни был на карте: у хоста загружена вся карта.
                    peer.Send(Message(MsgType.SceneState, scene, 0, Build(at)));
                }
                else if (GameApi.InGame() && scene == ItemSync.SceneKey())
                {
                    // Хост сам только что вошёл сюда и ещё не устоялся: ответим своей раскладкой в HostOnSettled.
                    Waiting.RemoveAll(x => x.peer == peer);
                    Waiting.Add((peer, scene, at));
                    MelonLogger.Msg($"[локация] {scene}: друг спросил раскладку, а я ещё гружусь — отвечу, когда устоюсь");
                }
                else if (inCopy)
                {
                    peer.Send(Message(MsgType.SceneSaved, scene));
                }
                else if (HostLayouts.TryGetValue(scene, out var mine))
                {
                    // Хост был здесь уже после входа друга: его раскладка и события после неё.
                    peer.Send(Message(MsgType.SceneState, scene, 0, mine));
                    foreach (var raw in ItemSync.PendingFor(scene)) peer.Send(raw);
                    MelonLogger.Msg($"[локация] {scene}: у друга в копии мира этой локации нет, отправил свою раскладку с последнего ухода");
                }
                else if (Stored.TryGetValue(scene, out var stored))
                {
                    peer.Send(Message(MsgType.SceneState, scene, 1, stored));
                    foreach (var raw in ItemSync.PendingFor(scene)) peer.Send(raw);
                }
                else if (Handed.TryGetValue(scene, out var h))
                {
                    // Сохранение локации подставили от друга: раскладка до этого, кусочки до и после.
                    peer.Send(Message(MsgType.SceneState, scene, h.origin, h.layout));
                    foreach (var raw in h.pieces) peer.Send(raw);
                    foreach (var raw in ItemSync.PendingFor(scene)) peer.Send(raw);
                    MelonLogger.Msg($"[локация] {scene}: у друга в копии мира этой локации нет, отправил раскладку до передачи сохранения и события после");
                }
                else
                {
                    peer.Send(Message(MsgType.SceneFirst, scene));
                }
                return;
            }
            if (type == MsgType.SceneState)
            {
                byte origin = r.ReadByte();
                var data = r.ReadBytes(r.ReadInt32());
                // Хост здесь и уже устоялся (или знает свою раскладку) — его раскладка главнее. Если хост здесь,
                // но ещё грузится, раскладку друга берём: под неё хост выровняется в HostOnSettled.
                if (origin != 2 || HostLayouts.ContainsKey(scene) || (GameApi.InGame() && ItemSync.Settled && scene == ItemSync.SceneKey())) return;
                // У друга нет DLC или модов хоста: в его раскладке нет их вещей.
                if (ContentCheck.LacksHostContent(peer.Id))
                {
                    MelonLogger.Msg($"[локация] {scene}: раскладку друга не беру — у него нет DLC/модов хоста");
                    return;
                }
                // Локация уже есть у хоста (была в сохранении, подставлена от друга или хост в ней был): случайная
                // раскладка друга её не перепишет.
                if (SavedScenes.Contains(scene) || HostBeen.Contains(scene) || Handed.ContainsKey(scene))
                {
                    MelonLogger.Msg($"[локация] {scene}: раскладку друга не беру — эта локация у меня уже есть");
                    return;
                }
                Stored[scene] = data;
                ItemSync.ClearPending(scene);   // всё, что было до раскладки, в ней уже есть
                MelonLogger.Msg($"[локация] {scene}: сохранил раскладку от друга ({data.Length / 1024} КБ)");
            }
        }

        // Хост: проверка нашла расхождение — присылаем другу свою раскладку вокруг него.
        // Для сверки в локации без хоста (Game/SyncCheck.cs): моя раскладка вокруг точки и сообщение-раскладка «от первого посетителя».
        internal static byte[] BuildAround(Vector3 at) => Build(at);
        internal static byte[] FixMessage(string scene, byte[] packed) => Message(MsgType.SceneState, scene, 1, packed);

        public static void SendFix(Peer peer, string scene, Vector3 at)
        {
            if (Session.Mode != Mode.Host || !GameApi.InGame() || !ItemSync.Settled || scene != ItemSync.SceneKey()) return;
            try
            {
                peer.Send(Message(MsgType.SceneState, scene, 0, Build(at)));
                MelonLogger.Msg($"[проверка] {scene}: расхождение повторилось, отправил правильную раскладку");
            }
            catch (Exception e) { MelonLogger.Warning("[проверка] починка: " + e.Message); }
        }

        // Хост пришёл в локацию (до применения отложенных событий): раскладка от друга главнее.
        public static void HostOnSettled(string scene)
        {
            if (Session.Mode != Mode.Host) return;
            if (Stored.TryGetValue(scene, out var data))
            {
                Stored.Remove(scene);
                if (!HostLayouts.ContainsKey(scene))   // своя раскладка (мы здесь уже были) главнее
                {
                    var (removed, added) = Align(data);
                    MelonLogger.Msg($"[локация] {scene}: выровнял под раскладку друга: убрано {removed}, добавлено {added}");
                }
            }
            // Друзья, спросившие раскладку, пока мы грузились сюда же.
            foreach (var (peer, sc, at) in Waiting)
                if (sc == scene && !peer.Closed)
                {
                    try { peer.Send(Message(MsgType.SceneState, scene, 0, Build(at))); MelonLogger.Msg($"[локация] {scene}: устоялся, отправил раскладку другу, который ждал"); }
                    catch (Exception e) { MelonLogger.Warning("[локация] ответ ждавшему: " + e.Message); }
                }
            Waiting.Clear();
        }

        // ---------- Хранение у хоста ----------

        public static int StoredCount => Stored.Count;

        // Хосту подставили сохранение этой локации от друга (Game/SceneHandoff.cs): раскладка друга уже внутри,
        // а локация теперь есть в нашем сохранении.
        public static void HandedOver(string scene)
        {
            // Что ответили бы до передачи (см. HostReceive SceneJoin) — вызывается до ItemSync.ClearPending.
            try
            {
                byte[] layout = null;
                byte origin = 0;
                var pieces = new List<byte[]>();
                if (HostLayouts.TryGetValue(scene, out var mine)) layout = mine;
                else if (Stored.TryGetValue(scene, out var st)) { layout = st; origin = 1; }
                else if (Handed.TryGetValue(scene, out var old)) { layout = old.layout; origin = old.origin; pieces.AddRange(old.pieces); }
                if (layout != null)
                {
                    pieces.AddRange(ItemSync.PendingFor(scene));
                    Handed[scene] = (origin, layout, pieces);
                }
            }
            catch (Exception e) { MelonLogger.Warning("[локация] раскладка при передаче: " + e.Message); }
            Stored.Remove(scene);
            HostLayouts.Remove(scene);
            HostLayoutOrder.Remove(scene);
            SavedScenes.Add(scene);
        }

        public static void Write(BinaryWriter w)
        {
            w.Write(Stored.Count);
            foreach (var kv in Stored) { w.Write(kv.Key); w.Write(kv.Value.Length); w.Write(kv.Value); }
        }

        public static void Read(BinaryReader r)
        {
            Stored.Clear();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) { string k = r.ReadString(); Stored[k] = r.ReadBytes(r.ReadInt32()); }
        }

        public static void Reset()
        {
            Stored.Clear();
            SavedScenes.Clear();
            HostLayouts.Clear();
            HostLayoutOrder.Clear();
            HostBeen.Clear();
            Handed.Clear();
            Waiting.Clear();
            _askedFor = "";
            _firstIn = "";
        }
    }

    // Хост уходит из локации: пока она ещё загружена, запоминаем её раскладку (SceneSnapshot.HostLeaving).
    [HarmonyLib.HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadScene), new[] { typeof(string), typeof(string) })]
    internal static class Patch_HostLeavingScene
    {
        private static void Prefix() { try { SceneSnapshot.HostLeaving(); } catch { } }
    }

    [HarmonyLib.HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadSceneWithLoadingScreen))]
    internal static class Patch_HostLeavingSceneScreen
    {
        private static void Prefix() { try { SceneSnapshot.HostLeaving(); } catch { } }
    }
}
