using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using TLDCoop.Game;
using UnityEngine;

namespace TLDCoop.Net
{
    // Номера событий мира. Хост нумерует каждое событие, которое меняет мир (вещь выброшена или поднята,
    // контейнер, дверь, костёр, туша, торговец, подарок…), и держит последние несколько тысяч.
    // Друг видит, что получил 1041, а потом сразу 1043, и просит 1042. После переподключения он просит
    // всё после своего последнего номера. Раньше всё, что хост сделал за время обрыва, у друга так и не появлялось,
    // а сообщение, которое Steam не принял, пропадало молча.
    //
    // Хост шлёт такое событие обёрнутым: Seq [номер журнала][номер события][событие]. Тому, кто событие и прислал,
    // уходит только номер (пустое событие), чтобы у него не было дыры в счёте.
    //
    // С 0.17.19 ничего не пропускаем: друг раз в секунду подтверждает «получил всё до N», и хост стирает из журнала
    // (сверх обычных 6000) только то, что подтвердили все подключённые. Друг с дырой в счёте не перескакивает через
    // неё, а ждёт и просит снова. Если журнал всё же переполнился (друг очень долго молчал), хост не говорит
    // «пропусти», а просит друга загрузить мир заново, как при входе.
    public static class EventLog
    {
        private const int MaxEntries = 6000, HardEntries = 30000;
        private const long MaxBytes = 24L << 20, HardBytes = 64L << 20;

        // Номер журнала: новый при каждом запуске хоста. Друг с другим номером начинает счёт заново.
        public static readonly int LogId = new System.Random().Next(1, int.MaxValue);

        private struct Entry { public uint Seq; public string Origin; public byte[] Data; }
        private static readonly LinkedList<Entry> Log = new();
        private static uint _seq;
        private static long _bytes;
        private static int _resent, _gapsAsked, _dups;

        public static uint Current => _seq;

        // Хост: номер последнего события в момент, когда игра собирала сохранение (патч на SaveGameSystem.SaveGame).
        // Всё до него уже в файле, всё после — нет. По нему друг, получивший мир, знает, с какого события продолжать.
        public static uint SavedAt { get; private set; }

        [HarmonyLib.HarmonyPatch(typeof(Il2Cpp.SaveGameSystem), nameof(Il2Cpp.SaveGameSystem.SaveGame))]
        private static class Patch_SaveGame
        {
            private static void Prefix() { SavedAt = _seq; }
        }

        public static bool Logged(byte type) => (MsgType)type switch
        {
            MsgType.ItemDrop or MsgType.ItemPick or MsgType.ItemUpdate or MsgType.ObjectState or MsgType.WorldEvent or MsgType.TraderState
                or MsgType.GiveItem or MsgType.GiveAck or MsgType.AnimalHarvest or MsgType.Feature or MsgType.AnimalArrow or MsgType.Smokehouse => true,
            _ => false,
        };

        // ---------- Хост ----------

        public static void HostSend(byte[] data, Peer except, IEnumerable<Peer> peers, Func<Peer, string> keyOf)
        {
            uint seq = ++_seq;
            string origin = except != null ? keyOf(except) : "host";
            Log.AddLast(new Entry { Seq = seq, Origin = origin, Data = data });
            _bytes += data.Length;
            Trim(peers);
            var full = Wrap(seq, data);
            byte[] mark = null;
            foreach (var p in peers)
            {
                if (!p.Greeted) continue;
                if (p == except) p.Send(mark ??= Wrap(seq, null));
                else p.Send(full);
            }
        }

