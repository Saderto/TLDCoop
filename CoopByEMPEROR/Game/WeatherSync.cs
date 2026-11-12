using System;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Погода. В игре она одна на весь мир: при переходе на другую карту погода продолжается,
    // только в варианте для этой карты. В кооперативе её ведёт хост:
    //  - у друзей игра сама следующую погоду не выбирает (WeatherTransition.ChooseNextWeatherSet);
    //  - хост при каждой смене погоды и раз в 30 секунд рассылает состояние (Serialize);
    //  - друг на той же карте, что хост, ставит его целиком (Deserialize): та же погода, та же стадия,
    //    смена в тот же момент;
    //  - друг на другой карте ставит ту же погоду в варианте для своей карты (по виду погоды).
    // Ветер (0.18.0): на одной карте с хостом направление и сила ветра те же, что у хоста (Wind.Serialize),
    // своих смен ветра у друга там нет. Укрытие от ветра (m_PlayerOccluded, за стеной, в пещере) игра считает
    // у каждого сама: за скалой ты защищён, друг на открытом месте — нет. На другой карте ветер у каждого свой.
    public static class WeatherSync
    {
        private static bool _windChanged, _windApplied;
        private static uint _windNo, _seenWindNo;
        private static float _nextSend;
        private static bool _changed;
        private static bool _applying;
        private static string _lastHostRegion = "";
        private static bool _appliedOnce;
        // Номер смены погоды у хоста. Друг на другой карте ставит погоду, если номер новее того, что он уже поставил:
        // раньше смена, пришедшая во время загрузки или долгого действия, терялась, а дальше хост слал «не сменилась».
        private static uint _changeNo, _seenNo;

        private static bool ClientControlled =>
            Session.Mode == Mode.Client && Session.InHostWorld && Session.HostWeatherSync && GameApi.InGame();

        // Патч: у друга своя смена погоды выключена.
        public static bool AllowChoose() => _applying || !ClientControlled;

        // Друг на одной карте с хостом: свой ветер не меняет, ждёт ветер хоста.
        private static bool SameMapAsHost() => _lastHostRegion.Length > 0 && _lastHostRegion == (NewGame.CurrentRegionKey() ?? "");
        public static bool AllowOwnWind() => _applying || !ClientControlled || !SameMapAsHost();

        public static void WindPhaseStarted()
        {
            if (Session.Mode == Mode.Host && !_applying) _windChanged = true;
        }

        public static void MarkChanged()
        {
            if (Session.Mode == Mode.Host && !_applying) _changed = true;
        }

        // Хост: каждый кадр.
        public static void Tick(float now)
        {
            if (Session.Mode != Mode.Host || !Config.SyncWeather || !GameApi.InGame()) return;
            if (ActionTime.Active) return;   // у хоста идёт долгое действие: его погода сейчас ускорена, не рассылаем
            if (!_changed && !_windChanged && now < _nextSend) return;
            bool changed = _changed;
            _changed = false;
            if (changed) _changeNo++;
            if (_windChanged) _windNo++;
            _windChanged = false;
            _nextSend = now + 30f;
            try
            {
                var wt = GameManager.GetWeatherTransitionComponent();
                var weather = GameManager.GetWeatherComponent();
                if (wt == null || weather == null) return;
                var w = Msg.New(MsgType.WeatherState);
                w.Write(changed);
                w.Write(NewGame.CurrentRegionKey() ?? "");
                w.Write(GameApi.WeatherStage());
                w.Write(wt.Serialize() ?? "");
                w.Write(weather.Serialize() ?? "");
                w.Write(_changeNo);
                string wind = "";
                try { wind = GameManager.GetWindComponent()?.Serialize() ?? ""; } catch { }
                w.Write(wind);
                w.Write(_windNo);
                Session.SendGameEvent(Msg.Done(w));
                if (changed) MelonLogger.Msg($"[погода] сменилась, разослал: стадия {GameApi.WeatherStage()}");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[погода] " + e.Message);
            }
        }

        // Друг: состояние погоды от хоста.
        public static void Receive(BinaryReader r)
        {
            bool changed = r.ReadBoolean();
            string region = r.ReadString();
            int stage = r.ReadInt32();
            string transition = r.ReadString(), weatherText = r.ReadString();
            uint no = r.BaseStream.Length - r.BaseStream.Position >= 4 ? r.ReadUInt32() : 0;
            string wind = r.BaseStream.Position < r.BaseStream.Length ? r.ReadString() : "";
            uint windNo = r.BaseStream.Length - r.BaseStream.Position >= 4 ? r.ReadUInt32() : 0;
            if (!ClientControlled || GameApi.IsLoading()) return;
            ApplyWeather(changed, region, stage, transition, weatherText, no);
            ApplyWind(region, wind, windNo);
        }

        private static void ApplyWeather(bool changed, string region, int stage, string transition, string weatherText, uint no)
        {
            int mine = GameApi.WeatherStage();
            string myRegion = NewGame.CurrentRegionKey() ?? "";
            bool sameRegion = region.Length > 0 && region == myRegion;
            // У нас идёт долгое действие: погода заморожена, свежую от хоста поставим, когда оно кончится.
            if (_frozen && sameRegion) { _frozenWt = transition; _frozenW = weatherText; return; }
            // На другой карте во время долгого действия не ставим: в конце погода вернётся к той, что была в начале.
            // Номер смены останется непоставленным, и свежую погоду поставим со следующим сообщением хоста.
            if (_frozen) return;
            if (no != _seenNo) changed = true;
            // Ставим, когда погода сменилась, когда стадии разные или когда хост перешёл на нашу карту.
            bool regionJoined = sameRegion && _lastHostRegion != region;
            _lastHostRegion = region;
            if (!sameRegion && !changed && _appliedOnce) return;   // на другой карте стадии внутри погоды идут свои
            if (!changed && mine == stage && !regionJoined) return;
            _applying = true;
            try
            {
                if (sameRegion)
                {
                    GameManager.GetWeatherTransitionComponent()?.Deserialize(transition);
                    GameManager.GetWeatherComponent()?.Deserialize(weatherText);
                }
                else if ((changed || !_appliedOnce) && mine != stage && stage >= 0)
                {
                    GameManager.GetWeatherTransitionComponent()?.ActivateWeatherSet((WeatherStage)stage);
                }
                _appliedOnce = true;
                _seenNo = no;
                MelonLogger.Msg($"[погода] от хоста ({(sameRegion ? "та же карта" : "другая карта")}): {mine} → {stage}, стало {GameApi.WeatherStage()}");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[погода] применить: " + e.Message);
            }
            finally
            {
                _applying = false;
            }
        }

        // Ветер хоста: ставим, когда у хоста началась новая фаза ветра, когда мы только пришли на его карту
        // и в первый раз. Внутри фазы ветер у обоих идёт одинаково сам. Укрытие игра считает у каждого своё.
        private static void ApplyWind(string region, string wind, uint windNo)
        {
            if (string.IsNullOrEmpty(wind) || _frozen) return;
            string myRegion = NewGame.CurrentRegionKey() ?? "";
            if (region.Length == 0 || region != myRegion) { _windApplied = false; return; }   // на другой карте ветер свой
            if (_windApplied && windNo == _seenWindNo) return;
            var wc = GameManager.GetWindComponent();
            if (wc == null) return;
            _applying = true;
            try
            {
                wc.Deserialize(wind);
                _windApplied = true;
                _seenWindNo = windNo;
                MelonLogger.Msg($"[погода] ветер от хоста: {wc.GetStrength()}, {wc.GetSpeedMPH_Base():0} миль/ч, {wc.GetWindAngle_Base():0}°");
            }
            catch (Exception e) { MelonLogger.Warning("[погода] ветер: " + e.Message); }
            finally { _applying = false; }
        }

        // Долгое действие (Game/ActionTime.cs): у себя часы ускорены, и погода побежала бы вперёд.
        // Запоминаем её в начале и возвращаем в конце — погода у всех идёт по общим часам.
        private static bool _frozen;
        private static string _frozenWt, _frozenW;

        public static void Freeze()
        {
            _frozen = false;
            try
            {
                var wt = GameManager.GetWeatherTransitionComponent();
                var weather = GameManager.GetWeatherComponent();
                if (wt == null || weather == null) return;
                _frozenWt = wt.Serialize();
                _frozenW = weather.Serialize();
                _frozen = !string.IsNullOrEmpty(_frozenWt) && !string.IsNullOrEmpty(_frozenW);
            }
            catch { }
        }

        public static void Unfreeze(bool restore)
        {
            if (!_frozen) return;
            _frozen = false;
            if (!restore) return;
            _applying = true;
            try
            {
                GameManager.GetWeatherTransitionComponent()?.Deserialize(_frozenWt);
                GameManager.GetWeatherComponent()?.Deserialize(_frozenW);
            }
            catch (Exception e) { MelonLogger.Warning("[погода] вернуть после действия: " + e.Message); }
            finally { _applying = false; }
            if (Session.Mode == Mode.Host) _changed = false;
        }

        public static void Reset()
        {
            _frozen = false;
            _nextSend = 0f;
            _changed = false;
            _lastHostRegion = "";
            _appliedOnce = false;
            _seenNo = 0;
            _windChanged = false;
            _windApplied = false;
            _seenWindNo = 0;
        }
    }

    [HarmonyPatch(typeof(WeatherTransition), nameof(WeatherTransition.ChooseNextWeatherSet))]
    internal static class Patch_WeatherChoose
    {
        private static bool Prefix() => WeatherSync.AllowChoose();
        private static void Postfix() => WeatherSync.MarkChanged();
    }

    // Ветер: у друга на карте хоста свои смены выключены; у хоста каждая новая фаза уходит сразу.
    [HarmonyPatch(typeof(Wind), nameof(Wind.MaybeChangePhase))]
    internal static class Patch_WindMaybeChange
    {
        private static bool Prefix() => WeatherSync.AllowOwnWind();
    }

    [HarmonyPatch(typeof(Wind), nameof(Wind.ForceImmediateWindChangeBasedOnWeather))]
    internal static class Patch_WindForce
    {
        private static bool Prefix() => WeatherSync.AllowOwnWind();
        private static void Postfix() => WeatherSync.WindPhaseStarted();
    }

    [HarmonyPatch(typeof(Wind), nameof(Wind.StartPhase), new[] { typeof(WindDirection), typeof(WindStrength) })]
    internal static class Patch_WindPhaseDir
    {
        private static void Postfix() => WeatherSync.WindPhaseStarted();
    }

    [HarmonyPatch(typeof(Wind), nameof(Wind.StartPhase), new[] { typeof(WindStrength), typeof(float) })]
    internal static class Patch_WindPhaseAngle
    {
        private static void Postfix() => WeatherSync.WindPhaseStarted();
    }

    [HarmonyPatch(typeof(Wind), nameof(Wind.StartPhaseImmediate), new[] { typeof(WindStrength), typeof(float) })]
    internal static class Patch_WindPhaseImmediate
    {
        private static void Postfix() => WeatherSync.WindPhaseStarted();
    }

    [HarmonyPatch(typeof(Wind), nameof(Wind.StartPhaseImmediate), new[] { typeof(WindDirection), typeof(WindStrength) })]
    internal static class Patch_WindPhaseImmediateDir
    {
        private static void Postfix() => WeatherSync.WindPhaseStarted();
    }

    [HarmonyPatch(typeof(WeatherTransition), nameof(WeatherTransition.ActivateWeatherSetImmediate))]
    internal static class Patch_WeatherImmediate
    {
        private static void Postfix() => WeatherSync.MarkChanged();
    }

    [HarmonyPatch(typeof(WeatherTransition), nameof(WeatherTransition.ActivateWeatherSet), new[] { typeof(WeatherStage) })]
    internal static class Patch_WeatherActivate
    {
        private static void Postfix() => WeatherSync.MarkChanged();
    }
}
