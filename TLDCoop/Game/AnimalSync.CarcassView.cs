using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Interactions;
using MelonLoader;
using UnityEngine;

namespace TLDCoop.Game
{
    // Туши: смерть копии сразу, туша не прячется, и в логе — почему туша пропала.
    //
    // 1) Зверь, убитый другим: у нас его копия становится настоящей и погибает (OnDead). Из стойки на месте
    //    («Wolf_Default_Idle», -220268959) игра в смерть не переходит, пока клип стойки (~5 с) не кончится — волк
    //    стоял 3–5 с и только потом падал. Сброс аниматора (Rebind, 0.19.9) этого не лечил: он ставит ту же стойку.
    //    Теперь сразу после смерти ставим позу смерти, которую играет убивший (DeathStart), и первые 10 с повторяем
    //    за ним (ShadowFollow): падает так же и ложится так же, как у него.
    //
    // 2) «Туша пропала перед лицом»: игра (BaseAiManager.Update) каждый кадр прячет модель зверя, если его капсула
    //    (m_CharacterController) вне кадра. У туши тело может лежать в метре-двух от капсулы — смотришь на тушу,
    //    капсулы не видно, модель выключена. Туши в коопе не прячем (Unity сама не рисует то, чего не видно), и раз в
    //    30 с на тушу пишем, когда игра пыталась это сделать и как далеко тело от капсулы.
    //
    // 3) Если туша всё же исчезнет: раз в секунду смотрим туши в 60 м от нас — сдвинулась ли, выключена ли модель;
    //    а в строке «игра убрала» — сколько прошло после смерти и после состояния туши от другого игрока.
    public static partial class AnimalSync
    {
        private static int _deathRebinds, _carcassKept, _carcassViewLogs, _carcassMoves, _carcassHidden, _carcassWatchLogs;

        // Позы смерти по видам: что играл убивший через 6 с и больше после смерти (из его состояний туши) — туша уже
        // лежит (стойка, если была, к этому времени кончилась).
        private static readonly Dictionary<string, List<int>> DeathStates = new();
        private static int _deathLearnLogs, _deathFollows;

        private static int CurState(Animator an)
        {
            try { return an.IsInTransition(0) ? an.GetNextAnimatorStateInfo(0).fullPathHash : an.GetCurrentAnimatorStateInfo(0).fullPathHash; }
            catch { return 0; }
        }

        private static string ClipOf(Animator an)
        {
            try { var ci = an.GetCurrentAnimatorClipInfo(0); if (ci != null && ci.Length > 0 && ci[0].clip != null) return ci[0].clip.name; } catch { }
            return "?";
        }

        private static void LearnDeathState(Animal a, int state)
        {
            if (state == 0 || a.Anim == null) return;
            if (!DeathStates.TryGetValue(a.Prefab, out var list)) DeathStates[a.Prefab] = list = new List<int>();
            if (list.Contains(state) || list.Count >= 12) return;
            list.Add(state);
            if (_deathLearnLogs++ < 20) MelonLogger.Msg($"[звери] поза смерти {a.Prefab}: {state} (у убившего)");
        }

        private static bool FadeTo(Animal a, int state, float t, float fade)
        {
            var an = a.Anim;
            if (an == null || state == 0) return false;
            if (a.NoState != null && a.NoState.Contains(state)) return false;
            if (!an.HasState(0, state)) { (a.NoState ??= new HashSet<int>()).Add(state); return false; }
            an.CrossFade(state, fade, 0, t);
            return true;
        }

