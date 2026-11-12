using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using NativeLibrary = System.Runtime.InteropServices.NativeLibrary;
using MelonLoader;

namespace CoopByEMPEROR.Net
{
    // Сеть через Steam без IP и портов. Игра уже запустила Steam API (своё приложение 305620),
    // поэтому мод просто вызывает функции из той же steam_api64.dll.
    //
    // Обратные вызовы Steam (callbacks) мод НЕ использует: их разбирает сама игра, и вмешиваться
    // туда опасно. Всё сделано опросом:
    //  - хост пишет в Rich Presence «tldcoop=host»;
    //  - друг видит это в списке друзей, пишет «tldcoop=join:<SteamID хоста>» и шлёт хосту Hello;
    //  - хост раз в секунду смотрит друзей, видит запрос и сам шлёт другу сообщение. Отправка
    //    сообщения автоматически принимает сессию Steam с этим человеком, после этого сообщения
    //    идут в обе стороны через ретрансляторы Steam.
    public static class Steam
    {
        public const uint TldAppId = 305620;
        private const string Lib = "steam_api64";

        // Флаги отправки ISteamNetworkingMessages.
        private const int SendUnreliable = 0, SendNoNagle = 1, SendReliable = 8;

        private static IntPtr _friends, _user, _messages;
        public static bool Ready { get; private set; }
        public static string Error { get; private set; }
        public static ulong MySteamId { get; private set; }

        public static bool Init()
        {
            if (Ready) return true;
            if (Game.Bot.Active)
            {
                Error = "у бота Steam выключен";
                return false;
            }
            try
            {
                if (!PickLibrary()) return false;
                if (!SteamAPI_IsSteamRunning())
                {
                    Error = "Steam не запущен";
                    return false;
                }
                _friends = Accessor("SteamAPI_SteamFriends_v018", "SteamAPI_SteamFriends_v017");
                _user = Accessor("SteamAPI_SteamUser_v023", "SteamAPI_SteamUser_v022", "SteamAPI_SteamUser_v021");
                _messages = Accessor("SteamAPI_SteamNetworkingMessages_SteamAPI_v002");
                if (_friends == IntPtr.Zero || _user == IntPtr.Zero || _messages == IntPtr.Zero)
                {
                    Error = "Steam API игры недоступен";
                    return false;
                }
                MySteamId = SteamAPI_ISteamUser_GetSteamID(_user);
                Ready = MySteamId != 0;
                if (!Ready) Error = "Не удалось узнать свой SteamID";
                else MelonLogger.Msg($"[steam] готов, мой SteamID {MySteamId}, ник «{MyName()}»");
                return Ready;
            }
            catch (Exception e)
            {
                Error = e.GetType().Name + ": " + e.Message;
                MelonLogger.Warning("[steam] " + Error);
                return false;
            }
        }

        // ---------- Какая steam_api64.dll ----------
        // В папке игры их две: в корне (новая, Friends v018) и в tld_Data/Plugins/x86_64 (старая, v017).
        // Нужна та, через которую игра уже запустила Steam: у неё SteamAPI_GetHSteamUser() != 0.
        // Все [DllImport("steam_api64")] ниже направляются в выбранную библиотеку.

        private static IntPtr _lib;
        private static bool _resolverSet;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr AccessorFn();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int HSteamUserFn();

        private static bool PickLibrary()
        {
            if (_lib != IntPtr.Zero) return true;
            var paths = new List<string>();
            try
            {
                foreach (ProcessModule m in Process.GetCurrentProcess().Modules)
                    if (string.Equals(m.ModuleName, "steam_api64.dll", StringComparison.OrdinalIgnoreCase)) paths.Add(m.FileName);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[steam] список модулей: " + e.Message);
            }
            bool loadedByGame = paths.Count > 0;
            if (!loadedByGame)
            {
                string root = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName ?? "") ?? "";
                paths.Add(Path.Combine(root, "steam_api64.dll"));
                paths.Add(Path.Combine(root, "tld_Data", "Plugins", "x86_64", "steam_api64.dll"));
            }

            IntPtr fallback = IntPtr.Zero;
            foreach (var path in paths.Distinct())
            {
                if (!File.Exists(path) || !NativeLibrary.TryLoad(path, out var h)) continue;
                int user = 0;
                if (NativeLibrary.TryGetExport(h, "SteamAPI_GetHSteamUser", out var fn))
                    try { user = Marshal.GetDelegateForFunctionPointer<HSteamUserFn>(fn)(); } catch { }
                MelonLogger.Msg($"[steam] {path}: HSteamUser={user}");
                if (user != 0) { _lib = h; break; }
                if (fallback == IntPtr.Zero) fallback = h;
            }
            if (_lib == IntPtr.Zero) _lib = fallback;
            if (_lib == IntPtr.Zero)
            {
                Error = "не найдена steam_api64.dll игры";
                return false;
            }
            if (!_resolverSet)
            {
                _resolverSet = true;
                NativeLibrary.SetDllImportResolver(typeof(Steam).Assembly, Resolve);
            }
            return true;
        }

