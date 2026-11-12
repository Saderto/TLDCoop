using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppTLD.Gameplay;
using Il2CppTLD.AI;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace CoopByEMPEROR.Game
{
    // Разные механики игры, которые меняют мир не через «выбросить» и не через контейнер.
    //  — Четвертование туши: куски мяса, шкура и кишки появляются у разделывающего сами. Через 1,5 с после
    //    конца четвертования новые вещи вокруг туши рассылаются как выброшенные, а туша убирается у всех.
    //  — Большие вещи, которые море выносит на берег после бури (BeachcombingSpawner): раскладывает их только
    //    хост, у друзей своя раскладка не делается. Хост рассылает список больших вещей локации, друг ставит
    //    такие же (с теми же GUID, поэтому их ящики дальше синхронизируются как обычные контейнеры).
    //    Мелочь на берегу раскладывают обычные спавнеры, она идёт через Game/RadialSync.cs.
    //  — Украшения убежища: поставил, переставил, выложил из рюкзака — через секунду у всех оно стоит там же
    //    (у кого его нет, создаётся из того же префаба с тем же GUID); убрал в рюкзак или разобрал — пропадает у всех.
    //  — Кугуар: когда он приходит и где его территория, решает только хост. Его состояние (CougarManager)
    //    уходит друзьям, у них самих кугуар не «приходит». Уровень угрозы по регионам берётся больший из двух.
    //  — Испуг зверей: выстрел, взрыв шумелки и ракета из ракетницы пугают зверей только в игре стрелявшего,
    //    а мозг зверя может быть у друга. Поэтому место и сила испуга уходят всем, и у кого зверь настоящий,
    //    тот его и пугает (убегает; на того, кто уже нападает или в схватке, не действует).
    //  — Ящик для засолки (CuringStorage): закрыл ящик — его состояние (соль, мясо, сколько просолилось) у всех.
    //  — Тайник в камнях: построил — появляется у всех на том же месте с тем же GUID (содержимое дальше
    //    идёт как обычный контейнер); разобрал — пропадает у всех.
    //  — Износ: раз в минуту хост шлёт состояние (%) вещей своей локации, если там есть друзья: на земле
    //    (у друга вещь с тем же именем на том же месте), в контейнерах (по id контейнера и имени вещи)
    //    и состояние туш. У друга всё это становится как у хоста.
    public static class WorldEvents
    {
        private const byte KCarcassGone = 1, KBeachBig = 2, KBeachAsk = 3, KDeco = 4, KCougar = 5, KScare = 6, KCuring = 7, KRock = 8, KDecay = 9, KChimney = 15;

        private static bool Active => Session.Mode != Mode.Off && Session.InSharedWorld && GameApi.InGame();

        // Отложенные события для незагруженных локаций.
        private static readonly Dictionary<string, List<byte[]>> Pending = new();
        private static string _settled = "";

        // Событие мира хоста к нам не относится: соло, или друг не в мире хоста и не грузится в него.
        private static bool Ignored => Session.Mode == Mode.Off || (Session.Mode == Mode.Client && !Session.InHostWorld && !WorldTransfer.Busy);

        // Пришли, пока игрок на экране загрузки: события не по локации (карта, случайные объекты, кугуар, трубы).
        // Применяем по порядку, когда снова в игре.
        // Хост помнит мир события: вышел в меню и загрузил другой мир — старое не применяем.
        private static readonly List<(byte[] raw, Peer from, string save)> Held = new();

        private static string SaveName()
        {
            try { return SaveGameSystem.GetCurrentSaveName() ?? ""; } catch { return ""; }
        }

        private static void Hold(byte kind, byte[] raw, Peer from)
        {
            if (Ignored) return;
            if (kind == KCougar) Held.RemoveAll(h => h.raw.Length > 1 && h.raw[1] == KCougar);   // нужно только последнее состояние
            if (Held.Count >= 500) Held.RemoveAt(0);
            Held.Add((raw, from, Session.Mode == Mode.Host ? SaveName() : null));
        }

        public static void Tick(float now)
        {
            if (!Active) { _settled = ""; return; }
            if (Held.Count > 0)
            {
                var held = new List<(byte[] raw, Peer from, string save)>(Held);
                Held.Clear();
                string save = Session.Mode == Mode.Host ? SaveName() : null;
                foreach (var (raw, from, hs) in held)
                {
                    if (from != null && from.Closed) continue;
                    if (hs != null && hs != save) continue;
                    try { using var r = Msg.Read(raw, out _); Receive(r, raw, from); }
                    catch (Exception e) { MelonLogger.Warning("[мир] отложенное событие: " + e.Message); }
                }
            }
            if (ItemSync.Settled && ItemSync.SceneKey() != _settled)
            {
                _settled = ItemSync.SceneKey();
                OnSceneSettled(_settled);
            }
            QuarterTick(now);
            BeachTick(now);
            DecoTick(now);
            SurfaceTick(now);
            DecoLoadTick(now);
            DecoHiddenTick(now);
            CougarTick(now);
            DecayTick(now);
            MapShare.Tick(now);
            WorldMechanics.Tick(now);
        }

        private static void OnSceneSettled(string scene)
        {
            if (Pending.TryGetValue(scene, out var list))
            {
                Pending.Remove(scene);
                foreach (var raw in list) ApplyRaw(raw);
                PlayerProgress.NoteApplied(list);
                MelonLogger.Msg($"[мир] {scene}: применено отложенных событий {list.Count}");
            }
            RandomObjects.OnSceneSettled(scene);
            MapShare.OnSceneSettled(scene);
            QuestItems.OnSceneSettled(scene);
            WorldMechanics.OnSceneSettled(scene);
            _beachSent = null;
            _nextBeach = Time.unscaledTime + 2f;
            if (Session.Mode == Mode.Client)
            {
                var w = Msg.New(MsgType.WorldEvent);
                w.Write(KBeachAsk);
                w.Write(scene);
                Session.SendGameEvent(Msg.Done(w));
            }
        }

        private static void Send(byte kind, string scene, Action<BinaryWriter> body)
        {
            var w = Msg.New(MsgType.WorldEvent);
            w.Write(kind);
            w.Write(scene);
            body(w);
            Session.SendGameEvent(Msg.Done(w));
        }

        // ---------- Четвертование туши ----------

        private static Vector3 _qPos;
        private static string _qName;
        private static HashSet<IntPtr> _qBefore;
        private static float _qAt = -1f;
        private const float QuarterRadius = 12f;

        private static List<GearItem> GroundItemsNear(Vector3 pos, float radius)
        {
            var list = new List<GearItem>();
            foreach (var gi in UnityEngine.Object.FindObjectsOfType<GearItem>())
            {
                if (gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer) continue;
                if ((gi.transform.position - pos).sqrMagnitude <= radius * radius) list.Add(gi);
            }
            return list;
        }

        private static string Clean(string n) =>
            System.Text.RegularExpressions.Regex.Replace(n ?? "", @"\s*\((Clone|\d+)\)", "").Trim();

        internal static void QuarterStart(Panel_BodyHarvest panel)
        {
            _qBefore = null;
            if (!Active || panel == null) return;
            try
            {
                var bh = panel.m_BodyHarvest;
                if (bh == null) return;
                _qPos = bh.transform.position;
                _qName = Clean(bh.gameObject.name);
                _qBefore = new HashSet<IntPtr>();
                foreach (var gi in GroundItemsNear(_qPos, QuarterRadius)) _qBefore.Add(gi.Pointer);
            }
            catch (Exception e) { MelonLogger.Warning("[туша] " + e.Message); _qBefore = null; }
        }

        internal static void QuarterDone()
        {
            if (_qBefore == null || !Active) return;
            _qAt = Time.unscaledTime + 1.5f;
            var pos = _qPos;
            string name = _qName;
            Send(KCarcassGone, ItemSync.SceneKey(), w => { w.Write(pos.x); w.Write(pos.y); w.Write(pos.z); w.Write(name); });
            MelonLogger.Msg($"[туша] четвертовал {name}: туша убрана у всех");
        }

        private static void QuarterTick(float now)
        {
            if (_qAt < 0f || now < _qAt) return;
            _qAt = -1f;
            var before = _qBefore;
            _qBefore = null;
            if (before == null) return;
            int n = 0;
            try
            {
                foreach (var gi in GroundItemsNear(_qPos, QuarterRadius))
                {
                    if (before.Contains(gi.Pointer) || ItemSync.TryGetSentPos(gi, out _)) continue;
                    ItemSync.OnLocalDrop(gi);
                    n++;
                }
            }
            catch (Exception e) { MelonLogger.Warning("[туша] куски: " + e.Message); }
            MelonLogger.Msg($"[туша] разослал куски после четвертования: {n}");
        }

        private static void ApplyCarcassGone(BinaryReader r)
        {
            var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            string name = r.ReadString();
            BodyHarvest best = null;
            float bd = 3f * 3f;
            foreach (var bh in UnityEngine.Object.FindObjectsOfType<BodyHarvest>())
            {
                if (bh == null) continue;
                float d = (bh.transform.position - pos).sqrMagnitude;
                if (d > bd) continue;
                if (best != null && Clean(bh.gameObject.name) != name && Clean(best.gameObject.name) == name) continue;
                bd = d;
                best = bh;
            }
            if (best == null) { MelonLogger.Msg($"[туша] друг четвертовал {name}, у меня туши там нет"); return; }
            try { best.BodyHarvestDestroy(); }
            catch { UnityEngine.Object.Destroy(best.gameObject); }
            MelonLogger.Msg($"[туша] друг четвертовал {name}, убрал тушу");
        }

        // ---------- Большие вещи на берегу ----------

        private static string _beachSent;
        private static float _nextBeach;
        // Хост: последний список по локациям (для тех, кто придёт позже).
        private static readonly Dictionary<string, string> BeachByScene = new();
        // Друг: свои копии по GUID.
        private static readonly Dictionary<string, CustomSpawnedItem> BeachCopies = new();

        internal static bool AllowBigItems() => !(Active && Session.Mode == Mode.Client);

        private static bool IsBeachItem(CustomSpawnedItem it)
        {
            if (it == null) return false;
            try
            {
                if (it.TryCast<DropppedDecorationStorageCustomSpawnedItem>() != null) return false;
                if (it.TryCast<RuinedTravoisCustomSpawnedItem>() != null) return false;
            }
            catch { }
            return true;
        }

        private static string BeachList()
        {
            var parts = new List<string>();
            foreach (var it in UnityEngine.Object.FindObjectsOfType<CustomSpawnedItem>())
            {
                if (!IsBeachItem(it)) continue;
                try
                {
                    var data = it.Serialize();
                    if (data == null || string.IsNullOrEmpty(data.m_Guid)) continue;
                    parts.Add(FastJson.Serialize(data));
                }
                catch { }
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join("\n", parts);
        }

        private static void BeachTick(float now)
        {
            if (Session.Mode != Mode.Host || now < _nextBeach || !ItemSync.Settled) return;
            _nextBeach = now + 3f;
            if (UnityEngine.Object.FindObjectOfType<BeachcombingSpawner>() == null) return;
            string list;
            try { list = BeachList(); } catch (Exception e) { MelonLogger.Warning("[берег] " + e.Message); return; }
            if (list == _beachSent) return;
            _beachSent = list;
            string scene = ItemSync.SceneKey();
            BeachByScene[scene] = list;
            Send(KBeachBig, scene, w => w.Write(list));
            MelonLogger.Msg($"[берег] большие вещи на берегу изменились, разослал ({(list.Length == 0 ? 0 : list.Split('\n').Length)})");
        }

        private static void ApplyBeach(BinaryReader r)
        {
            if (Session.Mode != Mode.Client) return;
            string list = r.ReadString();
            var want = new Dictionary<string, CustomSpawnedItemSaveData>();
            foreach (var json in list.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var d = FastJson.Deserialize<CustomSpawnedItemSaveData>(json);
                    if (d != null && !string.IsNullOrEmpty(d.m_Guid)) want[d.m_Guid] = d;
                }
                catch { }
            }
            // Что уже стоит в локации (из сохранения хоста или поставлено раньше).
            var have = new Dictionary<string, CustomSpawnedItem>();
            foreach (var it in UnityEngine.Object.FindObjectsOfType<CustomSpawnedItem>())
            {
                if (!IsBeachItem(it)) continue;
                try { var g = it.Serialize()?.m_Guid; if (!string.IsNullOrEmpty(g)) have[g] = it; } catch { }
            }
            int added = 0, removed = 0;
            foreach (var kv in want)
            {
                if (have.ContainsKey(kv.Key)) continue;
                try
                {
                    var it = CustomSpawnedItemManager.SpawnBigItem(kv.Value.m_Address);
                    if (it == null) continue;
                    it.Deserialize(kv.Value);
                    it.transform.SetPositionAndRotation(kv.Value.m_Position, kv.Value.m_Rotation);
                    BeachCopies[kv.Key] = it;
                    added++;
                }
                catch (Exception e) { MelonLogger.Warning("[берег] поставить: " + e.Message); }
            }
            foreach (var kv in have)
            {
                if (want.ContainsKey(kv.Key)) continue;
                try { UnityEngine.Object.Destroy(kv.Value.gameObject); removed++; } catch { }
                BeachCopies.Remove(kv.Key);
            }
            if (added + removed > 0) MelonLogger.Msg($"[берег] большие вещи как у хоста: +{added}, −{removed}");
        }

        // ---------- Украшения убежища ----------

        private class DecoQueued { public DecorationItem Item; public float At; }
        private static readonly List<DecoQueued> DecoQueue = new();
        private static bool _decoApplying;

        private static string DecoId(DecorationItem di)
        {
            try { return ObjectGuid.MaybeGetGuidFromGameObject(di.gameObject) ?? ""; } catch { return ""; }
        }

        internal static void OnDecoPlaced(DecorationItem di)
        {
            if (!Active || _decoApplying || di == null) return;
            DecoQueue.RemoveAll(q => q.Item == null || q.Item.Pointer == di.Pointer);
            DecoQueue.Add(new DecoQueued { Item = di, At = Time.unscaledTime + 1f });   // упадёт и встанет на место
        }

        internal static void OnDecoRemoved(DecorationItem di)
        {
            if (!Active || _decoApplying || di == null) return;
            DecoQueue.RemoveAll(q => q.Item == null || q.Item.Pointer == di.Pointer);
            string id = DecoId(di);
            if (id.Length == 0) return;
            Send(KDeco, ItemSync.SceneKey(), w => { w.Write(false); w.Write(id); });
            MelonLogger.Msg($"[убежище] убрал украшение {Clean(di.gameObject.name)}");
        }

        private static void DecoTick(float now)
        {
            for (int i = DecoQueue.Count - 1; i >= 0; i--)
            {
                var q = DecoQueue[i];
                if (q.Item == null) { DecoQueue.RemoveAt(i); continue; }
                if (now < q.At) continue;
                DecoQueue.RemoveAt(i);
                try
                {
                    var di = q.Item;
                    if (!di.gameObject.activeInHierarchy) continue;
                    string id = DecoId(di);
                    if (id.Length == 0)
                    {
                        // Взял из рюкзака и поставил: у новой копии может не быть GUID — даём свой, иначе у других
                        // украшение пропадало («убрал» дошло, а «поставил» нет).
                        id = Guid.NewGuid().ToString();
                        try { ObjectGuid.MaybeAttachObjectGuidAddOrReplace(di.gameObject, id); }
                        catch (Exception e) { MelonLogger.Msg("[убежище] у украшения нет GUID, не отправляю: " + e.Message); continue; }
                    }
                    string key = "";
                    try { key = di.GetDecorationPrefab()?.AssetGUID ?? ""; } catch { }
                    if (key.Length == 0) key = "name:" + Clean(di.gameObject.name);
                    var t = di.transform;
                    var pos = t.position;
                    var rot = t.rotation;
                    Send(KDeco, ItemSync.SceneKey(), w =>
                    {
                        w.Write(true); w.Write(id); w.Write(key);
                        w.Write(pos.x); w.Write(pos.y); w.Write(pos.z);
                        w.Write(rot.x); w.Write(rot.y); w.Write(rot.z); w.Write(rot.w);
                    });
                    MelonLogger.Msg($"[убежище] поставил украшение {Clean(di.gameObject.name)}, разослал");
                    SendMovedSurfaceItems("переставил украшение");
                }
                catch (Exception e) { MelonLogger.Warning("[убежище] " + e.Message); }
            }
        }

        // ---------- Вещи на мебели (0.19.35) ----------
        // Переставил или убрал мебель — вещи на ней у меня сдвинулись (уехали с ней или игра опустила их на пол:
        // BreakDown.StickSurfaceObjectsToGround в DecorationItem.OnPickup), а у остальных остались висеть на старом месте.
        // Дальше чужой подбор такой вещи не находил её по месту, и сверка клала её обратно — патроны и прочее двоились.
        // Запоминаем вещи вокруг перед перестановкой и после неё рассылаем те, что сдвинулись (замена под тем же местом).
        private static List<(GearItem Gi, Vector3 Pos)> _surfaceSnap;
        private static float _surfaceSnapAt, _surfaceSendAt = -1f;

        internal static void SnapSurfaceItems(Vector3 center, float radius)
        {
            if (!Active || _decoApplying) return;
            try
            {
                var list = new List<(GearItem, Vector3)>();
                float r2 = radius * radius;
                foreach (var gi in UnityEngine.Object.FindObjectsOfType<GearItem>())
                {
                    if (gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer || !gi.gameObject.activeInHierarchy) continue;
                    var p = gi.transform.position;
                    if ((p - center).sqrMagnitude > r2) continue;
                    if (ItemSync.IsPersonal(gi) || ItemSync.InPot(gi)) continue;
                    if (gi.GetComponent<DecorationItem>() != null) continue;   // само украшение идёт своим событием
                    list.Add((gi, p));
                }
                _surfaceSnap = list;
                _surfaceSnapAt = Time.unscaledTime;
            }
            catch (Exception e) { MelonLogger.Warning("[убежище] вещи вокруг мебели: " + e.Message); }
        }

        // Убрал мебель в рюкзак: вещи с неё игра опускает сразу, рассылаем через секунду (успеют встать).
        internal static void SendMovedSurfaceItemsSoon() { if (_surfaceSnap != null) _surfaceSendAt = Time.unscaledTime + 1f; }

        private static void SurfaceTick(float now)
        {
            if (_surfaceSendAt > 0f && now >= _surfaceSendAt) { _surfaceSendAt = -1f; SendMovedSurfaceItems("убрал украшение"); }
        }

        internal static void SendMovedSurfaceItems(string why)
        {
            var snap = _surfaceSnap;
            _surfaceSnap = null;
            if (snap == null || !Active || Time.unscaledTime - _surfaceSnapAt > 120f) return;
            int n = 0;
            foreach (var (gi, old) in snap)
            {
                try
                {
                    if (gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer || !gi.gameObject.activeInHierarchy) continue;
                    if ((gi.transform.position - old).sqrMagnitude < 0.05f * 0.05f) continue;
                    ItemSync.Resend(gi, old);
                    n++;
                }
                catch { }
            }
            if (n > 0) MelonLogger.Msg($"[убежище] {why}: сдвинулись вещи вокруг ({n}), разослал их новое место");
        }

        private static readonly Dictionary<string, float> DecoHidden = new();

        private static void DecoHiddenTick(float now)
        {
            if (DecoHidden.Count == 0) return;
            List<string> gone = null;
            foreach (var kv in DecoHidden) if (now > kv.Value) (gone ??= new List<string>()).Add(kv.Key);
            if (gone == null) return;
            foreach (var id in gone)
            {
                DecoHidden.Remove(id);
                var di = FindDeco(id);
                if (di != null && !di.gameObject.activeSelf) UnityEngine.Object.Destroy(di.gameObject);
            }
        }

        private static DecorationItem FindDeco(string id)
        {
            foreach (var di in UnityEngine.Object.FindObjectsOfType<DecorationItem>(true))
                if (di != null && DecoId(di) == id) return di;
            return null;
        }

        private static void ApplyDeco(BinaryReader r)
        {
            bool placed = r.ReadBoolean();
            string id = r.ReadString();
            var di = FindDeco(id);
            // Новое событие об этом украшении отменяет старое, которое ещё ждёт модель.
            DecoWaits.Remove(id);
            _decoApplying = true;
            try
            {
                if (!placed)
                {
                    // Не удаляем сразу: друг обычно берёт украшение, чтобы переставить. Прячем, и если через 2 минуты
                    // оно так и не встало обратно — удаляем.
                    if (di != null)
                    {
                        di.gameObject.SetActive(false);
                        DecoHidden[id] = Time.unscaledTime + 120f;
                        MelonLogger.Msg("[убежище] друг убрал украшение " + Clean(di.gameObject.name));
                    }
                    return;
                }
                DecoHidden.Remove(id);
                string key = r.ReadString();
                var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                var rot = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                GameObject src = null;
                if (di == null)
                {
                    if (key.Length == 0) { MelonLogger.Warning("[убежище] друг поставил украшение без ссылки на модель"); return; }
                    src = DecoPrefab(key, Time.unscaledTime);
                    if (src == null)
                    {
                        if (DecoLoads.ContainsKey(key))
                        {
                            // Модель ещё грузится: поставим в следующих кадрах (DecoLoadTick).
                            DecoWaits[id] = new DecoWait { Id = id, Key = key, Pos = pos, Rot = rot, Scene = _settled, Raw = _applyRaw };
                            MelonLogger.Msg("[убежище] модель украшения ещё грузится, поставлю позже: " + key);
                            return;
                        }
                        MelonLogger.Warning("[убежище] нет префаба украшения " + key);
                        return;
                    }
                }
                PlaceDeco(di, src, id, pos, rot);
            }
            finally { _decoApplying = false; }
        }

        private static void PlaceDeco(DecorationItem di, GameObject src, string id, Vector3 pos, Quaternion rot)
        {
            GameObject go = di != null ? di.gameObject : null;
            if (go == null)
            {
                go = UnityEngine.Object.Instantiate(src);
                ObjectGuid.MaybeAttachObjectGuidAddOrReplace(go, id);
            }
            go.transform.SetPositionAndRotation(pos, rot);
            go.SetActive(true);
            MelonLogger.Msg($"[убежище] друг поставил украшение {Clean(go.name)}" + (di == null ? " (создал)" : ""));
        }

        // Модели украшений грузим без ожидания (WaitForCompletion во время загрузки сцены может не кончиться
        // никогда). Одна загрузка на ключ; готовые забираем в следующих кадрах.
        private sealed class DecoLoad
        {
            public UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject> H;
            public float Since;
        }

        // Украшение, которое ждёт свою модель. Raw — исходное событие: ушли из локации — вернём его в Pending.
        private sealed class DecoWait
        {
            public string Id, Key, Scene;
            public Vector3 Pos;
            public Quaternion Rot;
            public byte[] Raw;
        }

        private static readonly Dictionary<string, DecoLoad> DecoLoads = new();
        private static readonly Dictionary<string, GameObject> DecoPrefabs = new();
        private static readonly Dictionary<string, DecoWait> DecoWaits = new();
        private const float DecoLoadTimeout = 30f;
        private static byte[] _applyRaw;

        // Готовая модель или null; если null и ключ есть в DecoLoads — ещё грузится.
        private static GameObject DecoPrefab(string key, float now)
        {
            if (DecoPrefabs.TryGetValue(key, out var go) && go != null) return go;
            if (!DecoLoads.TryGetValue(key, out var l))
            {
                string k = key.StartsWith("name:") ? key.Substring(5) : key;
                try { l = new DecoLoad { H = Addressables.LoadAssetAsync<GameObject>(new Il2CppSystem.Object(IL2CPP.ManagedStringToIl2Cpp(k))), Since = now }; }
                catch { return null; }
                DecoLoads[key] = l;
            }
            PollDecoLoad(key, l, now);
            return DecoPrefabs.TryGetValue(key, out go) && go != null ? go : null;
        }

        private static void PollDecoLoad(string key, DecoLoad l, float now)
        {
            bool done;
            try { done = l.H.IsDone; } catch { done = true; }
            if (!done)
            {
                if (now - l.Since < DecoLoadTimeout) return;
                MelonLogger.Warning($"[убежище] модель украшения {key} не загрузилась за {DecoLoadTimeout:0} с, бросил");
                DecoLoads.Remove(key);
                return;
            }
            DecoLoads.Remove(key);
            GameObject obj = null;
            try
            {
                if (l.H.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded) obj = l.H.Result;
            }
            catch { }
            if (obj != null) DecoPrefabs[key] = obj;
        }

        // Каждый кадр: забираем готовые модели и ставим украшения, которые их ждали. Ничего не ждём.
        private static void DecoLoadTick(float now)
        {
            if (DecoLoads.Count > 0)
                foreach (var kv in new List<KeyValuePair<string, DecoLoad>>(DecoLoads)) PollDecoLoad(kv.Key, kv.Value, now);
            if (DecoWaits.Count == 0) return;
            foreach (var w in new List<DecoWait>(DecoWaits.Values))
            {
                if (w.Scene != _settled || !ItemSync.Settled || ItemSync.SceneKey() != w.Scene)
                {
                    // Ушли из локации, пока грузилась модель: событие снова отложенное, применится при входе.
                    DecoWaits.Remove(w.Id);
                    Repend(w);
                    continue;
                }
                GameObject src = DecoPrefabs.TryGetValue(w.Key, out var go) && go != null ? go : null;
                if (src == null && DecoLoads.ContainsKey(w.Key)) continue;   // ещё грузится
                DecoWaits.Remove(w.Id);
                if (src == null) { MelonLogger.Warning("[убежище] нет префаба украшения " + w.Key); continue; }
                _decoApplying = true;
                try { PlaceDeco(FindDeco(w.Id), src, w.Id, w.Pos, w.Rot); }
                catch (Exception e) { MelonLogger.Warning("[убежище] " + e.Message); }
                finally { _decoApplying = false; }
            }
        }

        private static void Repend(DecoWait w)
        {
            if (w.Raw == null || string.IsNullOrEmpty(w.Scene)) return;
            if (!Pending.TryGetValue(w.Scene, out var p)) Pending[w.Scene] = p = new List<byte[]>();
            string ck = CompactKey(w.Raw);
            if (ck != null && p.Exists(o => CompactKey(o) == ck)) return;   // там уже событие новее
            p.Add(w.Raw);
        }

        // ---------- Кугуар ----------

        private static string _cougarSent;
        private static float _nextCougar;
        private static bool _cougarApplying;

        private static CougarManager Cougar()
        {
            try { return UnityEngine.Object.FindObjectOfType<CougarManager>(); } catch { return null; }
        }

        private static string CougarJson()
        {
            var cm = Cougar();
            var d = cm != null ? cm.Serialize() : null;
            return d != null ? FastJson.Serialize(d) : null;
        }

        private static void CougarTick(float now)
        {
            if (Session.Mode != Mode.Host || now < _nextCougar) return;
            _nextCougar = now + 5f;
            string json;
            try { json = CougarJson(); } catch (Exception e) { MelonLogger.Warning("[кугуар] " + e.Message); return; }
            if (string.IsNullOrEmpty(json) || json == _cougarSent) return;
            _cougarSent = json;
            Send(KCougar, "", w => w.Write(json));
            MelonLogger.Msg("[кугуар] состояние кугуара изменилось, разослал");
        }

        private static byte[] CougarMsg()
        {
            string json = _cougarSent ?? CougarJson();
            if (string.IsNullOrEmpty(json)) return null;
            var w = Msg.New(MsgType.WorldEvent);
            w.Write(KCougar);
            w.Write("");
            w.Write(json);
            return Msg.Done(w);
        }

        internal static bool AllowCougarArrival() => _cougarApplying || !(Active && Session.Mode == Mode.Client);

        private static void ApplyCougar(BinaryReader r)
        {
            string json = r.ReadString();
            if (Session.Mode != Mode.Client) return;
            var cm = Cougar();
            if (cm == null) return;
            _cougarApplying = true;
            try
            {
                var host = FastJson.Deserialize<CougarManagerData>(json);
                if (host == null) return;
                // Угроза по регионам: где у нас выше — оставляем свою.
                var mine = cm.Serialize();
                if (mine?.m_RegionThreatLevelNames != null && host.m_RegionThreatLevelNames != null && host.m_RegionThreatLevels != null)
                {
                    for (int i = 0; i < mine.m_RegionThreatLevelNames.Count; i++)
                    {
                        string region = mine.m_RegionThreatLevelNames[i];
                        int lvl = mine.m_RegionThreatLevels[i];
                        int j = host.m_RegionThreatLevelNames.IndexOf(region);
                        if (j < 0) { host.m_RegionThreatLevelNames.Add(region); host.m_RegionThreatLevels.Add(lvl); }
                        else if (lvl > host.m_RegionThreatLevels[j]) host.m_RegionThreatLevels[j] = lvl;
                    }
                }
                cm.Deserialize(host);
                MelonLogger.Msg("[кугуар] состояние кугуара как у хоста");
            }
            catch (Exception e) { MelonLogger.Warning("[кугуар] применить: " + e.Message); }
            finally { _cougarApplying = false; }
        }

        // ---------- Испуг зверей ----------

        internal enum Scare : byte { Shot = 0, NoiseMaker = 1, Flare = 2 }
        private static float _lastScare;

        internal static void OnScare(Vector3 pos, Scare what)
        {
            if (!Active) return;
            // Выстрел теперь слышат сами звери у всех игроков, как в одиночной (AnimalSync.Noise.cs):
            // своя дальность у каждого зверя и бегство от стрелявшего. Старый испуг (50 м, бег куда попало) не шлём.
            if (what == Scare.Shot) return;
            float now = Time.unscaledTime;
            if (what == Scare.Shot && now - _lastScare < 0.5f) return;
            _lastScare = now;
            Send(KScare, ItemSync.SceneKey(), w => { w.Write(pos.x); w.Write(pos.y); w.Write(pos.z); w.Write((byte)what); });
        }

        private static void ApplyScare(BinaryReader r)
        {
            var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var what = (Scare)r.ReadByte();
            // Ракета: дальность испуга у каждого вида своя (ниже), отбираем с запасом.
            float radius = what switch { Scare.Shot => 50f, Scare.NoiseMaker => 40f, _ => 100f };
            var list = BaseAiManager.m_BaseAis;
            if (list == null) return;
            int n = 0;
            var kinds = new HashSet<AiSubType>();
            for (int i = 0; i < list.Count; i++)
            {
                var ai = list[i];
                if (ai == null || !ai.enabled || (ai.transform.position - pos).sqrMagnitude > radius * radius) continue;
                var m = ai.m_CurrentMode;
                if (m == AiMode.Dead || m == AiMode.Struggle || m == AiMode.Attack || m == AiMode.Flee) continue;
                var t = ai.m_AiSubType;
                // От выстрела бегут олени, кролики и волки; шумелка пугает всех в 40 м.
                if (what == Scare.Shot && t != AiSubType.Stag && t != AiSubType.Rabbit && t != AiSubType.Wolf) continue;
                if (what == Scare.Flare)
                {
                    // Как у игры (BaseAi.MaybeFleeFromFlareGunRound): у каждого вида своя дальность испуга от ракеты,
                    // у кого она 0 — тот ракеты не боится. Раньше пугались только волки, олени и зайцы.
                    float d = ai.m_FleeFromFlareGunRoundImpactDistance;
                    if (d <= 0f || (ai.transform.position - pos).sqrMagnitude > d * d) continue;
                }
                // Бегут прочь от места, а не куда попало.
                try { ai.FleeFrom(pos); n++; kinds.Add(t); }
                catch { try { ai.SetAiMode(AiMode.Flee); n++; kinds.Add(t); } catch { } }
            }
            if (n > 0) MelonLogger.Msg($"[звери] испуг от друга ({what}): убегают {n} ({string.Join(", ", kinds)})");
        }

        // ---------- Дым из трубы ----------
        // Огонь в печи дома дымит трубой снаружи (Chimney, m_LifetimeGameMinutes). Игра ставит это время,
        // когда сам игрок выходит из дома от горящей печи, — у второго игрока снаружи труба не дымила.
        // Тот, у кого горит печь, раз в полминуты шлёт GUID трубы и сколько ещё гореть; у остальных труба
        // дымит столько же (и после захода в ту локацию — с поправкой на прошедшее время).

        private static readonly Dictionary<string, (float minutes, float at)> Chimneys = new();
        private static float _nextChimneyApply;
        private static int _chimneysSmoking;

        private static float GameHours()
        {
            try { var tod = GameManager.GetTimeOfDayComponent(); return tod.GetDayNumber() * 24f + tod.GetNormalizedTime() * 24f; }
            catch { return 0f; }
        }

        internal static void SendChimney(string guid, float minutes)
        {
            if (!Active || string.IsNullOrEmpty(guid)) return;
            float at = GameHours();
            Send(KChimney, ItemSync.SceneKey(), w => { w.Write(guid); w.Write(minutes); w.Write(at); });
        }

        private static void ApplyChimney(BinaryReader r)
        {
            string guid = r.ReadString();
            float minutes = r.ReadSingle(), at = r.ReadSingle();
            Chimneys[guid] = (minutes, at);
            TickChimneys(true);
        }

        internal static void TickChimneys(bool now = false)
        {
            if (Chimneys.Count == 0) return;
            float t = Time.unscaledTime;
            if (!now && t < _nextChimneyApply) return;
            _nextChimneyApply = t + 5f;
            float hours = GameHours();
            foreach (var guid in new List<string>(Chimneys.Keys))
            {
                var (minutes, at) = Chimneys[guid];
                float left = minutes - Math.Max(0f, hours - at) * 60f;
                if (left <= 0f) { Chimneys.Remove(guid); continue; }
                Chimney c = null;
                try { c = Chimney.FindByGuid(guid); } catch { }
                if (c == null || c.m_LifetimeGameMinutes >= left - 1f) continue;
                c.m_LifetimeGameMinutes = left;
                if (_chimneysSmoking++ < 5) MelonLogger.Msg($"[мир] труба {guid}: дымит от чужой печи ещё {left:0} мин");
            }
        }

        // ---------- Ящик для засолки ----------

        private static bool _curingApplying;

        internal static void OnCuringClosed(CuringStorage cs)
        {
            if (!Active || _curingApplying || cs == null) return;
            try
            {
                var st = cs.SerializeCuringStorage();
                if (st == null || string.IsNullOrEmpty(st.m_GUID)) return;
                string json = FastJson.Serialize(st);
                Send(KCuring, ItemSync.SceneKey(), w => { w.Write(st.m_GUID); w.Write(json); });
                MelonLogger.Msg("[засолка] закрыл ящик, разослал");
            }
            catch (Exception e) { MelonLogger.Warning("[засолка] " + e.Message); }
        }

        private static void ApplyCuring(BinaryReader r)
        {
            string guid = r.ReadString(), json = r.ReadString();
            foreach (var cs in UnityEngine.Object.FindObjectsOfType<CuringStorage>(true))
            {
                if (cs == null) continue;
                string g;
                try { g = cs.SerializeCuringStorage()?.m_GUID; } catch { continue; }
                if (g != guid) continue;
                _curingApplying = true;
                try
                {
                    cs.DeserializeCuringStorage(FastJson.Deserialize<CuringStorageState>(json));
                    MelonLogger.Msg("[засолка] ящик как у друга");
                }
                catch (Exception e) { MelonLogger.Warning("[засолка] применить: " + e.Message); }
                finally { _curingApplying = false; }
                return;
            }
        }

        // ---------- Тайник в камнях ----------

        private static bool _rockApplying;

        internal static void OnRockAdded(RockCache rc)
        {
            if (!Active || _rockApplying || rc == null || !ItemSync.Settled) return;
            try
            {
                if (!rc.IsUserPlaced()) return;
                string guid = rc.GetGuid();
                if (string.IsNullOrEmpty(guid)) return;
                var t = rc.transform;
                var pos = t.position;
                var rot = t.rotation;
                Send(KRock, ItemSync.SceneKey(), w =>
                {
                    w.Write(true); w.Write(guid);
                    w.Write(pos.x); w.Write(pos.y); w.Write(pos.z);
                    w.Write(rot.x); w.Write(rot.y); w.Write(rot.z); w.Write(rot.w);
                });
                MelonLogger.Msg("[тайник] построил тайник в камнях, разослал");
            }
            catch (Exception e) { MelonLogger.Warning("[тайник] " + e.Message); }
        }

        internal static void OnRockDestroyed(string guid)
        {
            if (!Active || _rockApplying || string.IsNullOrEmpty(guid)) return;
            Send(KRock, ItemSync.SceneKey(), w => { w.Write(false); w.Write(guid); });
            MelonLogger.Msg("[тайник] разобрал тайник, разослал");
        }

        private static RockCache FindRock(string guid)
        {
            foreach (var rc in UnityEngine.Object.FindObjectsOfType<RockCache>(true))
            {
                try { if (rc != null && rc.GetGuid() == guid) return rc; } catch { }
            }
            return null;
        }

        private static void ApplyRock(BinaryReader r)
        {
            bool built = r.ReadBoolean();
            string guid = r.ReadString();
            var rc = FindRock(guid);
            _rockApplying = true;
            try
            {
                if (!built)
                {
                    if (rc == null) return;
                    try { RockCacheManager.TrackDestroyedRockCacheInScene(GameApi.Scene(), guid); } catch { }
                    try { RockCacheManager.Remove(rc); } catch { }
                    UnityEngine.Object.Destroy(rc.gameObject);
                    MelonLogger.Msg("[тайник] друг разобрал тайник");
                    return;
                }
                var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                var rot = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                if (rc != null) return;
                var mgr = UnityEngine.Object.FindObjectOfType<RockCacheManager>();
                if (mgr == null || mgr.m_RockCachePrefab == null) { MelonLogger.Warning("[тайник] нет префаба тайника"); return; }
                var created = UnityEngine.Object.Instantiate(mgr.m_RockCachePrefab.gameObject, pos, rot);
                ObjectGuid.MaybeAttachObjectGuidAddOrReplace(created, guid);
                var nrc = created.GetComponent<RockCache>();
                if (nrc != null)
                {
                    nrc.m_UserPlaced = true;
                    nrc.Activate(guid);
                    try { RockCacheManager.Add(nrc); } catch { }
                }
                MelonLogger.Msg("[тайник] друг построил тайник в камнях, поставил");
            }
            catch (Exception e) { MelonLogger.Warning("[тайник] применить: " + e.Message); }
            finally { _rockApplying = false; }
        }

        // ---------- Износ вещей на земле ----------

        private static float _nextDecay;

        private static void DecayTick(float now)
        {
            if (Session.Mode != Mode.Host || now < _nextDecay || !ItemSync.Settled) return;
            _nextDecay = now + 60f;
            string scene = GameApi.Scene();
            bool anyone = false;
            foreach (var p in Session.Players.Values) if (p.Id != Session.LocalId && Session.With(p, scene)) { anyone = true; break; }
            if (!anyone) return;
            var items = new List<(string name, Vector3 pos, ushort hp)>();
            // Шкуры и кишки, сохнущие на полу: сколько уже просохло (каждая игра считает по своим часам).
            var drying = new List<(string name, Vector3 pos, float hours)>();
            foreach (var gi in UnityEngine.Object.FindObjectsOfType<GearItem>())
            {
                if (gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer || !gi.gameObject.activeInHierarchy) continue;
                try { var ev = gi.m_EvolveItem; if (ev != null && ev.m_TimeSpentEvolvingGameHours > 0f && drying.Count < 200) drying.Add((ItemSync.PrefabName(gi), gi.transform.position, ev.m_TimeSpentEvolvingGameHours)); } catch { }
                float n;
                try { n = gi.GetNormalizedCondition(); } catch { continue; }
                if (n >= 0.999f) continue;
                items.Add((ItemSync.PrefabName(gi), gi.transform.position, (ushort)Mathf.RoundToInt(Mathf.Clamp01(n) * 65535f)));
                if (items.Count >= 600) break;
            }
            // Контейнеры: только уже открытые (с вещами), где есть что-то не новое.
            var boxes = new List<(string id, List<(string name, ushort hp)> list)>();
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Container>())
            {
                if (c == null) continue;
                try
                {
                    var got = new Il2CppSystem.Collections.Generic.List<GearItem>();
                    c.GetItems(got);
                    if (got.Count == 0) continue;
                    var list = new List<(string, ushort)>();
                    bool worn = false;
                    for (int i = 0; i < got.Count; i++)
                    {
                        var gi = got[i];
                        if (gi == null) continue;
                        float n = Mathf.Clamp01(gi.GetNormalizedCondition());
                        if (n < 0.999f) worn = true;
                        list.Add((ItemSync.PrefabName(gi), (ushort)Mathf.RoundToInt(n * 65535f)));
                    }
                    if (worn) boxes.Add((WorldObjects.IdOf(c), list));
                }
                catch { }
            }
            var bodies = new List<(string name, Vector3 pos, float cond)>();
            foreach (var bh in UnityEngine.Object.FindObjectsOfType<BodyHarvest>())
            {
                if (bh == null || !bh.gameObject.activeInHierarchy) continue;
                try { bodies.Add((Clean(bh.gameObject.name), bh.transform.position, bh.m_Condition)); } catch { }
            }
            if (items.Count == 0 && boxes.Count == 0 && bodies.Count == 0 && drying.Count == 0) return;
            Send(KDecay, ItemSync.SceneKey(), w =>
            {
                w.Write(items.Count);
                foreach (var it in items) { w.Write(it.name); w.Write(it.pos.x); w.Write(it.pos.y); w.Write(it.pos.z); w.Write(it.hp); }
                w.Write(boxes.Count);
                foreach (var b in boxes)
                {
                    w.Write(b.id);
                    w.Write(b.list.Count);
                    foreach (var it in b.list) { w.Write(it.name); w.Write(it.hp); }
                }
                w.Write(bodies.Count);
                foreach (var b in bodies) { w.Write(b.name); w.Write(b.pos.x); w.Write(b.pos.y); w.Write(b.pos.z); w.Write(b.cond); }
                w.Write(drying.Count);
                foreach (var d in drying) { w.Write(d.name); w.Write(d.pos.x); w.Write(d.pos.y); w.Write(d.pos.z); w.Write(d.hours); }
            });
        }

        private static bool FixHp(GearItem gi, float want)
        {
            float have = gi.GetNormalizedCondition();
            if (Math.Abs(have - want) < 0.01f || have <= 0f) return false;
            gi.CurrentHP = gi.CurrentHP * (want / have);
            return true;
        }

        private static void ApplyDecay(BinaryReader r)
        {
            if (Session.Mode != Mode.Client) return;
            int n = r.ReadInt32();
            var byName = new Dictionary<string, List<GearItem>>();
            foreach (var gi in UnityEngine.Object.FindObjectsOfType<GearItem>())
            {
                if (gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer) continue;
                string name = ItemSync.PrefabName(gi);
                if (!byName.TryGetValue(name, out var l)) byName[name] = l = new List<GearItem>();
                l.Add(gi);
            }
            int fixedN = 0;
            for (int i = 0; i < n; i++)
            {
                string name = r.ReadString();
                var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                float want = r.ReadUInt16() / 65535f;
                if (!byName.TryGetValue(name, out var list)) continue;
                GearItem best = null;
                float bd = 0.3f * 0.3f;
                foreach (var gi in list)
                {
                    float d = (gi.transform.position - pos).sqrMagnitude;
                    if (d < bd) { bd = d; best = gi; }
                }
                if (best == null) continue;
                try { if (FixHp(best, want)) fixedN++; } catch { }
            }
            if (r.BaseStream.Position < r.BaseStream.Length)
            {
                int nb = r.ReadInt32();
                for (int b = 0; b < nb; b++)
                {
                    string id = r.ReadString();
                    int cnt = r.ReadInt32();
                    var want = new List<(string name, float hp)>();
                    for (int i = 0; i < cnt; i++) want.Add((r.ReadString(), r.ReadUInt16() / 65535f));
                    var c = WorldObjects.Find<Container>(id);
                    if (c == null || (WorldObjects.OpenContainer != null && WorldObjects.OpenContainer.Pointer == c.Pointer)) continue;
                    try
                    {
                        var got = new Il2CppSystem.Collections.Generic.List<GearItem>();
                        c.GetItems(got);
                        var used = new bool[got.Count];
                        foreach (var wnt in want)
                        {
                            for (int i = 0; i < got.Count; i++)
                            {
                                if (used[i] || got[i] == null || ItemSync.PrefabName(got[i]) != wnt.name) continue;
                                used[i] = true;
                                if (FixHp(got[i], wnt.hp)) fixedN++;
                                break;
                            }
                        }
                    }
                    catch { }
                }
                int nc = r.ReadInt32();
                var all = UnityEngine.Object.FindObjectsOfType<BodyHarvest>();
                for (int b = 0; b < nc; b++)
                {
                    string name = r.ReadString();
                    var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    float cond = r.ReadSingle();
                    BodyHarvest best = null;
                    float bd = 1f;
                    foreach (var bh in all)
                    {
                        if (bh == null || Clean(bh.gameObject.name) != name) continue;
                        float d = (bh.transform.position - pos).sqrMagnitude;
                        if (d < bd) { bd = d; best = bh; }
                    }
                    if (best == null || AnimalSync.HarvestFresh(best)) continue;   // свежая туша от игрока новее этого износа
                    try { if (Math.Abs(best.m_Condition - cond) > 1f) { best.m_Condition = cond; fixedN++; } } catch { }
                }
            }
            if (r.BaseStream.Position < r.BaseStream.Length)
            {
                int nd = r.ReadInt32();
                int dried = 0;
                for (int i = 0; i < nd; i++)
                {
                    string name = r.ReadString();
                    var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    float hours = r.ReadSingle();
                    if (!byName.TryGetValue(name, out var list)) continue;
                    foreach (var gi in list)
                    {
                        if ((gi.transform.position - pos).sqrMagnitude > 0.09f) continue;
                        try
                        {
                            var ev = gi.m_EvolveItem;
                            if (ev != null && Math.Abs(ev.m_TimeSpentEvolvingGameHours - hours) > 0.25f) { ev.m_TimeSpentEvolvingGameHours = hours; dried++; }
                        }
                        catch { }
                        break;
                    }
                }
                if (dried > 0) MelonLogger.Msg($"[износ] сушка на полу как у хоста: поправлено {dried}");
            }
            if (fixedN > 0) MelonLogger.Msg($"[износ] состояние вещей и туш как у хоста: поправлено {fixedN}");
        }

        // ---------- Сеть ----------

        public static void Receive(BinaryReader r, byte[] raw, Peer from)
        {
            byte kind = r.ReadByte();
            string scene = r.ReadString();
            if (MapShare.Handles(kind) || RandomObjects.Handles(kind))
            {
                if (!Active) Hold(kind, raw, from);
                else if (MapShare.Handles(kind)) MapShare.Receive(scene, r);
                else RandomObjects.Receive(scene, r, from);
                return;
            }
            if (WorldMechanics.ReceiveEarly(kind, scene, r)) return;
            if (from != null) WorldMechanics.HostObserve(kind, scene, raw);
            if (kind == KBeachAsk)
            {
                if (Session.Mode == Mode.Host && from != null)
                {
                    try { var cg = CougarMsg(); if (cg != null) from.Send(cg); } catch { }
                    try { MapShare.SendSceneTo(from, scene); } catch { }
                    try { WorldMechanics.SendSceneTo(from, scene); } catch { }
                }
                if (Session.Mode == Mode.Host && from != null && BeachByScene.TryGetValue(scene, out var list))
                {
                    var w = Msg.New(MsgType.WorldEvent);
                    w.Write(KBeachBig);
                    w.Write(scene);
                    w.Write(list);
                    from.Send(Msg.Done(w));
                }
                return;
            }
            // На экране загрузки (дверь, переезд, вход в мир хоста) не выбрасываем: журнал уже счёл событие доставленным.
            if (!Active && Ignored) return;
            if (kind == KCougar || kind == KChimney)
            {
                if (!Active) Hold(kind, raw, from);
                else if (kind == KCougar) Apply(kind, r);
                else ApplyChimney(r);   // труба в другой локации, не в той, где огонь
                return;
            }
            if (!Active || scene != _settled || !ItemSync.Settled || ItemSync.SceneKey() != scene)
            {
                if (kind == KBeachBig || kind == KScare || kind == KDecay) return;   // список придёт заново, когда зайдём в локацию; испуг — только сейчас
                if (!Pending.TryGetValue(scene, out var p)) Pending[scene] = p = new List<byte[]>();
                // Из нескольких событий об одном объекте нужно только последнее.
                string key = CompactKey(raw);
                if (key != null) p.RemoveAll(old => CompactKey(old) == key);
                p.Add(raw);
                return;
            }
            _applyRaw = raw;
            try { Apply(kind, r); } finally { _applyRaw = null; }
        }

        private static string CompactKey(byte[] raw)
        {
            try
            {
                using var r = Msg.Read(raw, out _);
                byte kind = r.ReadByte();
                r.ReadString();
                switch (kind)
                {
                    case KDeco: case KRock: r.ReadBoolean(); return kind + "|" + r.ReadString();
                    case KCuring: return kind + "|" + r.ReadString();
                    case 12: return kind + "|" + r.ReadByte();
                    case 13: r.ReadByte(); return kind + "|" + r.ReadString();
                }
            }
            catch { }
            return null;
        }

        public static void ReceiveRaw(byte[] raw)
        {
            using var r = Msg.Read(raw, out _);
            Receive(r, raw, null);
        }

        private static void ApplyRaw(byte[] raw)
        {
            using var r = Msg.Read(raw, out _);
            byte kind = r.ReadByte();
            r.ReadString();
            _applyRaw = raw;
            try { Apply(kind, r); } finally { _applyRaw = null; }
        }

        private static void Apply(byte kind, BinaryReader r)
        {
            try
            {
                if (kind == KCarcassGone) ApplyCarcassGone(r);
                else if (kind == KBeachBig) ApplyBeach(r);
                else if (kind == KDeco) ApplyDeco(r);
                else if (kind == KCougar) ApplyCougar(r);
                else if (kind == KScare) ApplyScare(r);
                else if (kind == KCuring) ApplyCuring(r);
                else if (kind == KRock) ApplyRock(r);
                else if (kind == KDecay) ApplyDecay(r);
                else if (WorldMechanics.Handles(kind)) WorldMechanics.Apply(kind, r);
            }
            catch (Exception e) { MelonLogger.Warning("[мир] событие: " + e.Message); }
        }

        public static void ClearPending(string scene) => Pending.Remove(scene);

        // Хост: отложенное для друга, который только что получил мир.
        public static IEnumerable<byte[]> AllPending()
        {
            foreach (var list in Pending.Values)
                foreach (var raw in list) yield return raw;
        }

        public static void Reset()
        {
            Chimneys.Clear();
            Pending.Clear();
            Held.Clear();
            BeachByScene.Clear();
            BeachCopies.Clear();
            _settled = "";
            _beachSent = null;
            _qBefore = null;
            _qAt = -1f;
            DecoQueue.Clear();
            DecoHidden.Clear();
            DecoWaits.Clear();
            DecoLoads.Clear();
            DecoPrefabs.Clear();
            _cougarSent = null;
            WorldMechanics.Reset();
        }
    }

    [HarmonyPatch(typeof(Panel_BodyHarvest), nameof(Panel_BodyHarvest.QuarterSuccessful))]
    internal static class Patch_QuarterSuccessful
    {
        private static void Prefix(Panel_BodyHarvest __instance) => WorldEvents.QuarterStart(__instance);
        private static void Postfix() => WorldEvents.QuarterDone();
    }

    [HarmonyPatch(typeof(CuringStorage), nameof(CuringStorage.CloseCuringStorage))]
    internal static class Patch_CuringClose
    {
        private static void Postfix(CuringStorage __instance) => WorldEvents.OnCuringClosed(__instance);
    }

    [HarmonyPatch(typeof(RockCacheManager), nameof(RockCacheManager.Add))]
    internal static class Patch_RockAdd
    {
        private static void Postfix(RockCache rc) => WorldEvents.OnRockAdded(rc);
    }

    [HarmonyPatch(typeof(RockCacheManager), nameof(RockCacheManager.TrackDestroyedRockCacheInScene))]
    internal static class Patch_RockDestroyed
    {
        private static void Postfix(string guid) => WorldEvents.OnRockDestroyed(guid);
    }

    [HarmonyPatch(typeof(GunItem), nameof(GunItem.Fired))]
    internal static class Patch_ScareGun
    {
        private static void Postfix()
        {
            try { var p = GameManager.GetPlayerTransform(); if (p != null) WorldEvents.OnScare(p.position, WorldEvents.Scare.Shot); } catch { }
        }
    }

    [HarmonyPatch(typeof(NoiseMakerItem), nameof(NoiseMakerItem.PerformDetonation))]
    internal static class Patch_ScareNoise
    {
        private static void Postfix(NoiseMakerItem __instance)
        {
            if (ItemSync.Applying) return;
            try { WorldEvents.OnScare(__instance.transform.position, WorldEvents.Scare.NoiseMaker); } catch { }
        }
    }

    // Ракета упала: игра пугает зверей вокруг места попадания (MaybeScareWildlife из HandleCollisionWithObject).
    // Прежний патч на FlareGunRoundManager.AddFlareGunRoundItem в 2.59 встроен в Awake и не вызывался.
    [HarmonyPatch(typeof(FlareGunRoundItem), nameof(FlareGunRoundItem.MaybeScareWildlife))]
    internal static class Patch_ScareFlare
    {
        private static void Postfix(FlareGunRoundItem __instance)
        {
            if (Session.Mode == Mode.Off) return;
            try { if (__instance != null && !FlareShots.IsRemote(__instance)) WorldEvents.OnScare(__instance.transform.position, WorldEvents.Scare.Flare); } catch { }
        }
    }

    [HarmonyPatch(typeof(CougarManager), nameof(CougarManager.SetCougarArrived))]
    internal static class Patch_CougarArrived
    {
        private static bool Prefix() => WorldEvents.AllowCougarArrival();
    }

    [HarmonyPatch(typeof(CougarManager), nameof(CougarManager.MaybeCougarArrivesAfterSleep))]
    internal static class Patch_CougarAfterSleep
    {
        private static bool Prefix() => WorldEvents.AllowCougarArrival();
    }

    [HarmonyPatch(typeof(SafehouseManager), nameof(SafehouseManager.PlayPlaceAudio))]
    internal static class Patch_DecoPlace
    {
        private static void Postfix(DecorationItem di) => WorldEvents.OnDecoPlaced(di);
    }

    // Поставил вещь в режиме установки (украшение, взятое для перестановки, идёт именно сюда — ни звук установки,
    // ни FinalizePlacement при этом не срабатывали, и у другого украшение пропадало).
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.PlaceMeshInWorld))]
    internal static class Patch_DecoPlaceMesh
    {
        private static void Prefix(PlayerManager __instance, out GameObject __state)
        {
            __state = null;
            try { __state = __instance.GetObjectToPlace(); } catch { }
        }

        private static void Postfix(GameObject __state)
        {
            if (__state == null) return;
            try
            {
                var di = __state.GetComponent<DecorationItem>() ?? __state.GetComponentInChildren<DecorationItem>();
                if (di != null) WorldEvents.OnDecoPlaced(di);
            }
            catch { }
        }
    }

    // Переставил украшение (взял и поставил в режиме обустройства): звук установки при этом не играет,
    // и у других украшение пропадало — «убрал» уходило, «поставил» нет. Конец установки ловим здесь.
    [HarmonyPatch(typeof(DecorationItem), nameof(DecorationItem.FinalizePlacement))]
    internal static class Patch_DecoFinalize
    {
        private static void Postfix(DecorationItem __instance, bool success)
        {
            if (success) WorldEvents.OnDecoPlaced(__instance);
        }
    }

    // Начало установки (в том числе перестановки украшения): запоминаем вещи вокруг, пока они на своих местах.
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.InitPlaceMeshMode))]
    internal static class Patch_DecoPlaceStart
    {
        private static void Prefix()
        {
            try { var t = GameManager.GetPlayerTransform(); if (t != null) WorldEvents.SnapSurfaceItems(t.position, 12f); } catch { }
        }
    }

    // Убрал украшение в рюкзак: игра опускает вещи с него на пол (BreakDown.StickSurfaceObjectsToGround).
    [HarmonyPatch(typeof(DecorationItem), nameof(DecorationItem.OnPickup))]
    internal static class Patch_DecoPickupSurface
    {
        private static void Prefix(DecorationItem __instance)
        {
            try { WorldEvents.SnapSurfaceItems(__instance.transform.position, 4f); } catch { }
        }

        private static void Postfix() => WorldEvents.SendMovedSurfaceItemsSoon();
    }

    [HarmonyPatch(typeof(SafehouseManager), nameof(SafehouseManager.PlayDropAudio))]
    internal static class Patch_DecoDrop
    {
        private static void Postfix(DecorationItem di) => WorldEvents.OnDecoPlaced(di);
    }

    [HarmonyPatch(typeof(SafehouseManager), nameof(SafehouseManager.PlayStowAudio))]
    internal static class Patch_DecoStow
    {
        private static void Prefix(DecorationItem di) => WorldEvents.OnDecoRemoved(di);
    }

    [HarmonyPatch(typeof(SafehouseManager), nameof(SafehouseManager.PlayDestroyAudio))]
    internal static class Patch_DecoDestroy
    {
        private static void Prefix(DecorationItem di) => WorldEvents.OnDecoRemoved(di);
    }

    [HarmonyPatch(typeof(BeachcombingSpawner), nameof(BeachcombingSpawner.DrawAndPlaceBigItems))]
    internal static class Patch_BeachBigPlace
    {
        private static bool Prefix() => WorldEvents.AllowBigItems();
    }

    [HarmonyPatch(typeof(BeachcombingSpawner), nameof(BeachcombingSpawner.ExpireBigItems))]
    internal static class Patch_BeachBigExpire
    {
        private static bool Prefix() => WorldEvents.AllowBigItems();
    }
}
