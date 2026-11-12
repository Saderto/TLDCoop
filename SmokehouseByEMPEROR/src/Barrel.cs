using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmokehouseByEMPEROR;

public class Slot
{
    public string Raw = "";        // the raw piece's gear name
    public string Species = "";
    public float Kg, RawCal, Cal, Cond;
    public float Hours;            // smoked so far
    public bool Salted;
}

/// <summary>A log in the firebox: what it was and how much of it is left. A log the fire has not touched can be taken back.</summary>
public class Log
{
    public string Name = "";
    public float Hours, Full;
    [JsonIgnore] public bool Whole => Hours >= Full - .01f;
}

/// <summary>One barrel standing in the world. It cannot be moved while it holds anything or burns, so its place is its name.</summary>
public class BarrelState
{
    public string Scene = "";
    public float X, Y, Z;
    public bool Cold;
    public bool Lit;
    public float Fuel;             // hours of smoulder left
    public List<Log> Logs = new(); // the wood that makes up Fuel, the first burns first
    public float Last;             // this player's game clock at the last step (never sent as is)
    public Slot?[] Slots = new Slot?[Tune.Slots];

    [JsonIgnore] public float Need => Cold ? Tune.ColdHours : Tune.HotHours;
    public bool Done(Slot s) => s.Hours >= Need - .001f;
    [JsonIgnore] public int Count => Slots.Count(s => s != null);
    [JsonIgnore] public int DoneCount => Slots.Count(s => s != null && Done(s));
    [JsonIgnore] public bool Started => Slots.Any(s => s != null && s.Hours > 0f);
    [JsonIgnore] public bool Idle => !Lit && Count == 0;
    [JsonIgnore] public Vector3 Pos => new(X, Y, Z);
}

public class WorldState
{
    public List<BarrelState> Barrels = new();
}

/// <summary>Barrels of this world: found by place, stepped by the game clock, kept in a file per world.</summary>
public static partial class Barrels
{
    internal static readonly JsonSerializerOptions Json = new() { IncludeFields = true };
    public static WorldState State = new();
    static string? loadedKey;
    static float nextStep;
    static bool sharedClock;
    // after a load: no time passed while the game was not running, whichever clock the file was written by
    static bool resync = true;
    // coop: another player's change arrived after our last Save; the file on disk is older than memory
    static bool remoteSinceSave;
    const float Near = .9f;

    public static float Now
    {
        get
        {
            try
            {
                var shared = CoopApi.Hours;
                if (shared != null) return shared();
            }
            catch (Exception e) { Safe.Log("Shared clock", e); }
            try { return GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused(); }
            catch { return 0f; }
        }
    }

    public static string SceneName => GameManager.m_ActiveScene ?? "";

    public static BarrelState? Find(string scene, Vector3 pos)
    {
        BarrelState? best = null;
        float d = Near;
        foreach (var b in State.Barrels)
        {
            if (b.Scene != scene) continue;
            float x = Vector3.Distance(b.Pos, pos);
            if (x < d) { d = x; best = b; }
        }
        return best;
    }

    public static BarrelState? Of(GearItem g) => g == null ? null : Find(SceneName, g.transform.position);

    public static BarrelState Ensure(GearItem g)
    {
        var b = Of(g);
        if (b != null) return b;
        var p = g.transform.position;
        b = new BarrelState { Scene = SceneName, X = p.x, Y = p.y, Z = p.z, Last = Now };
        State.Barrels.Add(b);
        return b;
    }

    /// <summary>The fire eats its wood and the food smokes for as long as it burned.</summary>
    internal static bool Step(BarrelState b, float now)
    {
        float dt = now - b.Last;
        b.Last = now;
        if (dt <= 0f || !b.Lit) return false;
        float burn = Math.Min(dt, b.Fuel);
        b.Fuel -= burn;
        b.Logs ??= new();
        float eaten = burn;
        while (eaten > 0f && b.Logs.Count > 0)
        {
            var l = b.Logs[0];
            float t = Math.Min(l.Hours, eaten);
            l.Hours -= t;
            eaten -= t;
            if (l.Hours <= .0005f) b.Logs.RemoveAt(0);
        }
        foreach (var s in b.Slots)
            if (s != null) s.Hours = Math.Min(b.Need, s.Hours + burn);
        if (b.Fuel <= .0005f)
        {
            b.Fuel = 0f;
            b.Logs.Clear();
            b.Lit = false;
            return true;
        }
        return false;
    }

