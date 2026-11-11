using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace TLDCoop.Net
{
    // Запасной способ: прямое TCP-соединение по IP (локальная сеть, VPN, открытый порт).
    // Каждое сообщение уходит кадром «длина (4 байта) + данные». Чтение и запись идут в своих
    // потоках, игра не ждёт сеть.
    public class TcpPeer : Peer
    {
        private readonly TcpClient _tcp;
        private readonly NetworkStream _stream;
        private readonly BlockingCollection<byte[]> _outbox = new(new ConcurrentQueue<byte[]>());
        private readonly string _remote;

        public override string Describe => _remote;

        public TcpPeer(TcpClient tcp)
        {
            _tcp = tcp;
            _tcp.NoDelay = true;
            _stream = tcp.GetStream();
            _remote = tcp.Client.RemoteEndPoint?.ToString() ?? "?";
            new Thread(ReadLoop) { IsBackground = true, Name = "TLDCoop read" }.Start();
            new Thread(WriteLoop) { IsBackground = true, Name = "TLDCoop write" }.Start();
        }

        public override void Send(byte[] data, bool reliable = true)
        {
            if (Closed) return;
            if (data.Length > BigMsg.Limit) { foreach (var part in BigMsg.Split(data)) Send(part, reliable); return; }
            // Позиции и состояния зверей (без гарантии доставки) при отставании не копим: в очереди они уже устарели,
            // и фигура друга отставала всё сильнее. Надёжные события остаются.
            if (!reliable && _outbox.Count > 20) return;
            // Если сеть не успевает, не копим бесконечную очередь: соединение явно мёртвое.
            if (_outbox.Count > 2000)
            {
                Close();
                return;
            }
            try { _outbox.Add(data); } catch (InvalidOperationException) { }
        }

        public override void Close()
        {
            if (Closed) return;
            Closed = true;
            try { _outbox.CompleteAdding(); } catch { }
            try { _tcp.Close(); } catch { }
            Inbox.Queue.Enqueue((this, null));
        }

        // Последнее (Bye, Reject) допишет поток записи и сам закроет сокет; не дописал за 1 с — закрываем всё равно.
        private Timer _closeTimer;
        private volatile bool _closing;

        public override void CloseAfterSend()
        {
            if (Closed) return;
            _closing = true;
            Closed = true;
            try { _outbox.CompleteAdding(); } catch { }
            _closeTimer = new Timer(CloseSocket, null, 1000, System.Threading.Timeout.Infinite);
            Inbox.Queue.Enqueue((this, null));
        }

        private void CloseSocket(object state)
        {
            try { _tcp.Close(); } catch { }
        }

        private void ReadLoop()
        {
            var header = new byte[4];
            try
            {
                while (!Closed)
                {
                    ReadExact(header, 4);
                    int len = BitConverter.ToInt32(header, 0);
                    if (len <= 0 || len > Protocol.MaxFrame) throw new IOException("Плохой размер сообщения: " + len);
                    var data = new byte[len];
                    ReadExact(data, len);
                    if (_closing) break;   // мы его закрываем (выгнан, выход): досланное не разбираем
                    Inbox.Queue.Enqueue((this, data));
                }
            }
            catch
            {
            }
            Close();
        }

        private void ReadExact(byte[] buf, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n = _stream.Read(buf, got, count - got);
                if (n <= 0) throw new IOException("Соединение закрыто");
                got += n;
            }
        }

        private void WriteLoop()
        {
            try
            {
                foreach (var data in _outbox.GetConsumingEnumerable())
                {
                    _stream.Write(BitConverter.GetBytes(data.Length), 0, 4);
                    _stream.Write(data, 0, data.Length);
                }
            }
            catch
            {
            }
            Close();
            try { _tcp.Close(); } catch { }   // после CloseAfterSend Close() уже ничего не делает
        }

        // ---------- Сервер ----------

        private static TcpListener _listener;

        public static void Listen(int port, Action<TcpPeer> onAccept, bool loopbackOnly = false)
        {
            StopListening();
            _listener = new TcpListener(loopbackOnly ? IPAddress.Loopback : IPAddress.Any, port);
            _listener.Start();
            var listener = _listener;
            new Thread(() =>
            {
                try
                {
                    while (true)
                    {
                        var tcp = listener.AcceptTcpClient();
                        onAccept(new TcpPeer(tcp));
                    }
                }
                catch
                {
                }
            }) { IsBackground = true, Name = "TLDCoop listen" }.Start();
        }

        public static void StopListening()
        {
            try { _listener?.Stop(); } catch { }
            _listener = null;
        }

        public static TcpPeer Connect(string host, int port)
        {
            var tcp = new TcpClient();
            var task = tcp.ConnectAsync(host, port);
            if (!task.Wait(5000) || !tcp.Connected)
            {
                tcp.Close();
                throw new IOException("Не удалось подключиться к " + host + ":" + port);
            }
            return new TcpPeer(tcp);
        }
    }
}
