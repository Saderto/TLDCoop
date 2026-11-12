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
    // Звуки друга: выстрелы, шаги, рубка и разделка, двери и ящики, лук, фонарь и т. п.
    //  — Свои звуки ловим там, где игра их запускает (GameAudioManager.PlaySound / Play3DSound) и где
    //    звук идёт от самого игрока (его объект, камера, оружие в руках) или от двери/ящика рядом с ним.
    //    Берём только звуки из списка ниже, без голоса, дыхания, музыки, интерфейса и зацикленных фонов.
    //  — Выстрел рассылается отдельно, прямо из GunItem.Fired, и слышен далеко (до 350 м).
    //  — Шаги: из FootStepSounds.PlayFootStepSound, вместе с поверхностью (снег, дерево, лёд…).
    // У остальных звук играет из фигуры друга, громкость падает с расстоянием (VoiceSync делает так же).
    // Каждый звук обрывается не позже чем через 12 с, чтобы зацикленный звук не играл вечно.
    public static class SoundSync
    {
        private const byte KEvent = 0, KStep = 1;
        private const byte RNear = 0, RMid = 1, RShot = 2;
        // Дальность: обычные звуки, рубка и двери, выстрел. Раньше 25/60/350 м — просьба игрока снять ограничение
        // (0.17.12): выстрел слышен на всю локацию, остальное заметно дальше.
        private static readonly float[] Ranges = { 60f, 150f, 3000f };

        private static readonly string[] Allow =
        {
            "Bow", "Arrow", "Hatchet", "Axe", "Knife", "Hacksaw", "Saw", "Chop", "Harvest", "Breakdown", "BreakDown",
            "Door", "Container", "Open", "Close", "Search", "Pickup", "PickUp", "Eat", "Drink", "Torch", "Match",
            "Lantern", "Flare", "Reload", "Climb", "Rope", "Jump", "Land", "Fall", "Snare", "Hammer", "Prybar",
            "Bandage", "Craft", "Cook", "Stone", "Rock", "Ice", "Rifle", "Revolver", "Gun", "Shot", "Noisemaker",
            "Struggle", "Punch", "Kick", "Bash", "Smash", "Fish", "Dig",
        };
        private static readonly string[] Deny =
        {
            "Footstep", "Voice", "VO_", "Breath", "Heart", "Music", "UI_", "_UI", "GUI", "Menu", "Stinger", "Amb", "Wind",
            "Tinnitus", "Stomach", "Shiver", "Loop", "Rumble", "Aurora", "Weather", "Radio", "Trader", "Cutscene",
            // Звуки-эффекты на весь звук игры (замедление при выборе оружия в схватке, глушение, когда тебя рвут):
            // у другого они включались на всё и не выключались — «замедленный звук у всех».
            "Slow", "Muffle", "Snapshot", "TimeScale", "Filter", "Duck",
            // Звуки схватки сделаны «от первого лица»: без направления, со своими криками героя (по его полу).
            // У другого игрока они звучали как его собственная схватка и его голосом (0.18.4) — не отправляем.
            "Struggle",
        };

        private static float _windowStart;
        private static int _windowCount;
        private static uint _skipId;
        private static float _skipUntil, _shotSentAt = -10f;   // _shotSentAt: выстрел ушёл из OnPlay (звук игры)
        private static int _stepFrame = -1;
        private static int _sentLog, _heardLog, _shotLog, _reloadLog;

        private class Heard { public GameObject Go; public uint Playing; public float Until; public float Range; public int From = -1; }
        private static readonly List<Heard> Playing = new();
        private static GameObject _listener;
        private static AkAudioListener _listenerComp;

        private static bool Active => Session.Mode != Mode.Off && Session.InSharedWorld && GameApi.InGame();

        private static bool AnyoneHere()
        {
            string scene = GameApi.Scene();
            foreach (var p in Session.Players.Values) if (p.Id != Session.LocalId && Session.With(p, scene)) return true;
            return false;
        }

        private static bool Contains(string name, string[] list)
        {
            foreach (var s in list) if (name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static byte Classify(string name)
        {
            bool gun = Contains(name, new[] { "Rifle", "Revolver", "FlareGun", "Gun" });
            // «Shotgun» — это и «Shot», и «Gun»: зарядка дробовика (Play_FPH_Shotgun_Reload_Open/Close/Slug) считалась
            // выстрелом и была слышна на всю локацию (до 3000 м), как и щелчок пустого (Play_*DryFire). Выстрел — только «Fire»/«Shoot».
            string rest = name.Replace("Shotgun", "", StringComparison.OrdinalIgnoreCase);
            if (gun && Contains(rest, new[] { "Fire", "Shot", "Shoot" }) && !Contains(name, new[] { "Shell", "DryFire", "Reload" })) return RShot;
            if (Contains(name, new[] { "Chop", "Hatchet", "Axe", "Hammer", "Smash", "Breakdown", "BreakDown", "Door" })) return RMid;
            return RNear;
        }

        // Звук идёт от нас: наш объект или камера в цепочке родителей, либо дверь/ящик в 3 м.
        private static bool FromMe(GameObject go, string name)
        {
            if (go == null) return false;
            try
            {
                if (go.name.StartsWith("TLDCoop_")) return false;
                var player = GameManager.GetPlayerTransform();
                var cam = GameManager.GetMainCamera()?.transform;
                int depth = 0;
                for (var t = go.transform; t != null && depth < 20; t = t.parent, depth++)
                    if (t == player || (cam != null && t == cam)) return true;
                if (player != null && Contains(name, new[] { "Door", "Container", "Open", "Close", "Search" }))
                    return (go.transform.position - player.position).sqrMagnitude < 9f;
            }
            catch { }
            return false;
        }

        private static bool RateOk()
        {
            float now = Time.unscaledTime;
            if (now - _windowStart > 1f) { _windowStart = now; _windowCount = 0; }
            return ++_windowCount <= 12;
        }

        internal static void OnPlay(string name, uint id, GameObject go, uint playing = 0)
        {
            if (string.IsNullOrEmpty(name) || id == 0) return;
            if (playing != 0 && Session.Mode != Mode.Off && ForeignStruggleEffect(name, playing)) return;
            // «Звуки друзей» — что слышу я. Свои звуки шлём всегда: раньше с выключенной галочкой игрок и сам
            // никому не слал (выстрелы, двери, рубку), и друзья его не слышали (0.19.42).
            if (!Active) return;
            if (!name.StartsWith("Play", StringComparison.OrdinalIgnoreCase)) return;
            if (!Contains(name, Allow) || Contains(name, Deny) || StruggleEffect(name)) return;
            byte range = Classify(name);
            // Этот выстрел уже ушёл из GunItem.Fired (OnShot) — второй раз не шлём, даже под другим именем.
            if ((id == _skipId || range == RShot) && Time.unscaledTime < _skipUntil) return;
            if (!FromMe(go, name) || !AnyoneHere() || !RateOk()) return;
            if (range == RShot) _shotSentAt = Time.unscaledTime;
            SendEvent(id, range, name);
        }

        // Те же эффекты по именам из самой игры (PlayerStruggle): замедление при выборе оружия, выход из него, глушение.
        private static readonly HashSet<string> Effects = new(StringComparer.OrdinalIgnoreCase);
        private static bool _effectsRead;

        private static bool StruggleEffect(string name)
        {
            if (!_effectsRead)
            {
                try
                {
                    var ps = GameManager.GetPlayerStruggleComponent();
                    if (ps != null)
                    {
                        _effectsRead = true;
                        void Add(string n) { if (!string.IsNullOrEmpty(n)) Effects.Add(n); }
                        Add(ps.m_PlayMauledMuffleAudioId);
                        Add(ps.m_StopMauledMuffleAudioId);
                        var wp = ps.m_WeaponPicker;
                        if (wp != null) { Add(wp.m_WeaponSelectionSlowdownAudio); Add(wp.m_ExitAudio); }
                        MelonLogger.Msg("[звуки] эффекты схватки не уходят другим: " + string.Join(", ", Effects));
                    }
                }
                catch { _effectsRead = true; }
            }
            return Effects.Contains(name);
        }

        // Эффект схватки (замедление времени, глушение) включила МОЯ игра, а сам я ни с кем не дерусь: так бывает,
        // когда зверь, которого ведёт моя игра, бросается на друга (зверь «думает», что друг — это я). У меня этот
        // звук замедления не выключался. Глушим сразу и один раз пишем в лог, откуда он взялся.
        private static readonly HashSet<string> ForeignLogged = new(StringComparer.OrdinalIgnoreCase);
        private static int _foreignStopped;

        private static bool ForeignStruggleEffect(string name, uint playing)
        {
            if (!name.StartsWith("Play", StringComparison.OrdinalIgnoreCase)) return false;
            if (!StruggleEffect(name) && !Contains(name, SlowNames)) return false;
            if (LocalStruggle()) return false;
            try { AkSoundEngine.StopPlayingID(playing); } catch { }
            _foreignStopped++;
            if (ForeignLogged.Add(name))
                MelonLogger.Msg($"[звуки] моя игра включила «{name}», хотя у меня схватки нет — заглушил. Откуда: {Environment.StackTrace}");
            return true;
        }

        // Только эффекты схватки: PLAY_SPARKSSLOW (искры) и PLAY_STUNNEDMUFFLE (оглушение) — свои, их не трогаем.
        private static readonly string[] SlowNames = { "MauledMuffle", "WeaponPicker" };

        private static HashSet<uint> _effectIds;

        private static int _shotHeardLog;

        private static bool EffectId(uint id)
        {
            if (_effectIds == null)
            {
                var set = new HashSet<uint>();
                StruggleEffect("");   // прочитать имена из игры
                foreach (var n in Effects) { try { set.Add(AkSoundEngine.GetIDFromString(n)); } catch { } }
                foreach (var n in new[] { "Play_WeaponPickerEnter", "Play_MauledMuffle", "Play_Cougar_AI_MauledMuffle" })
                    try { set.Add(AkSoundEngine.GetIDFromString(n)); } catch { }
                if (!_effectsRead) return false;   // имена ещё не прочитаны — соберём в следующий раз
                _effectIds = set;
            }
            return _effectIds.Contains(id);
        }

        internal static bool IsStruggleEffectName(string name) =>
            !string.IsNullOrEmpty(name) && name.StartsWith("Play", StringComparison.OrdinalIgnoreCase) && (StruggleEffect(name) || Contains(name, SlowNames));

        private static bool LocalStruggle()
        {
            try
            {
                var ps = GameManager.GetPlayerStruggleComponent();
                return ps != null && (ps.m_Active || ps.InStruggle());
            }
            catch { return true; }   // не знаем — ничего не трогаем
        }

        // Схватка друга кончилась, а у меня своей нет: на всякий случай выключаем эффекты схватки моей игры
        // (если что-то успело включиться мимо проверки выше, звук замедления иначе тянулся бесконечно).
        private static bool _remoteStruggle;
        private static float _cleanAt = -1f, _holdAt;
        private static bool _cleanLogged;

        private static void WatchRemoteStruggle()
        {
            bool any = false;
            try { foreach (var pl in Session.Players.Values) if (pl?.Puppet != null && pl.Puppet.Struggling) { any = true; break; } } catch { }
            float now = Time.unscaledTime;
            if (_remoteStruggle && !any) _cleanAt = now + 0.5f;
            SoundDiag.Tick(any, any && LocalStruggle());
            // Пока дерётся друг, а я нет, — каждые полсекунды гасим у себя замедление и глушение, чем бы они ни включились.
            if (any && now >= _holdAt) { _holdAt = now + 0.5f; _cleanAt = now; }
            _remoteStruggle = any;
            if (_cleanAt < 0f || now < _cleanAt) return;
            _cleanAt = -1f;
            if (LocalStruggle()) return;
            try
            {
                var ps = GameManager.GetPlayerStruggleComponent();
                if (ps == null) return;
                try { ps.StopMauledMuffle(); } catch { }
                try { ps.StopWeaponSlowdownAudio(); } catch { }
                if (!_remoteStruggle || !_cleanLogged) { _cleanLogged = true; MelonLogger.Msg($"[звуки] {(_remoteStruggle ? "друг в схватке" : "схватка друга кончилась")}: выключил у себя эффекты схватки (заглушено раньше: {_foreignStopped})"); }
            }
            catch { }
        }

        private static void SendEvent(uint id, byte range, string name)
        {
            var w = Msg.New(MsgType.PlayerSound);
            w.Write(KEvent);
            w.Write((byte)Session.LocalId);
            w.Write(GameApi.Scene());
            w.Write(id);
            w.Write(range);
            w.Write(ReloadAct(name));   // дописано в конец: старые версии это просто не читают
            Session.SendGameEvent(Msg.Done(w));
            if (_sentLog++ < 10) MelonLogger.Msg($"[звуки] мой звук ушёл другим: {name}");
        }

        // Звук зарядки → что показать руками у фигуры (HeldItems.Held.Reload). Имена из игры:
        // Play_FPH_HuntingRifle_Reload_OpenBolt, Play_FPH_HuntingRifle_Reload_Cartridge_CloseBolt_ и подобные.
        private static byte ReloadAct(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOf("Reload", StringComparison.OrdinalIgnoreCase) < 0) return 0;
            byte a = 0;
            string rest = name.Replace("Reload", "", StringComparison.OrdinalIgnoreCase);   // «Load» внутри «Reload» не считается
            if (Contains(rest, new[] { "Open" })) a |= HeldItems.Held.ReloadOpen;
            if (Contains(rest, new[] { "Cartridge", "Bullet", "Round", "Shell", "Insert", "Load" })) a |= HeldItems.Held.ReloadRound;
            if (Contains(rest, new[] { "Close" })) a |= HeldItems.Held.ReloadClose;
            if (a == 0) a = HeldItems.Held.ReloadRound;
            if (_reloadLog++ < 6) MelonLogger.Msg($"[звуки] зарядка ушла другим: {name} → {a}");
            return a;
        }

        internal static void OnShot(GunItem gun)
        {
            if (gun == null || !Active || !AnyoneHere()) return;
            try
            {
                var type = gun.m_GunType;
                if (type == GunType.Camera) return;   // снимок фотоаппаратом — не выстрел
                float now = Time.unscaledTime;
                // Звук этого выстрела игра уже сыграла через PlaySound, и он ушёл другим (OnPlay).
                if (now - _shotSentAt < 0.3f) return;
                var ev = gun.m_FireAudio;
                uint id = ev != null ? ev.Id : 0u;
                string name = ev?.Name;
                if (id == 0)
                {
                    // В 2.59 у револьвера m_FireAudio пустой: шлём звук выстрела этого оружия по имени из игры.
                    id = type switch
                    {
                        GunType.Revolver => Il2CppAK.EVENTS.PLAY_REVOLVERFIRE,
                        GunType.Rifle => Il2CppAK.EVENTS.PLAY_RIFLEFIRE,
                        GunType.FlareGun => Il2CppAK.EVENTS.PLAY_FLAREGUNFIRE,
                        GunType.Shotgun => Il2CppAK.EVENTS.PLAY_SHOTGUNFIRE,
                        _ => 0u,
                    };
                    name = "Play_" + type + "Fire";
                }
                if (id == 0) { if (_shotLog++ < 3) MelonLogger.Msg("[звуки] у оружия нет звука выстрела (m_FireAudio), другим не ушёл"); return; }
                if (_shotLog++ < 5) MelonLogger.Msg($"[звуки] выстрел ушёл другим: {name}");
                _skipId = id;
                _skipUntil = now + 0.3f;
                SendEvent(id, RShot, name ?? "выстрел");
            }
            catch (Exception e) { MelonLogger.Warning("[звуки] выстрел: " + e.Message); }
        }

        internal static void OnStep(string tag)
        {
            if (Time.frameCount == _stepFrame || !Active || !AnyoneHere()) return;
            _stepFrame = Time.frameCount;
            var w = Msg.New(MsgType.PlayerSound);
            w.Write(KStep);
            w.Write((byte)Session.LocalId);
            w.Write(GameApi.Scene());
            w.Write(tag ?? "");
            Session.SendFast(Msg.Done(w));
        }

        // ---------- Чужие звуки ----------

        public static void Receive(BinaryReader r)
        {
            byte kind = r.ReadByte();
            int from = r.ReadByte();
            string scene = r.ReadString();
            uint id = 0;
            byte range = RNear;
            string tag = "";
            byte act = 0;
            if (kind == KEvent)
            {
                id = r.ReadUInt32(); range = r.ReadByte();
                if (r.BaseStream.Position < r.BaseStream.Length) act = r.ReadByte();
            }
            else tag = r.ReadString();
            if (from == Session.LocalId || !GameApi.InGame() || scene != GameApi.Scene()) return;
            if (!Session.Players.TryGetValue(from, out var pl) || !pl.Puppet.Alive) return;
            if (act != 0) { try { pl.Puppet.Reload(act); } catch (Exception e) { MelonLogger.Warning("[звуки] зарядка: " + e.Message); } }
            if (!Session.Sees(Shown.Sounds)) return;
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return;
            var me = new Vector3(x, y, z);
            if (kind == KStep) { FriendTracks.SetStepTag(pl.Id, tag); Step(pl, me, tag); return; }
            if (EffectId(id)) return;   // замедление/глушение схватки друга мне не нужно, даже если его старая версия прислала
            var src = pl.Puppet.SoundEmitter();
            if (src == null) return;
            float maxD = Ranges[Math.Min(range, (byte)2)];
            float d = Vector3.Distance(me, src.transform.position);
            if (d > maxD) return;
            var go = Proxy(pl.Id);
            if (go == null) return;
            try
            {
                Place(go, me, src.transform.position, d, maxD);
                // Голос в звуках действий (вздохи, кряхтение) выбирается по полу: ставим пол того, кто играет.
                if (pl.Puppet.Female.HasValue)
                    AkSoundEngine.SetSwitch("PlayerGender", pl.Puppet.Female.Value ? "Female" : "Male", go);
                uint playing;
                SoundDiag.Self = true;
                try { playing = AkSoundEngine.PostEvent(id, go); } finally { SoundDiag.Self = false; }
                if (range == RShot && _shotHeardLog++ < 6)
                    MelonLogger.Msg($"[звуки] выстрел {pl.Name} ({d:0} м): звук {id} {(playing != 0 ? "играет" : "НЕ ЗАИГРАЛ")}");
                if (playing == 0 && range == RShot)
                {
                    // 0.19.33: выстрел дробовика друга не был слышен. Если звук этого выстрела у меня не заиграл
                    // (нет банка, событие не того типа), пусть будет хотя бы выстрел ружья — тишины от выстрела быть не должно.
                    SoundDiag.Self = true;
                    try { playing = AkSoundEngine.PostEvent(Il2CppAK.EVENTS.PLAY_RIFLEFIRE, go); } finally { SoundDiag.Self = false; }
                }
                if (playing == 0) return;
                Playing.Add(new Heard { Go = go, Playing = playing, Until = Time.unscaledTime + 12f, Range = maxD, From = pl.Id });
                if (_heardLog++ < 10) MelonLogger.Msg($"[звуки] слышу звук {pl.Name} ({d:0} м)");
            }
            catch (Exception e) { MelonLogger.Warning("[звуки] " + e.Message); }
        }

        // Шаг друга. Звук шагов в игре сделан для своих ног и с расстоянием глохнет почти сразу, поэтому
        // источник ставим в ту же сторону, но в 4 раза ближе (не ближе 1 м), а громкость сами гасим к 30 м.
        private const float StepRange = 50f;

        private static void Step(Player pl, Vector3 me, string tag)
        {
            var go = pl.Puppet.StepEmitter();
            if (go == null) return;
            var feet = pl.Puppet.FeetPoint;
            float d = Vector3.Distance(me, feet);
            if (d > StepRange) return;
            var dir = d > 0.01f ? (feet - me) / d : Vector3.forward;
            try
            {
                float v = Mathf.Clamp01(1f - Mathf.Max(0f, d - 4f) / (StepRange - 4f));
                // Громкость не поставилась (нет слушателя) — шаг звучит там, где друг: затухание Wwise, а не в полную силу у уха.
                go.transform.position = BusVolume(go, v) ? me + dir * Mathf.Max(1f, d * 0.25f) + Vector3.up * 0.2f : feet + Vector3.up * 0.2f;
                if (tag.Length > 0) AkSoundEngine.SetSwitch("MaterialTag", tag, go);
                SoundDiag.Self = true;
                try { AkSoundEngine.PostEvent("Play_Footsteps", go); } finally { SoundDiag.Self = false; }
            }
            catch (Exception e) { MelonLogger.Warning("[звуки] шаг: " + e.Message); }
        }

        // Друг провалился под лёд: треск и всплеск там, где он провалился, и надпись на экране.
        public static void OnFriendFellThroughIce(Player pl)
        {
            if (!GameApi.InGame() || !Session.With(pl, GameApi.Scene())) return;
            GameApi.HudMessage(pl.Name + " провалился под лёд!");
            if (!Session.Sees(Shown.Sounds) || !pl.Puppet.Alive) return;
            var go = pl.Puppet.SoundEmitter();
            if (go == null || !GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return;
            var at = new Vector3(pl.X, pl.Y, pl.Z);
            float d = Vector3.Distance(new Vector3(x, y, z), at);
            const float range = 80f;
            if (d > range) return;
            try
            {
                SetVolume(go, d, range);
                uint playing = AkSoundEngine.PostEvent(Il2CppAK.EVENTS.PLAY_ICECRACKINGFALLINWATER, go);
                if (playing != 0) Playing.Add(new Heard { Go = go, Playing = playing, Until = Time.unscaledTime + 10f, Range = range });
            }
            catch (Exception e) { MelonLogger.Warning("[звуки] лёд: " + e.Message); }
        }

        public static void Tick()
        {
            if (Session.Mode != Mode.Off && GameApi.InGame()) WatchRemoteStruggle();
            if (Playing.Count == 0) return;
            if (!GameApi.InGame() || Session.Mode == Mode.Off) { StopAll(); return; }
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out _, out _)) return;
            var me = new Vector3(x, y, z);
            float now = Time.unscaledTime;
            for (int i = Playing.Count - 1; i >= 0; i--)
            {
                var h = Playing[i];
                if (h.Go == null) { Playing.RemoveAt(i); continue; }
                if (now > h.Until)
                {
                    try { AkSoundEngine.StopPlayingID(h.Playing); } catch { }
                    Playing.RemoveAt(i);
                    continue;
                }
                try
                {
                    if (h.From >= 0 && Session.Players.TryGetValue(h.From, out var pl) && pl.Puppet.Alive && pl.Puppet.SoundEmitter() is GameObject src)
                    {
                        Place(h.Go, me, src.transform.position, Vector3.Distance(me, src.transform.position), h.Range);
                    }
                    else if (h.From >= 0)
                    {
                        // Фигуры больше нет: друг ушёл в другую локацию (или мы). Заместитель не уничтожается со сценой,
                        // и его звук (горящий факел, фальшфейер, рубка) доигрывал до 12 с уже в другом месте — обрываем.
                        try { AkSoundEngine.StopPlayingID(h.Playing); } catch { }
                        Playing.RemoveAt(i);
                    }
                    else SetVolume(h.Go, Vector3.Distance(me, h.Go.transform.position), h.Range);
                }
                catch { }
            }
        }

        // Звуки игры сделаны для своего игрока и в Wwise глохнут уже метров через 20–30 (выстрел друга в 30 м
        // не было слышно). Поэтому чужой звук играет из «заместителя»: в ту же сторону, что и друг, но не дальше 4 м
        // от нас, а затухание с расстоянием делаем сами (SetVolume).
        // Громкость в Wwise задаётся на весь объект, поэтому у каждого звучащего звука свой заместитель
        // (до ProxySlots на игрока): иначе тихая зарядка и громкий выстрел перебивали громкость друг друга.
        private const int ProxySlots = 6;
        private static readonly Dictionary<int, GameObject> Proxies = new();

        private static GameObject Proxy(int id)
        {
            Heard oldest = null;
            for (int slot = 0; slot < ProxySlots; slot++)
            {
                int key = id * ProxySlots + slot;
                if (!Proxies.TryGetValue(key, out var go) || go == null)
                {
                    go = new GameObject($"TLDCoop_SoundProxy_{id}_{slot}");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    // Зарегистрировать в Wwise сразу: громкость ставится до первого PostEvent.
                    try { go.AddComponent<AkGameObj>(); } catch { }
                    Proxies[key] = go;
                    return go;
                }
                Heard busy = null;
                foreach (var h in Playing) if (h.Go == go && (busy == null || h.Until > busy.Until)) busy = h;
                if (busy == null) return go;
                if (oldest == null || busy.Until < oldest.Until) oldest = busy;
            }
            // Все заняты: самый старый звук обрываем, его заместитель берём.
            if (oldest == null) return null;
            Playing.RemoveAll(h => h.Go == oldest.Go && StopHeard(h));
            return oldest.Go;
        }

        private static bool StopHeard(Heard h)
        {
            try { AkSoundEngine.StopPlayingID(h.Playing); } catch { }
            return true;
        }

        private static void PlaceProxy(GameObject go, Vector3 me, Vector3 at)
        {
            var v = at - me;
            float d = v.magnitude;
            var dir = d > 0.01f ? v / d : Vector3.forward;
            go.transform.position = me + dir * Mathf.Clamp(d * 0.25f, Mathf.Min(d, 1f), 4f);
        }

        // Заместитель у самого уха годится, только если громкость по расстоянию поставилась: иначе звук друга
        // за 100 м (выстрел — за километр) звучал бы в полную силу в 4 м от нас. Тогда ставим его туда, где друг,
        // и звук глохнет с расстоянием сам (по затуханию Wwise, т. е. ближе, чем наши дальности).
        private static void Place(GameObject go, Vector3 me, Vector3 at, float d, float range)
        {
            if (SetVolume(go, d, range)) PlaceProxy(go, me, at);
            else go.transform.position = at;
        }

        private static bool SetVolume(GameObject go, float d, float range)
        {
            // Вблизи в полную силу, к краю слышимости тише до нуля.
            float near = Math.Min(3f, range * 0.1f);
            float v = Mathf.Clamp01(1f - Mathf.Max(0f, d - near) / (range - near));
            v *= v;
            return BusVolume(go, v);
        }

        private static int _volumeLog;

        private static bool BusVolume(GameObject go, float v)
        {
            var listener = Listener();
            if (listener == null)
            {
                if (_volumeLog++ < 3) MelonLogger.Warning("[звуки] не нашёл слушателя (AkAudioListener): звуки друга звучат с его места, без своей громкости");
                return false;
            }
            var res = AkSoundEngine.SetGameObjectOutputBusVolume(go, listener, v);
            if (res == AKRESULT.AK_Success) return true;
            if (_volumeLog++ < 3) MelonLogger.Warning($"[звуки] громкость звука друга не поставилась ({res}): звучит с его места");
            _listener = null;   // в следующий раз ищем слушателя заново
            return false;
        }

        // Слушатель, которому игра отдаёт звук: из слушателей по умолчанию (их слышат все источники), включённый.
        // Раньше брали первый попавшийся и запоминали навсегда: выключенный или не тот слушатель — громкость не действовала.
        internal static GameObject Listener()
        {
            try { if (_listener != null && _listenerComp != null && _listenerComp.isActiveAndEnabled) return _listener; } catch { }
            _listener = null;
            _listenerComp = null;
            try
            {
                var list = AkAudioListener.DefaultListeners?.ListenerList;
                if (list != null)
                    for (int i = 0; i < list.Count; i++)
                    {
                        var l = list[i];
                        if (l != null && l.isActiveAndEnabled) { _listenerComp = l; break; }
                    }
            }
            catch { }
            if (_listenerComp == null) _listenerComp = UnityEngine.Object.FindObjectOfType<AkAudioListener>();
            _listener = _listenerComp != null ? _listenerComp.gameObject : null;
            return _listener;
        }

        private static void StopAll()
        {
            foreach (var h in Playing) { try { AkSoundEngine.StopPlayingID(h.Playing); } catch { } }
            Playing.Clear();
        }

        public static void Reset()
        {
            StopAll();
            _listener = null;
            _listenerComp = null;
        }
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.PlaySound), new[] { typeof(string), typeof(GameObject) })]
    internal static class Patch_SoundString
    {
        private static void Postfix(string soundID, GameObject go, uint __result)
        {
            if (__result == 0) return;
            try { SoundSync.OnPlay(soundID, AkSoundEngine.GetIDFromString(soundID), go, __result); } catch { }
        }
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.PlaySound), new[] { typeof(Il2CppAK.Wwise.Event), typeof(GameObject) })]
    internal static class Patch_SoundEvent
    {
        private static void Postfix(Il2CppAK.Wwise.Event soundEvent, GameObject go, uint __result)
        {
            if (__result == 0 || soundEvent == null) return;
            try { SoundSync.OnPlay(soundEvent.Name, soundEvent.Id, go, __result); } catch { }
        }
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.Play3DSound), new[] { typeof(string), typeof(GameObject) })]
    internal static class Patch_Sound3DString
    {
        private static void Postfix(string soundID, GameObject go, uint __result)
        {
            if (__result == 0) return;
            try { SoundSync.OnPlay(soundID, AkSoundEngine.GetIDFromString(soundID), go, __result); } catch { }
        }
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.Play3DSound), new[] { typeof(Il2CppAK.Wwise.Event), typeof(GameObject) })]
    internal static class Patch_Sound3DEvent
    {
        private static void Postfix(Il2CppAK.Wwise.Event soundEvent, GameObject go, uint __result)
        {
            if (__result == 0 || soundEvent == null) return;
            try { SoundSync.OnPlay(soundEvent.Name, soundEvent.Id, go, __result); } catch { }
        }
    }

    [HarmonyPatch(typeof(GunItem), nameof(GunItem.Fired))]
    internal static class Patch_SoundGun
    {
        private static void Postfix(GunItem __instance) => SoundSync.OnShot(__instance);
    }

    [HarmonyPatch(typeof(FootStepSounds), nameof(FootStepSounds.PlayFootStepSound), new[] { typeof(Vector3), typeof(string), typeof(FootStepSounds.State) })]
    internal static class Patch_SoundStep
    {
        private static void Postfix(string tag) => SoundSync.OnStep(tag);
    }

    [HarmonyPatch(typeof(FootStepSounds), nameof(FootStepSounds.PlayFootStepSound), new[] { typeof(Vector3), typeof(string) })]
    internal static class Patch_SoundStep2
    {
        private static void Postfix(string tag) => SoundSync.OnStep(tag);
    }
}
