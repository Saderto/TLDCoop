using System;
using System.Collections.Generic;
using System.IO;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Фразы выживальщика («Холодно…», «Надо поесть» и т. п.) слышат и другие игроки, если стоят ближе 20 м.
    // Свою фразу ловим по смене проигрываемой реплики в PlayerVoice и рассылаем её номер события Wwise.
    // У остальных она звучит из головы фигуры: голос мужской или женский по персонажу, которым его видят
    // (переключатель Wwise «PlayerGender»), а громкость падает с расстоянием до нуля на 20 м.
    public static class VoiceSync
    {
        public const float Range = 20f;

        private static uint _lastPlayback;
        private static int _sent, _heard;

        private class Heard
        {
            public int From;
            public uint Playing;
            public GameObject Go;
            public float Until;
        }

        private static readonly List<Heard> Playing = new();

        // ---------- Своя фраза ----------

        public static void Tick(float now)
        {
            if (Session.Mode == Mode.Off || !Session.InSharedWorld || !GameApi.InGame()) { Playing.Clear(); return; }
            try { CheckOwn(); } catch { }
            try { UpdateVolumes(now); } catch (Exception e) { MelonLogger.Warning("[голос] " + e.Message); Playing.Clear(); }
        }

        private static void CheckOwn()
        {
            var ar = GameManager.GetPlayerVoiceComponent()?.m_ActiveRequest;
            if (ar == null) return;
            uint pb = ar.m_PlaybackID;
            if (pb == 0 || pb == _lastPlayback) return;
            _lastPlayback = pb;
            uint ev = ar.m_EventID;
            string name = ar.GetEventName() ?? "";
            if (ev == 0 && name.Length == 0) return;
            string scene = GameApi.Scene();
            bool anyone = false;
            foreach (var p in Session.Players.Values) if (p.Id != Session.LocalId && Session.With(p, scene)) { anyone = true; break; }
            if (!anyone) return;
            var w = Msg.New(MsgType.PlayerVoice);
            w.Write((byte)Session.LocalId);
            w.Write(scene);
            w.Write(ev);
            w.Write(name);
            w.Write((byte)(PlayerManager.m_VoicePersona == VoicePersona.Female ? 1 : 0));
            Session.SendGameEvent(Msg.Done(w));
            if (_sent++ < 5) MelonLogger.Msg($"[голос] моя фраза ушла другим: {(name.Length > 0 ? name : ev.ToString())}");
        }

        // ---------- Чужая фраза ----------

        public static void Receive(BinaryReader r)
        {
            int from = r.ReadByte();
            string scene = r.ReadString();
            uint ev = r.ReadUInt32();
            string name = r.ReadString();
            bool senderFemale = r.ReadByte() == 1;
            if (from == Session.LocalId || !Session.Sees(Shown.Voices) || !GameApi.InGame() || scene != GameApi.Scene()) return;
            if (!Session.Players.TryGetValue(from, out var pl) || !pl.Puppet.Alive) return;
            var go = pl.Puppet.VoiceEmitter();
            if (go == null || !GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return;
            float d = Vector3.Distance(new Vector3(x, y, z), go.transform.position);
            if (d > Range) return;
            try
            {
                bool female = pl.Puppet.Female ?? senderFemale;
                AkSoundEngine.SetSwitch("PlayerGender", female ? "Female" : "Male", go);
                SetVolume(go, d);
                uint playing = ev != 0 ? AkSoundEngine.PostEvent(ev, go) : AkSoundEngine.PostEvent(name, go);
                if (playing == 0) return;
                Playing.Add(new Heard { From = from, Playing = playing, Go = go, Until = Time.unscaledTime + 20f });
                if (_heard++ < 5) MelonLogger.Msg($"[голос] {pl.Name} сказал фразу ({(female ? "женский" : "мужской")} голос, {d:0} м)");
            }
            catch (Exception e) { MelonLogger.Warning("[голос] " + e.Message); }
        }

        // Пока фраза звучит, громкость следует за расстоянием.
        private static void UpdateVolumes(float now)
        {
            if (Playing.Count == 0) return;
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return;
            var me = new Vector3(x, y, z);
            for (int i = Playing.Count - 1; i >= 0; i--)
            {
                var h = Playing[i];
                if (h.Go == null || now > h.Until) { Playing.RemoveAt(i); continue; }
                SetVolume(h.Go, Vector3.Distance(me, h.Go.transform.position));
            }
        }

        private static void SetVolume(GameObject go, float d)
        {
            // Вблизи (до 2 м) в полную силу, дальше тише, на 20 м уже не слышно.
            float v = Mathf.Clamp01(1f - Mathf.Max(0f, d - 2f) / (Range - 2f));
            v *= v;
            v *= Config.VoiceVolume / 100f;
            var listener = Listener();
            if (listener == null) return;
            AkSoundEngine.SetGameObjectOutputBusVolume(go, listener, v);
        }

        // Тот же слушатель, что и у звуков действий: включённый из слушателей по умолчанию, перепроверяется.
        private static GameObject Listener() => SoundSync.Listener();

        public static void Reset()
        {
            Playing.Clear();
            _lastPlayback = 0;
        }
    }
}
