using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Trader;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Общее для всего мира, а не для одной локации.
    // Торговец по рации один на всех: кто бы ни поговорил с ним, ни заказал или ни сдал обмен, состояние
    // торговца уходит хосту, хост принимает его и рассылает остальным.
    // Полярное сияние ведёт хост: у друзей оно включается и гаснет вместе с ним.
    public static class GlobalSync
    {
        private static bool Shared => Session.Mode != Mode.Off && Session.InSharedWorld && GameApi.InGame();

        // ---------- Торговец ----------

        private static bool _traderDirty, _applyingTrader;
        private static float _traderDirtyAt;
        private static string _lastTrader = "";

        internal static void TraderChanged()
        {
            if (_applyingTrader || !Shared) return;
            if (!_traderDirty) _traderDirtyAt = Time.unscaledTime;
            _traderDirty = true;
        }

        private static string TraderJson()
        {
            var tm = GameManager.GetTraderManager();
            if (tm == null) return null;
            var st = tm.Serialize();
            return st == null ? null : FastJson.Serialize(st);
        }

        private static void TickTrader(float now)
        {
            if (!_traderDirty || now - _traderDirtyAt < 0.5f) return;
            _traderDirty = false;
            string json;
            try { json = TraderJson(); } catch (Exception e) { MelonLogger.Warning("[торговец] " + e.Message); return; }
            if (string.IsNullOrEmpty(json) || json == _lastTrader) return;
            _lastTrader = json;
            var w = Msg.New(MsgType.TraderState);
            w.Write(json);
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg($"[торговец] отправил состояние торговца ({json.Length / 1024} КБ)");
        }

        private static void ApplyTrader(string json)
        {
            if (!GameApi.InGame()) return;
            var tm = GameManager.GetTraderManager();
            if (tm == null || json == _lastTrader) return;
            _applyingTrader = true;
            try
            {
                tm.Deserialize(FastJson.Deserialize<TraderState>(json));
                _lastTrader = json;
                MelonLogger.Msg("[торговец] состояние торговца как у другого игрока");
            }
            catch (Exception e) { MelonLogger.Warning("[торговец] применить: " + e.Message); }
            finally { _applyingTrader = false; }
        }

        // ---------- Полярное сияние ----------

        private static float _nextAurora, _nextAuroraSlow;
        private static bool _auroraWas;

        private static void TickAurora(float now)
        {
            if (Session.Mode != Mode.Host || now < _nextAurora) return;
            var am = GameManager.GetAuroraManager();
            if (am == null) return;
            bool active = am.AuroraIsActive() || am.GetNormalizedAlpha() > 0.01f;
            // Пока сияние идёт — каждые 2 секунды (с часами хоста и силой сияния: лампы у всех загораются вместе,
            // Game/AuroraPower.cs), иначе раз в минуту (на случай, если у друга началось своё).
            _nextAurora = now + (active ? 2f : 1f);
            if (!active && !_auroraWas && now < _nextAuroraSlow) return;
            _auroraWas = active;
            _nextAuroraSlow = now + 60f;
            var w = Msg.New(MsgType.AuroraState);
            w.Write(am.Serialize() ?? "");
            w.Write(AuroraPower.Clock);
            w.Write(am.m_NormalizedActive);
            Session.SendFast(Msg.Done(w));
        }

        private static void ApplyAurora(string state, float strength)
        {
            if (Session.Mode != Mode.Client || !GameApi.InGame() || string.IsNullOrEmpty(state)) return;
            var am = GameManager.GetAuroraManager();
            if (am == null) return;
            bool was = am.AuroraIsActive();
            am.Deserialize(state);
            // Сила сияния как у хоста: от неё зависит, какие лампы горят.
            if (strength >= 0f && Math.Abs(am.m_NormalizedActive - strength) > 0.002f) am.m_NormalizedActive = strength;
            if (was != am.AuroraIsActive()) MelonLogger.Msg("[мир] полярное сияние " + (am.AuroraIsActive() ? "началось" : "кончилось") + " вместе с хостом");
        }

        // ---------- Сеть ----------

        public static void Receive(MsgType type, BinaryReader r)
        {
            string s = r.ReadString();
            double hostClock = 0.0;
            float strength = -1f;
            if (type == MsgType.AuroraState) { hostClock = r.ReadDouble(); strength = r.ReadSingle(); }
            if (Session.InOwnWorld) return;   // друг в своём сохранении: торговец и сияние хоста к нему не относятся
            try
            {
                if (type == MsgType.TraderState) ApplyTrader(s);
                else if (type == MsgType.AuroraState) { AuroraPower.OnHostClock(hostClock); ApplyAurora(s, strength); }
            }
            catch (Exception e) { MelonLogger.Warning("[мир] " + type + ": " + e.Message); }
        }

        public static void Tick(float now)
        {
            if (!Shared) return;
            try
            {
                TickTrader(now);
                TickAurora(now);
                AuroraPower.Tick(now);
            }
            catch (Exception e) { MelonLogger.Warning("[мир] " + e.Message); }
        }

        public static void Reset()
        {
            _traderDirty = false;
            _lastTrader = "";
            _nextAurora = 0f;
            _nextAuroraSlow = 0f;
            _auroraWas = false;
            AuroraPower.Reset();
        }
    }

    // Всё, что меняет торговца: разговор, заказ, сдача и отмена обмена, доверие, открытия.
    [HarmonyPatch]
    internal static class Patch_TraderChanged
    {
        private static readonly string[] Names =
        {
            nameof(TraderManager.ContactTrader), nameof(TraderManager.NotifyConversationHad), nameof(TraderManager.InitiateTrade),
            nameof(TraderManager.CloseTrade), nameof(TraderManager.CompleteTrade), nameof(TraderManager.RemoveActiveTrade),
            nameof(TraderManager.CancelTrade), nameof(TraderManager.AddTrust), nameof(TraderManager.UnlockExchange),
            nameof(TraderManager.UnlockImprovement), nameof(TraderManager.UnlockConversation), nameof(TraderManager.SetSpecialRequest),
            nameof(TraderManager.RollExchanges), nameof(TraderManager.AddTradeExchange), nameof(TraderManager.SetHasInterruptedPlayerPassingTime),
        };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var n in Names)
            {
                var m = AccessTools.Method(typeof(TraderManager), n);
                if (m != null) yield return m;
            }
        }

        private static void Postfix() => GlobalSync.TraderChanged();
    }
}
