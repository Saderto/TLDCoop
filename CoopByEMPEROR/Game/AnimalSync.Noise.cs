using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Выстрел слышат все звери. Игра сообщает зверям о выстреле (vp_FPSShooter.Update →
    // GameAudioManager.NotifyAiAudioEvent) только у стрелявшего, а мозг зверя обычно у другого игрока:
    // олень у хоста выстрела друга не слышал и не убегал. Свой выстрел рассылаем; получивший отдаёт его
    // своим настоящим зверям, а «игрока» на это время ставит на место стрелявшего — зверь бежит от него.
    //
    // Шаги — так же (FootStepSounds.PlayFootStepSound → NotifyAiAudioEvent(Footstep)), но без гарантии доставки
    // и не чаще 2 раз в секунду, и только если рядом с нами есть зверь, которого ведёт другой (у нас его копия):
    // иначе олень хозяина подпускал идущего друга, а на шаги хозяина реагировал. Крадётся ли идущий, зверь
    // узнаёт из его состояния (подмена игрока, как для выстрела).
    public static partial class AnimalSync
    {
        private const float NoiseRange = 500f;   // дальше этого зверя не трогаем (у игры свой предел слышимости)
        private static bool _noiseIn, _noiseStep;
        private static int _noiseOut, _noiseIn2, _noiseAnswered, _noiseLogs;
        private static float _lastNoiseAt = -10f, _lastStepAt = -10f;
        private const float StepGap = 0.5f;       // шаги другим — не чаще
        private const float StepHearFallback = 60f;   // если у зверя не прочитать m_HearFootstepsRange
        private static int _stepsOut, _stepsIn, _stepsAnswered;

        // Наш выстрел или шаг: игра оповещает своих зверей, мы — остальных игроков.
        internal static void OnLocalNoise(GameObject sender, Vector3 pos, GameAudioAiEvent type)
        {
            if (_noiseIn || !Active || sender == null) return;
            bool step = type == GameAudioAiEvent.Footstep;
            if (type != GameAudioAiEvent.Gunshot && !step) return;
            try
            {
                if (sender != GameManager.GetPlayerObject()) return;
                float now = Time.unscaledTime;
                if (step)
                {
                    if (now - _lastStepAt < StepGap || !StepHeardByOthers(pos)) return;
                    _lastStepAt = now;
                }
                else
                {
                    if (now - _lastNoiseAt < 0.1f) return;
                    _lastNoiseAt = now;
                    if (PlayersHere(_scene, now, false).Count == 0) return;
                }
                var w = Msg.New(MsgType.AnimalNoise);
                w.Write((byte)Session.LocalId);
                w.Write(_scene ?? "");
                w.Write((byte)type);
                w.Write(pos.x); w.Write(pos.y); w.Write(pos.z);
                // Шаг — без гарантии доставки: потерянный шаг не беда, а надёжные события копятся в журнале хоста.
                if (step) { Session.SendFast(Msg.Done(w)); _stepsOut++; }
                else { Session.SendGameEvent(Msg.Done(w)); _noiseOut++; }
            }
            catch (Exception e) { if (_noiseLogs++ < 5) MelonLogger.Warning("[звери] шум другим: " + e.Message); }
        }

        // Рядом с местом шага есть живой зверь, которого ведёт другой игрок (у нас его копия), и он мог бы услышать.
        private static bool StepHeardByOthers(Vector3 pos)
        {
            foreach (var a in Animals.Values)
            {
                if (a.Real || a.Dead || a.Go == null || a.Ai == null) continue;
                float range = HearSteps(a.Ai) + 5f;
                if ((a.Go.transform.position - pos).sqrMagnitude < range * range) return true;
            }
            return false;
        }

        private static float HearSteps(BaseAi ai)
        {
            try { float r = ai.m_HearFootstepsRange; return r > 0f ? r : StepHearFallback; } catch { return StepHearFallback; }
        }

        private static void ReceiveNoise(BinaryReader r)
        {
            int from = r.ReadByte();
            string scene = r.ReadString();
            var type = (GameAudioAiEvent)r.ReadByte();
            var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            if (!Active || from == Session.LocalId || scene != _scene || OtherWorld(from)) return;
            var player = GameManager.GetPlayerObject();
            if (player == null) return;
            bool step = type == GameAudioAiEvent.Footstep;
            if (step) _stepsIn++; else _noiseIn2++;
            int answered = 0;
            foreach (var a in new List<Animal>(Animals.Values))
            {
                if (!a.Real || a.Shadow || a.Dead || a.Ai == null || a.Go == null || !a.Go.activeInHierarchy) continue;
                float range = step ? HearSteps(a.Ai) + 5f : NoiseRange;
                if ((a.Go.transform.position - pos).sqrMagnitude > range * range) continue;
                var before = a.Ai.m_CurrentMode;
                int focus = a.Focus;
                HitBy = from;   // подмена игрока — на стрелявшего (идущего)
                _noiseIn = true;
                _noiseStep = step;
                try
                {
                    BeforeAiUpdate(a.Ai, true);
                    _bForHit = false;   // от звука зверь не умирает — физику после подмены не пересчитываем
                    // Шаг слышит только тот, кто думает про идущего (подмена удалась): иначе зверь решал бы за нас —
                    // присели ли мы, а не он. Выстрел — как раньше, в любом случае.
                    if (!step || (Borrowing && BorrowId == from)) a.Ai.ProcessAudioEvent(player, pos, type);
                }
                catch (Exception e) { if (_noiseLogs++ < 5) MelonLogger.Warning("[звери] чужой шум: " + e.Message); }
                finally { AfterAiUpdate(); _noiseIn = false; _noiseStep = false; HitBy = -1; }
                var after = a.Ai.m_CurrentMode;
                try
                {
                    if (Locked(before) && Locked(after))
                    {
                        // Уже нападал или выслеживал — так и смотрит на прежнего, а не на стрелявшего.
                        if (focus != a.Focus) SetFocus(a, focus);
                    }
                    else if (after != before && after != AiMode.Dead && !Unaware(after))
                    {
                        // Звук его потревожил (услышал шаги — выслеживает, выстрел — бежит): цель — тот, кто шумел.
                        // Раньше после шагов, после которых волк начал выслеживать идущего, взгляд возвращали
                        // прежнему игроку, и волк выслеживал хозяина, а не друга.
                        a.Rotating = false;
                        SetFocus(a, from, $"услышал: {before} → {after}");
                    }
                    else if (step && focus != a.Focus) SetFocus(a, focus);   // шаг его не потревожил — взгляд прежний
                }
                catch { }
                if (after != before)
                {
                    answered++;
                    if (step) _stepsAnswered++; else _noiseAnswered++;
                    if (_noiseLogs++ < 30) MelonLogger.Msg($"[звери] {a.Prefab} услышал {(step ? "шаги" : "выстрел")} {NameOf(from)}: {before} → {after}");
                }
            }
        }

        private static string NoiseStats => $"выстрелов разослал {_noiseOut}, чужих получил {_noiseIn2}, звери ответили {_noiseAnswered}; шагов разослал {_stepsOut}, чужих получил {_stepsIn}, звери ответили {_stepsAnswered}";
    }

    [HarmonyPatch(typeof(GameAudioManager), nameof(GameAudioManager.NotifyAiAudioEvent))]
    internal static class Patch_AnimalNoiseOut
    {
        private static void Prefix(GameObject sender, Vector3 pos, GameAudioAiEvent eventType) => AnimalSync.OnLocalNoise(sender, pos, eventType);
    }
}