        private static IntPtr Resolve(string name, System.Reflection.Assembly asm, DllImportSearchPath? search) =>
            name == Lib || name == Lib + ".dll" ? _lib : IntPtr.Zero;

        // Первая из версий интерфейса, которая есть в библиотеке.
        private static IntPtr Accessor(params string[] names)
        {
            foreach (var n in names)
            {
                if (!NativeLibrary.TryGetExport(_lib, n, out var fn)) continue;
                var p = Marshal.GetDelegateForFunctionPointer<AccessorFn>(fn)();
                MelonLogger.Msg($"[steam] {n} = {(p == IntPtr.Zero ? "null" : "ok")}");
                return p;
            }
            MelonLogger.Warning("[steam] нет ни одной из версий: " + string.Join(", ", names));
            return IntPtr.Zero;
        }

        public static string MyName() => Utf8(SteamAPI_ISteamFriends_GetPersonaName(_friends));
        public static string FriendName(ulong id) => Utf8(SteamAPI_ISteamFriends_GetFriendPersonaName(_friends, id));

        // ---------- Rich Presence ----------

        public const string Key = "tldcoop";

        public static void SetPresence(string value)
        {
            if (!Ready) return;
            SteamAPI_ISteamFriends_SetRichPresence(_friends, Key, value ?? "");
            // Видно в списке друзей Steam рядом с названием игры (если игра это поддерживает).
            SteamAPI_ISteamFriends_SetRichPresence(_friends, "status", value == null ? "" : "CoopByEMPEROR");
            // Кнопка Steam «Присоединиться к игре» у друзей. Если игра у друга не запущена, Steam запустит её
            // с этой строкой в командной строке, и мод сам подключится (LaunchJoinTarget).
            bool host = value != null && value.StartsWith("host");
            SteamAPI_ISteamFriends_SetRichPresence(_friends, "connect", host ? JoinArg + " " + MySteamId : "");
        }

        // Строка под ником в списке друзей Steam. У игры в настройках Steam есть токен «#Status», который
        // показывает переменную status_value, — туда и пишем свой текст (так делает и мод Embers).
        public static void SetDisplay(string text, string displayKey, string token, string variable)
        {
            if (!Ready) return;
            SteamAPI_ISteamFriends_SetRichPresence(_friends, variable, text ?? "");
            SteamAPI_ISteamFriends_SetRichPresence(_friends, displayKey, string.IsNullOrEmpty(text) ? "" : token);
        }

        public const string JoinArg = "+tldcoop_join";

