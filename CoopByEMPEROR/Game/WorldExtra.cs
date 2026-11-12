using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Вещи мира, которые раньше оставались только у того, кто с ними что-то сделал:
    // трос на точке крепления, снежное укрытие, замки (вскрытые ломом и выпавшие случайно),
    // лунки во льду, силки, вещи, поставленные через «разместить» (и кастрюля на печи),
    // брошенные вещи и выпущенные стрелы.
    public static class WorldExtra
    {
        private const char Sep = '\u0002';
        private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // ---------- Применение чужого состояния (из WorldObjects.Apply) ----------

        internal static Component Apply(WorldObjects.Kind kind, string id, string state)
        {
            switch (kind)
            {
                case WorldObjects.Kind.Rope:
                {
                    var rap = WorldObjects.Find<RopeAnchorPoint>(id);
                    bool on = state == "1";
                    if (rap != null && rap.m_RopeDeployed != on) rap.SetRopeActiveState(on, false);
                    if (rap != null) LastRope[ItemSync.SceneKey() + id] = on;
                    return rap;
                }
                case WorldObjects.Kind.Lock: return ApplyLock(id, state);
                case WorldObjects.Kind.Shelter: return ApplyShelter(id, state);
                case WorldObjects.Kind.IceHoles: ApplyHoles(state); return null;
                case WorldObjects.Kind.BigCarry: return ApplyCarry(id, state);
                case WorldObjects.Kind.Safe: return WorldStates.ApplySafe(id, state);
                case WorldObjects.Kind.Heater: return WorldStates.ApplyHeater(id, state);
                case WorldObjects.Kind.FireClock: return WorldStates.ApplyFireClock(id, state);
                case WorldObjects.Kind.Elevator: return WorldStates.ApplyElevator(id, state);
                case WorldObjects.Kind.Water: return WaterTanks.Apply(id, state);
                case WorldObjects.Kind.Decoration: return ApplyDecoration(id);
                case WorldObjects.Kind.Lantern: return ApplyLantern(id, state);
            }
            return null;
        }

        // ---------- Сломанное украшение и настенный фонарь ----------
        // Починку украшения и зажжённый (погашенный) фонарь видел только тот, кто это сделал. Шлём только действие
        // игрока (не загрузку и не задания): починено — у всех починено; фонарь — как у него, вместе со связанными.

        private static Component ApplyDecoration(string id)
        {
            var d = WorldObjects.Find<BrokenDecorationInteraction>(id);
            if (d == null) return null;
            try { if (d.m_State != BrokenDecorationState.Repaired) d.UpdateState(BrokenDecorationState.Repaired); }
            catch (Exception e) { MelonLogger.Warning("[объекты] украшение: " + e.Message); }
            return d;
        }

        private static Component ApplyLantern(string id, string state)
        {
            var ls = WorldObjects.Find<InteractiveLightsource>(id);
            if (ls == null) return null;
            try
            {
                bool on = state == "1";
                ls.SetState(on);
                var links = ls.m_LightsourceLinks;
                if (links != null)
                    for (int i = 0; i < links.Count; i++) { var l = links[i]; if (l != null) l.SetState(on); }
            }
            catch (Exception e) { MelonLogger.Warning("[объекты] фонарь: " + e.Message); }
            return ls;
        }

        // ---------- Трос на точке крепления ----------

        private static readonly Dictionary<string, bool> LastRope = new();

        internal static void SendRope(RopeAnchorPoint rap)
        {
            if (rap == null || WorldObjects.Applying) return;
            string key = ItemSync.SceneKey() + WorldObjects.IdOf(rap);
            bool on = rap.m_RopeDeployed;
            if (LastRope.TryGetValue(key, out var was) && was == on) return;
            LastRope[key] = on;
            WorldObjects.Send(WorldObjects.Kind.Rope, rap, on ? "1" : "0");
        }

        // ---------- Замки ----------
        // Заперт ли сейф или дверь, игра решает случайно при первом заходе в локацию, у каждого своё.
        // Правило: кто зашёл первым, того и замки. Если зашли одновременно, остаётся бросок игрока с меньшим номером.
        // Вскрытый ломом замок уходит всем сразу.

        private static readonly List<Lock> Rolled = new();
        private static readonly HashSet<string> RolledByMe = new();
        private static readonly HashSet<string> LockKnown = new();

        internal static void OnLockRolled(Lock lk)
        {
            if (lk != null && Session.Mode != Mode.Off) Rolled.Add(lk);
        }

        internal static void OnLockForced(Lock lk)
        {
            if (lk == null || WorldObjects.Applying) return;
            LockKnown.Add(ItemSync.SceneKey() + WorldObjects.IdOf(lk));
            WorldObjects.Send(WorldObjects.Kind.Lock, lk, "S" + lk.Serialize());
        }

        // Перед отложенными состояниями локации: свои броски запоминаем, но пока не отправляем.
        internal static void BeforePending(string scene)
        {
            foreach (var lk in Rolled)
                if (lk != null) RolledByMe.Add(scene + WorldObjects.IdOf(lk));
        }

        // После: замки, о которых никто не рассказал, — наш бросок, рассылаем.
        internal static void AfterPending(string scene)
        {
            int sent = 0;
            foreach (var lk in Rolled)
            {
                if (lk == null) continue;
                string key = scene + WorldObjects.IdOf(lk);
                if (LockKnown.Contains(key)) continue;
                LockKnown.Add(key);
                WorldObjects.Send(WorldObjects.Kind.Lock, lk, "R" + Session.LocalId + Sep + lk.Serialize());
                sent++;
            }
            Rolled.Clear();
            if (sent > 0) MelonLogger.Msg($"[объекты] замки: отправил свои {sent}");
        }

        private static Component ApplyLock(string id, string state)
        {
            var lk = WorldObjects.Find<Lock>(id);
            if (lk == null || state.Length == 0) return lk;
            string key = ItemSync.SceneKey() + id;
            if (state[0] == 'R')
            {
                int cut = state.IndexOf(Sep);
                if (cut < 0) return lk;
                int.TryParse(state.Substring(1, cut - 1), out int from);
                // Замок у нас из сохранения (не выпадал сейчас) — он и так общий, чужой бросок не нужен.
                if (!RolledByMe.Contains(key)) return lk;
                // Бросили оба одновременно: остаётся бросок игрока с меньшим номером.
                if (!WorldObjects.ApplyingPending && LockKnown.Contains(key) && from > Session.LocalId) return lk;
                state = state.Substring(cut + 1);
            }
            else state = state.Substring(1);
            LockKnown.Add(key);
            lk.Deserialize(state);
            return lk;
        }

        // ---------- Снежное укрытие ----------

        internal static void SendShelter(SnowShelter ss, bool gone)
        {
            if (ss == null || WorldObjects.Applying) return;
            var t = ss.transform;
            var p = t.position;
            var q = t.rotation;
            string state = gone
                ? string.Join(Sep, "GONE", F(p.x), F(p.y), F(p.z))
                : string.Join(Sep, "SS", ss.gameObject.name, F(p.x), F(p.y), F(p.z), F(q.x), F(q.y), F(q.z), F(q.w), ss.Serialize() ?? "");
            WorldObjects.Send(WorldObjects.Kind.Shelter, ss, state);
        }

        private static string F(float v) => v.ToString("R", IC);
        private static float P(string s) => float.Parse(s, IC);

        private static Component ApplyShelter(string id, string state)
        {
            var parts = state.Split(Sep);
            var ss = WorldObjects.Find<SnowShelter>(id);
            if (parts[0] == "GONE" && parts.Length >= 4)
            {
                var pos = new Vector3(P(parts[1]), P(parts[2]), P(parts[3]));
                ss ??= NearestShelter(pos, 1.5f);
                if (ss == null) return null;
                try { SnowShelterManager.Remove(ss); } catch { }
                UnityEngine.Object.Destroy(ss.gameObject);
                WorldObjects.ForgetCache(typeof(SnowShelter));
                MelonLogger.Msg("[объекты] другой игрок разобрал снежное укрытие");
                return null;
            }
            if (parts[0] != "SS" || parts.Length < 10) return ss;
            if (ss == null)
            {
                var pos = new Vector3(P(parts[2]), P(parts[3]), P(parts[4]));
                ss = NearestShelter(pos, 1.5f);
                if (ss == null)
                {
                    var mgr = GameManager.GetSnowShelterManager();
                    ss = mgr != null ? mgr.InstantiateSnowShelter() : null;
                    if (ss == null) { MelonLogger.Msg("[объекты] не смог создать снежное укрытие"); return null; }
                    ss.transform.SetPositionAndRotation(pos, new Quaternion(P(parts[5]), P(parts[6]), P(parts[7]), P(parts[8])));
                    ss.gameObject.name = parts[1];
                    if (!id.StartsWith("@")) ObjectGuid.MaybeAttachObjectGuidAddOrReplace(ss.gameObject, id);
                    WorldObjects.ForgetCache(typeof(SnowShelter));
                    MelonLogger.Msg("[объекты] построил у себя снежное укрытие другого игрока");
                }
            }
            ss.Deserialize(parts[9]);
            return ss;
        }

        private static SnowShelter NearestShelter(Vector3 pos, float max)
        {
            SnowShelter best = null;
            float bd = max * max;
            foreach (var s in UnityEngine.Object.FindObjectsOfType<SnowShelter>())
            {
                if (s == null) continue;
                float d = (s.transform.position - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        // ---------- Лунки во льду ----------
        // Прорубленную или заново очищенную лунку отправляем вместе со всеми лунками локации (так их хранит игра).

        internal static void SendHoles()
        {
            if (WorldObjects.Applying) return;
            string all;
            try { all = IceFishingHole.SerializeAll(); } catch (Exception e) { MelonLogger.Warning("[объекты] лунки: " + e.Message); return; }
            if (!string.IsNullOrEmpty(all)) WorldObjects.Send(WorldObjects.Kind.IceHoles, null, all, "all");
        }

        // Жерлицы: поставил, снял, клюнуло, снял рыбу — лунки локации уходят всем (через полсекунды, когда игра
        // всё допишет). Ждёт ли жерлица рыбу и когда клюнет, считает только хозяин животных локации — как у силков,
        // у остальных время жерлицы стоит, а состояние приходит от хозяина. Так и «выловленность» места общая:
        // игра считает её по соседним жерлицам, а они теперь у всех одни.
        private static float _holesAt = -1f, _nextHolesBeat;
        private static string _holesWaiting;

        internal static void SendHolesSoon()
        {
            if (WorldObjects.Applying || !Shared) return;
            _holesAt = Time.unscaledTime + 0.5f;
        }

        public static bool MayRollFish() => MayRollSnare();

        private static bool FishingHere()
        {
            foreach (var h in UnityEngine.Object.FindObjectsOfType<IceFishingHole>())
                if (h != null && h.m_State == FishingState.ActiveFishing) return true;
            return false;
        }

        private static void TickHoles(float now)
        {
            if (_holesAt >= 0f && now >= _holesAt) { _holesAt = -1f; SendHoles(); }
            // Пока сам ловишь удочкой, чужое состояние лунок не накладываем — дождёмся конца.
            if (_holesWaiting != null && !FishingHere())
            {
                string s = _holesWaiting;
                _holesWaiting = null;
                WorldObjects.Applying = true;
                try { ApplyHoles(s); } finally { WorldObjects.Applying = false; }
            }
            // Хозяин раз в минуту подтверждает жерлицы (сколько осталось наживки, не сорвало ли леску).
            if (now >= _nextHolesBeat)
            {
                _nextHolesBeat = now + 60f;
                if (!MayRollFish() || !AnyoneHere()) return;
                foreach (var h in UnityEngine.Object.FindObjectsOfType<IceFishingHole>())
                    if (h != null && h.m_State == FishingState.PassiveFishing) { SendHoles(); break; }
            }
        }

        private static bool AnyoneHere()
        {
            string scene = GameApi.Scene();
            foreach (var p in Session.Players.Values) if (p.Id != Session.LocalId && Session.With(p, scene) && Time.unscaledTime - p.LastState < 5f) return true;
            return false;
        }

        private static void ApplyHoles(string state)
        {
            if (FishingHere()) { _holesWaiting = state; return; }
            var before = new HashSet<IntPtr>();
            foreach (var h in UnityEngine.Object.FindObjectsOfType<IceFishingHole>()) if (h != null) before.Add(h.Pointer);
            IceFishingHole.DeserializeAll(state);
            // Ловлю удочкой ведёт только сам рыбак: если у другого игрока лунка была «ловит удочкой», у нас она просто свободна.
            foreach (var h in UnityEngine.Object.FindObjectsOfType<IceFishingHole>())
                if (h != null && h.m_State == FishingState.ActiveFishing) { try { h.m_State = FishingState.Idle; } catch { } }
            // На всякий случай: если игра создала лунку там, где у нас уже есть такая же, лишнюю убираем.
            var all = UnityEngine.Object.FindObjectsOfType<IceFishingHole>();
            int removed = 0;
            foreach (var h in all)
            {
                if (h == null || before.Contains(h.Pointer)) continue;
                foreach (var o in all)
                {
                    if (o == null || o.Pointer == h.Pointer || !before.Contains(o.Pointer)) continue;
                    if ((o.transform.position - h.transform.position).sqrMagnitude < 0.09f) { UnityEngine.Object.Destroy(h.gameObject); removed++; break; }
                }
            }
            MelonLogger.Msg($"[объекты] лунки как у другого игрока" + (removed > 0 ? $", лишних убрал {removed}" : ""));
        }

        // ---------- Силки ----------
        // Поймал ли силок зайца, решает только хозяин животных локации (у остальных бросок запрещён).
        // Изменилось состояние силка — у остальных он заменяется нашей копией.

        public static bool MayRollSnare() => Session.Mode == Mode.Off || !Session.InSharedWorld || AnimalSync.IsAuthority;

        private static readonly Dictionary<IntPtr, int> SnareStates = new();

        // Поиск по всей сцене (FindObjectsOfType) дорогой: раз в секунду четыре таких давали рывок.
        // Списки силков, котелков, ламп и факелов обновляем раз в 6 с, а их состояние проверяем каждую секунду.
        private static readonly Dictionary<Type, (float until, System.Collections.IList list)> Scans = new();

        internal static List<T> Scan<T>() where T : UnityEngine.Object
        {
            float now = Time.unscaledTime;
            if (Scans.TryGetValue(typeof(T), out var c) && now < c.until) return (List<T>)c.list;
            var list = new List<T>();
            foreach (var o in UnityEngine.Object.FindObjectsOfType<T>()) if (o != null) list.Add(o);
            Scans[typeof(T)] = (now + 6f + UnityEngine.Random.value * 2f, list);   // вразброс, не все в одном кадре
            return list;
        }

        private static void TickSnares()
        {
            foreach (var sn in Scan<SnareItem>())
            {
                if (sn == null) continue;
                var gi = sn.GetComponent<GearItem>();
                if (gi == null || !InWorld(gi)) continue;
                int st = (int)sn.m_State;
                if (!SnareStates.TryGetValue(sn.Pointer, out int was)) { SnareStates[sn.Pointer] = st; continue; }
                if (was == st) continue;
                SnareStates[sn.Pointer] = st;
                MelonLogger.Msg($"[предметы] силок: состояние {was} → {st}, отправляю");
                ItemSync.Resend(gi, gi.transform.position);
            }
        }

        // ---------- Котелки и кастрюли на огне ----------
        // Готовка идёт в каждой игре, но переходы (сварилось, сгорело, положили еду, налили воду или снег)
        // рассылает один: тот, кто последним прислал этот котелок, или тот, кто сейчас сам у котелка
        // (открыто окно готовки). Котелок у остальных заменяется копией с тем же состоянием.

        private static readonly Dictionary<IntPtr, string> PotStates = new();
        // Когда я последний раз был у этого котелка (окно готовки у него открыто, начал готовить, забрал готовое).
        // Раньше «я у котелка» было общим на все котелки: с окном у второго котелка того же костра мы рассылали и
        // первый — свою копию с еды по своим часам, и она заменяла у повара его котелок вместе с настоящей едой.
        private static readonly Dictionary<IntPtr, float> PotNearAt = new();

        internal static void NotePotTouched(CookingPotItem pot)
        {
            if (pot != null) PotNearAt[pot.Pointer] = Time.unscaledTime;
        }

        // Осмотр еды/воды из котелка: можно выпить или съесть только часть. Грубая сигнатура (вода по 0,5 л, только
        // какое блюдо) этого не видит, поэтому, пока я осматриваю, сравниваем ещё точное количество: литры и калории.
        private static readonly Dictionary<IntPtr, float> PotInspectAt = new();
        private static readonly Dictionary<IntPtr, string> PotFine = new();

        // start — осмотр только начался (запомнить, сколько было); иначе это уже действие «выпить / взять».
        internal static void NotePotInspected(CookingPotItem pot, bool start)
        {
            if (pot == null) return;
            if (start) { try { PotFine[pot.Pointer] = PotFineSignature(pot); } catch { } }
            PotInspectAt[pot.Pointer] = Time.unscaledTime;
            PotNearAt[pot.Pointer] = Time.unscaledTime;
        }

        private static bool Less(string now, string was, int i)
        {
            var a = now.Split('|'); var b = was.Split('|');
            return a.Length > i && b.Length > i && float.TryParse(a[i], out float x) && float.TryParse(b[i], out float y) && x < y - 0.5f;
        }

        private static string PotFineSignature(CookingPotItem pot)
        {
            float snow = 0f, water = 0f, cal = 0f;
            try { snow = pot.m_LitersSnowBeingMelted.m_Units; water = pot.m_LitersWaterBeingBoiled.m_Units; } catch { }
            try
            {
                var food = pot.m_GearItemBeingCooked != null ? pot.m_GearItemBeingCooked.m_FoodItem : null;
                if (food != null) cal = food.m_CaloriesRemaining;
            }
            catch { }
            return $"{snow:0}|{water:0}|{cal:0}";
        }

        private static string PotSignature(CookingPotItem pot)
        {
            string cooked = "";
            try { if (pot.m_GearItemBeingCooked != null) cooked = ItemSync.PrefabName(pot.m_GearItemBeingCooked); } catch { }
            int snow = 0, water = 0;
            try { float l = Il2CppTLD.IntBackedUnit.ItemLiquidVolume.Liter.m_Units; snow = Mathf.RoundToInt(pot.m_LitersSnowBeingMelted.m_Units * 2f / l); water = Mathf.RoundToInt(pot.m_LitersWaterBeingBoiled.m_Units * 2f / l); } catch { }
            // Есть ли снег/вода вообще: 0,25 л округлялись до 0, и банка со снегом считалась пустой (0.19.20, лог хоста).
            int anySnow = 0, anyWater = 0;
            try { anySnow = pot.m_LitersSnowBeingMelted.m_Units > 0 ? 1 : 0; anyWater = pot.m_LitersWaterBeingBoiled.m_Units > 0 ? 1 : 0; } catch { }
            return $"{(int)pot.m_CookingState}|{cooked}|{snow}|{water}|{anySnow}{anyWater}";
        }

        private static string Cooked(string sig)
        {
            var p = sig.Split('|');
            return p.Length > 1 ? p[1] : "";
        }

        // Вода, которую игра кладёт «к ногам» на время осмотра котелка (CookingPotItem.MaybeSpawnGearItemForInspectMode):
        // она есть только у осматривающего, это не вещь мира. Проверка считала её лишней («у хоста нет
        // GEAR_WaterSupplyNotPotable»), а выравнивание убирало прямо во время осмотра.
        private static readonly HashSet<IntPtr> InspectItems = new();
        private static int _inspectFrame = -1;

        internal static bool IsPotInspectItem(GearItem gi)
        {
            if (gi == null) return false;
            if (_inspectFrame != Time.frameCount)
            {
                _inspectFrame = Time.frameCount;
                InspectItems.Clear();
                try
                {
                    foreach (var pot in Scan<CookingPotItem>())
                    {
                        if (pot == null) continue;
                        var a = pot.m_GearItemSpawnedForInspectMode;
                        var b = pot.m_GearItemToEatDrinkFromInspectMode;
                        if (a != null) InspectItems.Add(a.Pointer);
                        if (b != null) InspectItems.Add(b.Pointer);
                    }
                }
                catch { }
            }
            return InspectItems.Contains(gi.Pointer);
        }

        // Сейчас осматриваю вещь, которую игра достала из этого котелка (или из любого, если pot == null).
        internal static bool InspectingPot(CookingPotItem pot = null)
        {
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                if (pm == null || !pm.IsInspectModeActive()) return false;
                var seen = pm.GearItemBeingInspected();
                if (seen == null) return false;
                if (pot != null) return Is(pot, seen);
                foreach (var p in Scan<CookingPotItem>()) if (p != null && Is(p, seen)) return true;
            }
            catch { }
            return false;

            static bool Is(CookingPotItem p, GearItem g) =>
                (p.m_GearItemSpawnedForInspectMode != null && p.m_GearItemSpawnedForInspectMode.Pointer == g.Pointer) ||
                (p.m_GearItemToEatDrinkFromInspectMode != null && p.m_GearItemToEatDrinkFromInspectMode.Pointer == g.Pointer);
        }

        // Убрать воду/еду «для осмотра» у котелка, который сейчас заменим (иначе она остаётся на земле).
        internal static void DropInspectItems(GearItem potGi)
        {
            try
            {
                var pot = potGi != null ? potGi.GetComponent<CookingPotItem>() : null;
                if (pot == null) return;
                foreach (var g in new[] { pot.m_GearItemSpawnedForInspectMode, pot.m_GearItemToEatDrinkFromInspectMode })
                    if (g != null && !g.m_InPlayerInventory && !g.m_InsideContainer) UnityEngine.Object.Destroy(g.gameObject);
            }
            catch { }
        }

        // Сигнатура котелка (для отложенной чужой замены, Game/ItemSync.cs); "" — не котелок.
        internal static string PotSig(GearItem gi)
        {
            try { var pot = gi != null ? gi.GetComponent<CookingPotItem>() : null; return pot != null ? PotSignature(pot) : ""; }
            catch { return ""; }
        }

        // Я сейчас у этого котелка с открытым окном готовки (в 3 м от него).
        internal static bool PotInUseByMe(GearItem gi, out string sig)
        {
            sig = "";
            var pot = gi != null ? gi.GetComponent<CookingPotItem>() : null;
            if (pot == null) return false;
            // Осматриваю воду/еду из этого котелка: замена котелка оставила бы её лежать на земле лишней вещью.
            if (InspectingPot(pot)) { sig = PotSig(gi); return true; }
            // Сам котелок у меня в руках на экране осмотра: замена унесла бы его с экрана обратно на печку.
            if (ItemSync.InspectingByMe(gi)) { sig = PotSig(gi); return true; }
            if (!CookingPanelOpen()) return false;
            // Окно знает свой котелок — только он; не знает — котелок в 3 м от меня.
            var mine = PanelPot();
            if (mine != null) { if (mine.Pointer != pot.Pointer) return false; }
            else
            {
                var me = GameManager.GetPlayerTransform();
                if (me == null || (me.position - gi.transform.position).sqrMagnitude > 9f) return false;
            }
            sig = PotSig(gi);
            return true;
        }

        // Котелок, которым я сейчас пользуюсь: осматриваю воду/еду из него или открыто его окно готовки (Game/Busy.cs).
        internal static CookingPotItem PotInUse()
        {
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                if (pm != null && pm.IsInspectModeActive())
                    foreach (var p in Scan<CookingPotItem>()) if (p != null && InspectingPot(p)) return p;
                var mine = PanelPot();
                if (mine != null)
                {
                    var gi = mine.GetComponent<GearItem>();
                    if (gi != null && InWorld(gi)) return mine;
                }
            }
            catch { }
            return null;
        }

        private static CookingPotItem PanelPot()
        {
            try
            {
                if (InterfaceManager.TryGetPanel<Panel_Cooking>(out var c) && c != null && c.IsEnabled()) return c.m_CookingPotInteractedWith;
                if (InterfaceManager.TryGetPanel<Panel_CookWater>(out var w) && w != null && w.IsEnabled()) return w.m_CookingPotInteractedWith;
            }
            catch { }
            return null;
        }

        internal static bool CookingPanelOpen()
        {
            try
            {
                if (InterfaceManager.TryGetPanel<Panel_Cooking>(out var c) && c != null && c.IsEnabled()) return true;
                if (InterfaceManager.TryGetPanel<Panel_CookWater>(out var w) && w != null && w.IsEnabled()) return true;
            }
            catch { }
            return false;
        }

        private static int _potFireErr;

        private static void TickPots(float now)
        {
            bool panel = CookingPanelOpen();
            foreach (var pot in Scan<CookingPotItem>())
            {
                if (pot == null) continue;
                var gi = pot.GetComponent<GearItem>();
                if (gi == null || !InWorld(gi)) continue;
                try { PotFire.Tick(pot, gi, now); } catch (Exception e) { if (_potFireErr++ < 5) MelonLogger.Warning("[котелок] огонь под котелком: " + e.Message); }
                // Котелок у меня на экране осмотра (взял с печки): он висит перед камерой. Его перемену разошлём, когда положу,
                // иначе у остальных он повисал у лица моей фигуры (0.19.27).
                if (ItemSync.InspectingByMe(gi)) { PotNearAt[pot.Pointer] = now; continue; }
                if (panel && PotInUseByMe(gi, out _)) PotNearAt[pot.Pointer] = now;
                // Осматриваю воду из котелка (взять, выпить): это тоже я у котелка. Раньше «рядом» был только
                // с окном готовки — друг забирал воду, а перемену никто не слал, и проверка возвращала воду обратно.
                else if (InspectingPot(pot))
                {
                    PotNearAt[pot.Pointer] = now;
                    if (!PotInspectAt.ContainsKey(pot.Pointer) || now - PotInspectAt[pot.Pointer] >= 3f) { try { PotFine[pot.Pointer] = PotFineSignature(pot); } catch { } }
                    PotInspectAt[pot.Pointer] = now;
                }
                string sig;
                try { sig = PotSignature(pot); } catch { continue; }
                int owner = ItemSync.OwnerOf(gi);
                bool nearMe = PotNearAt.TryGetValue(pot.Pointer, out float at) && now - at < 3f;
                if (!PotStates.TryGetValue(pot.Pointer, out var was))
                {
                    PotStates[pot.Pointer] = sig;
                    // Банку ставят на костёр сразу со снегом/водой: выброс ушёл пустым, а смены сигнатуры не было —
                    // у друга банка оставалась пустой, пока снег не растает. Свой котелок не пустой — шлём сразу.
                    if ((owner != Session.LocalId && !nearMe) || sig.EndsWith("|00") && sig.StartsWith("0||")) continue;
                    MelonLogger.Msg($"[котелок] {ItemSync.PrefabName(gi)}: поставлен ({sig}), отправляю");
                    ItemSync.Resend(gi, gi.transform.position);
                    continue;
                }
                bool inspected = PotInspectAt.TryGetValue(pot.Pointer, out float ia) && now - ia < 3f;
                string fine = inspected ? PotFineSignature(pot) : null;
                if (was == sig)
                {
                    // Выпили или съели часть, не закончив: грубая сигнатура та же, точная — нет.
                    if (fine == null) { PotFine.Remove(pot.Pointer); continue; }
                    if (!PotFine.TryGetValue(pot.Pointer, out var fwas)) { PotFine[pot.Pointer] = fine; continue; }
                    if (fwas == fine) continue;
                    PotFine[pot.Pointer] = fine;
                    // Шлём только убыль воды или калорий: это взяли мы. Снег тает и вода прибывает у всех сама.
                    if (!Less(fine, fwas, 1) && !Less(fine, fwas, 2)) continue;
                    MelonLogger.Msg($"[котелок] {ItemSync.PrefabName(gi)}: из осмотра взяли часть ({fwas} → {fine}), отправляю");
                    ItemSync.Resend(gi, gi.transform.position);
                    continue;
                }
                PotStates[pot.Pointer] = sig;
                if (fine != null) PotFine[pot.Pointer] = fine;
                // Положили или забрали еду (блюдо по рецепту готовится через крафт с полосой, окно к концу уже закрыто):
                // это сделал тот, кто стоит рядом. У второго котелок меняется только заменой — это новый объект.
                // Сырое → готовое меняется у обоих само, это не считаем.
                if (!nearMe && Cooked(was) != Cooked(sig) && (Cooked(was).Length == 0 || Cooked(sig).Length == 0))
                {
                    var me = GameManager.GetPlayerTransform();
                    if (me != null && (me.position - gi.transform.position).sqrMagnitude <= 25f) nearMe = true;   // 5 м
                }
                if (!nearMe && owner != Session.LocalId) continue;
                MelonLogger.Msg($"[котелок] {ItemSync.PrefabName(gi)}: {was} → {sig}, отправляю");
                ItemSync.Resend(gi, gi.transform.position);
            }
            TickLights();
            try { PotAmounts.Tick(now); } catch (Exception e) { MelonLogger.Warning("[котелок] точное количество: " + e.Message); }
        }

        // Одно место у огня — одна посуда. Игра считает место свободным, если вещь на нём уничтожена или пуста ссылка
        // (GearPlacePoint.FindFirstEmptyPlacePoint), и сама ищет, что стоит на месте, только после загрузки
        // (CheckForPlacedGearAfterLoad). Чужие котелки и банки появляются у нас мимо этого (замена, сверка локации), и
        // место под ними могло остаться «свободным» — второй ставил туда своё, вещи вставали одна в другую. Раз в секунду
        // свободные места спрашивают игру, не стоит ли на них котелок или банка (FindGearAtPlacePoint — тот же поиск, что после загрузки).
        private static int _slotsFixed;
        private static void TickPlaceSlots()
        {
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                if (pm != null && pm.IsInMeshPlacementMode()) return;   // я сам сейчас ставлю вещь — не мешаем
            }
            catch { }
            try { ItemSync.ProcessSlotTakes(); } catch { }
            foreach (var gpp in Scan<GearPlacePoint>())
            {
                try
                {
                    if (gpp == null || !gpp.gameObject.activeInHierarchy) continue;
                    var was = gpp.m_PlacedGear;
                    if (ItemSync.SlotHolds(was)) continue;
                    var now = gpp.FindGearAtPlacePoint();
                    if (!ItemSync.SlotHolds(now) || now.GetComponent<CookingPotItem>() == null) continue;
                    // Как RefreshPlacedGear, но только когда на месте правда что-то стоит (иначе игра каждый раз
                    // «ставила» бы пустоту: обработчики места, подставка, огонь).
                    gpp.ItemPlaced(null, now);
                    if (_slotsFixed++ < 20) MelonLogger.Msg($"[котелок] место у огня числилось свободным, а на нём стоит {ItemSync.PrefabName(now)} — занял");
                }
                catch { }
            }
        }

        // Лампа, факел, фальшфейер и фонарик на земле: включил, выключил, зажёг, догорел — у всех так же. Шлёт тот, кто стоит рядом (он и щёлкнул).
        private static readonly Dictionary<IntPtr, int> LightStates = new();

        private static void TickLights()
        {
            var me = GameManager.GetPlayerTransform();
            if (me == null) return;
            // Ищем только лампы и факелы, а не все вещи локации: их сотни, а проверка раз в секунду.
            foreach (var lamp in Scan<Il2CppTLD.Gear.KeroseneLampItem>())
            {
                if (lamp == null) continue;
                int st;
                try { st = lamp.IsOn() ? 1 : 0; } catch { continue; }
                CheckLight(lamp.GetComponent<GearItem>(), st, me.position);
            }
            foreach (var torch in Scan<TorchItem>())
            {
                if (torch == null) continue;
                CheckLight(torch.GetComponent<GearItem>(), 10 + (int)torch.m_State, me.position);
            }
            // Фальшфейеры (красный и синий) и фонарики (аврора, фонарь-динамо из DLC): догорел, погас, включили.
            foreach (var flare in Scan<FlareItem>())
            {
                if (flare == null) continue;
                CheckLight(flare.GetComponent<GearItem>(), 20 + (int)flare.m_State, me.position);
            }
            foreach (var fl in Scan<FlashlightItem>())
            {
                if (fl == null) continue;
                int st;
                try { st = 30 + (fl.IsOn() ? 1 : 0); } catch { continue; }
                CheckLight(fl.GetComponent<GearItem>(), st, me.position);
            }
        }

        private static void CheckLight(GearItem gi, int st, Vector3 me)
        {
            if (gi == null || !InWorld(gi)) return;
            if (!LightStates.TryGetValue(gi.Pointer, out int was)) { LightStates[gi.Pointer] = st; return; }
            if (was == st) return;
            LightStates[gi.Pointer] = st;
            // Щёлкнул тот, кто рядом (лампу на столе гасят и с 3 м). Погасла сама (кончился керосин) — шлёт тот, чья
            // она: раньше дальше 2.5 м не слал никто, и у остальных фонарь светил дальше (друзья, 0.19.44).
            bool near = (gi.transform.position - me).sqrMagnitude <= 4f * 4f;
            int owner = ItemSync.OwnerOf(gi);
            bool mine = owner == Session.LocalId || (owner < 0 && Session.Mode == Mode.Host);
            if (!near && !mine) return;
            MelonLogger.Msg($"[свет] {ItemSync.PrefabName(gi)}: {was} → {st}, отправляю ({(near ? "я рядом" : "моя вещь")})");
            ItemSync.Resend(gi, gi.transform.position);
        }

        // ---------- Размещённые, брошенные вещи и стрелы ----------

        private enum WatchKind { Placed, Thrown }

        private class Watch
        {
            public GearItem Gi;
            public WatchKind Kind;
            public bool WasWorld;
            public Vector3 OldPos, LastPos;
            public float Due, Until;
            public int Still;
            public bool Early;   // бросок: в полёте уже разослали (с начальной скоростью)
        }

        private static readonly List<Watch> Watching = new();
        private static GearItem _placing;
        private static bool _placingWasWorld;
        private static Vector3 _placingPos;

        private static bool Shared => Session.Mode != Mode.Off && Session.InSharedWorld && GameApi.InGame();

        internal static bool InWorld(GearItem gi) =>
            gi != null && !gi.m_InPlayerInventory && !gi.m_InsideContainer && gi.gameObject.activeInHierarchy;

        internal static void OnPlaceStart(GearItem gi)
        {
            _placing = gi;
            if (gi == null) return;
            _placingWasWorld = InWorld(gi);
            _placingPos = gi.transform.position;
        }

        internal static void OnPlaceEnd(GearItem gi)
        {
            gi ??= _placing;
            if (gi == null || !Shared) { _placing = null; return; }
            bool same = _placing != null && _placing.Pointer == gi.Pointer;
            float now = Time.unscaledTime;
            Watching.Add(new Watch { Gi = gi, Kind = WatchKind.Placed, WasWorld = same && _placingWasWorld, OldPos = same ? _placingPos : gi.transform.position, Due = now + 0.5f, Until = now + 5f });
            _placing = null;
        }

        internal static void OnThrown(GearItem gi)
        {
            if (gi == null || !Shared) return;
            float now = Time.unscaledTime;
            // Брошенное (не стрела) рассылаем сразу, со скоростью, — у остальных оно летит; где легло, пришлём, когда остановится.
            bool early = gi.m_ArrowItem == null;
            Watching.Add(new Watch { Gi = gi, Kind = WatchKind.Thrown, LastPos = gi.transform.position, Due = now + (early ? 0.05f : 0.5f), Until = now + 30f, Early = !early });
        }

        // Вещь лежала в локации и сдвинулась сама (задели, скатилась): когда остановится, шлём замену
        // с тем же номером и местом, где она лежала, — ItemUpdate найдёт у остальных их копию и передвинет её.
        internal static void OnPushed(GearItem gi, Vector3 restPos)
        {
            if (gi == null || !Shared) return;
            float now = Time.unscaledTime;
            Watching.Add(new Watch { Gi = gi, Kind = WatchKind.Thrown, WasWorld = true, OldPos = restPos, LastPos = gi.transform.position, Due = now + 0.5f, Until = now + 20f, Early = true });
        }

        internal static bool IsWatched(GearItem gi)
        {
            if (gi == null) return false;
            foreach (var w in Watching) if (w.Gi != null && w.Gi.Pointer == gi.Pointer) return true;
            return false;
        }

        // Выброшенную вещь уже разослали там, где её выпустили из рук; когда она остановится, пришлём, где легла.
        internal static void WatchDrop(GearItem gi)
        {
            if (gi == null || !Shared) return;
            float now = Time.unscaledTime;
            Watching.Add(new Watch { Gi = gi, Kind = WatchKind.Thrown, LastPos = gi.transform.position, Due = now + 0.5f, Until = now + 20f, Early = true });
        }

        private static void TickWatch(float now)
        {
            for (int i = Watching.Count - 1; i >= 0; i--)
            {
                var w = Watching[i];
                if (now < w.Due) continue;
                w.Due = now + 0.5f;
                var gi = w.Gi;
                // Вещь легла в котелок (начали готовить): её несёт замена котелка.
                if (gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer || ItemSync.InPot(gi)) { Watching.RemoveAt(i); continue; }
                if (!gi.gameObject.activeInHierarchy) { if (now > w.Until) Watching.RemoveAt(i); continue; }
                if (w.Kind == WatchKind.Thrown && !w.Early)
                {
                    w.Early = true;
                    Vector3 vel = Vector3.zero;
                    try { var rb = gi.GetComponent<Rigidbody>(); if (rb != null) vel = rb.velocity; } catch { }
                    if (InWorld(gi) && !ItemSync.TryGetSentPos(gi, out _)) ItemSync.OnLocalDrop(gi, vel);
                    w.LastPos = gi.transform.position;
                    continue;
                }
                if (w.Kind == WatchKind.Thrown)
                {
                    // Стрела в звере уходит вместе с тушей, отдельно её не шлём.
                    if (gi.GetComponentInParent<BaseAi>() != null) { Watching.RemoveAt(i); continue; }
                    bool flying = false;
                    try { flying = gi.m_ArrowItem != null && gi.m_ArrowItem.InFlight(false); } catch { }
                    var p = gi.transform.position;
                    w.Still = !flying && (p - w.LastPos).sqrMagnitude < 0.0004f ? w.Still + 1 : 0;
                    w.LastPos = p;
                    if (w.Still < 2 && now < w.Until) continue;
                }
                Watching.RemoveAt(i);
                Publish(gi, w.WasWorld, w.OldPos);
            }
        }

        // Вещь лежит в мире у нас: рассказываем остальным, где она теперь.
        private static void Publish(GearItem gi, bool wasWorld, Vector3 oldPos)
        {
            if (!InWorld(gi)) return;
            var pos = gi.transform.position;
            if (ItemSync.TryGetSentPos(gi, out var sent))
            {
                if ((pos - sent).sqrMagnitude > 0.0004f) ItemSync.Resend(gi, sent);
            }
            else if (wasWorld)
            {
                if ((pos - oldPos).sqrMagnitude > 0.0004f) ItemSync.Resend(gi, oldPos);
            }
            else ItemSync.OnLocalDrop(gi);
        }

        // ---------- Волокуша (и всё, что игра даёт тащить за собой) ----------
        // Взял тащить — у остальных она исчезает. Поставил — у остальных появляется там же, с тем же грузом.

        private static Il2CppTLD.BigCarry.BigCarryItem _carried;

        internal static void OnCarry(Il2CppTLD.BigCarry.BigCarryItem item)
        {
            _carried = item;
            if (item == null || WorldObjects.Applying) return;
            WorldObjects.Send(WorldObjects.Kind.BigCarry, item, "TAKE");
        }

        internal static void OnDropFinished()
        {
            var item = _carried;
            _carried = null;
            SendPut(item);
        }

        // Волокуша убрана в рюкзак: у остальных исчезает.
        internal static void OnTravoisPickup(Il2CppTLD.BigCarry.BigCarryItem item)
        {
            if (item == null || WorldObjects.Applying) return;
            WorldObjects.Send(WorldObjects.Kind.BigCarry, item, "TAKE");
        }

        // Волокуша из рюкзака поставлена на землю: игра создаёт её заново, ищем новую чуть позже.
        private static readonly HashSet<IntPtr> KnownCarry = new();
        private static float _scanCarryAt = -1f;

        internal static void OnTravoisDropped() => _scanCarryAt = Time.unscaledTime + 0.7f;

        private static void TickCarry(float now)
        {
            bool scan = _scanCarryAt >= 0f && now >= _scanCarryAt;
            if (scan) _scanCarryAt = -1f;
            foreach (var it in UnityEngine.Object.FindObjectsOfType<Il2CppTLD.BigCarry.BigCarryItem>())
            {
                if (it == null || !KnownCarry.Add(it.Pointer)) continue;
                if (scan) SendPut(it);
            }
        }

        private static void SendPut(Il2CppTLD.BigCarry.BigCarryItem item)
        {
            if (item == null || WorldObjects.Applying) return;
            KnownCarry.Add(item.Pointer);
            try
            {
                string key = item.m_AssetReference?.AssetGUID ?? "";
                var t = item.transform;
                var p = t.position;
                var q = t.rotation;
                string json = Il2Cpp.FastJson.Serialize(item.Serialize()) ?? "";
                WorldObjects.Send(WorldObjects.Kind.BigCarry, item,
                    string.Join(Sep, "PUT", key, F(p.x), F(p.y), F(p.z), F(q.x), F(q.y), F(q.z), F(q.w), json));
            }
            catch (Exception e) { MelonLogger.Warning("[объекты] волокуша: " + e.Message); }
        }

        private static Il2CppTLD.BigCarry.BigCarryItem FindCarry(string id)
        {
            foreach (var it in UnityEngine.Object.FindObjectsOfType<Il2CppTLD.BigCarry.BigCarryItem>())
                if (it != null && WorldObjects.IdOf(it) == id) return it;
            return null;
        }

        private static Component ApplyCarry(string id, string state)
        {
            var sys = Il2CppTLD.BigCarry.BigCarrySystem.Instance;
            var item = FindCarry(id);
            var parts = state.Split(Sep);
            if (parts[0] == "TAKE")
            {
                if (item == null) return null;
                // Запоминаем, как она выглядит (с грузом), чтобы показывать её за тем, кто её тащит.
                HeldItems.Remember(item.m_AssetReference?.AssetGUID, item.gameObject);
                try { sys?.RemoveItem(item); } catch { }
                UnityEngine.Object.Destroy(item.gameObject);
                MelonLogger.Msg("[объекты] другой игрок взял тащить: " + item.gameObject.name);
                return null;
            }
            if (parts[0] != "PUT" || parts.Length < 10 || sys == null) return item;
            var pos = new Vector3(P(parts[2]), P(parts[3]), P(parts[4]));
            var rot = new Quaternion(P(parts[5]), P(parts[6]), P(parts[7]), P(parts[8]));
            if (item == null)
            {
                item = sys.FindOrCreateBigCarryItem(id, parts[1], out bool made);
                if (item == null) { MelonLogger.Msg("[объекты] не смог создать волокушу " + parts[1]); return null; }
                if (made) MelonLogger.Msg("[объекты] поставил у себя волокушу другого игрока");
                KnownCarry.Add(item.Pointer);
            }
            try { item.Deserialize(Il2Cpp.FastJson.Deserialize<Il2CppTLD.BigCarry.BigCarryItemSaveData>(parts[9]), false); }
            catch (Exception e) { MelonLogger.Warning("[объекты] волокуша, груз: " + e.Message); }
            item.SetPositionAndRotation(pos, rot);
            if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
            return item;
        }

        // ---------- Каждый кадр ----------

        private static string _scene = "";
        private static float _nextSnares, _nextCarry;

        public static void Tick(float now)
        {
            if (!Shared) { if (!GameApi.InGame()) { _scene = ""; Watching.Clear(); } return; }
            string scene = ItemSync.SceneKey();
            if (scene != _scene)
            {
                _scene = scene;
                SnareStates.Clear();
                PotStates.Clear();
                PotNearAt.Clear();
                PotInspectAt.Clear();
                PotFine.Clear();
                Scans.Clear();
                LightStates.Clear();
                LastRope.Clear();
                KnownCarry.Clear();
                Watching.Clear();
            }
            try
            {
                TickWatch(now);
                if (ItemSync.Settled) TickHoles(now);
                if (ItemSync.Settled && now >= _nextSnares) { _nextSnares = now + 1f; TickSnares(); TickPots(now); TickPlaceSlots(); }
                if (ItemSync.Settled && (_scanCarryAt >= 0f ? now >= _scanCarryAt : now >= _nextCarry)) { _nextCarry = now + 10f; TickCarry(now); }   // полный поиск волокуш дорогой; после «поставил» есть _scanCarryAt
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[объекты] " + e.Message);
            }
        }

        public static void Reset()
        {
            Rolled.Clear();
            RolledByMe.Clear();
            LockKnown.Clear();
            LastRope.Clear();
            SnareStates.Clear();
            PotStates.Clear();
            PotNearAt.Clear();
            LightStates.Clear();
            Watching.Clear();
            _placing = null;
            _carried = null;
            _scene = "";
        }
    }

    [HarmonyPatch(typeof(Il2CppTLD.BigCarry.BigCarrySystem), nameof(Il2CppTLD.BigCarry.BigCarrySystem.Carry))]
    internal static class Patch_CarryStart
    {
        private static void Prefix(Il2CppTLD.BigCarry.BigCarryItem carryItem) => WorldExtra.OnCarry(carryItem);
    }

    [HarmonyPatch(typeof(Il2CppTLD.Gear.TravoisItem), nameof(Il2CppTLD.Gear.TravoisItem.DropTravois))]
    internal static class Patch_TravoisFromPack
    {
        private static void Postfix() => WorldExtra.OnTravoisDropped();
    }

    [HarmonyPatch(typeof(Il2CppTLD.BigCarry.TravoisBigCarryItem), nameof(Il2CppTLD.BigCarry.TravoisBigCarryItem.PickupCallback))]
    internal static class Patch_TravoisToPack
    {
        private static void Prefix(Il2CppTLD.BigCarry.TravoisBigCarryItem __instance) => WorldExtra.OnTravoisPickup(__instance);
    }

    [HarmonyPatch(typeof(Il2CppTLD.BigCarry.BigCarrySystem), nameof(Il2CppTLD.BigCarry.BigCarrySystem.FinishDrop))]
    internal static class Patch_CarryDropped
    {
        private static void Postfix() => WorldExtra.OnDropFinished();
    }

    // ---------- Патчи ----------

    [HarmonyPatch(typeof(BrokenDecorationInteraction), nameof(BrokenDecorationInteraction.OnRepairSuccessful))]
    internal static class Patch_DecorationRepaired
    {
        private static void Postfix(BrokenDecorationInteraction __instance) => WorldObjects.Send(WorldObjects.Kind.Decoration, __instance, "1");
    }

    [HarmonyPatch(typeof(InteractiveLightsource), nameof(InteractiveLightsource.OnInteractComplete))]
    internal static class Patch_LanternToggled
    {
        private static void Postfix(InteractiveLightsource __instance, bool success)
        {
            if (!success || Session.Mode == Mode.Off || __instance == null) return;
            bool on = false;
            try { on = __instance.m_IsOn; } catch { return; }
            WorldObjects.Send(WorldObjects.Kind.Lantern, __instance, on ? "1" : "0");
        }
    }

    [HarmonyPatch(typeof(RopeAnchorPoint), nameof(RopeAnchorPoint.SetRopeActiveState))]
    internal static class Patch_RopeState
    {
        private static void Postfix(RopeAnchorPoint __instance, bool isInteraction) { if (isInteraction) WorldExtra.SendRope(__instance); }
    }

    [HarmonyPatch(typeof(RopeAnchorPoint), nameof(RopeAnchorPoint.ActionFinished))]
    internal static class Patch_RopeAction
    {
        private static void Postfix(RopeAnchorPoint __instance) => WorldExtra.SendRope(__instance);
    }

    [HarmonyPatch(typeof(Lock), nameof(Lock.RollLockedState))]
    internal static class Patch_LockRoll
    {
        private static void Postfix(Lock __instance) => WorldExtra.OnLockRolled(__instance);
    }

    [HarmonyPatch(typeof(Lock), nameof(Lock.FinishForceLock))]
    internal static class Patch_LockForce
    {
        private static void Postfix(Lock __instance, bool complete) { if (complete) WorldExtra.OnLockForced(__instance); }
    }

    [HarmonyPatch(typeof(SnowShelter), nameof(SnowShelter.BuildFinished))]
    internal static class Patch_ShelterBuilt
    {
        private static void Postfix(SnowShelter __instance) => WorldExtra.SendShelter(__instance, false);
    }

    [HarmonyPatch(typeof(SnowShelter), nameof(SnowShelter.RepairFinished))]
    internal static class Patch_ShelterRepaired
    {
        private static void Postfix(SnowShelter __instance) => WorldExtra.SendShelter(__instance, false);
    }

    [HarmonyPatch(typeof(SnowShelter), nameof(SnowShelter.DismantleFinished))]
    internal static class Patch_ShelterDismantled
    {
        private static void Prefix(SnowShelter __instance) => WorldExtra.SendShelter(__instance, true);
    }

    [HarmonyPatch(typeof(IceFishingHole), nameof(IceFishingHole.OnBuild))]
    internal static class Patch_HoleBuilt
    {
        private static void Postfix() => WorldExtra.SendHoles();
    }

    [HarmonyPatch(typeof(IceFishingHole), nameof(IceFishingHole.ClearHole))]
    internal static class Patch_HoleCleared
    {
        private static void Postfix() => WorldExtra.SendHoles();
    }

    [HarmonyPatch(typeof(IceFishingHole), nameof(IceFishingHole.StartFishing))]
    internal static class Patch_TipupPlaced
    {
        private static void Postfix(FishingState fishingType) { if (fishingType == FishingState.PassiveFishing) WorldExtra.SendHolesSoon(); }
    }

    [HarmonyPatch(typeof(IceFishingHole), nameof(IceFishingHole.CollectFish))]
    internal static class Patch_TipupFishTaken
    {
        private static void Postfix() => WorldExtra.SendHolesSoon();
    }

    [HarmonyPatch(typeof(IceFishingHole), nameof(IceFishingHole.MaybeDestroyTipup))]
    internal static class Patch_TipupDestroyed
    {
        private static void Postfix() => WorldExtra.SendHolesSoon();
    }

    [HarmonyPatch(typeof(IceFishingHole), nameof(IceFishingHole.PickupTackle))]
    internal static class Patch_TipupPickedUp
    {
        private static void Postfix() => WorldExtra.SendHolesSoon();
    }

    // Выловленность места: игра считает соседние лунки, где идёт ловля. Жерлицы и так общие, а друзья,
    // которые рядом ловят удочкой, у нас видны только фигурой — добавляем их к счёту.
    [HarmonyPatch(typeof(IceFishingHole), nameof(IceFishingHole.CountNearbyActiveFishingHoles))]
    internal static class Patch_FriendsFishingNearby
    {
        private static void Postfix(IceFishingHole __instance, ref int __result)
        {
            try
            {
                if (__instance == null || Session.Mode == Mode.Off || !Session.InSharedWorld) return;
                var at = __instance.transform.position;
                float r = __instance.m_OverfishingRange;
                string scene = GameApi.Scene();
                foreach (var p in Session.Players.Values)
                    if (p.Id != Session.LocalId && Session.With(p, scene) && p.Action == 10 && Time.unscaledTime - p.LastState < 5f
                        && (new Vector3(p.X, p.Y, p.Z) - at).sqrMagnitude <= r * r) __result++;
            }
            catch { }
        }
    }

    // Клюнуло ли на жерлицу, решает только хозяин животных локации.
    // Клюнуло (HasCaughtFish стало true на жерлице) — лунки сразу всем. SetTipupAnimationTrigger в 2.59 встроен сюда.
    [HarmonyPatch(typeof(IceFishingHole), nameof(IceFishingHole.UpdateFishing))]
    internal static class Patch_TipupTime
    {
        private static bool Prefix(IceFishingHole __instance, out bool __state)
        {
            __state = true;
            if (__instance == null || Session.Mode == Mode.Off) return true;
            try { __state = __instance.HasCaughtFish; } catch { }
            return __instance.m_State != FishingState.PassiveFishing || WorldExtra.MayRollFish();
        }

        private static void Postfix(IceFishingHole __instance, bool __state)
        {
            if (__state || Session.Mode == Mode.Off) return;
            try
            {
                if (__instance != null && __instance.HasCaughtFish && __instance.m_State == FishingState.PassiveFishing && WorldExtra.MayRollFish())
                    WorldExtra.SendHolesSoon();
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(SnareItem), nameof(SnareItem.CheckForCatch))]
    internal static class Patch_SnareCheck
    {
        private static bool Prefix() => WorldExtra.MayRollSnare();
    }

    [HarmonyPatch(typeof(SnareItem), nameof(SnareItem.DoRoll))]
    internal static class Patch_SnareRoll
    {
        private static bool Prefix() => WorldExtra.MayRollSnare();
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.StartPlaceMesh),
        new[] { typeof(GameObject), typeof(PlaceMeshFlags), typeof(Il2CppTLD.Placement.PlaceMeshRules) })]
    internal static class Patch_PlaceStart
    {
        private static void Postfix(PlayerManager __instance, bool __result) { if (__result) WorldExtra.OnPlaceStart(__instance.m_ObjectToPlaceGearItem); }
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.StartPlaceMesh),
        new[] { typeof(GameObject), typeof(float), typeof(PlaceMeshFlags), typeof(Il2CppTLD.Placement.PlaceMeshRules) })]
    internal static class Patch_PlaceStart2
    {
        private static void Postfix(PlayerManager __instance, bool __result) { if (__result) WorldExtra.OnPlaceStart(__instance.m_ObjectToPlaceGearItem); }
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.CleanUpPlaceMesh))]
    internal static class Patch_PlaceEnd
    {
        private static void Prefix(PlayerManager __instance) => WorldExtra.OnPlaceEnd(__instance.m_ObjectToPlaceGearItem);
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.InstantiateCopyOfThrownObject))]
    internal static class Patch_Thrown
    {
        internal static IntPtr LastCopy;

        private static void Postfix(GearItem __result)
        {
            if (__result != null) LastCopy = __result.Pointer;
            HeldItems.OnThrow(); WorldExtra.OnThrown(__result);
        }
    }

    // Фальшфейер, факел и шумелку игра бросает сам предмет из рук (ReleaseFromHands), а не копию, как камень:
    // Patch_Thrown их не видел — у остальных фигура не замахивалась, а предмет не летел, а появлялся там, где лёг.
    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.ReleaseThrownObject))]
    internal static class Patch_ThrownFromHands
    {
        private static void Prefix(PlayerManager __instance, out GearItem __state)
        {
            __state = null;
            try
            {
                var gi = __instance.m_ThrownItem;
                if (gi != null && (gi.m_FlareItem != null || gi.m_TorchItem != null || gi.m_NoiseMakerItem != null)) __state = gi;
            }
            catch { }
        }

        private static void Postfix(GearItem __state)
        {
            if (__state == null || __state.Pointer == Patch_Thrown.LastCopy) return;   // копию уже посчитал Patch_Thrown
            HeldItems.OnThrow();
            WorldExtra.OnThrown(__state);
        }
    }

    [HarmonyPatch(typeof(ArrowItem), nameof(ArrowItem.Fire))]
    internal static class Patch_ArrowFired
    {
        private static void Postfix(ArrowItem __instance) => WorldExtra.OnThrown(__instance.GetComponent<GearItem>());
    }
}
