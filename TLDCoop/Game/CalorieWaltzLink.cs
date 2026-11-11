using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Связь с FoodsByCalorieWaltz без жёсткой ссылки на его dll (мода может не быть).
    // 1) Карточку из пряника, которую игрок «положил», мод ставит ему под ноги без Drop: мы её не видели,
    //    и при следующем заходе она записывалась как квестовая вещь локации — копию получал каждый (и свечу при подборе).
    //    Отмечаем её как выброшенную своим игроком (QuestItems.OnDropped).
    // 2) Свечу-медведя с лебёдки (GEAR_candlePurpleD) после осмотра мод тоже ставит под ноги: шлём остальным замену.
    // 3) Свечу зажгли или задули на месте (мод пропускает обычное взаимодействие): шлём замену свечи с новым
    //    состоянием, иначе у другого она не горела, не сгорала и оставалась целой.
    // 4) Свечу погасили пулей или стрелой (префиксы мода GetBulletHit/GetArrowHit): так же, как в 3).
    //    Чужие пули и стрелы у нас не летят (Features.Impacts, WorldExtra), поэтому эха нет.
    //    Выстрел, который доломал свечу (keepFigurines выключен), мод уничтожает без ToggleLight: остальным — подбор.
    //    Негорящей свече выстрел только снимает прочность: остальным — замена.
    // В одиночной игре всё как в моде.
    internal static class CalorieWaltzLink
    {
        private static FieldInfo _currentCard;   // GetCardWhenDiscarded.currentCard
        private static FieldInfo _currentBear;   // CatchInteractionWithCandle.currentBear (что под прицелом)
        private static FieldInfo _thisGear;      // CollectibleCandleComponent.thisGearItem
        private static FieldInfo _options, _keepFigurines;   // Settings.options, CWSettings.keepFigurines

        private static GearItem _card, _inspected;
        private static Vector3 _inspectFrom;

        // Свеча, с которой игрок только что взаимодействовал: первое переключение огня у неё уходит остальным.
        private static IntPtr _touched;
        private static float _touchedAt = -100f;

        // Свеча под выстрелом (префикс → постфикс того же попадания) и её состояние до урона мода.
        private static GearItem _shot;
        private static float _shotHp;
        private static bool _shotWorn;

        public static void Patch(HarmonyLib.Harmony h)
        {
            Type card, catcher, candle;
            try
            {
                card = AccessTools.TypeByName("CalorieWaltz.GetCardWhenDiscarded");
                catcher = AccessTools.TypeByName("CalorieWaltz.CatchInteractionWithCandle");
                candle = AccessTools.TypeByName("CalorieWaltz.CollectibleCandleComponent");
            }
            catch (Exception e) { MelonLogger.Warning("[моды] поиск FoodsByCalorieWaltz: " + e.Message); return; }
            if (card == null && candle == null) return;

            try
            {
                _currentCard = card != null ? AccessTools.Field(card, "currentCard") : null;
                if (_currentCard != null && (!_currentCard.IsStatic || _currentCard.FieldType != typeof(GearItem))) _currentCard = null;
                var exit = AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.ExitInspectGearMode), new[] { typeof(bool) });
                if (exit != null)
                {
                    h.Patch(exit, prefix: new HarmonyMethod(typeof(CalorieWaltzLink), nameof(ExitPrefix)),
                        postfix: new HarmonyMethod(typeof(CalorieWaltzLink), nameof(ExitPostfix)) { priority = Priority.Last });
                }
            }
            catch (Exception e) { MelonLogger.Warning("[моды] CalorieWaltz, осмотр: " + e.Message); }

            if (candle != null && catcher != null)
            {
                try
                {
                    _currentBear = AccessTools.Field(catcher, "currentBear");
                    _thisGear = AccessTools.Field(candle, "thisGearItem");
                    var toggle = AccessTools.Method(candle, "ToggleLight", new[] { typeof(bool), typeof(bool) });
                    var interact = AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.InteractiveObjectsProcessInteraction));
                    if (_currentBear?.FieldType == typeof(GameObject) && _currentBear.IsStatic && _thisGear?.FieldType == typeof(GearItem) && toggle != null && interact != null)
                    {
                        h.Patch(interact, postfix: new HarmonyMethod(typeof(CalorieWaltzLink), nameof(InteractPostfix)));
                        h.Patch(toggle, postfix: new HarmonyMethod(typeof(CalorieWaltzLink), nameof(TogglePostfix)));
                        try
                        {
                            _options = AccessTools.Field(AccessTools.TypeByName("CalorieWaltz.Settings"), "options");
                            _keepFigurines = _options != null ? AccessTools.Field(_options.FieldType, "keepFigurines") : null;
                        }
                        catch { _options = _keepFigurines = null; }
                        var shotDone = new HarmonyMethod(typeof(CalorieWaltzLink), nameof(ShotPostfix));
                        var bullet = AccessTools.Method(typeof(vp_Bullet), nameof(vp_Bullet.SpawnImpactEffects));
                        if (bullet != null) h.Patch(bullet, prefix: new HarmonyMethod(typeof(CalorieWaltzLink), nameof(BulletPrefix)), postfix: shotDone);
                        var arrow = AccessTools.Method(typeof(ArrowItem), nameof(ArrowItem.HandleCollisionWithObject));
                        if (arrow != null) h.Patch(arrow, prefix: new HarmonyMethod(typeof(CalorieWaltzLink), nameof(ArrowPrefix)), postfix: shotDone);
                        MelonLogger.Msg("[моды] FoodsByCalorieWaltz: огонь свечей на месте и карточки «положить» видны остальным");
                    }
                }
                catch (Exception e) { MelonLogger.Warning("[моды] CalorieWaltz, свечи: " + e.Message); }
            }
        }

        private static void ExitPrefix(PlayerManager __instance)
        {
            _card = _inspected = null;
            if (Session.Mode == Mode.Off) return;
            try
            {
                _card = _currentCard?.GetValue(null) as GearItem;
                var gi = __instance.GearItemBeingInspected();
                if (gi != null && gi.name == "GEAR_candlePurpleD" && ItemSync.Inspecting != null && ItemSync.Inspecting.Pointer == gi.Pointer)
                {
                    _inspected = gi;
                    _inspectFrom = ItemSync.InspectPos;
                }
            }
            catch { _card = _inspected = null; }
        }

        // После постфикса мода (он ставит вещь под ноги).
        private static void ExitPostfix()
        {
            var card = _card;
            var gi = _inspected;
            _card = _inspected = null;
            if (Session.Mode == Mode.Off) return;
            try
            {
                if (card != null && !card.m_InPlayerInventory && !card.m_InsideContainer && card.gameObject.activeInHierarchy)
                {
                    ItemSync.OnLocalDrop(card);
                    if (gi != null && gi.Pointer == card.Pointer) gi = null;
                    // Место создания карточки — не её место: поднимут её уже из-под ног.
                    if (ItemSync.Inspecting != null && ItemSync.Inspecting.Pointer == card.Pointer) ItemSync.Inspecting = null;
                }
                if (gi != null && !gi.m_InPlayerInventory && !gi.m_InsideContainer && gi.gameObject.activeInHierarchy &&
                    Vector3.Distance(gi.transform.position, _inspectFrom) > 0.05f)
                {
                    ItemSync.Resend(gi, _inspectFrom);
                    ItemSync.Inspecting = null;   // поднимут уже отсюда, а не с лебёдки
                }
            }
            catch (Exception e) { MelonLogger.Warning("[моды] CalorieWaltz, после осмотра: " + e.Message); }
        }

        private static void InteractPostfix()
        {
            if (Session.Mode == Mode.Off) return;
            try
            {
                var go = _currentBear.GetValue(null) as GameObject;
                if (go == null) return;
                var gi = go.GetComponent<GearItem>();
                if (gi == null || !go.name.StartsWith("GEAR_candle", StringComparison.Ordinal)) return;
                _touched = gi.Pointer;
                _touchedAt = Time.unscaledTime;
            }
            catch { }
        }

        // Своя пуля или стрела попала в огонёк свечи (как в моде: коллайдер flamePoint, свеча — дед коллайдера).
        // Мод гасит её, меняя isLit, а сам огонь игра выключит в Update: первое переключение уйдёт остальным.
        private static void OnShot(GameObject collider)
        {
            if (Session.Mode == Mode.Off || collider == null || collider.name != "flamePoint") return;
            var gi = collider.transform.parent?.parent?.GetComponent<GearItem>();
            if (gi == null || !gi.name.StartsWith("GEAR_candle", StringComparison.Ordinal)) return;
            _touched = gi.Pointer;
            _touchedAt = Time.unscaledTime;
            _shot = gi;
            _shotHp = gi.CurrentHP;
            _shotWorn = gi.m_WornOut;
        }

        private static void BulletPrefix(RaycastHit hit)
        {
            _shot = null;
            try { OnShot(hit.collider != null ? hit.collider.gameObject : null); } catch { }
        }

        private static void ArrowPrefix(GameObject collider)
        {
            _shot = null;
            try { OnShot(collider); } catch { }
        }

        // После префикса мода (урон уже снят).
        private static void ShotPostfix()
        {
            var gi = _shot;
            _shot = null;
            if (Session.Mode == Mode.Off || gi == null) return;
            try
            {
                if (gi.m_InPlayerInventory || gi.m_InsideContainer) return;
                if (gi.m_WornOut && !_shotWorn)
                {
                    // Без keepFigurines мод в следующем Update уничтожит свечу (DestroyWhenSafe): у остальных её убираем.
                    if (KeepFigurines()) return;   // фигурка остаётся, гаснет через ToggleLight — это уйдёт в 3)
                    _touched = IntPtr.Zero;
                    ItemSync.OnLocalPickup(gi, gi.transform.position);
                    return;
                }
                if (gi.m_WornOut || Mathf.Approximately(gi.CurrentHP, _shotHp)) return;
                // Горящую погасит Update через ToggleLight (уйдёт в 3)); негорящей переключения не будет.
                var lights = gi.transform.Find("wick")?.GetChild(0)?.Find("Lights");
                if (lights == null || lights.gameObject.activeSelf) return;
                _touched = IntPtr.Zero;
                ItemSync.Resend(gi, gi.transform.position);
            }
            catch (Exception e) { MelonLogger.Warning("[моды] CalorieWaltz, выстрел по свече: " + e.Message); }
        }

        private static bool KeepFigurines()
        {
            try
            {
                var o = _options?.GetValue(null);
                return o != null && _keepFigurines != null && (bool)_keepFigurines.GetValue(o);
            }
            catch { return false; }
        }

        private static void TogglePostfix(Component __instance)
        {
            if (Session.Mode == Mode.Off || _touched == IntPtr.Zero) return;
            try
            {
                var gi = _thisGear.GetValue(__instance) as GearItem;
                if (gi == null || gi.Pointer != _touched) return;
                bool fresh = Time.unscaledTime - _touchedAt <= 3f;
                _touched = IntPtr.Zero;
                if (!fresh || gi.m_InPlayerInventory || gi.m_InsideContainer) return;
                ItemSync.Resend(gi, gi.transform.position);
            }
            catch (Exception e) { MelonLogger.Warning("[моды] CalorieWaltz, свеча: " + e.Message); }
        }
    }
}
