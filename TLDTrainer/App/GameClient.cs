using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TLDTrainer.Shared;

namespace TLDTrainer.App
{
    // Связь со встроенной частью трейнера в игре (локальный TCP, строка JSON на запрос и на ответ).
    internal sealed class GameClient
    {
        private readonly object _lock = new();
        private TcpClient _client;
        private StreamReader _reader;
        private StreamWriter _writer;

        public JsonElement Send(object request)
        {
            lock (_lock)
            {
                try
                {
                    if (_client == null) Connect();
                    _writer.WriteLine(JsonSerializer.Serialize(request));
                    string line = _reader.ReadLine() ?? throw new IOException("Соединение закрыто");
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement.Clone();
                    if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
                        throw new GameException(root.TryGetProperty("error", out var e) ? e.GetString() : "Ошибка");
                    return root;
                }
                catch (GameException)
                {
                    throw;
                }
                catch
                {
                    Close();
                    throw;
                }
            }
        }

        private void Connect()
        {
            var c = new TcpClient();
            if (!c.ConnectAsync("127.0.0.1", Features.Port).Wait(400))
            {
                c.Dispose();
                throw new IOException("Игра не отвечает");
            }
            c.ReceiveTimeout = 25000;
            var stream = c.GetStream();
            _client = c;
            _reader = new StreamReader(stream, new UTF8Encoding(false));
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        }

        private void Close()
        {
            try
            {
                _client?.Dispose();
            }
            catch
            {
                // уже закрыто
            }
            _client = null;
            _reader = null;
            _writer = null;
        }
    }

    internal sealed class GameException : Exception
    {
        public GameException(string message) : base(message)
        {
        }
    }
}
