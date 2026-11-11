using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;

namespace TLDCoop.Game
{
    // Подвиги (значки, которые выбирают перед новой игрой) у каждого игрока свои.
    // Игра хранит выбранные подвиги в общем сохранении мира (global m_FeatsEnabledSerialized → FeatEnabledTracker),
    // поэтому друг вместе с копией мира получал подвиги хоста. Теперь:
    //  • m_FeatsEnabledSerialized — личный ключ игрока (PlayerProgress.PlayerKeys): уходит хосту с прогрессом друга
    //    и возвращается в копии мира при следующем входе;
    //  • своих подвигов друга в прогрессе нет (первый вход в этот мир или прогресс до 0.19.49) — хост ставит в копии
    //    мира метку AskKey. Друг, получив такую копию в главном меню, сначала выбирает подвиги в игровом окне выбора,
    //    потом грузится. Выбор подставляется при загрузке (FeatEnabledTracker.Deserialize) вместо подвигов хоста.
    //  • Вход из своей игры (не из главного меню) окна выбора не даёт: друг входит без подвигов.
    internal static class FeatsChoice
    {
        internal const string AskKey = "tldcoop_feats_ask";

        private static string _override;     // что подставить в FeatEnabledTracker.Deserialize при загрузке мира хоста
        private static Action _then;
        private static Panel_MainMenu _menu;
        internal static bool Choosing { get; private set; }

        // Открыть игровое окно выбора подвигов. false — нельзя (не главное меню, нечего выбирать): друг без подвигов.
        internal static bool Begin(Action then)
        {
            try
            {
                if (!GameManager.IsMainMenuActive()) { MelonLogger.Msg("[подвиги] вхожу не из главного меню — окна выбора нет, вхожу без подвигов"); return false; }
                if (!InterfaceManager.TryGetPanel<Panel_MainMenu>(out var m) || m == null) { MelonLogger.Msg("[подвиги] нет главного меню — вхожу без подвигов"); return false; }
                if (!m.ShouldShowFeatSelection()) { MelonLogger.Msg("[подвиги] выбирать нечего (нет открытых подвигов или режим их не даёт) — вхожу без подвигов"); return false; }
                _then = then;
                _menu = m;
                Choosing = true;
                UI.CoopWindow.Open = false;
                m.SelectWindow(m.m_SelectFeatWindow);
                MelonLogger.Msg("[подвиги] первый вход в мир хоста: открыл выбор подвигов");
                return true;
            }
            catch (Exception e)
            {
                Choosing = false; _then = null; _menu = null;
                MelonLogger.Warning("[подвиги] окно выбора: " + e.Message);
                return false;
            }
        }

        // Окно выбора не открылось: входим без подвигов (не с подвигами хоста).
        internal static void UseNone()
        {
            _override = EmptyList();
            MelonLogger.Msg("[подвиги] вхожу без подвигов");
        }

        internal static void Clear() => _override = null;

        // Соединение пропало, пока выбирали: окно закрываем, загрузку не продолжаем.
        internal static void Cancel()
        {
            if (!Choosing) { _override = null; return; }
            Choosing = false;
            _then = null;
            try { _menu?.SelectWindow(_menu.m_MainWindow); } catch { }
            _menu = null;
            _override = null;
            MelonLogger.Msg("[подвиги] выбор подвигов отменён");
        }

        // Panel_MainMenu.OnSelectFeatsContinue уже записал выбор в FeatEnabledTracker и зовёт ShowSurvivalSettings
        // (следующий шаг новой игры). Вместо него — назад в главное меню и дальше загрузка мира хоста.
        internal static bool BeforeSurvivalSettings(Panel_MainMenu m)
        {
            if (!Choosing) return true;
            Choosing = false;
            try { _override = FeatEnabledTracker.Serialize(); }
            catch (Exception e) { _override = EmptyList(); MelonLogger.Warning("[подвиги] выбор не прочитался: " + e.Message); }
            MelonLogger.Msg("[подвиги] выбрано: " + Names());
            try { m.SelectWindow(m.m_MainWindow); } catch { }
            _menu = null;
            var then = _then;
            _then = null;
            try { then?.Invoke(); }
            catch (Exception e) { MelonLogger.Error("[подвиги] загрузка после выбора: " + e); }
            return false;
        }

        // Назад из окна выбора во время входа: остаёмся в окне (выйти некуда — мир хоста ждёт выбора).
        internal static bool BeforeBack()
        {
            if (!Choosing) return true;
            if (Session.Mode != Mode.Client) { Cancel(); return true; }
            return false;
        }

        // FeatEnabledTracker.Deserialize при загрузке мира хоста: подвиги друга вместо подвигов хоста.
        internal static void OnDeserialize(ref string text)
        {
            if (_override == null || !WorldTransfer.LoadingHostWorld) return;
            text = _override;
            MelonLogger.Msg("[подвиги] в мире хоста свои подвиги вместо подвигов хоста");
        }

        internal static void AfterDeserialize()
        {
            if (_override == null || !WorldTransfer.LoadingHostWorld) return;
            MelonLogger.Msg("[подвиги] в игре теперь: " + Names());
        }

        private static string EmptyList()
        {
            var list = FeatEnabledTracker.m_FeatsEnabledThisSandbox;
            if (list == null) return null;
            var keep = new List<FeatType>();
            foreach (var f in list) keep.Add(f);
            try { list.Clear(); return FeatEnabledTracker.Serialize(); }
            catch (Exception e) { MelonLogger.Warning("[подвиги] пустой список: " + e.Message); return null; }
            finally { list.Clear(); foreach (var f in keep) list.Add(f); }
        }

        private static string Names()
        {
            try
            {
                var list = FeatEnabledTracker.m_FeatsEnabledThisSandbox;
                if (list == null || list.Count == 0) return "ничего";
                var n = new List<string>();
                foreach (var f in list) n.Add(f.ToString());
                return string.Join(", ", n);
            }
            catch { return "?"; }
        }
    }

    [HarmonyPatch(typeof(Panel_MainMenu), nameof(Panel_MainMenu.ShowSurvivalSettings))]
    internal static class Patch_FeatsChoiceDone
    {
        private static bool Prefix(Panel_MainMenu __instance) => FeatsChoice.BeforeSurvivalSettings(__instance);
    }

    [HarmonyPatch(typeof(Panel_MainMenu), nameof(Panel_MainMenu.OnSelectFeatsBack))]
    internal static class Patch_FeatsChoiceBack
    {
        private static bool Prefix() => FeatsChoice.BeforeBack();
    }

    [HarmonyPatch(typeof(FeatEnabledTracker), nameof(FeatEnabledTracker.Deserialize))]
    internal static class Patch_FeatsDeserialize
    {
        private static void Prefix(ref string __0)
        {
            try { FeatsChoice.OnDeserialize(ref __0); } catch (Exception e) { MelonLogger.Warning("[подвиги] " + e.Message); }
        }

        private static void Postfix()
        {
            try { FeatsChoice.AfterDeserialize(); } catch { }
        }
    }
}
