using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Linq;
using System.Threading.Tasks;
using TrainerByEMPEROR.Shared;

namespace TrainerByEMPEROR.App
{
    // Общее состояние окна: связь с игрой, настройки, текущие значения функций.
    internal sealed class Ctx
    {
        public readonly GameClient Client = new();
        public readonly Settings Settings = Settings.Load();
        public readonly Dictionary<string, float> Values = new();

        public bool Connected;
        public bool InGame;
        public bool GameRunning;
        private bool _bindsPushed;

        public event Action StateChanged;
        public Action<string, bool> Notify = (_, _) => { };
        public Action<string> Navigate = _ => { };
        public Overlay Overlay;

        // Срабатывает, когда в игре загрузили сохранение: разделы, которым нужна игра, обновляются сами.
        public event Action EnteredGame;
        private bool _lastInGame;

        // Язык сменили: разделы окна строятся заново, старые больше не слушают игру.
        public void ClearPageEvents()
        {
            StateChanged = null;
            EnteredGame = null;
        }

        public void RaiseStateChanged()
        {
            StateChanged?.Invoke();
            if (InGame && !_lastInGame) EnteredGame?.Invoke();
            _lastInGame = InGame;
        }

        public static Dictionary<string, string> DefaultBinds() =>
            Features.All.Where(f => Features.Bindable(f) && !string.IsNullOrEmpty(f.Hotkey)).ToDictionary(f => f.Id, f => f.Hotkey);

        public Dictionary<string, string> Binds => Settings.Binds ??= DefaultBinds();

        public string GetBind(string id) => Binds.TryGetValue(id, out var c) ? c : "";

        // Назначает сочетание; если оно уже занято другой функцией, снимает его оттуда.
        public void SetBind(string id, string combo)
        {
            if (!string.IsNullOrEmpty(combo))
                foreach (var other in Binds.Where(kv => kv.Key != id && kv.Value == combo).Select(kv => kv.Key).ToList())
                    Binds.Remove(other);
            if (string.IsNullOrEmpty(combo)) Binds.Remove(id);
            else Binds[id] = combo;
            Settings.Save();
        }

        public void ResetBinds()
        {
            Settings.Binds = DefaultBinds();
            Settings.Save();
        }

        public Task PushBinds() => Request(new { cmd = "setbinds", binds = Binds });

        public float Get(string id)
        {
            lock (Values) return Values.TryGetValue(id, out var v) ? v : 0f;
        }

        public void SetLocal(string id, float value)
        {
            lock (Values) Values[id] = value;
        }

        public async Task<JsonElement?> Request(object request, string okMessage = null)
        {
            try
            {
                var r = await Task.Run(() => Client.Send(request));
                if (okMessage != null) Notify(okMessage, false);
                return r;
            }
            catch (Exception e)
            {
                Notify(Friendly(e), true);
                return null;
            }
        }

        public static string Friendly(Exception e)
        {
            if (e is AggregateException ae && ae.InnerException != null) e = ae.InnerException;
            if (e is GameException) return e.Message;
            if (e is IOException || e is SocketException)
                return L.T("Нет связи с игрой. Запустите игру кнопкой «Играть» (трейнер должен быть установлен).", "No connection to the game. Start the game with “Play” (the trainer must be installed).");
            return e.Message;
        }

        // Опрос игры раз в секунду (вызывается не из UI-потока).
        public void Poll()
        {
            GameRunning = GameInstaller.IsGameRunning();
            if (!Connected) _bindsPushed = false;
            try
            {
                var r = Client.Send(new { cmd = "state", lang = L.Code });
                Connected = true;
                InGame = r.GetProperty("inGame").GetBoolean();
                lock (Values)
                {
                    foreach (var p in r.GetProperty("values").EnumerateObject())
                        Values[p.Name] = (float)p.Value.GetDouble();
                }
            }
            catch
            {
                Connected = false;
                InGame = false;
                return;
            }

            if (!_bindsPushed)
            {
                try
                {
                    Client.Send(new { cmd = "setbinds", binds = Binds });
                    _bindsPushed = true;
                }
                catch
                {
                    // повторим при следующем опросе
                }
            }
        }
    }
}
