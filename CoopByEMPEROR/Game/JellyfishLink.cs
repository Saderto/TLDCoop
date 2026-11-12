using System;
using System.IO;
using System.Reflection;
using MelonLoader;
using CoopByEMPEROR.Net;

namespace CoopByEMPEROR.Game
{
    // Связь с модом JellyfishByEMPEROR (медузы) без жёсткой ссылки на его dll — по образцу SmokehouseLink, те же поля CoopApi:
    //  - Send (Action<string>): мод зовёт его на каждое событие — медуза всплыла в лунке, её забрали/она ушла, аквариум изменился.
    //    Строка уходит всем как событие мира с номером (MsgType.Jellyfish), у остальных — CoopApi.Receive(строка);
    //  - друг, войдя в мир хоста, просит все аквариумы (MsgType.JellyfishAll), хост отвечает CoopApi.Snapshot();
    //  - решает хост (MsgType.JellyfishAsk): кто первым забрал медузу из лунки, перелить в аквариум (не больше трёх),
    //    забрать из аквариума банкой — у друга мод спрашивает хоста через Request, хост — HostRequest, ответ — Answer.
    //  Банки и аквариум сами — обычные вещи, их кооп делит и так.
    internal static class JellyfishLink
    {
        private static bool _tried, _set, _asked;
        private static FieldInfo _send, _request;
        private static Action<string> _receive, _applyAll;
        private static Func<string, string> _hostRequest;
        private static Action<int, string> _answer;
        private static Func<string> _snapshot;
        private static readonly Action<string> OurSend = Send;
        private static readonly Func<string, int> OurRequest = Request;
        private static int _reqId, _asks, _answers;
        private static bool _requestSet;
        private static int _sent, _got, _logs;
        private static float _nextAsk;

