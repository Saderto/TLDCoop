using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Связь с модом CrankLampByEMPEROR (фонарь с ручкой, GEAR_Flashlight_CrankLamp) без жёсткой ссылки на его dll — как SmokesLink.
    // LocalCranking: этот игрок сейчас крутит ручку (держит ПКМ) — уходит флагом HeldItems.FlagCrank вместе с вещью в руке.
    // RemoteCrank(copy, on): фигура друга начала или перестала крутить; copy — фонарь в руке фигуры. Мод играет звук
    // ручки от этой копии и крутит её кость «HandCrankLight:Crank_jnt», если она у копии есть.
    internal static class CrankLampLink
    {
        public const string Prefix = "GEAR_Flashlight_CrankLamp";
        private static bool _tried;
        private static Func<bool> _local;
        private static Action<GameObject, bool> _remote;

        public static bool IsCrankLamp(string prefab) => prefab != null && prefab.StartsWith(Prefix, StringComparison.Ordinal);

        private static void Init()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                Type api = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    api = asm.GetType("CrankLampByEMPEROR.CoopApi", false);
                    if (api != null) break;
                }
                if (api == null) return;
                const BindingFlags PS = BindingFlags.Public | BindingFlags.Static;
                var l = api.GetMethod("LocalCranking", PS, null, Type.EmptyTypes, null);
                if (l != null && l.ReturnType == typeof(bool)) _local = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), l);
                var r = api.GetMethod("RemoteCrank", PS, null, new[] { typeof(GameObject), typeof(bool) }, null);
                if (r != null && r.ReturnType == typeof(void)) _remote = (Action<GameObject, bool>)Delegate.CreateDelegate(typeof(Action<GameObject, bool>), r);
                MelonLogger.Msg($"[фонарь] CrankLampByEMPEROR найден: свой ручка {(_local != null ? "есть" : "нет")}, у фигуры {(_remote != null ? "есть" : "нет")} / crank lamp mod found");
            }
            catch (Exception e) { MelonLogger.Warning("[фонарь] связь с CrankLampByEMPEROR: " + e.Message); }
        }

        public static bool LocalCranking()
        {
            Init();
            if (_local == null) return false;
            try { return _local(); } catch { return false; }
        }

        public static void Remote(GameObject copy, bool on)
        {
            if (copy == null) return;
            Init();
            if (_remote == null) return;
            try { _remote(copy, on); } catch { }
        }
    }
}
