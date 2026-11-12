using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JellyfishByEMPEROR;

/// <summary>A jellyfish poured into an aquarium: its species, colour and condition in % (0: dead, lying on the sand).</summary>
public class Fish
{
    public string Sp = "", Col = "";
    public float Hp = 100f;
    [JsonIgnore] public Species? Species => Defs.SpeciesById(Sp);
    [JsonIgnore] public Colour? Colour => Species is { } s ? Defs.ColourOf(s, Col) : null;
    [JsonIgnore] public bool Dead => Hp <= .01f;
}

/// <summary>One aquarium standing in the world. It cannot be moved while jellyfish are in it, so its place is its name.</summary>
public class TankState
{
    public string Scene = "";
    public float X, Y, Z;
    public List<Fish> Fish = new();
    [JsonIgnore] public Vector3 Pos => new(X, Y, Z);
}

public class TankWorld
{
    public List<TankState> Tanks = new();
}

/// <summary>The owner (11-12): a jellyfish is poured from its jar into the aquarium (the jar is left empty), three at most, and caught back out
/// with an empty jar. What is in each aquarium is kept in a file per world (as the smokehouse's barrels) and shared in coop: the host decides.</summary>
public static class Tanks
{
    internal static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    public static TankWorld State = new();
    static string? loadedKey;
    static bool remoteSinceSave;
    const float Near = .6f;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string SceneName => GameManager.m_ActiveScene ?? "";

    public static TankState? Find(string scene, Vector3 pos)
    {
        TankState? best = null;
        float d = Near;
        foreach (var t in State.Tanks)
        {
            if (t.Scene != scene) continue;
            float x = Vector3.Distance(t.Pos, pos);
            if (x < d) { d = x; best = t; }
        }
        return best;
    }

    public static TankState? Of(GearItem g) => g != null && !g.m_InPlayerInventory ? Find(SceneName, g.transform.position) : null;

    public static int CountOf(GearItem g) => Of(g)?.Fish.Count ?? 0;

    static TankState Ensure(string scene, Vector3 p)
    {
        var t = Find(scene, p);
        if (t != null) return t;
        t = new TankState { Scene = scene, X = p.x, Y = p.y, Z = p.z };
        State.Tanks.Add(t);
        return t;
    }

    /// <summary>After any change made here: told to the other players. An empty aquarium has no state (it may be moved again).</summary>
    public static void Changed(TankState t)
    {
        if (t.Fish.Count == 0) State.Tanks.Remove(t);
        try { CoopApi.Send?.Invoke("tank|" + JsonSerializer.Serialize(t, Json)); }
        catch (Exception e) { Safe.Log("Coop send", e); }
    }

    /// <summary>An aquarium as another player has it now: takes the place of ours.</summary>
    public static void Apply(string json)
    {
        var t = JsonSerializer.Deserialize<TankState>(json, Json);
        if (t == null) return;
        t.Fish ??= new();
        var old = Find(t.Scene, t.Pos);
        if (old != null)
        {
            old.Fish = t.Fish;
            if (old.Fish.Count == 0) State.Tanks.Remove(old);
        }
        else if (t.Fish.Count > 0) State.Tanks.Add(t);
        remoteSinceSave = true;
    }

    public static string Snapshot() => JsonSerializer.Serialize(State, Json);

    public static void ApplySnapshot(string json)
    {
        var s = JsonSerializer.Deserialize<TankWorld>(json, Json);
        if (s == null) return;
        foreach (var t in s.Tanks) t.Fish ??= new();
        State = s;
        remoteSinceSave = true;
    }

    // ---- the host decides what a friend asks for (the same key asked twice gets the same answer, done once)

    static readonly Dictionary<string, string> answered = new();

    static string F(float v) => v.ToString("R", Inv);
    static float P(string s) => float.Parse(s, Inv);

    public static string Op(string kind, TankState t, params string[] rest) =>
        string.Join("|", new[] { kind, Remote.NewKey(), t.Scene, F(t.X), F(t.Y), F(t.Z) }.Concat(rest));

    public static string OpAt(string kind, string scene, Vector3 p, params string[] rest) =>
        string.Join("|", new[] { kind, Remote.NewKey(), scene, F(p.x), F(p.y), F(p.z) }.Concat(rest));

    public static string HostRequest(string op)
    {
        var a = op.Split('|');
        if (a.Length < 2) return "";
        string key = a[1];
        if (answered.TryGetValue(key, out var done)) return done;
        string r = a[0] == "claim" ? Catch.HostClaim(a.Length > 2 ? a[2] : "") : TankOp(a);
        if (answered.Count > 500) answered.Clear();
        answered[key] = r;
        return r;
    }

