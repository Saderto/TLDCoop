using System;
using System.IO;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Тащить раненого: пока друг лежит «ранен», его можно утащить в укрытие, подальше от волка.
    // Рядом с раненым нажми клавишу «тащить» (по умолчанию G): ты идёшь, он едет за тобой по земле в метре позади.
    // Ещё раз G — отпустить. Пока его тащат, таймер раненого стоит, как при лечении.
    // Тащащий 10 раз в секунду шлёт раненому, где он и куда смотрит (Drag, берёт адресат).
    public static class Drag
    {
        private const float Reach = 2.5f, LetGo = 6f, Behind = 1.1f;
        private static int _target = -1;
        private static float _nextSend;
        // У раненого: кто тащит и куда.
        private static int _by = -1;
        private static float _byUntil;
        private static Vector3 _to;

        public static bool Dragging => _target >= 0;
        public static bool BeingDragged => _by >= 0 && Time.unscaledTime < _byUntil;
        public static string ByName => _by >= 0 && Session.Players.TryGetValue(_by, out var p) ? p.Name : "друг";

        // Тащащий: каждый кадр из Together.Tick (downed — раненый рядом, если есть).
        public static void Tick(Player downed)
        {
            bool key = Input.GetKeyDown(Config.DragKey) && !UI.Widgets.Typing && !UI.CoopWindow.Open;
            if (_target < 0)
            {
                if (key && downed != null && !Death.IsDowned) Start(downed);
                return;
            }
            if (!Session.Players.TryGetValue(_target, out var pl) || pl.Action != Activity.Downed || !Session.With(pl, GameApi.Scene()) || Death.IsDowned) { Stop("друг уже не лежит"); return; }
            if (!GameApi.TryGetPlayer(out float x, out float y, out float z, out float yaw, out _)) return;
            var me = new Vector3(x, y, z);
            if ((new Vector3(pl.X, pl.Y, pl.Z) - me).sqrMagnitude > LetGo * LetGo) { Stop("друг слишком далеко"); return; }
            if (key) { Stop("отпустил"); return; }
            float now = Time.unscaledTime;
            if (now < _nextSend) return;
            _nextSend = now + 0.1f;
            var w = Msg.New(MsgType.Drag);
            w.Write((byte)Session.LocalId);
            w.Write((byte)_target);
            w.Write(true);
            w.Write(x); w.Write(y); w.Write(z); w.Write(yaw);
            Session.SendFast(Msg.Done(w));
        }

        private static void Start(Player downed)
        {
            _target = downed.Id;
            GameApi.HudMessage($"Тащишь {downed.Name}. {Config.DragKey} — отпустить");
            MelonLogger.Msg("[смерть] тащу " + downed.Name);
            Chronicle.Add($"{Session.LocalName} тащит раненого {downed.Name}");
        }

        private static void Stop(string why)
        {
            if (_target < 0) return;
            var w = Msg.New(MsgType.Drag);
            w.Write((byte)Session.LocalId);
            w.Write((byte)_target);
            w.Write(false);
            w.Write(0f); w.Write(0f); w.Write(0f); w.Write(0f);
            Session.SendGameEvent(Msg.Done(w));
            MelonLogger.Msg("[смерть] перестал тащить: " + why);
            _target = -1;
        }

        public static string Hint(Player downed) => _target >= 0 ? $" · {Config.DragKey} — отпустить" : downed != null ? $" · {Config.DragKey} — тащить" : "";

        // Раненый: нас тащат.
        public static void Receive(BinaryReader r)
        {
            int from = r.ReadByte();
            int to = r.ReadByte();
            bool on = r.ReadBoolean();
            float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle(), yaw = r.ReadSingle();
            if (to != Session.LocalId || !Death.IsDowned) return;
            if (!on) { if (_by == from) _by = -1; return; }
            if (_by != from) { GameApi.HudMessage(Session.Players.TryGetValue(from, out var p) ? p.Name + " тащит тебя" : "Тебя тащат"); _by = from; }
            _byUntil = Time.unscaledTime + 0.6f;
            _to = new Vector3(x, y, z) - Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * Behind;
        }

        // Раненый: каждый кадр едем за тащащим, по земле.
        public static void LateTick()
        {
            if (!BeingDragged || !Death.IsDowned) return;
            var t = GameManager.GetPlayerTransform();
            if (t == null) return;
            var target = _to;
            try
            {
                if (Physics.Raycast(target + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)) target.y = hit.point.y;
            }
            catch { }
            var p = Vector3.Lerp(t.position, target, Mathf.Clamp01(Time.deltaTime * 6f));
            t.position = p;
        }

        public static void Reset() { _target = -1; _by = -1; }
    }
}
