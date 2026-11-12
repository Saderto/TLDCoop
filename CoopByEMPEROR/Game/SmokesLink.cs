using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Связь с модом SmokesByEMPEROR (сигареты, сигары, трубки) без жёсткой ссылки на его dll.
    // Его зажжённая вещь — копия факела только ради рук от первого лица; у фигуры она в пальцах (сигарета, сигара)
    // или в кулаке (трубка), по фазе затяжки рука подносит её ко рту, трубка может остаться в зубах без руки.
    // LocalPhase: 0 — в руке, 1 — рука у рта (затяжка), 2 — трубка в зубах, рука опущена (уходит флагами позиции).
    // HeldLook: копии вещи для руки фигуры мод даёт «ручную» модель (начало — мундштук/фильтр, +Z к горящему концу)
    // и огонёк на горящем конце; возвращает её длину по Z. RemotePhase: фаза фигуры сменилась (выдох после затяжки).
    internal static class SmokesLink
    {
        public const string Prefix = "GEAR_SeagullSmoke_";
        private static bool _tried;
        private static Func<int> _phase;
        private static Func<GameObject, string, float> _look;
        private static Action<GameObject, int> _remote;
        private static Action<GameObject, bool> _voice;
        private static readonly Dictionary<string, float> Lengths = new();

        public static bool IsLitSmoke(string prefab) => prefab != null && prefab.StartsWith(Prefix + "Lit", StringComparison.Ordinal);

        private static void Init()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                Type api = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    api = asm.GetType("SmokesByEMPEROR.CoopApi", false);
                    if (api != null) break;
                }
                if (api == null) return;
                const BindingFlags PS = BindingFlags.Public | BindingFlags.Static;
                var m = api.GetMethod("LocalPhase", PS, null, Type.EmptyTypes, null);
                if (m != null && m.ReturnType == typeof(int)) _phase = (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), m);
                var l = api.GetMethod("HeldLook", PS, null, new[] { typeof(GameObject), typeof(string) }, null);
                if (l != null && l.ReturnType == typeof(float)) _look = (Func<GameObject, string, float>)Delegate.CreateDelegate(typeof(Func<GameObject, string, float>), l);
                var r = api.GetMethod("RemotePhase", PS, null, new[] { typeof(GameObject), typeof(int) }, null);
                if (r != null && r.ReturnType == typeof(void)) _remote = (Action<GameObject, int>)Delegate.CreateDelegate(typeof(Action<GameObject, int>), r);
                // RemoteVoice (SmokesByEMPEROR 0.1.79+): выдох фигуры женским голосом, если персонаж друга женский.
                var v = api.GetMethod("RemoteVoice", PS, null, new[] { typeof(GameObject), typeof(bool) }, null);
                if (v != null && v.ReturnType == typeof(void)) _voice = (Action<GameObject, bool>)Delegate.CreateDelegate(typeof(Action<GameObject, bool>), v);
                MelonLogger.Msg($"[курение] SmokesByEMPEROR найден: фаза {(_phase != null ? "есть" : "нет")}, модель в руке {(_look != null ? "есть" : "нет")}, выдох {(_remote != null ? "есть" : "нет")}, голос выдоха {(_voice != null ? "есть" : "нет")}");
            }
            catch (Exception e) { MelonLogger.Warning("[курение] связь с SmokesByEMPEROR: " + e.Message); }
        }

        public static int LocalPhase()
        {
            Init();
            if (_phase == null) return 0;
            try { return _phase(); } catch { return 0; }
        }

        // Шаблон вещи для руки фигуры только что собран: ставим «ручную» модель.
        public static void Look(GameObject template, string prefab)
        {
            if (!IsLitSmoke(prefab)) return;
            Init();
            if (_look == null) return;
            try { float len = _look(template, prefab); if (len > 0.005f) Lengths[prefab] = len; } catch { }
        }

        public static float Length(string prefab) => Lengths.TryGetValue(prefab ?? "", out var l) ? l : 0f;

        public static void Remote(GameObject copy, int phase, bool female)
        {
            Init();
            if (copy == null) return;
            // Голос ставим перед фазой: выдох, который выпустит смена фазы, уже звучит голосом этой фигуры.
            if (_voice != null) { try { _voice(copy, female); } catch { } }
            if (_remote == null) return;
            try { _remote(copy, phase); } catch { }
        }
    }
}
