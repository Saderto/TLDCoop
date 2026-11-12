using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // «Пауза» на котелке или еде на огне только у одного игрока (0.19.52, друзья 11-10: у одного «Пауза», у второго
    // готовится; у одного мясо готово, у второго сырое; ждать до готовности мог только один).
    // Игра считает огонь под котелком горящим, только если котелок привязан к огню (m_FireBeingUsed) и этот огонь у
    // меня на стадии «горит» (CookingPotItem.AttachedFireIsBurning: FullBurn). Иначе идёт отсчёт «без огня»
    // (m_GracePeriodElapsedHours), над котелком «Пауза», еда не готовится и окно готовки не открывается.
    // У копии котелка (его ведёт другой игрок) одно из двух могло не совпасть с ведущим. Теперь:
    //  • котелок на месте у огня, а привязан не к огню этого места (GearPlacePoint.m_FireToAttach) или ни к какому —
    //    привязываем, как сделала бы игра (раз в секунду, в логе «привязал»);
    //  • ведущий котелка раз в 3 с сообщает (Game/PotAmounts.cs), горит ли огонь под котелком у него. Если у него
    //    горит, у меня копия тоже считает огонь горящим: готовится вместе с ним, без «Паузы» (в логе «у ведущего горит»);
    //  • огонь под котелком с едой или водой 5 с «не горит» — в лог, почему (нет огня, стадия огня, что у ведущего).
    // Если огонь погас у ведущего, «Пауза» будет у всех — так и должно быть.
    internal static class PotFire
    {
        private const float LeadFresh = 8f;
        private static readonly Dictionary<IntPtr, (bool Burning, float At)> Lead = new();
        private static readonly Dictionary<IntPtr, float> OffSince = new();
        private static readonly HashSet<IntPtr> ForcedLogged = new();
        private static int _logs, _relinked, _forced;
        private static float _nextClean;

        internal static int LeadCount => Lead.Count;

        // Копия: ведущий прислал, горит ли у него огонь под этим котелком.
        internal static void NoteLead(CookingPotItem pot, bool burning)
        {
            if (pot == null) return;
            float now = Time.unscaledTime;
            Lead[pot.Pointer] = (burning, now);
            if (now >= _nextClean)
            {
                _nextClean = now + 30f;
                var old = new List<IntPtr>();
                foreach (var kv in Lead) if (now - kv.Value.At > 60f) old.Add(kv.Key);
                foreach (var k in old) Lead.Remove(k);
            }
        }

        // Котелок теперь веду я: чужое «горит» к нему больше не относится.
        internal static void IAmLead(CookingPotItem pot)
        {
            if (pot != null) Lead.Remove(pot.Pointer);
        }

        internal static bool LeadSaysBurning(CookingPotItem pot) =>
            pot != null && Lead.TryGetValue(pot.Pointer, out var v) && v.Burning && Time.unscaledTime - v.At < LeadFresh;

        // Горит ли огонь под котелком по самой игре, без подсказки ведущего.
        internal static bool OwnBurning(CookingPotItem pot)
        {
            var f = pot.m_FireBeingUsed;
            return f != null && f.m_FireState == FireState.FullBurn;
        }

        // Патч AttachedFireIsBurning: у меня огонь «не горит», у ведущего горит.
        internal static void NoteForced(CookingPotItem pot)
        {
            _forced++;
            if (ForcedLogged.Count > 200) ForcedLogged.Clear();
            if (!ForcedLogged.Add(pot.Pointer) || _logs >= 60) return;
            _logs++;
            try
            {
                var gi = pot.GetComponent<GearItem>();
                var f = pot.m_FireBeingUsed;
                MelonLogger.Msg($"[котелок] {(gi != null ? ItemSync.PrefabName(gi) : "?")}{Food(pot)}: у меня огонь под ним {(f == null ? "не привязан" : $"{WorldObjects.IdOf(f)} на стадии {f.m_FireState}")}, у ведущего горит — считаю горящим, «Паузы» нет");
            }
            catch { }
        }

        // Раз в секунду для каждого котелка в мире (Game/WorldExtra.cs, TickPots).
        // 0.19.57: «Пауза» над котелком (так её показывает игра: стадия Cooking, готово > 0, отсчёт без огня > 0) дольше 5 с —
        // в лог всё, от чего она зависит (друзья 11-10: «Пауза» у обоих на банке с водой, не проходит, а строк «не горит» нет).
        private static readonly Dictionary<IntPtr, float> PausedSince = new();

        private static void TickPause(CookingPotItem pot, GearItem gi, float now)
        {
            bool paused = pot.m_CookingState == CookingPotItem.CookingState.Cooking && pot.m_PercentCooked > 0f && pot.m_GracePeriodElapsedHours > 0f;
            if (!paused) { PausedSince.Remove(pot.Pointer); return; }
            if (!PausedSince.TryGetValue(pot.Pointer, out float since)) { PausedSince[pot.Pointer] = now; return; }
            if (since < 0f || now - since < 5f || _pauseLogs >= 40) return;
            PausedSince[pot.Pointer] = -1f;
            _pauseLogs++;
            var f = pot.m_FireBeingUsed;
            var gpp = pot.m_GearPlacePointAttachedTo;
            int owner = ItemSync.OwnerOf(gi);
            bool gameSays = false;
            try { gameSays = pot.AttachedFireIsBurning(); } catch { }
            MelonLogger.Msg($"[котелок] {ItemSync.PrefabName(gi)}{Food(pot)}: «Пауза» 5 с — огонь {(f == null ? "не привязан" : $"{WorldObjects.IdOf(f)} на стадии {f.m_FireState}")}, " +
                $"игра считает {(gameSays ? "горящим" : "не горящим")}, место у огня {(gpp == null ? "нет" : "есть")}, ставится {pot.m_IsBeingPlaced}, " +
                $"отсчёт без огня {pot.m_GracePeriodElapsedHours * 60f:0.#} мин, готово {pot.m_PercentCooked:0}%, снег {pot.m_LitersSnowBeingMelted.m_Units}, вода {pot.m_LitersWaterBeingBoiled.m_Units}, " +
                $"ведущий {(owner == Net.Session.LocalId ? "я" : owner < 0 ? "неизвестен" : "игрок " + owner)}{(LeadSaysBurning(pot) ? ", у ведущего горит" : "")}");
        }
        private static int _pauseLogs;

        internal static void Tick(CookingPotItem pot, GearItem gi, float now)
        {
            try { TickPause(pot, gi, now); } catch { }
            bool busy = pot.m_GearItemBeingCooked != null || pot.m_LitersSnowBeingMelted.m_Units > 0 || pot.m_LitersWaterBeingBoiled.m_Units > 0;
            var gpp = pot.m_GearPlacePointAttachedTo;
            // 0.19.57: и без места у огня (раньше такой котелок молча пропускали, и «Пауза» на нём не попадала в лог).
            if (!busy || (gpp == null && pot.m_FireBeingUsed == null && pot.m_GracePeriodElapsedHours <= 0f) || OwnBurning(pot)) { OffSince.Remove(pot.Pointer); return; }

            // Стоит на месте у огня, а привязан не к его огню.
            var want = gpp != null ? gpp.m_FireToAttach : null;
            var cur = pot.m_FireBeingUsed;
            if (want != null && (cur == null || cur.Pointer != want.Pointer))
            {
                string was = cur == null ? "ни к какому огню" : "к другому огню " + WorldObjects.IdOf(cur);
                if (cur != null) pot.DetachFromFire();
                pot.AttachToFire(want, gpp);
                _relinked++;
                if (_logs++ < 60)
                    MelonLogger.Msg($"[котелок] {ItemSync.PrefabName(gi)}{Food(pot)}: стоит на месте у огня {WorldObjects.IdOf(want)} (стадия {want.m_FireState}), а был привязан {was} — привязал");
                if (OwnBurning(pot)) { OffSince.Remove(pot.Pointer); return; }
            }

            if (!OffSince.TryGetValue(pot.Pointer, out float since)) { OffSince[pot.Pointer] = now; return; }
            if (since < 0f || now - since < 5f || _logs >= 60) return;
            OffSince[pot.Pointer] = -1f;   // один раз, пока снова не загорится
            _logs++;
            var f = pot.m_FireBeingUsed;
            string why = f == null ? (gpp == null ? "котелок не на месте у огня и ни к какому огню не привязан" : want == null ? "котелок ни к какому огню не привязан, у места у огня огня тоже нет" : "котелок ни к какому огню не привязан")
                : $"огонь {WorldObjects.IdOf(f)} на стадии {f.m_FireState}, осталось {(f.m_MaxOnTODSeconds - f.m_ElapsedOnTODSeconds) / 60f:0} мин";
            int owner = ItemSync.OwnerOf(gi);
            string lead = owner == Net.Session.LocalId ? "веду его я"
                : LeadSaysBurning(pot) ? "у ведущего горит — у меня считается горящим"
                : Lead.ContainsKey(pot.Pointer) ? "у ведущего тоже не горит" : "ведущий про него не сообщал";
            MelonLogger.Msg($"[котелок] {ItemSync.PrefabName(gi)}{Food(pot)}: огонь под ним 5 с не горит ({why}); {lead}; отсчёт без огня {pot.m_GracePeriodElapsedHours * 60f:0} мин, готово {pot.m_PercentCooked:0}%");
        }

        internal static string Stats => $"огонь под котелками: перепривязал {_relinked}, по ведущему считал горящим {_forced} раз";

        private static string Food(CookingPotItem pot)
        {
            try { var c = pot.m_GearItemBeingCooked; return c != null ? " (" + ItemSync.PrefabName(c) + ")" : ""; } catch { return ""; }
        }
    }

    [HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.AttachedFireIsBurning))]
    internal static class Patch_PotFireByLead
    {
        private static void Postfix(CookingPotItem __instance, ref bool __result)
        {
            if (__result || PotFire.LeadCount == 0) return;
            try
            {
                // Только при огне у котелка: игра после «горит» может обращаться к самому огню.
                if (__instance.m_FireBeingUsed != null && PotFire.LeadSaysBurning(__instance)) { __result = true; PotFire.NoteForced(__instance); }
            }
            catch { }
        }
    }
}
