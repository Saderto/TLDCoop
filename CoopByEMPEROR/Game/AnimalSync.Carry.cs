using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Друг был хозяином зверей локации без хоста, а потом все ушли. Хост хранит то, что там стало
    // (места появления и туши — через состояния объектов, они же попадают в pending.bin), и, войдя туда,
    // берёт это вместо своего старого: места появления — ещё при загрузке, до первого спавна,
    // туши — создаёт сам.
    public static partial class AnimalSync
    {
        private static readonly Dictionary<long, string> SentCarcasses = new();
        private static int _regionsTaken, _regionsLate, _carcassesMade, _carcassesSkipped;
        private static string _lateRegionsScene = "";

        public static string CarryStats => $"мест появления от друга при загрузке {_regionsTaken}, после загрузки {_regionsLate}; туш от друга создал {_carcassesMade}, пропустил {_carcassesSkipped}, ждали модель {_carcassesDeferred}";

        // ---------- Отправка (хозяин локации — не хост) ----------

        private static void SendCarcasses()
        {
            foreach (var a in Animals.Values)
            {
                if (!a.Real || a.Ai == null || a.Go == null) continue;
                bool dead;
                try { dead = a.Ai.m_CurrentMode == AiMode.Dead || a.Ai.m_CurrentHP <= 0f; } catch { continue; }
                if (!dead) continue;
                string harvest = "";
                try { var bh = a.Go.GetComponent<BodyHarvest>(); if (bh != null) harvest = bh.Serialize() ?? ""; } catch { }
                var t = a.Go.transform;
                var p = t.position;
                var q = t.rotation;
                var ic = CultureInfo.InvariantCulture;
                string state = string.Join("|", a.Prefab, a.RegionHash.ToString(ic),
                    p.x.ToString("R", ic), p.y.ToString("R", ic), p.z.ToString("R", ic),
                    q.x.ToString("R", ic), q.y.ToString("R", ic), q.z.ToString("R", ic), q.w.ToString("R", ic),
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(harvest)));
                if (SentCarcasses.TryGetValue(a.Gid, out var old) && old == state) continue;
                SentCarcasses[a.Gid] = state;
                WorldObjects.Send(WorldObjects.Kind.Carcass, null, state, CarcassId(a.Gid));
            }
        }

        // Туша исчезла у хозяина (съедена целиком, убрана игрой): хосту — «нет её», чтобы не создавал.
        private static void SendCarcassGone(Animal a)
        {
            if (Session.Mode != Mode.Client || !IsAuthority || !SentCarcasses.Remove(a.Gid)) return;
            WorldObjects.Send(WorldObjects.Kind.Carcass, null, "", CarcassId(a.Gid));
        }

        private static string CarcassId(long gid) => "carcass:" + gid.ToString(CultureInfo.InvariantCulture);

        // Уходим из локации, пока она ещё загружена: дослать то, что не успело уйти по таймеру.
        public static void Leaving()
        {
            if (!Active || _scene != GameApi.Scene()) return;
            if (Time.unscaledTime - _regionsLeftAt < 5f) return;   // LoadScene и LoadSceneWithLoadingScreen на один переход
            if (!IsAuthority)
            {
                _regionsLeftAt = Time.unscaledTime;
                ReturnLeases("ухожу с локации");
                return;
            }
            _regionsLeftAt = Time.unscaledTime;
            // «Хозяин ушёл» — сейчас, пока локация и _scene ещё прежние: на экране загрузки мод уже всё сбросил, и
            // из Tick это сообщение не уходило никогда. Оставшиеся выбирают нового хозяина сразу и без двойного.
            Announce(_scene, true);
            _nextAnnounce = Time.unscaledTime + 10f;   // до загрузки не объявляем себя хозяином снова (если переход сорвался — продолжим)
            SendRegionsNow(true, "ухожу");
            if (Session.Mode == Mode.Client) SendCarcasses();
            MelonLogger.Msg($"[звери] ухожу из локации хозяином: дослал места появления" + (Session.Mode == Mode.Client ? " и туши" : ""));
        }

        // Не хозяин уходит с локации: одолженных ему зверей (и убитых им — туши) сразу отдаём хозяину локации.
        // Раньше хозяин ждал 4-15 секунд тишины (зверь стоял), а туша оставалась у него тенью.
        private static void ReturnLeases(string why)
        {
            float now = Time.unscaledTime;
            if (_authority < 0 || !Here(_authority, now)) return;
            int n = 0;
            foreach (var a in new List<Animal>(Animals.Values))
            {
                if (!a.Real || a.Go == null || a.Ai == null) continue;
                Transfer(a, _authority, why);
                n++;
            }
            if (n > 0) MelonLogger.Msg($"[звери] ухожу с локации: отдал хозяину {NameOf(_authority)} зверей {n}");
        }

        // ---------- Приём ----------

        public static void ApplyCarcass(string id, string state)
        {
            string prev = Enter("туша от друга " + id);
            try { ApplyCarcassCore(id, state); }
            finally { Leave(prev); }
        }

        private static void ApplyCarcassCore(string id, string state)
        {
            if (ApplyMapCarcass(id, state)) return;   // туша, лежащая на карте (Game/AnimalSync.MapCarcass.cs)
            if (!id.StartsWith("carcass:") || !long.TryParse(id.Substring(8), NumberStyles.Integer, CultureInfo.InvariantCulture, out long gid)) return;
            if (string.IsNullOrEmpty(state) || Animals.ContainsKey(gid)) return;
            if (IsGone(gid, Time.unscaledTime)) { SkipGone(state.Split('|')[0], "пришла его туша"); return; }
            // Создаёт тушу только хозяин локации; у остальных она появится копией от него.
            if (!IsAuthority) return;
            var f = state.Split('|');
            if (f.Length < 10) return;
            var ic = CultureInfo.InvariantCulture;
            string prefab = f[0];
            int regionHash = int.Parse(f[1], ic);
            var pos = new Vector3(float.Parse(f[2], ic), float.Parse(f[3], ic), float.Parse(f[4], ic));
            var rot = new Quaternion(float.Parse(f[5], ic), float.Parse(f[6], ic), float.Parse(f[7], ic), float.Parse(f[8], ic));
            string harvest = Encoding.UTF8.GetString(Convert.FromBase64String(f[9]));
            // Такая туша у нас уже есть (например, из своего сохранения) — второй не надо.
            foreach (var ai in BaseAiManager.m_BaseAis)
            {
                if (ai == null || (ai.transform.position - pos).sqrMagnitude > 4f) continue;
                try
                {
                    if (ai.m_CurrentMode == AiMode.Dead && SpawnRegion.GetPrefabNameFromInstanceName(ai.gameObject.name) == prefab)
                    {
                        _carcassesSkipped++;
                        return;
                    }
                }
                catch { }
            }
            var made = InstantiateFromRegion(prefab, KeyOfHash(regionHash), pos, rot);
            if (made == null && PrefabsLoading)
            {
                // Модели локации ещё грузятся (без ожидания): туша подождёт, а не пропадёт.
                DeferCarcass(id, state);
                return;
            }
            if (made == null)
            {
                _carcassesSkipped++;
                MelonLogger.Msg($"[звери] туша {prefab} от друга: не смог создать");
                return;
            }
            // Убитого друг уже учёл в своём месте появления: у нас туша к месту не относится, иначе учёт двойной.
            try { made.m_SpawnRegionParent?.m_Spawns?.Remove(made); made.SetSpawnRegionParent(null); } catch { }
            var a = new Animal { Gid = gid, Prefab = prefab, Real = true, Controller = Session.LocalId, Pos = pos, Rot = rot, Seen = Time.unscaledTime, RegionHash = regionHash, RegionKey = KeyOfHash(regionHash), RealSince = Time.unscaledTime };
            Adopt(a, made);
            Animals[gid] = a;
            _applyingHit = true;
            try { made.ApplyDamage(100000f, 0f, DamageSource.Player, ""); }
            catch (Exception e) { MelonLogger.Warning("[звери] туша от друга: " + e.Message); }
            finally { _applyingHit = false; }
            a.Dead = true;
            a.DeadHandled = true;
            a.KillNoted = true;
            a.DeadAt = Time.unscaledTime;
            StartColliderSync(a);
            if (!string.IsNullOrEmpty(harvest)) PendingHarvest[gid] = harvest;   // HarvestTick применит, когда туша устоится
            _carcassesMade++;
            MelonLogger.Msg($"[звери] туша {prefab} от друга: создал у себя");
        }

        private static readonly List<(string id, string state, float at)> DeferredCarcasses = new();
        private static int _carcassesDeferred;

        private static void DeferCarcass(string id, string state)
        {
            DeferredCarcasses.RemoveAll(c => c.id == id);
            if (DeferredCarcasses.Count >= 64) return;
            DeferredCarcasses.Add((id, state, Time.unscaledTime));
            _carcassesDeferred++;
        }

        // Модели готовы — создаём отложенные туши. Сменили локацию или ждём больше минуты — бросаем.
        private static void RetryCarcasses(float now)
        {
            if (DeferredCarcasses.Count == 0 || PrefabsLoading) return;
            var list = new List<(string id, string state, float at)>(DeferredCarcasses);
            DeferredCarcasses.Clear();
            foreach (var c in list)
            {
                if (now - c.at > 60f) { _carcassesSkipped++; continue; }
                try { ApplyCarcass(c.id, c.state); }
                catch (Exception e) { MelonLogger.Warning("[звери] отложенная туша: " + e.Message); }
            }
        }

        // Игра при загрузке читает место появления из своего сохранения. Если у нас лежит его более новое
        // состояние от друга, подставляем его — тогда игра сразу создаёт тех зверей, что остались у друга.
        internal static void BeforeRegionDeserialize(SpawnRegion sr, ref string text)
        {
            if (RegionLive || Session.Mode == Mode.Off || !Session.InSharedWorld || sr == null) return;
            string id;
            try { id = WorldObjects.IdOf(sr); } catch { return; }
            var state = WorldObjects.TakePendingState(WorldObjects.Kind.Region, id);
            if (string.IsNullOrEmpty(state)) return;
            string json = SplitRegion(state, out var tree);
            if (!string.IsNullOrEmpty(tree)) PendingTree[id] = tree;
            if (string.IsNullOrEmpty(json)) return;
            text = json;
            _regionsTaken++;
            MelonLogger.Msg($"[звери] место появления {id}: при загрузке взял последнее состояние от другого игрока" + (tree != null ? " (с группами над ним)" : ""));
        }

        // Места появления, которых не было в нашем сохранении (мы тут ещё не были): применяем один раз, как только
        // локация загрузилась, до первого спавна. Зверей, которых место уже успело создать, убираем — иначе двойные.
        internal static void ApplyLateRegions()
        {
            if (Session.Mode == Mode.Off || !Session.InSharedWorld || !GameApi.InGame() || GameApi.IsLoading()) return;
            string scene = ItemSync.SceneKey();
            if (scene == _lateRegionsScene) return;
            if (!PlayerProgress.HostPendingLoaded) return;   // мир только загрузили: отложенные ещё не прочитаны
            _lateRegionsScene = scene;
            foreach (var (id, state) in WorldObjects.TakePendingStates(scene, WorldObjects.Kind.Region))
            {
                var sr = WorldObjects.Find<SpawnRegion>(id);
                if (sr == null || string.IsNullOrEmpty(state)) continue;
                try
                {
                    var had = new List<BaseAi>();
                    var spawns = sr.m_Spawns;
                    if (spawns != null) for (int i = 0; i < spawns.Count; i++) if (spawns[i] != null) had.Add(spawns[i]);
                    _despawning = true;
                    try { foreach (var ai in had) ai.Despawn(); }
                    finally { _despawning = false; }
                    string json = SplitRegion(state, out var tree);
                    if (!string.IsNullOrEmpty(json)) sr.Deserialize(json);
                    if (!string.IsNullOrEmpty(tree)) PendingTree[id] = tree;
                    _regionsLate++;
                    MelonLogger.Msg($"[звери] место появления {id}: взял состояние от друга после загрузки" + (had.Count > 0 ? $", убрал своих {had.Count}" : ""));
                }
                catch (Exception e) { MelonLogger.Warning($"[звери] место появления {id}: {e.Message}"); }
            }
            ApplyPendingTrees();
        }
    }

    [HarmonyPatch(typeof(SpawnRegion), nameof(SpawnRegion.Deserialize))]
    internal static class Patch_RegionFromFriend
    {
        private static void Prefix(SpawnRegion __instance, ref string text)
        {
            try { AnimalSync.BeforeRegionDeserialize(__instance, ref text); } catch { }
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadScene), new[] { typeof(string), typeof(string) })]
    internal static class Patch_AnimalsLeaving
    {
        private static void Prefix() { try { AnimalSync.Leaving(); } catch { } }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadSceneWithLoadingScreen))]
    internal static class Patch_AnimalsLeavingScreen
    {
        private static void Prefix() { try { AnimalSync.Leaving(); } catch { } }
    }
}
