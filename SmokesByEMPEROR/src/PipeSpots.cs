using System.Collections;

namespace SmokesByEMPEROR;

/// <summary>The four pipes are found only in four fixed places, one each (user 11-09): Mystery Lake camp office, Coastal Highway gas station,
/// Pleasant Valley farmhouse, Blackrock prison warden's office. The first time a world visits the scene, the pipe is put there once:
/// "world|scene|pipe" goes to placed.txt (world = slot + game id + seed, so a new game in the same slot gets them again), and nothing is put
/// if a pipe of that kind already lies nearby (a coop friend's copy of the host's scene, an older placing).</summary>
internal static class PipeSpots
{
    sealed record Spot(string Pipe, string Scene, Vector3 Pos, float Yaw);

    // filled from the scene scan (research/pipe_spots.json)
    static readonly Spot[] All =
    {
        new("PipeBilliard", "CampOffice", new Vector3(3.149f, 1.177f, -0.005f), 0f),                          // Mystery Lake, camp office: the counter with the laptop
        new("PipeBent", "QuonsetGasStation", new Vector3(5.680f, 1.043f, 5.000f), 0f),                        // Coastal Highway, gas station: the shop counter
        new("PipeCorncob", "FarmHouseA", new Vector3(-7.979f, 0.848f, -3.475f), 90f),                           // Pleasant Valley, farmhouse: the dining table
        new("PipeChurchwarden", "BlackrockPrisonSurvivalZone", new Vector3(-230.063f, 235.129f, 158.305f), 0f), // Blackrock prison: the warden's desk
    };

    static string DoneFile => Path.Combine(Mod.Root, "placed.txt");
    static HashSet<string>? done;

    static HashSet<string> Done()
    {
        if (done != null) return done;
        done = new HashSet<string>();
        try { if (File.Exists(DoneFile)) foreach (var l in File.ReadAllLines(DoneFile)) if (l.Length > 0) done.Add(l.Trim()); } catch { }
        return done;
    }

    static void Mark(string key)
    {
        Done().Add(key);
        try { File.AppendAllText(DoneFile, key + Environment.NewLine); } catch (Exception e) { Safe.Log("Pipe spots file", e); }
    }

    internal static string WorldKey(string save)
    {
        uint id = 0; int seed = 0;
        try { id = SaveGameSlots.GetSaveSlotFromName(save)?.m_GameId ?? 0; } catch { }
        try { seed = GameManager.m_SceneTransitionData?.m_GameRandomSeed ?? 0; } catch { }
        return save + "_" + id + "_" + seed;
    }

    public static void OnScene(string scene)
    {
        foreach (var s in All)
            if (string.Equals(s.Scene, scene, StringComparison.OrdinalIgnoreCase))
            {
                // 11-10: in a coop shared world a pipe is put only on the very first visit of the scene in that world (the scene is not in
                // the save yet). Whoever comes first puts it, the coop mod hands that layout to the others; later visitors get the scene
                // from the save or the layout, with the pipe in it or already taken. Checked now, before the game saves this scene.
                bool? firstHere = null;
                if (CoopSharedWorld())
                {
                    try
                    {
                        string save = SaveGameSystem.GetCurrentSaveName() ?? "";
                        string key = GameManager.m_SceneTransitionData?.m_SceneSaveFilenameCurrent ?? "";
                        if (string.IsNullOrEmpty(key)) key = scene;
                        firstHere = !string.IsNullOrEmpty(save) && !SaveGameSlots.HasFilenameInSlot(save, key);
                        Safe.Info($"Pipe spot: coop shared world, {key} {(firstHere == true ? "visited for the first time" : "already in the save")}");
                    }
                    catch (Exception e) { Safe.Log("Pipe spots first visit", e); firstHere = false; }
                }
                MelonLoader.MelonCoroutines.Start(Place(scene, firstHere));
                return;
            }
    }

    static bool LyingThere(Spot s)
    {
        string name = Defs.Prefix + s.Pipe;
        foreach (var gi in UnityEngine.Object.FindObjectsOfType<GearItem>())
        {
            if (gi == null || gi.m_InPlayerInventory || !gi.gameObject.activeInHierarchy) continue;
            if (!gi.name.StartsWith(name, StringComparison.Ordinal)) continue;
            if ((gi.transform.position - s.Pos).sqrMagnitude < 2f * 2f) return true;
        }
        return false;
    }

    static IEnumerator Place(string scene, bool? sharedFirst)
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
        while (Time.realtimeSinceStartup - t1 < .4f) yield return null;   // 11-09: 4 s showed the pipe appearing after entering
        string save = "";
        try { save = SaveGameSystem.GetCurrentSaveName() ?? ""; } catch { }
        if (string.IsNullOrEmpty(save)) yield break;
        // 11-10: a coop friend put its own pipe into a scene the host's world already had (only it had one, the coop check showed a mismatch)
        if (sharedFirst == false) { Safe.Info("Pipe spot: scene already in the shared world, its pipe comes from there"); yield break; }
        foreach (var s in All)
        {
            if (!string.Equals(s.Scene, scene, StringComparison.OrdinalIgnoreCase)) continue;
            string key = $"{WorldKey(save)}|{s.Scene}|{s.Pipe}";
            if (Done().Contains(key)) continue;
            if (LyingThere(s)) { Mark(key); continue; }
            try
            {
                var gi = Items.Spawn(s.Pipe);
                if (gi == null) { Safe.Info("Pipe spot: " + s.Pipe + " not created"); continue; }
                gi.m_RolledSpawnChance = true;
                var root = GearManager.m_GearCategory;
                if (root != null) gi.transform.SetParent(root.transform, true);
                gi.transform.rotation = Quaternion.Euler(0f, s.Yaw, 0f);
                gi.transform.position = Ground(gi, s.Pos) + Vector3.up * .003f;
                Mark(key);
                Safe.Info($"Pipe spot: {s.Pipe} put in {s.Scene} at {gi.transform.position}");
            }
            catch (Exception e) { Safe.Log("Pipe spot", e); }
        }
    }

    /// <summary>CoopByEMPEROR (by reflection): this player is in a coop shared world (the host in its own, a friend in the host's).</summary>
    static bool CoopSharedWorld()
    {
        try
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.GetName().Name != "CoopByEMPEROR") continue;
                var t = a.GetType("CoopByEMPEROR.Net.Session");
                if (t == null) return false;
                const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                var mode = t.GetProperty("Mode", F)?.GetValue(null)?.ToString();
                var inHost = t.GetProperty("InHostWorld", F)?.GetValue(null) as bool?;
                return mode == "Host" || (mode == "Client" && inHost == true);
            }
        }
        catch (Exception e) { Safe.Log("Pipe spots coop check", e); }
        return false;
    }

    // the top of whatever is under the spot (the desk), nearest to the spot's height; our own item is skipped
    static Vector3 Ground(GearItem gi, Vector3 p)
    {
        var at = p;
        float best = float.MaxValue;
        foreach (var h in Physics.RaycastAll(p + Vector3.up * .3f, Vector3.down, .6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider == null || h.collider.transform.IsChildOf(gi.transform) || h.collider.GetComponentInParent<GearItem>() != null) continue;
            float dy = Mathf.Abs(h.point.y - p.y);
            if (dy < best) { best = dy; at = h.point; }
        }
        return at;
    }
}