        // Сразу после ApplyDamage у копии, погибшей у другого: в позу смерти. Если убивший в миг смерти играл уже не
        // то, что наша копия (у него начинается падение), — то же, что он; иначе — известная поза смерти этого вида
        // (лёжа); не знаем ни того, ни другого — ждём его состояний (ShadowFollow).
        private static void DeathStart(Animal a)
        {
            var an = a.Anim;
            if (an == null) return;
            try
            {
                int was = CurState(an);
                string clip = ClipOf(an);
                int to = 0;
                float t = 0f;
                if (a.State != 0 && a.State != was) { to = a.State; t = a.StateTime; }
                else if (DeathStates.TryGetValue(a.Prefab, out var l) && l.Count > 0) to = l[0];
                bool done = to != 0 && to != was && FadeTo(a, to, t, 0.1f);
                _deathRebinds++;
                if (_carcassWatchLogs++ < 30)
                    MelonLogger.Msg($"[звери] {a.Prefab} погибает у меня из копии: играл {was} «{clip}», "
                        + (done ? $"сразу ставлю позу смерти {to}" : "позы смерти пока не знаю — повторю за убившим"));
            }
            catch (Exception e) { if (_carcassWatchLogs++ < 30) MelonLogger.Warning("[звери] поза смерти: " + e.Message); }
        }

        // Состояние туши-тени от убившего, первые 10 с после смерти: повторяем его позу (и запоминаем позы смерти).
        private static void ShadowFollow(Animal a, int state, float stateTime, float now)
        {
            if (a.DeadAt < 0f || now - a.DeadAt > 10f || state == 0 || a.Anim == null) return;
            try
            {
                if (now - a.DeadAt > 6f) LearnDeathState(a, state);
                if (CurState(a.Anim) == state || now < a.NextFade) return;
                if (FadeTo(a, state, stateTime, 0.2f)) { _deathFollows++; a.NextFade = now + 0.5f; }
            }
            catch { }
        }

        // Prefix BaseAi.EnableSkinnedMeshRenderers(false): туше — не прятать.
        internal static bool KeepCarcassVisible(BaseAi ai)
        {
            if (!Active || ai == null || _despawning) return false;
            if (!ByAi.TryGetValue(ai.Pointer, out var g) || !Animals.TryGetValue(g, out var a) || !a.Dead || a.Go == null) return false;
            try { if (!a.Go.activeInHierarchy) return false; } catch { return false; }
            _carcassKept++;
            float now = Time.unscaledTime;
            if (now - a.ViewLogAt > 30f && _carcassViewLogs < 20 && NearCamera(a, 60f))
            {
                a.ViewLogAt = now;
                _carcassViewLogs++;
                MelonLogger.Msg($"[звери] игра хотела спрятать модель туши {a.Prefab}{(a.Shadow ? " (убита у другого)" : a.Real ? "" : " (копия)")} — оставил; {BodyVsCapsule(a)}");
            }
            return true;
        }

        private static bool NearCamera(Animal a, float range)
        {
            try
            {
                var cam = GameManager.GetMainCamera();
                return cam != null && (a.Go.transform.position - cam.transform.position).sqrMagnitude < range * range;
            }
            catch { return false; }
        }

        // Копия туши — как настоящая туша нашей игры. ToReplica выключает у копии всё, кроме аниматора, BodyHarvest
        // и звука, — в том числе точки взаимодействия. У волка, оленя, медведя это разделка (BodyHarvestInteraction:
        // без неё точка есть, а «Разделать» нет), у кролика и куропатки — «Взять» (WildlifeItemInteraction: тушку
        // поднимают в руки, а разделывают уже вещь). 0.19.10 включал только разделку, и тушу кролика друг не мог
        // ни взять, ни разделать. Теперь включаем обратно все точки взаимодействия, что выключил сам мод, — какие бы
        // они ни были у зверя. И как игра при смерти (BaseAi.EnterDead): объект зверя — на слой туш (20, у медведя 19
        // вместе с капсулами попаданий), капсула передвижения выключена (иначе на месте стоявшего зверя невидимая стенка).
        private static int _carcassInteractions, _carcassInteractLogs;
        private const int CarcassLayer = 20, BearCarcassLayer = 19;

