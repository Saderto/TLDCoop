using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace TLDCoop.Net
{
    // Другая сторона соединения: через Steam (SteamPeer) или напрямую по IP (TcpPeer).
    // Session работает с любым из них одинаково.
    public abstract class Peer
    {
        public int Id;            // id игрока (назначает хост; у хоста всегда 0)
        public bool Greeted;      // хост: клиент уже прислал Hello
        public float LastHeard;   // Time.unscaledTime последнего сообщения
        public volatile bool Closed;
        public float Rtt = -1f;   // пинг туда-обратно, секунды (-1 — ещё не измерен)
        public uint Resent;       // хост: до какого номера события мира уже дослали после переподключения (Net/EventLog.cs)
        public uint Acked;        // хост: друг подтвердил, что получил все события мира до этого номера (журнал AckedLog)
        public int AckedLog;
        public float ReloadAt = -100f;   // хост: когда последний раз просили друга загрузить мир заново (журнал переполнился)
        public float HelloAskedAt = -100f;   // хост: когда последний раз просили неприветствованного Steam-друга поздороваться заново

        // Сборка большого сообщения из кусков (Net/Peer.cs, BigMsg).
        internal uint BigId;
        internal byte[][] BigParts;
        internal int BigGot;

        // Досылка того, что не ушло с первого раза (Steam отказал: буфер полон). Вызывается каждый кадр.
        public virtual void Flush() { }
        public virtual int Queued => 0;

        public abstract string Describe { get; }

        // reliable = false — можно потерять (позиции, их всё равно шлём 15 раз в секунду).
        public abstract void Send(byte[] data, bool reliable = true);

        // Надёжная отправка, которая сообщает, принята ли она (у Steam ограничен буфер отправки).
        public virtual bool TrySend(byte[] data)
        {
            Send(data, true);
            return !Closed;
        }
        public abstract void Close();

        // Закрыть, когда уйдёт уже поставленное (Bye, Reject): при Close оно терялось вместе с соединением.
        public virtual void CloseAfterSend() => Close();
    }

    // Все полученные сообщения из всех соединений. data == null значит «соединение закрылось».
    public static class Inbox
    {
        public static readonly ConcurrentQueue<(Peer Peer, byte[] Data)> Queue = new();
    }

    // Большие сообщения: Steam не принимает больше 512 КБ (и такое сообщение раньше висело в очереди повторов 10 с,
    // а потом выбрасывалось), а кадр TCP у нас до 1 МБ. Режем на куски по 256 КБ и собираем на приёме.
    // Надёжные сообщения одному игроку идут по порядку, поэтому куски одного сообщения приходят подряд.
    public static class BigMsg
    {
        public const int Limit = 400 * 1024, Part = 256 * 1024;
        private static uint _next;
        public static int Split_, Joined;

        public static IEnumerable<byte[]> Split(byte[] data)
        {
            uint id = ++_next;
            int total = (data.Length + Part - 1) / Part;
            Split_++;
            for (int i = 0; i < total; i++)
            {
                int off = i * Part, len = Math.Min(Part, data.Length - off);
                var w = Msg.New(MsgType.Big);
                w.Write(id); w.Write(i); w.Write(total); w.Write(len); w.Write(data, off, len);
                yield return Msg.Done(w);
            }
        }

        // Кусок пришёл. Собрали все — возвращаем целое сообщение, иначе null.
        public static byte[] Add(Peer peer, BinaryReader r)
        {
            uint id = r.ReadUInt32();
            int index = r.ReadInt32(), total = r.ReadInt32(), len = r.ReadInt32();
            var bytes = r.ReadBytes(len);
            if (total <= 0 || total > 4096 || index < 0 || index >= total || bytes.Length != len) return null;
            if (peer.BigParts == null || peer.BigId != id || peer.BigParts.Length != total)
            {
                peer.BigParts = new byte[total][];
                peer.BigId = id;
                peer.BigGot = 0;
            }
            if (peer.BigParts[index] == null) { peer.BigParts[index] = bytes; peer.BigGot++; }
            if (peer.BigGot < total) return null;
            int size = 0;
            foreach (var p in peer.BigParts) size += p.Length;
            var full = new byte[size];
            int o = 0;
            foreach (var p in peer.BigParts) { Buffer.BlockCopy(p, 0, full, o, p.Length); o += p.Length; }
            peer.BigParts = null;
            Joined++;
            return full;
        }
    }
}