        // Сверх 6000 событий (24 МБ) стираем только подтверждённое всеми подключёнными, сверх 30000 (64 МБ) — всё равно.
        private static void Trim(IEnumerable<Peer> peers)
        {
            while (Log.Count > 0 && (Log.Count > MaxEntries || _bytes > MaxBytes))
            {
                uint first = Log.First.Value.Seq;
                bool hard = Log.Count > HardEntries || _bytes > HardBytes;
                if (!hard)
                {
                    bool all = true;
                    foreach (var p in peers) if (p.Greeted && !p.Closed && (p.AckedLog != LogId || p.Acked < first)) { all = false; break; }
                    if (!all) break;
                }
                else if (Time.unscaledTime >= _nextHardWarn)
                {
                    _nextHardWarn = Time.unscaledTime + 30f;
                    MelonLogger.Warning($"[сеть] журнал событий переполнен ({Log.Count}, {_bytes >> 20} МБ): кто-то из друзей давно не подтверждает");
                }
                _bytes -= Log.First.Value.Data.Length;
                Log.RemoveFirst();
            }
        }

        private static float _nextHardWarn;

        // Хост: друг подтвердил, что получил всё до last.
        public static void HostAcked(Peer peer, BinaryReader r)
        {
            int log = r.ReadInt32();
            uint last = r.ReadUInt32();
            r.ReadUInt32();
            if (log != LogId) return;
            if (peer.AckedLog != log) { peer.AckedLog = log; peer.Acked = last; }
            else if (last > peer.Acked) peer.Acked = last;
        }

        // Хост: другу нужны события старше журнала. В своём сохранении — пусть не ждёт (SeqSkip).
        // В общем мире — просим загрузить мир заново, как при входе (не чаще раза в минуту), а не пропускать.
        public static void SendSkipOrReload(Peer peer, bool inWorld)
        {
            if (!inWorld) { SendSkip(peer); return; }
            if (Time.unscaledTime - peer.ReloadAt < 60f) return;
            peer.ReloadAt = Time.unscaledTime;
            var w = Msg.New(MsgType.SeqCtl);
            w.Write(ClientLog.KReload); w.Write(LogId); w.Write(Oldest); w.Write(0u);
            peer.Send(Msg.Done(w));
            MelonLogger.Warning($"[сеть] другу нужны события старше №{Oldest}, их в журнале уже нет: прошу его загрузить мир заново");
        }

        private static byte[] Wrap(uint seq, byte[] data)
        {
            var w = Msg.New(MsgType.Seq);
            w.Write(LogId);
            w.Write(seq);
            if (data != null) w.Write(data);
            return Msg.Done(w);
        }

        // Досылаем события from..to тому, кто просит (после обрыва или при дыре в счёте).
        // full: слать и свои события друга целиком (после передачи мира их нет ни в файле, ни у него).
        // skip: событие уже ушло другим путём (отложенные новичку) — шлём только номер, чтобы не было дыры в счёте.
        public static int HostResend(Peer peer, string key, uint from, uint to, bool full = false, HashSet<(int, ulong)> skip = null)
        {
            if (Log.Count == 0 || from > to) return 0;
            int n = 0;
            foreach (var e in Log)
            {
                if (e.Seq < from) continue;
                if (e.Seq > to) break;
                bool mark = (e.Origin == key && !full) || (skip != null && skip.Contains(Fingerprint(e.Data)));
                peer.Send(mark ? Wrap(e.Seq, null) : Wrap(e.Seq, e.Data));
                n++;
            }
            _resent += n;
            return n;
        }

        // Отпечаток содержимого события: длина и FNV-1a 64.
        public static (int, ulong) Fingerprint(byte[] data)
        {
            ulong h = 14695981039346656037UL;
            if (data != null) foreach (byte b in data) { h ^= b; h *= 1099511628211UL; }
            return (data?.Length ?? 0, h);
        }

        public static uint Oldest => Log.Count > 0 ? Log.First.Value.Seq : _seq + 1;

        // Хост: событий старше журнала уже нет — друг перестаёт их ждать (SeqSkip).
        public static void SendSkip(Peer peer)
        {
            var w = Msg.New(MsgType.SeqSkip);
            w.Write(LogId);
            w.Write(Oldest);
            peer.Send(Msg.Done(w));
        }