        // Игру запустили кнопкой Steam «Присоединиться к игре»: SteamID хоста из командной строки, иначе 0.
        public static ulong LaunchJoinTarget()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (string.Equals(args[i], JoinArg, StringComparison.OrdinalIgnoreCase) && ulong.TryParse(args[i + 1], out ulong id)) return id;
            }
            catch { }
            return 0;
        }

        public static string FriendPresence(ulong id) => Utf8(SteamAPI_ISteamFriends_GetFriendRichPresence(_friends, id, Key));

        public struct Friend
        {
            public ulong Id;
            public string Name;
            public string Presence;
        }

        private static float _nextRequest;

        // Друзья, которые прямо сейчас в The Long Dark, и их значение tldcoop.
        public static List<Friend> FriendsInGame(float now)
        {
            var list = new List<Friend>();
            if (!Ready) return list;
            bool request = now >= _nextRequest;
            if (request) _nextRequest = now + 5f;
            int count = SteamAPI_ISteamFriends_GetFriendCount(_friends, 4 /* k_EFriendFlagImmediate */);
            var info = new byte[32];
            for (int i = 0; i < count; i++)
            {
                ulong id = SteamAPI_ISteamFriends_GetFriendByIndex(_friends, i, 4);
                if (!SteamAPI_ISteamFriends_GetFriendGamePlayed(_friends, id, info)) continue;
                uint app = (uint)(BitConverter.ToUInt64(info, 0) & 0xFFFFFF);
                if (app != TldAppId) continue;
                if (request) SteamAPI_ISteamFriends_RequestFriendRichPresence(_friends, id);
                list.Add(new Friend { Id = id, Name = FriendName(id), Presence = FriendPresence(id) });
            }
            return list;
        }

        // ---------- Сообщения ----------

        public static bool Send(ulong to, byte[] data, bool reliable)
        {
            if (!Ready) return false;
            var ident = Marshal.AllocHGlobal(IdentitySize);
            var buf = Marshal.AllocHGlobal(data.Length);
            try
            {
                Zero(ident, IdentitySize);
                SteamAPI_SteamNetworkingIdentity_SetSteamID64(ident, to);
                Marshal.Copy(data, 0, buf, data.Length);
                int flags = (reliable ? SendReliable : SendUnreliable) | SendNoNagle;
                int result = SteamAPI_ISteamNetworkingMessages_SendMessageToUser(_messages, ident, buf, (uint)data.Length, flags, 0);
                return result == 1;
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
                Marshal.FreeHGlobal(ident);
            }
        }

        public static void CloseSession(ulong with)
        {
            if (!Ready) return;
            var ident = Marshal.AllocHGlobal(IdentitySize);
            try
            {
                Zero(ident, IdentitySize);
                SteamAPI_SteamNetworkingIdentity_SetSteamID64(ident, with);
                SteamAPI_ISteamNetworkingMessages_CloseSessionWithUser(_messages, ident);
            }
            finally
            {
                Marshal.FreeHGlobal(ident);
            }
        }

        private static readonly IntPtr[] Received = new IntPtr[64];

        // Забрать всё пришедшее. Вызывать каждый кадр из игрового потока.
        public static void Poll(Action<ulong, byte[]> onMessage)
        {
            if (!Ready) return;
            while (true)
            {
                int n = SteamAPI_ISteamNetworkingMessages_ReceiveMessagesOnChannel(_messages, 0, Received, Received.Length);
                if (n <= 0) return;
                for (int i = 0; i < n; i++)
                {
                    IntPtr msg = Received[i];
                    try
                    {
                        // SteamNetworkingMessage_t (64 бита): данные @0, размер @8, отправитель @16.
                        IntPtr data = Marshal.ReadIntPtr(msg, 0);
                        int size = Marshal.ReadInt32(msg, 8);
                        ulong from = SteamAPI_SteamNetworkingIdentity_GetSteamID64(msg + 16);
                        if (size > 0 && size <= Protocol.MaxFrame && from != 0)
                        {
                            var bytes = new byte[size];
                            Marshal.Copy(data, bytes, 0, size);
                            onMessage(from, bytes);
                        }
                    }
                    finally
                    {
                        SteamAPI_SteamNetworkingMessage_t_Release(msg);
                    }
                }
                if (n < Received.Length) return;
            }
        }

        // ---------- Вспомогательное ----------

        private const int IdentitySize = 160; // SteamNetworkingIdentity = 136 байт, берём с запасом

        private static void Zero(IntPtr p, int size)
        {
            for (int i = 0; i < size; i += 8) Marshal.WriteInt64(p, i, 0);
        }

        private static string Utf8(IntPtr p) => p == IntPtr.Zero ? "" : Marshal.PtrToStringUTF8(p) ?? "";

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SteamAPI_IsSteamRunning();

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong SteamAPI_ISteamUser_GetSteamID(IntPtr self);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SteamAPI_ISteamFriends_GetPersonaName(IntPtr self);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SteamAPI_ISteamFriends_GetFriendPersonaName(IntPtr self, ulong id);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SteamAPI_ISteamFriends_GetFriendCount(IntPtr self, int flags);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong SteamAPI_ISteamFriends_GetFriendByIndex(IntPtr self, int index, int flags);

        // FriendGameInfo_t: m_gameID (uint64) @0, IP @8, порты @12/@14, m_steamIDLobby @16.
        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SteamAPI_ISteamFriends_GetFriendGamePlayed(IntPtr self, ulong id, [Out] byte[] info);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SteamAPI_ISteamFriends_SetRichPresence(IntPtr self,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SteamAPI_ISteamFriends_GetFriendRichPresence(IntPtr self, ulong id,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string key);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SteamAPI_ISteamFriends_RequestFriendRichPresence(IntPtr self, ulong id);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SteamAPI_SteamNetworkingIdentity_SetSteamID64(IntPtr identity, ulong id);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong SteamAPI_SteamNetworkingIdentity_GetSteamID64(IntPtr identity);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SteamAPI_ISteamNetworkingMessages_SendMessageToUser(IntPtr self, IntPtr identity,
            IntPtr data, uint size, int flags, int channel);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern int SteamAPI_ISteamNetworkingMessages_ReceiveMessagesOnChannel(IntPtr self, int channel,
            [Out] IntPtr[] messages, int max);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SteamAPI_ISteamNetworkingMessages_CloseSessionWithUser(IntPtr self, IntPtr identity);

        [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SteamAPI_SteamNetworkingMessage_t_Release(IntPtr message);
    }

    public class SteamPeer : Peer
    {
        public readonly ulong SteamId;
        public SteamPeer(ulong id) { SteamId = id; }

        public override string Describe => "Steam " + SteamId;

        // Надёжное сообщение, которое Steam не принял (буфер отправки полон), не теряем: ставим в очередь
        // и досылаем по порядку. Пока очередь не пуста, новые надёжные встают за ней, чтобы не перемешать порядок.
        private readonly System.Collections.Generic.Queue<byte[]> _retry = new();
        private int _headTries;
        private float _headSince = -1f;   // с какого времени (реального) головное сообщение не уходит
        public static int Failed, Retried, Dropped;

        public override void Send(byte[] data, bool reliable = true)
        {
            if (Closed) return;
            if (!reliable) { Steam.Send(SteamId, data, false); return; }
            if (data.Length > BigMsg.Limit) { foreach (var part in BigMsg.Split(data)) Send(part, true); return; }
            if (_retry.Count == 0 && Steam.Send(SteamId, data, true)) return;
            if (_retry.Count >= 20000)
            {
                // Очередь не двигается: связь оборвана. Закрываем — друг переподключится, и всё неподтверждённое дошлётся.
                Dropped++;
                MelonLoader.MelonLogger.Warning("[сеть] очередь повторов Steam переполнена: считаю связь оборванной");
                Close();
                return;
            }
            _retry.Enqueue(data);
            if (Failed++ < 10) MelonLoader.MelonLogger.Msg($"[сеть] Steam не принял сообщение ({data.Length} Б, в очереди {_retry.Count}), дошлю");
        }

        public override int Queued => _retry.Count;

        public override void Flush()
        {
            while (_retry.Count > 0 && !Closed)
            {
                if (!Steam.Send(SteamId, _retry.Peek(), true))
                {
                    // Одно и то же сообщение не уходит 10 секунд. Большие режем на куски, так что дело не в размере, а в связи:
                    // раньше сообщение выбрасывали, и событие пропадало навсегда. Теперь закрываем соединение — после
                    // переподключения всё неподтверждённое дошлётся (Net/ClientLog.cs, Net/EventLog.cs).
                    // Считаем реальные секунды (раньше — 600 кадров, при 144 FPS это 4 с), и не меньше 60 попыток:
                    // после долгой загрузки сцены у Steam должно быть время отправить.
                    float now = UnityEngine.Time.unscaledTime;
                    if (_headSince < 0f) _headSince = now;
                    if (++_headTries < 60 || now - _headSince < 10f) return;
                    MelonLoader.MelonLogger.Warning($"[сеть] сообщение ({_retry.Peek().Length} Б) не уходит 10 с: считаю связь оборванной, переподключаемся");
                    Dropped++;
                    Close();
                    return;
                }
                else Retried++;
                _retry.Dequeue();
                _headTries = 0;
                _headSince = -1f;
            }
        }

        public override bool TrySend(byte[] data) => !Closed && Steam.Send(SteamId, data, true);

        public override void Close()
        {
            if (Closed) return;
            Closed = true;
            Steam.CloseSession(SteamId);
            Inbox.Queue.Enqueue((this, null));
        }

        // Steam рвёт сессию вместе с неотправленным: закрываемые после Bye/Reject ещё 1 с досылают очередь,
        // потом закрываем сессию. Тикает и вне коопа (Session.Tick до проверки режима).
        private float _closeAt;
        private static readonly System.Collections.Generic.List<SteamPeer> Lingering = new();

        public override void CloseAfterSend()
        {
            if (Closed) return;
            Closed = true;
            _closeAt = UnityEngine.Time.unscaledTime + 1f;
            Lingering.Add(this);
            Inbox.Queue.Enqueue((this, null));
        }

        public static bool IsLingering(ulong id) => Lingering.Exists(p => p.SteamId == id);

        public static void TickLingering()
        {
            for (int i = Lingering.Count - 1; i >= 0; i--)
            {
                var p = Lingering[i];
                while (p._retry.Count > 0 && Steam.Send(p.SteamId, p._retry.Peek(), true)) p._retry.Dequeue();
                if (UnityEngine.Time.unscaledTime < p._closeAt) continue;
                Lingering.RemoveAt(i);
                Steam.CloseSession(p.SteamId);
            }
        }

        // Закрыть сразу (id = 0 — все): новое соединение с тем же игроком, старое не должно закрыть его сессию.
        public static void CloseLingering(ulong id = 0)
        {
            for (int i = Lingering.Count - 1; i >= 0; i--)
            {
                if (id != 0 && Lingering[i].SteamId != id) continue;
                Steam.CloseSession(Lingering[i].SteamId);
                Lingering.RemoveAt(i);
            }
        }
    }
}
