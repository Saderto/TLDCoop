using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Жалоба: «пока друг дерётся с волком и после схватки у меня слышно замедленный звук». Через GameAudioManager.PlaySound
    // моя игра в это время эффектов схватки не включала (в логе «заглушено раньше: 0»), значит звук идёт мимо него —
    // прямо через Wwise (AK.Wwise.Event.Post → AkSoundEngine.PostEvent) или это параметр (RTPC) / состояние Wwise.
    // Поэтому, только пока друг в схватке и 20 с после неё, вешаемся на сами AkSoundEngine.PostEvent / SetRTPCValue /
    // SetState: пишем в лог, что моя игра включает и меняет, и глушим эффекты схватки (замедление, глушение), если сам
    // я ни с кем не дерусь. Потом патчи снимаем — в обычной игре этих перехватов нет.
    public static class SoundDiag
    {
        private static HarmonyLib.Harmony _h;
        private static bool _on, _failed;
        private static float _until, _started;
        private static int _lines;
        private const int MaxLines = 150;
        private static readonly HashSet<string> Seen = new();
        private static readonly Dictionary<string, float> Rtpc = new();
        private static readonly Dictionary<string, int> RtpcCount = new();
        private static Dictionary<uint, string> _names;
        [ThreadStatic] public static bool Self;   // звук ставит сам мод (звуки друга) — не пишем и не глушим

        public static void Tick(bool remoteStruggle, bool localStruggle)
        {
            float now = Time.unscaledTime;
            if (remoteStruggle) _until = now + 20f;
            if (!_on && remoteStruggle && !localStruggle && !_failed) Enable(now);
            else if (_on && now > _until) Disable();
        }

        private static void Enable(float now)
        {
            try
            {
                _h ??= new HarmonyLib.Harmony("tldcoop.sounddiag");
                var t = typeof(AkSoundEngine);
                int n = 0;
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    var ps = m.GetParameters();
                    if (ps.Length == 0) continue;
                    var p0 = ps[0].ParameterType;
                    string post = null;
                    // Только простые перегрузки (id/имя + объект): с обратным вызовом и внешними источниками не трогаем.
                    if (m.Name == "PostEvent" && ps.Length == 2 && m.ReturnType == typeof(uint))
                        post = p0 == typeof(uint) ? nameof(PostU) : p0 == typeof(string) ? nameof(PostS) : null;
                    else if (m.Name == "SetRTPCValue" && ps.Length >= 2 && ps[1].ParameterType == typeof(float))
                        post = p0 == typeof(uint) ? nameof(RtpcU) : p0 == typeof(string) ? nameof(RtpcS) : null;
                    else if (m.Name == "SetState" && ps.Length == 2)
                        post = p0 == typeof(uint) ? nameof(StateU) : p0 == typeof(string) ? nameof(StateS) : null;
                    if (post == null) continue;
                    try { _h.Patch(m, postfix: new HarmonyMethod(typeof(SoundDiag).GetMethod(post, BindingFlags.NonPublic | BindingFlags.Static))); n++; }
                    catch (Exception e) { MelonLogger.Warning($"[звуки-диаг] не вышло перехватить {m.Name}({string.Join(",", ps.Select(p => p.ParameterType.Name))}): {e.Message}"); }
                }
                _on = true;
                _started = now;
                _lines = 0;
                Seen.Clear();
                Rtpc.Clear();
                RtpcCount.Clear();
                MelonLogger.Msg($"[звуки-диаг] друг в схватке: слушаю, что включает моя игра (перехватов {n}), до 20 с после схватки");
            }
            catch (Exception e) { _failed = true; MelonLogger.Warning("[звуки-диаг] " + e.Message); }
        }

        private static void Disable()
        {
            _on = false;
            try { _h?.UnpatchSelf(); } catch (Exception e) { MelonLogger.Warning("[звуки-диаг] снять: " + e.Message); }
            MelonLogger.Msg($"[звуки-диаг] перестал слушать (строк {_lines})");
        }

        private static string Name(uint id)
        {
            if (_names == null)
            {
                var d = new Dictionary<uint, string>();
                try
                {
                    var asm = typeof(Il2CppAK.EVENTS).Assembly;
                    foreach (var ty in asm.GetTypes())
                    {
                        if (ty.Namespace == null || !ty.Namespace.StartsWith("Il2CppAK")) continue;
                        foreach (var f in ty.GetFields(BindingFlags.Public | BindingFlags.Static))
                            if (f.FieldType == typeof(uint) && f.IsLiteral) { try { d.TryAdd((uint)f.GetRawConstantValue(), (ty.Name == "EVENTS" ? "" : ty.Name + ".") + f.Name); } catch { } }
                    }
                }
                catch { }
                _names = d;
            }
            return _names.TryGetValue(id, out var s) ? s : "#" + id;
        }

        private static bool Busy() => !_on || Self || _lines >= MaxLines;
        private static string At() => $"+{Time.unscaledTime - _started:0.0} с";

        private static void Line(string s)
        {
            _lines++;
            MelonLogger.Msg($"[звуки-диаг] {At()}: {s}{(_lines == MaxLines ? " (дальше не пишу)" : "")}");
        }

        private static bool LocalStruggle()
        {
            try { var ps = GameManager.GetPlayerStruggleComponent(); return ps != null && (ps.m_Active || ps.InStruggle()); }
            catch { return true; }
        }

        private static void Post(string name, uint result)
        {
            bool effect = SoundSync.IsStruggleEffectName(name);
            if (effect && result != 0 && !LocalStruggle())
            {
                try { AkSoundEngine.StopPlayingID(result); } catch { }
                Line($"событие {name} — эффект схватки, а я не дерусь: заглушил. Откуда: {Environment.StackTrace}");
                return;
            }
            if (Seen.Add("e:" + name)) Line("событие " + name);
        }

        private static void PostU(uint __0, uint __result) { if (!_on || Self) return; try { if (_lines < MaxLines || SoundSync.IsStruggleEffectName(Name(__0))) Post(Name(__0), __result); } catch { } }
        private static void PostS(string __0, uint __result) { if (!_on || Self || __0 == null) return; try { Post(__0, __result); } catch { } }

        private static void Param(string name, float v)
        {
            if (Rtpc.TryGetValue(name, out var old) && Mathf.Abs(old - v) < 0.05f * Mathf.Max(1f, Mathf.Abs(old))) return;
            Rtpc[name] = v;
            RtpcCount.TryGetValue(name, out int c);
            if (c >= 4) return;   // часто меняющийся параметр (ветер, расстояние) — первые несколько раз хватит
            RtpcCount[name] = c + 1;
            Line($"параметр {name} = {v:0.###}");
        }

        private static void RtpcU(uint __0, float __1) { if (Busy()) return; try { Param(Name(__0), __1); } catch { } }
        private static void RtpcS(string __0, float __1) { if (Busy() || __0 == null) return; try { Param(__0, __1); } catch { } }
        private static void StateU(uint __0, uint __1) { if (Busy()) return; try { Line($"состояние {Name(__0)} = {Name(__1)}"); } catch { } }
        private static void StateS(string __0, string __1) { if (Busy()) return; try { Line($"состояние {__0} = {__1}"); } catch { } }
    }
}
