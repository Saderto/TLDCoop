using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Сообщения 0.18.0:
    //   Feature (79, с номером): рюкзак на месте смерти появился / убран (Game/DeathPacks.cs).
    //   Fx (60, без номера): след чужой пули; «беру уголь / факел» — друг спрашивает хоста, хост отвечает.
    public static class Features
    {
        public const byte PackAdd = 1, PackRemove = 2;
        public const byte Impact = 1, ClaimAsk = 2, ClaimAnswer = 3;

        // Feature: у всех одинаково (хост перешлёт остальным сам).
        public static void Receive(BinaryReader r)
        {
            // Друг не в мире хоста (в своём сохранении, в том числе пока оно грузится): рюкзаки мира хоста не к нему,
            // иначе они попали бы в его слот (Game/SlotStore.cs). Пока идёт или грузится копия мира хоста — берём.
            if (Session.Mode == Mode.Client && !Session.InHostWorld && !WorldTransfer.Receiving && !WorldTransfer.Busy) return;
            byte sub = r.ReadByte();
            try
            {
                switch (sub)
                {
                    case PackAdd: DeathPacks.ReceiveAdd(r); break;
                    case PackRemove: DeathPacks.ReceiveRemove(r); break;
                }
            }
            catch (Exception e) { MelonLogger.Warning("[0.18] " + e.Message); }
        }

        // Fx у хоста: true — переслать остальным.
        public static bool HostFx(BinaryReader r, Peer from)
        {
            byte sub = r.ReadByte();
            try
            {
                switch (sub)
                {
                    case Impact: Impacts.Receive(r); return true;
                    case ClaimAsk: Claims.HostAsk(r, from); return false;
                }
            }
            catch (Exception e) { MelonLogger.Warning("[0.18] " + e.Message); }
            return false;
        }

        public static void ClientFx(BinaryReader r)
        {
            byte sub = r.ReadByte();
            try
            {
                switch (sub)
                {
                    case Impact: Impacts.Receive(r); break;
                    case ClaimAnswer: Claims.ClientAnswer(r); break;
                }
            }
            catch (Exception e) { MelonLogger.Warning("[0.18] " + e.Message); }
        }
    }

    // ---------- Следы чужих пуль ----------
    // Свой выстрел попал во что-то: остальным уходит точка, нормаль и материал. У них на этом месте те же
    // искры, снежная пыль или щепки и тот же след на стене, что видит стрелявший. Урона это не наносит —
    // попадания по зверям и игрокам считаются как раньше.
    public static class Impacts
    {
        private static float _lastSent = -1f;
        private static int _inWindow;

        internal static void OnOwnImpact(vp_Bullet b, RaycastHit hit)
        {
            if (Session.Mode == Mode.Off || !Session.InSharedWorld || !GameApi.InGame()) return;
            float now = Time.unscaledTime;
            // 0.19.32: у дробовика за выстрел 8 дробин — следы всех за 0,05 с, а не одного (не больше 12 за окно).
            if (now - _lastSent >= 0.05f) { _lastSent = now; _inWindow = 0; }
            if (++_inWindow > 12) return;
            string tag = "";
            try { tag = Utils.GetMaterialTagForObjectAtPosition(hit.collider.gameObject, hit.point) ?? ""; } catch { }
            var w = Msg.New(MsgType.Fx);
            w.Write(Features.Impact);
            w.Write(GameApi.Scene());
            w.Write(hit.point.x); w.Write(hit.point.y); w.Write(hit.point.z);
            w.Write(hit.normal.x); w.Write(hit.normal.y); w.Write(hit.normal.z);
            w.Write(tag);
            byte gun = 0;
            try { gun = (byte)b.m_GunType; } catch { }
            w.Write(gun);
            var data = Msg.Done(w);
            if (Session.Mode == Mode.Host) Session.BroadcastFast(data);
            else Session.SendGameEvent(data);
        }

        internal static void Receive(BinaryReader r)
        {
            string scene = r.ReadString();
            var p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var n = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            string tag = r.ReadString();
            r.ReadByte();
            if (!GameApi.InGame() || scene != GameApi.Scene()) return;
            var me = GameManager.GetPlayerTransform();
            if (me != null && (me.position - p).sqrMagnitude > 250f * 250f) return;
            if (n.sqrMagnitude < 0.01f) n = Vector3.up;
            try
            {
                var type = vp_Bullet.GetImpactEffectTypeBasedOnMaterial(tag);
                GameManager.GetEffectPoolManager()?.GetBulletImpactEffectPool()?.SpawnUntilParticlesDone(type, p, Quaternion.LookRotation(n));
                try { GameManager.GetDynamicDecalsManager()?.AddImpactDecal(ProjectileType.Bullet, ImpactDecals.MapBulletImpactEffectTypeToMaterialEffectType(type), p, n); } catch { }
            }
            catch (Exception e) { MelonLogger.Warning("[пуля] след: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(vp_Bullet), nameof(vp_Bullet.SpawnImpactEffects))]
    internal static class Patch_BulletImpact
    {
        private static void Postfix(vp_Bullet __instance, RaycastHit hit)
        {
            try { Impacts.OnOwnImpact(__instance, hit); } catch { }
        }
    }

    // ---------- Уголь и факел из костра: берёт один ----------
    // Раньше двое, взявшие уголь из одного костра почти одновременно, оба получали весь уголь, а с факелом
    // костёр терял время горения только у одного. Теперь решает хост: друг сначала спрашивает («беру»),
    // хост отвечает «бери» или «уже забирает такой-то», и только потом игра отдаёт уголь или факел.
    // Хост не ответил за 1,5 с — берём, как раньше (связь не должна мешать играть).
    public static class Claims
    {
        private const float Window = 3f;
        private static readonly Dictionary<string, (int who, float at)> Held = new();
        private static string _granted, _askKey;
        private static float _grantedAt, _askAt;
        private static int _askId;
        private static Action _retry;

        private static bool Active => Session.Mode != Mode.Off && Session.InSharedWorld && GameApi.InGame() && OthersHere();

        private static bool OthersHere()
        {
            string scene = GameApi.Scene();
            float now = Time.unscaledTime;
            foreach (var p in Session.Players.Values)
                if (p.Id != Session.LocalId && Session.With(p, scene) && now - p.LastState < 5f) return true;
            return false;
        }

        public static string FireKey(string what)
        {
            var me = GameManager.GetPlayerTransform();
            if (me == null) return null;
            Fire best = null;
            float bd = 16f;
            foreach (var f in UnityEngine.Object.FindObjectsOfType<Fire>())
            {
                if (f == null) continue;
                float d = (f.transform.position - me.position).sqrMagnitude;
                if (d < bd) { bd = d; best = f; }
            }
            return best == null ? null : what + ":" + WorldObjects.IdOf(best);
        }

        // Для патчей: можно брать сейчас. false — спросили хоста (или отказ), retry выполнит действие после «бери».
        public static bool Allow(string key, Action retry)
        {
            if (key == null) return true;
            bool active;
            try { active = Active; } catch { active = false; }
            if (!active) return true;
            float now = Time.unscaledTime;
            if (_granted == key && now - _grantedAt < Window) { _granted = null; return true; }
            if (Session.Mode == Mode.Host)
            {
                if (Held.TryGetValue(key, out var h) && h.who != Session.LocalId && now - h.at < Window)
                {
                    GameApi.HudMessage("Уже забирает: " + NameOf(h.who));
                    return false;
                }
                Held[key] = (Session.LocalId, now);
                return true;
            }
            if (_askKey == key && now - _askAt < 1.5f) return false;   // уже спросили, ждём
            _askKey = key;
            _askAt = now;
            _askId++;
            _retry = retry;
            var w = Msg.New(MsgType.Fx);
            w.Write(Features.ClaimAsk);
            w.Write(_askId);
            w.Write(key);
            Session.SendGameEvent(Msg.Done(w));
            return false;
        }

        internal static void HostAsk(BinaryReader r, Peer from)
        {
            int id = r.ReadInt32();
            string key = r.ReadString();
            if (from == null) return;
            float now = Time.unscaledTime;
            bool ok = !(Held.TryGetValue(key, out var h) && h.who != from.Id && now - h.at < Window);
            if (ok) Held[key] = (from.Id, now);
            var w = Msg.New(MsgType.Fx);
            w.Write(Features.ClaimAnswer);
            w.Write(id);
            w.Write(ok);
            w.Write(ok ? "" : NameOf(h.who));
            from.Send(Msg.Done(w));
            MelonLogger.Msg($"[костёр] {key}: {(ok ? "отдал" : "отказал")} игроку {from.Id}");
        }

        internal static void ClientAnswer(BinaryReader r)
        {
            int id = r.ReadInt32();
            bool ok = r.ReadBoolean();
            string who = r.ReadString();
            if (id != _askId || _askKey == null) return;
            string key = _askKey;
            var retry = _retry;
            _askKey = null;
            _retry = null;
            if (!ok) { GameApi.HudMessage("Уже забирает: " + who); return; }
            Grant(key, retry);
        }

        private static void Grant(string key, Action retry)
        {
            _granted = key;
            _grantedAt = Time.unscaledTime;
            try { retry?.Invoke(); } catch (Exception e) { MelonLogger.Warning("[костёр] взять: " + e.Message); }
            _granted = null;
        }

        public static void Tick()
        {
            if (_askKey == null || Time.unscaledTime - _askAt < 1.5f) return;
            string key = _askKey;
            var retry = _retry;
            _askKey = null;
            _retry = null;
            MelonLogger.Msg($"[костёр] хост не ответил про {key}, беру сам");
            Grant(key, retry);
        }

        private static string NameOf(int id) => Session.Players.TryGetValue(id, out var p) && p.Name.Length > 0 ? p.Name : "другой игрок";
    }

    [HarmonyPatch(typeof(Panel_FireStart), nameof(Panel_FireStart.OnCharcoalHarvest))]
    internal static class Patch_ClaimCharcoal
    {
        private static bool Prefix(Panel_FireStart __instance)
        {
            try
            {
                var panel = __instance;
                return Claims.Allow(Claims.FireKey("coal"), () => panel.OnCharcoalHarvest());
            }
            catch { return true; }
        }
    }

    [HarmonyPatch(typeof(Panel_FeedFire), nameof(Panel_FeedFire.OnTakeTorch))]
    internal static class Patch_ClaimTorch
    {
        private static float _leftBefore;

        internal static float MinutesLeft(Fire f)
        {
            try { return f == null ? -1f : (f.m_MaxOnTODSeconds - f.m_ElapsedOnTODSeconds) / 60f; } catch { return -1f; }
        }

        private static bool Prefix(Panel_FeedFire __instance, out Fire __state)
        {
            __state = null;
            try
            {
                if (!__instance.CanTakeTorch()) return true;   // нельзя — игра сама скажет
                var panel = __instance;
                bool go = Claims.Allow(Claims.FireKey("torch"), () => panel.OnTakeTorch());
                if (go && Session.Mode != Mode.Off) { __state = panel.m_FireplaceInteraction?.Fire; _leftBefore = MinutesLeft(__state); }
                return go;
            }
            catch { return true; }
        }

        // Факел взят: игра (Panel_FeedFire.OnTakeTorch) прибавляет костру прошедшее время (m_ElapsedOnTODSeconds) и убавляет
        // жар (m_FuelHeatIncrease) — рассылаем костёр, как после подкидывания топлива.
        // Иначе таймер костра хозяина локации возвращал время, а жар у взявшего оставался меньше.
        private static void Postfix(Fire __state)
        {
            if (__state == null || WorldObjects.Applying) return;
            MelonLogger.Msg($"[костёр] взял факел: костру оставалось {_leftBefore:0.#} мин, стало {MinutesLeft(__state):0.#} мин");
            try { WorldObjects.Send(WorldObjects.Kind.Fire, __state, WorldObjects.FireState(__state)); } catch { }
        }
    }
}
