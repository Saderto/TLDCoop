using System;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Урон по своим (правило хоста, по умолчанию выключено).
    // У фигур друзей нет коллайдеров, поэтому попадание считаем сами: в момент выстрела (ружьё, револьвер,
    // ракетница, стрела) берём луч из камеры и смотрим, проходит ли он через фигуру друга раньше, чем
    // упирается в стену или землю. Попал — другу уходит сообщение, и его игра снимает здоровье
    // (в голову — больше). Если здоровье кончилось, дальше как обычная смерть: «ранен», если друг рядом.
    public static class FriendlyFire
    {
        private const float Radius = 0.32f, MaxRange = 300f;

        private static bool On => Session.Mode != Mode.Off && Session.InSharedWorld && Session.CurrentRules.FriendlyFire && GameApi.InGame();

        internal static void OnShot(string weapon)
        {
            if (!On) return;
            try
            {
                var cam = GameManager.GetMainCamera();
                if (cam == null) return;
                var o = cam.transform.position;
                var d = cam.transform.forward;
                float wall = Physics.Raycast(o, d, out RaycastHit hit, MaxRange, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : MaxRange;
                TryHit(o, d, wall, weapon);
            }
            catch (Exception e) { MelonLogger.Warning("[урон] " + e.Message); }
        }

        // Стрела (0.19.23): раньше попадание считалось лучом из камеры в миг выстрела — прямо, без дуги и без времени
        // полёта. Теперь ведём саму стрелу: каждый кадр отрезок от прошлой её точки до новой; задела фигуру — попала.
        private sealed class Tracked { public ArrowItem Arrow; public Vector3 Last; public float Until; }
        private static readonly System.Collections.Generic.List<Tracked> Arrows = new();

        internal static void OnArrow(ArrowItem a)
        {
            if (a == null || !On) return;
            try { if (!a.BelongsToPlayer()) return; } catch { }
            Arrows.Add(new Tracked { Arrow = a, Last = a.transform.position, Until = Time.unscaledTime + 6f });
        }

        internal static void TickArrows()
        {
            if (Arrows.Count == 0) return;
            float now = Time.unscaledTime;
            for (int i = Arrows.Count - 1; i >= 0; i--)
            {
                var t = Arrows[i];
                try
                {
                    if (t.Arrow == null || now > t.Until || !On) { Arrows.RemoveAt(i); continue; }
                    var p = t.Arrow.transform.position;
                    var seg = p - t.Last;
                    float len = seg.magnitude;
                    if (len > 0.001f && TryHit(t.Last, seg / len, len, "arrow")) { Arrows.RemoveAt(i); continue; }
                    t.Last = p;
                    // Остановилась (воткнулась, легла) — дальше не ведём.
                    bool flying = true;
                    try { flying = t.Arrow.InFlight(false); } catch { }
                    if (!flying && now > t.Until - 5.8f) Arrows.RemoveAt(i);
                }
                catch { Arrows.RemoveAt(i); }
            }
        }

        // Луч o + d·t (d единичный) до дальности range: первая фигура друга на нём получает попадание.
        private static bool TryHit(Vector3 o, Vector3 d, float range, string weapon)
        {
            {
                float wall = range;
                string scene = GameApi.Scene();
                Player best = null;
                float bestT = wall;
                bool head = false;
                foreach (var p in Session.Players.Values)
                {
                    if (!Session.With(p, scene) || !p.Puppet.Alive || p.Action == Activity.Downed || p.Action == Activity.Dead) continue;
                    var feet = p.Puppet.LabelPoint - new Vector3(0f, 1.95f, 0f);
                    float h = p.Crouched ? 1.15f : 1.8f;
                    if (!Hits(o, d, feet, h, out float t, out float y) || t >= bestT) continue;
                    best = p;
                    bestT = t;
                    head = y > feet.y + h - 0.28f;
                }
                if (best == null) return false;
                int dmg = Damage(weapon, head, bestT);
                var w = Msg.New(MsgType.Hit);
                w.Write((byte)Session.LocalId);
                w.Write((byte)best.Id);
                w.Write((byte)dmg);
                w.Write(head);
                Session.SendGameEvent(Msg.Done(w));
                GameApi.HudMessage($"Ты попал в {best.Name}" + (head ? " (в голову)" : ""));
                MelonLogger.Msg($"[урон] попал в {best.Name}: {weapon}, {dmg}{(head ? ", в голову" : "")}");
                return true;
            }
        }

        // Луч против вертикального столбика фигуры: ближе всего по горизонтали, на высоте тела.
        private static bool Hits(Vector3 o, Vector3 d, Vector3 feet, float h, out float t, out float y)
        {
            t = 0f; y = 0f;
            var rel = o - feet;
            float dxz = d.x * d.x + d.z * d.z;
            if (dxz < 1e-6f) return false;
            t = -(rel.x * d.x + rel.z * d.z) / dxz;
            if (t <= 0f) return false;
            var p = o + d * t;
            float hx = p.x - feet.x, hz = p.z - feet.z;
            if (hx * hx + hz * hz > Radius * Radius) return false;
            y = p.y;
            return y >= feet.y && y <= feet.y + h + 0.1f;
        }

        private static int Damage(string weapon, bool head, float dist)
        {
            int dmg = weapon switch { "rifle" => 50, "revolver" => 35, "flare" => 15, "arrow" => 40, _ => 30 };
            // 0.19.32: дробь (мод TLDShotgun) — 70 в упор до 10 м, к 35 м падает до 10, дальше 10.
            if (weapon == "shotgun") dmg = dist <= 10f ? 70 : dist >= 35f ? 10 : (int)Math.Round(70f - (dist - 10f) / 25f * 60f);
            return Math.Min(100, head ? dmg * 2 : dmg);
        }

        public static void Receive(BinaryReader r)
        {
            int from = r.ReadByte(), target = r.ReadByte(), dmg = r.ReadByte();
            bool head = r.ReadBoolean();
            if (target != Session.LocalId || !Session.InSharedWorld || !Session.CurrentRules.FriendlyFire || !GameApi.InGame()) return;
            if (Session.Players.TryGetValue(from, out var shooter) && !Session.With(shooter, GameApi.Scene())) return;
            string name = Session.Players.TryGetValue(from, out var p) ? p.Name : "Друг";
            try
            {
                var cond = GameManager.GetConditionComponent();
                if (cond == null || cond.m_NeverDie && Death.IsDowned) return;
                cond.AddHealth(-cond.m_MaxHP * dmg / 100f, DamageSource.BulletWound);
                GameApi.HudMessage($"{name} попал в тебя" + (head ? " — в голову" : ""));
                MelonLogger.Msg($"[урон] {name} попал в меня: {dmg}%{(head ? ", в голову" : "")}");
            }
            catch (Exception e) { MelonLogger.Warning("[урон] " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(GunItem), nameof(GunItem.Fired))]
    internal static class Patch_FriendlyGun
    {
        private static void Postfix(GunItem __instance)
        {
            string n = "";
            try { if (__instance.m_GunType == GunType.Camera) return; } catch { }   // снимок фотоаппаратом — не выстрел
            try { n = ItemSync.PrefabName(__instance.GetComponent<GearItem>()); } catch { }
            FriendlyFire.OnShot(n.StartsWith("GEAR_Rifle") ? "rifle" : n == "GEAR_Shotgun" ? "shotgun" : n.StartsWith("GEAR_Revolver") ? "revolver" : n == "GEAR_FlareGun" ? "flare" : "gun");
        }
    }

    [HarmonyPatch(typeof(ArrowItem), nameof(ArrowItem.Fire))]
    internal static class Patch_FriendlyArrow
    {
        private static void Postfix(ArrowItem __instance) { try { FriendlyFire.OnArrow(__instance); } catch { } }
    }
}
