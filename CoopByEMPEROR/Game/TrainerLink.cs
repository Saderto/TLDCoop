using System;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Связь с TrainerByEMPEROR (он стоит только у хоста) без жёсткой ссылки на его dll (мода может не быть).
    //  • «Скорость игры» (Time.timeScale) и «Заморозка времени» гнут общие часы: у хоста день, огонь, порча, готовка
    //    и звери идут в N раз быстрее (или стоят), у друга — как обычно, и его время каждые несколько секунд
    //    дёргается на 15 минут. Пока в мире есть другой игрок, обе функции выключены (с подсказкой).
    //  • «Открыть рецепты» не делится с друзьями (SharedRecipes): друг их не находил.
    //  • «Убийство с одного попадания» хоста по зверю, которым управляет друг: наш префикс урона пересылает
    //    урон и пропускает игру, и срабатывал ли трейнер до него, зависело от порядка префиксов. Теперь урон
    //    хоста увеличиваем сами раньше всех (OneHit), как это сделал бы трейнер; чужие попадания не трогаем.
    // В одиночной игре и без других игроков трейнер работает как обычно.
    internal static class TrainerLink
    {
        private static FieldInfo _speedApplied, _freezeActive;
        private static MethodInfo _get, _set;
        private static bool _blocked;
        internal static bool Unlocking;   // идёт открытие рецептов трейнером
        private static Func<string, bool> _on;   // State.On(id)

        public static void Patch(HarmonyLib.Harmony h)
        {
            Type cheats, recipes, state;
            try
            {
                cheats = AccessTools.TypeByName("TrainerByEMPEROR.Cheats");
                recipes = AccessTools.TypeByName("TrainerByEMPEROR.Recipes");
                state = AccessTools.TypeByName("TrainerByEMPEROR.State");
            }
            catch (Exception e) { MelonLogger.Warning("[трейнер] поиск TrainerByEMPEROR: " + e.Message); return; }
            if (cheats == null && recipes == null) return;
            int ok = 0;
            try
            {
                if (state != null)
                {
                    _get = AccessTools.Method(state, "Get", new[] { typeof(string) });
                    _set = AccessTools.Method(state, "Set", new[] { typeof(string), typeof(float) });
                    var on = AccessTools.Method(state, "On", new[] { typeof(string) });
                    if (on != null && on.IsStatic && on.ReturnType == typeof(bool)) _on = (Func<string, bool>)Delegate.CreateDelegate(typeof(Func<string, bool>), on);
                }
                if (cheats != null)
                {
                    _speedApplied = AccessTools.Field(cheats, "_gameSpeedApplied");
                    _freezeActive = AccessTools.Field(cheats, "_freezeActive");
                    ok += Try(h, AccessTools.Method(cheats, "GameSpeed", Type.EmptyTypes), prefix: nameof(GameSpeedPrefix));
                    ok += Try(h, AccessTools.Method(cheats, "FreezeTime", Type.EmptyTypes), prefix: nameof(FreezeTimePrefix));
                }
                if (recipes != null)
                {
                    ok += Try(h, AccessTools.Method(recipes, "Unlock", new[] { typeof(int) }), nameof(UnlockPrefix), nameof(UnlockFinalizer));
                    ok += Try(h, AccessTools.Method(recipes, "UnlockAll", Type.EmptyTypes), nameof(UnlockPrefix), nameof(UnlockFinalizer));
                }
            }
            catch (Exception e) { MelonLogger.Warning("[трейнер] " + e.Message); }
            MelonLogger.Msg($"[трейнер] TrainerByEMPEROR найден: кооп-ограничений поставлено {ok} из 4");
        }

        private static int Try(HarmonyLib.Harmony h, MethodInfo m, string prefix = null, string finalizer = null)
        {
            if (m == null) return 0;
            try
            {
                h.Patch(m, prefix: prefix != null ? new HarmonyMethod(typeof(TrainerLink), prefix) : null,
                        finalizer: finalizer != null ? new HarmonyMethod(typeof(TrainerLink), finalizer) : null);
                return 1;
            }
            catch (Exception e) { MelonLogger.Warning($"[трейнер] {m.Name}: {e.Message}"); return 0; }
        }

        // В мире есть другой игрок (как ActionTime.Coop, но и без синхронизации времени: timeScale гонит ещё
        // физику и зверей, которыми управляет хост). Окно 120 с: друг не шлёт состояние, пока грузит локацию.
        private static bool Block
        {
            get
            {
                if (Session.Mode == Mode.Off || !Session.InSharedWorld) return false;
                float now = Time.unscaledTime;
                foreach (var p in Session.Players.Values)
                    if (p.Id != Session.LocalId && p.InWorld && p.Scene.Length > 0 && now - p.LastState < 120f) return true;
                return false;
            }
        }

        private static float Get(string id)
        {
            try { return _get != null ? (float)_get.Invoke(null, new object[] { id }) : 0f; } catch { return 0f; }
        }

        private static void Hint()
        {
            if (_blocked) return;
            _blocked = true;
            MelonLogger.Msg("[трейнер] скорость игры и заморозка времени выключены, пока в мире есть другой игрок");
            GameApi.HudMessage("Кооп: скорость игры и заморозка времени трейнера не работают, пока в мире друг");
        }

        private static bool GameSpeedPrefix()
        {
            if (!Block) { _blocked = false; return true; }
            try
            {
                if (!GameManager.m_IsPaused && Time.timeScale != 0f)
                {
                    bool applied = _speedApplied != null ? (bool)_speedApplied.GetValue(null) : Time.timeScale != 1f;
                    if (applied)
                    {
                        Time.timeScale = 1f;
                        _speedApplied?.SetValue(null, false);
                    }
                }
                if (Math.Abs(Get("gamespeed") - 1f) > 0.01f) Hint();
            }
            catch { }
            return false;
        }

        private static bool FreezeTimePrefix()
        {
            if (!Block) return true;
            try
            {
                if (Get("freezetime") > 0.5f)
                {
                    // Выключаем саму галочку: иначе после коопа трейнер вернул бы старое замороженное время.
                    _set?.Invoke(null, new object[] { "freezetime", 0f });
                    Hint();
                }
                _freezeActive?.SetValue(null, false);
            }
            catch { }
            return false;
        }

        // Свой урон зверю при включённом «одном попадании» — как в префиксе трейнера (Patch_AiApplyDamage3/4).
        // Попадание другого игрока (AnimalSync.HitBy) не трогаем: его урон решил стрелявший.
        internal static void OneHit(BaseAi ai, ref float damage)
        {
            if (_on == null || Session.Mode == Mode.Off || damage <= 0f || ai == null) return;
            if (AnimalSync.HitBy >= 0 && AnimalSync.HitBy != Session.LocalId) return;
            if (_on("onehit")) damage = Mathf.Max(damage, ai.m_MaxHP * 10f + 1000f);
        }

        private static void UnlockPrefix() => Unlocking = true;

        private static void UnlockFinalizer() => Unlocking = false;
    }

    // Попадание другого игрока по нашему зверю: урон такой, как прислал стрелявший. Префиксы других модов
    // (например, «убийство с одного удара» трейнера хоста) меняли его у нас, и чит хоста действовал на выстрелы друга.
    internal static class RemoteHitDamage
    {
        private static float _damage;
        private static bool _have;

        internal static void Keep(float damage)
        {
            _have = AnimalSync.HitBy >= 0 && AnimalSync.HitBy != Session.LocalId;
            _damage = damage;
        }

        internal static void Restore(ref float damage)
        {
            if (!_have) return;
            _have = false;
            damage = _damage;
        }
    }

    // Раньше префикса AnimalSync (Patch_AnimalDamage3/4), который пересылает урон по чужому зверю.
    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), new[] { typeof(float), typeof(float), typeof(DamageSource), typeof(string) })]
    internal static class Patch_TrainerOneHit4
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(BaseAi __instance, ref float damage) { try { TrainerLink.OneHit(__instance, ref damage); } catch { } }
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), new[] { typeof(float), typeof(DamageSource), typeof(string) })]
    internal static class Patch_TrainerOneHit3
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(BaseAi __instance, ref float damage) { try { TrainerLink.OneHit(__instance, ref damage); } catch { } }
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), new[] { typeof(float), typeof(float), typeof(DamageSource), typeof(string) })]
    internal static class Patch_RemoteHitKeep4
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(float damage) { try { RemoteHitDamage.Keep(damage); } catch { } }
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), new[] { typeof(float), typeof(float), typeof(DamageSource), typeof(string) })]
    internal static class Patch_RemoteHitRestore4
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(ref float damage) { try { RemoteHitDamage.Restore(ref damage); } catch { } }
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), new[] { typeof(float), typeof(DamageSource), typeof(string) })]
    internal static class Patch_RemoteHitKeep3
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(float damage) { try { RemoteHitDamage.Keep(damage); } catch { } }
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.ApplyDamage), new[] { typeof(float), typeof(DamageSource), typeof(string) })]
    internal static class Patch_RemoteHitRestore3
    {
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(ref float damage) { try { RemoteHitDamage.Restore(ref damage); } catch { } }
    }
}