        private static bool Init()
        {
            if (_tried) return _send != null;
            _tried = true;
            try
            {
                Type api = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    api = asm.GetType("JellyfishByEMPEROR.CoopApi", false);
                    if (api != null) break;
                }
                if (api == null) return false;
                const BindingFlags PS = BindingFlags.Public | BindingFlags.Static;
                var f = api.GetField("Send", PS);
                if (f != null && f.FieldType == typeof(Action<string>)) _send = f;
                var q = api.GetField("Request", PS);
                if (q != null && q.FieldType == typeof(Func<string, int>)) _request = q;
                var hr = api.GetMethod("HostRequest", PS, null, new[] { typeof(string) }, null);
                if (hr != null && hr.ReturnType == typeof(string)) _hostRequest = (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>), hr);
                var an = api.GetMethod("Answer", PS, null, new[] { typeof(int), typeof(string) }, null);
                if (an != null && an.ReturnType == typeof(void)) _answer = (Action<int, string>)Delegate.CreateDelegate(typeof(Action<int, string>), an);
                var rc = api.GetMethod("Receive", PS, null, new[] { typeof(string) }, null);
                if (rc != null && rc.ReturnType == typeof(void)) _receive = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), rc);
                var sn = api.GetMethod("Snapshot", PS, null, Type.EmptyTypes, null);
                if (sn != null && sn.ReturnType == typeof(string)) _snapshot = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), sn);
                var ap = api.GetMethod("ApplySnapshot", PS, null, new[] { typeof(string) }, null);
                if (ap != null && ap.ReturnType == typeof(void)) _applyAll = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), ap);
                MelonLogger.Msg($"[медузы] JellyfishByEMPEROR найден: отправка {(_send != null ? "есть" : "нет")}, приём {(_receive != null ? "есть" : "нет")}, все аквариумы {(_snapshot != null && _applyAll != null ? "есть" : "нет")}, решает хост {(_request != null && _hostRequest != null && _answer != null ? "да" : "нет")} / jellyfish mod found");
            }
            catch (Exception e) { MelonLogger.Warning("[медузы] связь с JellyfishByEMPEROR: " + e.Message); }
            return _send != null;
        }

        // Конец сессии (Session.Stop) и смена мира.
        public static void Reset()
        {
            _asked = false;
            Unset();
        }

        private static void Unset()
        {
            if (!_set) return;
            _set = false;
            try { _send?.SetValue(null, null); } catch { }
            SetRequest(false);
        }

        public static void Tick(float now)
        {
            if (!Session.InSharedWorld) { if (_set || _asked) Reset(); return; }
            if (!Init()) return;
            if (!_set)
            {
                try
                {
                    _send.SetValue(null, OurSend);
                    _set = true;
                    MelonLogger.Msg("[медузы] общий мир: изменения медуз рассылаю остальным");
                }
                catch (Exception e) { MelonLogger.Warning("[медузы] " + e.Message); }
            }
            // Запросы к хосту — только у друга; стал хостом или мод не умеет — поле пустое.
            SetRequest(Session.Mode == Mode.Client && _hostRequest != null && _answer != null);
            // Друг: один раз за вход в мир просим у хоста все аквариумы (когда уже в игре — моду есть куда их применить).
            if (Session.Mode == Mode.Client && !_asked && now >= _nextAsk && GameApi.InGame() && _applyAll != null)
            {
                _nextAsk = now + 5f;
                var w = Msg.New(MsgType.JellyfishAll);
                w.Write("");
                Session.SendToHost(Msg.Done(w));
                _asked = true;
                MelonLogger.Msg("[медузы] прошу у хоста все аквариумы мира");
            }
        }

        private static void SetRequest(bool on)
        {
            if (_request == null || on == _requestSet) return;
            try { _request.SetValue(null, on ? OurRequest : null); _requestSet = on; if (on) MelonLogger.Msg("[медузы] я друг: действия с медузой спрашиваю у хоста"); }
            catch (Exception e) { MelonLogger.Warning("[медузы] " + e.Message); }
        }

        // Мод у друга: хочу сделать с медузой вот это. Номер запроса или 0, если отправить некому.
        private static int Request(string op)
        {
            if (string.IsNullOrEmpty(op) || Session.Mode != Mode.Client || !Session.InSharedWorld || Session.Reconnecting) return 0;
            try
            {
                int id = ++_reqId;
                var w = Msg.New(MsgType.JellyfishAsk);
                w.Write(id);
                w.Write(op);
                Session.SendToHost(Msg.Done(w));
                _asks++;
                if (_logs++ < 40) MelonLogger.Msg($"[медузы] спрашиваю хоста №{id} ({op.Length} симв.)");
                return id;
            }
            catch (Exception e) { MelonLogger.Warning("[медузы] запрос хосту: " + e.Message); return 0; }
        }

        // Хост: друг просит действие с медузой. Мод решает и применяет; ответ — другу.
        public static void HostAsk(Peer peer, BinaryReader r)
        {
            int id = r.ReadInt32();
            string op = r.ReadString();
            string answer = "";
            if (Init() && _hostRequest != null)
            {
                try { answer = _hostRequest(op) ?? ""; }
                catch (Exception e) { MelonLogger.Warning("[медузы] запрос друга: " + e.Message); }
            }
            var w = Msg.New(MsgType.JellyfishAsk);
            w.Write(id);
            w.Write(answer);
            Session.SendTo(peer.Id, Msg.Done(w));
            if (_logs++ < 40) MelonLogger.Msg($"[медузы] игрок {peer.Id} спросил №{id} — ответил ({answer.Length} симв.)");
        }

        // Друг: ответ хоста на наш запрос.
        public static void ClientAnswer(BinaryReader r)
        {
            int id = r.ReadInt32();
            string answer = r.ReadString();
            _answers++;
            if (!Init() || _answer == null) return;
            try { _answer(id, answer); }
            catch (Exception e) { MelonLogger.Warning("[медузы] ответ хоста: " + e.Message); }
        }

        // Мод: медузы изменились у нас.
        private static void Send(string barrel)
        {
            if (string.IsNullOrEmpty(barrel) || !Session.InSharedWorld) return;
            try
            {
                var w = Msg.New(MsgType.Jellyfish);
                w.Write(barrel);
                Session.SendGameEvent(Msg.Done(w));
                _sent++;
                if (_logs++ < 40) MelonLogger.Msg($"[медузы] медузы изменились у меня — отправил ({barrel.Length} симв.)");
            }
            catch (Exception e) { MelonLogger.Warning("[медузы] отправка: " + e.Message); }
        }

        // Все: медузы изменились у другого игрока.
        public static void Receive(BinaryReader r)
        {
            string barrel = r.ReadString();
            if (!Init() || _receive == null || barrel.Length == 0) return;
            try
            {
                _receive(barrel);
                _got++;
                if (_logs++ < 40) MelonLogger.Msg($"[медузы] медузы изменились у другого игрока — применил ({barrel.Length} симв.)");
            }
            catch (Exception e) { MelonLogger.Warning("[медузы] приём: " + e.Message); }
        }

        // Хост: друг просит все аквариумы.
        public static void HostAll(Peer peer, BinaryReader r)
        {
            r.ReadString();
            if (!Init() || _snapshot == null) return;
            try
            {
                string all = _snapshot() ?? "";
                var w = Msg.New(MsgType.JellyfishAll);
                w.Write(all);
                Session.SendTo(peer.Id, Msg.Done(w));
                MelonLogger.Msg($"[медузы] игрок {peer.Id} просит все аквариумы — отправил ({all.Length} симв.)");
            }
            catch (Exception e) { MelonLogger.Warning("[медузы] все аквариумы: " + e.Message); }
        }

        // Друг: все аквариумы от хоста.
        public static void ClientAll(BinaryReader r)
        {
            string all = r.ReadString();
            if (!Init() || _applyAll == null) return;
            try
            {
                _applyAll(all);
                MelonLogger.Msg($"[медузы] получил от хоста все аквариумы ({all.Length} симв.)");
            }
            catch (Exception e) { MelonLogger.Warning("[медузы] все аквариумы от хоста: " + e.Message); }
        }

        public static string Stats => _tried && _send != null ? $"медузы: отправил {_sent}, получил {_got}, спросил хоста {_asks}, ответов {_answers}" : "";
    }
}
