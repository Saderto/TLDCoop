using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using UnityEngine;

namespace CoopByEMPEROR.Net
{
    // Номера событий мира в сторону «друг → хост» — зеркало EventLog. Обрыв мод замечает только через 20 с тишины,
    // и всё, что друг сделал за эти секунды (поднял, обыскал, разобрал), могло пропасть вместе с закрытой сессией Steam:
    // друг взял вещь, а у хоста она лежит. Друг нумерует свои события, хост помнит, до какого номера получил всё подряд.
    //
    // Ничего не пропускаем (с 0.17.19):
    // - хост раз в секунду подтверждает «получил всё до N», и друг стирает из своего списка только подтверждённое
    //   (раньше держал последние 2000 и стирал старые, не зная, дошли ли они);
    // - пришло 105, а 104 нет: хост откладывает 105 и дальше и каждые 2 с просит «дошли с 104», а когда 104 пришло,
    //   применяет 104, 105, 106 по порядку (раньше писал «пропущены» и шёл дальше, 104 пропадало навсегда);
    // - пропуск возможен только если у друга события уже нет (список переполнился, 50 000) — тогда друг так и отвечает,
    //   и хост пишет это в лог;
    // - после вылета игры друга не досылаем: журнал у новой игры другой, а рюкзак откатился вместе с сохранением.
    //
    // Сообщение CSeq: [номер журнала друга][номер события][событие].
    // SeqCtl: [вид][номер журнала][номер][номер] — подтверждения и просьбы в обе стороны (виды ниже).
    public static class ClientLog
    {
        public const byte KAck = 0, KAsk = 1, KGone = 2, KHostAck = 3, KReload = 4;
        private const int Keep = 50000;
        public static readonly int LogId = new System.Random().Next(1, int.MaxValue);
        private static uint _seq, _acked;
        private static readonly LinkedList<(uint seq, byte[] data)> Sent = new();
        private static int _resent, _lost;
        private static float _nextFullWarn;

        // Друг: обернуть своё событие мира номером и запомнить до подтверждения.
        public static byte[] Wrap(byte[] data)
        {
            uint seq = ++_seq;
            var w = Msg.New(MsgType.CSeq);
            w.Write(LogId);
            w.Write(seq);
            w.Write(data);
            var full = Msg.Done(w);
            Sent.AddLast((seq, full));
            while (Sent.Count > Keep)
            {
                Sent.RemoveFirst();
                _lost++;
                if (Time.unscaledTime >= _nextFullWarn)
                {
                    _nextFullWarn = Time.unscaledTime + 30f;
                    MelonLogger.Warning($"[сеть] хост давно не подтверждает мои события: в списке {Keep}, самые старые стираю");
                }
            }
            return full;
        }

        // Друг: хост в приветствии сказал, до какого нашего номера всё получил, — досылаем остальное.
        public static void Resend(uint hostHas, Action<byte[]> send)
        {
            Acked(hostHas);
            int n = 0;
            foreach (var (seq, data) in Sent)
                if (seq > hostHas) { send(data); n++; }
            _resent += n;
            if (n > 0) MelonLogger.Msg($"[сеть] дослал хосту свои события после №{hostHas}: {n}");
        }

        private static void Acked(uint hostHas)
        {
            if (hostHas > _acked) _acked = hostHas;
            while (Sent.Count > 0 && Sent.First.Value.seq <= hostHas) Sent.RemoveFirst();
        }

        // Друг: подтверждение или просьба от хоста.
        public static void ClientControl(byte kind, BinaryReader r, Action<byte[]> send)
        {
            int log = r.ReadInt32();
            uint a = r.ReadUInt32(), b = r.ReadUInt32();
            if (log != LogId) return;
            if (kind == KAck) { Acked(a); return; }
            if (kind != KAsk) return;
            // Хост просит дослать a..b (у него дыра в счёте).
            if (a > 0) Acked(a - 1);
            uint oldest = Sent.Count > 0 ? Sent.First.Value.seq : _seq + 1;
            if (a < oldest)
            {
                // Этих событий у нас уже нет: говорим хосту, с какого номера ждать.
                var w = Msg.New(MsgType.SeqCtl);
                w.Write(KGone); w.Write(LogId); w.Write(oldest); w.Write(0u);
                send(Msg.Done(w));
                MelonLogger.Warning($"[сеть] хост просит мои события {a}–{b}, а у меня есть только с №{oldest}");
            }
            int n = 0;
            foreach (var (seq, data) in Sent)
            {
                if (seq < a) continue;
                if (seq > b) break;
                send(data);
                n++;
            }
            _resent += n;
            if (n > 0) MelonLogger.Msg($"[сеть] хост попросил мои события {a}–{b}, дослал {n}");
        }

        public static void Reset() { Sent.Clear(); _acked = 0; }

        // Сколько моих событий хост ещё не подтвердил (Game/SyncCheck.cs).
        public static int Unacked => Sent.Count;

        // ---------- Хост ----------

        private class From
        {
            public int Log;
            public uint Last, LastAcked;
            public readonly SortedDictionary<uint, byte[]> Held = new();
            public float HeldSince, NextAsk, NextAck, NextWaitLog;
        }

        private static readonly Dictionary<string, From> Got = new();
        private static int _dups, _asked, _gone;

