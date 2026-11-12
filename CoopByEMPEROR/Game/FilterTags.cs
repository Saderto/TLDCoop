using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Обычные (не квестовые) вещи с метками занятий игра прячет, если метки нет в разрешённом списке
    // (MissionServicesManager.m_MissionObjectFilterTags). Список строится из прогресса заданий/Баек, а он у каждого
    // игрока свой — у друга прятались книги, которые у хоста лежат («Растопка», «Кулинария», «Огнестрельное оружие»).
    // Мир общий, значит, есть ли такая вещь, решает список хоста: хост рассылает его, а у друга при регистрации
    // безымянного (не квестового) объекта на время проверки подставляется список хоста. Квестовые вещи — по своему.
    public static class FilterTags
    {
        private static string _sent = "";
        private static float _next;
        private static int _sentTo;
        private static string[] _host;
        internal static int Swapped;

        public static void Reset() { _host = null; _sent = ""; _sentTo = 0; }

        private static string[] Mine()
        {
            try
            {
                var msm = GameManager.GetMissionServicesManager();
                var arr = msm?.m_MissionObjectFilterTags;
                if (arr == null) return Array.Empty<string>();
                var res = new string[arr.Length];
                for (int i = 0; i < arr.Length; i++) res[i] = arr[i] ?? "";
                return res;
            }
            catch { return null; }
        }

        // Хост: каждый кадр. Рассылаем при смене списка, при новом игроке и раз в 30 с.
        public static void Tick(float now)
        {
            if (Session.Mode != Mode.Host || !GameApi.InGame() || Session.Players.Count == 0) return;
            if (now < _next && Session.Players.Count == _sentTo) return;
            _next = now + 5f;
            var tags = Mine();
            if (tags == null) return;
            string joined = string.Join(";", tags);
            bool force = Session.Players.Count != _sentTo || now % 30f < 5f;
            if (joined == _sent && !force) return;
            bool changed = joined != _sent;
            _sent = joined;
            _sentTo = Session.Players.Count;
            var w = Msg.New(MsgType.FilterTags);
            w.Write(joined);
            Session.SendGameEvent(Msg.Done(w));
            if (changed) MelonLogger.Msg($"[метки] разрешённые метки предметов: «{joined}» — разослал");
        }

        // Друг: список хоста.
        public static void Receive(BinaryReader r)
        {
            string joined = r.ReadString();
            var tags = joined.Length == 0 ? Array.Empty<string>() : joined.Split(';');
            bool changed = _host == null || string.Join(";", _host) != joined;
            _host = tags;
            if (changed)
            {
                var mine = Mine();
                MelonLogger.Msg($"[метки] у хоста разрешены «{joined}», у меня «{(mine == null ? "?" : string.Join(";", mine))}»");
            }
        }

        internal static bool UseHost(MissionObjectIdentifier moi)
            => _host != null && moi != null && Session.Mode == Mode.Client && Session.InSharedWorld && moi.m_IsBlankId && moi.m_NumTags != 0;

        internal static Il2CppStringArray HostArray()
        {
            var a = new Il2CppStringArray(_host.Length);
            for (int i = 0; i < _host.Length; i++) a[i] = _host[i];
            return a;
        }
    }

    [HarmonyPatch(typeof(MissionServicesManager), nameof(MissionServicesManager.RegisterMissionObject))]
    internal static class Patch_RegisterMissionObject
    {
        private static void Prefix(MissionServicesManager __instance, MissionObjectIdentifier moi, out Il2CppStringArray __state)
        {
            __state = null;
            try
            {
                if (!FilterTags.UseHost(moi)) return;
                __state = __instance.m_MissionObjectFilterTags;
                __instance.m_MissionObjectFilterTags = FilterTags.HostArray();
                FilterTags.Swapped++;
            }
            catch { }
        }

        private static void Postfix(MissionServicesManager __instance, Il2CppStringArray __state)
        {
            if (__state == null) return;
            try { __instance.m_MissionObjectFilterTags = __state; } catch { }
        }
    }
}
