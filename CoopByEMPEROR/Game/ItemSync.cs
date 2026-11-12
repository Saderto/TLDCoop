using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace CoopByEMPEROR.Game
{
    // Предметы на земле. Кто-то выбросил предмет — у остальных он появляется в том же месте
    // с тем же состоянием (сериализация игры: SerializeToString / InstantiateAndDeserializeGearItem).
    // Кто-то поднял — у остальных он исчезает. Хост пересылает события всем и держит события
    // для локаций, где сейчас никого нет из получателей (Pending), — они применяются при входе.
    //
    // Локация = m_SceneSaveFilenameCurrent: у одинаковых домиков (LakeCabinA) разные экземпляры.
    // Предмет узнаётся по сетевому id (если его выбросили при моде) или по имени и месту
    // (предметы из сохранения у всех на одних и тех же местах, потому что мир один).
    public static class ItemSync
    {
        private static readonly Dictionary<uint, GearItem> ById = new();
        private static readonly Dictionary<IntPtr, uint> IdOf = new();
        // Счётчик с случайного места: друг, вернувшийся после вылета с тем же номером игрока, иначе выдавал бы
        // своим новым вещам номера старых, которые у других ещё лежат.
        private static uint _counter = (uint)new System.Random().Next(1, 0xFFFFFF);
        private static bool _applying;

        private class Pending
        {
            public MsgType Type;
            public byte[] Raw;
        }
        private static readonly Dictionary<string, List<Pending>> PendingByScene = new();
        private static string _appliedScene = "";
        private static float _sceneSeen;
        private static int _sceneSeenFrame;
        private static string _seenScene = "";

        public static string SceneKey()
        {
            try
            {
                string k = GameManager.m_SceneTransitionData?.m_SceneSaveFilenameCurrent;
                return string.IsNullOrEmpty(k) ? GameApi.Scene() : k;
            }
            catch { return GameApi.Scene(); }
        }

        // Текущая локация загрузилась, отложенные события в ней применены.
        public static bool Settled => _appliedScene.Length > 0 && _appliedScene == SceneKey();
        public static string AppliedScene => _appliedScene;   // последняя устоявшаяся локация (при уходе из неё — та, откуда уходим)

        internal static bool Applying { get => _applying; set => _applying = value; }

        public static IEnumerable<byte[]> PendingFor(string scene)
        {
            if (PendingByScene.TryGetValue(scene, out var list))
                foreach (var p in list) yield return p.Raw;
        }

        public static void ClearPending(string scene) => PendingByScene.Remove(scene);

        private static bool Active => Session.Mode != Mode.Off && !_applying && GameApi.InGame() && Session.InSharedWorld;

        public static bool Quiet;   // вещь кладёт в рюкзак сам мод (подарок друга), это не подбор с земли

        // Котелок сам двигает еду (CookingPotItem.StartCooking роняет её из рюкзака и вешает на котелок,
        // PickUpCookedGearItem создаёт готовую «под ногами» и кладёт в рюкзак): это не выброс и не подбор.
        // Раньше у остальных появлялась свободная копия еды, а её подбор по номеру удалял еду из котелка у повара.
        internal static int PotWork;

        // Котелок кладёт мне «к ногам» вещь для экрана осмотра (CookingPotItem.MaybeSpawnGearItemForInspectMode: кружка чая,
        // вода, банка, лапша). Её видит только осматривающий, это не выброс. Раньше она уходила остальным как выброшенная
        // (в самом вызове котелок ещё не записал её в m_GearItemSpawnedForInspectMode) и оставалась у них лежать под ногами
        // забравшего — «две кружки под тем, кто забрал чай» (0.19.41).
        internal static int PotInspectSpawn;
        private static int _inspectSpawnLogged;
        private static void NoteInspectSpawn(GearItem gi)
        {
            if (_inspectSpawnLogged++ < 10) MelonLogger.Msg($"[котелок] {PrefabName(gi)} для осмотра котелка у ног — не выброс, другим не шлю");
        }

        // Вещь лежит в котелке (дочерняя у котелка): её состояние несёт замена котелка, по номеру и месту её не трогаем.
        internal static bool InPot(GearItem gi)
        {
            try
            {
                var p = gi != null ? gi.transform.parent : null;
                if (p != null && p.GetComponentInParent<CookingPotItem>() != null) return true;
                return WorldExtra.IsPotInspectItem(gi);   // вода «для осмотра» котелка — тоже не вещь мира
            }
            catch { return false; }
        }

        // Котелок начал готовить вещь, которая лежала в мире под сетевым номером (у остальных — её свободная копия):
        // у остальных копия убирается, у нас номер забывается.
        internal static void OnStartedCooking(CookingPotItem pot)
        {
            if (!Active || pot == null) return;
            var food = pot.m_GearItemBeingCooked;
            if (food == null || !IdOf.TryGetValue(food.Pointer, out uint id) || id == 0) return;
            Forget(food);
            NotePicked(id);
            var w = Msg.New(MsgType.ItemPick);
            w.Write(id);
            w.Write(SceneKey());
            w.Write(PrefabName(food));
            WriteVec(w, food.transform.position);
            Session.SendGameEvent(Msg.Done(w));
        }

        // Можно ли создать вещь у нас: вещь из DLC или мода, которых у нас нет (FoodPackByEMPEROR на DLC-префабе),
        // игра не создаёт. Проверяем один раз на имя.
        private static readonly Dictionary<string, bool> CanCreate = new();

        internal static bool Creatable(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return true;
            if (CanCreate.TryGetValue(prefab, out bool ok)) return ok;
            ok = false;
            bool q = Quiet;
            Quiet = true;
            try
            {
                var gi = GearItem.InstantiateGearItem(prefab);
                ok = gi != null;
                if (gi != null) { gi.gameObject.SetActive(false); UnityEngine.Object.Destroy(gi.gameObject); }
            }
            catch { ok = false; }
            finally { Quiet = q; }
            CanCreate[prefab] = ok;
            if (!ok) MelonLogger.Msg($"[предметы] {prefab}: у меня не создаётся (нет DLC или мода)");
            return ok;
        }

        public static string PrefabName(GearItem gi)
        {
            string n = gi.name ?? "";
            n = Regex.Replace(n, @"\(Clone\)", "");
            n = Regex.Replace(n, @"\s*\(\d+\)$", "");
            return n.Trim();
        }

        private static uint NewId() => ((uint)(Session.LocalId & 0xFF) << 24) | (++_counter & 0xFFFFFF);

        // Вещь по номеру — только если это та же вещь (тот же префаб и рядом с местом из события): номер мог остаться
        // от прошлого запуска игры отправителя и принадлежать другой вещи. near <= 0 — место не проверять.
        // Радиус большой: брошенная вещь у каждого летит сама и может лечь в десятке метров от места у бросившего.
        private const float KnownNear = 50f;

        private static GearItem Known(uint id, string prefab, Vector3 a, Vector3 b, float near)
        {
            if (id == 0 || !ById.TryGetValue(id, out var gi) || gi == null) return null;
            try
            {
                if (PrefabName(gi) != prefab) return null;
                if (near > 0f)
                {
                    var p = gi.transform.position;
                    if ((p - a).sqrMagnitude > near * near && (p - b).sqrMagnitude > near * near) return null;
                }
                return gi;
            }
            catch { return null; }
        }

        // Номера поднятых вещей (свой подбор и чужой): поздняя замена такой вещи (бросивший через секунду шлёт
        // точное место) не создаёт копию у того, кто её уже поднял, а хост такую замену дальше не шлёт.
        private static readonly Dictionary<uint, float> Picked = new();

        private static void NotePicked(uint id)
        {
            if (id == 0) return;
            float now = Time.unscaledTime;
            Picked[id] = now;
            if (Picked.Count > 500)
            {
                var old = new List<uint>();
                foreach (var kv in Picked) if (now - kv.Value > 30f) old.Add(kv.Key);
                foreach (var k in old) Picked.Remove(k);
            }
        }

        public static bool PickedRecently(byte[] raw)
        {
            uint id = IdIn(raw);
            return id != 0 && Picked.TryGetValue(id, out float at) && Time.unscaledTime - at < 30f;
        }

        // Вещь уничтожает мод (выравнивание локации): забыть её номер, иначе новая вещь по тому же адресу памяти
        // получила бы чужой номер.
        internal static void Forget(GearItem gi)
        {
            if (gi == null) return;
            if (IdOf.TryGetValue(gi.Pointer, out uint id)) { IdOf.Remove(gi.Pointer); ById.Remove(id); }
            SentPos.Remove(gi.Pointer);
            Sender.Remove(gi.Pointer);
            ThrownAt.Remove(gi.Pointer);
        }

        // Номер вещи из раскладки хоста (0.19.39): после выравнивания у обоих одна вещь под одним номером, и подбор
        // находит её по номеру, а не по месту в 0,5 м. Раньше вещи, добавленные или оставленные выравниванием, были без
        // номера, и в куче одинаковых (палки, патроны, кишки) подбор у другого часто не находил свою: у него вещь
        // оставалась, а сверка потом клала её поднявшему ещё раз.
        internal static void AdoptId(GearItem gi, uint id)
        {
            if (gi == null || id == 0) return;
            if (IdOf.TryGetValue(gi.Pointer, out uint had))
            {
                if (had == id) return;
                ById.Remove(had);
            }
            if (ById.TryGetValue(id, out var other) && other != null && other.Pointer != gi.Pointer) IdOf.Remove(other.Pointer);
            ById[id] = gi;
            IdOf[gi.Pointer] = id;
            _adopted++;
        }

        // Хост: номер для раскладки. Вещи без номера (лежали в локации с самого начала) получают его сейчас.
        internal static uint IdForLayout(GearItem gi)
        {
            if (gi == null) return 0;
            if (IdOf.TryGetValue(gi.Pointer, out uint id) && id != 0) return id;
            if (Session.Mode != Mode.Host) return 0;
            try { if (WorldExtra.IsWatched(gi)) return 0; } catch { }   // ещё летит после броска: номер даст её выброс
            id = NewId();
            ById[id] = gi;
            IdOf[gi.Pointer] = id;
            return id;
        }

        internal static int _adopted, _pickNear;

        private static void Track(uint id, GearItem gi)
        {
            if (gi == null) return;
            ById[id] = gi;
            IdOf[gi.Pointer] = id;
            SentPos[gi.Pointer] = gi.transform.position;
        }

        // Где вещь лежит у всех (последнее, что мы о ней отправили или получили).
        private static readonly Dictionary<IntPtr, Vector3> SentPos = new();

        // Кто последним прислал эту вещь (номер игрока из её id), -1 — неизвестно.
        // Чья вещь по нашим записям: только тот, кто её прислал или переслал (без догадки по номеру).
        internal static bool HasSender(GearItem gi) => gi != null && Sender.ContainsKey(gi.Pointer);

        // 0.19.57: котелок ведёт другой игрок (его отчёт пришёл), а у меня ведущий неизвестен — записываем его.
        internal static void AdoptSender(GearItem gi, int from)
        {
            if (gi != null && from >= 0) Sender[gi.Pointer] = from;
        }

        public static int OwnerOf(GearItem gi)
        {
            if (gi == null) return -1;
            if (Sender.TryGetValue(gi.Pointer, out int s)) return s;
            return IdOf.TryGetValue(gi.Pointer, out uint id) && id != 0 ? (int)(id >> 24) : -1;
        }

        public static bool TryGetSentPos(GearItem gi, out Vector3 pos)
        {
            pos = default;
            return gi != null && IdOf.ContainsKey(gi.Pointer) && SentPos.TryGetValue(gi.Pointer, out pos);
        }

        // Вещь в мире поменялась у нас (силок сработал, котелок закипел, вещь переставили): у остальных заменяем
        // их копию нашей под тем же номером. Раньше это были подбор и новый выброс с новым номером: если другой
        // в ту же секунду брал котелок или доливал воду, его событие шло на старый номер — котелок двоился или
        // пропадал, а подбор-«замена» ещё и занимал вещь у хоста на 30 с (второй не мог её взять).
        public static void Resend(GearItem gi, Vector3 oldPos)
        {
            if (!Active || gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer || IsPersonal(gi) || FlareShots.IsFiredRound(gi)) return;
            // Вещь для осмотра котелка у ног (её сдвинула физика): это не вещь мира.
            if (PotInspectSpawn > 0 || (gi.GetComponent<CookingPotItem>() == null && InPot(gi))) return;
            // Вещь у меня на экране осмотра (взял с земли или с печки и смотрю): игра держит её перед камерой, и у остальных
            // она повисала в воздухе у лица моей фигуры (0.19.27, сковородка). Шлём, когда осмотр кончится и вещь останется в мире.
            if (InspectingByMe(gi))
            {
                if (!DeferredResend.ContainsKey(gi.Pointer)) DeferredResend[gi.Pointer] = new Deferred { Gi = gi, OldPos = Inspecting != null && Inspecting.Pointer == gi.Pointer ? InspectPos : oldPos };
                return;
            }
            try
            {
                IdOf.TryGetValue(gi.Pointer, out uint id);
                if (id == 0) id = NewId();
                Track(id, gi);
                Sender[gi.Pointer] = Session.LocalId;
                var t = gi.transform;
                if (Session.Mode == Mode.Host) FreePlace(SceneKey(), PrefabName(gi), t.position);
                var w = Msg.New(MsgType.ItemUpdate);
                w.Write(id);
                w.Write(SceneKey());
                w.Write(PrefabName(gi));
                WriteVec(w, t.position);
                WriteQuat(w, t.rotation);
                w.Write(gi.SerializeToString() ?? "");
                WriteVec(w, oldPos);
                w.Write((byte)Session.LocalId);
                // Что готовится в котелке: чай, кружка в «кружке-подставке» (GEAR_CookingPotDummy), еда из рюкзака.
                // Этой вещи у остальных нет (она была в рюкзаке у готовящего), котелок ищет её по GUID и без неё пуст,
                // а подставка без вещи сразу уничтожает себя (CookingPotItem.Update → DestroyGearObject).
                GearItem food = null;
                try { food = gi.GetComponent<CookingPotItem>()?.m_GearItemBeingCooked; } catch { }
                if (food != null)
                {
                    w.Write(true);
                    w.Write(PrefabName(food));
                    w.Write(food.SerializeToString() ?? "");
                    w.Write(ObjectGuid.MaybeGetGuidFromGameObject(food.gameObject) ?? "");
                }
                else w.Write(false);
                Session.SendGameEvent(Msg.Done(w));
            }
            catch (Exception e) { MelonLogger.Warning("[предметы] замена: " + e.Message); }
        }

        // Кто последним прислал замену вещи (котелок шлёт тот, кто последним менял его или стоит у него).
        private static readonly Dictionary<IntPtr, int> Sender = new();

        // Замена котелка, пока у нас открыто окно готовки у этого котелка: применяем, когда окно закроется,
        // и только если мы сами за это время его не поменяли (тогда наша замена новее и уйдёт остальным).
        private class HeldUpdate { public byte[] Raw; public string Sig; }
        private static readonly Dictionary<uint, HeldUpdate> HeldUpdates = new();

        // ---------- Свои действия ----------

        // Квестовые вещи (Tales from the Far Territory и другие задания) и записки-находки у каждого игрока свои:
        // их подбор и выброс другим не отправляем, чужие такие вещи не принимаем.
        // Создать вещь по чужому состоянию. Игра при первом запуске вещи (GearItem.ManualStart) бросает шанс появления
        // (GameManager.RollSpawnChance, зерно — место вещи и зерно мира) и при неудаче выключает её. У друга этот бросок
        // выходил не таким, как у хоста: книга «Растопка», потом «Кулинария» в Конторе лагеря добавлялись выравниванием
        // и тут же выключались — «у Талян нет» каждые 20 с. Пришедшая вещь уже прошла бросок у того, кто её прислал.
        internal static int SpawnRollSkipped, ModeLimitRemoved, TagsCleared;
        public static GearItem Spawn(string prefab, string json, Transform list, bool a, bool b)
        {
            var gi = GearItem.InstantiateAndDeserializeGearItem(prefab, json, list, a, b);
            if (gi != null) { try { if (!gi.m_RolledSpawnChance) { gi.m_RolledSpawnChance = true; SpawnRollSkipped++; } } catch { } }
            // Главная причина пропадавших книг (0.19.17, лог друга): у книг есть MissionObjectIdentifier с метками занятий
            // (m_ActivityTags). MissionServicesManager.RegisterMissionObject выключает объект, если его метки не входят в
            // разрешённые сейчас у этого игрока (у друга свои задания и Байки), — так у друга выключались «Растопка»,
            // «Кулинария», «Огнестрельное оружие». У того, кто прислал, вещь есть: снимаем метки у безымянных (не квестовых)
            // объектов, тогда проверка меток не делается. Регистрация идёт из очереди позже, мы успеваем.
            if (gi != null)
            {
                try
                {
                    var moi = gi.GetComponent<MissionObjectIdentifier>();
                    if (moi != null && moi.m_IsBlankId && moi.m_NumTags != 0) { moi.m_NumTags = 0; moi.m_ActivityTags = ""; TagsCleared++; }
                }
                catch { }
            }
            // Компоненты «только для такого-то режима/сложности» у копии из префаба выключают вещь в своём Update.
            // У того, кто прислал, вещь есть — значит, и у нас она должна быть.
            if (gi != null)
            {
                try
                {
                    foreach (var comp in gi.GetComponentsInChildren<Component>(true))
                    {
                        if (comp == null) continue;
                        string tn = comp.GetIl2CppType().Name;
                        if (tn.StartsWith("DisableObjectFor") || tn.StartsWith("EnableObjectFor")) { UnityEngine.Object.Destroy(comp); ModeLimitRemoved++; }
                    }
                }
                catch { }
            }
            PruneDeadRenderers(gi);
            return gi;
        }

        public static bool IsPersonal(GearItem gi)
        {
            if (gi == null) return false;
            try
            {
                var mo = gi.m_MissionObject;
                if (mo != null && !mo.m_IsBlankId && !string.IsNullOrEmpty(mo.m_Id)) return true;
                return gi.m_NarrativeCollectibleItem != null;
            }
            catch { return false; }
        }

        // Готовое из котелка или подставки игра создаёт у меня под ногами (PickUpCookedGearItem →
        // InstantiateItemAtPlayersFeet) и сразу кладёт в рюкзак: это мой подбор у костра. Если у другого после этого
        // осталась такая же вещь на земле (у друзей — рыба с костра), сверка не положит её мне второй раз, а уберёт
        // у него (TakeMyRecentPick).
        internal static void NotePotTake(GearItem made)
        {
            if (PotWork <= 0 || made == null || !Active) return;
            try
            {
                NoteMyPick(SceneKey(), PrefabName(made), made.transform.position);
                MelonLogger.Msg($"[котелок] забрал готовое: {PrefabName(made)}");
            }
            catch { }
        }

        public static bool OnLocalDrop(GearItem dropped) => OnLocalDrop(dropped, null);

        // vel — начальная скорость брошенной вещи: у остальных она пролетит тот же путь.
        public static bool OnLocalDrop(GearItem dropped, Vector3? vel)
        {
            if (!Active || dropped == null || dropped.m_InPlayerInventory || dropped.m_InsideContainer || PotWork > 0 || InPot(dropped) || FlareShots.IsFiredRound(dropped)) return false;
            if (PotInspectSpawn > 0) { NoteInspectSpawn(dropped); return false; }
            if (IsPersonal(dropped)) { if (!Quiet) QuestItems.OnDropped(dropped); return false; }
            try
            {
                uint id = NewId();
                Track(id, dropped);
                var t = dropped.transform;
                if (Session.Mode == Mode.Host) FreePlace(SceneKey(), PrefabName(dropped), t.position);
                var w = Msg.New(MsgType.ItemDrop);
                w.Write(id);
                w.Write(SceneKey());
                w.Write(PrefabName(dropped));
                WriteVec(w, t.position);
                WriteQuat(w, t.rotation);
                w.Write(dropped.SerializeToString() ?? "");
                w.Write(vel.HasValue);
                if (vel.HasValue) { WriteVec(w, vel.Value); NoteThrown(dropped); }
                Session.SendGameEvent(Msg.Done(w));
                NoteDropLog(PrefabName(dropped) + UnitsText(dropped));
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[предметы] выброс: " + e.Message);
                return false;
            }
        }

        // Сколько чего выбросил за последнюю секунду: одной строкой в лог (скинуть много вещей — это много выбросов).
        private static readonly Dictionary<string, int> DropLog = new();
        private static float _dropLogAt;

        private static void NoteDropLog(string prefab)
        {
            if (DropLog.Count == 0) _dropLogAt = Time.unscaledTime;
            DropLog[prefab] = DropLog.TryGetValue(prefab, out int n) ? n + 1 : 1;
        }

        private static void TickDropLog(float now)
        {
            if (DropLog.Count == 0 || now - _dropLogAt < 1f) return;
            var parts = new List<string>();
            foreach (var kv in DropLog) parts.Add(kv.Value > 1 ? $"{kv.Key} ×{kv.Value}" : kv.Key);
            DropLog.Clear();
            MelonLogger.Msg("[предметы] выбросил: " + string.Join(", ", parts));
        }

        // Сколько штук в пачке (патроны, палки): « (4 шт)», для одиночных вещей пусто.
        internal static int UnitsOf(GearItem gi)
        {
            try { var st = gi != null ? gi.m_StackableItem : null; return st != null ? st.m_Units : 0; }
            catch { return 0; }
        }
        internal static string UnitsText(GearItem gi)
        {
            int n = UnitsOf(gi);
            return n > 1 ? $" ({n} шт)" : n == 1 && gi.m_StackableItem.m_DefaultUnitsInItem > 1 ? " (1 шт)" : "";
        }
        private static int _unitsLogged;

        public static void OnLocalPickup(GearItem gi, Vector3 pos)
        {
            if (!Active || gi == null) return;
            if (UnitsOf(gi) > 0 && _unitsLogged++ < 60) MelonLogger.Msg($"[предметы] поднял {PrefabName(gi)}{UnitsText(gi)}, шлю подбор");
            if (IsPersonal(gi)) { if (!_applying && !Quiet) QuestItems.OnTakenFromGround(gi, pos); return; }
            if (InPot(gi)) { Forget(gi); return; }   // из котелка: у остальных это несёт замена котелка
            IdOf.TryGetValue(gi.Pointer, out uint id);
            if (id != 0) { ById.Remove(id); IdOf.Remove(gi.Pointer); }
            NotePicked(id);
            SentPos.Remove(gi.Pointer);
            string prefab = PrefabName(gi), scene = SceneKey();
            string key = PickKey(id, scene, prefab, pos);
            if (Session.Mode == Mode.Host && !HostClaimPick(id, scene, prefab, pos, 0, out int holder))
            {
                // Друг взял эту вещь раньше (в ту же долю секунды): у хоста её не будет.
                Recent[key] = new RecentPick { Gi = gi, Prefab = prefab, Units = gi.m_StackableItem != null ? Math.Max(1, gi.m_StackableItem.m_Units) : 1, At = Time.unscaledTime };
                _denyLocal.Add((key, holder));
                return;
            }
            Recent[key] = new RecentPick { Gi = gi, Prefab = prefab, Units = gi.m_StackableItem != null ? Math.Max(1, gi.m_StackableItem.m_Units) : 1, At = Time.unscaledTime };
            NoteMyPick(scene, prefab, pos);
            var w = Msg.New(MsgType.ItemPick);
            w.Write(id);
            w.Write(scene);
            w.Write(prefab);
            WriteVec(w, pos);
            Session.SendGameEvent(Msg.Done(w));
        }

        // ---------- Одна вещь, поднятая двумя сразу ----------
        // Подбор не ждёт ответа, поэтому хост — судья: кто первым прислал подбор вещи, того она и есть.
        // Второму хост отвечает «уже взяли», и у него вещь убирается из рюкзака.

        private class RecentPick { public GearItem Gi; public string Prefab; public int Units; public float At; }
        private static readonly Dictionary<string, RecentPick> Recent = new();
        private static readonly Dictionary<string, (int who, float at)> Claims = new();
        private static readonly List<(string key, int who)> _denyLocal = new();

        private static string PickKey(uint id, string scene, string prefab, Vector3 p) => id != 0 ? "#" + id : PlaceKey(scene, prefab, p);

        private static string PlaceKey(string scene, string prefab, Vector3 p) =>
            $"{scene}|{prefab}|{Mathf.RoundToInt(p.x * 10)}|{Mathf.RoundToInt(p.y * 10)}|{Mathf.RoundToInt(p.z * 10)}";

        // Занять вещь и по номеру, и по месту: у одного игрока у вещи может быть сетевой номер, а у другого (выходил
        // из домика и вернулся) — нет, и ключи у них разные. false — вещь уже за другим (holder).
        private static bool HostClaimPick(uint id, string scene, string prefab, Vector3 pos, int who, out int holder)
        {
            holder = -1;
            string num = id != 0 ? "#" + id : null, place = PlaceKey(scene, prefab, pos);
            float now = Time.unscaledTime;
            foreach (string k in new[] { num, place })
                if (k != null && Claims.TryGetValue(k, out var c) && c.who != who && now - c.at < 30f) { holder = c.who; return false; }
            if (num != null) HostClaim(num, who);
            HostClaim(place, who);
            return true;
        }

        // Хост: на это место снова положили вещь (котелок обратно на плиту — то же место до сантиметра). Подбор
        // новой вещи — уже не тот, прежний, ключ места больше не держим, иначе второму откажут зря.
        private static void FreePlace(string scene, string prefab, Vector3 pos) => Claims.Remove(PlaceKey(scene, prefab, pos));

        private static bool HostClaim(string key, int who)
        {
            float now = Time.unscaledTime;
            if (Claims.TryGetValue(key, out var c) && c.who != who && now - c.at < 30f) return false;
            Claims[key] = (who, now);
            if (Claims.Count > 2000)
            {
                var old = new List<string>();
                foreach (var kv in Claims) if (now - kv.Value.at > 30f) old.Add(kv.Key);
                foreach (var k in old) Claims.Remove(k);
            }
            return true;
        }

        // Хост: подбор от друга. false — вещь уже взял другой, друг получил отказ.
        public static bool HostAcceptPick(Peer peer, byte[] raw)
        {
            uint id; string scene, prefab; Vector3 pos;
            using (var r = Msg.Read(raw, out _)) { id = r.ReadUInt32(); scene = r.ReadString(); prefab = r.ReadString(); pos = ReadVec(r); }
            string key = PickKey(id, scene, prefab, pos);
            if (HostClaimPick(id, scene, prefab, pos, peer.Id, out int holder)) return true;
            string by = holder == 0 ? Session.LocalName : (Session.Players.TryGetValue(holder, out var p) ? p.Name : "друг");
            var w = Msg.New(MsgType.PickDenied);
            w.Write(key);
            w.Write(by);
            peer.Send(Msg.Done(w));
            MelonLogger.Msg($"[предметы] {prefab}: два подбора сразу, вещь остаётся у {by}");
            return false;
        }

        public static void ReceiveDenied(BinaryReader r)
        {
            string key = r.ReadString(), by = r.ReadString();
            TakeBack(key, by);
        }

        // Убрать из своего рюкзака вещь, которую на самом деле взял другой.
        private static void TakeBack(string key, string by)
        {
            if (!Recent.TryGetValue(key, out var p)) return;
            Recent.Remove(key);
            var inv = GameManager.GetInventoryComponent();
            if (inv == null) return;
            Quiet = true;
            try
            {
                if (p.Gi != null && p.Gi.m_InPlayerInventory && (p.Gi.m_StackableItem == null || p.Gi.m_StackableItem.m_Units <= p.Units))
                    inv.DestroyGear(p.Gi);
                else
                {
                    // Вещь влилась в стопку: убираем столько штук, сколько подобрали.
                    int left = p.Units;
                    foreach (var o in inv.m_Items)
                    {
                        var gi = o?.m_GearItem;
                        if (gi == null || PrefabName(gi) != p.Prefab) continue;
                        var st = gi.m_StackableItem;
                        if (st == null || st.m_Units <= left) { left -= st != null ? st.m_Units : 1; inv.DestroyGear(gi); }
                        else { st.m_Units -= left; left = 0; }
                        if (left <= 0) break;
                    }
                }
                GameApi.HudMessage($"Эту вещь уже взял {by}");
                MelonLogger.Msg($"[предметы] {p.Prefab} ({p.Units} шт) уже взял {by} — убрал из своего рюкзака");
            }
            catch (Exception e) { MelonLogger.Warning("[предметы] вернуть вещь: " + e.Message); }
            finally { Quiet = false; }
        }

        // Хост: отказы самому себе разбираем в следующем кадре (вещь к этому времени уже в рюкзаке).
        private static void TickDenied()
        {
            if (_denyLocal.Count > 0)
            {
                foreach (var d in _denyLocal.ToArray()) TakeBack(d.key, Session.Players.TryGetValue(d.who, out var p) ? p.Name : "друг");
                _denyLocal.Clear();
            }
            // Старые подборы забываем у всех (раньше у друга список рос всю сессию). Ответ хоста «уже взяли»
            // приходит за доли секунды, 30 с — с запасом на переподключение.
            if (Recent.Count == 0) return;
            float now = Time.unscaledTime;
            if (now < _nextRecentClean) return;
            _nextRecentClean = now + 1f;
            var old = new List<string>();
            foreach (var kv in Recent) if (now - kv.Value.At > 30f) old.Add(kv.Key);
            foreach (var k in old) Recent.Remove(k);
        }

        private static float _nextRecentClean;

        // ---------- Чужие действия ----------

        // Возвращает true, если событие применено (локация загружена), иначе оно отложено.
        public static void Receive(MsgType type, byte[] raw)
        {
            try { SyncCheck.ItemEvent(); } catch { }
            if (Session.InOwnWorld) return;   // в своём мире события мира хоста не применяем и не копим
            if (type == MsgType.ItemPick) NotePicked(IdIn(raw));
            else if (type == MsgType.ItemUpdate && PickedRecently(raw)) return;   // вещь уже подняли: замена создала бы копию
            string scene;
            using (var r = Msg.Read(raw, out _))
            {
                r.ReadUInt32();
                scene = r.ReadString();
                if (Session.Mode == Mode.Host && type != MsgType.ItemPick)
                {
                    try { string prefab = r.ReadString(); FreePlace(scene, prefab, ReadVec(r)); } catch { }
                }
            }
            if (GameApi.InGame() && scene == SceneKey() && _appliedScene == scene)
            {
                Apply(type, raw);
                // Хост: событие друга применено в нашей локации — до сохранения игры помним его (Game/PlayerProgress.cs).
                if (Session.Mode == Mode.Host) PlayerProgress.NoteFriendApplied(raw);
            }
            else Defer(scene, type, raw);
        }

        private static void Defer(string scene, MsgType type, byte[] raw)
        {
            if (!PendingByScene.TryGetValue(scene, out var list)) PendingByScene[scene] = list = new List<Pending>();
            // Очередь не копим: вещь выбросили, а потом подняли (или переложили) в той же незагруженной
            // локации — выброс и подбор гасят друг друга.
            if (type == MsgType.ItemDrop)
            {
                uint did = IdIn(raw);
                if (did != 0 && list.Exists(p => p.Type == MsgType.ItemDrop && IdIn(p.Raw) == did)) return;   // тот же выброс второй раз
            }
            if (type == MsgType.ItemPick)
            {
                uint id = IdIn(raw);
                if (id != 0)
                {
                    list.RemoveAll(p => p.Type == MsgType.ItemUpdate && IdIn(p.Raw) == id);
                    int i = list.FindIndex(p => p.Type == MsgType.ItemDrop && IdIn(p.Raw) == id);
                    if (i >= 0) { list.RemoveAt(i); return; }
                }
            }
            // Замена: прежние замены той же вещи больше не нужны, нужна последняя.
            if (type == MsgType.ItemUpdate)
            {
                uint id = IdIn(raw);
                if (id != 0) list.RemoveAll(p => p.Type == MsgType.ItemUpdate && IdIn(p.Raw) == id);
            }
            list.Add(new Pending { Type = type, Raw = raw });
        }

        private static uint IdIn(byte[] raw)
        {
            try { using var r = Msg.Read(raw, out _); return r.ReadUInt32(); } catch { return 0; }
        }

        private static void Apply(MsgType type, byte[] raw)
        {
            _applying = true;
            try
            {
                using var r = Msg.Read(raw, out _);
                uint id = r.ReadUInt32();
                r.ReadString();
                string prefab = r.ReadString();
                var pos = ReadVec(r);
                if (type == MsgType.ItemDrop)
                {
                    // Тот же выброс второй раз (отложенное хоста и досылка журнала после передачи мира): вещь уже лежит.
                    // Только та же вещь (префаб): брошенная могла улететь далеко, место не сверяем.
                    var already = Known(id, prefab, pos, pos, 0f);
                    if (already != null && !already.m_InPlayerInventory) return;
                    var rot = ReadQuat(r);
                    string json = r.ReadString();
                    var gi = ItemSync.Spawn(prefab, json, null, false, false);
                    if (gi == null) { MelonLogger.Warning("[предметы] не создался " + prefab); return; }
                    if (IsPersonal(gi)) { UnityEngine.Object.Destroy(gi.gameObject); return; }
                    gi.transform.SetPositionAndRotation(pos, rot);
                    gi.gameObject.SetActive(true);
                    Track(id, gi);
                    GuardFall(gi, pos);
                    if (UnitsOf(gi) > 0 && _unitsLogged++ < 60) MelonLogger.Msg($"[предметы] чужой выброс {prefab}: положил{UnitsText(gi)}");
                    AttachToPlacePoint(gi);
                    // Бросок: летит так же, как у бросившего.
                    if (r.BaseStream.Position < r.BaseStream.Length && r.ReadBoolean())
                    {
                        var vel = ReadVec(r);
                        try
                        {
                            var rb = gi.GetComponent<Rigidbody>();
                            if (rb != null) { rb.isKinematic = false; rb.velocity = vel; rb.WakeUp(); }
                            NoteThrown(gi);
                            FallGuard.RemoveAll(f => f.Gi != null && f.Gi.Pointer == gi.Pointer);   // брошенная может лететь вниз
                        }
                        catch { }
                    }
                }
                else if (type == MsgType.ItemUpdate)
                {
                    var rot = ReadQuat(r);
                    string json = r.ReadString();
                    var oldPos = ReadVec(r);
                    int from = r.ReadByte();
                    string fPrefab = null, fJson = null, fGuid = null;
                    if (r.BaseStream.Position < r.BaseStream.Length && r.ReadBoolean()) { fPrefab = r.ReadString(); fJson = r.ReadString(); fGuid = r.ReadString(); }
                    GearItem cur = null;
                    cur = Known(id, prefab, oldPos, pos, KnownNear);
                    cur ??= Find(0, prefab, oldPos) ?? Find(0, prefab, pos);
                    // Мы её уже взяли: наш подбор под тем же номером уберёт её и у остальных.
                    if (cur != null && (cur.m_InPlayerInventory || cur.m_InsideContainer)) return;
                    if (cur != null && IsPersonal(cur)) return;
                    if (cur != null && InPot(cur)) return;   // у нас она уже в котелке: её несёт замена котелка
                    if (cur != null && !_reapplying && WorldExtra.PotInUseByMe(cur, out string sig))
                    {
                        HeldUpdates[id] = new HeldUpdate { Raw = raw, Sig = sig };
                        MelonLogger.Msg($"[котелок] {prefab}: замена от другого придёт, когда закроешь окно готовки");
                        return;
                    }
                    // Еда в заменяемом котелке — его дочерний объект, Destroy котелка унёс бы и её. Снимаем её с котелка:
                    // если новый котелок готовит её же (тот же GUID), он сам найдёт её в первом Update.
                    string oldSig = cur != null ? WorldExtra.PotSig(cur) : "";
                    GearItem food = cur != null ? DetachPotFood(cur) : null;
                    bool wasThrown = cur != null && ThrownAt.ContainsKey(cur.Pointer);
                    if (cur != null) { WorldExtra.DropInspectItems(cur); Forget(cur); FlareShots.Silence(cur.gameObject); UnityEngine.Object.Destroy(cur.gameObject); }
                    var gi = ItemSync.Spawn(prefab, json, null, false, false);
                    if (gi == null) { MelonLogger.Warning("[предметы] не создался " + prefab); DropPotFood(food); return; }
                    if (IsPersonal(gi)) { UnityEngine.Object.Destroy(gi.gameObject); DropPotFood(food); return; }
                    gi.transform.SetPositionAndRotation(pos, rot);
                    gi.gameObject.SetActive(true);
                    Track(id, gi);
                    Sender[gi.Pointer] = from;
                    if (wasThrown) NoteThrown(gi);
                    GuardFall(gi, pos);
                    bool keep = food != null && SameFood(gi, food);
                    if (food != null && !keep) DropPotFood(food);
                    if (!keep && fPrefab != null && !string.IsNullOrEmpty(fGuid)) SpawnPotFood(gi, fPrefab, fJson, fGuid);
                    // Снятая со старого котелка еда ждёт, пока новый возьмёт её по GUID; не взял — лишняя копия, уберём.
                    if (keep) PendingPotFood.Add((food, gi, Time.unscaledTime));
                    bool onFire = AttachToPlacePoint(gi, cur);
                    string newSig = WorldExtra.PotSig(gi);
                    if (newSig.Length > 0 && _potApplyLogged++ < 400)
                        MelonLogger.Msg($"[котелок] {prefab}: замена от {(Session.Players.TryGetValue(from, out var fromPl) ? fromPl.Name : "игрока " + from)}: {(cur == null ? "у меня не было" : oldSig)} → {newSig}{(onFire ? "" : ", не на месте у огня")}");
                }
                else
                {
                    var gi = Find(id, prefab, pos) ?? FindPileNear(prefab, pos) ?? FindThrownNear(prefab, pos);
                    if (gi == null) { LogPickMiss(prefab, pos, id); return; }
                    if (UnitsOf(gi) > 0 && _unitsLogged++ < 60) MelonLogger.Msg($"[предметы] чужой подбор {prefab}: убрал у себя{UnitsText(gi)}");
                    if (IsPersonal(gi)) return;
                    Forget(gi);   // номер найденной вещи, а не из события: по месту могла найтись другая
                    FlareShots.Silence(gi.gameObject);
                    UnityEngine.Object.Destroy(gi.gameObject);
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[предметы] {type}: {e.Message}");
            }
            finally
            {
                _applying = false;
            }
        }

        // Создать вещь, которую готовит присланный котелок, с тем же GUID: котелок найдёт её в первом Update
        // (CookingPotItem.FindGearItemBeingCooked → PdidTable.GetGameObject) и повесит на себя.
        internal static void SpawnPotFood(GearItem potGi, string prefab, string json, string guid)
        {
            try
            {
                var have = Il2CppTLD.PDID.PdidTable.GetGameObject(guid);
                if (have != null) return;
            }
            catch { }
            try
            {
                var food = Spawn(prefab, json, null, false, false);
                if (food == null) { MelonLogger.Warning($"[котелок] не создалась вещь в котелке: {prefab}"); return; }
                try { food.m_InPlayerInventory = false; } catch { }
                ObjectGuid.MaybeAttachObjectGuidAddOrReplace(food.gameObject, guid);
                food.transform.position = potGi.transform.position;
                food.gameObject.SetActive(true);
                PendingPotFood.Add((food, potGi, Time.unscaledTime));
                MelonLogger.Msg($"[котелок] {PrefabName(potGi)}: создал то, что в нём готовится: {prefab}");
            }
            catch (Exception e) { MelonLogger.Warning("[котелок] вещь в котелке: " + e.Message); }
        }

        // Еда, созданная для чужого котелка, лежит отдельной вещью, пока котелок в своём Update не найдёт её по GUID и не
        // повесит на себя. Если котелок за это время исчез (его заменили, убрала сверка локации, подставку забрали),
        // еда оставалась лежать на земле лишней копией — «задвоились напитки» у друзей (0.19.34). Убираем такую.
        private static readonly List<(GearItem Food, GearItem Pot, float At)> PendingPotFood = new();

        private static void TickPendingPotFood(float now)
        {
            for (int i = PendingPotFood.Count - 1; i >= 0; i--)
            {
                var (food, pot, at) = PendingPotFood[i];
                try
                {
                    if (food == null || food.m_InPlayerInventory || food.m_InsideContainer) { PendingPotFood.RemoveAt(i); continue; }
                    if (food.transform.parent != null) { PendingPotFood.RemoveAt(i); continue; }   // котелок её взял
                    bool potGone = pot == null || !pot.gameObject.activeInHierarchy;
                    if (potGone)
                    {
                        // Котелок заменили новым (замена от другого игрока): новый ждёт ту же еду по GUID — её не трогаем.
                        var other = PotWaitingFor(food);
                        if (other != null) { PendingPotFood[i] = (food, other, now); continue; }
                    }
                    else if (now - at < 10f) continue;
                    {
                        PendingPotFood.RemoveAt(i);
                        // Через 10 с котелок её так и не взял — значит, он ждёт другую вещь; эта лишняя.
                        Forget(food);
                        UnityEngine.Object.Destroy(food.gameObject);
                        MelonLogger.Msg($"[котелок] убрал лишнюю {PrefabName(food)}: котелок, для которого она была, {(potGone ? "исчез" : "её не взял")}");
                    }
                }
                catch { PendingPotFood.RemoveAt(i); }
            }
        }

        private static GearItem PotWaitingFor(GearItem food)
        {
            string guid = null;
            try { guid = ObjectGuid.MaybeGetGuidFromGameObject(food.gameObject); } catch { }
            if (string.IsNullOrEmpty(guid)) return null;
            foreach (var pot in UnityEngine.Object.FindObjectsOfType<CookingPotItem>())
            {
                try { if (pot != null && pot.m_GearItemGUIDFromDeserialize == guid) return pot.GetComponent<GearItem>(); } catch { }
            }
            return null;
        }

        // Чужой котелок или кружка-подставка у костра: игра у меня не знала, что он стоит на месте у огня
        // (GearPlacePoint.m_PlacedGear пустой, CookingPotItem.m_FireBeingUsed пустой). Поэтому у меня он не грелся и
        // писал «Пауза» (идёт отсчёт «без огня», m_GracePeriodElapsedHours), а место у огня считалось свободным, и
        // второй игрок ставил своё прямо в него — вещи вставали одна в другую. Ставим его на ближайшее место, как
        // сделала бы сама игра: GearPlacePoint.ItemPlaced → SetPlacePoint и UpdateAttachedFire → AttachToFire.
        internal static int PlacedOnFire;
        private static int _slotBusyLogged;

        // replacing — котелок, который этот заменяет (чужая замена, ItemUpdate). Старый мы уничтожаем в том же кадре,
        // и до конца кадра он ещё «стоит» на месте у огня: раньше новый считал место занятым и не вставал на него, а
        // в следующем кадре место оказывалось свободным (игра считает уничтоженную вещь пустым местом,
        // GearPlacePoint.FindFirstEmptyPlacePoint) — второй игрок ставил свою банку прямо в чужую кастрюлю (0.19.41).
        private static int _potApplyLogged;
        internal static bool AttachToPlacePoint(GearItem gi, GearItem replacing = null)
        {
            try
            {
                if (gi == null || gi.GetComponent<CookingPotItem>() == null) return false;
                var p = gi.transform.position;
                GearPlacePoint best = null, busy = null;
                float bestD = 0.35f, busyD = 0.35f;
                foreach (var gpp in UnityEngine.Object.FindObjectsOfType<GearPlacePoint>())
                {
                    try
                    {
                        if (gpp == null || !gpp.gameObject.activeInHierarchy) continue;
                        float d = Vector3.Distance(gpp.transform.position, p);
                        if (d > bestD && d > busyD) continue;
                        var placed = gpp.m_PlacedGear;
                        bool other = SlotHolds(placed) && placed.Pointer != gi.Pointer && (replacing == null || placed.Pointer != replacing.Pointer);
                        if (other) { if (d <= busyD) { busy = gpp; busyD = d; } continue; }   // занято другой вещью
                        if (d <= bestD) { best = gpp; bestD = d; }
                    }
                    catch { }
                }
                if (best == null)
                {
                    if (busy != null) NoteSlotConflict(busy, gi);
                    if (busy != null && _slotBusyLogged++ < 20)
                    {
                        string who = "?";
                        try { who = PrefabName(busy.m_PlacedGear); } catch { }
                        MelonLogger.Msg($"[котелок] {PrefabName(gi)} пришёл на место у огня, где у меня уже стоит {who} ({busyD:0.00} м)");
                    }
                    return false;
                }
                var prev = best.m_PlacedGear;
                if (prev != null && prev.Pointer == gi.Pointer) return true;
                if (!SlotHolds(prev) && !(prev != null && replacing != null && prev.Pointer == replacing.Pointer)) prev = null;
                best.ItemPlaced(prev, gi);
                PlacedOnFire++;
                return true;
            }
            catch (Exception e) { MelonLogger.Warning("[котелок] место у огня: " + e.Message); return false; }
        }

        // Два котелка на одном месте у огня: оба игрока поставили в одну и ту же секунду (у каждого место было
        // свободно), или у одного старая версия мода. Правило автора: одно место — одна посуда. Решение у всех одно и
        // то же без лишних сообщений: остаётся вещь игрока с меньшим номером (хост — 0), а свою вещь с места
        // забирает в рюкзак тот, чья она, — его подбор убирает её у всех, и место у остальных займёт оставшаяся.
        internal struct SlotTake { public GearPlacePoint Gpp; public GearItem Mine, Other; public float At; }
        internal static readonly List<SlotTake> SlotTakes = new();

        private static void NoteSlotConflict(GearPlacePoint gpp, GearItem arriving)
        {
            try
            {
                var placed = gpp.m_PlacedGear;
                if (!SlotHolds(placed) || arriving == null) return;
                int mine = OwnerOf(placed), theirs = OwnerOf(arriving);
                if (mine < 0) mine = 0;
                if (theirs < 0) theirs = 0;
                if (mine != Session.LocalId || mine <= theirs) return;
                if (SlotTakes.Exists(t => t.Mine != null && t.Mine.Pointer == placed.Pointer)) return;
                SlotTakes.Add(new SlotTake { Gpp = gpp, Mine = placed, Other = arriving, At = Time.unscaledTime });
            }
            catch { }
        }

        // Раз в секунду (WorldExtra.TickPlaceSlots), не внутри применения чужого события.
        internal static void ProcessSlotTakes()
        {
            if (SlotTakes.Count == 0) return;
            var list = SlotTakes.ToArray();
            SlotTakes.Clear();
            foreach (var t in list)
            {
                try
                {
                    var mine = t.Mine;
                    if (!SlotHolds(mine) || t.Gpp == null || t.Gpp.m_PlacedGear == null || t.Gpp.m_PlacedGear.Pointer != mine.Pointer) continue;
                    if (t.Other == null || t.Other.gameObject == null || !t.Other.gameObject.activeInHierarchy) continue;
                    string who = "?";
                    int o = OwnerOf(t.Other);
                    if (Session.Players.TryGetValue(o < 0 ? 0 : o, out var pl)) who = pl.Name;
                    var pot = mine.GetComponent<CookingPotItem>();
                    if (pot != null && pot.IsDummyPot())
                    {
                        // Еда прямо на огне (подставка-«котелок»): её так не забрать, только пишем.
                        if (_slotTakeLogged++ < 20) MelonLogger.Msg($"[котелок] место у огня занял и {who} ({PrefabName(t.Other)}), моё {PrefabName(mine)} — еда прямо на огне, не забираю");
                        continue;
                    }
                    t.Gpp.ItemPlaced(mine, null);
                    GameManager.GetInventoryComponent().AddGear(mine, true);
                    AttachToPlacePoint(t.Other);
                    if (_slotTakeLogged++ < 20) MelonLogger.Msg($"[котелок] место у огня в ту же секунду занял {who} ({PrefabName(t.Other)}) — своё {PrefabName(mine)} вернул в рюкзак");
                    GameApi.HudMessage(UI.L.T($"Это место у огня уже занял {who} — посуда вернулась в рюкзак", $"{who} already took this cooking spot — your item went back to your pack"));
                }
                catch (Exception e) { MelonLogger.Warning("[котелок] место у огня: " + e.Message); }
            }
        }

        private static int _slotTakeLogged;

        // На месте у огня стоит живая вещь (не уничтоженная, не в рюкзаке, не выключенная).
        internal static bool SlotHolds(GearItem g)
        {
            try { return g != null && g.gameObject != null && g.gameObject.activeInHierarchy && !g.m_InPlayerInventory && !g.m_InsideContainer; }
            catch { return false; }
        }

        // Забрал готовое из кружки-подставки: подставка у меня исчезнет сама (игра уничтожает пустую), у остальных — по
        // этому сообщению, иначе у них осталась бы копия с готовым чаем, и его можно было бы забрать второй раз.
        internal static void OnDummyPotEmptied(CookingPotItem pot)
        {
            if (!Active || pot == null) return;
            var gi = pot.GetComponent<GearItem>();
            if (gi == null || !IdOf.TryGetValue(gi.Pointer, out uint id) || id == 0) return;
            Forget(gi);
            NotePicked(id);
            var w = Msg.New(MsgType.ItemPick);
            w.Write(id);
            w.Write(SceneKey());
            w.Write(PrefabName(gi));
            WriteVec(w, gi.transform.position);
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg($"[котелок] {PrefabName(gi)}: забрал готовое, подставку убираю и у остальных");
        }

        private static GearItem DetachPotFood(GearItem potGi)
        {
            try
            {
                var pot = potGi.GetComponent<CookingPotItem>();
                var food = pot != null ? pot.m_GearItemBeingCooked : null;
                if (food == null) return null;
                food.transform.SetParent(null, true);
                return food;
            }
            catch { return null; }
        }

        // Новый котелок ждёт ту же вещь (GUID из его состояния = GUID нашей еды).
        private static bool SameFood(GearItem potGi, GearItem food)
        {
            try
            {
                var pot = potGi.GetComponent<CookingPotItem>();
                string want = pot != null ? pot.m_GearItemGUIDFromDeserialize : null;
                return !string.IsNullOrEmpty(want) && want == ObjectGuid.MaybeGetGuidFromGameObject(food.gameObject);
            }
            catch { return false; }
        }

        private static void DropPotFood(GearItem food)
        {
            if (food == null) return;
            try { Forget(food); UnityEngine.Object.Destroy(food.gameObject); } catch { }
        }

        // Все вещи локации ищем один раз за кадр: событий в кадре бывает десятки (снимок локации, берег).
        private static Il2CppArrayBase<GearItem> _allGear;
        private static int _allGearFrame = -1;

        private static Il2CppArrayBase<GearItem> AllGear()
        {
            if (_allGear == null || _allGearFrame != Time.frameCount)
            {
                _allGear = UnityEngine.Object.FindObjectsOfType<GearItem>();
                _allGearFrame = Time.frameCount;
            }
            return _allGear;
        }

        // Вещь на земле по имени и месту (проверка просит переслать её, Game/SyncCheck.cs).
        internal static GearItem FindNear(string prefab, Vector3 pos) => Find(0, prefab, pos);

        // Общий для всех ключ вещи: сетевой номер (если есть) и место. У одного игрока номер может быть, у другого нет,
        // поэтому сравнивать надо по любому из двух (Game/Busy.cs, «котелок занят»).
        internal static string ShareKey(GearItem gi)
        {
            if (gi == null) return "|";
            IdOf.TryGetValue(gi.Pointer, out uint id);
            return (id != 0 ? "#" + id : "") + "|" + PlaceKey(SceneKey(), PrefabName(gi), gi.transform.position);
        }

        internal static uint IdFor(GearItem gi) => gi != null && IdOf.TryGetValue(gi.Pointer, out uint id) ? id : 0u;
        internal static GearItem FindShared(uint id, string prefab, Vector3 pos) => Find(id, prefab, pos);

        // ---------- Свои недавние подборы (0.19.35) ----------
        // Друзья: «патроны на полу задвоились». Друг поднял вещь, а у хоста она лежала чуть в другом месте (больше 0,5 м:
        // сдвинули мебель под ней и т. п.), подбор у хоста её не нашёл, и сверка локации потом клала её другу обратно на пол —
        // одна в рюкзаке, вторая на полу. Теперь сверка вместо этого просит убрать вещь у остальных.
        private static readonly List<(string Scene, string Prefab, Vector3 Pos, float At)> MyPicks = new();
        private const float MyPickAge = 300f, MyPickDist = 3f;

        private static void NoteMyPick(string scene, string prefab, Vector3 pos)
        {
            float now = Time.unscaledTime;
            MyPicks.RemoveAll(p => now - p.At > MyPickAge);
            if (MyPicks.Count >= 200) MyPicks.RemoveAt(0);
            MyPicks.Add((scene, prefab, pos, now));
        }

        // Сверка хочет положить мне вещь, которой у меня нет: если я сам недавно поднял такую же рядом — это она.
        internal static bool TakeMyRecentPick(string scene, string prefab, Vector3 pos)
        {
            float now = Time.unscaledTime;
            int best = -1;
            float bestD = MyPickDist;
            for (int i = 0; i < MyPicks.Count; i++)
            {
                var p = MyPicks[i];
                if (now - p.At > MyPickAge || p.Scene != scene || p.Prefab != prefab) continue;
                float d = Vector3.Distance(p.Pos, pos);
                if (d <= bestD) { bestD = d; best = i; }
            }
            if (best < 0) return false;
            MyPicks.RemoveAt(best);
            return true;
        }

        // Подбор той вещи, что лежит у остальных в pos (по их раскладке): у них она уберётся по месту.
        internal static void SendPickAt(string scene, string prefab, Vector3 pos)
        {
            var w = Msg.New(MsgType.ItemPick);
            w.Write(0u);
            w.Write(scene);
            w.Write(prefab);
            WriteVec(w, pos);
            Session.SendGameEvent(Msg.Done(w));
        }

        // Брошенная вещь (фальшфейер, факел, шумелка, камень) летит у каждого игрока сама и ложится в паре метров от того
        // места, где у бросившего. Точное место бросивший шлёт, только когда она остановится, а поднять её могут раньше.
        // Подбор такой вещи у остальных искал её по номеру и в 2 м от места и промахивался (друзья 11-10: «ближайшая в 2.1 м»),
        // и у остальных оставался лишний фальшфейер. Теперь недавно брошенную вещь ищем и дальше — до 8 м (0.19.53).
        private static readonly Dictionary<IntPtr, float> ThrownAt = new();
        private const float ThrownNear = 8f, ThrownAge = 600f;
        private static int _thrownPick;

        private static void NoteThrown(GearItem gi)
        {
            if (gi == null) return;
            float now = Time.unscaledTime;
            ThrownAt[gi.Pointer] = now;
            if (ThrownAt.Count > 200)
            {
                var old = new List<IntPtr>();
                foreach (var kv in ThrownAt) if (now - kv.Value > ThrownAge) old.Add(kv.Key);
                foreach (var k in old) ThrownAt.Remove(k);
            }
        }

        private static GearItem FindThrownNear(string prefab, Vector3 pos)
        {
            if (ThrownAt.Count == 0) return null;
            float now = Time.unscaledTime;
            GearItem best = null;
            float bestD = ThrownNear;
            foreach (var gi in AllGear())
            {
                if (gi == null || !ThrownAt.TryGetValue(gi.Pointer, out float at) || now - at > ThrownAge) continue;
                if (gi.m_InPlayerInventory || gi.m_InsideContainer || IsPersonal(gi) || InPot(gi) || PrefabName(gi) != prefab) continue;
                float d = Vector3.Distance(gi.transform.position, pos);
                if (d < bestD) { bestD = d; best = gi; }
            }
            if (best != null && _thrownPick++ < 40)
                MelonLogger.Msg($"[предметы] чужой подбор {prefab} ({pos.x:0.0} {pos.y:0.0} {pos.z:0.0}): брошенная вещь легла у меня в {bestD:0.0} м от места у него — убрал её");
            return best;
        }

        private static void LogPickMiss(string prefab, Vector3 pos, uint id = 0)
        {
            try
            {
                float near = float.MaxValue;
                foreach (var g in AllGear())
                {
                    if (g == null || g.m_InPlayerInventory || g.m_InsideContainer || PrefabName(g) != prefab) continue;
                    near = Math.Min(near, Vector3.Distance(g.transform.position, pos));
                }
                string num = id == 0 ? "без номера" : $"номер {id:X8} " + (ById.ContainsKey(id) ? "у меня есть, но не там" : "у меня неизвестен");
                MelonLogger.Msg($"[предметы] чужой подбор {prefab} ({pos.x:0.0} {pos.y:0.0} {pos.z:0.0}): у меня такой там нет" + (near < 1000f ? $", ближайшая в {near:0.0} м" : "") + $"; {num}");
            }
            catch { }
        }

        // Подбор не нашёл вещь в 0,5 м: в куче одинаковых вещей (скинули много палок, патронов) они у каждого
        // раскатываются по-своему. Берём ближайшую такую же в 2 м — у поднявшего её больше нет, и у нас одной меньше.
        private static GearItem FindPileNear(string prefab, Vector3 pos)
        {
            GearItem best = null;
            float bestD = 2f;
            foreach (var gi in AllGear())
            {
                if (gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer || IsPersonal(gi) || InPot(gi)) continue;
                if (PrefabName(gi) != prefab || gi.GetComponent<CookingPotItem>() != null) continue;
                float d = Vector3.Distance(gi.transform.position, pos);
                if (d < bestD) { bestD = d; best = gi; }
            }
            if (best != null && _pickNear++ < 30) MelonLogger.Msg($"[предметы] чужой подбор {prefab} ({pos.x:0.0} {pos.y:0.0} {pos.z:0.0}): на месте нет, убрал такую же в {bestD:0.0} м");
            return best;
        }

        private static GearItem Find(uint id, string prefab, Vector3 pos)
        {
            var known = Known(id, prefab, pos, pos, KnownNear);
            if (known != null && !InPot(known)) return known;
            GearItem best = null;
            float bestD = 0.5f;
            foreach (var gi in AllGear())
            {
                if (gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer || IsPersonal(gi) || InPot(gi)) continue;
                if (PrefabName(gi) != prefab) continue;
                var p = gi.transform.position;
                float dx = p.x - pos.x, dy = p.y - pos.y, dz = p.z - pos.z;
                float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (d < bestD) { bestD = d; best = gi; }
            }
            return best;
        }

        // Каждый кадр: когда новая локация загрузилась и устоялась — применить отложенное.
        // Чужая вещь у меня проваливается сквозь землю (друзья, 0.19.44: гильзы «под текстурой»): её создают там, где она
        // была у бросившего (гильза — у ног стрелка, на уровне земли или чуть ниже), и физика роняет её сквозь коллайдер.
        // 4 с следим: ушла больше чем на 0.75 м ниже присланного места — возвращаем туда и больше не роняем.
        private static readonly List<(GearItem Gi, Vector3 Pos, float Until)> FallGuard = new();
        private static float _fallNext;
        private static int _fallFixed;
        internal static int FellFixed => _fallFixed;

        private static void GuardFall(GearItem gi, Vector3 pos)
        {
            if (gi == null) return;
            try { if (gi.GetComponent<Rigidbody>() == null) return; } catch { return; }
            if (FallGuard.Count > 200) FallGuard.RemoveAt(0);
            FallGuard.Add((gi, pos, Time.unscaledTime + 4f));
        }

        private static void TickFallGuard(float now)
        {
            if (FallGuard.Count == 0 || now < _fallNext) return;
            _fallNext = now + 0.25f;
            for (int i = FallGuard.Count - 1; i >= 0; i--)
            {
                var (gi, pos, until) = FallGuard[i];
                try
                {
                    if (gi == null || gi.gameObject == null || gi.m_InPlayerInventory || gi.m_InsideContainer || gi.transform.parent != null) { FallGuard.RemoveAt(i); continue; }
                    if (gi.transform.position.y < pos.y - 0.75f)
                    {
                        FallGuard.RemoveAt(i);
                        float drop = pos.y - gi.transform.position.y;
                        var rb = gi.GetComponent<Rigidbody>();
                        if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.isKinematic = true; }
                        gi.transform.position = pos;
                        if (_fallFixed++ < 30) MelonLogger.Msg($"[предметы] {PrefabName(gi)} провалилась у меня на {drop:0.#} м под землю — вернул туда, где она лежит у бросившего ({pos.x:0.0} {pos.y:0.0} {pos.z:0.0})");
                        continue;
                    }
                    if (now > until) FallGuard.RemoveAt(i);
                }
                catch { FallGuard.RemoveAt(i); }
            }
        }

        public static void Tick(float now)
        {
            TickFallGuard(now);
            if (PendingPotFood.Count > 0) TickPendingPotFood(now);
            TickDropLog(now);
            TickDenied();
            TickHeldUpdates();
            TickDeferredResend();
            if (Session.Mode == Mode.Off || !GameApi.InGame() || Session.InOwnWorld)
            {
                // И отсчёт ожидания: ключ новой локации мог прочитаться ещё до экрана загрузки — считаем заново после него.
                _appliedScene = ""; _sceneSeen = 0f; _sceneSeenFrame = 0; _seenScene = "";
                return;
            }
            string scene = SceneKey();
            if (scene != _appliedScene)
            {
                // Отсчёт заново, если за время ожидания локация сменилась ещё раз.
                if (_sceneSeen == 0f || _seenScene != scene) { _sceneSeen = now; _sceneSeenFrame = Time.frameCount; _seenScene = scene; }
                if (now - _sceneSeen < (LoadVeil.Holding ? 2f : 3f)) return;   // под экраном загрузки ждём меньше
                // И не меньше 60 кадров: моды (FoodPackByEMPEROR и др.) кладут вещи первого посещения через 30–60 кадров
                // после загрузки, а при низком FPS 2–3 с наступают раньше — события подбора их бы не нашли.
                if (Time.frameCount - _sceneSeenFrame < 60) return;
                // Моды ещё кладут/убирают вещи первого посещения (FoodPackByEMPEROR, FoodBalanceByEMPEROR) — ждём, но не дольше ~10 с.
                if (now - _sceneSeen < 10f && FoodBalanceLink.WorldSpawnsPending()) return;
                _sceneSeen = 0f;
                _seenScene = "";
                _appliedScene = scene;
                ById.Clear();
                IdOf.Clear();
                Sender.Clear();
                HeldUpdates.Clear();
                SceneSnapshot.HostOnSettled(scene);
                if (PendingByScene.TryGetValue(scene, out var list))
                {
                    PendingByScene.Remove(scene);
                    foreach (var p in list) Apply(p.Type, p.Raw);
                    PlayerProgress.NoteApplied(list.ConvertAll(p => p.Raw));
                    MelonLogger.Msg($"[предметы] {scene}: применено отложенных событий {list.Count}");
                }
            }
        }

        private static bool _reapplying;

        private static void TickHeldUpdates()
        {
            if (HeldUpdates.Count == 0 || WorldExtra.CookingPanelOpen() || WorldExtra.InspectingPot() || (Inspecting != null && InspectingByMe(Inspecting))) return;
            var list = new List<HeldUpdate>(HeldUpdates.Values);
            var ids = new List<uint>(HeldUpdates.Keys);
            HeldUpdates.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                var h = list[i];
                // Мы сами поменяли котелок, пока окно было открыто: наша замена новее, чужую не применяем.
                if (ById.TryGetValue(ids[i], out var cur) && cur != null && WorldExtra.PotSig(cur) != h.Sig) continue;
                _reapplying = true;
                try { Receive(MsgType.ItemUpdate, h.Raw); }
                finally { _reapplying = false; }
            }
        }

        // Хост: все отложенные события (для друга, который только что получил мир).
        public static IEnumerable<byte[]> AllPending()
        {
            foreach (var list in PendingByScene.Values)
                foreach (var p in list) yield return p.Raw;
        }

        public static void Reset()
        {
            PendingByScene.Clear();
            ById.Clear();
            IdOf.Clear();
            Sender.Clear();
            HeldUpdates.Clear();
            Picked.Clear();
            _appliedScene = "";
            _sceneSeen = 0f;
            _seenScene = "";
            CreatingIntoInventory = 0;
            PotWork = 0;
            PotInspectSpawn = 0;
            Recent.Clear();
            Claims.Clear();
            _denyLocal.Clear();
        }

        private static void WriteVec(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        private static Vector3 ReadVec(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        private static void WriteQuat(BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
        private static Quaternion ReadQuat(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        // ---------- Осмотр предмета: он уезжает к камере, запоминаем, где он лежал ----------

        // Вещи, созданные игрой за последние полсекунды (Patch_GearCreated*).
        private static readonly Dictionary<IntPtr, float> Created = new();
        private static int _createdSkipped;

        internal static void NoteCreated(GearItem gi)
        {
            if (gi == null) return;
            float now = Time.unscaledTime;
            if (Created.Count > 200)
            {
                var old = new List<IntPtr>();
                foreach (var kv in Created) if (now - kv.Value > 0.5f) old.Add(kv.Key);
                foreach (var k in old) Created.Remove(k);
            }
            Created[gi.Pointer] = now;
        }

        // Игра создаёт вещь прямо в рюкзаке (PlayerManager.InstantiateItemInPlayerInventory*): всё, что попадёт в рюкзак
        // за время такого вызова, — не подбор с земли. FoodPackByEMPEROR создаёт там копию шаблона (FromPrefab), мимо
        // InstantiateGearItem, поэтому одного NoteCreated мало: у остальных пропадала такая же вещь рядом, а второго,
        // кто сделал то же блюдо за 30 с, хост лишал его.
        internal static int CreatingIntoInventory;

        internal static bool JustCreated(GearItem gi)
        {
            if (!Created.Remove(gi.Pointer, out float at) || Time.unscaledTime - at > 0.5f) return false;
            if (_createdSkipped++ < 10) MelonLogger.Msg($"[вещи] {PrefabName(gi)} создала игра прямо в рюкзак — это не подбор с земли");
            return true;
        }

        internal static GearItem Inspecting;
        internal static Vector3 InspectPos;

        // Эту вещь я сейчас держу на экране осмотра (подобрал из мира и ещё не решил «взять»/«оставить»).
        internal static bool InspectingByMe(GearItem gi)
        {
            if (gi == null) return false;
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                if (pm == null || !pm.IsInspectModeActive()) return false;
                if (Inspecting != null && Inspecting.Pointer == gi.Pointer) return true;
                var g = pm.GearItemBeingInspected();
                return g != null && g.Pointer == gi.Pointer && !g.m_InPlayerInventory && !g.m_InsideContainer;
            }
            catch { return false; }
        }

        private class Deferred { public GearItem Gi; public Vector3 OldPos; }
        private static readonly Dictionary<IntPtr, Deferred> DeferredResend = new();

        private static void TickDeferredResend()
        {
            if (DeferredResend.Count == 0) return;
            foreach (var key in DeferredResend.Keys.ToList())
            {
                var d = DeferredResend[key];
                bool alive;
                try { alive = d.Gi != null && d.Gi.gameObject != null; } catch { alive = false; }
                if (alive && InspectingByMe(d.Gi)) continue;
                DeferredResend.Remove(key);
                // Взял в рюкзак — уйдёт подбором; осталась в мире — шлём её настоящее место.
                if (alive && !d.Gi.m_InPlayerInventory && !d.Gi.m_InsideContainer && d.Gi.gameObject.activeInHierarchy) Resend(d.Gi, d.OldPos);
            }
        }

        // Части модели, которые мод еды (FoodPackByEMPEROR) удаляет у только что созданной вещи (меняет модель), остаются
        // в списках GearItem.m_MeshRenderers / m_SkinnedMeshRenderers. Игра выключает их, когда прячет вещь
        // (Inventory.HideInventoryItem: котелок вешает на себя еду), и на удалённой части падала каждый кадр — у друга
        // колбаса в сковородке не появлялась, а в логе 41 тыс. ошибок (0.19.27). Убираем удалённые из списков.
        private static readonly HashSet<string> PrunedLogged = new();

        internal static void PruneDeadRenderers(GearItem gi)
        {
            if (gi == null) return;
            int removed = 0;
            try
            {
                var mr = gi.m_MeshRenderers;
                if (mr != null)
                {
                    var keep = new List<MeshRenderer>();
                    foreach (var r in mr) if (r != null) keep.Add(r);
                    if (keep.Count != mr.Length) { removed += mr.Length - keep.Count; gi.m_MeshRenderers = new Il2CppReferenceArray<MeshRenderer>(keep.ToArray()); }
                }
            }
            catch { }
            try
            {
                var sr = gi.m_SkinnedMeshRenderers;
                if (sr != null)
                {
                    var keep = new List<SkinnedMeshRenderer>();
                    foreach (var r in sr) if (r != null) keep.Add(r);
                    if (keep.Count != sr.Length) { removed += sr.Length - keep.Count; gi.m_SkinnedMeshRenderers = new Il2CppReferenceArray<SkinnedMeshRenderer>(keep.ToArray()); }
                }
            }
            catch { }
            if (removed > 0 && PrunedLogged.Add(PrefabName(gi)) && PrunedLogged.Count <= 20)
                MelonLogger.Msg($"[вещи] {PrefabName(gi)}: в списке частей модели были удалённые ({removed}) — убрал, иначе игра падает, когда прячет вещь");
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.HideInventoryItem))]
    internal static class Patch_HideDeadRenderers
    {
        private static void Prefix(GearItem gi) => ItemSync.PruneDeadRenderers(gi);
    }

    [HarmonyPatch(typeof(GearItem), nameof(GearItem.InstantiateGearItem), new[] { typeof(string) })]
    internal static class Patch_GearCreatedByName
    {
        private static void Postfix(GearItem __result) => ItemSync.NoteCreated(__result);
    }

    [HarmonyPatch(typeof(GearItem), nameof(GearItem.InstantiateGearItem), new[] { typeof(UnityEngine.AddressableAssets.AssetReference) })]
    internal static class Patch_GearCreatedByRef
    {
        private static void Postfix(GearItem __result) => ItemSync.NoteCreated(__result);
    }

    [HarmonyPatch]
    internal static class Patch_CreateInInventory
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
            AccessTools.GetDeclaredMethods(typeof(PlayerManager))
                .Where(m => m.Name == "InstantiateItemInPlayerInventory" || m.Name == "InstantiateItemInPlayerInventoryFromPrefab")
                .Cast<MethodBase>();

        [HarmonyPriority(Priority.First)]
        private static void Prefix() => ItemSync.CreatingIntoInventory++;

        private static Exception Finalizer(Exception __exception)
        {
            if (ItemSync.CreatingIntoInventory > 0) ItemSync.CreatingIntoInventory--;
            return __exception;
        }
    }

    // Котелок сам роняет еду из рюкзака и вешает на себя (StartCooking) или меняет сырое на готовое и кладёт в рюкзак
    // (PickUpCookedGearItem): ни выброса, ни подбора остальным не шлём, котелок у них заменит TickPots (Game/WorldExtra.cs).
    [HarmonyPatch]
    internal static class Patch_PotWork
    {
        private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
        {
            AccessTools.Method(typeof(CookingPotItem), nameof(CookingPotItem.StartCooking)),
            AccessTools.Method(typeof(CookingPotItem), nameof(CookingPotItem.PickUpCookedGearItem)),
        };

        [HarmonyPriority(Priority.First)]
        private static void Prefix() => ItemSync.PotWork++;

        private static void Postfix(CookingPotItem __instance, MethodBase __originalMethod, object[] __args)
        {
            if (Session.Mode == Mode.Off) return;
            try
            {
                bool start = __originalMethod.Name == nameof(CookingPotItem.StartCooking);
                if (start) ItemSync.OnStartedCooking(__instance);
                // Перемену котелка разошлём мы: начали готовить или забрали готовое в рюкзак (не замена сырого на готовое в котелке).
                bool took = !start && __args != null && __args.Length > 0 && __args[0] is bool add && add;
                if (start || took) WorldExtra.NotePotTouched(__instance);
                if (took && !ItemSync.Applying && __instance.IsDummyPot()) ItemSync.OnDummyPotEmptied(__instance);
            }
            catch (Exception e) { MelonLogger.Warning("[котелок] " + e.Message); }
        }

        private static Exception Finalizer(Exception __exception)
        {
            if (ItemSync.PotWork > 0) ItemSync.PotWork--;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.MaybeSpawnGearItemForInspectMode))]
    internal static class Patch_PotInspectSpawn
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix() => ItemSync.PotInspectSpawn++;

        private static Exception Finalizer(Exception __exception)
        {
            if (ItemSync.PotInspectSpawn > 0) ItemSync.PotInspectSpawn--;
            return __exception;
        }
    }

    // Осмотр воды/еды из котелка и «взять / выпить» из него: я у этого котелка — перемену разошлю я (Game/WorldExtra.cs).
    [HarmonyPatch]
    internal static class Patch_PotInspect
    {
        private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
        {
            AccessTools.Method(typeof(CookingPotItem), nameof(CookingPotItem.MaybeSpawnGearItemForInspectMode)),
            AccessTools.Method(typeof(CookingPotItem), nameof(CookingPotItem.DoSpecialActionFromInspectMode)),
        };

        private static void Postfix(CookingPotItem __instance, MethodBase __originalMethod)
        {
            if (Session.Mode == Mode.Off) return;
            try { WorldExtra.NotePotInspected(__instance, __originalMethod.Name == nameof(CookingPotItem.MaybeSpawnGearItemForInspectMode)); } catch { }
        }
    }

    [HarmonyPatch(typeof(GearItem), nameof(GearItem.Drop))]
    internal static class Patch_GearDrop
    {
        // Выброшенная вещь ещё катится или падает: где она остановится, пришлём отдельно (иначе у всех она лежит в 1–3 м
        // от того места, где у выбросившего).
        private static void Postfix(GearItem __result)
        {
            if (ItemSync.OnLocalDrop(__result)) WorldExtra.WatchDrop(__result);
        }
    }

    // Вещь, которую игра кладёт игроку под ноги: гильза после выстрела (GunItem.HandleShellDischarge), лишнее при разделке
    // и т. п. Раньше её видел только он сам.
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.InstantiateItemAtPlayersFeet), new[] { typeof(string), typeof(int) })]
    internal static class Patch_AtFeetByName
    {
        private static void Postfix(GearItem __result)
        {
            ItemSync.NotePotTake(__result);
            try { if (ItemSync.OnLocalDrop(__result)) WorldExtra.WatchDrop(__result); } catch (Exception e) { MelonLogger.Warning("[предметы] под ноги: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.InstantiateItemAtPlayersFeet), new[] { typeof(AssetReferenceGearItem), typeof(int) })]
    internal static class Patch_AtFeetByRef
    {
        private static void Postfix(GearItem __result)
        {
            ItemSync.NotePotTake(__result);
            try { if (ItemSync.OnLocalDrop(__result)) WorldExtra.WatchDrop(__result); } catch (Exception e) { MelonLogger.Warning("[предметы] под ноги: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.EnterInspectGearMode),
        new[] { typeof(GearItem), typeof(Container), typeof(IceFishingHole), typeof(Harvestable), typeof(CookingPotItem) })]
    internal static class Patch_InspectStart
    {
        private static void Prefix(GearItem gear, Container c, IceFishingHole hole, Harvestable h, CookingPotItem pot)
        {
            ItemSync.Inspecting = null;
            if (gear == null || c != null || hole != null || h != null || pot != null) return;
            if (gear.m_InPlayerInventory || gear.m_InsideContainer) return;
            ItemSync.Inspecting = gear;
            ItemSync.InspectPos = gear.transform.position;
        }
    }

    // Подбор с земли: предмет попадает в инвентарь не из контейнера.
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddGear))]
    internal static class Patch_InventoryAdd
    {
        private static void Prefix(GearItem gi)
        {
            if (!GroundPick(gi, out var pos)) return;
            ItemSync.Inspecting = null;
            ItemSync.OnLocalPickup(gi, pos);
        }

        // Вещь берут с земли (а не создаёт игра прямо в рюкзак): и где она лежала.
        internal static bool GroundPick(GearItem gi, out Vector3 pos)
        {
            pos = default;
            if (ItemSync.Quiet || gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer) return false;
            // Вещь, которую игра только что создала (крафт, мясо с туши, рыба, готовая еда), — не подбор с земли:
            // иначе у друга пропадала такая же вещь, лежащая рядом.
            if (ItemSync.CreatingIntoInventory > 0 || ItemSync.PotWork > 0) return false;
            if (ItemSync.JustCreated(gi)) return false;
            if (ItemSync.Inspecting != null && ItemSync.Inspecting.Pointer == gi.Pointer) { pos = ItemSync.InspectPos; return true; }
            if (!gi.gameObject.activeInHierarchy) return false;
            pos = gi.transform.position;
            return true;
        }
    }

    // Подбор пачки (патроны, палки, кишки, ракеты), когда такая же уже есть в рюкзаке: игра досыпает штуки в свою пачку
    // (PlayerManager.AddItemToPlayerInventory → TryAddToExistingStackable) и уничтожает поднятую, а Inventory.AddGear
    // не зовёт. Раньше такой подбор никому не уходил: у бросившего пачка оставалась лежать, и сверка потом клала её
    // поднявшему ещё раз — «дюп» патронов у друзей (0.19.40).
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.TryAddToExistingStackable),
        new[] { typeof(GearItem), typeof(float), typeof(int), typeof(GearItem) }, new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })]
    internal static class Patch_StackPickup
    {
        private struct State { public GearItem Gi; public Vector3 Pos; public bool Ground; }
        internal static int Merged;

        private static void Prefix(GearItem gearToAdd, out State __state)
        {
            __state = default;
            try { __state.Ground = Patch_InventoryAdd.GroundPick(gearToAdd, out __state.Pos); __state.Gi = gearToAdd; } catch { }
        }

        private static void Postfix(bool __result, State __state)
        {
            if (!__result || !__state.Ground || __state.Gi == null) return;
            try
            {
                ItemSync.Inspecting = null;
                ItemSync.OnLocalPickup(__state.Gi, __state.Pos);
                if (Merged++ < 20) MelonLogger.Msg($"[предметы] поднял {ItemSync.PrefabName(__state.Gi)} в свою пачку — подбор ушёл другим");
            }
            catch (Exception e) { MelonLogger.Warning("[предметы] подбор в пачку: " + e.Message); }
        }
    }
}
