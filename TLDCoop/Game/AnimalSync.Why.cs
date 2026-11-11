using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace TLDCoop.Game
{
    // Почему зверь побежал. Переданный другу зверь (бросок на него) у друга в первую же секунду уходит в бегство,
    // а игра причину нигде не пишет. Пишем сами: когда игра переводит нашего настоящего зверя в бегство —
    // какой её метод это решил (цепочка снаружи внутрь), причина бегства, режим до того и что игра думает о цели.
    public static partial class AnimalSync
    {
        private static readonly List<string> WhyStack = new();
        private static int _fleeLogs, _fleeLogsLeased;
        private static bool _ownMode;   // режим ставим мы сами (Restart) — это не решение игры

        private static readonly Dictionary<string, string> WhyNames = new()
        {
            ["SetAiMode"] = "смена режима",
            ["MaybeSetAiToFleeOrDespawn"] = "место появления выключено (пума)",
            ["ProcessAttack"] = "нападение",
            ["ProcessStalking"] = "выслеживание",
            ["ProcessWander"] = "бродит",
            ["ProcessHoldGround"] = "стойка",
            ["ScanForNewTarget"] = "ищет цель",
            ["ChangeModeWhenTargetDetected"] = "заметил цель",
            ["CantReachTarget"] = "не может дойти до цели",
            ["MaybeHoldGround"] = "страх (огонь/факел/фальшфейер/схватка)",
            ["HoldGroundFightOrFlight"] = "стойка: бой или бегство",
            ["HoldGroundCommon"] = "стойка: время вышло",
            ["MaybeFleeFromHoldGround"] = "бегство после стойки",
            ["MaybeHandleTimeoutFleeing"] = "бегство по времени",
            ["ProcessGunshotAudioEvent"] = "услышал выстрел",
            ["AttackOrFleeAfterNearMissGunshot"] = "выстрел мимо",
            ["MaybeFleeFromThrownItem"] = "брошенная вещь",
            ["MaybeFleeDueToTorchSwing"] = "взмах факелом",
            ["MaybeFleeChemicalHazard"] = "ядовитая зона",
            ["DoPostSpearStruggleBehavior"] = "после копья",
            ["MaybeApplyAttack"] = "удар",
            ["MaybeFleeAndDisbandOnMoraleCheck"] = "стая: боевой дух",
            ["MaybeFleeAndDisbandOnBearOrMooseCheck"] = "стая: рядом медведь или лось",
            ["MaybeForceMoveMembers"] = "стая: перестроение",
            ["MaybeDisbandGroupOnTargetLost"] = "стая: потеряла цель",
            ["RemoveGroup"] = "стая распалась",
            ["Update"] = "кадр зверя",
            ["ProcessCurrentAiMode"] = "текущий режим",
            ["ProcessCommonPre"] = "общие проверки",
            ["ProcessCommonPost"] = "общие проверки после",
            ["ProcessFlee"] = "бегство",
            ["ProcessGenericAudioEvent"] = "услышал звук",
            ["ProcessFootstepAudioEvent"] = "услышал шаги",
            ["ProcessCarAlarmAudioEvent"] = "услышал сигнализацию",
            ["WarnOthersNearby"] = "другой зверь предупредил",
            ["ApplyDamage"] = "урон",
            ["FleeFrom"] = "бежать от (вызов извне)",
            ["SetForcedFleePos"] = "бежать к точке (вызов извне)",
            ["Stun"] = "оглушён",
            ["UpdateWounds"] = "раны",
            ["MaybeRestoreTargetAfterSpear"] = "после копья: цель",
            ["EnterAttack"] = "начало нападения",
            ["ReturnToDefaultBehavior"] = "обычное поведение",
            ["BaseAiManager.Update"] = "менеджер зверей",
            ["PackManager.Update"] = "менеджер стай",
        };

        internal static void WhyEnter(string method)
        {
            if (WhyStack.Count > 40) WhyStack.Clear();   // страховка: где-то не вышли
            WhyStack.Add(WhyNames.TryGetValue(method, out var n) ? n : method);
        }

        internal static void WhyExit()
        {
            if (WhyStack.Count > 0) WhyStack.RemoveAt(WhyStack.Count - 1);
        }

        // BaseAi.SetAiMode, до смены режима (причина бегства к этому времени уже записана игрой).
        internal static void BeforeSetMode(BaseAi ai, AiMode mode)
        {
            if (mode != AiMode.Flee || !Active || ai == null || _ownMode) return;
            if (!ByAi.TryGetValue(ai.Pointer, out var g) || !Animals.TryGetValue(g, out var a) || !a.Real || a.Shadow || a.Dead) return;
            bool leased = !IsAuthority;   // мы не хозяин зверей локации, а зверь у нас настоящий — значит, нам его одолжили
            if (leased ? _fleeLogsLeased++ >= 150 : _fleeLogs++ >= 40) return;
            var parts = new List<string>();
            // Последний в цепочке — сама эта смена режима на Flee; решил тот, кто снаружи.
            string who = WhyStack.Count > 1 ? string.Join(" → ", WhyStack.GetRange(0, WhyStack.Count - 1)) : "?";
            try { parts.Add("причина " + ai.m_FleeReason); } catch { }
            try { parts.Add("был " + ai.m_CurrentMode); } catch { }
            parts.Add($"у меня {Time.unscaledTime - a.RealSince:0.0} с");
            try
            {
                var t = ai.m_CurrentTarget;
                if (t == null) parts.Add("цели нет");
                else
                {
                    string tn = IsLocalPlayerTarget(t) ? "я" : t.gameObject.name;
                    parts.Add($"цель {tn} в {Vector3.Distance(t.transform.position, ai.transform.position):0.0} м");
                    try { parts.Add("дойдёт " + (ai.CanPathfindToPosition(t.transform.position, MoveAgent.PathRequirement.FullPath) ? "да" : "нет")); } catch { }
                }
            }
            catch { }
            try { parts.Add("всегда бежать " + (ai.ShouldAlwaysFleeFromCurrentTarget() ? "да" : "нет")); } catch { }
            try { parts.Add("боится " + (ai.IsTooScaredToAttack() ? "да" : "нет")); } catch { }
            try { parts.Add($"мир зверя {ai.m_WildlifeMode}{(ai.IsMismatchWildlifeMode() ? " (не тот)" : "")}"); } catch { }
            try { parts.Add("стая " + (ai.m_PackAnimal == null ? "нет" : PackManager.InPack(ai.m_PackAnimal) ? "в стае" : "одиночка")); } catch { }
            try { parts.Add($"здоровье {ai.m_CurrentHP:0}/{ai.m_MaxHP:0}"); } catch { }
            try
            {
                var xp = GameManager.GetExperienceModeManagerComponent();
                if (xp != null && xp.GetWildlifeNotAttackUnprovoked()) parts.Add("режим: звери не нападают первыми");
                var pm = GameManager.GetPlayerManagerComponent();
                if (pm != null && pm.IsPumpkinPieBuffActive()) parts.Add("тыквенный пирог");
                if (pm != null && pm.m_ForceAIFlee) parts.Add("игра велит всем бежать");
            }
            catch { }
            if (Borrowing) parts.Add("подмена на " + NameOf(BorrowId));
            parts.Add(BodyState(a));
            MelonLogger.Msg($"[звери] {a.Prefab} побежал{(leased ? " (одолжен мне)" : "")}: решил {who}; " + string.Join(", ", parts));
            // Никто из известных методов игры: возможно, нас вызвал наш же код — тогда он виден в стеке.
            if (who == "?") MelonLogger.Msg("[звери] стек: " + ManagedStack());
        }

        private static string ManagedStack()
        {
            try
            {
                var frames = new System.Diagnostics.StackTrace(2, false).GetFrames();
                var names = new List<string>();
                foreach (var f in frames)
                {
                    var m = f.GetMethod();
                    if (m == null) continue;
                    string n = (m.DeclaringType != null ? m.DeclaringType.Name + "." : "") + m.Name;
                    if (n.StartsWith("Patch_FleeWhy") || n.Contains("DMD<") || n.StartsWith("RuntimeMethodHandle")) continue;
                    names.Add(n);
                    if (names.Count >= 12) break;
                }
                return names.Count > 0 ? string.Join(" ← ", names) : "(только код игры)";
            }
            catch (Exception e) { return e.Message; }
        }

        // Живо ли тело зверя у нас: включён ли ИИ, навигация, стоит ли на навигационной сетке.
        private static string BodyState(Animal a)
        {
            var parts = new List<string>();
            try
            {
                if (a.Ai != null) parts.Add("ИИ " + (a.Ai.enabled && a.Ai.gameObject.activeInHierarchy ? "вкл" : "ВЫКЛ"));
                var agent = a.Go != null ? a.Go.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() : null;
                if (agent == null) parts.Add("навигации нет");
                else parts.Add("навигация " + (!agent.enabled ? "ВЫКЛ" : agent.isOnNavMesh ? "на сетке" : "НЕ НА СЕТКЕ"));
                var move = a.Ai != null ? a.Ai.m_MoveAgent : null;
                parts.Add("движение " + (move == null ? "нет" : move.enabled ? "вкл" : "ВЫКЛ"));
            }
            catch (Exception e) { parts.Add("тело: " + e.Message); }
            return string.Join(", ", parts);
        }

        // Ожившая копия: навигация не встала на сетку (копию двигали мы) — ставим на ближайшую точку сетки.
        private static void SnapToNavMesh(Animal a)
        {
            try
            {
                var agent = a.Go != null ? a.Go.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() : null;
                if (agent == null || !agent.enabled || agent.isOnNavMesh) return;
                if (UnityEngine.AI.NavMesh.SamplePosition(agent.transform.position, out var hit, 4f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    bool ok = agent.Warp(hit.position);
                    MelonLogger.Msg($"[звери] {a.Prefab}: навигация была не на сетке, поставил на ближайшую точку ({Vector3.Distance(hit.position, agent.transform.position):0.0} м): {(ok ? "да" : "нет")}");
                }
                else MelonLogger.Msg($"[звери] {a.Prefab}: навигация не на сетке, сетки рядом нет");
            }
            catch (Exception e) { MelonLogger.Warning($"[звери] {a.Prefab}: сетка: {e.Message}"); }
        }
    }

    [HarmonyPatch]
    internal static class Patch_FleeWhy
    {
        private static readonly (Type type, string name)[] Spots =
        {
            (typeof(BaseAi), nameof(BaseAi.ProcessAttack)),
            (typeof(BaseAi), nameof(BaseAi.ProcessStalking)),
            (typeof(BaseAi), nameof(BaseAi.ProcessWander)),
            (typeof(BaseAi), "ProcessHoldGround"),
            (typeof(BaseAi), nameof(BaseAi.ScanForNewTarget)),
            (typeof(BaseAi), nameof(BaseAi.ChangeModeWhenTargetDetected)),
            (typeof(BaseAi), nameof(BaseAi.CantReachTarget)),
            (typeof(BaseAi), nameof(BaseAi.MaybeHoldGround)),
            (typeof(BaseAi), nameof(BaseAi.HoldGroundFightOrFlight)),
            (typeof(BaseAi), nameof(BaseAi.HoldGroundCommon)),
            (typeof(BaseAi), nameof(BaseAi.MaybeFleeFromHoldGround)),
            (typeof(BaseAi), nameof(BaseAi.MaybeHandleTimeoutFleeing)),
            (typeof(BaseAi), nameof(BaseAi.ProcessGunshotAudioEvent)),
            (typeof(BaseAi), nameof(BaseAi.AttackOrFleeAfterNearMissGunshot)),
            (typeof(BaseAi), "MaybeFleeFromThrownItem"),
            (typeof(BaseAi), nameof(BaseAi.MaybeFleeDueToTorchSwing)),
            (typeof(BaseAi), nameof(BaseAi.MaybeFleeChemicalHazard)),
            (typeof(BaseAi), nameof(BaseAi.DoPostSpearStruggleBehavior)),
            (typeof(BaseAi), nameof(BaseAi.MaybeApplyAttack)),
            (typeof(PackManager), "MaybeFleeAndDisbandOnMoraleCheck"),
            (typeof(PackManager), "MaybeFleeAndDisbandOnBearOrMooseCheck"),
            (typeof(PackManager), "MaybeForceMoveMembers"),
            (typeof(PackManager), "MaybeDisbandGroupOnTargetLost"),
            (typeof(PackManager), "RemoveGroup"),
            (typeof(BaseAi), "Update"),
            (typeof(BaseAi), "ProcessCurrentAiMode"),
            (typeof(BaseAi), "ProcessCommonPre"),
            (typeof(BaseAi), "ProcessCommonPost"),
            (typeof(BaseAi), "ProcessFlee"),
            (typeof(BaseAi), "ProcessGenericAudioEvent"),
            (typeof(BaseAi), "ProcessFootstepAudioEvent"),
            (typeof(BaseAi), "ProcessCarAlarmAudioEvent"),
            (typeof(BaseAi), "WarnOthersNearby"),
            (typeof(BaseAi), "ApplyDamage"),
            (typeof(BaseAi), "FleeFrom"),
            (typeof(BaseAi), "SetForcedFleePos"),
            (typeof(BaseAi), "Stun"),
            (typeof(BaseAi), "UpdateWounds"),
            (typeof(BaseAi), "MaybeRestoreTargetAfterSpear"),
            (typeof(BaseAi), "EnterAttack"),
            (typeof(BaseAi), "ReturnToDefaultBehavior"),
            (typeof(BaseAiManager), "Update"),
            (typeof(PackManager), "Update"),
        };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var (type, name) in Spots)
            {
                var found = new List<MethodBase>();
                try
                {
                    foreach (var m in AccessTools.GetDeclaredMethods(type))
                        if (m.Name == name && !m.IsAbstract && !m.ContainsGenericParameters) found.Add(m);
                }
                catch { }
                if (found.Count == 0) MelonLogger.Warning($"[звери] почему бежит: не нашёл {type.Name}.{name}");
                foreach (var m in found) yield return m;
            }
        }

        private static void Prefix(MethodBase __originalMethod)
        {
            var t = __originalMethod.DeclaringType;
            string n = __originalMethod.Name;
            AnimalSync.WhyEnter(t != null && t != typeof(BaseAi) && n == "Update" ? t.Name + "." + n : n);
        }
        private static Exception Finalizer(Exception __exception) { AnimalSync.WhyExit(); return __exception; }
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.SetAiMode))]
    internal static class Patch_FleeWhySetMode
    {
        private static bool Prefix(BaseAi __instance, AiMode mode)
        {
            AnimalSync.WhyEnter("SetAiMode");
            if (mode == AiMode.Flee && AnimalSync.RegionCheckBlocks(__instance, "прогнать его")) return false;
            try { AnimalSync.BeforeSetMode(__instance, mode); } catch { }
            return true;
        }
        private static Exception Finalizer(Exception __exception) { AnimalSync.WhyExit(); return __exception; }
    }
}
