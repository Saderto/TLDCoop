using System;
using HarmonyLib;
using Il2Cpp;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // «Занято»: пока один игрок пользуется вещью мира, остальные не могут начать с ней то же самое.
    // Это трос (лезет по нему), печь или конфорка (разжигает огонь), туша (разделывает), верстак
    // и контейнер (обыскивает). Своё занятое уходит вместе с позицией (ключ объекта), поэтому
    // как только игрок закрыл окно, отошёл или вышел из игры, вещь сама освобождается.
    public static class Busy
    {
        private static string _cached = "";
        private static float _next;

        // Что занято нами сейчас (ключ объекта или пусто). Проверяем два раза в секунду.
        public static string Current()
        {
            float now = Time.unscaledTime;
            if (now < _next) return _cached;
            _next = now + 0.5f;
            try { _cached = Detect(); } catch { _cached = ""; }
            return _cached;
        }

        private static string Detect()
        {
            if (!GameApi.InGame()) return "";
            string car = Cars.OwnKey();
            if (car.Length > 0) return car;   // в машине — это место и есть наше занятое (Game/Cars.cs)
            string bed = BedPose.Key();
            if (bed.Length > 0) return bed;   // сплю в кровати/спальнике: где лежит фигура (Game/BedPose.cs)
            var pm = GameManager.GetPlayerManagerComponent();
            if (pm != null && pm.PlayerIsClimbing())
            {
                var rope = GameManager.GetPlayerClimbRopeComponent()?.m_Rope;
                if (rope != null) return RopeKey(rope);
            }
            if (InterfaceManager.TryGetPanel<Panel_BodyHarvest>(out var bh) && bh.IsEnabled() && bh.m_BodyHarvest != null)
                return CarcassKey(bh.m_BodyHarvest);
            if (InterfaceManager.TryGetPanel<Panel_FireStart>(out var fs) && fs.IsEnabled())
            {
                var me = GameManager.GetPlayerTransform();
                var fire = me != null ? Nearest<Fire>(me.position, 3f) : null;
                if (fire != null) return FireKey(fire);
            }
            if ((InterfaceManager.TryGetPanel<Panel_Crafting>(out var cr) && cr.IsEnabled()) ||
                (InterfaceManager.TryGetPanel<Panel_BreakDown>(out var bd) && bd.IsEnabled()))
            {
                var me = GameManager.GetPlayerTransform();
                var wb = me != null ? Nearest<WorkBench>(me.position, 3f) : null;
                if (wb != null) return BenchKey(wb);
            }
            // Окно «сколько штук» (Panel_PickUnits) при перекладывании пачки прячет окно ящика (NGUITools.SetActive false
            // в Panel_Container.OnContainerToInventory/OnInventoryToContainer), но ящик всё ещё открыт у меня. Раньше в
            // это время я считался свободным, второй открывал тот же ящик, и при закрытии одна из версий затирала
            // другую — положенное пропадало у обоих (0.19.39).
            if (WorldObjects.OpenContainer != null && InterfaceManager.TryGetPanel<Panel_Container>(out var ct) &&
                (ct.IsEnabled() || (InterfaceManager.TryGetPanel<Panel_PickUnits>(out var pu) && pu.IsEnabled())))
                return ContainerKey(WorldObjects.OpenContainer);
            // Набираю воду из бачка (0.19.23): второй, открыв окно по старому количеству, набрал бы из полного бачка.
            if (InterfaceManager.TryGetPanel<Panel_PickWater>(out var pw) && pw.IsEnabled() && pw.m_WaterSource != null)
                return WaterKey(pw.m_WaterSource);
            // Осматриваю воду/еду из котелка или открыто его окно готовки (0.19.23): второй мог забрать этот котелок,
            // и вода или еда из него раздваивалась.
            var pot = WorldExtra.PotInUse();
            if (pot != null) return PotKey(pot.GetComponent<GearItem>());
            return "";
        }

        private static string RopeKey(Rope r) => "rope:" + WorldObjects.IdOf(r);
        private static string CarcassKey(BodyHarvest b) => "body:" + AnimalSync.CarcassKey(b);
        private static string FireKey(Fire f) => "fire:" + WorldObjects.IdOf(f);
        private static string BenchKey(WorkBench w) => "bench:" + WorldObjects.IdOf(w);
        private static string ContainerKey(Container c) => "box:" + WorldObjects.IdOf(c);
        internal static string WaterKey(WaterSource w) => "water:" + WorldObjects.IdOf(w);
        private static string PotKey(GearItem gi) => "pot:" + ItemSync.ShareKey(gi);

        private static T Nearest<T>(Vector3 p, float max) where T : Component
        {
            T best = null;
            float bd = max * max;
            foreach (var c in UnityEngine.Object.FindObjectsOfType<T>())
            {
                if (c == null) continue;
                float d = (c.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        // Кто из других игроков занял этот объект (null — никто).
        private static string Who(string key)
        {
            if (string.IsNullOrEmpty(key) || Session.Mode == Mode.Off || !Session.InSharedWorld) return null;
            float now = Time.unscaledTime;
            string scene = GameApi.Scene();
            foreach (var p in Session.Players.Values)
                if (p.Id != Session.LocalId && p.Busy == key && Session.With(p, scene) && now - p.LastState < 3f) return p.Name;
            return null;
        }

        // Для патчей: можно ли начать. Нельзя — показываем, кем занято.
        private static bool Allow(string key)
        {
            string who;
            try { who = Who(key); } catch { return true; }
            if (who == null) return true;
            GameApi.HudMessage("Занято: " + who);
            return false;
        }

        public static bool AllowKey(string key) => Allow(key);
        public static bool AllowWater(WaterSource w) => w == null || Allow(WaterKey(w));

        // Котелок: ключ «номер|место», совпадение по любой части (у одного игрока номер может быть, у другого нет).
        public static bool AllowPot(GearItem gi)
        {
            if (gi == null || gi.GetComponent<CookingPotItem>() == null || Session.Mode == Mode.Off || !Session.InSharedWorld) return true;
            string who = null;
            try
            {
                var mine = ItemSync.ShareKey(gi).Split('|');
                float now = Time.unscaledTime;
                string scene = GameApi.Scene();
                foreach (var p in Session.Players.Values)
                {
                    if (p.Id == Session.LocalId || p.Busy == null || !p.Busy.StartsWith("pot:") || !Session.With(p, scene) || now - p.LastState >= 3f) continue;
                    var theirs = p.Busy.Substring(4).Split('|');
                    if (theirs.Length < 2) continue;
                    if ((mine[0].Length > 0 && theirs[0] == mine[0]) || theirs[1] == mine[1]) { who = p.Name; break; }
                }
            }
            catch { return true; }
            if (who == null) return true;
            GameApi.HudMessage("Занято: " + who);
            return false;
        }
        public static bool AllowRope(RopeClimbPoint p) => p == null || p.m_Rope == null || Allow(RopeKey(p.m_Rope));
        public static bool AllowCarcass(BodyHarvest b) => b == null || Allow(CarcassKey(b));
        public static bool AllowBench(WorkBench w) => w == null || Allow(BenchKey(w));
        public static bool AllowContainer(Container c) => c == null || Allow(ContainerKey(c));

        public static bool AllowFire(Component near)
        {
            if (near == null) return true;
            var fire = near.GetComponent<Fire>() ?? Nearest<Fire>(near.transform.position, 3f);
            return fire == null || Allow(FireKey(fire));
        }
    }

    public static partial class AnimalSync
    {
        // Туша зверя из общей жизни узнаётся по общему номеру, остальные (из сохранения) — как обычный объект.
        public static string CarcassKey(BodyHarvest bh)
        {
            if (bh == null) return "";
            foreach (var a in Animals.Values)
                if (a.Go != null && a.Go.Pointer == bh.gameObject.Pointer) return "a" + a.Gid;
            return WorldObjects.IdOf(bh);
        }
    }

    [HarmonyPatch(typeof(RopeClimbPoint), nameof(RopeClimbPoint.PerformInteraction))]
    internal static class Patch_BusyRope
    {
        private static bool Prefix(RopeClimbPoint __instance) => Busy.AllowRope(__instance);
    }

    [HarmonyPatch(typeof(BodyHarvest), nameof(BodyHarvest.InteractWithBodyHarvest))]
    internal static class Patch_BusyCarcass
    {
        private static bool Prefix(BodyHarvest __instance) => Busy.AllowCarcass(__instance);
    }

    [HarmonyPatch(typeof(FireplaceInteraction), nameof(FireplaceInteraction.AttemptToStartFire))]
    internal static class Patch_BusyFireplace
    {
        private static bool Prefix(FireplaceInteraction __instance) => Busy.AllowFire(__instance);
    }

    [HarmonyPatch(typeof(WorkBench), nameof(WorkBench.InteractWithWorkbench))]
    internal static class Patch_BusyBench
    {
        private static bool Prefix(WorkBench __instance) => Busy.AllowBench(__instance);
    }

    // Бачок: занят другим — нельзя; свободен — ещё спросить хоста, чтобы двое в одну секунду не открыли окно оба.
    [HarmonyPatch(typeof(WaterSource), nameof(WaterSource.PerformInteraction))]
    internal static class Patch_BusyWater
    {
        private static bool Prefix(WaterSource __instance)
        {
            if (!Busy.AllowWater(__instance)) return false;
            var ws = __instance;
            try { return Claims.Allow(Busy.WaterKey(ws), () => ws.PerformInteraction()); } catch { return true; }
        }
    }

    // Котелок, которым пользуется другой (осматривает воду/еду, открыто окно готовки): не трогать и не забирать.
    [HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.PerformInteraction))]
    internal static class Patch_BusyPot
    {
        private static bool Prefix(CookingPotItem __instance) => Busy.AllowPot(__instance.GetComponent<GearItem>());
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.ProcessPickupItemInteraction))]
    internal static class Patch_BusyPotPickup
    {
        private static bool Prefix(GearItem item) => item == null || item.m_InPlayerInventory || Busy.AllowPot(item);
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.ProcessPickupWithNoInspectScreen))]
    internal static class Patch_BusyPotPickupQuick
    {
        private static bool Prefix(GearItem pickupItem) => pickupItem == null || pickupItem.m_InPlayerInventory || Busy.AllowPot(pickupItem);
    }

    [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.EnterInspectGearMode),
        new[] { typeof(GearItem), typeof(Container), typeof(IceFishingHole), typeof(Harvestable), typeof(CookingPotItem) })]
    internal static class Patch_BusyPotInspect
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(GearItem gear, Container c, IceFishingHole hole, CookingPotItem pot)
        {
            if (c != null || hole != null) return true;
            if (pot != null) return Busy.AllowPot(pot.GetComponent<GearItem>());
            return gear == null || gear.m_InPlayerInventory || gear.m_InsideContainer || Busy.AllowPot(gear);
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.BeginContainerOpen))]
    internal static class Patch_BusyContainer
    {
        private static bool Prefix(Container __instance) => Busy.AllowContainer(__instance);
    }
}