        private static void CarcassInteraction(Animal a, string why)
        {
            if (a.Go == null) return;
            try
            {
                var turnedOn = new List<string>();
                foreach (var b in a.Off)
                {
                    if (b == null || b.enabled || b.TryCast<BaseInteraction>() == null) continue;
                    b.enabled = true;
                    turnedOn.Add(b.GetIl2CppType().Name);
                }
                int can = 0;
                foreach (var bi in Root(a).GetComponentsInChildren<BaseInteraction>(true))
                {
                    if (bi == null || !bi.enabled || bi.m_CanInteract) continue;
                    if (bi.TryCast<BodyHarvestInteraction>() == null && bi.TryCast<WildlifeItemInteraction>() == null) continue;
                    bi.m_CanInteract = true;
                    can++;
                }
                // Кролик, оглушённый камнем, у ведущего умер, а у копии режим тот же, что был: «Взять» с режимом
                // «оглушён» (Stunned) берёт живого зверька в руки, а не тушку.
                if (a.Ai != null && a.Ai.m_CurrentMode == AiMode.Stunned) a.Ai.m_CurrentMode = AiMode.None;
                bool bear = a.Prefab.Contains("Bear");
                int layer = bear ? BearCarcassLayer : CarcassLayer, wasLayer = a.Go.layer;
                if (wasLayer != layer)
                {
                    a.Go.layer = layer;
                    if (bear && a.Ai != null) try { a.Ai.ChangeCollisionCapsulesLayer(layer); } catch { }
                }
                bool capOff = false;
                var cc = a.Ai != null ? a.Ai.m_CharacterController : null;
                if (cc != null && cc.enabled) { cc.enabled = false; capOff = true; }
                if (turnedOn.Count == 0 && can == 0 && wasLayer == layer && !capOff) return;
                _carcassInteractions++;
                if (_carcassInteractLogs++ < 30)
                    MelonLogger.Msg($"[звери] копия туши {a.Prefab} ({why}): включил {(turnedOn.Count > 0 ? string.Join(", ", turnedOn) : "—")}"
                        + $"{(can > 0 ? $", разрешил {can}" : "")}; слой {wasLayer}{(wasLayer != layer ? " → " + layer : "")}{(capOff ? ", капсулу выключил" : "")}");
            }
            catch (Exception e) { if (_carcassWatchLogs++ < 30) MelonLogger.Warning("[звери] взаимодействие копии туши: " + e.Message); }
        }

        // Для лога смерти: что у туши с точками взаимодействия, слоем и капсулой (сравнить свою тушу и копию).
        private static string InteractLook(Animal a)
        {
            try
            {
                if (a.Go == null) return "";
                var parts = new List<string>();
                foreach (var bi in Root(a).GetComponentsInChildren<BaseInteraction>(true))
                    if (bi != null) parts.Add($"{bi.GetIl2CppType().Name} {(bi.enabled ? "вкл" : "выкл")}{(bi.m_CanInteract ? "" : " (нельзя)")}");
                var cc = a.Ai != null ? a.Ai.m_CharacterController : null;
                return $"слой {a.Go.layer}, капсула {(cc == null ? "нет" : cc.enabled ? "вкл" : "выкл")}, взаимодействие: {(parts.Count > 0 ? string.Join(", ", parts) : "нет")}";
            }
            catch (Exception e) { return e.Message; }
        }

        // ---------- Тушку подняли («Взять»: кролик, куропатка) ----------
        // Игра делает из тушки вещь (WildlifeItem.SpawnCarcass → GEAR_RabbitCarcass), даёт её игроку (GearItem.PerformInteraction:
        // сразу в рюкзак или экран осмотра «Взять / Оставить») и убирает зверя (BaseAi.Despawn) — при любом выборе.
        // Убрала у себя копию — остальные об этом не знали: у ведущего тушка лежала бы дальше, её можно было взять второй раз.
        // Теперь, когда выбор сделан, взявший просит всех убрать этого зверя (как «ведущий убрал»). Если на экране осмотра
        // он выбрал «Оставить», тушка-вещь осталась лежать у него на месте зверя — её рассылаем, как выброшенную вещь
        // (ItemSync.OnLocalDrop), и у всех на земле одно и то же. Выбор ждём, пока вещь на осмотре (до 2 минут).
        private static int _pickedUp, _pickupLeft;
        private static bool _inPickup;
        private static GearItem _spawnedNow, _pickupItem;
        private static long _pickupGid;
        private static float _pickupAt;
        private static string _pickupPrefab = "";