        // Сколько мы получили от этого друга подряд (для Welcome). Журнал другой (друг перезапустил игру) — ноль.
        public static uint HostHas(string key, int theirLog) =>
            Got.TryGetValue(key, out var g) && g.Log == theirLog ? g.Last : 0;

        // Хост: событие друга с номером. apply получает события строго по порядку.
        public static void HostReceive(Peer peer, string key, int log, uint seq, byte[] inner, Action<Peer, byte[]> apply)
        {
            bool known = Got.TryGetValue(key, out var g);
            if (!known || g.Log != log)
            {
                // Первое событие этого журнала: друг впервые у нас, или его игра перезапустилась (после вылета
                // старое не ждём — его рюкзак откатился к сохранению), или перезапустились мы.
                if (g != null && g.Held.Count > 0) MelonLogger.Msg($"[сеть] {key}: новая игра у друга, его отложенные события ({g.Held.Count}) от прошлой не применяю");
                // Друг перезапустил игру — его счёт начинается с 1, ждём с 1. Мы перезапустились — берём с первого пришедшего.
                Got[key] = g = new From { Log = log, Last = known ? 0 : seq - 1 };
            }
            if (seq <= g.Last || g.Held.ContainsKey(seq)) { _dups++; return; }
            if (seq != g.Last + 1)
            {
                if (g.Held.Count == 0) { g.HeldSince = Time.unscaledTime; g.NextAsk = 0f; }
                g.Held[seq] = inner ?? Array.Empty<byte>();
                Ask(peer, key, g);
                return;
            }
            g.Last = seq;
            Deliver(peer, inner, apply);
            Drain(peer, key, g, apply);
        }

        private static void Deliver(Peer peer, byte[] inner, Action<Peer, byte[]> apply)
        {
            if (inner == null || inner.Length == 0) return;
            try { apply(peer, inner); } catch (Exception e) { MelonLogger.Warning("[сеть] событие друга: " + e.Message); }
        }

        private static void Drain(Peer peer, string key, From g, Action<Peer, byte[]> apply)
        {
            int n = 0;
            while (g.Held.Count > 0)
            {
                uint first = FirstHeld(g);
                if (first > g.Last + 1) break;
                var d = g.Held[first];
                g.Held.Remove(first);
                if (first <= g.Last) continue;
                g.Last = first;
                Deliver(peer, d, apply);
                n++;
            }
            if (n > 0) MelonLogger.Msg($"[сеть] {key}: дыра в его счёте закрылась, применил по порядку ещё {n}");
            if (g.Held.Count > 0) g.HeldSince = Time.unscaledTime;
        }

        private static uint FirstHeld(From g) { foreach (var k in g.Held.Keys) return k; return 0; }

        private static void Ask(Peer peer, string key, From g)
        {
            float now = Time.unscaledTime;
            if (g.Held.Count == 0 || now < g.NextAsk || peer == null || peer.Closed) return;
            g.NextAsk = now + 2f;
            uint from = g.Last + 1, to = FirstHeld(g) - 1;
            var w = Msg.New(MsgType.SeqCtl);
            w.Write(KAsk); w.Write(g.Log); w.Write(from); w.Write(to);
            peer.Send(Msg.Done(w));
            _asked++;
            if (now >= g.NextWaitLog)
            {
                g.NextWaitLog = now + 30f;
                MelonLogger.Msg($"[сеть] {key}: нет его событий {from}–{to}, жду и прошу дослать (отложено {g.Held.Count}, жду {now - g.HeldSince:0} с)");
            }
        }

        // Друг ответил, что событий до from у него уже нет: дальше ждать нечего.
        public static void HostGone(Peer peer, string key, BinaryReader r, Action<Peer, byte[]> apply)
        {
            int log = r.ReadInt32();
            uint from = r.ReadUInt32();
            r.ReadUInt32();
            if (!Got.TryGetValue(key, out var g) || g.Log != log || from <= g.Last + 1) return;
            _gone++;
            MelonLogger.Warning($"[сеть] {key}: его события {g.Last + 1}–{from - 1} пропали (у него их уже нет), продолжаю с №{from}");
            g.Last = from - 1;
            Drain(peer, key, g, apply);
        }

        // Хост, раз в секунду каждому другу: «получил всё до N»; дыра не закрылась — прошу снова.
        public static void HostTick(Peer peer, string key)
        {
            if (peer == null || !peer.Greeted || peer.Closed || !Got.TryGetValue(key, out var g)) return;
            float now = Time.unscaledTime;
            if (g.Held.Count > 0) Ask(peer, key, g);
            if (g.Last == g.LastAcked && now < g.NextAck) return;
            g.LastAcked = g.Last;
            g.NextAck = now + 5f;
            var w = Msg.New(MsgType.SeqCtl);
            w.Write(KAck); w.Write(g.Log); w.Write(g.Last); w.Write(0u);
            peer.Send(Msg.Done(w));
        }

        public static string Stats => Session.Mode == Mode.Host
            ? $"от друзей: повторов {_dups}, просил дослать {_asked}, пропало у друга {_gone}, ждут дыры {HeldTotal()}"
            : $"свои события: №{_seq}, подтверждено №{_acked}, не подтверждено {Sent.Count}, дослано {_resent}" + (_lost > 0 ? $", стёрто без подтверждения {_lost}" : "");

        private static int HeldTotal() { int n = 0; foreach (var g in Got.Values) n += g.Held.Count; return n; }
    }
}
