using System;
using System.IO;
using System.Reflection;
using MelonLoader;
using CoopByEMPEROR.Net;

namespace CoopByEMPEROR.Game
{
    // Связь с модом SmokehouseByEMPEROR (коптильня: бочка GEAR_SH_Barrel) без жёсткой ссылки на его dll — как SmokesLink.
    // Сама бочка и копчёности — обычные вещи, их кооп и так делит. Что внутри бочки, огонь и дрова — состояние мода:
    //  - SmokehouseByEMPEROR.CoopApi.Send (Action<string>) — на время общего мира ставим свой делегат; мод зовёт его после
    //    каждого изменения одной бочки (заложил, вынул, дрова, разжёг, потушил, способ, забрал бочку). Строка (JSON одной
    //    бочки) уходит всем остальным как событие мира с номером (MsgType.Smokehouse: надёжно, по порядку, после обрыва
    //    связи досылается), у них — CoopApi.Receive(строка);
    //  - друг, войдя в мир хоста, просит все бочки (MsgType.SmokehouseAll, пустая строка); хост отвечает CoopApi.Snapshot(),
    //    у друга — CoopApi.ApplySnapshot.
    //  - CoopApi.Hours (Func<float>, если у мода есть) — на время общего мира даём общие часы мира (день × 24 + время
    //    суток, ActionTime.SharedTotal): по ним бочка горит и коптит у всех одинаково. Своими «часами в игре» мод считать
    //    в коопе не должен: у спавшего отдельно или делавшего долгое действие они убегают вперёд на часы (кооп их не
    //    возвращает), мясо у него «докапчивалось» бы за сон, и это состояние уходило бы всем — как было с кострами.
    //  - 0.19.64, решает хост: двое жали в одной бочке в одну долю секунды — оба получали один кусок, или полено
    //    пропадало (состояние бочки уходит целиком, позднее затирало раннее). Теперь у друга мод не меняет бочку сам, а
    //    спрашивает хоста: CoopApi.Request (Func<string,int>, ставим только другу в мире хоста; строка — действие мода,
    //    возвращаем номер запроса, 0 — не отправить) → MsgType.SmokehouseAsk → у хоста CoopApi.HostRequest(действие)
    //    (мод сам проверяет и применяет, новое состояние бочки рассылает через Send) → ответ-строка другу →
    //    CoopApi.Answer(номер, ответ). Что в строках — дело мода. Хост свои действия применяет сразу, как раньше.
    //    Мод без этих полей (до 0.1.9) работает по-старому.
    internal static class SmokehouseLink
    {
        private static bool _tried, _set, _asked;
        private static FieldInfo _send, _hours, _request;
        private static Action<string> _receive, _applyAll;
        private static Func<string, string> _hostRequest;
        private static Action<int, string> _answer;
        private static Func<string> _snapshot;
        private static readonly Action<string> OurSend = Send;
        private static readonly Func<float> OurHours = SharedHours;
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
                    api = asm.GetType("SmokehouseByEMPEROR.CoopApi", false);
                    if (api != null) break;
                }
                if (api == null) return false;
                const BindingFlags PS = BindingFlags.Public | BindingFlags.Static;
                var f = api.GetField("Send", PS);
                if (f != null && f.FieldType == typeof(Action<string>)) _send = f;
                var h = api.GetField("Hours", PS);
                if (h != null && h.FieldType == typeof(Func<float>)) _hours = h;
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
                MelonLogger.Msg($"[коптильня] SmokehouseByEMPEROR найден: отправка {(_send != null ? "есть" : "нет")}, приём {(_receive != null ? "есть" : "нет")}, все бочки {(_snapshot != null && _applyAll != null ? "есть" : "нет")}, общие часы {(_hours != null ? "берёт" : "не берёт (считает по своим)")}, решает хост {(_request != null && _hostRequest != null && _answer != null ? "да" : "нет (мод старее 0.1.9)")} / smokehouse mod found");
            }
            catch (Exception e) { MelonLogger.Warning("[коптильня] связь с SmokehouseByEMPEROR: " + e.Message); }
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
            try { _hours?.SetValue(null, null); } catch { }
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
                    _hours?.SetValue(null, OurHours);
                    _send.SetValue(null, OurSend);
                    _set = true;
                    MelonLogger.Msg("[коптильня] общий мир: изменения бочек рассылаю остальным" + (_hours != null ? ", бочки считают по общим часам мира" : ""));
                }
                catch (Exception e) { MelonLogger.Warning("[коптильня] " + e.Message); }
            }
            // Запросы к хосту — только у друга; стал хостом или мод не умеет — поле пустое.
            SetRequest(Session.Mode == Mode.Client && _hostRequest != null && _answer != null);
            // Друг: один раз за вход в мир просим у хоста все бочки (когда уже в игре — моду есть куда их применить).
            if (Session.Mode == Mode.Client && !_asked && now >= _nextAsk && GameApi.InGame() && _applyAll != null)
            {
                _nextAsk = now + 5f;
                var w = Msg.New(MsgType.SmokehouseAll);
                w.Write("");
                Session.SendToHost(Msg.Done(w));
                _asked = true;
                MelonLogger.Msg("[коптильня] прошу у хоста все бочки мира");
            }
        }

        private static void SetRequest(bool on)
        {
            if (_request == null || on == _requestSet) return;
            try { _request.SetValue(null, on ? OurRequest : null); _requestSet = on; if (on) MelonLogger.Msg("[коптильня] я друг: действия с бочкой спрашиваю у хоста"); }
            catch (Exception e) { MelonLogger.Warning("[коптильня] " + e.Message); }
        }

        // Мод у друга: хочу сделать с бочкой вот это. Номер запроса или 0, если отправить некому.
        private static int Request(string op)
        {
            if (string.IsNullOrEmpty(op) || Session.Mode != Mode.Client || !Session.InSharedWorld || Session.Reconnecting) return 0;
            try
            {
                int id = ++_reqId;
                var w = Msg.New(MsgType.SmokehouseAsk);
                w.Write(id);
                w.Write(op);
                Session.SendToHost(Msg.Done(w));
                _asks++;
                if (_logs++ < 40) MelonLogger.Msg($"[коптильня] спрашиваю хоста №{id} ({op.Length} симв.)");
                return id;
            }
            catch (Exception e) { MelonLogger.Warning("[коптильня] запрос хосту: " + e.Message); return 0; }
        }

        // Хост: друг просит действие с бочкой. Мод решает и применяет; ответ — другу.
        public static void HostAsk(Peer peer, BinaryReader r)
        {
            int id = r.ReadInt32();
            string op = r.ReadString();
            string answer = "";
            if (Init() && _hostRequest != null)
            {
                try { answer = _hostRequest(op) ?? ""; }
                catch (Exception e) { MelonLogger.Warning("[коптильня] запрос друга: " + e.Message); }
            }
            var w = Msg.New(MsgType.SmokehouseAsk);
            w.Write(id);
            w.Write(answer);
            Session.SendTo(peer.Id, Msg.Done(w));
            if (_logs++ < 40) MelonLogger.Msg($"[коптильня] игрок {peer.Id} спросил №{id} — ответил ({answer.Length} симв.)");
        }

        // Друг: ответ хоста на наш запрос.
        public static void ClientAnswer(BinaryReader r)
        {
            int id = r.ReadInt32();
            string answer = r.ReadString();
            _answers++;
            if (!Init() || _answer == null) return;
            try { _answer(id, answer); }
            catch (Exception e) { MelonLogger.Warning("[коптильня] ответ хоста: " + e.Message); }
        }

        // Общие часы мира: не убегают вперёд за сон отдельно и долгие действия этого игрока, общий сон проматывает их всем.
        private static float SharedHours()
        {
            try { return ActionTime.SharedTotal() * 24f; }
            catch { return 0f; }
        }

        // Мод: бочка изменилась у нас.
        private static void Send(string barrel)
        {
            if (string.IsNullOrEmpty(barrel) || !Session.InSharedWorld) return;
            try
            {
                var w = Msg.New(MsgType.Smokehouse);
                w.Write(barrel);
                Session.SendGameEvent(Msg.Done(w));
                _sent++;
                if (_logs++ < 40) MelonLogger.Msg($"[коптильня] бочка изменилась у меня — отправил ({barrel.Length} симв.)");
            }
            catch (Exception e) { MelonLogger.Warning("[коптильня] отправка: " + e.Message); }
        }

        // Все: бочка изменилась у другого игрока.
        public static void Receive(BinaryReader r)
        {
            string barrel = r.ReadString();
            if (!Init() || _receive == null || barrel.Length == 0) return;
            try
            {
                _receive(barrel);
                _got++;
                if (_logs++ < 40) MelonLogger.Msg($"[коптильня] бочка изменилась у другого игрока — применил ({barrel.Length} симв.)");
            }
            catch (Exception e) { MelonLogger.Warning("[коптильня] приём: " + e.Message); }
        }

        // Хост: друг просит все бочки.
        public static void HostAll(Peer peer, BinaryReader r)
        {
            r.ReadString();
            if (!Init() || _snapshot == null) return;
            try
            {
                string all = _snapshot() ?? "";
                var w = Msg.New(MsgType.SmokehouseAll);
                w.Write(all);
                Session.SendTo(peer.Id, Msg.Done(w));
                MelonLogger.Msg($"[коптильня] игрок {peer.Id} просит все бочки — отправил ({all.Length} симв.)");
            }
            catch (Exception e) { MelonLogger.Warning("[коптильня] все бочки: " + e.Message); }
        }

        // Друг: все бочки от хоста.
        public static void ClientAll(BinaryReader r)
        {
            string all = r.ReadString();
            if (!Init() || _applyAll == null) return;
            try
            {
                _applyAll(all);
                MelonLogger.Msg($"[коптильня] получил от хоста все бочки ({all.Length} симв.)");
            }
            catch (Exception e) { MelonLogger.Warning("[коптильня] все бочки от хоста: " + e.Message); }
        }

        public static string Stats => _tried && _send != null ? $"коптильня: отправил {_sent}, получил {_got}, спросил хоста {_asks}, ответов {_answers}" : "";
    }
}