        internal static long PickupStart(WildlifeItemInteraction wi)
        {
            _inPickup = false;
            _spawnedNow = null;
            if (!Active || wi == null) return 0;
            try
            {
                var ai = wi.m_BaseAi;
                if (ai == null || !ByAi.TryGetValue(ai.Pointer, out var g) || !Animals.TryGetValue(g, out var a) || !a.Dead) return 0;
                _inPickup = true;
                return g;
            }
            catch { return 0; }
        }

        // Postfix WildlifeItem.SpawnCarcass: вещь, в которую превратилась тушка.
        internal static void OnCarcassSpawned(GearItem gi)
        {
            if (_inPickup && gi != null) _spawnedNow = gi;
        }

        internal static void PickupDone(long gid)
        {
            var gi = _spawnedNow;
            _inPickup = false;
            _spawnedNow = null;
            if (gid == 0 || gi == null) return;   // вещи не вышло — игра зверя и не убирала
            try
            {
                if (_pickupGid != 0) ResolvePickup(true);   // прежняя ещё ждала выбора
                Animals.TryGetValue(gid, out var a);
                MarkGone(gid);
                _pickupPrefab = a?.Prefab ?? "?";
                if (a != null)
                {
                    MelonLogger.Msg($"[звери] беру тушку {a.Prefab}{(a.Real ? (a.Shadow ? " (убита у другого)" : "") : " (копия, ведёт " + NameOf(a.Controller) + ")")}");
                    if (a.Real) SendCarcassGone(a);
                    Forget(a);
                    if (!a.Real && a.Go != null && a.Go.activeInHierarchy) DestroyReplica(a);   // игра копию не убрала
                }
                // Остальным — когда станет ясно, взял или оставил (из HarvestTick, не раньше следующей секунды:
                // экран осмотра может открыться не в этом кадре).
                _pickupGid = gid;
                _pickupItem = gi;
                _pickupAt = Time.unscaledTime;
            }
            catch (Exception e) { MelonLogger.Warning("[звери] взял тушку: " + e.Message); }
        }

        // Из HarvestTick (раз в секунду). force — не ждать конца осмотра.
        private static void ResolvePickup(bool force)
        {
            if (_pickupGid == 0) return;
            float now = Time.unscaledTime;
            var gi = _pickupItem;
            if (!force)
            {
                if (now - _pickupAt < 0.5f) return;
                bool inspecting = false;
                try
                {
                    var pm = GameManager.GetPlayerManagerComponent();
                    var seen = pm != null && pm.IsInspectModeActive() ? pm.GearItemBeingInspected() : null;
                    inspecting = seen != null && gi != null && seen.Pointer == gi.Pointer;
                }
                catch { }
                if (inspecting && now - _pickupAt < 120f) return;
            }
            long gid = _pickupGid;
            _pickupGid = 0;
            _pickupItem = null;
            bool left = false;
            try { left = gi != null && !gi.m_InPlayerInventory && !gi.m_InsideContainer && gi.gameObject.activeInHierarchy; } catch { }
            SendRemove(new List<long> { gid });
            MarkGone(gid);   // ведущий узнает только сейчас: его состояния ещё придут
            if (left)
            {
                _pickupLeft++;
                // Мод помнит вещь с экрана осмотра (ItemSync.Inspecting) и после «Оставить»: раскладка локации её
                // пропускала как «в руках на осмотре», и при выравнивании под хоста друг получил вторую такую же
                // тушку (у хоста она была от нашей рассылки) — поднял обе.
                try { if (ItemSync.Inspecting != null && ItemSync.Inspecting.Pointer == gi.Pointer) { ItemSync.Inspecting = null; _inspectCleared++; } } catch { }
                bool sent = false;
                try { sent = ItemSync.OnLocalDrop(gi); } catch (Exception e) { MelonLogger.Warning("[звери] оставленная тушка: " + e.Message); }
                MelonLogger.Msg($"[звери] тушку {_pickupPrefab} оставил на земле: зверя убираю у всех, тушку-вещь {(sent ? "разослал" : "разослать не вышло")}");
            }
            else
            {
                _pickedUp++;
                MelonLogger.Msg($"[звери] взял тушку {_pickupPrefab} — убираю у всех");
            }
        }

