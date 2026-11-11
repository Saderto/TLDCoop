using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.IntBackedUnit;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // То, что раньше расходилось молча (раздел 2 разбора 0.11.17):
    //  - уголь, взятый из костра: у остальных он оставался, и его можно было взять ещё раз;
    //  - костёр, задутый ветром: каждая игра решала сама. Теперь решает только хозяин локации (Game/AnimalSync.cs);
    //  - костёр, догоревший сам: хозяин локации раз в минуту рассылает, сколько горят его костры;
    //  - замки, открытые ключом или кодом, и сейфы, вскрытые подбором шифра: ловили только лом;
    //  - обогреватель на ворвани: горит ли и сколько в нём топлива;
    //  - лифт с воротом: на каком этаже кабина (раньше у каждого стояла там, где он сам её оставил).
    // Всё это — опрос раз в полсекунды: что поменялось у нас (а не пришло по сети), то и рассылаем.
    public static class WorldStates
    {
        private static string _scene = "";
        private static float _nextScan, _nextPoll, _nextClock, _nextChimney;
        private static int _scanStep;
        private static bool _scanAll;
        private static readonly List<Fire> Fires = new();
        private static readonly List<Lock> Locks = new();
        private static readonly List<SafeCracking> Safes = new();
        private static readonly List<WhaleOilHeaterItem> Heaters = new();
        private static readonly List<Elevator> Elevators = new();
        private static readonly List<WaterSource> Waters = new();
        // Кто сейчас в локации с нами (по id игрока) и когда разослать им воду в бачках.
        private static readonly HashSet<int> Here = new();
        private static float _waterSendAt = -1f, _tieSendAt = -1f;
        private static bool _herePrimed;
        private static int _waters;
        // Последнее, что мы видели у объекта (по id). Пришедшее по сети тоже записываем сюда — тогда не отправим его обратно.
        private static readonly Dictionary<string, string> Seen = new();
        private static readonly Dictionary<string, long> Fuel = new();
        private static int _charcoal, _locks, _safes, _heaters, _windBlocked, _clocks, _elevators;

        private static bool Active => Session.Mode != Mode.Off && Session.InSharedWorld && GameApi.InGame() && ItemSync.Settled;

        private static bool OthersHere()
        {
            string scene = GameApi.Scene();
            float now = Time.unscaledTime;
            foreach (var p in Session.Players.Values)
                if (p.Id != Session.LocalId && Session.With(p, scene) && now - p.LastState < 5f) return true;
            return false;
        }

        // Решает за локацию тот же, кто ведёт её зверей без места появления: первый пришедший (Game/AnimalSync.cs).
        internal static bool Decider => !OthersHere() || AnimalSync.IsAuthority;
        internal static bool OthersNear => OthersHere();

        public static void Tick(float now)
        {
            if (!Active) { _scene = ""; return; }
            string scene = ItemSync.SceneKey();
            if (scene != _scene)
            {
                _scene = scene;
                Seen.Clear();
                Fuel.Clear();
                StartingSince.Clear();
                Here.Clear();
                _waterSendAt = -1f;
                _tieSendAt = -1f;
                _herePrimed = false;
                _nextScan = 0f;
                _scanAll = true;
                _nextPoll = now + 3f;   // пусть сначала применятся отложенные состояния локации
            }
            if (now >= _nextScan) { _nextScan = now + 0.8f; Scan(_scanAll); _scanAll = false; }
            if (now >= _nextPoll) { _nextPoll = now + 0.5f; Poll(); WatchArrivals(now); }
            if (now >= _nextClock) { _nextClock = now + 60f; if (Decider && OthersHere()) SendFireClocks(); }
            if (now >= _nextChimney) { _nextChimney = now + 30f; SendChimneys(); }
            WorldEvents.TickChimneys();
        }

        // По одному виду за раз по кругу (каждый раз в ~5 с): все шесть поисков в одном кадре давали рывок 60-80 мс.
        // Сразу после входа в локацию — все.
        private static void Scan(bool all)
        {
            for (int i = 0; i < (all ? 6 : 1); i++)
            {
                switch (_scanStep)
                {
                    case 0: Fill(Fires); break;
                    case 1: Fill(Locks); break;
                    case 2: Fill(Safes); break;
                    case 3: Fill(Heaters); break;
                    case 4: Fill(Elevators); break;
                    default: Fill(Waters); break;
                }
                _scanStep = (_scanStep + 1) % 6;
            }
        }

        private static void Fill<T>(List<T> list) where T : Component
        {
            list.Clear();
            try { foreach (var c in UnityEngine.Object.FindObjectsOfType<T>()) if (c != null) list.Add(c); } catch { }
        }

        private static bool Changed(string key, string now, out bool first)
        {
            first = !Seen.TryGetValue(key, out var was);
            if (!first && was == now) return false;
            Seen[key] = now;
            return true;
        }

        internal static void Note(string prefix, Component c, string state)
        {
            if (c == null) return;
            try { Seen[prefix + WorldObjects.IdOf(c)] = state; } catch { }
        }

        // Костёр, застрявший на стадии розжига. Стадию «горит» (FullBurn) игра ставит только тому, кто разжигал
        // (Fire.ExitFireStarting) или костру, зажжённому заранее (StartFireLit); UpdateFireStage доводит розжиг лишь до
        // Starting_FullBurn. У остальных костёр, пришедший во время розжига, так и оставался «разжигается»: тепло шло,
        // а котелок на нём проверяет именно FullBurn (CookingPotItem.AttachedFireIsBurning) и стоял на «Паузе»
        // (друзья, 0.19.36: рыба на костре друга). Держится на розжиге 20 с и сам я его не разжигаю — ставим «горит».
        private static readonly Dictionary<IntPtr, float> StartingSince = new();
        private static int _stuckFires;
        internal static int StuckFires => _stuckFires;

        private static void FixStuckStart(Fire f, float now)
        {
            int st = (int)f.m_FireState;
            if (st < 1 || st > 5) { StartingSince.Remove(f.Pointer); return; }
            if (!StartingSince.TryGetValue(f.Pointer, out float since)) { StartingSince[f.Pointer] = now; return; }
            if (now - since < 20f) return;
            try
            {
                var me = GameManager.GetPlayerTransform();
                if (Fire.m_IsStartingFireUIInProgress && me != null && Vector3.Distance(me.position, f.transform.position) < 6f) { StartingSince[f.Pointer] = now; return; }
            }
            catch { }
            StartingSince.Remove(f.Pointer);
            f.FireStateSet(FireState.FullBurn);
            try { var hs = f.m_HeatSource; if (hs != null && !hs.m_TurnedOn) hs.TurnOn(); } catch { }
            _stuckFires++;
            MelonLogger.Msg($"[костёр] {WorldObjects.IdOf(f)}: {now - since:0} с стоял на стадии розжига {(FireState)st} — поставил «горит» (иначе котелки на нём на «Паузе»)");
        }

        private static void Poll()
        {
            float pollNow = Time.unscaledTime;
            foreach (var f in Fires) { if (f == null) continue; try { FixStuckStart(f, pollNow); } catch { } }
            // Костры и печки рассылаем и тогда, когда в локации больше никого: изменение ляжет в отложенные
            // и дойдёт до того, кто придёт позже (раньше хост один брал уголь, а друг потом брал его второй раз).
            foreach (var f in Fires)
            {
                if (f == null) continue;
                try
                {
                    // Уголь: взяли у нас — рассылаем костёр.
                    string sig = FireSig(f);
                    string key = "F" + WorldObjects.IdOf(f);
                    Seen.TryGetValue(key, out var was);
                    if (!Changed(key, sig, out bool first) || first) continue;
                    if (CharcoalTaken(was, sig))
                    {
                        WorldObjects.Send(WorldObjects.Kind.Fire, f, WorldObjects.FireState(f));
                        _charcoal++;
                        MelonLogger.Msg($"[объекты] взял уголь из костра {key.Substring(1)}, отправил");
                    }
                    else if (WasBurning(was) && !f.IsBurning() && Decider)
                    {
                        // Догорел или задут ветром у того, кто решает за локацию: гаснет у всех.
                        WorldObjects.Send(WorldObjects.Kind.Fire, f, WorldObjects.FireState(f));
                    }
                }
                catch { }
            }
            foreach (var lk in Locks)
            {
                if (lk == null) continue;
                try
                {
                    string st = ((int)lk.m_LockState).ToString();
                    if (!Changed("L" + WorldObjects.IdOf(lk), st, out bool first) || first) continue;
                    if (lk.m_LockState == LockState.Locked) continue;
                    // Открыт ключом, кодом, вместе с соседним замком — для всех так же, как лом.
                    WorldExtra.OnLockForced(lk);
                    _locks++;
                    MelonLogger.Msg($"[объекты] замок {WorldObjects.IdOf(lk)} открыт у меня (не ломом), отправил");
                }
                catch { }
            }
            foreach (var sf in Safes)
            {
                if (sf == null) continue;
                try
                {
                    string st = sf.m_Cracked ? "1" : "0";
                    if (!Changed("S" + WorldObjects.IdOf(sf), st, out bool first) || first || !sf.m_Cracked) continue;
                    WorldObjects.Send(WorldObjects.Kind.Safe, sf, sf.Serialize() ?? "");
                    _safes++;
                    MelonLogger.Msg($"[объекты] сейф {WorldObjects.IdOf(sf)} вскрыт у меня, отправил");
                }
                catch { }
            }
            foreach (var h in Heaters)
            {
                if (h == null) continue;
                try
                {
                    // Рассылаем, когда включили, выключили, сменили режим или долили топливо (расход каждый считает сам).
                    string id = WorldObjects.IdOf(h);
                    var hs = h.m_State;
                    if (hs == null) continue;
                    string sig = $"{(hs.m_IsOn ? 1 : 0)}|{hs.m_BurnRate}";
                    long fuel = hs.m_Fuel.m_Units;
                    bool refuel = Fuel.TryGetValue(id, out long was) && fuel > was + ItemLiquidVolume.Centiliter.m_Units * 5;
                    Fuel[id] = fuel;
                    if (!(Changed("H" + id, sig, out bool first) || refuel) || first) continue;
                    WorldObjects.Send(WorldObjects.Kind.Heater, h, HeaterState(h));
                    _heaters++;
                }
                catch { }
            }
            PollWaters();
            foreach (var e in Elevators)
            {
                if (e == null) continue;
                try
                {
                    // Шлём, когда кабина встала пустой (игрок вышел) на другом этаже или в другом месте шахты.
                    if (!ElevatorFree(e)) continue;
                    if (!Changed("E" + WorldObjects.IdOf(e), ElevatorSig(e), out bool first) || first) continue;
                    WorldObjects.Send(WorldObjects.Kind.Elevator, e, e.Serialize() ?? "");
                    _elevators++;
                    MelonLogger.Msg($"[объекты] лифт {WorldObjects.IdOf(e)}: кабина на этаже {e.m_LastFloorIndex}, отправил");
                }
                catch { }
            }
        }

        // ---------- Вода в бачках и бочках (Game/WaterTanks.cs) ----------
        // Сколько воды в бачке, игра каждого решала сама (случайно при первом заходе, по своему сохранению), а по сети
        // уходило только «набрал/выпил». Теперь тот, кто решает за локацию, рассылает воду во всех бачках, когда
        // к нему в локацию приходит другой игрок (через 3 с, когда тот загрузился), и когда вода у него меняется сама.

        internal static string WaterSig(WaterSource ws) => ws.m_CurrentLiters.m_Units.ToString(CultureInfo.InvariantCulture);

        private static void WatchArrivals(float now)
        {
            string scene = GameApi.Scene();
            float t = Time.unscaledTime;
            int lowest = int.MaxValue;
            foreach (var p in Session.Players.Values)
            {
                if (p.Id == Session.LocalId) continue;
                bool here = Session.With(p, scene) && t - p.LastState < 5f;
                // Шлёт тот, кто был в локации раньше пришедшего. Кого застали при своём приходе — те были раньше нас,
                // им своё не шлём: у пришедшего вода из своего сохранения или случайная.
                if (here && Here.Add(p.Id)) { if (_herePrimed) _waterSendAt = now + 3f; else lowest = Math.Min(lowest, p.Id); }
                else if (!here) Here.Remove(p.Id);
            }
            // Пришли почти вместе: каждый застал другого и не шлёт никто. Тогда через 3 с шлёт игрок с наименьшим
            // номером. Если пришёл поздно и вода от бывших тут уже пришла, он отошлёт её же — это не вредит.
            if (!_herePrimed && lowest != int.MaxValue && Session.LocalId < lowest) _tieSendAt = now + 3f;
            _herePrimed = true;
            if ((_waterSendAt > 0f && now >= _waterSendAt) || (_tieSendAt > 0f && now >= _tieSendAt))
            {
                _waterSendAt = -1f;
                _tieSendAt = -1f;
                if (OthersHere()) SendWaters();
            }
        }

        private static void SendWaters()
        {
            int n = 0;
            foreach (var ws in Waters)
            {
                if (ws == null) continue;
                try
                {
                    string st = ws.Serialize();
                    if (string.IsNullOrEmpty(st)) continue;
                    WorldObjects.Send(WorldObjects.Kind.Water, ws, st);
                    Seen["W" + WorldObjects.IdOf(ws)] = WaterSig(ws);
                    n++;
                }
                catch { }
            }
            _waters += n;
            if (n > 0) MelonLogger.Msg($"[объекты] вода: разослал, сколько её в бачках локации ({n})");
        }

        private static void PollWaters()
        {
            foreach (var ws in Waters)
            {
                if (ws == null) continue;
                try
                {
                    // Своё «набрал» уходит само (Patch_WaterTaken); здесь — то, что поменялось без игрока (дождь, время),
                    // и только у того, кто решает за локацию.
                    if (!Changed("W" + WorldObjects.IdOf(ws), WaterSig(ws), out bool first) || first || !Decider || !OthersHere()) continue;
                    string st = ws.Serialize();
                    if (!string.IsNullOrEmpty(st)) { WorldObjects.Send(WorldObjects.Kind.Water, ws, st); _waters++; }
                }
                catch { }
            }
        }

        // ---------- Дым из трубы (Game/WorldEvents.cs) ----------

        private static readonly Dictionary<string, (float minutes, float at)> ChimneySent = new();

        private static void SendChimneys()
        {
            if (Session.Players.Count < 2) return;
            float now = Time.unscaledTime;
            foreach (var f in Fires)
            {
                if (f == null) continue;
                try
                {
                    string guid = f.m_ChimneyGuid;
                    if (!f.m_ChimneyExpected || string.IsNullOrEmpty(guid) || !f.IsBurning()) continue;
                    float minutes = f.GetRemainingLifeTimeSeconds() / 60f;
                    // Заново — если подкинули дров (на 10+ мин больше) или раз в 5 минут.
                    if (ChimneySent.TryGetValue(guid, out var was) && minutes < was.minutes + 10f && now - was.at < 300f) continue;
                    ChimneySent[guid] = (minutes, now);
                    WorldEvents.SendChimney(guid, minutes);
                }
                catch { }
            }
        }

        // ---------- Лифт ----------

        // Кабина свободна: в ней никого (состояние WaitingForPlayer). Пока в ней едет свой игрок, чужое не применяем.
        private static bool ElevatorFree(Elevator e) => (int)e.m_StateInternal == 0;

        private static string ElevatorSig(Elevator e)
        {
            var p = e.transform.position;
            if (e.m_AnimatedObject != null) p = e.m_AnimatedObject.position;
            return $"{e.m_LastFloorIndex}|{Mathf.RoundToInt(p.y * 10f)}";
        }

        internal static Component ApplyElevator(string id, string state)
        {
            var e = WorldObjects.Find<Elevator>(id);
            if (e == null || string.IsNullOrEmpty(state)) return e;
            try
            {
                if (!ElevatorFree(e)) { MelonLogger.Msg($"[объекты] лифт {id}: я сейчас в кабине, чужой этаж не ставлю"); return e; }
                e.Deserialize(state);
                Note("E", e, ElevatorSig(e));
                MelonLogger.Msg($"[объекты] лифт {id}: кабина на этаже {e.m_LastFloorIndex}, как у другого");
            }
            catch (Exception ex) { MelonLogger.Warning("[объекты] лифт: " + ex.Message); }
            return e;
        }

        // ---------- Костры ----------

        private static string FireSig(Fire f)
        {
            int burning = f.IsBurning() ? 1 : 0;
            return $"{burning}|{(f.m_HasCollectedCharcoal ? 1 : 0)}|{f.m_NumGeneratedCharcoalPieces}";
        }

        private static bool WasBurning(string sig) => sig != null && sig.StartsWith("1|");

        private static bool CharcoalTaken(string was, string now)
        {
            if (was == null) return false;
            var a = was.Split('|');
            var b = now.Split('|');
            if (a.Length < 3 || b.Length < 3) return false;
            if (a[1] == "0" && b[1] == "1") return true;
            return int.TryParse(a[2], out int na) && int.TryParse(b[2], out int nb) && nb < na;
        }

        // Хозяин локации: сколько горят его костры. У остальных костёр догорал в другое время (кто спал отдельно,
        // кто был в другой локации). Горит у обоих — подгоняем таймер; у кого-то иначе — полное состояние.
        private static void SendFireClocks()
        {
            var ic = CultureInfo.InvariantCulture;
            foreach (var f in Fires)
            {
                if (f == null) continue;
                try
                {
                    if (!f.IsBurning()) continue;
                    string st = string.Join("\u0001", "C", f.m_ElapsedOnTODSeconds.ToString("R", ic), f.m_MaxOnTODSeconds.ToString("R", ic), WorldObjects.FireState(f));
                    WorldObjects.Send(WorldObjects.Kind.FireClock, f, st);
                    _clocks++;
                }
                catch { }
            }
        }

        internal static Component ApplyFireClock(string id, string state)
        {
            var f = WorldObjects.Find<Fire>(id);
            var p = state.Split('\u0001');
            if (p.Length < 4 || p[0] != "C") return f;
            var ic = CultureInfo.InvariantCulture;
            if (f == null || !f.IsBurning())
            {
                // У нас не горит (или костра нет): полное состояние, как при разжигании.
                string full = string.Join("\u0001", p, 3, p.Length - 3);
                WorldObjects.ApplyFire(id, full);
                return WorldObjects.Find<Fire>(id);
            }
            if (float.TryParse(p[1], NumberStyles.Float, ic, out float el) && float.TryParse(p[2], NumberStyles.Float, ic, out float max))
            {
                float was = (f.m_MaxOnTODSeconds - f.m_ElapsedOnTODSeconds) / 60f, now = (max - el) / 60f;
                if (Mathf.Abs(now - was) >= 1f) MelonLogger.Msg($"[костёр] таймер хозяина локации: у меня оставалось {was:0.#} мин, у него {now:0.#} мин");
                f.m_ElapsedOnTODSeconds = el;
                f.m_MaxOnTODSeconds = max;
            }
            return f;
        }

        // ---------- Сейфы ----------

        internal static Component ApplySafe(string id, string state)
        {
            var sf = WorldObjects.Find<SafeCracking>(id);
            if (sf == null) return null;
            try { if (!sf.m_Cracked && state.Length > 0) sf.Deserialize(state); } catch (Exception e) { MelonLogger.Warning("[объекты] сейф: " + e.Message); }
            try { Note("S", sf, sf.m_Cracked ? "1" : "0"); } catch { }
            return sf;
        }

        // ---------- Обогреватель на ворвани ----------

        private static string HeaterState(WhaleOilHeaterItem h)
        {
            var s = h.m_State;
            if (s == null) return "";
            return $"{(s.m_IsOn ? 1 : 0)}|{s.m_BurnRate}|0|{s.m_Fuel.m_Units}";
        }

        internal static Component ApplyHeater(string id, string state)
        {
            var h = WorldObjects.Find<WhaleOilHeaterItem>(id);
            if (h == null) return null;
            var p = state.Split('|');
            if (p.Length < 4) return h;
            try
            {
                bool on = p[0] == "1";
                int.TryParse(p[1], out int rate);
                long.TryParse(p[3], out long units);
                var s = h.m_State;
                if (s != null)
                {
                    s.m_Fuel = new ItemLiquidVolume(units);
                    if (s.m_BurnRate != rate) h.SetBurnRate(rate);
                    if (s.m_IsOn != on) { if (on) h.TurnOn(); else h.TurnOff(); }
                }
                var ns = h.m_State;
                if (ns != null) { string hid = WorldObjects.IdOf(h); Note("H", h, $"{(ns.m_IsOn ? 1 : 0)}|{ns.m_BurnRate}"); Fuel[hid] = ns.m_Fuel.m_Units; }
            }
            catch (Exception e) { MelonLogger.Warning("[объекты] обогреватель: " + e.Message); }
            return h;
        }

        // ---------- Ветер ----------

        internal static bool AllowWind(Fire f)
        {
            if (!Active || Decider) return true;
            if (_windBlocked++ < 5) MelonLogger.Msg("[объекты] задуть ли костёр ветром, решает хозяин локации");
            return false;
        }

        public static string Stats => $"уголь {_charcoal}, замки без лома {_locks}, сейфы {_safes}, обогреватели {_heaters}, таймеры костров {_clocks}, лифты {_elevators}";

        public static void Reset()
        {
            _scene = "";
            Seen.Clear();
            Fuel.Clear();
            Fires.Clear(); Locks.Clear(); Safes.Clear(); Heaters.Clear(); Elevators.Clear();
        }
    }

    [HarmonyPatch(typeof(Fire), nameof(Fire.MaybeBlowOutFromWind))]
    internal static class Patch_FireWind
    {
        private static bool Prefix(Fire __instance) => WorldStates.AllowWind(__instance);
    }

    // Пришедшее по сети запоминаем как уже виденное, чтобы опрос не отправил его обратно.
    [HarmonyPatch(typeof(Lock), nameof(Lock.Deserialize))]
    internal static class Patch_LockDeserializeSeen
    {
        private static void Postfix(Lock __instance) { if (WorldObjects.Applying) WorldStates.Note("L", __instance, ((int)__instance.m_LockState).ToString()); }
    }

    [HarmonyPatch(typeof(Fire), nameof(Fire.Deserialize))]
    internal static class Patch_FireDeserializeSeen
    {
        private static void Postfix(Fire __instance)
        {
            if (!WorldObjects.Applying) return;
            try { WorldStates.Note("F", __instance, $"{(__instance.IsBurning() ? 1 : 0)}|{(__instance.m_HasCollectedCharcoal ? 1 : 0)}|{__instance.m_NumGeneratedCharcoalPieces}"); } catch { }
        }
    }
}
