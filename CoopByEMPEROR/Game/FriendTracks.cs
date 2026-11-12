using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Следы друга на снегу. Своих следов у фигуры нет, поэтому для каждого друга заводим отдельную «тропу»
    // игры (FootstepTrail, как у зверей) и на каждом шаге фигуры кладём отпечаток человеческого ботинка
    // туда, где под ногой снег. Тропа временная: в сохранение не попадает, со временем заметается как обычно.
    public static class FriendTracks
    {
        // Расстояние между соседними отпечатками. Берём таким же, как у своих следов игрока (меряем по его тропе):
        // с постоянными 0,75 м следы друга шли вдвое чаще, чем свои.
        private static float Stride = 0.75f;
        private static Vector3 _ownLast;
        private static bool _ownHas;
        private static int _ownCount;

        internal static void OnOwnFootstep(Vector3 pos)
        {
            if (_ownHas)
            {
                float d = new Vector2(pos.x - _ownLast.x, pos.z - _ownLast.z).magnitude;
                if (d > 0.3f && d < 2.5f)
                {
                    Stride = Mathf.Lerp(Stride, d, 0.2f);
                    if (++_ownCount == 20) MelonLogger.Msg($"[следы] шаг по своим следам: {Stride:0.00} м");
                }
            }
            _ownLast = pos;
            _ownHas = true;
        }
        private const float FootSide = 0.13f;    // нога от середины

        private class Track
        {
            public FootstepTrail Trail;
            public Vector3 Last;
            public bool Flip;
            public bool HasLast;
        }

        private static readonly Dictionary<int, Track> Tracks = new();
        private static string _scene = "";
        private static bool _broken;
        private static int _logged;
        // Массив игры, а не C#: обычный RaycastHit[] при вызове копируется, и игра писала попадания в копию —
        // у нас оставались пустые (0.12.1: «1 попаданий, первое» без имени, следов не было).
        private static readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit> Hits = new(8);

        // После обновления фигуры: sp — где она сейчас стоит.
        public static void Step(Player pl, Vector3 pos, float yaw)
        {
            if (_broken) return;
            try
            {
                string scene = GameApi.Scene();
                if (scene != _scene) { Tracks.Clear(); _scene = scene; }   // тропы старой сцены игра уже убрала
                var mgr = FootstepTrailManager.m_Instance;
                if (mgr == null) mgr = UnityEngine.Object.FindObjectOfType<FootstepTrailManager>();
                if (mgr == null) { Diag("нет FootstepTrailManager в сцене"); return; }
                if (!Tracks.TryGetValue(pl.Id, out var t)) Tracks[pl.Id] = t = new Track();
                bool walking = pl.Action == 0 || pl.Action == Activity.Eating || pl.Action == Activity.Drinking;
                if (!walking) { t.HasLast = false; return; }
                if (!t.HasLast) { t.Last = pos; t.HasLast = true; return; }
                var flat = new Vector3(pos.x - t.Last.x, 0f, pos.z - t.Last.z);
                float d = flat.magnitude;
                if (d > 6f) { Close(t); t.Last = pos; return; }   // телепорт — новая тропа
                if (d < Stride) return;
                var dir = flat / d;
                var at = t.Last + dir * Stride;
                at.y = pos.y;
                t.Last = at;
                t.Flip = !t.Flip;
                var side = Vector3.Cross(Vector3.up, dir) * (t.Flip ? -FootSide : FootSide);
                // По звуку шагов друга знаем, по чему он идёт: по дереву, камню, льду следов не оставляем.
                if (StepTags.TryGetValue(pl.Id, out var tag) && tag.Length > 0 && tag.IndexOf("snow", StringComparison.OrdinalIgnoreCase) < 0) return;
                if (!Ground(at + side, out var point, out var normal)) { Diag("под ногой ничего не нашёл"); return; }
                if (t.Trail == null)
                {
                    t.Trail = new FootstepTrail(FootstepTrailType.AiTransient, SnowImprintType.HumanFootprint);
                    try { t.Trail.Initialize(FootstepTrailType.AiTransient, SnowImprintType.HumanFootprint); } catch { }
                    mgr.AddFootstepTrail(t.Trail, false);
                }
                t.Trail.AddFootstep(at, point, normal, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, t.Flip, 0, 0f);
                if (_logged++ == 0) MelonLogger.Msg($"[следы] первый след {pl.Name} на снегу");
            }
            catch (Exception e)
            {
                _broken = true;
                MelonLogger.Warning("[следы] выключены: " + e.Message);
            }
        }

        // Земля под ногой: ближайшее попадание вниз (вещи с физикой пропускаем). Снег ли это, решает звук шагов
        // друга (StepTags): в 0.11.26 требовали тег снега у коллайдера, а у земли его нет — следов не было.
        private static bool Ground(Vector3 foot, out Vector3 point, out Vector3 normal)
        {
            point = foot; normal = Vector3.up;
            int n = Physics.RaycastNonAlloc(foot + Vector3.up * 1.5f, Vector3.down, Hits, 4f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = Hits[i];
                if (h.collider == null || h.distance >= best || h.collider.isTrigger || h.collider.attachedRigidbody != null) continue;
                if (h.collider.gameObject.name.StartsWith("TLDCoop")) continue;   // фигура самого друга
                if (_tagLogged++ == 0) MelonLogger.Msg($"[следы] под ногой {h.collider.gameObject.name}, тег {h.collider.gameObject.tag}");
                best = h.distance;
                point = h.point;
                normal = h.normal;
                found = true;
            }
            if (!found && _missLogged++ < 3)
                MelonLogger.Msg($"[следы] под ногой пусто: {n} попаданий, нога на высоте {foot.y:0.0}" + (n > 0 ? ", первое " + Hits[0].collider?.gameObject.name : ""));
            return found;
        }

        private static int _missLogged;

        private static void Close(Track t)
        {
            try { t.Trail?.Close(); } catch { }
            t.Trail = null;
        }

        // Материал под ногой друга (из его шагов, Game/SoundSync.cs), например Snow, Wood.
        private static readonly Dictionary<int, string> StepTags = new();
        private static int _tagLogged;
        private static readonly HashSet<string> Diags = new();

        public static void SetStepTag(int id, string tag)
        {
            if (!StepTags.TryGetValue(id, out var old) || old != tag)
            {
                StepTags[id] = tag ?? "";
                if (Diags.Add("tag:" + tag)) MelonLogger.Msg($"[следы] шаги друга по: {tag}");
            }
        }

        private static void Diag(string what)
        {
            if (Diags.Add(what)) MelonLogger.Msg("[следы] " + what);
        }

        public static void Forget(int id)
        {
            if (Tracks.TryGetValue(id, out var t)) { Close(t); Tracks.Remove(id); }
        }

        public static void Reset()
        {
            foreach (var t in Tracks.Values) Close(t);
            Tracks.Clear();
            _scene = "";
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(FootstepTrail), nameof(FootstepTrail.AddFootstep))]
    internal static class Patch_OwnFootstep
    {
        private static void Postfix(FootstepTrail __instance, Vector3 pos)
        {
            try
            {
                var mgr = FootstepTrailManager.m_Instance;
                if (mgr != null && mgr.m_ActivePlayerTrail != null && mgr.m_ActivePlayerTrail.Pointer == __instance.Pointer) FriendTracks.OnOwnFootstep(pos);
            }
            catch { }
        }
    }
}