    static string TankOp(string[] a)
    {
        if (a.Length < 8) return "";
        string scene = a[2];
        var pos = new Vector3(P(a[3]), P(a[4]), P(a[5]));
        switch (a[0])
        {
            case "pour":
            {
                if (a.Length < 9) return "";
                var t = Ensure(scene, pos);
                if (t.Fish.Count >= Tune.TankMax) { if (t.Fish.Count == 0) State.Tanks.Remove(t); return ""; }
                t.Fish.Add(new Fish { Sp = a[6], Col = a[7], Hp = Mathf.Clamp(P(a[8]), 0f, 100f) });
                Changed(t);
                return "ok";
            }
            case "take":
            case "remove":
            {
                if (a.Length < 9) return "";
                var t = Find(scene, pos);
                if (t == null || !int.TryParse(a[6], out int i) || i < 0 || i >= t.Fish.Count) return "";
                var f = t.Fish[i];
                if (f.Sp != a[7] || f.Col != a[8]) return "";
                if (a[0] == "take" && f.Dead) return "";
                t.Fish.RemoveAt(i);
                Changed(t);
                return "ok|" + F(f.Hp);
            }
        }
        return "";
    }

    // ---- per-world file (the slot's name and the world's seed: a new game in the same slot is another world)

    static string Folder => Path.Combine(Mod.Root, "saves");

    public static string WorldKey()
    {
        string slot = SaveGameSystem.GetCurrentSaveName() ?? "";
        if (string.IsNullOrEmpty(slot)) return "";
        return slot + "|" + GameManager.m_SceneTransitionData.m_GameRandomSeed.ToString(Inv);
    }

    static string FileOf(string key)
    {
        var safe = new string(key.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_').ToArray());
        return Path.Combine(Folder, safe + ".json");
    }

    public static void Load()
    {
        TankWindow.Close();
        try
        {
            // coop, same world, a change came in during the loading screen: the file was written before it, memory is newer
            string now = WorldKey();
            if (CoopApi.Send != null && remoteSinceSave && now.Length > 0 && now == loadedKey)
            {
                Safe.Info($"Aquariums for {loadedKey}: kept from memory (coop change after the last save), {State.Tanks.Count}");
                return;
            }
        }
        catch (Exception e) { Safe.Log("Aquarium load check", e); }
        State = new TankWorld();
        remoteSinceSave = false;
        try
        {
            loadedKey = WorldKey();
            var path = FileOf(loadedKey);
            if (loadedKey.Length > 0 && File.Exists(path)) State = JsonSerializer.Deserialize<TankWorld>(File.ReadAllText(path), Json) ?? new TankWorld();
            foreach (var t in State.Tanks) t.Fish ??= new();
            Safe.Info($"Aquariums for {loadedKey}: {State.Tanks.Count} with jellyfish");
        }
        catch (Exception e) { MelonLogger.Error("Aquarium state unreadable, starting clean: " + e.Message); }
    }

    public static void Save()
    {
        try
        {
            string key = WorldKey();
            if (string.IsNullOrEmpty(key)) return;
            loadedKey = key;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FileOf(key), JsonSerializer.Serialize(State, Json));
            remoteSinceSave = false;
        }
        catch (Exception e) { Safe.Log("Saving aquariums", e); }
    }

    public static void Reset()
    {
        State = new TankWorld();
        loadedKey = null;
        remoteSinceSave = false;
        TankWindow.Close();
    }
}

[HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.SaveGlobalData))]
internal static class SavePatch
{
    static void Postfix() => Tanks.Save();
}

[HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.RestoreGlobalData))]
internal static class LoadPatch
{
    static void Postfix() => Tanks.Load();
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.LaunchSandbox))]
internal static class NewGamePatch
{
    static void Prefix() => Tanks.Reset();
}

/// <summary>Clicking a standing aquarium (or picking it up) opens its window; it is taken or moved only empty.</summary>
[HarmonyPatch]
internal static class TankUse
{
    static bool IsTank(GearItem? g) => g != null && Defs.Is(g, out var d) && d.Kind == Kind.Aquarium && !g.m_InPlayerInventory;

    [HarmonyPrefix, HarmonyPatch(typeof(GearItem), nameof(GearItem.PerformInteraction))]
    static bool Click(GearItem __instance, ref bool __result)
    {
        try
        {
            if (!IsTank(__instance) || TankWindow.IsTaking(__instance)) return true;
            TankWindow.Open(__instance);
            __result = true;
            return false;
        }
        catch (Exception e) { Safe.Log("Aquarium click", e); return true; }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(GearItem), nameof(GearItem.PerformAlternativeInteraction))]
    static bool Move(GearItem __instance, ref bool __result)
    {
        try
        {
            if (!IsTank(__instance) || Tanks.CountOf(__instance) == 0) return true;
            Catch.Say("MSG_BUSY");
            __result = false;
            return false;
        }
        catch (Exception e) { Safe.Log("Aquarium move", e); return true; }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.ProcessPickupItemInteraction))]
    static bool PickUp(GearItem item, ref bool __result)
    {
        try
        {
            if (!IsTank(item) || TankWindow.IsTaking(item)) return true;
            TankWindow.Open(item);
            __result = true;
            return false;
        }
        catch (Exception e) { Safe.Log("Aquarium pick up", e); return true; }
    }
}
