using System;
using System.IO;
using System.Runtime.InteropServices;
using Il2Cpp;
using UnityEngine;

namespace CrankLampByEMPEROR
{
    /// <summary>
    /// The crank lamp's own WINTERMUTE sounds. Survival has none of them, so three episode 5 Wwise banks ship with the
    /// mod (Mods/CrankLampByEMPEROR/audio): FlashlighCrankLamp.bnk (the recordings), _StoryStructures.bnk (sound objects) and
    /// _StoryEvents.bnk (event names). Survival has no banks with these names, so they load next to its own.
    /// </summary>
    internal static class Sound
    {
        internal const string Equip = "Play_Gear_FlashlightCrankLamp_Equip_Equip";
        internal const string Unequip = "Play_Gear_FlashlightCrankLamp_Equip_Unequip";
        internal const string TurnOn = "Play_Gear_FlashlightCrankLamp_Gear_TurnOn";
        internal const string TurnOff = "Play_Gear_FlashlightCrankLamp_Gear_TurnOff";
        internal const string CrankStart = "Play_Gear_FlashlightCrankLamp_Action_Start";
        internal const string CrankLoop = "Play_Gear_FlashlightCrankLamp_Action_Loop_Crank";
        internal const string CrankStop = "Stop_Gear_FlashlightCrankLamp_Crank";
        internal const string CrankExit = "Play_Gear_FlashlightCrankLamp_Action_Exit";

        static readonly string[] Banks = { "FlashlighCrankLamp.bnk", "_StoryStructures.bnk", "_StoryEvents.bnk", "TLDCrankLamp_Events.bnk" };
        static bool _loaded;
        static readonly System.Collections.Generic.HashSet<string> _done = new();
        static float _nextTry;

        // 11-11 (log of 0.2.0): at the main menu _StoryStructures.bnk answered AK_InitBankNotLoaded (the game's own Init bank
        // was not in yet), so the events had nothing to play. Banks that did not load are tried again later (scene load,
        // lamp taken in hands) until they do.
        internal static void Load()
        {
            if (_done.Count == Banks.Length) return;
            if (Time.realtimeSinceStartup < _nextTry) return;
            _nextTry = Time.realtimeSinceStartup + 3f;
            foreach (var b in Banks)
            {
                if (_done.Contains(b)) continue;
                string path = Path.Combine(LampMod.Folder, "audio", b);
                if (!File.Exists(path)) { LampMod.Warn("нет звукового банка / missing bank " + path); AssetsHint.Missing(); _done.Add(b); continue; }
                try
                {
                    var data = File.ReadAllBytes(path);
                    IntPtr mem = Marshal.AllocHGlobal(data.Length);
                    Marshal.Copy(data, 0, mem, data.Length);
                    var r = AkSoundEngine.LoadBankMemoryCopy(mem, (uint)data.Length, out uint id);
                    Marshal.FreeHGlobal(mem);
                    LampMod.Log($"банк {b}: {r} (id {id})");
                    if (r == AKRESULT.AK_Success || r == AKRESULT.AK_BankAlreadyLoaded) { _done.Add(b); _loaded = true; }
                }
                catch (Exception e) { LampMod.Warn("bank " + b + ": " + e.Message); }
            }
        }

        // 11-11: our own sound object next to the player. The game stops and restarts its sounds on the player object
        // (flashlight state changes), and the crank loop went silent with them.
        static GameObject _emitter;
        static int _logLeft = 60;
        static GameObject Emitter()
        {
            var p = GameManager.GetPlayerObject();
            if (p == null) return null;
            if (_emitter == null) { _emitter = new GameObject("TLDCrankLamp_Audio"); }
            if (_emitter.transform.parent != p.transform) { _emitter.transform.SetParent(p.transform, false); _emitter.transform.localPosition = new Vector3(0f, 1.4f, 0.3f); }
            return _emitter;
        }

        internal static uint Play(string ev)
        {
            if (!_loaded) return 0;
            try
            {
                var go = Emitter();
                if (go == null) return 0;
                uint id = GameAudioManager.PlaySound(ev, go);
                if (_logLeft > 0) { _logLeft--; LampMod.Log($"звук {ev}: id {id} / sound"); }

                return id;
            }
            catch (Exception e) { LampMod.Warn("sound " + ev + ": " + e.Message); return 0; }
        }