        // Где тело туши относительно капсулы игры и нас.
        private static string BodyVsCapsule(Animal a)
        {
            try
            {
                var cam = GameManager.GetMainCamera();
                Vector3 me = cam != null ? cam.transform.position : Vector3.zero;
                Vector3 body = a.Go.transform.position;
                a.Skins ??= a.Go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var s in a.Skins) if (s != null) { body = s.bounds.center; break; }
                string cap = "капсулы нет";
                var cc = a.Ai != null ? a.Ai.m_CharacterController : null;
                if (cc != null) cap = $"тело от капсулы {Vector3.Distance(body, cc.bounds.center):0.0} м (капсула {(cc.enabled ? "вкл" : "выкл")})";
                return $"от меня {Vector3.Distance(body, me):0} м, {cap}";
            }
            catch (Exception e) { a.Skins = null; return e.Message; }
        }

        // Из HarvestTick, раз в секунду: туши рядом с нами.
        private static void WatchCarcasses()
        {
            var pt = GameManager.GetPlayerTransform();
            if (pt == null) return;
            var me = pt.position;
            foreach (var a in Animals.Values)
            {
                if (!a.Dead || a.Go == null) continue;
                Vector3 p;
                try { if (!a.Go.activeInHierarchy) continue; p = a.Go.transform.position; } catch { continue; }
                bool near = (p - me).sqrMagnitude < 60f * 60f;
                if (a.HasDeadPos && near)
                {
                    float moved = Vector3.Distance(p, a.DeadPos);
                    if (moved > 1.5f)
                    {
                        _carcassMoves++;
                        if (_carcassWatchLogs++ < 30) MelonLogger.Msg($"[звери] туша {a.Prefab}{(a.Shadow ? " (убита у другого)" : a.Real ? "" : " (копия)")} сдвинулась на {moved:0.0} м (от меня {Vector3.Distance(p, me):0} м)");
                    }
                }
                a.DeadPos = p;
                a.HasDeadPos = true;
                if (!near) continue;
                if (a.Real) ColliderSync(a, Time.unscaledTime);
                if ((p - me).sqrMagnitude < 6f * 6f) LookAtCopy(a, Vector3.Distance(p, me));
                // Модель выключена, хотя туша на месте (игра прячет иначе, чем через EnableSkinnedMeshRenderers).
                try
                {
                    a.Skins ??= a.Go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    int all = 0, on = 0;
                    foreach (var s in a.Skins) { if (s == null) continue; all++; if (s.enabled && s.gameObject.activeInHierarchy) on++; }
                    if (all > 0 && on == 0)
                    {
                        _carcassHidden++;
                        float now = Time.unscaledTime;
                        if (now - a.ViewLogAt > 30f && _carcassWatchLogs++ < 30)
                        {
                            a.ViewLogAt = now;
                            MelonLogger.Msg($"[звери] у туши {a.Prefab}{(a.Shadow ? " (убита у другого)" : a.Real ? "" : " (копия)")} выключены все части модели ({all}); {BodyVsCapsule(a)}");
                        }
                    }
                }
                catch { a.Skins = null; }
            }
        }

