using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Полёт чужой стрелы (0.19.23). Раньше остальные стрелу в полёте не видели: она появлялась сразу там, где легла.
    // Теперь стрелявший в момент выстрела рассылает, откуда, куда и с какой скоростью она полетела, а у остальных летит
    // одна лишь модель стрелы: без вещи, без коллайдеров, без урона. Она падает под силой тяжести, втыкается
    // в первое, во что попала, и через пару секунд исчезает — к этому времени приходит настоящая стрела там, где легла
    // (Game/WorldExtra.cs). Урон зверю, как и раньше, идёт отдельным попаданием (Game/AnimalSync.cs).
    public static class ArrowFlight
    {
        private const float StuckFor = 2.5f;     // сколько торчит воткнувшаяся модель
        private const float MaxFlight = 6f;       // дольше не летит — убираем
        private const float IgnoreFirst = 0.03f;  // первые доли секунды не втыкаем (рядом фигура самого стрелявшего)

        private sealed class Flight
        {
            public string Prefab, Scene;
            public Vector3 Pos, Vel;
            public Quaternion Rot0;
            public Vector3 Vel0;
            public float Drag;
            public float Age;
            public float StuckAt = -1f;
            public GameObject Go;
        }

        private static readonly List<Flight> Flights = new();
        private static readonly List<GearItem> Pending = new();   // свои стрелы: скорость читаем кадром позже
        private static int _sent, _shown;
        public static string Stats => $"стрел в полёте отправил {_sent}, показал {_shown}";

        // ArrowItem.Fire: скорость стреле игра даёт в физике, поэтому читаем её в следующем кадре.
        internal static void OnFired(ArrowItem arrow)
        {
            if (arrow == null || Session.Mode == Mode.Off || !Session.InSharedWorld || WorldObjects.Applying) return;
            try { if (!arrow.BelongsToPlayer()) return; } catch { }
            var gi = arrow.GetComponent<GearItem>();
            if (gi != null) Pending.Add(gi);
        }

        public static void Tick(float now)
        {
            if (Pending.Count > 0) SendPending();
            if (Flights.Count > 0) Fly(Time.deltaTime);
            FriendlyFire.TickArrows();
        }

        private static void SendPending()
        {
            var list = new List<GearItem>(Pending);
            Pending.Clear();
            foreach (var gi in list)
            {
                try
                {
                    if (gi == null) continue;
                    var rb = gi.GetComponent<Rigidbody>();
                    Vector3 vel = rb != null ? rb.velocity : Vector3.zero;
                    if (vel.sqrMagnitude < 1f) continue;   // физика ещё не толкнула или стрела уже легла
                    var t = gi.transform;
                    var w = Msg.New(MsgType.ArrowFlight);
                    w.Write(ItemSync.SceneKey());
                    w.Write((byte)Session.LocalId);
                    w.Write(ItemSync.PrefabName(gi));
                    var p = t.position; var q = t.rotation;
                    w.Write(p.x); w.Write(p.y); w.Write(p.z);
                    w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w);
                    w.Write(vel.x); w.Write(vel.y); w.Write(vel.z);
                    w.Write(rb != null ? rb.drag : 0f);
                    Session.SendGameEvent(Msg.Done(w));
                    _sent++;
                }
                catch (Exception e) { MelonLogger.Warning("[стрела] полёт: " + e.Message); }
            }
        }

        // Все: чужая стрела вылетела.
        public static void Receive(BinaryReader r)
        {
            string scene = r.ReadString();
            int shooter = r.ReadByte();
            string prefab = r.ReadString();
            var p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var q = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var v = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            float drag = r.ReadSingle();
            if (shooter == Session.LocalId || !GameApi.InGame() || scene != ItemSync.SceneKey()) return;
            try
            {
                var me = GameManager.GetPlayerTransform();
                if (me != null && (me.position - p).sqrMagnitude > 400f * 400f) return;   // далеко — не видно
            }
            catch { }
            if (Flights.Count > 30) return;
            Flights.Add(new Flight { Prefab = prefab, Scene = scene, Pos = p, Vel = v, Vel0 = v, Rot0 = q, Drag = drag });
        }

        private static void Fly(float dt)
        {
            if (dt <= 0f) return;
            for (int i = Flights.Count - 1; i >= 0; i--)
            {
                var f = Flights[i];
                f.Age += dt;
                try
                {
                    if (f.Scene != ItemSync.SceneKey()) { Remove(i); continue; }
                    if (f.Go == null && f.StuckAt < 0f) Show(f);
                    if (f.StuckAt >= 0f)
                    {
                        if (f.Age - f.StuckAt > StuckFor || f.Go == null) { Remove(i); }
                        continue;
                    }
                    if (f.Age > MaxFlight) { Remove(i); continue; }
                    var from = f.Pos;
                    f.Vel += Physics.gravity * dt;
                    if (f.Drag > 0f) f.Vel *= Mathf.Clamp01(1f - f.Drag * dt);
                    var step = f.Vel * dt;
                    float len = step.magnitude;
                    if (f.Age > IgnoreFirst && len > 0.0001f
                        && Physics.Raycast(from, step / len, out var hit, len, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        // Воткнулась: остаётся там, куда попала (и едет вместе с тем, во что попала, например со зверем).
                        f.Pos = hit.point;
                        f.StuckAt = f.Age;
                        if (f.Go != null)
                        {
                            f.Go.transform.position = hit.point;
                            try { f.Go.transform.SetParent(hit.transform, true); } catch { }
                        }
                        continue;
                    }
                    f.Pos = from + step;
                    if (f.Go != null)
                    {
                        f.Go.transform.position = f.Pos;
                        // Поворот: как у стрелы при выстреле, повёрнутый вслед за скоростью (ось модели знать не нужно).
                        if (f.Vel.sqrMagnitude > 0.01f) f.Go.transform.rotation = Quaternion.FromToRotation(f.Vel0, f.Vel) * f.Rot0;
                    }
                }
                catch { Remove(i); }
            }
        }

        // Модель стрелы без скриптов и коллайдеров (та же, что у вещей в руках фигур). Пока грузится — летит невидимой.
        private static void Show(Flight f)
        {
            var t = HeldItems.Template(f.Prefab);
            if (t == null) return;
            var go = UnityEngine.Object.Instantiate(t);
            go.name = "TLDCoop_Arrow";
            go.transform.localScale = t.transform.lossyScale;
            go.transform.position = f.Pos;
            go.transform.rotation = f.Vel.sqrMagnitude > 0.01f ? Quaternion.FromToRotation(f.Vel0, f.Vel) * f.Rot0 : f.Rot0;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(c);
            go.SetActive(true);
            f.Go = go;
            _shown++;
        }

        private static void Remove(int i)
        {
            var f = Flights[i];
            Flights.RemoveAt(i);
            if (f.Go != null) { try { UnityEngine.Object.Destroy(f.Go); } catch { } }
        }

        public static void Reset()
        {
            for (int i = Flights.Count - 1; i >= 0; i--) Remove(i);
            Pending.Clear();
        }
    }

    [HarmonyPatch(typeof(ArrowItem), nameof(ArrowItem.Fire))]
    internal static class Patch_ArrowFlight
    {
        private static void Postfix(ArrowItem __instance)
        {
            try { ArrowFlight.OnFired(__instance); } catch { }
        }
    }
}