        // 11-11 (0.3.4 log: the episode's loop event was posted on every turn, yet the sound "broke off after 2 s"): the
        // episode's events duck and mute the tick container (Stop_Crank mutes it for 3 s). Our own events bank
        // (TLDCrankLamp_Events.bnk, tools/mkbank.py) plays the 12 tick recordings and the 3 start recordings directly.
        const int Ticks = 12, Starts = 3;
        static int _lastTick = -1, _lastTick2 = -1;
        static string TickName()
        {
            int k; int guard = 0;
            do { k = UnityEngine.Random.Range(0, Ticks); } while ((k == _lastTick || k == _lastTick2) && ++guard < 8);
            _lastTick2 = _lastTick; _lastTick = k;
            return "TLDCrank_Tick_" + k.ToString("00");
        }
        static readonly float[] StartLen = { 2.233f, 1.577f, 2.184f };
        const float Gap = 0.03f;
        const float TickRate = 0.36f;   // the episode's tick container: trigger rate 360 ms
        // 11-11 (tests of 0.3.6-0.3.8): ticks posted with a pause between them were heard, ticks posted so that they
        // overlap (0.3.7, 0.3.8) were not: the sound engine drops a crank sound that starts while another one still
        // plays (where that limit sits was not found). So nothing overlaps now: each sound returns its full length plus
        // a short gap, and the caller posts the next one only then.
        /// <summary>Plays a start sound and returns the seconds after which the first tick may be posted.</summary>
        internal static float CrankStartSound(GameObject on = null)
        {
            int k = UnityEngine.Random.Range(0, Starts);
            string ev = "TLDCrank_Start_" + k;
            if (on != null) PlayOn(ev, on); else if (Play(ev) == 0) Play(CrankStart);
            return 0.9f;   // the episode ducks the ticks for 1 s after the start sound begins
        }
        static readonly float[] TickLen = { 0.541f, 0.552f, 0.420f, 0.503f, 0.398f, 0.497f, 0.480f, 0.490f, 0.586f, 0.483f, 0.519f, 0.455f };
        /// <summary>Plays one tick and returns the seconds after which the next one should be posted.</summary>
        internal static float CrankTick(GameObject on = null)
        {
            string ev = TickName();
            if (on != null) PlayOn(ev, on);
            else if (Play(ev) == 0) Play(CrankLoop);
            return TickRate;
        }

        // diagnostics: 0.2 s after a tick was posted, is its playing id still alive in the sound engine?
        static uint _watchId; static string _watchEv; static float _watchAt; static int _watchLeft = 24;
        static void Watch(uint id, string ev) { if (_watchLeft <= 0 || _watchId != 0) return; _watchId = id; _watchEv = ev; _watchAt = Time.realtimeSinceStartup + 0.2f; }
        internal static void Tick()
        {
            if (_watchId == 0 || Time.realtimeSinceStartup < _watchAt) return;
            uint id = _watchId; _watchId = 0; _watchLeft--;
            try
            {
                var arr = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<uint>(16);
                uint n = 16;
                var r = AkSoundEngine.GetPlayingIDsFromGameObject(_emitter, ref n, arr);
                bool alive = false; string all = "";
                for (int i = 0; i < n && i < 16; i++) { if (arr[i] == id) alive = true; all += arr[i] + " "; }
                LampMod.Log($"звук {_watchEv} id {id}: через 0.2 с {(alive ? "играет" : "НЕ играет")}, всего на источнике {n} [{all.Trim()}] ({r}) / alive check");
            }
            catch (Exception e) { LampMod.Log("alive check: " + e.Message); _watchLeft = 0; }
        }

        // a friend's lamp in coop: the sound comes from that lamp
        internal static uint PlayOn(string ev, GameObject go)
        {
            if (!_loaded || go == null) return 0;
            try { return GameAudioManager.PlaySound(ev, go); } catch { return 0; }
        }

        internal static void SwitchOn(GameObject go, bool cranking)
        {
            if (!_loaded || go == null) return;
            try { AkSoundEngine.SetSwitch("FlashlightCrankLamp_Switch", cranking ? "fcl_Cranking" : "fcl_Idle", go); } catch { }
        }

        internal static void Switch(bool cranking)
        {
            if (!_loaded) return;
            try
            {
                var go = Emitter();
                if (go != null) AkSoundEngine.SetSwitch("FlashlightCrankLamp_Switch", cranking ? "fcl_Cranking" : "fcl_Idle", go);
            }
            catch { }
        }
    }
}
