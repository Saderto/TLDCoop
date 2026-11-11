using System.Text.Json;
using System.Text.Json.Serialization;
using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;

namespace SmokesBySeagull;

/// <summary>What smoking leaves behind, per world: when and how much was smoked, the dulled hunger's end, the addiction.</summary>
public class HabitState
{
    public List<float[]> History { get; set; } = new();   // [game hour, cigarettes' worth]
    public float LastSmoke { get; set; } = -1000f;
    public bool Addicted { get; set; }
    public float HungerUntil { get; set; }
    public Dictionary<string, float> Bonus { get; set; } = new();   // flavour bonus -> game hour it ends
    public List<string> Lighters { get; set; } = new();             // 11-10: collectible lighters found in this world, in order found
}

/// <summary>Effects of a smoke, shortness of breath, addiction and craving (the owner's numbers in Settings).</summary>
public static class Habit
{
    public static HabitState State = new();
    static string? loadedKey;
    static float nextCheck;

    public static float Now
    {
        get
        {
            try { return GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused(); }
            catch { return 0f; }
        }
    }

    public static bool HungerDulled => State.HungerUntil > Now;
    public static bool Breathless { get; private set; }
    public static bool FreshOn { get; private set; }
    public static bool WarmOn { get; private set; }
    public static bool CalmOn { get; private set; }
    public static bool HandsOn { get; private set; }
    public static bool SteadyOn { get; private set; }
    public static bool DryOn { get; private set; }
    public static bool DizzyOn { get; private set; }

    static float BonusLeft(string kind, float now) => State.Bonus != null && State.Bonus.TryGetValue(kind, out var until) ? until - now : 0f;
    public static bool Craving { get; private set; }

    /// <summary>A smoke ended: share is how much of it was smoked (0..1), already halved for a torn cigar.</summary>
    public static void Smoked(SmokeStats s, float share, string cause)
    {
        if (share <= .01f) return;
        float now = Now;
        try
        {
            var f = GameManager.GetFatigueComponent();
            f.m_CurrentFatigue = Math.Max(0f, f.m_CurrentFatigue - f.m_MaxFatigue * s.FatiguePct / 100f * share);
        }
        catch (Exception e)
        {
            Safe.Log("Fatigue relief", e);
        }
        try
        {
            var c = GameManager.GetConditionComponent();
            c.AddHealth(-c.m_MaxHP * s.HealthPct / 100f * share, DamageSource.Unspecified);
        }
        catch (Exception e)
        {
            Safe.Log("Health cost", e);
        }
        State.HungerUntil = Math.Max(State.HungerUntil, now + s.HungerMinutes / 60f * share);
        if (!string.IsNullOrEmpty(s.Bonus) && s.BonusHours > 0f)
        {
            State.Bonus ??= new();
            State.Bonus[s.Bonus] = Math.Max(BonusLeft(s.Bonus, 0f), now + s.BonusHours * Math.Max(.5f, share));
        }
        // a downside lives in the same table as the bonuses: kind -> game hour it ends
        if (!string.IsNullOrEmpty(s.Side) && s.SideHours > 0f)
        {
            State.Bonus ??= new();
            State.Bonus[s.Side] = Math.Max(BonusLeft(s.Side, 0f), now + s.SideHours * Math.Max(.5f, share));
        }
        State.History.Add(new[] { now, s.Counts * share });
        State.LastSmoke = now;
        Safe.Debug($"Smoked {cause}: share {share:0.00}, fatigue -{s.FatiguePct * share:0.0}%, HP -{s.HealthPct * share:0.0}%, hunger dulled until {State.HungerUntil:0.00}");
        nextCheck = 0f;
        Save();
    }

