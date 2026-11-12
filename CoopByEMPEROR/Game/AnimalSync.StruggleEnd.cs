using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Конец схватки со зверем.
    //
    // Игрок «умер» в схватке (в коопе он ранен, игра идёт дальше). Игра в PlayerStruggle.OnPlayerDeath ставит зверю
    // Wander, не завершая схватку (BreakStruggle): в одиночной дальше экран смерти. У нас зверь оставался в анимации
    // схватки и ехал по земле — «волк после схватки скользит». Делаем, как при обычном конце схватки: зверь убегает
    // от игрока (BaseAi.FleeFrom).
    //
    // И на любой конец схватки — сторож: 6 секунд пишем в лог, что с бывшим партнёром (режим, анимация, скорость), и
    // если он едет, а аниматор так и стоит в анимации схватки, сбрасываем аниматор (Rebind) — дальше его ведёт ИИ.
    public static partial class AnimalSync
    {
        private static readonly HashSet<int> StruggleStates = new();
        private static BaseAi _strPartner;
        private static Animator _strAnim;
        private static bool _strWas;
        private static float _strEndAt = -100f, _strNextLook, _strLastAt, _strBeganAt;
        private static bool _strLookedIn;

        // Своя схватка, через секунду: виден ли нам партнёр (жалоба «схватка с невидимым волком»).
        private static void LookAtPartner(BaseAi ai)
        {
            try
            {
                int all = 0, on = 0, vis = 0;
                foreach (var r in ai.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    all++;
                    if (r.enabled && r.gameObject.activeInHierarchy) on++;
                    if (r.isVisible) vis++;
                }
                var cam = GameManager.GetMainCamera();
                string d = cam != null ? $"{Vector3.Distance(cam.transform.position, ai.transform.position):0.0} м от камеры" : "камеры нет";
                bool puppet = IsPuppet(ai);
                MelonLogger.Msg($"[звери] моя схватка с {ai.gameObject.name}: {(puppet ? "копия" : "настоящий")}, {d}, активен {(ai.gameObject.activeInHierarchy ? "да" : "нет")}, частей модели видно {vis}, включено {on} из {all}, режим {ai.m_CurrentMode}");
            }
            catch (Exception e) { MelonLogger.Warning("[звери] моя схватка: " + e.Message); }
        }
        private static int _strLooks, _strRebinds, _strFlees, _strLogs;
        private static Vector3 _strLastPos;

        private static string StruggleEndStats => $"после «смерти» в схватке прогнал зверя {_strFlees}, сбросил застрявший аниматор {_strRebinds}; замедление после схватки снял {_slowFixed}, звук выхода из выбора оружия доиграл {_pickerExitPlayed}";

        // ---------- Замедление после схватки ----------
        // В начале схватки, пока выбирают оружие, игра замедляет время (GameManager.m_GlobalTimeScale, по нему же
        // параметр звука TIMESCALE — звук «тянется») и включает звук замедления (Play_WeaponPickerEnter). Назад время
        // возвращает только сама схватка, пока она идёт (PlayerStruggle.Update / HandleTimeSlowdownFadeout), а звук
        // выхода (PlayPickerExitAudio) играет только обычный конец выбора. В одиночной игре схватка в это время не
        // кончается. В коопе зверя может убить другой игрок прямо во время выбора оружия (так было: волк погиб на
        // 3-й секунде схватки GNOM от выстрела хоста, медведь — в схватке хоста от выстрелов GNOM): схватка
        // обрывается, и замедление могло так и остаться. Поэтому: если схватки у меня нет, а замедление осталось
        // (время медленнее или звук замедления всё ещё числится) — дольше 1 с и в пределах 20 с после любой схватки (правило автора: 20 с, не минута)
        // (своей или чужой) — возвращаем время и доигрываем звук выхода, как игра при обычном конце выбора.
        private static float _slowSince = -1f, _anyStruggleAt = -100f, _strSlowSeenAt = -100f;
        private static bool _strSlowSeen, _exitHeard, _exitChecked = true, _remoteStrWas;
        private static int _slowFixed, _pickerExitPlayed, _slowLogs;

        internal static void OnPickerExitAudio() => _exitHeard = true;

        private static string SlowState(PlayerStruggle st)
        {
            string s = "";
            try { s += $"время игры ×{GameManager.m_GlobalTimeScale:0.00} (Unity ×{Time.timeScale:0.00})"; } catch { }
            try { if (st != null) s += $", звук замедления {(st.m_WeaponSelectionSlowdownAudioId != 0 ? "числится" : "нет")}, до замедления было ×{st.m_PreviousTimeScaleFactor:0.00}"; } catch { }
            return s;
        }

        private static void SlowmoGuard(float now, PlayerStruggle st, bool inStr)
        {
            if (st == null) return;
            // Чужая схватка: друг лежит под зверем или встаёт после неё (действие 33/34 на его фигуре).
            bool remote = false;
            try { foreach (var pl in Session.Players.Values) if (pl != null && pl.Id != Session.LocalId && (pl.Action == 33 || pl.Action == 34)) { remote = true; break; } } catch { }
            if (remote) _anyStruggleAt = now;
            if (_remoteStrWas && !remote && !inStr && _slowLogs++ < 20) MelonLogger.Msg("[звери] схватка друга кончилась, у меня: " + SlowState(st));
            _remoteStrWas = remote;
            bool local = inStr;
            try { local = local || st.InStruggle(); } catch { }
            if (local)
            {
                _anyStruggleAt = now;
                _slowSince = -1f;
                try { if (st.m_WeaponSelectionSlowdownAudioId != 0) { _strSlowSeen = true; _strSlowSeenAt = now; } } catch { }
                return;
            }
            // Своя схватка кончилась: замедление было, а звука выхода из него игра так и не сыграла — играем сами.
            if (!_exitChecked && now - _strEndAt > 1f)
            {
                _exitChecked = true;
                if (_strSlowSeen && !_exitHeard)
                {
                    try { st.StopWeaponSlowdownAudio(); } catch { }
                    try { st.PlayPickerExitAudio(); _pickerExitPlayed++; } catch { }
                    MelonLogger.Msg($"[звери] схватка кончилась посреди выбора оружия (замедление было {now - _strSlowSeenAt:0.0} с назад), а звука выхода из замедления игра не сыграла — сыграл сам; " + SlowState(st));
                }
            }
            if (now - _anyStruggleAt > 20f) { _slowSince = -1f; return; }
            bool paused = false;
            try { paused = GameManager.m_IsPaused; } catch { }
            float scale;
            uint slowId;
            try { scale = GameManager.m_GlobalTimeScale; slowId = st.m_WeaponSelectionSlowdownAudioId; } catch { return; }
            bool slow = !paused && (scale < 0.98f || slowId != 0);
            if (!slow) { _slowSince = -1f; return; }
            if (_slowSince < 0f) { _slowSince = now; return; }
            if (now - _slowSince < 1f) return;
            _slowSince = -1f;
            string was = SlowState(st);
            try { if (scale < 0.98f) GameManager.m_GlobalTimeScale = 1f; } catch { }
            try { st.StopWeaponSlowdownAudio(); } catch { }
            try { st.PlayPickerExitAudio(); _pickerExitPlayed++; } catch { }
            _slowFixed++;
            if (_slowLogs++ < 20) MelonLogger.Msg($"[звери] схватки у меня нет (последняя — {(now - _strEndAt <= now - _anyStruggleAt + 0.5f ? "моя" : "у друга")}, {now - _anyStruggleAt:0} с назад), а замедление осталось: {was} — вернул обычное время и звук");
        }

        internal static void AfterStruggleDeath(BaseAi ai)
        {
            if (!Active || ai == null) return;
            try
            {
                if (ai.m_CurrentMode == AiMode.Dead || IsPuppet(ai)) return;
                var t = GameManager.GetPlayerTransform();
                var was = ai.m_CurrentMode;
                if (t != null) ai.FleeFrom(t.position);
                else ai.SetAiMode(AiMode.Flee);
                _strFlees++;
                MelonLogger.Msg($"[звери] схватка кончилась тем, что я ранен: {ai.gameObject.name} убегает, как после обычной схватки (игра ставила {was}, теперь {ai.m_CurrentMode})");
            }
            catch (Exception e) { MelonLogger.Warning("[звери] после смерти в схватке: " + e.Message); }
        }

        private static void StruggleEndTick(float now)
        {
            PlayerStruggle st = null;
            try { st = GameManager.GetPlayerStruggleComponent(); } catch { }
            bool inStr = false;
            BaseAi partner = null;
            try { inStr = st != null && st.m_Active; partner = inStr ? st.m_PartnerBaseAi : null; } catch { }
            try { SlowmoGuard(now, st, inStr); } catch (Exception e) { if (_slowLogs++ < 20) MelonLogger.Warning("[звери] замедление после схватки: " + e.Message); }
            if (inStr && partner != null)
            {
                if (_strPartner == null || _strPartner.Pointer != partner.Pointer)
                {
                    _strPartner = partner;
                    _strAnim = AnimOf(partner);
                    StruggleStates.Clear();
                }
                try { if (_strAnim != null && _strAnim.isActiveAndEnabled) StruggleStates.Add(_strAnim.GetCurrentAnimatorStateInfo(0).fullPathHash); } catch { }
                if (!_strWas) { _strBeganAt = now; _strLookedIn = false; _strSlowSeen = _exitHeard = false; _exitChecked = true; }
                _strWas = true;
                if (!_strLookedIn && now - _strBeganAt >= 1f) { _strLookedIn = true; LookAtPartner(partner); }
                return;
            }
            if (_strWas)
            {
                _strWas = false;
                _strEndAt = now;
                _exitChecked = false;
                if (_slowLogs++ < 20) MelonLogger.Msg($"[звери] моя схватка кончилась через {now - _strBeganAt:0.0} с: {SlowState(st)}{(_strSlowSeen ? ", замедление было" : "")}{(_exitHeard ? ", звук выхода был" : "")}");
                _strNextLook = now + 0.5f;
                _strLooks = 0;
                _strLastAt = now;
                try { if (_strPartner != null) _strLastPos = _strPartner.transform.position; } catch { }
            }
            if (_strPartner == null || now < _strNextLook) return;
            if (now - _strEndAt > 6.5f) { _strPartner = null; _strAnim = null; return; }
            float dt = now - _strLastAt;
            _strLastAt = now;
            _strNextLook = now + 1f;
            _strLooks++;
            try
            {
                var ai = _strPartner;
                if (ai == null) return;
                var p = ai.transform.position;
                float moved = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(_strLastPos.x, 0f, _strLastPos.z));
                _strLastPos = p;
                var an = _strAnim;
                int hash = 0;
                bool trans = false;
                string clip = "";
                float speedParam = float.NaN;
                if (an != null && an.isActiveAndEnabled)
                {
                    hash = an.GetCurrentAnimatorStateInfo(0).fullPathHash;
                    trans = an.IsInTransition(0);
                    try { var ci = an.GetCurrentAnimatorClipInfo(0); if (ci != null && ci.Length > 0 && ci[0].clip != null) clip = ci[0].clip.name; } catch { }
                    try { if (an.parameterCount > 0) speedParam = an.GetFloat(FloatH[2]); } catch { }
                }
                bool stuck = hash != 0 && StruggleStates.Contains(hash) && !trans && ai.m_CurrentMode != AiMode.Struggle && ai.m_CurrentMode != AiMode.Dead;
                if (_strLogs++ < 40)
                    MelonLogger.Msg($"[звери] после схватки через {now - _strEndAt:0.0} с: {ai.gameObject.name} режим {ai.m_CurrentMode}, прошёл {moved:0.0} м за {dt:0.0} с, анимация {hash}{(clip.Length > 0 ? " «" + clip + "»" : "")}{(trans ? ", в переходе" : "")}, Speed {speedParam:0.00}, {(stuck ? "всё ещё анимация схватки" : "анимация не из схватки")}, аниматор {(an != null && an.enabled ? "вкл" : "выкл")}, root motion {(an != null && an.applyRootMotion ? "да" : "нет")}");
                if (stuck && moved > 0.5f && now - _strEndAt >= 1f)
                {
                    an.Rebind();
                    _strRebinds++;
                    MelonLogger.Msg($"[звери] {ai.gameObject.name} едет в анимации схватки — сбросил аниматор");
                }
            }
            catch (Exception e) { if (_strLogs++ < 40) MelonLogger.Warning("[звери] после схватки: " + e.Message); _strPartner = null; }
        }

        // ---------- Копия зверя в чужой схватке ----------
        // «Схватка с невидимым волком»: у того, кто смотрит со стороны, копии зверя в схватке другого не видно.
        // Пока не ясно почему, поэтому: на время позы пересчитываем границы модели каждый кадр (updateWhenOffscreen —
        // в клипе схватки тело далеко от корня, и игра могла считать модель вне экрана) и включаем выключенные
        // части модели; и пишем в лог, где копия, видна ли она и что играет.

        private static int _poseLogs, _poseFixes;

        private static void PoseWatch(Animal a, float now)
        {
            if (a.Posed && !a.WasPosed)
            {
                a.PoseSince = now;
                a.PoseLooks = 0;
                Offscreen(a, true);
            }
            else if (!a.Posed && a.WasPosed)
            {
                Offscreen(a, false);
                LogPose(a, now, "поза кончилась");
            }
            a.WasPosed = a.Posed;
            if (!a.Posed || a.PoseLooks >= 3) return;
            float at = a.PoseLooks == 0 ? 0.3f : a.PoseLooks == 1 ? 1.5f : 4f;
            if (now - a.PoseSince < at) return;
            a.PoseLooks++;
            LogPose(a, now, $"через {now - a.PoseSince:0.0} с");
        }

        private static void Offscreen(Animal a, bool on)
        {
            try
            {
                a.Skins ??= a.Go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var s in a.Skins)
                {
                    if (s == null) continue;
                    s.updateWhenOffscreen = on;
                    if (on && !s.enabled) { s.enabled = true; _poseFixes++; }
                }
            }
            catch { a.Skins = null; }
        }

        private static void LogPose(Animal a, float now, string when)
        {
            if (_poseLogs++ >= 40 || a.Go == null) return;
            try
            {
                var t = a.Go.transform;
                int all = 0, on = 0, vis = 0;
                foreach (var r in a.Go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    all++;
                    if (r.enabled && r.gameObject.activeInHierarchy) on++;
                    if (r.isVisible) vis++;
                }
                string fig = "фигуры нет";
                if (Session.Players.TryGetValue(a.Controller, out var pl))
                {
                    var f = new Vector3(pl.X, pl.Y, pl.Z);
                    fig = $"от фигуры {Vector3.Distance(t.position, f):0.0} м (у ведущего {Vector3.Distance(a.Pos, f):0.0} м)";
                }
                var me = GameManager.GetPlayerTransform();
                string mine = me != null ? $", от меня {Vector3.Distance(me.position, t.position):0} м" : "";
                int state = 0;
                bool animOn = a.Anim != null && a.Anim.isActiveAndEnabled;
                try { if (animOn) state = a.Anim.GetCurrentAnimatorStateInfo(0).fullPathHash; } catch { }
                bool pose = PoseOf(a.Prefab, out _, out _);
                MelonLogger.Msg($"[звери] копия {a.Prefab} в схватке у {NameOf(a.Controller)} ({when}): {fig}{mine}, y {t.position.y:0.00}, земля {a.GroundY:0.00}, низ модели {a.LowRel:0.00}, "
                    + $"активна {(a.Go.activeInHierarchy ? "да" : "нет")}, частей модели видно {vis}, включено {on} из {all}, анимация {state} у ведущего {a.State}, поза еды {(pose ? "есть" : "нет")}, аниматор {(animOn ? "вкл" : "выкл")}, включил выключенных {_poseFixes}");
            }
            catch (Exception e) { MelonLogger.Warning("[звери] копия в схватке: " + e.Message); }
        }

        // Ведущий в схватке, а зверь у него далеко от игрока — позу не ставим. Пишем, раз в 10 секунд.
        private static void PoseTooFar(Animal a, float dist)
        {
            float now = Time.unscaledTime;
            if (now - a.FarLogAt < 10f || _poseLogs++ >= 40) return;
            a.FarLogAt = now;
            MelonLogger.Msg($"[звери] {NameOf(a.Controller)} в схватке, а {a.Prefab} у него в {dist:0.0} м от игрока — копию в позу не ставлю");
        }

        private static Animator AnimOf(BaseAi ai)
        {
            try
            {
                if (ByAi.TryGetValue(ai.Pointer, out var g) && Animals.TryGetValue(g, out var a) && a.Anim != null) return a.Anim;
                return ai.GetComponentInChildren<Animator>();
            }
            catch { return null; }
        }
    }

    [HarmonyPatch(typeof(PlayerStruggle), nameof(PlayerStruggle.OnPlayerDeath))]
    internal static class Patch_StruggleDeathFlee
    {
        private static void Prefix(PlayerStruggle __instance, out BaseAi __state)
        {
            __state = null;
            try { __state = __instance.m_PartnerBaseAi; } catch { }
        }

        private static void Postfix(BaseAi __state) => AnimalSync.AfterStruggleDeath(__state);
    }

    // Звук выхода из замедления выбора оружия: игра сыграла его сама — доигрывать не нужно.
    [HarmonyPatch(typeof(PlayerStruggle), nameof(PlayerStruggle.PlayPickerExitAudio))]
    internal static class Patch_PickerExitAudio
    {
        private static void Postfix() => AnimalSync.OnPickerExitAudio();
    }
}
