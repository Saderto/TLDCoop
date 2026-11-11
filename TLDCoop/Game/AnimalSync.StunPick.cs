using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // 0.19.61. Оглушённого камнем кролика у копии нельзя было взять (друзья 11-10: друг оглушал кролика хоста
    // пять раз подряд, стоял в 1 м, а поднять не мог). Оглушение уходит ведущему (AnimalSync.Stone.cs), его кролик
    // лежит оглушённый ~4 с, но у копии «Взять» нет: ToReplica выключает у неё точки взаимодействия, а
    // WildlifeItemInteraction.CanInteract требует ещё и режим зверя «оглушён» (BaseAi.m_CurrentMode == Stunned), который
    // у копии не меняется (режим ведущего мод знает: Animal.CtrlMode).
    // Теперь: пока у ведущего зверь оглушён, у копии включаем WildlifeItemInteraction и ставим ей режим Stunned; очнулся —
    // выключаем обратно. Взял оглушённую копию (WildlifeItem.PickupWildlifeItem: зверёк в руки) — у всех этого зверя
    // убираем (у ведущего настоящий кролик исчезает), у себя копию забываем: она теперь вещь в руках.
    // Так же, когда берёт свой (настоящий) оглушённый зверь его ведущий: раньше у остальных он так и лежал оглушённым
    // на месте, и его мог взять второй игрок — два кролика.
    // Отпустил из рук (WildlifeItem.OnReleased: игра включает ИИ, модель, и зверь бежит от игрока) — зверь снова общий:
    // взявший ведёт его как нового (новый общий номер: старый у остальных 30 с считается убранным), остальные получают
    // копию из его состояний, а хозяин локации заберёт его, если взявший уйдёт. Убил в руках (WildlifeItem.OnKilled →
    // тушка в рюкзак, Despawn) — у остальных он уже убран, больше ничего не нужно.
    public static partial class AnimalSync
    {
        private static readonly Dictionary<long, List<Behaviour>> StunOn = new();
        // Зверь у нас в руках: адрес BaseAi → запись зверя (у остальных он убран) и локация.
        private static readonly Dictionary<IntPtr, (Animal a, string scene)> InHands = new();
        private static int _stunEnabled, _stunPicked, _stunOwnPicked, _stunReleased, _stunLogs;

        private static void StunTick()
        {
            if (InHands.Count > 0)
            {
                List<IntPtr> dead = null;
                foreach (var kv in InHands) if (kv.Value.a.Go == null || kv.Value.scene != _scene) (dead ??= new List<IntPtr>()).Add(kv.Key);
                if (dead != null) foreach (var p in dead) InHands.Remove(p);   // убит в руках (Despawn) или ушли с локации
            }
            if (Animals.Count == 0 && StunOn.Count == 0) return;
            foreach (var a in Animals.Values)
            {
                if (a.Real || a.Dead || a.Go == null || a.Ai == null) continue;
                bool stunned = a.CtrlMode == AiMode.Stunned;
                bool on = StunOn.ContainsKey(a.Gid);
                if (stunned && !on) StunOnCopy(a);
                else if (!stunned && on) StunOff(a, "очнулся у ведущего");
            }
            if (StunOn.Count > 0)
            {
                List<long> stale = null;
                foreach (var g in StunOn.Keys)
                    if (!Animals.TryGetValue(g, out var a) || a.Real || a.Dead) (stale ??= new List<long>()).Add(g);
                if (stale != null) foreach (var g in stale) StunOn.Remove(g);
            }
        }

        private static void StunOnCopy(Animal a)
        {
            try
            {
                var turned = new List<Behaviour>();
                foreach (var b in a.Off)
                {
                    if (b == null || b.enabled || b.TryCast<WildlifeItemInteraction>() == null) continue;
                    b.enabled = true;
                    turned.Add(b);
                }
                foreach (var wi in Root(a).GetComponentsInChildren<WildlifeItemInteraction>(true))
                    if (wi != null && wi.enabled && !wi.m_CanInteract) wi.m_CanInteract = true;
                a.Ai.m_CurrentMode = AiMode.Stunned;
                StunOn[a.Gid] = turned;
                _stunEnabled++;
                if (_stunLogs++ < 30)
                    MelonLogger.Msg($"[звери] {a.Prefab} оглушён у {NameOf(a.Controller)}: у копии можно взять ({InteractLook(a)})");
            }
            catch (Exception e) { if (_stunLogs++ < 30) MelonLogger.Warning("[звери] оглушённая копия: " + e.Message); }
        }

        private static void StunOff(Animal a, string why)
        {
            if (!StunOn.TryGetValue(a.Gid, out var turned)) return;
            StunOn.Remove(a.Gid);
            try
            {
                foreach (var b in turned) if (b != null) b.enabled = false;
                if (a.Ai != null && a.Ai.m_CurrentMode == AiMode.Stunned) a.Ai.m_CurrentMode = AiMode.None;
                if (_stunLogs++ < 30) MelonLogger.Msg($"[звери] {a.Prefab} {why}: у копии «Взять» убрал");
            }
            catch { }
        }

        // Prefix WildlifeItemInteraction.PerformInteraction: берут оглушённого зверя (копию или своего)? Тушку разбирает PickupStart.
        internal static long StunPickStart(WildlifeItemInteraction wi)
        {
            if (!Active || wi == null) return 0;
            try
            {
                var ai = wi.m_BaseAi;
                if (ai == null || !ByAi.TryGetValue(ai.Pointer, out var g) || !Animals.TryGetValue(g, out var a)) return 0;
                if (a.Dead || ai.m_CurrentMode != AiMode.Stunned) return 0;
                if (a.Real ? a.Controller != Session.LocalId : !StunOn.ContainsKey(g)) return 0;
                return g;
            }
            catch { return 0; }
        }

        internal static void StunPickDone(long gid, WildlifeItemInteraction wi)
        {
            if (gid == 0 || !Animals.TryGetValue(gid, out var a)) return;
            try
            {
                var item = wi != null ? wi.m_WildlifeItem : null;
                if (item == null || !item.m_PickedUp) return;   // игра не дала взять (нельзя сейчас) — ничего не меняем
                StunOn.Remove(gid);
                Forget(a);
                MarkGone(gid);
                SendRemove(new List<long> { gid });
                // Из списка зверей игры — вон: иначе хозяин локации снова записал бы его как нового (и разослал),
                // а не хозяин убрал бы как «лишнего». У копии это уже сделано.
                if (a.Real && a.Ai != null) Unlist(a.Ai);
                if (a.Ai != null) InHands[a.Ai.Pointer] = (a, _scene);
                if (a.Real) _stunOwnPicked++; else _stunPicked++;
                MelonLogger.Msg($"[звери] взял оглушённого {a.Prefab} ({(a.Real ? "вёл я" : "вёл " + NameOf(a.Controller))}) — у остальных его убираю");
            }
            catch (Exception e) { MelonLogger.Warning("[звери] взял оглушённого: " + e.Message); }
        }

        // Postfix WildlifeItem.OnReleased: отпустил из рук — зверь снова общий, ведём его мы.
        internal static void StunReleased(WildlifeItem wi)
        {
            if (wi == null || InHands.Count == 0) return;
            try
            {
                var ai = wi.m_BaseAi;
                if (ai == null || !InHands.TryGetValue(ai.Pointer, out var held)) return;
                InHands.Remove(ai.Pointer);
                var a = held.a;
                if (!Active || held.scene != _scene || GameApi.Scene() != _scene || a.Go == null)
                {
                    MelonLogger.Msg($"[звери] отпустил {a.Prefab}, но коопа на этой локации уже нет — бежит только у меня");
                    return;
                }
                float now = Time.unscaledTime;
                a.Gid = ((long)Session.LocalId << 32) | (uint)(++_counter);
                a.Controller = Session.LocalId;
                a.Dead = a.DeadHandled = a.Shadow = false;
                a.CtrlMode = AiMode.None;
                a.SnapCount = 0;
                a.Seen = now;
                a.WantTarget = -1;
                Animals[a.Gid] = a;
                MapAi(a, ai);
                var t = a.Go.transform;
                if (!a.Real) MakeReal(a, t.position, t.rotation, ai.m_CurrentHP, AiMode.Wander);
                else
                {
                    try { if (BaseAiManager.m_BaseAis != null && !BaseAiManager.m_BaseAis.Contains(ai)) BaseAiManager.Add(ai); } catch { }
                    a.RealSince = now;
                }
                // Игра уже велела ему бежать от игрока, но копия тогда ещё была без мозга (MakeReal перезапускает режим).
                var me = GameManager.GetPlayerTransform();
                if (a.Ai != null && me != null) { try { a.Ai.FleeFrom(me.position); } catch { } }
                _stunReleased++;
                MelonLogger.Msg($"[звери] отпустил {a.Prefab} из рук — снова общий, веду я (режим {(a.Ai != null ? a.Ai.m_CurrentMode.ToString() : "?")})");
            }
            catch (Exception e) { MelonLogger.Warning("[звери] отпустил из рук: " + e.Message); }
        }

        private static string StunStats => $"оглушённые: у копий можно было взять {_stunEnabled}, взяли копий {_stunPicked}, своих {_stunOwnPicked}, отпустили из рук {_stunReleased}";
    }

    [HarmonyPatch(typeof(WildlifeItemInteraction), nameof(WildlifeItemInteraction.PerformInteraction))]
    internal static class Patch_StunnedCopyPicked
    {
        private static void Prefix(WildlifeItemInteraction __instance, out long __state) => __state = AnimalSync.StunPickStart(__instance);
        private static void Postfix(WildlifeItemInteraction __instance, long __state) => AnimalSync.StunPickDone(__state, __instance);
    }

    [HarmonyPatch(typeof(WildlifeItem), nameof(WildlifeItem.OnReleased))]
    internal static class Patch_HeldAnimalReleased
    {
        private static void Postfix(WildlifeItem __instance) => AnimalSync.StunReleased(__instance);
    }
}
