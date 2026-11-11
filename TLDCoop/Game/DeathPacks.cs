using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TLDCoop.Game
{
    // Рюкзак на месте смерти (правило «вещи останутся на месте смерти»). Раньше вещи вываливались на землю
    // кучей по одной — десятки предметов в снегу, часть проваливалась в склон. Теперь всё складывается
    // в один рюкзак (контейнер игры CONTAINER_BackPack), который видят все: открыть, забрать своё или чужое.
    //   • появляется у всех там же (сообщение Feature/1, с номером — дойдёт и тому, кто зайдёт позже);
    //   • что в нём лежит — обычная синхронизация контейнеров (Game/WorldObjects.cs, по его GUID);
    //   • опустел — исчезает у всех (Feature/2);
    //   • хранится в слоте сохранения (Game/SlotStore.cs): игра сама такие контейнеры не сохраняет.
    // Если рюкзак не загрузился, вещи выкладываются на землю, как раньше.
    // Префаб грузится без ожидания (WaitForCompletion в кадре мог повесить игру после загрузки локации):
    // загрузку запускаем заранее, чужие рюкзаки до готовности откладываем на следующие кадры (Deferred).
    public static class DeathPacks
    {
        private const string PrefabKey = "CONTAINER_BackPack";
        public const string GuidPrefix = "tldcoop_pack_";

        private sealed class Pack
        {
            public string Id, Scene, Owner = "", Contents = "";
            public Vector3 Pos;
            public float Yaw;
            public GameObject Go;
            public Container C;
            public float SpawnedAt;
            public float NetAt = -100f;   // когда пришло последнее состояние от другого
            public bool Filled;
        }

        private static readonly Dictionary<string, Pack> Packs = new();
        private static GameObject _prefab;
        private static bool _prefabFailed;
        private static UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject> _prefabH;
        private static bool _prefabStarted;
        private static float _prefabSince;
        private const float PrefabTimeout = 60f;
        private static readonly List<string> Deferred = new();   // рюкзаки, ждущие префаба

        static DeathPacks()
        {
            SlotStore.Register("pack", SaveLines, LoadLines);
        }

        public static void Init() { }   // чтобы статический конструктор записался в SlotStore до первой загрузки

        public static bool IsPackId(string id) => id != null && id.StartsWith(GuidPrefix, StringComparison.Ordinal);

        // Запас воды игрока (WaterSupplyPotable / NotPotable) — служебные вещи: в окне контейнера их не видно и
        // забрать нельзя. В 0.18.0 они попадали в рюкзак, и он казался пустым, но не исчезал.
        public static bool IsWaterSupply(GearItem gi)
        {
            if (gi == null) return false;
            try { if (gi.m_WaterSupply != null && gi.m_LiquidItem == null) return true; } catch { }
            try { return gi.name.Contains("WaterSupply"); } catch { return false; }
        }

        // Префаб ещё грузится: «нет префаба» сейчас значит «подожди».
        public static bool PrefabPending => _prefabStarted && _prefab == null && !_prefabFailed;

        // Готовый префаб или null (ещё грузится или не загрузился). Ничего не ждём.
        private static GameObject Prefab()
        {
            if (_prefab != null || _prefabFailed) return _prefab;
            StartPrefab();
            PollPrefab();
            return _prefab;
        }

        private static void StartPrefab()
        {
            if (_prefabStarted || _prefab != null || _prefabFailed) return;
            _prefabStarted = true;
            _prefabSince = Time.unscaledTime;
            try
            {
                var key = new Il2CppSystem.Object(IL2CPP.ManagedStringToIl2Cpp(PrefabKey));
                _prefabH = Addressables.LoadAssetAsync<GameObject>(key);
            }
            catch (Exception e) { MelonLogger.Warning("[рюкзак] не загрузился: " + e.Message); PrefabFail(); }
        }

        // Забрать готовый префаб, если загрузка кончилась.
        private static void PollPrefab()
        {
            if (!_prefabStarted || _prefab != null || _prefabFailed) return;
            bool done;
            try { done = _prefabH.IsDone; } catch { done = true; }
            if (!done)
            {
                if (Time.unscaledTime - _prefabSince < PrefabTimeout) return;
                MelonLogger.Warning($"[рюкзак] префаб не загрузился за {PrefabTimeout:0} с");
                PrefabFail();
                return;
            }
            try
            {
                if (_prefabH.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded) _prefab = _prefabH.Result;
            }
            catch (Exception e) { MelonLogger.Warning("[рюкзак] не загрузился: " + e.Message); }
            if (_prefab == null) PrefabFail();
            else MelonLogger.Msg($"[рюкзак] префаб загружен за {Time.unscaledTime - _prefabSince:0.0} с");
        }

        private static void PrefabFail()
        {
            _prefabFailed = true;
            Deferred.Clear();
            MelonLogger.Warning("[рюкзак] нет префаба " + PrefabKey + ", вещи лягут на землю");
        }

        // Префаб готов: создаём отложенные рюкзаки. Не в этой локации — появятся в SpawnScene, когда в неё придём.
        private static void SpawnDeferred()
        {
            var ids = new List<string>(Deferred);
            Deferred.Clear();
            int n = 0;
            foreach (var id in ids)
                if (Packs.TryGetValue(id, out var p) && p.Go == null && Here(p) && Spawn(p)) n++;
            if (n > 0) MelonLogger.Msg($"[рюкзак] создал отложенных рюкзаков: {n}");
        }

        // ---------- Свой рюкзак при смерти (Game/Death.cs) ----------

        private static Pack _own;

        // Создать свой рюкзак на месте смерти. null — не вышло, выкладывать по-старому.
        public static Container BeginOwn(Vector3 pos, float yaw)
        {
            _own = null;
            if (Session.Mode == Mode.Off || !Session.InSharedWorld) return null;
            var p = new Pack
            {
                Id = GuidPrefix + Session.LocalId + "_" + Guid.NewGuid().ToString("N").Substring(0, 10),
                Scene = ItemSync.SceneKey(),
                Owner = Config.Name ?? "",
                Pos = Ground(pos),
                Yaw = yaw,
            };
            // Свой рюкзак Death.cs наполняет сразу: отложить нельзя, без готового префаба — на землю, как раньше.
            if (Prefab() == null)
            {
                if (PrefabPending) MelonLogger.Warning("[рюкзак] префаб ещё грузится, вещи лягут на землю");
                return null;
            }
            if (!Spawn(p)) return null;
            p.Filled = true;   // вещи кладём сами
            Packs[p.Id] = p;
            _own = p;
            MelonLogger.Msg($"[рюкзак] свой рюкзак {p.Id} в {p.Scene} {p.Pos}");
            return p.C;
        }

        // Всё сложили — рассказать остальным.
        public static void FinishOwn()
        {
            var p = _own;
            _own = null;
            if (p == null || p.C == null) return;
            try { p.Contents = p.C.Serialize() ?? ""; } catch { }
            var w = Msg.New(MsgType.Feature);
            w.Write(Features.PackAdd);
            w.Write(p.Id);
            w.Write(p.Scene);
            w.Write(p.Pos.x); w.Write(p.Pos.y); w.Write(p.Pos.z);
            w.Write(p.Yaw);
            w.Write(p.Owner);
            w.Write(p.Contents);
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg($"[рюкзак] разослал {p.Id}: {WorldObjects.Contents(p.C)}");
        }

        private static Vector3 Ground(Vector3 pos)
        {
            try
            {
                if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)) return hit.point + Vector3.up * 0.02f;
            }
            catch { }
            return pos;
        }

        // ---------- Чужие ----------

        public static void ReceiveAdd(BinaryReader r)
        {
            var p = new Pack
            {
                Id = r.ReadString(),
                Scene = r.ReadString(),
                Pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
                Yaw = r.ReadSingle(),
                Owner = r.ReadString(),
                Contents = r.ReadString(),
            };
            if (!IsPackId(p.Id)) return;
            if (Packs.TryGetValue(p.Id, out var old))
            {
                if (old.Go != null) return;   // это наш же или уже есть
                Packs.Remove(p.Id);
            }
            Packs[p.Id] = p;
            MelonLogger.Msg($"[рюкзак] {p.Owner}: рюкзак {p.Id} в {p.Scene}");
            if (Here(p)) Spawn(p);
        }

        public static void ReceiveRemove(BinaryReader r) => Remove(r.ReadString(), false);

        private static void Remove(string id, bool announce)
        {
            if (!Packs.TryGetValue(id, out var p)) return;
            Packs.Remove(id);
            if (p.Go != null) { try { UnityEngine.Object.Destroy(p.Go); } catch { } }
            WorldObjects.ForgetCache(typeof(Container));
            WorldObjects.ForgetUncreated(p.Scene, p.Id);
            MelonLogger.Msg($"[рюкзак] {id} пуст, убран");
            if (!announce) return;
            var w = Msg.New(MsgType.Feature);
            w.Write(Features.PackRemove);
            w.Write(id);
            Session.SendGameEvent(Msg.Done(w));
        }

        // Содержимое пришло обычным сообщением контейнера (Game/WorldObjects.cs): помним его для сохранения
        // и для того случая, когда рюкзак появится у нас позже.
        public static void NoteContents(string id, string state)
        {
            if (Packs.TryGetValue(id, out var p) && state != null) { p.Contents = state; p.NetAt = Time.unscaledTime; }
        }

        // Рюкзак сейчас открыт у другого игрока в нашей локации: «пуст» решает он, когда закроет.
        private static bool OpenByOther(Pack p)
        {
            try
            {
                string key = "box:" + p.Id, scene = GameApi.Scene();
                float now = Time.unscaledTime;
                foreach (var pl in Session.Players.Values)
                    if (pl.Id != Session.LocalId && pl.Busy == key && Session.With(pl, scene) && now - pl.LastState < 3f) return true;
            }
            catch { }
            return false;
        }

        private static bool Here(Pack p) => GameApi.InGame() && ItemSync.Settled && p.Scene == ItemSync.SceneKey();

        private static bool Spawn(Pack p)
        {
            var prefab = Prefab();
            if (prefab == null)
            {
                // Ещё грузится: создадим в следующих кадрах (Tick), не ждём и не бросаем.
                if (PrefabPending && Packs.ContainsKey(p.Id) && !Deferred.Contains(p.Id)) Deferred.Add(p.Id);
                return false;
            }
            try
            {
                var go = UnityEngine.Object.Instantiate(prefab, p.Pos, Quaternion.Euler(0f, p.Yaw, 0f));
                go.name = "TLDCoop_Pack_" + p.Id.Substring(GuidPrefix.Length);
                go.SetActive(true);
                var c = go.GetComponentInChildren<Container>(true);
                if (c == null) { UnityEngine.Object.Destroy(go); MelonLogger.Warning("[рюкзак] у префаба нет контейнера"); _prefabFailed = true; return false; }
                // Свой GUID на самом контейнере: по нему контейнер узнают все (IdOf) и синхронизируют содержимое.
                ObjectGuid.MaybeAttachObjectGuidAddOrReplace(c.gameObject, p.Id);
                Prepare(c);
                p.Go = go;
                p.C = c;
                p.SpawnedAt = Time.unscaledTime;
                p.Filled = false;
                WorldObjects.ForgetCache(typeof(Container));
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[рюкзак] появиться: " + e.Message);
                return false;
            }
        }

        // Контейнер без своей добычи: уже обыскан, сам игрой не сохраняется (хранится у нас в слоте).
        private static void Prepare(Container c)
        {
            try
            {
                c.m_NotPopulated = false;
                c.m_Inspected = true;
                c.m_StartInspected = true;
                c.m_DisableSerialization = true;
                c.m_GearToInstantiate?.Clear();
                c.m_LootTablePrefab = null;
                c.m_LockedLootTablePrefab = null;
                // В 2.59 Container.Start тянет лут из m_LootTableData (старые поля выше — запасные): без этого в рюкзак
                // умершего на следующем кадре докладывалась добыча из таблицы CONTAINER_BackPack.
                c.m_LootTableData = null;
                c.m_LockedLootTableData = null;
                c.m_MinRandomItems = 0;
                c.m_MaxRandomItems = 0;
            }
            catch { }
        }

        private static void Fill(Pack p)
        {
            p.Filled = true;
            if (p.C == null || string.IsNullOrEmpty(p.Contents)) return;
            WorldObjects.Applying = true;
            try
            {
                p.C.DestroyAllGear();
                var list = new Il2CppSystem.Collections.Generic.List<GearItem>();
                p.C.Deserialize(p.Contents, list);
                Prepare(p.C);
            }
            catch (Exception e) { MelonLogger.Warning("[рюкзак] содержимое: " + e.Message); }
            finally { WorldObjects.Applying = false; }
        }

        // Локация загрузилась: рюкзаки этой локации — до отложенных состояний контейнеров (Game/WorldObjects.cs).
        public static void SpawnScene(string scene)
        {
            foreach (var p in Packs.Values)
                if (p.Scene == scene && p.Go == null) Spawn(p);
        }

        private static float _next;

        public static void Tick()
        {
            if (!GameApi.InGame()) return;
            // Префаб грузим заранее, как только мы в игре: к смерти он уже будет готов.
            if (Session.Mode != Mode.Off) { StartPrefab(); PollPrefab(); }
            if (Deferred.Count > 0 && _prefab != null) SpawnDeferred();
            if (Packs.Count == 0) return;
            float now = Time.unscaledTime;
            List<string> empty = null;
            foreach (var p in Packs.Values)
            {
                if (p.Go == null) continue;
                if (p.C == null) { p.Go = null; continue; }   // локация выгружена — объект уничтожен Unity
                // Первые секунды игра может вызвать Start контейнера: держим его «обысканным», содержимое — после.
                if (now - p.SpawnedAt < 3f) Prepare(p.C);
                if (!p.Filled && now - p.SpawnedAt > 0.2f) Fill(p);
                if (now < _next || p == _own) continue;
                bool open = WorldObjects.OpenContainer != null && WorldObjects.OpenContainer.Pointer == p.C.Pointer;
                if (open || now - p.SpawnedAt < 5f) continue;
                // Другой ещё держит рюкзак открытым (или только что прислал его состояние): не убираем у него из-под окна.
                if (now - p.NetAt < 4f || OpenByOther(p)) continue;
                try
                {
                    // Вещи, которые у нас не создались (нет DLC или мода), остаются в рюкзаке: не «пусто» и не теряются из содержимого.
                    p.Contents = Serialized(p) ?? p.Contents;
                    var items = new Il2CppSystem.Collections.Generic.List<GearItem>();
                    p.C.GetItems(items);
                    int real = WorldObjects.UncreatedCount(p.Scene, p.Id);
                    foreach (var gi in items) if (!IsWaterSupply(gi)) real++;
                    if (real == 0) (empty ??= new()).Add(p.Id);
                }
                catch { }
            }
            if (now >= _next) _next = now + 2f;
            if (empty != null) foreach (var id in empty) Remove(id, true);
        }

        // Содержимое рюкзака у нас + вещи, которые у нас не создались (Game/WorldObjects.cs).
        private static string Serialized(Pack p)
        {
            string s = p.C.Serialize();
            return s == null ? null : WorldObjects.WithUncreated(p.Scene, p.Id, s);
        }

        // ---------- Слот сохранения ----------

        private static IEnumerable<string> SaveLines()
        {
            foreach (var p in Packs.Values)
            {
                if (p.C != null) { try { p.Contents = Serialized(p) ?? p.Contents; } catch { } }
                yield return string.Join("|", p.Id, p.Scene, F(p.Pos.x), F(p.Pos.y), F(p.Pos.z), F(p.Yaw),
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(p.Owner)), Convert.ToBase64String(Encoding.UTF8.GetBytes(p.Contents ?? "")));
            }
        }

        private static void LoadLines(List<string> lines)
        {
            foreach (var p in Packs.Values) if (p.Go != null) { try { UnityEngine.Object.Destroy(p.Go); } catch { } }
            Packs.Clear();
            Deferred.Clear();
            _own = null;
            foreach (var l in lines)
            {
                try
                {
                    var a = l.Split('|');
                    if (a.Length < 8 || !IsPackId(a[0])) continue;
                    Packs[a[0]] = new Pack
                    {
                        Id = a[0], Scene = a[1],
                        Pos = new Vector3(P(a[2]), P(a[3]), P(a[4])), Yaw = P(a[5]),
                        Owner = Encoding.UTF8.GetString(Convert.FromBase64String(a[6])),
                        Contents = Encoding.UTF8.GetString(Convert.FromBase64String(a[7])),
                    };
                }
                catch { }
            }
            if (Packs.Count > 0) MelonLogger.Msg($"[рюкзак] в сохранении рюкзаков: {Packs.Count}");
        }

        private static string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        private static float P(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
    }
}
