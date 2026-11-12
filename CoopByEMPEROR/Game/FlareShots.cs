using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Ракета сигнального пистолета (0.19.38). Раньше остальные видели её только как вещь: физика мода замечала летящую
    // ракету рядом со стрелявшим и рассылала её как сдвинутую вещь. У остальных появлялась копия, уже горящая, а когда
    // сверка потом убирала такую копию, звук горения оставался висеть на месте («звук ракеты там, где её нет»).
    // Теперь стрелявший рассылает сам выстрел: откуда и куда. У остальных игра выпускает такую же ракету
    // (FlareGunRoundItem.SpawnAndFire делает то же самое): она летит, светит, дымит, горит и гаснет, как у стрелявшего.
    // Урона и испуга зверей от чужой ракеты нет: попадание и испуг приходят от стрелявшего. Выпущенные ракеты
    // (свои и чужие) мод вещами не рассылает и не сверяет. Чужую ракету убираем, когда она догорела.
    public static class FlareShots
    {
        // Выпущенные ракеты (свои и чужие): их не рассылаем как вещи.
        private static readonly HashSet<IntPtr> Fired = new();
        private sealed class Remote { public GameObject Go; public FlareGunRoundItem Round; public float At; public string Scene; }
        private static readonly List<Remote> Remotes = new();
        private static bool _spawning;
        private static int _sent, _shown, _failed, _removed;
        public static string Stats => $"ракет отправил {_sent}, показал чужих {_shown}, не смог {_failed}, убрал догоревших {_removed}";

        internal static bool IsRemote(FlareGunRoundItem r)
        {
            if (r == null) return false;
            foreach (var x in Remotes) if (x.Round != null && x.Round.Pointer == r.Pointer) return true;
            return false;
        }

        // Выпущенная ракета (в полёте, горит или выпущена при нас): не вещь для рассылки и сверки.
        internal static bool IsFiredRound(GearItem gi)
        {
            if (gi == null) return false;
            try
            {
                var r = gi.GetComponent<FlareGunRoundItem>();
                if (r == null) return false;
                if (Fired.Contains(gi.gameObject.Pointer)) return true;
                return r.InFlight(false) || r.IsBurning();
            }
            catch { return false; }
        }

        // FlareGunRoundItem.SpawnAndFire: ракета вылетела. Своя — рассылаем, чужая (это мы её выпустили) — только запоминаем.
        internal static void OnSpawned(GameObject prefab, Vector3 pos, Quaternion rot, GameObject round)
        {
            if (round == null) return;
            Fired.Add(round.Pointer);
            if (_spawning || Session.Mode == Mode.Off || !Session.InSharedWorld) return;
            string name = prefab != null ? prefab.name : round.name;
            name = (name ?? "").Replace("(Clone)", "").Trim();
            var w = Msg.New(MsgType.FlareShot);
            w.Write(ItemSync.SceneKey());
            w.Write((byte)Session.LocalId);
            w.Write(name);
            w.Write(pos.x); w.Write(pos.y); w.Write(pos.z);
            w.Write(rot.x); w.Write(rot.y); w.Write(rot.z); w.Write(rot.w);
            Session.SendGameEvent(Msg.Done(w));
            if (_sent++ < 10) MelonLogger.Msg($"[ракета] выстрелил {name}: разослал выстрел");
        }

        public static void Receive(BinaryReader r)
        {
            string scene = r.ReadString();
            int shooter = r.ReadByte();
            string prefab = r.ReadString();
            var p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var q = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            if (shooter == Session.LocalId || !GameApi.InGame() || scene != ItemSync.SceneKey() || string.IsNullOrEmpty(prefab)) return;
            if (Remotes.Count > 20) return;
            GearItem gi = null;
            _spawning = true;
            ItemSync.Applying = true;
            try
            {
                gi = GearItem.InstantiateGearItem(prefab);
                if (gi == null) { _failed++; MelonLogger.Msg($"[ракета] чужая {prefab}: игра не создала"); return; }
                var go = gi.gameObject;
                try { DisableObjectForXPMode.RemoveDisabler(go); } catch { }
                go.transform.SetParent(null, true);
                go.transform.SetPositionAndRotation(p, q);
                var round = go.GetComponent<FlareGunRoundItem>();
                if (round == null) { _failed++; UnityEngine.Object.Destroy(go); MelonLogger.Msg($"[ракета] чужая {prefab}: это не ракета"); return; }
                Fired.Add(go.Pointer);
                round.Fire();
                Remotes.Add(new Remote { Go = go, Round = round, At = Time.unscaledTime, Scene = scene });
                if (_shown++ < 10) MelonLogger.Msg($"[ракета] чужая {prefab} от {shooter}: выпустил у себя");
            }
            catch (Exception e)
            {
                _failed++;
                MelonLogger.Warning("[ракета] чужая: " + e.Message);
                try { if (gi != null) UnityEngine.Object.Destroy(gi.gameObject); } catch { }
            }
            finally { _spawning = false; ItemSync.Applying = false; }
        }

        // Чужую ракету убираем, когда догорела и дым рассеялся (или ушли из локации).
        public static void Tick(float now)
        {
            for (int i = Remotes.Count - 1; i >= 0; i--)
            {
                var x = Remotes[i];
                bool gone = x.Go == null || x.Round == null;
                if (!gone)
                {
                    bool burning = false;
                    try { burning = x.Round.InFlight(false) || x.Round.IsBurning(); } catch { }
                    float limit = 60f;
                    try { limit = x.Round.m_BurnTime * 60f + 60f; } catch { }
                    if (x.Scene == ItemSync.SceneKey() && (burning || now - x.At < 30f) && now - x.At < Mathf.Clamp(limit, 60f, 900f)) continue;
                }
                Remotes.RemoveAt(i);
                if (gone) continue;
                Silence(x.Go);
                try { Fired.Remove(x.Go.Pointer); UnityEngine.Object.Destroy(x.Go); _removed++; } catch { }
            }
        }

        // Перед тем как мод уничтожает вещь: глушим её звуки. Иначе зацикленный звук (горящая ракета) остаётся висеть.
        internal static void Silence(GameObject go)
        {
            if (go == null) return;
            try
            {
                var r = go.GetComponent<FlareGunRoundItem>();
                if (r != null && r.m_BurningAudioId != 0) { uint id = r.m_BurningAudioId; GameAudioManager.StopPlayingID(ref id, 100); r.m_BurningAudioId = 0; }
            }
            catch { }
            try { GameAudioManager.StopAllSoundsFromGameObject(go); } catch { }
            AnimalSync.Silence(go);   // звук играет на дочернем излучателе (AkGameObj): глушим и его
        }

        public static void Reset()
        {
            foreach (var x in Remotes) { try { if (x.Go != null) { Silence(x.Go); UnityEngine.Object.Destroy(x.Go); } } catch { } }
            Remotes.Clear();
            Fired.Clear();
        }
    }

    [HarmonyPatch(typeof(FlareGunRoundItem), nameof(FlareGunRoundItem.SpawnAndFire))]
    internal static class Patch_FlareSpawnAndFire
    {
        private static void Postfix(GameObject prefab, Vector3 startPos, Quaternion startRot, GameObject __result)
        {
            try { FlareShots.OnSpawned(prefab, startPos, startRot, __result); } catch (Exception e) { MelonLogger.Warning("[ракета] выстрел: " + e.Message); }
        }
    }

    // Чужая ракета у нас только для вида: урон и испуг зверей приходят от стрелявшего.
    [HarmonyPatch(typeof(FlareGunRoundItem), nameof(FlareGunRoundItem.InflictDamage))]
    internal static class Patch_FlareNoDamage
    {
        private static bool Prefix(FlareGunRoundItem __instance) => !FlareShots.IsRemote(__instance);
    }

    [HarmonyPatch(typeof(FlareGunRoundItem), nameof(FlareGunRoundItem.MaybeScareWildlife))]
    internal static class Patch_FlareNoScare
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(FlareGunRoundItem __instance) => !FlareShots.IsRemote(__instance);
    }
}