        public static void HostReceiveResend(Peer peer, string key, BinaryReader r, bool inWorld)
        {
            int id = r.ReadInt32();
            uint from = r.ReadUInt32(), to = r.ReadUInt32();
            if (id != LogId) return;
            if (from < Oldest) SendSkipOrReload(peer, inWorld);
            int n = HostResend(peer, key, from, Math.Min(to, _seq));
            MelonLogger.Msg($"[сеть] дослал пропущенные события {from}–{to}: {n}" + (from < Oldest ? $" (старше {Oldest} уже нет в журнале)" : ""));
        }

        // ---------- Друг ----------

        private static int _clientLog;
        private static uint _last;
        // События, пришедшие после дыры в счёте: ждут, пока хост дошлёт пропущенное, и применяются по порядку.
        // Иначе старое досланное ложилось поверх нового (дверь открыли и закрыли — осталась открытой).
        private static readonly SortedDictionary<uint, byte[]> Held = new();
        // Друг: жду пропущенные события хоста (Game/SyncCheck.cs).
        public static bool ClientHasGap => Held.Count > 0;
        private static float _heldSince, _nextAsk, _nextAck, _nextWaitLog, _reloadAt = -100f;
        private const float HoldMax = 60f;
        private const int HeldMax = 20000;

        public static void WriteHello(BinaryWriter w)
        {
            w.Write(_clientLog);
            w.Write(_last);
        }

        // Хост в приветствии: какой у него журнал и до какого номера. Журнал тот же — счёт продолжаем,
        // хост сам дошлёт пропущенное. Другой (новый хост, первый вход) — начинаем с его текущего номера.
        // true — журнал был и сменился: игра хоста перезапустилась, его мир откатился к сохранению.
        public static bool ClientWelcome(int logId, uint current)
        {
            if (logId == _clientLog && _clientLog != 0) return false;
            bool changed = _clientLog != 0;
            _clientLog = logId;
            _last = current;
            Held.Clear();
            return changed;
        }

        // Друг получил мир хоста, сохранённый на событии cut: счёт с него, остальное хост дошлёт сам.
        public static void ClientStartAt(int logId, uint cut)
        {
            _clientLog = logId;
            _last = cut;
            Held.Clear();
            MelonLogger.Msg($"[сеть] мир хоста сохранён на событии №{cut}, дальше — по журналу");
        }

        // Событие с номером. apply получает события строго по порядку (inner пустой — своё событие, только номер).
        public static void ClientReceive(int logId, uint seq, byte[] inner, Action<byte[]> apply)
        {
            if (logId != _clientLog) { _clientLog = logId; _last = seq; Held.Clear(); Deliver(inner, apply); return; }
            if (seq <= _last || Held.ContainsKey(seq)) { _dups++; return; }
            if (seq == _last + 1)
            {
                _last = seq;
                Deliver(inner, apply);
                Drain(apply);
                return;
            }
            if (Held.Count == 0) _heldSince = Time.unscaledTime;
            Held[seq] = inner ?? Array.Empty<byte>();
            AskGap();
        }

        private static void Deliver(byte[] inner, Action<byte[]> apply)
        {
            if (inner == null || inner.Length == 0) return;
            try { apply(inner); } catch (Exception e) { MelonLogger.Warning("[сеть] событие мира: " + e.Message); }
        }

        private static void Drain(Action<byte[]> apply)
        {
            int n = 0;
            while (Held.Count > 0)
            {
                uint first = FirstHeld();
                if (first != _last + 1) break;
                var d = Held[first];
                Held.Remove(first);
                _last = first;
                Deliver(d, apply);
                n++;
            }
            if (n > 0) MelonLogger.Msg($"[сеть] дыра в счёте закрылась, применил по порядку ещё {n}");
            if (Held.Count > 0) _heldSince = Time.unscaledTime;
        }

        private static uint FirstHeld() { foreach (var k in Held.Keys) return k; return 0; }

