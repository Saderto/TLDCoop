using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Audio;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Звуки животных. Кукла сама не думает, поэтому рычание, вой, бегство и прочее играет тот, у кого мозг:
    // каждый звук его настоящего зверя уходит остальным (если кто-то рядом), и они проигрывают его на своей копии.
    // Шаги и звуки из самой анимации помечены: если у нас копии получают события анимации сами (тогда у них
    // и шаги, и следы на снегу свои, точно в такт), помеченные звуки не проигрываем, чтобы не было двойных.
    public static partial class AnimalSync
    {
        // Дальше этого звук зверя не рассылаем.
        private const float SoundRange = 150f;

        private const byte OpName = 0, OpId = 1, OpStop = 2, OpStopAll = 3, FromAnimation = 0x10;

        private static int _animDepth, _puppetAnimEvents, _staleMuted, _struggleMuted, _silenced, _heardStopped;

        // Копия без вестей от ведущего дольше этого стоит там, где зверя уже нет: звуков её анимации не играем.
        private const float StaleSound = 2f;

        private static int _soundDepth;
        private static bool _playingRemote;
        // Наши разосланные звуки (id проигрывания → зверь), чтобы разослать и их остановку.
        private static readonly Dictionary<uint, long> SentSounds = new();
        // Чужие звуки на наших копиях: (кто, его id проигрывания) → (зверь, наш id проигрывания).
        private static readonly Dictionary<(int from, uint id), (long gid, uint local)> HeardSounds = new();
        private static int _soundsOut, _soundsIn;

        private static Animal AnimalOf(GameObject go)
        {
            if (go == null || Animals.Count == 0) return null;
            var ai = go.GetComponentInParent<BaseAi>();
            if (ai == null || !ByAi.TryGetValue(ai.Pointer, out var gid)) return null;
            return Animals.TryGetValue(gid, out var a) ? a : null;
        }

        // Патч событий анимации зверя (шаги, звуки из анимации). false — событие копии не играем:
        //  — копия без вестей от ведущего (дольше StaleSound): зверь у ведущего уже ушёл или выключен, а копия
        //    до того, как её уберут (6 с), стоит на старом месте и «шагает» и рычит там, где зверя нет;
        //  — звук схватки: игра играет его у НАШЕГО игрока (BaseAi.OnAudioStruggleEvent → Play3DSound на
        //    GetPlayerObject), и схватка друга с волком в стороне звучала бы у нас в ушах. Свою схватку не трогаем.
        public static bool AnimEventBegin(BaseAi ai, bool struggle, out bool counted)
        {
            counted = false;
            Animal a = null;
            try { if (ai != null && Animals.Count > 0 && ByAi.TryGetValue(ai.Pointer, out var g)) Animals.TryGetValue(g, out a); } catch { }
            if (a != null && !a.Real)
            {
                if (struggle && !IsLocalPartner(a)) { _struggleMuted++; return false; }
                if (!a.Dead && Time.unscaledTime - a.Seen > StaleSound) { _staleMuted++; return false; }
                _puppetAnimEvents++;
            }
            _animDepth++;
            counted = true;
            return true;
        }

        public static void AnimEventEnd(bool counted)
        {
            if (counted) _animDepth = Math.Max(0, _animDepth - 1);
        }

        // Wwise не глушит звук, когда объект уничтожают: AkGameObj.OnDestroy только снимает его с учёта
        // (UnregisterGameObj), а начатый звук доигрывает там, где объект был, петля — без конца. Поэтому игра
        // перед уничтожением зверя глушит его сама (BaseAi.Despawn → StopAllSoundsFromGameObject). Копии и лишних
        // зверей мод уничтожает сам — глушим так же. Звучит у зверя не он сам, а «излучатель» — дочерний объект,
        // который игра вешает при первом звуке (GameAudioManager.AddAudioEmitterProxy), поэтому глушим через игру
        // (она найдёт излучатель) и ещё каждый объект с AkGameObj внутри (звук мог играть и на части модели).
        internal static void Silence(GameObject go)
        {
            if (go == null) return;
            bool any = false;
            try
            {
                foreach (var ak in go.GetComponentsInChildren<AkGameObj>(true))
                {
                    if (ak == null) continue;
                    try { AkSoundEngine.StopAll(ak.gameObject); any = true; } catch { }
                }
            }
            catch { }
            try
            {
                var ai = go.GetComponentInChildren<BaseAi>(true);
                if (ai != null && any) GameAudioManager.StopAllSoundsFromGameObject(ai.gameObject);
            }
            catch { }
            if (any) _silenced++;
        }

        // Патч звуков игры: звук нашего настоящего зверя запоминаем, чтобы после разослать.
        public static bool SoundPrefix(GameObject go, ref uint result, out object state)
        {
            state = null;
            _soundDepth++;
            if (_soundDepth > 1 || _playingRemote) return true;
            try
            {
                if (!Active) return true;
                var a = AnimalOf(go);
                if (a != null && a.Real && !a.Shadow) state = a;
            }
            catch { }
            return true;
        }

        // То же для Play3DSound по имени и номеру: так игра играет и шаги, и звуки анимации (OnAudioEvent) —
        // копии играют их сами из своей анимации, поэтому рассылаем только остальное (рычание, бегство, укус).
        public static bool Sound3DPrefix(GameObject go, ref uint result, out object state)
        {
            SoundPrefix(go, ref result, out state);
            if (_animDepth > 0) state = null;
            return true;
        }

        public static void SoundPostfix(object state, string name, uint id, uint playing)
        {
            _soundDepth = Math.Max(0, _soundDepth - 1);
            if (_soundDepth > 0 || state is not Animal a) return;
            try
            {
                if (a.Go == null || !NearOthers(a.Go.transform.position)) return;
                SendSound(a.Gid, (byte)((name != null ? OpName : OpId) | (_animDepth > 0 ? FromAnimation : 0)), name ?? "", id, playing);
                if (playing != 0)
                {
                    if (SentSounds.Count > 512) SentSounds.Clear();
                    SentSounds[playing] = a.Gid;
                }
            }
            catch { }
        }

        // Патч остановки звука: остановили разосланный — останавливаем и у остальных.
        public static void OnStopSound(uint playing)
        {
            if (playing == 0 || !SentSounds.TryGetValue(playing, out long gid)) return;
            SentSounds.Remove(playing);
            try { SendSound(gid, OpStop, "", 0, playing); } catch { }
        }

        public static void OnStopAllSounds(GameObject go)
        {
            if (_playingRemote) return;
            try
            {
                if (!Active) return;
                var a = AnimalOf(go);
                if (a == null || !a.Real || a.Shadow) return;
                StopSent(a.Gid, false);
                if (NearOthers(go.transform.position)) SendSound(a.Gid, OpStopAll, "", 0, 0);
            }
            catch { }
        }

        private static bool NearOthers(Vector3 pos)
        {
            foreach (var p in PlayersHere(_scene, Time.unscaledTime, false))
                if ((p.pos - pos).sqrMagnitude < SoundRange * SoundRange) return true;
            return false;
        }

        private static void SendSound(long gid, byte op, string name, uint id, uint playing)
        {
            var w = Msg.New(MsgType.AnimalSound);
            w.Write((byte)Session.LocalId);
            w.Write(_scene);
            w.Write(gid);
            w.Write(op);
            w.Write(name);
            w.Write(id);
            w.Write(playing);
            Session.SendGameEvent(Msg.Done(w));
            if ((op & 0x0F) <= OpId) _soundsOut++;
        }

        // Наш зверь стал копией (или ушёл): его звуки, что ещё играют у остальных, останавливаем.
        private static void StopSent(long gid, bool send)
        {
            List<uint> ids = null;
            foreach (var kv in SentSounds) if (kv.Value == gid) (ids ??= new List<uint>()).Add(kv.Key);
            if (ids == null) return;
            foreach (var id in ids)
            {
                SentSounds.Remove(id);
                if (send) { try { SendSound(gid, OpStop, "", 0, id); } catch { } }
            }
        }

        // Кукла стала нашим зверем (или пропала): чужие звуки на ней останавливаем.
        private static void StopHeard(long gid)
        {
            List<(int, uint)> keys = null;
            foreach (var kv in HeardSounds) if (kv.Value.gid == gid) (keys ??= new List<(int, uint)>()).Add(kv.Key);
            if (keys == null) return;
            foreach (var k in keys)
            {
                uint local = HeardSounds[k].local;
                HeardSounds.Remove(k);
                try { GameAudioManager.StopPlayingID(ref local, 100); } catch { }
            }
        }

        private static void SoundsOnReplica(Animal a)
        {
            StopSent(a.Gid, true);
            try { if (a.Go != null) GameAudioManager.StopAllSoundsFromGameObject(a.Go); } catch { }
        }

        private static void ClearSounds()
        {
            // Чужие звуки на копиях обрываем: их объекты сейчас уничтожаются, а звук без объекта доигрывал бы на месте.
            foreach (var h in HeardSounds.Values)
            {
                uint local = h.local;
                try { GameAudioManager.StopPlayingID(ref local, 100); _heardStopped++; } catch { }
            }
            SentSounds.Clear();
            HeardSounds.Clear();
            _soundDepth = 0;
        }

        private static void ReceiveSound(BinaryReader r)
        {
            int from = r.ReadByte();
            string scene = r.ReadString();
            long gid = r.ReadInt64();
            byte op = r.ReadByte();
            bool fromAnimation = (op & FromAnimation) != 0;
            op &= 0x0F;
            string name = r.ReadString();
            uint id = r.ReadUInt32();
            uint playing = r.ReadUInt32();
            if (from == Session.LocalId || scene != _scene || OtherWorld(from)) return;
            var key = (from, playing);
            if (op == OpStop)
            {
                if (!HeardSounds.TryGetValue(key, out var h)) return;
                HeardSounds.Remove(key);
                uint local = h.local;
                GameAudioManager.StopPlayingID(ref local, 100);
                return;
            }
            if (!Animals.TryGetValue(gid, out var a) || a.Go == null || a.Real) return;
            if (op == OpStopAll)
            {
                StopHeard(gid);
                _playingRemote = true;
                try { GameAudioManager.StopAllSoundsFromGameObject(a.Go); }
                finally { _playingRemote = false; }
                return;
            }
            if (fromAnimation && _puppetAnimEvents > 0) return;   // копия сама играет звуки своей анимации
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return;
            if ((new Vector3(x, y, z) - a.Go.transform.position).sqrMagnitude > (SoundRange + 30f) * (SoundRange + 30f)) return;
            _playingRemote = true;
            try
            {
                uint local = op == OpName ? GameAudioManager.PlaySound(name, a.Go) : GameAudioManager.PlaySound(id, a.Go);
                _soundsIn++;
                if (local != 0 && playing != 0)
                {
                    if (HeardSounds.Count > 512) HeardSounds.Clear();
                    HeardSounds[key] = (gid, local);
                }
            }
            finally { _playingRemote = false; }
        }
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.PlaySound), new[] { typeof(string), typeof(GameObject) })]
    internal static class Patch_SoundByName
    {
        private static bool Prefix(GameObject go, ref uint __result, out object __state) => AnimalSync.SoundPrefix(go, ref __result, out __state);
        private static void Postfix(string soundID, uint __result, object __state) => AnimalSync.SoundPostfix(__state, soundID, 0, __result);
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.PlaySound), new[] { typeof(uint), typeof(GameObject) })]
    internal static class Patch_SoundById
    {
        private static bool Prefix(GameObject go, ref uint __result, out object __state) => AnimalSync.SoundPrefix(go, ref __result, out __state);
        private static void Postfix(uint soundID, uint __result, object __state) => AnimalSync.SoundPostfix(__state, null, soundID, __result);
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.PlaySound), new[] { typeof(Il2CppAK.Wwise.Event), typeof(GameObject) })]
    internal static class Patch_SoundByEvent
    {
        private static bool Prefix(GameObject go, ref uint __result, out object __state) => AnimalSync.SoundPrefix(go, ref __result, out __state);
        private static void Postfix(Il2CppAK.Wwise.Event soundEvent, uint __result, object __state) =>
            AnimalSync.SoundPostfix(__state, null, __state != null && soundEvent != null ? soundEvent.Id : 0, __result);
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.Play3DSound), new[] { typeof(Il2CppAK.Wwise.Event), typeof(GameObject) })]
    internal static class Patch_Sound3D
    {
        private static bool Prefix(GameObject go, ref uint __result, out object __state) => AnimalSync.SoundPrefix(go, ref __result, out __state);
        private static void Postfix(Il2CppAK.Wwise.Event soundEvent, uint __result, object __state) =>
            AnimalSync.SoundPostfix(__state, null, __state != null && soundEvent != null ? soundEvent.Id : 0, __result);
    }

    // Рычание при выслеживании, взвизг при бегстве, укус (BaseAi.UpdateStalkingAudio, PlayEnterFleeAudio,
    // PlayMeleeAttackAudio) игра играет через Play3DSound по имени или номеру. Шаги и OnAudioEvent идут тем же
    // путём, но их копии играют сами из своей анимации — их не рассылаем (Sound3DPrefix).
    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.Play3DSound), new[] { typeof(string), typeof(GameObject) })]
    internal static class Patch_Sound3DByName
    {
        private static bool Prefix(GameObject go, ref uint __result, out object __state) => AnimalSync.Sound3DPrefix(go, ref __result, out __state);
        private static void Postfix(string soundID, uint __result, object __state) => AnimalSync.SoundPostfix(__state, soundID, 0, __result);
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.Play3DSound), new[] { typeof(uint), typeof(GameObject) })]
    internal static class Patch_Sound3DById
    {
        private static bool Prefix(GameObject go, ref uint __result, out object __state) => AnimalSync.Sound3DPrefix(go, ref __result, out __state);
        private static void Postfix(uint soundID, uint __result, object __state) => AnimalSync.SoundPostfix(__state, null, soundID, __result);
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.PlaySoundWithPositionTracking),
        new[] { typeof(string), typeof(GameObject), typeof(AkCallbackManager.EventCallback), typeof(GameAudioManager.PlayOptions) })]
    internal static class Patch_SoundTracked
    {
        private static bool Prefix(GameObject go, ref uint __result, out object __state) => AnimalSync.SoundPrefix(go, ref __result, out __state);
        private static void Postfix(string soundID, uint __result, object __state) => AnimalSync.SoundPostfix(__state, soundID, 0, __result);
    }

    [HarmonyPatch(typeof(BaseAi))]
    internal static class Patch_AnimalAnimEvents
    {
        [HarmonyPatch(nameof(BaseAi.OnAudioEvent)), HarmonyPrefix] private static bool A(BaseAi __instance, out bool __state) => AnimalSync.AnimEventBegin(__instance, false, out __state);
        [HarmonyPatch(nameof(BaseAi.OnAudioEvent)), HarmonyPostfix] private static void A2(bool __state) => AnimalSync.AnimEventEnd(__state);
        [HarmonyPatch(nameof(BaseAi.OnAudioStruggleEvent)), HarmonyPrefix] private static bool B(BaseAi __instance, out bool __state) => AnimalSync.AnimEventBegin(__instance, true, out __state);
        [HarmonyPatch(nameof(BaseAi.OnAudioStruggleEvent)), HarmonyPostfix] private static void B2(bool __state) => AnimalSync.AnimEventEnd(__state);
        [HarmonyPatch(nameof(BaseAi.OnFootStepFrontLeft)), HarmonyPrefix] private static bool C(BaseAi __instance, out bool __state) => AnimalSync.AnimEventBegin(__instance, false, out __state);
        [HarmonyPatch(nameof(BaseAi.OnFootStepFrontLeft)), HarmonyPostfix] private static void C2(bool __state) => AnimalSync.AnimEventEnd(__state);
        [HarmonyPatch(nameof(BaseAi.OnFootStepFrontRight)), HarmonyPrefix] private static bool D(BaseAi __instance, out bool __state) => AnimalSync.AnimEventBegin(__instance, false, out __state);
        [HarmonyPatch(nameof(BaseAi.OnFootStepFrontRight)), HarmonyPostfix] private static void D2(bool __state) => AnimalSync.AnimEventEnd(__state);
        [HarmonyPatch(nameof(BaseAi.OnFootStepBackLeft)), HarmonyPrefix] private static bool E(BaseAi __instance, out bool __state) => AnimalSync.AnimEventBegin(__instance, false, out __state);
        [HarmonyPatch(nameof(BaseAi.OnFootStepBackLeft)), HarmonyPostfix] private static void E2(bool __state) => AnimalSync.AnimEventEnd(__state);
        [HarmonyPatch(nameof(BaseAi.OnFootStepBackRight)), HarmonyPrefix] private static bool F(BaseAi __instance, out bool __state) => AnimalSync.AnimEventBegin(__instance, false, out __state);
        [HarmonyPatch(nameof(BaseAi.OnFootStepBackRight)), HarmonyPostfix] private static void F2(bool __state) => AnimalSync.AnimEventEnd(__state);
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.StopPlayingID))]
    internal static class Patch_SoundStop
    {
        private static void Prefix(ref uint id) => AnimalSync.OnStopSound(id);
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.StopAllSoundsFromGameObject))]
    internal static class Patch_SoundStopAll
    {
        private static void Prefix(GameObject go) => AnimalSync.OnStopAllSounds(go);
    }
}