    public static void Tick()
    {
        if (Time.unscaledTime < nextStep) return;
        nextStep = Time.unscaledTime + .5f;
        if (GameManager.GetPlayerManagerComponent() == null || string.IsNullOrEmpty(SceneName)) return;
        float now = Now;
        string scene = SceneName;
        bool shared = CoopApi.Hours != null;
        if (shared != sharedClock || resync)
        {
            resync = false;
            // another clock from now on: no barrel may take the difference between the two for time passed
            sharedClock = shared;
            foreach (var b in State.Barrels) b.Last = now;
            Safe.Info("Smokehouse clock: " + (shared ? "the coop world's" : "the player's own"));
        }
        for (int i = State.Barrels.Count - 1; i >= 0; i--)
        {
            var b = State.Barrels[i];
            bool allDone = b.Count > 0 && b.DoneCount == b.Count;
            bool wentOut = Step(b, now);
            if (b.Scene == scene)
            {
                if (wentOut) Say("MSG_OUT");
                else if (!allDone && b.Count > 0 && b.DoneCount == b.Count) Say("MSG_READY");
            }
            if (b.Idle && b.Fuel <= 0f && !Window.IsOpenOn(b)) State.Barrels.RemoveAt(i);
        }
        Fx.Tick(scene);
    }

    public static void Say(string key, params object[] args)
    {
        try { HUDMessage.AddMessage(Texts.Get(key, args), false, true); }
        catch (Exception e) { Safe.Log("HUD message", e); }
    }

    /// <summary>After any change made here: told to the other players.</summary>
    public static void Changed(BarrelState b)
    {
        try { CoopApi.Send?.Invoke(Encode(b)); }
        catch (Exception e) { Safe.Log("Coop send", e); }
    }

    /// <summary>The untouched logs leave the firebox; their names are returned (the caller hands them out).</summary>
    public static List<string> TakeLogs(BarrelState b)
    {
        var names = new List<string>();
        b.Logs ??= new();
        for (int i = b.Logs.Count - 1; i >= 0; i--)
        {
            var l = b.Logs[i];
            if (!l.Whole || string.IsNullOrEmpty(l.Name)) continue;
            names.Add(l.Name);
            b.Fuel = Math.Max(0f, b.Fuel - l.Hours);
            b.Logs.RemoveAt(i);
        }
        if (b.Logs.Count == 0 && b.Fuel < .01f) b.Fuel = 0f;
        return names;
    }

    public static string Encode(BarrelState b) => JsonSerializer.Serialize(b, Json);

    /// <summary>A barrel as another player has it now: takes the place of ours.</summary>
    public static void Apply(string json)
    {
        var b = JsonSerializer.Deserialize<BarrelState>(json, Json);
        if (b == null) return;
        if (b.Slots == null || b.Slots.Length != Tune.Slots) Array.Resize(ref b.Slots, Tune.Slots);
        b.Last = Now;
        var old = Find(b.Scene, b.Pos);
        if (old != null)
        {
            // in place: an open window keeps looking at the same object
            old.Cold = b.Cold; old.Lit = b.Lit; old.Fuel = b.Fuel; old.Logs = b.Logs ?? new(); old.Last = b.Last; old.Slots = b.Slots;
        }
        else State.Barrels.Add(b);
        remoteSinceSave = true;
    }

    public static string Snapshot()
    {
        float now = Now;
        foreach (var b in State.Barrels) Step(b, now);
        return JsonSerializer.Serialize(State, Json);
    }

    public static void ApplySnapshot(string json)
    {
        var s = JsonSerializer.Deserialize<WorldState>(json, Json);
        if (s == null) return;
        float now = Now;
        foreach (var b in s.Barrels)
        {
            b.Last = now;
            if (b.Slots == null || b.Slots.Length != Tune.Slots) Array.Resize(ref b.Slots, Tune.Slots);
        }
        State = s;
        remoteSinceSave = true;
        Window.Close();
    }

    // ---- per-world file (the slot's name and the world's seed: a new game in the same slot is another world)

    static string Folder => Path.Combine(Mod.Root, "saves");

