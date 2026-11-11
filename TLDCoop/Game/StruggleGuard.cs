using System;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Схватка со зверем не должна оставлять игрока «залипшим». Игра ведёт схватку вместе со зверем-партнёром:
    // если зверь посреди схватки стал копией (его мозг ушёл другому игроку) или исчез, игра ждёт его вечно —
    // игрок лежит, рюкзак, меню и подбор не открываются (управление остаётся в режиме «схватка»).
    //  - партнёр — копия чужого зверя дольше 3 с, а схватка ещё идёт: прерываем её, как если бы зверь отпустил;
    //  - схватка (вместе с «лежит и встаёт») идёт дольше 60 с: доигрываем подъём сами;
    //  - схватки уже нет, а управление всё ещё в режиме «схватка» дольше 5 с: возвращаем обычное.
    //  - 90 с после схватки: меню (Esc) или подбор не работают, хотя ни одно окно игры не открыто, дольше 12 с —
    //    пишем в лог всё, что их держит (флаги из кода игры: InputManager.CanInvokePauseMenu,
    //    PlayerManager.AllowInteractionWithObjects, InterfaceManager.ShouldImmediatelyExitOverlay), и снимаем.
    //  Кнопка «Починить управление» в окне мода делает то же самое сразу.
    public static class StruggleGuard
    {
        private static float _puppetSince = -1f, _activeSince = -1f, _lockedSince = -1f;
        private static int _fixes;
        private static bool _wasActive;
        private static float _endedAt = -100f, _blockedSince = -1f, _nextHealth, _healthAfter = -1f, _nextImmortal;
        private static int _immortalLogs;

        // Бессмертие из чужого сохранения. Друг играет на копии мира хоста, и в первый раз берёт оттуда
        // состояние игрока хоста — вместе с флагами, при которых урон не проходит: Condition.m_Invulnerable
        // (его ставит «бессмертие» тренера и он сохраняется), Condition.m_NeverDie, PlayerManager.m_God
        // (с ним Condition.AddHealth вместо урона ставит полное здоровье). Дальше они жили в прогрессе друга.
        // Друг без тренера: в мире хоста снимаем их у друга, кроме времени, когда их держит сам мод (Death).
        internal static void ClearImmortal(PlayerManager pm, string where)
        {
            if (Session.Mode != Mode.Client || !Session.InSharedWorld || Death.Busy) return;
            var c = GameManager.GetConditionComponent();
            if (c == null || pm == null) return;
            if (!c.m_Invulnerable && !c.m_NeverDie && !pm.m_God) return;
            string was = (c.m_Invulnerable ? " m_Invulnerable" : "") + (c.m_NeverDie ? " m_NeverDie" : "") + (pm.m_God ? " m_God" : "");
            c.m_Invulnerable = false;
            c.m_NeverDie = false;
            pm.m_God = false;
            if (_immortalLogs++ < 5) MelonLogger.Msg($"[здоровье] снял бессмертие ({was.Trim()}) {where}: урон снова проходит");
        }

        public static void Tick(float now)
        {
            if (Session.Mode == Mode.Off || !GameApi.InGame()) { _puppetSince = _activeSince = _lockedSince = -1f; return; }
            PlayerStruggle st;
            PlayerManager pm;
            try { st = GameManager.GetPlayerStruggleComponent(); pm = GameManager.GetPlayerManagerComponent(); }
            catch { return; }
            if (st == null || pm == null) return;
            try
            {
                bool active = st.m_Active;
                bool locked = pm.GetControlMode() == PlayerControlMode.Struggle;

                BaseAi partner = active ? st.m_PartnerBaseAi : null;
                bool puppet = partner != null && AnimalSync.IsPuppet(partner);
                if (puppet && st.InStruggle() && !st.WaitingToRecover())
                {
                    if (_puppetSince < 0f) _puppetSince = now;
                    else if (now - _puppetSince > 3f)
                    {
                        _puppetSince = -1f;
                        Fix($"зверь-партнёр стал копией ({partner.gameObject.name}) — прерываю схватку");
                        st.BreakStruggle();
                    }
                }
                else _puppetSince = -1f;

                if (active)
                {
                    if (_activeSince < 0f) _activeSince = now;
                    else if (now - _activeSince > 60f)
                    {
                        _activeSince = now;
                        Fix($"схватка не кончается 60 с (камера {st.m_StruggleCameraMode}, партнёр {(partner == null ? "нет" : partner.gameObject.name)}) — поднимаю игрока");
                        st.GetUpAnimationComplete();
                        if (st.m_Active) st.m_Active = false;
                    }
                }
                else _activeSince = -1f;

                if (locked && !active)
                {
                    if (_lockedSince < 0f) _lockedSince = now;
                    else if (now - _lockedSince > 5f)
                    {
                        _lockedSince = -1f;
                        Fix("схватки нет, а управление осталось в режиме схватки — возвращаю обычное");
                        pm.SetControlMode(PlayerControlMode.Normal);
                    }
                }
                else _lockedSince = -1f;

                if (!_wasActive && active) MelonLogger.Msg("[схватка] началась: здоровье " + Diagnostics.Health());
                if (_wasActive && !active) { _endedAt = now; _healthAfter = now + 15f; MelonLogger.Msg("[схватка] кончилась: здоровье " + Diagnostics.Health()); }
                _wasActive = active;
                if (_healthAfter > 0f && now > _healthAfter) { _healthAfter = -1f; MelonLogger.Msg("[схватка] через 15 с: здоровье " + Diagnostics.Health()); }
                if (now > _nextHealth)
                {
                    if (_nextHealth == 0f) MelonLogger.Msg("[звук] настройки игры: " + Diagnostics.Audio());
                    _nextHealth = now + 300f; MelonLogger.Msg("[здоровье] " + Diagnostics.Health());
                }
                if (now > _nextImmortal) { _nextImmortal = now + 2f; ClearImmortal(pm, "в мире хоста"); }
                if (!active && now - _endedAt < 90f && !Death.ScreenOpen)
                {
                    var mode = pm.GetControlMode();
                    bool blocked = (mode == PlayerControlMode.Normal || mode == PlayerControlMode.Struggle || mode == PlayerControlMode.Locked)
                        && !InterfaceManager.IsOverlayActiveCached()
                        && (!InputManager.CanInvokePauseMenu() || !pm.AllowInteractionWithObjects());
                    if (!blocked) _blockedSince = -1f;
                    else if (_blockedSince < 0f) _blockedSince = now;
                    else if (now - _blockedSince > 12f)
                    {
                        _blockedSince = -1f;
                        _endedAt = -100f;
                        MelonLogger.Msg("[схватка] после схватки меню или подбор не работают: " + State());
                        Repair(false);
                    }
                }
                else _blockedSince = -1f;
            }
            catch (Exception e)
            {
                if (_fixes++ < 20) MelonLogger.Warning("[схватка] проверка: " + e.Message);
            }
        }

        // Что держит меню, рюкзак и подбор (для лога).
        public static string State()
        {
            var parts = new System.Collections.Generic.List<string>();
            void Add(string name, Func<object> get) { try { parts.Add(name + "=" + get()); } catch (Exception e) { parts.Add(name + "=!" + e.GetType().Name); } }
            var st = GameManager.GetPlayerStruggleComponent();
            var pm = GameManager.GetPlayerManagerComponent();
            Add("схватка", () => st.m_Active);
            Add("камера", () => st.m_StruggleCameraMode);
            Add("управление", () => pm.GetControlMode());
            Add("заблокировано", () => GameManager.ControlsLocked());
            Add("esc", () => InputManager.CanInvokePauseMenu());
            Add("подбор", () => pm.AllowInteractionWithObjects());
            Add("окно", () => InterfaceManager.IsOverlayActiveCached());
            Add("окно сейчас", () => InterfaceManager.IsOverlayActiveImmediate());
            Add("закрывать окна", () => InterfaceManager.ShouldImmediatelyExitOverlay());
            Add("загрузка", () => InterfaceManager.IsPanelEnabled<Panel_Loading>());
            Add("испытание", () => InterfaceManager.IsPanelEnabled<Panel_ChallengeComplete>());
            Add("осмотр", () => pm.m_InspectModeActive);
            Add("удержание", () => pm.m_IsHoldInteractionActive);
            Add("принуд", () => pm.m_IsForcedInteractionActive);
            Add("затемнение", () => $"{CameraFade.CurrentAlpha:0.00}{(CameraFade.IsFading ? " идёт" : "")}");
            return string.Join(", ", parts);
        }

        // Снять всё, что после схватки может держать меню, рюкзак и подбор. manual — нажали кнопку в окне мода.
        public static void Repair(bool manual)
        {
            var done = new System.Collections.Generic.List<string>();
            void Try(string what, Func<bool> act) { try { if (act()) done.Add(what); } catch (Exception e) { MelonLogger.Warning($"[схватка] починка ({what}): {e.Message}"); } }
            var st = GameManager.GetPlayerStruggleComponent();
            var pm = GameManager.GetPlayerManagerComponent();
            if (manual) MelonLogger.Msg("[схватка] кнопка «Починить управление»: " + State());
            Try("схватка", () =>
            {
                if (st == null || !st.m_Active) return false;
                st.GetUpAnimationComplete();
                if (st.m_Active) st.m_Active = false;
                return true;
            });
            Try("управление", () =>
            {
                if (pm == null) return false;
                var m = pm.GetControlMode();
                bool stuck = m == PlayerControlMode.Struggle || m == PlayerControlMode.Locked || (manual && m == PlayerControlMode.Dead && !Death.ScreenOpen);
                if (!stuck) return false;
                pm.SetControlMode(PlayerControlMode.Normal);
                return true;
            });
            Try("осмотр", () =>
            {
                if (pm == null || !pm.m_InspectModeActive || (!manual && pm.GearItemBeingInspected() != null)) return false;
                pm.ExitInspectGearMode(true);
                if (pm.m_InspectModeActive) pm.m_InspectModeActive = false;
                return true;
            });
            Try("удержание", () =>
            {
                if (pm == null || !(pm.m_IsHoldInteractionActive || pm.m_IsForcedInteractionActive)) return false;
                pm.m_IsHoldInteractionActive = false;
                pm.m_IsForcedInteractionActive = false;
                return true;
            });
            Try("закрывать окна", () =>
            {
                if (!InterfaceManager.m_ImmediatelyExitOverlay) return false;
                InterfaceManager.m_ImmediatelyExitOverlay = false;
                return true;
            });
            Try("испытание", () =>
            {
                if (!InterfaceManager.TryGetPanel<Panel_ChallengeComplete>(out var p) || p == null || !p.IsEnabled()) return false;
                p.Enable(false);
                return true;
            });
            Try("камера", () =>
            {
                // По кнопке: камера перекошена после схватки (слой камеры остался от неё) — вернуть, как в конце схватки.
                if (!manual || st == null || st.m_Active || Death.ScreenOpen) return false;
                Death.FixStruggleCamera("по кнопке");
                return true;
            });
            Try("затемнение", () =>
            {
                if (CameraFade.CurrentAlpha < 0.05f && !CameraFade.IsFading) return false;
                CameraFade.FadeIn(0.5f, 0f, null);
                return true;
            });
            string res = done.Count == 0 ? "держать было нечего" : "снял: " + string.Join(", ", done);
            Fix((manual ? "по кнопке " : "") + res + "; теперь: " + State());
            if (manual) GameApi.HudMessage("Кооп: управление — " + res);
        }

        private static void Fix(string what)
        {
            _fixes++;
            MelonLogger.Msg("[схватка] " + what);
        }
    }
}