        private static void AskGap()
        {
            float now = Time.unscaledTime;
            if (Held.Count == 0 || now < _nextAsk) return;
            _nextAsk = now + 2f;
            uint upTo = FirstHeld() - 1;
            var w = Msg.New(MsgType.SeqResend);
            w.Write(_clientLog);
            w.Write(_last + 1);
            w.Write(upTo);
            Session.SendControl(Msg.Done(w));
            _gapsAsked++;
            if (now >= _nextWaitLog)
            {
                _nextWaitLog = now + 15f;
                MelonLogger.Msg($"[сеть] пропущены события {_last + 1}–{upTo}, попросил дослать (отложено {Held.Count})");
            }
        }

        // Раз в секунду: подтверждаем хосту, до какого номера всё получили; дыра не закрылась — спрашиваем снова.
        // Не перескакиваем: хост держит неподтверждённое и дошлёт. Если за минуту так и не дошло (хост не отвечает
        // на просьбы), в общем мире загружаем его мир заново, в своём — применяем пришедшее по порядку.
        public static void ClientTick(Action<byte[]> apply)
        {
            float now = Time.unscaledTime;
            if (_clientLog != 0 && now >= _nextAck)
            {
                _nextAck = now + 1f;
                var w = Msg.New(MsgType.SeqCtl);
                w.Write(ClientLog.KHostAck); w.Write(_clientLog); w.Write(_last); w.Write(0u);
                Session.SendControl(Msg.Done(w));
            }
            if (Held.Count == 0) return;
            if (now - _heldSince < HoldMax && Held.Count < HeldMax) { AskGap(); return; }
            if (Session.InSharedWorld && GameApi.InGame()) { Reload($"события {_last + 1}–{FirstHeld() - 1} так и не дошли за минуту"); return; }
            GiveUp(apply, "хост так и не дослал");
        }

        // Хост: нужных событий в журнале уже нет — загружаем его мир заново (в своём сохранении — просто не ждём).
        public static void ClientReload(BinaryReader r, Action<byte[]> apply)
        {
            int id = r.ReadInt32();
            uint from = r.ReadUInt32();
            r.ReadUInt32();
            if (id != _clientLog) return;
            if (Session.InSharedWorld && GameApi.InGame()) { Reload($"у хоста в журнале уже нет событий до №{from}"); return; }
            if (from > _last + 1) { _last = from - 1; Drain(apply); }
        }

        private static void Reload(string why)
        {
            float now = Time.unscaledTime;
            if (now - _reloadAt < 60f) return;
            _reloadAt = now;
            MelonLogger.Warning($"[сеть] {why}: загружаю мир хоста заново, чтобы ничего не пропустить");
            GameApi.HudMessage("Не все изменения мира дошли — загружаю мир хоста заново");
            Held.Clear();
            Session.LoadHostWorld();
        }

        private static void GiveUp(Action<byte[]> apply, string why)
        {
            MelonLogger.Msg($"[сеть] события {_last + 1}–{FirstHeld() - 1} пропали ({why}): применяю пришедшие после них и спрашиваю локацию заново");
            foreach (var kv in new List<KeyValuePair<uint, byte[]>>(Held)) { _last = kv.Key; Deliver(kv.Value, apply); }
            Held.Clear();
            try { SceneSnapshot.AskAgain(); } catch { }
        }

        // Хост: событий до from в журнале уже нет — не ждём их.
        public static void ClientSkipTo(BinaryReader r, Action<byte[]> apply)
        {
            int id = r.ReadInt32();
            uint from = r.ReadUInt32();
            if (id != _clientLog || from <= _last + 1) return;
            MelonLogger.Msg($"[сеть] у хоста в журнале нет событий {_last + 1}–{from - 1}, продолжаю с №{from}");
            _last = from - 1;
            Drain(apply);
            try { SceneSnapshot.AskAgain(); } catch { }
        }

        public static void Reset()
        {
            _clientLog = 0;
            _last = 0;
            Held.Clear();
        }

        public static string Stats => Session.Mode == Mode.Host
            ? $"события мира: №{_seq}, в журнале {Log.Count}, дослано {_resent}"
            : $"события мира: №{_last}, просил дослать {_gapsAsked}, дублей {_dups}, ждут дыры {Held.Count}";
    }
}
