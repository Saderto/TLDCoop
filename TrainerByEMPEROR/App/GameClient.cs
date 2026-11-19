using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TrainerByEMPEROR.Shared;

namespace TrainerByEMPEROR.App
{
    // Связь со встроенной частью трейнера в игре (локальный TCP, строка JSON на запрос и на ответ).
    internal sealed class GameClient
    {
        // Несколько соединений: долгий запрос (карта, список предметов) или частый опрос не держат очередь,
        // и нажатие кнопки уходит в игру сразу, а не ждёт, пока ответят на всё, что было раньше.
        private const int MaxIdle = 4;

        private sealed class Conn : IDisposable
        {
            public TcpClient Client;
            public StreamReader Reader;
            public StreamWriter Writer;

            public void Dispose()
            {
                try
                {
                    Client?.Dispose();
                }
                catch
                {
                    // уже закрыто
                }
            }
        }

        private readonly ConcurrentBag<Conn> _idle = new();

        public JsonElement Send(object request)
        {
            string json = JsonSerializer.Serialize(request);
            var c = _idle.TryTake(out var free) ? free : Connect();
            bool keep = false;
            try
            {
                c.Writer.WriteLine(json);
                string line = c.Reader.ReadLine() ?? throw new IOException(L.T("Соединение закрыто", "Connection closed"));
                keep = true;
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement.Clone();
                if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
                    throw new GameException(root.TryGetProperty("error", out var e) ? e.GetString() : L.T("Ошибка", "Error"));
                return root;
            }
            finally
            {
                if (keep && _idle.Count < MaxIdle) _idle.Add(c);
                else c.Dispose();
            }
        }

        private static Conn Connect()
        {
            var c = new TcpClient { NoDelay = true };
            if (!c.ConnectAsync("127.0.0.1", Features.Port).Wait(400))
            {
                c.Dispose();
                throw new IOException(L.T("Игра не отвечает", "The game does not answer"));
            }
            c.ReceiveTimeout = 25000;
            var stream = c.GetStream();
            return new Conn
            {
                Client = c,
                Reader = new StreamReader(stream, new UTF8Encoding(false)),
                Writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" },
            };
        }
    }

    internal sealed class GameException : Exception
    {
        public GameException(string message) : base(message)
        {
        }
    }
}
