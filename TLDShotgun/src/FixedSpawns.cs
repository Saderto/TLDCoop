using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace TLDShotgun
{
    /// <summary>
    /// Fixed places chosen by the user (marked in 0.1.11–0.1.30 with placement keys, now built in). The first time a save visits
    /// the scene, the item is put there once; "save|scene|spot" goes to Mods/TLDShotgun/spawned.txt so it never comes
    /// back after it is taken. The item itself is saved by the game like any other item on the ground.
    /// </summary>
    internal static class FixedSpawns
    {
        sealed class Spot { public string Id, Kind, Scene; public Vector3 Pos; public float Yaw; public int Units = 1; }

        // 35 cm to the side of the gun, toward the player who marked it (0.1.28, user: shells under the gun on screen, not past it)
        static Vector3 Beside(Vector3 p, float yaw) => p + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -0.35f);

        static readonly Spot[] All =
        {
            // ids "…b" since 0.1.23: before it the items were not saved with the region (no parent), so they spawn once more.
            // 0.1.30: places as the user moved them with Ctrl+F7 (places.txt 05:01), built in so the friend has the same.
            // 11-08 04:30, user: Timberwolf Mountain; 04:38 Hushed River Valley. Next to each gun 3 shells (user 04:38).
            new Spot { Id = "shotgun-1b", Kind = "shotgun", Scene = "CrashMountainRegion", Pos = new Vector3(919.053f, 468.735f, 1210.674f), Yaw = 95f },
            new Spot { Id = "shells-1b", Kind = "shells", Units = 3, Scene = "CrashMountainRegion", Pos = Beside(new Vector3(919.053f, 468.735f, 1210.674f), 95f), Yaw = 125f },
            new Spot { Id = "shotgun-2c", Kind = "shotgun", Scene = "RiverValleyRegion", Pos = new Vector3(1431.593f, 286.454f, 1305.866f), Yaw = 140f },
            new Spot { Id = "shells-2c", Kind = "shells", Units = 3, Scene = "RiverValleyRegion", Pos = Beside(new Vector3(1431.593f, 286.454f, 1305.866f), 140f), Yaw = 170f },
        };

        // 0.1.33: Ctrl+F7 moving and places.txt removed (placement is done, places are built into All above).

        static string DoneFile => Path.Combine(ShotgunMod.Folder, "spawned.txt");
        static HashSet<string> _done;

        // keys written by 0.1.26–0.1.30 ("slot_gameId_seed|…") count as "slot_sSeed|…"
        static string Normalize(string key)
        {
            int bar = key.IndexOf('|');
            if (bar < 0) return key;
            var m = System.Text.RegularExpressions.Regex.Match(key.Substring(0, bar), @"^(.*)_(\d+)_(-?\d+)$");
            return m.Success ? m.Groups[1].Value + "_s" + m.Groups[3].Value + key.Substring(bar) : key;
        }

        static HashSet<string> Done()
        {
            if (_done != null) return _done;
            _done = new HashSet<string>();
            try { if (File.Exists(DoneFile)) foreach (var l in File.ReadAllLines(DoneFile)) if (l.Length > 0) { _done.Add(l.Trim()); _done.Add(Normalize(l.Trim())); } } catch { }
            return _done;
        }

        // 0.1.21: lie on its side (roll 90° around the barrel) and sit on the ground: the mark is a hit point that
        // may be on a collider above the snow, so look for the ground straight down and put the lowest point of the
        // model on it (0.1.20 stood upright and hung in the air).
        static void Settle(GearItem gi, Spot s)
        {
            var flat = Quaternion.Euler(0f, s.Yaw + 90f, s.Kind == "shotgun" ? 90f : 0f);
            gi.transform.rotation = flat;
            gi.transform.position = s.Pos + Vector3.up * 5f; // out of the way of our own rays
            // 0.1.25: the ground is sloped; 0.1.26: the gun is 1.5 m long (muzzle −0.71, butt +0.79 along its axis), so
            // sample the ground every ~15 cm along the whole length and 20 cm to each side, fit the slope, then lift the
            // gun over the highest bump (0.1.25 looked only ±0.6 m, and the barrel went into the cave floor).
            var axis = flat * Vector3.forward; axis.y = 0f; axis.Normalize();
            var side = Vector3.Cross(Vector3.up, axis);
            Vector3 c = Ground(gi, s.Pos, s.Pos.y);
            bool gun = s.Kind == "shotgun";
            float t0 = gun ? -0.75f : -0.15f, t1 = gun ? 0.82f : 0.15f;
            var pts = new List<Vector3>();
            float st = 0, sh = 0, stt = 0, sth = 0; int cnt = 0;
            for (float t = t0; t <= t1 + 0.001f; t += (t1 - t0) / 10f)
            {
                var g = Ground(gi, s.Pos + axis * t, c.y);
                pts.Add(g);
                float h = g.y - c.y; st += t; sh += h; stt += t * t; sth += t * h; cnt++;
            }
            float den = cnt * stt - st * st;
            float slope = Mathf.Abs(den) > 1e-5f ? (cnt * sth - st * sh) / den : 0f;   // height per metre along the gun
            Vector3 l = Ground(gi, s.Pos + side * 0.2f, c.y), r = Ground(gi, s.Pos - side * 0.2f, c.y);
            pts.Add(l); pts.Add(r);
            float sideSlope = (l.y - r.y) / 0.4f;
            slope = Mathf.Clamp(slope, -0.7f, 0.7f); sideSlope = Mathf.Clamp(sideSlope, -0.7f, 0.7f);
            var n = Vector3.Cross(axis + Vector3.up * slope, side + Vector3.up * sideSlope).normalized;
            if (n.y < 0f) n = -n;
            if (float.IsNaN(n.x)) n = Vector3.up;
            var rot = Quaternion.FromToRotation(Vector3.up, n) * flat;
            gi.transform.rotation = rot;
            float top = float.MinValue;
            foreach (var g in pts) top = Mathf.Max(top, Vector3.Dot(g - c, n));
            Vector3 basePt = c + n * Mathf.Max(0f, top);
            if (s.Kind == "shotgun")
                gi.transform.position = basePt + n * 0.045f;   // the model is 8 cm thick around its pivot (x −0.04…0.04)
            else
            {
                gi.transform.position = basePt + n * 0.2f;
                var rends = gi.GetComponentsInChildren<Renderer>(true);
                float minY = float.MaxValue;
                foreach (var rr in rends) if (rr != null && rr.enabled) minY = Mathf.Min(minY, rr.bounds.min.y);
                float lift = minY < float.MaxValue ? gi.transform.position.y - minY : 0.02f;
                gi.transform.position = new Vector3(basePt.x, basePt.y + lift + 0.005f, basePt.z);
            }
        }

        // the surface under p nearest in height to y (cave floors, slopes); our own item is ignored
        static Vector3 Ground(GearItem gi, Vector3 p, float y)
        {
            var hits = Physics.RaycastAll(new Vector3(p.x, y + 0.6f, p.z), Vector3.down, 1.6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; Vector3 at = new Vector3(p.x, y, p.z);
            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.transform.IsChildOf(gi.transform)) continue;
                if (h.collider.GetComponentInParent<GearItem>() != null) continue;
                float dy = Mathf.Abs(h.point.y - y);
                if (dy < best) { best = dy; at = h.point; }
            }
            return at;
        }

        // a gun put by an older build (upright, or sunk into a slope) is still lying there: lay it again once (0.1.25 rule)
        static void FixOld(Spot s, string key)
        {
            if (s.Kind == "shells")
            {
                // 0.1.28: shells used to lie past the gun; put the ones already lying there on the player side, once
                string k3 = key + "|side28";
                if (Done().Contains(k3)) return;
                foreach (var gi in UnityEngine.Object.FindObjectsOfType<GearItem>())
                {
                    if (gi == null || gi.m_InPlayerInventory || !gi.gameObject.activeInHierarchy) continue;
                    if (!gi.name.StartsWith("GEAR_ShotgunAmmoSingle")) continue;
                    if ((gi.transform.position - s.Pos).sqrMagnitude > 1.2f * 1.2f) continue;
                    Settle(gi, s);
                    ShotgunMod.Instance.LoggerInstance.Msg($"патроны на месте {s.Id} перенесены к игроку / shells moved");
                }
                Done().Add(k3);
                File.AppendAllText(DoneFile, k3 + Environment.NewLine);
                return;
            }
            if (s.Kind != "shotgun") return;
            string k2 = key + "|lay26";
            if (Done().Contains(k2)) return;
            foreach (var gi in UnityEngine.Object.FindObjectsOfType<GearItem>())
            {
                if (gi == null || gi.m_InPlayerInventory || !gi.gameObject.activeInHierarchy) continue;
                if (!gi.name.StartsWith("GEAR_Shotgun") || gi.name.Contains("Ammo")) continue;
                if ((gi.transform.position - s.Pos).sqrMagnitude > 2.5f * 2.5f) continue;
                Settle(gi, s);
                ShotgunMod.Instance.LoggerInstance.Msg($"дробовик на месте {s.Id} положен заново / re-settled");
            }
            Done().Add(k2);
            File.AppendAllText(DoneFile, k2 + Environment.NewLine);
        }

        static bool LyingThere(Spot s)
        {
            foreach (var gi in UnityEngine.Object.FindObjectsOfType<GearItem>())
            {
                if (gi == null || gi.m_InPlayerInventory || !gi.gameObject.activeInHierarchy) continue;
                bool gun = gi.name.StartsWith("GEAR_Shotgun") && !gi.name.Contains("Ammo");
                bool shell = gi.name.StartsWith("GEAR_ShotgunAmmoSingle");
                if (s.Kind == "shotgun" ? !gun : !shell) continue;
                if ((gi.transform.position - s.Pos).sqrMagnitude < 2.5f * 2.5f) return true;
            }
            return false;
        }

        // the world, not just the slot: a new game in the same slot (sandbox3 deleted and started again) has another seed
        static string WorldKey(string save)
        {
            // 0.1.31: slot + world seed only. The game id is still 0 in the first scene of a new game and appears after
            // the first save, so slot_id_seed changed between visits (FoodPack saw the same, 11-09).
            int seed = 0;
            try { seed = GameManager.m_SceneTransitionData?.m_GameRandomSeed ?? 0; } catch { }
            return save + "_s" + seed;
        }

        internal static void OnScene(string scene)
        {
            foreach (var s in All)
                if (string.Equals(s.Scene, scene, StringComparison.OrdinalIgnoreCase)) { MelonCoroutines.Start(Place(scene)); return; }
        }

        static IEnumerator Place(string scene)
        {
            float t0 = Time.realtimeSinceStartup;
            // wait until the scene is really in play (save loaded, player there)
            while (Time.realtimeSinceStartup - t0 < 60f)
            {
                bool ready = false;
                try { ready = GameManager.GetPlayerObject() != null && string.Equals(GameManager.m_ActiveScene, scene, StringComparison.OrdinalIgnoreCase) && !GameManager.IsMainMenuActive(); } catch { }
                if (ready) break;
                yield return null;
            }
            float t1 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t1 < 4f) yield return null;

            string save = "";
            try { save = SaveGameSystem.GetCurrentSaveName() ?? ""; } catch { }
            if (string.IsNullOrEmpty(save)) yield break;
            foreach (var s in All)
            {
                if (!string.Equals(s.Scene, scene, StringComparison.OrdinalIgnoreCase)) continue;
                string key = $"{WorldKey(save)}|{s.Scene}|{s.Id}";
                if (!Done().Contains(key) && LyingThere(s))
                {
                    Done().Add(key); File.AppendAllText(DoneFile, key + Environment.NewLine);
                    try { FixOld(s, key); } catch { }
                    continue;
                }
                if (Done().Contains(key)) { try { FixOld(s, key); } catch (Exception e) { ShotgunMod.Warn("fix old: " + e.Message); } continue; }
                try
                {
                    string prefab = s.Kind == "shotgun" ? "GEAR_Shotgun" : s.Kind == "box" ? "GEAR_ShotgunAmmoBox" : "GEAR_ShotgunAmmoSingle";
                    var gi = GearItem.InstantiateGearItem(prefab);
                    if (gi == null) { ShotgunMod.Warn("fixed spawn: " + prefab + " not created"); continue; }
                    gi.m_RolledSpawnChance = true;
                    Loot.ToScene(gi);
                    Settle(gi, s);
                    if (s.Kind == "shells" && gi.m_StackableItem != null) gi.m_StackableItem.m_Units = s.Units;
                    Done().Add(key);
                    File.AppendAllText(DoneFile, key + Environment.NewLine);
                    ShotgunMod.Instance.LoggerInstance.Msg($"положил {prefab} на место {s.Id} ({s.Scene}) / fixed spawn");
                }
                catch (Exception e) { ShotgunMod.Warn("fixed spawn: " + e.Message); }
            }
        }
    }
}