    public static void Tick()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        if (GameManager.m_IsPaused || !InGame()) return;
        float now = Now;
        float window = Settings.AddictionWindowHours.Value;
        State.History.RemoveAll(h => h.Length < 2 || now - h[0] > Math.Max(window, 24f) || h[0] > now + 1f);
        float day = 0f, recent = 0f;
        foreach (var h in State.History)
        {
            if (now - h[0] <= 24f) day += h[1];
            if (now - h[0] <= window) recent += h[1];
        }
        Breathless = day > Settings.BreathPerDay.Value + .001f;
        if (!State.Addicted && recent >= Settings.AddictionCount.Value - .001f)
        {
            State.Addicted = true;
            Save();
        }
        if (State.Addicted && now - State.LastSmoke >= Settings.AddictionEndsHours.Value)
        {
            State.Addicted = false;
            Save();
        }
        Craving = State.Addicted && now - State.LastSmoke >= Settings.CravingAfterHours.Value && !Smoking.Active;
        Sync("Hunger", HungerDulled, State.HungerUntil - now);
        Sync("Breath", Breathless, 0f);
        Sync("Addiction", State.Addicted && !Craving, 0f);
        Sync("Craving", Craving, 0f);
        float fl = BonusLeft("fresh", now), wl = BonusLeft("warm", now), cl = BonusLeft("calm", now);
        FreshOn = fl > 0f; WarmOn = wl > 0f; CalmOn = cl > 0f;
        Sync("Fresh", FreshOn, fl);
        Sync("Warm", WarmOn, wl);
        Sync("Calm", CalmOn, cl);
        float vl = BonusLeft("hands", now), sl = BonusLeft("steady", now), dl = BonusLeft("dry", now), zl = BonusLeft("dizzy", now);
        HandsOn = vl > 0f; SteadyOn = sl > 0f; DryOn = dl > 0f; DizzyOn = zl > 0f;
        Sync("Hands", HandsOn, vl);
        Sync("Steady", SteadyOn, sl);
        Sync("Dry", DryOn, dl);
        Sync("Dizzy", DizzyOn, zl);
    }

    static bool InGame()
    {
        try
        {
            return GameManager.GetPlayerManagerComponent() != null && !GameManager.IsMainMenuActive() && !SaveGameSystem.IsRestoreInProgress() && AfflictionManager.GetAfflictionManagerInstance() != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>The status line of an effect: there while wanted, once; gone when not.</summary>
    static void Sync(string kind, bool want, float hoursLeft)
    {
        try
        {
            var m = AfflictionManager.GetAfflictionManagerInstance();
            if (m == null) return;
            var have = m.m_Afflictions.OfType<SmokeAffliction>().Where(a => a.EffectKind == kind).ToList();
            for (int i = want ? 1 : 0; i < have.Count; i++) m.Remove(have[i]);
            if (!want)
            {
                if (have.Count > 0) Safe.Debug("Effect off: " + kind);
                return;
            }
            if (have.Count == 0)
            {
                new SmokeAffliction(kind, hoursLeft).Start();
                Safe.Debug("Effect on: " + kind);
            }
            else if (hoursLeft > 0f)
            {
                have[0].EndTime = Now + hoursLeft;
            }
        }
        catch (Exception e)
        {
            Safe.Log("Effect line " + kind, e);
        }
    }

    // ---- per-world file

    static string Folder => Path.Combine(Mod.Root, "saves");

    public static string WorldKey()
    {
        string slot = SaveGameSystem.GetCurrentSaveName() ?? "";
        string id = "";
        try
        {
            var info = string.IsNullOrEmpty(slot) ? null : SaveGameSlots.GetSaveSlotFromName(slot);
            if (info != null) id = info.m_GameId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception e)
        {
            Safe.Log("World key", e);
        }
        return slot + "|" + id + "|" + GameManager.m_SceneTransitionData.m_GameRandomSeed.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    static string FileOf(string key)
    {
        var safe = new string(key.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_').ToArray());
        return Path.Combine(Folder, safe + ".json");
    }

    public static void Load()
    {
        State = new HabitState();
        try
        {
            loadedKey = WorldKey();
            var path = FileOf(loadedKey);
            if (File.Exists(path)) State = JsonSerializer.Deserialize<HabitState>(File.ReadAllText(path)) ?? new HabitState();
            Safe.Debug($"Habit loaded for {loadedKey}: {State.History.Count} smokes remembered, addicted {State.Addicted}");
        }
        catch (Exception e)
        {
            MelonLogger.Error("Smoking state unreadable, starting clean: " + e.Message);
        }
        nextCheck = 0f;
    }

    public static void Save()
    {
        try
        {
            string key = WorldKey();
            if (string.IsNullOrEmpty(key) || key.StartsWith("|", StringComparison.Ordinal)) return;
            if (loadedKey != null && key != loadedKey)
            {
                // another world than the one loaded: its own file, not ours
                loadedKey = key;
            }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FileOf(key), JsonSerializer.Serialize(State));
        }
        catch (Exception e)
        {
            Safe.Log("Saving smoking state", e);
        }
    }

    public static void Reset()
    {
        State = new HabitState();
        loadedKey = null;
        Breathless = Craving = FreshOn = WarmOn = CalmOn = HandsOn = SteadyOn = DryOn = DizzyOn = false;
    }
}

/// <summary>One line in the status panel for each of our effects. Public fields and a public empty constructor, so AfflictionComponent's save can write and read it.</summary>
public class SmokeAffliction : CustomAffliction, IBuff, IDuration, IInstance
{
    public string EffectKind = "";

    public bool Buff { get; set; }
    public bool BuffCold { get; set; }
    public bool BuffFatigue { get; set; }
    public bool BuffHunger { get; set; }
    public bool BuffThirst { get; set; }
    public float Duration { get; set; }
    public float EndTime { get; set; }
    public InstanceType Type { get; set; } = InstanceType.Single;

    public SmokeAffliction() : base("", "", "", null, "", AfflictionBodyArea.Chest, true) { }

    public SmokeAffliction(string kind, float hours)
        : base("SMOKES_FX_" + kind + "_NAME", "SMOKES_FX_CAUSE", "SMOKES_FX_" + kind + "_DESC", null, "SmokesBySeagull.Icons.fx_" + kind.ToLowerInvariant() + ".png",
            kind == "Hunger" ? AfflictionBodyArea.Stomach : AfflictionBodyArea.Chest, true)
    {
        EffectKind = kind;
        Buff = kind == "Hunger" || kind == "Fresh" || kind == "Warm" || kind == "Calm" || kind == "Hands" || kind == "Steady";
        BuffHunger = kind == "Hunger";
        Duration = hours;
    }

    public override void OnUpdate() { }

    public void OnFoundExistingInstance(CustomAffliction existing)
    {
        if (existing is SmokeAffliction s && Duration > 0f) s.EndTime = Math.Max(s.EndTime, Habit.Now + Duration);
    }
}

[HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.SaveGlobalData))]
internal static class HabitSavePatch
{
    static void Postfix() => Habit.Save();
}

[HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.RestoreGlobalData))]
internal static class HabitLoadPatch
{
    static void Postfix()
    {
        Habit.Load();
        MelonLoader.MelonCoroutines.Start(Lighters.SyncLater());
    }
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.LaunchSandbox))]
internal static class HabitNewGamePatch
{
    static void Prefix() => Habit.Reset();
}

[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.UpdateCalorieBurnRate))]
internal static class HungerHook
{
    static void Postfix()
    {
        if (!Habit.HungerDulled) return;
        var h = GameManager.GetHungerComponent();
        if (h != null) h.m_CurrentCalorieBurnPerHour *= 1f - Settings.HungerLess.Value;
    }
}

/// <summary>Shortness of breath: of what stamina comes back each frame, a share is taken off (measured around the game's update, so other mods' changes stay).</summary>
[HarmonyPatch(typeof(PlayerMovement), nameof(PlayerMovement.Update))]
internal static class BreathHook
{
    static void Prefix(PlayerMovement __instance, out float __state) => __state = __instance.m_SprintStamina;

    static void Postfix(PlayerMovement __instance, float __state)
    {
        float gained = __instance.m_SprintStamina - __state;
        if (gained < 0f)
        {
            // "Steady breath": of what sprinting takes, 15% is given back
            if (Habit.SteadyOn) __instance.m_SprintStamina -= gained * .15f;
            return;
        }
        if (gained == 0f) return;
        float k = 0f;
        if (Habit.Breathless) k -= Settings.BreathStaminaLess.Value;
        if (Habit.FreshOn) k += .1f;   // menthol: a fresh breath
        if (k != 0f) __instance.m_SprintStamina += gained * k;
    }
}

/// <summary>"Warm smoke" (clove, papirosy, full cigars): the body feels a degree warmer.</summary>
[HarmonyPatch(typeof(Freezing), nameof(Freezing.CalculateBodyTemperature))]
internal static class WarmSmokeHook
{
    static void Postfix(ref float __result)
    {
        if (Habit.WarmOn) __result += 1f;
    }
}

[HarmonyPatch(typeof(Fatigue), nameof(Fatigue.CalculateFatigueIncrease))]
internal static class CravingFatigueHook
{
    static void Postfix(ref float __result)
    {
        if (Habit.Craving) __result *= 1f + Settings.CravingFatigueMore.Value;
    }
}

[HarmonyPatch]
internal static class CravingSwayHooks
{
    [HarmonyPostfix, HarmonyPatch(typeof(GunItem), nameof(GunItem.GetSwayIncreasePerSecond))]
    static void Gun(ref float __result)
    {
        if (Habit.Craving) __result *= 1f + Settings.CravingSwayMore.Value;
        if (Habit.CalmOn) __result *= .75f;
        if (Habit.DizzyOn) __result *= 1.3f;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(BowItem), nameof(BowItem.GetSwayIncreasePerSecond))]
    static void Bow(ref float __result)
    {
        if (Habit.Craving) __result *= 1f + Settings.CravingSwayMore.Value;
        if (Habit.CalmOn) __result *= .75f;
        if (Habit.DizzyOn) __result *= 1.3f;
    }
}

/// <summary>"Dry mouth": of what thirst grows each frame, 15% more (measured around the game's update, so other mods' changes stay).</summary>
[HarmonyPatch(typeof(Thirst), nameof(Thirst.Update))]
internal static class DryMouthHook
{
    static void Prefix(Thirst __instance, out float __state) => __state = __instance.m_CurrentThirst;

    static void Postfix(Thirst __instance, float __state)
    {
        if (!Habit.DryOn) return;
        float grew = __instance.m_CurrentThirst - __state;
        if (grew > 0f) __instance.m_CurrentThirst = Math.Min(__instance.m_CurrentThirst + grew * .15f, __instance.m_MaxThirst);
    }
}

/// <summary>"Steady fingers": the chance to start a fire, 10 points higher (the start panel shows the same number).</summary>
[HarmonyPatch(typeof(FireManager), nameof(FireManager.CalculateFireStartSuccess))]
internal static class SteadyFingersHook
{
    static void Postfix(ref float __result)
    {
        if (Habit.HandsOn && __result > 0f) __result = Math.Min(__result + 10f, 100f);
    }
}
