using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace CrankLampByEMPEROR
{
    /// <summary>
    /// What the coop mod (CoopByEMPEROR) asks us by reflection, with no hard link between the dlls (the same way as SmokesByEMPEROR.CoopApi).
    /// LocalCranking: this player is turning the crank right now (CoopByEMPEROR sends it with the held item).
    /// RemoteCrank: a friend's figure started or stopped cranking; copy is the lamp in the figure's hand. We play the crank
    /// sound from that lamp and turn its handle.
    /// </summary>
    public static class CoopApi
    {
        sealed class Remote { public GameObject Go; public Transform Bone; public Quaternion Base; public float Angle; public float NextLoop; }
        static readonly Dictionary<IntPtr, Remote> _on = new();
        static readonly List<IntPtr> _dead = new();

        public static bool LocalCranking()
        {
            try { return Crank.Cranking; } catch { return false; }
        }

        public static void RemoteCrank(GameObject copy, bool on)
        {
            try
            {
                if (copy == null) return;
                var key = copy.Pointer;
                if (on == _on.ContainsKey(key)) return;
                if (on)
                {
                    var r = new Remote { Go = copy };
                    foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                        if (t != null && t.name == "HandCrankLight:Crank_jnt") { r.Bone = t; r.Base = t.localRotation; break; }
                    _on[key] = r;
                    r.NextLoop = Time.time + Sound.CrankStartSound(copy);
                    LampMod.Log($"кооп: друг крутит ручку{(r.Bone != null ? "" : " (ручки у копии нет)")} / coop: friend cranks");
                }
                else
                {
                    var r = _on[key];
                    _on.Remove(key);
                    if (r.Bone != null) r.Bone.localRotation = r.Base;
                }
            }
            catch (Exception e) { LampMod.Warn("coop crank: " + e.Message); }
        }

        internal static void LateTick()
        {
            if (_on.Count == 0) return;
            foreach (var kv in _on)
            {
                var r = kv.Value;
                bool alive = false;
                try { alive = r.Go != null && r.Go.activeInHierarchy; } catch { }
                if (!alive) { _dead.Add(kv.Key); continue; }
                try
                {
                    if (Time.time >= r.NextLoop) { r.NextLoop = Time.time + Sound.CrankTick(r.Go); }
                    if (r.Bone != null)
                    {
                        r.Angle = (r.Angle + 720f * Time.deltaTime) % 360f;
                        r.Bone.localRotation = r.Base * Quaternion.AngleAxis(r.Angle, Vector3.right);
                    }
                }
                catch { }
            }
            if (_dead.Count > 0) { foreach (var k in _dead) _on.Remove(k); _dead.Clear(); }
        }
    }
}
