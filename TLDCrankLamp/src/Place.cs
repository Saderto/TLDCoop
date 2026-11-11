using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace TLDCrankLamp
{
    /// <summary>
    /// One crank lamp per world, at one fixed place (user 11-09: no random spawns, a single lamp we put ourselves).
    /// Ctrl+F7 in game: the lamp's place becomes the aimed point (Mods/TLDCrankLamp/place.txt "scene;x;y;z;yaw");
    /// a lamp already lying at the old place moves there at once. The first time a world visits that scene, the lamp is
    /// put there once ("world|scene|place" in spawned.txt, world = slot + game id + seed); if a lamp already lies
    /// within 2.5 m, nothing is added. The game saves the lamp with the region like any other item.
    /// </summary>
    internal static class Place
    {
        // built-in place (filled in from the user's Ctrl+F7 mark in a later build; place.txt wins)
        // 11-11: the user's mark, the hunting lodge in Broken Railroad
        static string _scene = "HuntingLodgeA";
        static Vector3 _pos = new Vector3(-5.797f, -2.487f, 4.31f);
        static float _yaw = 31f;
        static bool _read;

        static string PlaceFile => Path.Combine(LampMod.Folder, "place.txt");
        static string DoneFile => Path.Combine(LampMod.Folder, "spawned.txt");
        static HashSet<string> _done;
        static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        static void Read()
        {
            if (_read) return;
            _read = true;
            try
            {
                if (!File.Exists(PlaceFile)) return;
                foreach (var l in File.ReadAllLines(PlaceFile))
                {
                    var a = l.Split(';');
                    if (a.Length < 5) continue;
                    _scene = a[0];
                    _pos = new Vector3(float.Parse(a[1], CI), float.Parse(a[2], CI), float.Parse(a[3], CI));
                    _yaw = float.Parse(a[4], CI);
                }
            }
            catch (Exception e) { LampMod.Warn("place: " + e.Message); }
        }

        static HashSet<string> Done()
        {
            if (_done != null) return _done;
            _done = new HashSet<string>();
            try { if (File.Exists(DoneFile)) foreach (var l in File.ReadAllLines(DoneFile)) if (l.Length > 0) _done.Add(l.Trim()); } catch { }
            return _done;
        }

        static void MarkDone(string key)
        {
            Done().Add(key);
            try { File.AppendAllText(DoneFile, key + Environment.NewLine); } catch { }
        }

        static void Hud(string ru, string en)
        {
            try { HUDMessage.AddMessage(Texts.IsRussian() ? ru : en, 3f, false, true); } catch { }
            LampMod.Instance.LoggerInstance.Msg(en);
        }

        static bool IsLamp(GearItem gi) => gi != null && gi.name != null && gi.name.StartsWith(Assets.Gear, StringComparison.Ordinal);

        static List<GearItem> LampsNear(Vector3 p, float r)
        {
            var list = new List<GearItem>();
            foreach (var gi in UnityEngine.Object.FindObjectsOfType<GearItem>())
            {
                if (!IsLamp(gi) || gi.m_InPlayerInventory || !gi.gameObject.activeInHierarchy) continue;
                if ((gi.transform.position - p).sqrMagnitude < r * r) list.Add(gi);
            }
            return list;
        }

        /// <summary>Ctrl+F7: the lamp's place is here now.</summary>
        internal static void Update()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (!ctrl || !Input.GetKeyDown(KeyCode.F7)) return;
            try
            {
                if (GameManager.GetPlayerObject() == null) return;
                var cam = GameManager.GetMainCamera();
                if (cam == null) return;
                if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                { Hud("Слишком далеко: подойди ближе и наведи прицел на место", "Too far: come closer and aim at the spot"); return; }
                Read();
                string scene = GameManager.m_ActiveScene ?? "?";
                float yaw = cam.transform.eulerAngles.y;
                var old = string.Equals(scene, _scene, StringComparison.OrdinalIgnoreCase) ? LampsNear(_pos, 2.5f) : new List<GearItem>();
                _scene = scene; _pos = hit.point; _yaw = yaw;
                File.WriteAllText(PlaceFile, string.Join(";", scene, _pos.x.ToString("0.###", CI), _pos.y.ToString("0.###", CI), _pos.z.ToString("0.###", CI), yaw.ToString("0", CI)) + Environment.NewLine);
                foreach (var gi in old) Settle(gi);
                if (old.Count == 0)
                {
                    // put it here right away in this world, so the place can be seen
                    string save = "";
                    try { save = SaveGameSystem.GetCurrentSaveName() ?? ""; } catch { }
                    var gi = Spawn();
                    if (gi != null && save.Length > 0) MarkDone(Key(save));
                }
                Hud($"Место фонаря-динамо: {scene}. Запомнено", $"Crank lamp place: {scene}. Saved");
            }
            catch (Exception e) { LampMod.Warn("mark: " + e.Message); }
        }

        static string WorldKey(string save)
        {
            uint id = 0; int seed = 0;
            try { id = SaveGameSlots.GetSaveSlotFromName(save)?.m_GameId ?? 0; } catch { }
            try { seed = GameManager.m_SceneTransitionData?.m_GameRandomSeed ?? 0; } catch { }
            return save + "_" + id + "_" + seed;
        }

        static string Key(string save) => $"{WorldKey(save)}|{_scene}|{_pos.x.ToString("0", CI)},{_pos.z.ToString("0", CI)}";

        internal static void OnScene(string scene)
        {
            Read();
            if (_scene.Length == 0 || !string.Equals(scene, _scene, StringComparison.OrdinalIgnoreCase)) return;
            // coop shared world (the same rule as the pipes of SmokesBySeagull 0.1.124): the lamp is put only on the very first
            // visit of the scene in that world (the scene is not in the save yet). Whoever comes first puts it, TLDCoop hands
            // that layout to the others; later visitors get the scene from the save or the layout, with the lamp in it or
            // already taken. Checked now, before the game saves this scene.
            bool? firstHere = null;
            if (CoopSharedWorld())
            {
                try
                {
                    string save = SaveGameSystem.GetCurrentSaveName() ?? "";
                    string key = GameManager.m_SceneTransitionData?.m_SceneSaveFilenameCurrent ?? "";
                    if (string.IsNullOrEmpty(key)) key = scene;
                    firstHere = !string.IsNullOrEmpty(save) && !SaveGameSlots.HasFilenameInSlot(save, key);
                    LampMod.Instance.LoggerInstance.Msg($"место фонаря: общий мир коопа, {key} {(firstHere == true ? "первый визит / first visit" : "уже в сохранении / already in the save")}");
                }
                catch (Exception e) { LampMod.Warn("place coop: " + e.Message); firstHere = false; }
            }
            MelonCoroutines.Start(PlaceLater(scene, firstHere));
        }

        /// <summary>TLDCoop (by reflection): this player is in a coop shared world (the host in its own, a friend in the host's).</summary>
        static bool CoopSharedWorld()
        {
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.GetName().Name != "TLDCoop") continue;
                    var t = a.GetType("TLDCoop.Net.Session");
                    if (t == null) return false;
                    const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                    var mode = t.GetProperty("Mode", F)?.GetValue(null)?.ToString();
                    var inHost = t.GetProperty("InHostWorld", F)?.GetValue(null) as bool?;
                    return mode == "Host" || (mode == "Client" && inHost == true);
                }
            }
            catch (Exception e) { LampMod.Warn("coop check: " + e.Message); }
            return false;
        }

        static IEnumerator PlaceLater(string scene, bool? sharedFirst)
        {
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 60f)
            {
                bool ready = false;
                try { ready = GameManager.GetPlayerObject() != null && string.Equals(GameManager.m_ActiveScene, scene, StringComparison.OrdinalIgnoreCase) && !GameManager.IsMainMenuActive(); } catch { }
                if (ready) break;
                yield return null;
            }
            float t1 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t1 < .4f) yield return null;

            string save = "";
            try { save = SaveGameSystem.GetCurrentSaveName() ?? ""; } catch { }
            if (string.IsNullOrEmpty(save)) yield break;
            if (sharedFirst == false) { LampMod.Log("место фонаря: сцена уже есть в общем мире, фонарь приходит оттуда / lamp comes from the shared world"); yield break; }
            string key = Key(save);
            if (Done().Contains(key)) yield break;
            if (LampsNear(_pos, 2.5f).Count > 0) { MarkDone(key); yield break; }
            if (Spawn() != null)
            {
                MarkDone(key);
                LampMod.Instance.LoggerInstance.Msg($"положил фонарь-динамо: {_scene} {_pos} / placed");
            }
        }

        static GearItem Spawn()
        {
            try
            {
                var gi = GearItem.InstantiateGearItem(Assets.Gear);
                if (gi == null) { LampMod.Warn("lamp not created"); return null; }
                gi.m_RolledSpawnChance = true;
                var root = GearManager.m_GearCategory;
                if (root != null) gi.transform.SetParent(root.transform, true); // saved with the region, like GearItem.Drop
                Settle(gi);
                return gi;
            }
            catch (Exception e) { LampMod.Warn("spawn: " + e.Message); return null; }
        }

        // standing upright, turned to face the player who marked it, the lowest point of the model on the ground
        static void Settle(GearItem gi)
        {
            gi.transform.rotation = Quaternion.Euler(0f, _yaw + 180f, 0f);
            gi.transform.position = _pos + Vector3.up * 3f;
            float ground = _pos.y;
            var hits = Physics.RaycastAll(new Vector3(_pos.x, _pos.y + 0.5f, _pos.z), Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.GetComponentInParent<GearItem>() != null) continue;
                float dy = Mathf.Abs(h.point.y - _pos.y);
                if (dy < best) { best = dy; ground = h.point.y; }
            }
            float minY = float.MaxValue;
            foreach (var r in gi.GetComponentsInChildren<Renderer>(true)) if (r != null && r.enabled && !(r is ParticleSystemRenderer)) minY = Mathf.Min(minY, r.bounds.min.y);
            float lift = minY < float.MaxValue ? gi.transform.position.y - minY : 0f;
            gi.transform.position = new Vector3(_pos.x, ground + lift + 0.003f, _pos.z);
        }
    }
}