        // Мы рядом с тушей (до 6 м; своей, убитой у другого или копией): раз в 20 с на тушу пишем, во что попадает
        // взгляд (как у игры: луч из камеры по слоям, где игра ищет, с чем взаимодействовать), можно ли
        // взаимодействовать, видна ли модель и что с коллайдерами. По этому видно, почему друг тушу не видит или не
        // может её взять/разделать, и чем туша, убитая у другого, отличается от своей.
        private static readonly Dictionary<long, float> LookLogAt = new();
        private static int _lookLogs;
        private const int InteractMask = 0x83F9040;   // PlayerManager.GetInteractiveObjectUnderCrosshairs

        private static void LookAtCopy(Animal a, float dist)
        {
            float now = Time.unscaledTime;
            if (_lookLogs >= 24 || (LookLogAt.TryGetValue(a.Gid, out var at) && now - at < 20f)) return;
            LookLogAt[a.Gid] = now;
            _lookLogs++;
            try
            {
                var root = Root(a);
                string hit = "ни во что";
                try
                {
                    var cam = GameManager.GetMainCamera();
                    if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward, out var h, 6f, InteractMask, QueryTriggerInteraction.Collide))
                    {
                        var c = h.collider;
                        bool mine = c != null && c.transform.IsChildOf(root);
                        hit = c == null ? "?" : $"{c.gameObject.name} (слой {c.gameObject.layer}, {(mine ? "эта туша" : "не она")}, {h.distance:0.0} м)";
                    }
                }
                catch (Exception e) { hit = e.Message; }
                var acts = new List<string>();
                foreach (var bi in root.GetComponentsInChildren<BaseInteraction>(true))
                {
                    if (bi == null) continue;
                    bool can = false;
                    try { can = bi.CanInteract; } catch { }
                    acts.Add($"{bi.GetIl2CppType().Name} {(bi.enabled ? "вкл" : "выкл")}, можно {(can ? "да" : "нет")}");
                }
                int all = 0, on = 0, seen = 0;
                var skinLayers = new HashSet<int>();
                foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (r == null) continue;
                    all++;
                    if (r.enabled && r.gameObject.activeInHierarchy) on++;
                    if (r.isVisible) seen++;
                    skinLayers.Add(r.gameObject.layer);
                }
                int cAll = 0, cOn = 0;
                var colLayers = new HashSet<int>();
                foreach (var c in root.GetComponentsInChildren<Collider>(true))
                {
                    if (c == null) continue;
                    cAll++;
                    if (c.enabled && c.gameObject.activeInHierarchy) { cOn++; colLayers.Add(c.gameObject.layer); }
                }
                string kind = a.Real ? (a.Shadow ? "туша, убитая у другого," : "своя туша") : "копия туши";
                MelonLogger.Msg($"[звери] рядом {kind} {a.Prefab} ({dist:0.0} м, ведёт {NameOf(a.Controller)}): взгляд попал {hit}; "
                    + $"взаимодействие: {(acts.Count > 0 ? string.Join(", ", acts) : "нет")}; модель вкл {on} из {all}, видна камере {seen} (слои {string.Join("/", skinLayers)}); "
                    + $"коллайдеры вкл {cOn} из {cAll} (слои {string.Join("/", colLayers)}); слой зверя {a.Go.layer}, {BodyVsCapsule(a)}");
            }
            catch (Exception e) { MelonLogger.Warning("[звери] рядом копия туши: " + e.Message); }
        }

        // Для строки «игра убрала»: что было с тушей перед этим.
        private static string GoneWhy(Animal a, float now)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (a.DeadAt >= 0f) parts.Add($"погибла {now - a.DeadAt:0} с назад");
            if (a.HarvestAt > 0f) parts.Add($"состояние туши от другого {now - a.HarvestAt:0.0} с назад");
            try
            {
                if (a.Go != null)
                {
                    var bh = a.Go.GetComponent<BodyHarvest>();
                    if (bh != null) parts.Add($"состояние {bh.m_Condition:0.#}%, ресурсов {(bh.NoMoreResources() ? "нет" : "есть")}");
                }
                else parts.Add("объект уничтожен");
            }
            catch { parts.Add("объект уничтожен"); }
            return string.Join(", ", parts);
        }

        // После состояния туши от другого: игра (BodyHarvest.Deserialize → MaybeDestroyAfterDeserialize) уничтожает
        // тушу, у которой не осталось ресурсов или состояние 0 %. Пишем, если так вышло.
        private static void NoteHarvestApplied(Animal a, BodyHarvest bh)
        {
            try
            {
                if (bh == null || !(bh.NoMoreResources() || bh.ConditionReachedZero())) return;
                if (_carcassWatchLogs++ < 30) MelonLogger.Msg($"[звери] туша {a.Prefab}: пришло состояние, где ресурсов нет или состояние 0 % ({bh.m_Condition:0.#}%) — игра её уберёт");
            }
            catch { }
        }

        private static int _shadowDeaths, _shadowDeathsHere, _inspectCleared;

        // ---------- Капсулы туши, умершей у нас «по присланному» ----------
        // Взаимодействовать с тушей можно там, где её капсулы на костях (capsule_chest и т. п., слой 27). Их ставит на
        // кости MatchTransform, но через 5 с после смерти (BaseAi.m_TimeInDeadMode ≥ 5, MatchTransform.DoLateUpdate)
        // игра перестаёт их двигать: туша, считается, улеглась. Туша, убитая у другого, умирает у нас из позы копии:
        // у волка хоста аниматор ещё ~5 с стоял в стойке «Wolf_Default_Idle» (-220268959, лог друга 05:37:15–05:37:21)
        // и только потом падал, а туша не на виду костей не двигает вовсе (CullUpdateTransforms). Капсулы застывали в
        // позе стоя, и друг мог разделать волка только у ног и живота, а хост (у него туша своя) — по всему телу.
        // Теперь такой туше ставим капсулы на кости сами раз в секунду (рядом с нами): не меньше минуты после смерти
        // и пока туша не пробудет на виду 3 с (тогда кости уже в позе лёжа); дольше 10 минут — нет.
        private static int _colSyncs, _colSyncDone, _colSyncLogs;

        private static void StartColliderSync(Animal a)
        {
            a.ColSyncUntil = Time.unscaledTime + 60f;
            a.ColSeenAt = -1f;
            a.Matches = null;
        }

        private static void ColliderSync(Animal a, float now)
        {
            if (a.ColSyncUntil < 0f || a.Go == null) return;
            try
            {
                a.Matches ??= Root(a).GetComponentsInChildren<MatchTransform>(true);
                int moved = 0;
                foreach (var mt in a.Matches)
                {
                    if (mt == null) continue;
                    var to = mt.m_CachedTargetTransform;
                    if (to == null && mt.m_Target != null) to = mt.m_Target.transform;
                    var me = mt.m_CachedThisTransform ?? mt.transform;
                    if (to == null || me == null) continue;
                    if ((me.position - to.position).sqrMagnitude > 0.0004f) moved++;
                    me.SetPositionAndRotation(to.position, to.rotation);
                }
                _colSyncs++;
                if (moved > 0 && _colSyncLogs++ < 20)
                    MelonLogger.Msg($"[звери] туша {a.Prefab}{(a.Shadow ? " (убита у другого)" : "")}: капсулы отстали от костей ({moved} из {a.Matches.Length}, через {now - a.DeadAt:0} с после смерти) — поставил на кости");
                a.Skins ??= a.Go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                bool seen = false;
                foreach (var s in a.Skins) if (s != null && s.isVisible) { seen = true; break; }
                if (seen && a.ColSeenAt < 0f) a.ColSeenAt = now;
                if ((now > a.ColSyncUntil && a.ColSeenAt >= 0f && now - a.ColSeenAt > 3f) || (a.DeadAt >= 0f && now - a.DeadAt > 600f))
                {
                    a.ColSyncUntil = -1f;
                    _colSyncDone++;
                }
            }
            catch { a.Matches = null; a.Skins = null; }
        }

        // ---------- Убранные у нас звери ----------
        // Подняли тушку или ведущий убрал зверя: у нас его больше нет, но его ведущий ещё шлёт состояния — пока не
        // узнал об этом (мы шлём «убери» только после выбора «Взять / Оставить») или пока они в пути. По такому
        // состоянию ReceiveState создавал копию заново, а в 0.19.13 копия ещё и становится настоящей тушей: друг поднял
        // кролика хоста, кролик оказался в рюкзаке, а второй такой же лёг на прежнее место. Теперь 30 с после того,
        // как мы зверя убрали (и после того, как разослали «убери»), его состояния и передачи не создают его снова.
        private static readonly Dictionary<long, float> GoneHere = new();
        private static readonly HashSet<long> GoneLogged = new();   // о каком уже написали в лог
        private const float GoneHold = 30f;
        private static int _goneSkipped, _goneLogs;

        private static void MarkGone(long gid) { if (gid != 0) GoneHere[gid] = Time.unscaledTime; }

        private static bool IsGone(long gid, float now)
        {
            if (gid != 0 && gid == _pickupGid) return true;   // тушка ещё на экране осмотра: «убери» не отправлено
            if (GoneHere.Count == 0 || !GoneHere.TryGetValue(gid, out var at)) return false;
            if (now - at < GoneHold) return true;
            GoneHere.Remove(gid);
            GoneLogged.Remove(gid);
            return false;
        }

        private static void SkipGone(string prefab, string what)
        {
            _goneSkipped++;
            if (_goneLogs++ < 20) MelonLogger.Msg($"[звери] {prefab}: {what}, а я его только что убрал у себя — заново не создаю");
        }

        private static string CarcassViewStats => $"копий погибло своей смертью игры {_shadowDeaths} (не у хозяина {_shadowDeathsHere}), повторил позу убившего {_deathFollows}, знаю поз смерти {DeathStates.Count} видов, не создал заново убранных {_goneSkipped}, капсулы туш на кости {_colSyncs} (туш улеглось {_colSyncDone}), поставил позу смерти копии {_deathRebinds}, не дал спрятать туши {_carcassKept}, туши сдвигались {_carcassMoves}, модель туши выключена {_carcassHidden}, взяли тушек {_pickedUp}, оставили на земле {_pickupLeft}, {MapCarcassStats}";
    }

    [HarmonyPatch(typeof(WildlifeItemInteraction), nameof(WildlifeItemInteraction.PerformInteraction))]
    internal static class Patch_CarcassPickedUp
    {
        private static void Prefix(WildlifeItemInteraction __instance, out long __state) => __state = AnimalSync.PickupStart(__instance);
        private static void Postfix(long __state) => AnimalSync.PickupDone(__state);
    }

    [HarmonyPatch(typeof(WildlifeItem), nameof(WildlifeItem.SpawnCarcass))]
    internal static class Patch_CarcassSpawnedItem
    {
        private static void Postfix(GearItem __result) => AnimalSync.OnCarcassSpawned(__result);
    }

    [HarmonyPatch(typeof(BaseAi), nameof(BaseAi.EnableSkinnedMeshRenderers))]
    internal static class Patch_CarcassStaysVisible
    {
        private static void Prefix(BaseAi __instance, ref bool isEnabled)
        {
            if (!isEnabled && AnimalSync.KeepCarcassVisible(__instance)) isEnabled = true;
        }
    }
}