    public static string WorldKey()
    {
        string slot = SaveGameSystem.GetCurrentSaveName() ?? "";
        if (string.IsNullOrEmpty(slot)) return "";
        return slot + "|" + GameManager.m_SceneTransitionData.m_GameRandomSeed.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    static string FileOf(string key)
    {
        var safe = new string(key.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_').ToArray());
        return Path.Combine(Folder, safe + ".json");
    }

    public static void Load()
    {
        Window.Close();
        try
        {
            // coop, same world, a change came in during the loading screen: the file was written before it, memory is newer
            string now = WorldKey();
            if (CoopApi.Send != null && remoteSinceSave && now.Length > 0 && now == loadedKey)
            {
                resync = true;
                Safe.Info($"Smokehouse state for {loadedKey}: kept from memory (coop change after the last save), {State.Barrels.Count} barrels in use");
                return;
            }
        }
        catch (Exception e) { Safe.Log("Smokehouse load check", e); }
        State = new WorldState();
        remoteSinceSave = false;
        try
        {
            loadedKey = WorldKey();
            var path = FileOf(loadedKey);
            if (loadedKey.Length > 0 && File.Exists(path)) State = JsonSerializer.Deserialize<WorldState>(File.ReadAllText(path), Json) ?? new WorldState();
            foreach (var b in State.Barrels)
                if (b.Slots == null || b.Slots.Length != Tune.Slots) Array.Resize(ref b.Slots, Tune.Slots);
            resync = true;
            Safe.Info($"Smokehouse state for {loadedKey}: {State.Barrels.Count} barrels in use");
        }
        catch (Exception e)
        {
            MelonLogger.Error("Smokehouse state unreadable, starting clean: " + e.Message);
        }
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
        catch (Exception e)
        {
            Safe.Log("Saving smokehouse state", e);
        }
    }

    public static void Reset()
    {
        State = new WorldState();
        loadedKey = null;
        remoteSinceSave = false;
        Window.Close();
    }
}

[HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.SaveGlobalData))]
internal static class SavePatch
{
    static void Postfix() => Barrels.Save();
}

[HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.RestoreGlobalData))]
internal static class LoadPatch
{
    static void Postfix() => Barrels.Load();
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.LaunchSandbox))]
internal static class NewGamePatch
{
    static void Prefix() => Barrels.Reset();
}

/// <summary>Looking at the barrel and clicking opens its window; it is taken or moved only empty and cold.</summary>
[HarmonyPatch]
internal static class BarrelUse
{
    [HarmonyPrefix, HarmonyPatch(typeof(GearItem), nameof(GearItem.PerformInteraction))]
    static bool Click(GearItem __instance, ref bool __result)
    {
        try
        {
            if (!Defs.IsBarrel(__instance) || __instance.m_InPlayerInventory || Window.IsTaking(__instance)) return true;
            Window.Open(__instance);
            __result = true;
            return false;
        }
        catch (Exception e)
        {
            Safe.Log("Barrel click", e);
            return true;
        }
    }

    static bool Busy(GearItem g)
    {
        if (!Defs.IsBarrel(g) || g.m_InPlayerInventory) return false;
        var b = Barrels.Of(g);
        return b != null && !b.Idle;
    }

    [HarmonyPrefix, HarmonyPatch(typeof(GearItem), nameof(GearItem.PerformAlternativeInteraction))]
    static bool Move(GearItem __instance, ref bool __result)
    {
        try
        {
            if (!Busy(__instance)) return true;
            Barrels.Say("MSG_BUSY");
            __result = false;
            return false;
        }
        catch (Exception e)
        {
            Safe.Log("Barrel move", e);
            return true;
        }
    }

    /// <summary>Whatever way the game goes to pick a standing barrel up, the window opens instead; taking it is the window's button.</summary>
    [HarmonyPrefix, HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.ProcessPickupItemInteraction))]
    static bool PickUp(GearItem item, ref bool __result)
    {
        try
        {
            if (item == null || !Defs.IsBarrel(item) || item.m_InPlayerInventory || Window.IsTaking(item)) return true;
            Window.Open(item);
            __result = true;
            return false;
        }
        catch (Exception e)
        {
            Safe.Log("Barrel pick up", e);
            return true;
        }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(GearItem), nameof(GearItem.GetHoverText))]
    static void Hover(GearItem __instance, ref string __result)
    {
        try
        {
            if (!Defs.IsBarrel(__instance) || __instance.m_InPlayerInventory) return;
            var b = Barrels.Of(__instance);
            string s = Texts.Get("Barrel_NAME");
            if (b != null && !b.Idle)
            {
                s += "\n" + Texts.Get(b.Lit ? "LIT" : "UNLIT");
                if (b.Count > 0) s += ", " + Texts.Get("HOVER_ITEMS", b.DoneCount, b.Count);
            }
            __result = s;
        }
        catch (Exception e)
        {
            Safe.Log("Barrel hover", e);
        }
    }
}
