using System.Text.Json;
using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;

namespace SnacksByEMPEROR;

/// <summary>One effect a snack leaves: Hours for a whole pack (a part of the pack gives that part of the time).</summary>
public record Fx(string Id, bool Good, float Hours, string Ru, string En, string RuD, string EnD, string RuS, string EnS);

/// <summary>The snacks' own effects, good and bad. Each lasts game hours; eating more adds time, up to twice a whole pack's.
/// The state is a file per world; the status panel lines are AfflictionComponent's.</summary>
public static class Effects
{
    public static readonly Fx[] All =
    {
        new("SaltHold", true, 3f, "Соль держит воду", "Salt Holds Water", "Соль не отпускает воду из тела: всё, что вы пьёте, утоляет жажду на четверть лучше.", "Salt keeps the water in: whatever you drink quenches a quarter more thirst.", "питьё утоляет жажду на четверть лучше", "drinks quench a quarter more thirst"),
        new("Sinew", true, 2f, "Крепкая спина", "Strong Back", "Белок пошёл в спину и ноги: с перегруженным рюкзаком идёте заметно быстрее.", "The protein went into your back and legs: you walk much faster with an overloaded pack.", "перегруз замедляет почти вдвое меньше", "an overloaded pack slows you almost half as much"),
        new("SugarDry", true, 1.5f, "Сахарная печка", "Sugar Stove", "Сахар сгорает быстро и жарко: сырая одежда сохнет прямо на вас.", "Sugar burns fast and hot: damp clothes dry right on you.", "сырая одежда сохнет прямо на вас", "damp clothes dry right on you"),
        new("PepperBlood", true, 2f, "Перец в крови", "Pepper in the Blood", "Перец гонит кровь: когда вы совсем замёрзли, холод отнимает здоровье на 40% медленнее.", "Pepper drives the blood: when you are frozen through, the cold takes your condition 40% slower.", "замерзание отнимает здоровье на 40% медленнее", "freezing drains condition 40% slower"),
        new("SeedHabit", true, 3f, "Вместо сигареты", "Instead of a Smoke", "Руки и рот заняты семечками: тяга к курению молчит, пока щёлкаете.", "Hands and mouth are busy with seeds: the craving for a smoke keeps quiet while you crack them.", "тяга к курению отступает", "the craving for a smoke backs off"),
        new("Sweat", false, 1f, "Пот градом", "Dripping Sweat", "Острое прошибло в пот: надетая одежда понемногу сыреет изнутри.", "The heat broke you into a sweat: the clothes you wear slowly get damp from inside.", "одежда на вас понемногу сыреет", "the clothes you wear slowly get damp"),
        new("DryLump", false, 1f, "Сухой ком", "Dry Lump", "Съедено всухомятку: следующая еда даст на 15% меньше калорий. Попейте, и ком пройдёт.", "Eaten dry: the next food gives 15% fewer calories. Have a drink and the lump is gone.", "следующая еда даст на 15% меньше калорий, пока не попьёте", "the next food gives 15% fewer calories until you drink"),
    };

    static readonly Dictionary<string, Fx> byId = All.ToDictionary(f => f.Id);
    public static Fx? Get(string id) => byId.TryGetValue(id, out var f) ? f : null;

    /// <summary>Effect id -> the game hour it ends at.</summary>
    static Dictionary<string, float> until = new();
    static readonly HashSet<string> on = new();
    static string? loadedKey;
    static float nextCheck;

    public static bool On(string id) => on.Contains(id);

    /// <summary>Ends an effect now (the dry lump goes with a drink or with the next food).</summary>
    public static void End(string id)
    {
        if (!until.Remove(id)) return;
        on.Remove(id); nextCheck = 0f;   // written with the game's save only (FxSavePatch)
    }

    public static bool SmokesHere { get; private set; }

    /// <summary>Before the texts are built: "Instead of a smoke" needs SmokesByEMPEROR; without it seeds are simply salty.</summary>
    public static void Prepare()
    {
        try { SmokesHere = SmokesLink.Find(); } catch (Exception e) { Safe.Log("Smokes link", e); }
        if (SmokesHere) return;
        foreach (var d in Defs.All)
            for (int i = 0; i < d.Fx.Length; i++)
                if (d.Fx[i] == "SeedHabit") d.Fx[i] = "SaltHold";
    }

    public static float Now
    {
        get
        {
            try { return GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused(); }
            catch { return 0f; }
        }
    }

    public static void Eaten(Def def, float share)
    {
        if (def.Fx.Length == 0 || share <= 0f) return;
        float now = Now;
        foreach (var id in def.Fx)
        {
            if (Get(id) is not { } f) continue;
            float left = until.TryGetValue(id, out var u) && u > now ? u - now : 0f;
            until[id] = now + Math.Min(left + f.Hours * Math.Min(share, 1f), f.Hours * 2f);
            Safe.Debug($"Effect {id}: {until[id] - now:0.00} h after {def.Id} x{share:0.00}");
            // what it does, said at once: the status panel shows the name, the reading is here
            try { HUDMessage.AddMessage((Texts.Russian ? f.Ru + ": " + f.RuS : f.En + ": " + f.EnS) + $" ({until[id] - now:0.#} " + (Texts.Russian ? "ч)" : "h)"), 6f, false, false); }
            catch (Exception e) { Safe.Log("Effect message", e); }
        }
        if (def.Fx.Contains("DryLump")) Body.LumpFrom(def.Id);
        nextCheck = 0f;
        // 11-20 (Ivan): not written to the file here, only with the game's own save (FxSavePatch): eaten after the last save,
        // quit without saving and that save loaded again, the effects were still on.
    }

    public static void Tick()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        if (GameManager.m_IsPaused || !InGame()) return;
        float now = Now;
        foreach (var f in All)
        {
            float left = until.TryGetValue(f.Id, out var u) ? u - now : 0f;
            // a clock that went back (another save loaded without our file) ends the effect
            if (left > f.Hours * 2f + .1f) { left = 0f; until.Remove(f.Id); }
            bool want = left > 0f;
            if (want) on.Add(f.Id); else on.Remove(f.Id);
            Sync(f, want, left);
        }
        try { Body.Tick(now); } catch (Exception e) { Safe.Log("Body effects", e); }
    }

    static bool InGame()
    {
        try
        {
            return GameManager.GetPlayerManagerComponent() != null && !GameManager.IsMainMenuActive() && !SaveGameSystem.IsRestoreInProgress() && AfflictionManager.GetAfflictionManagerInstance() != null;
        }
        catch { return false; }
    }

    static void Sync(Fx f, bool want, float hoursLeft)
    {
        try
        {
            var m = AfflictionManager.GetAfflictionManagerInstance();
            if (m == null) return;
            var have = m.m_Afflictions.OfType<SnackAffliction>().Where(a => a.EffectKind == f.Id).ToList();
            for (int i = want ? 1 : 0; i < have.Count; i++) m.Remove(have[i]);
            if (!want) return;
            if (have.Count == 0) new SnackAffliction(f, hoursLeft).Start();
            else have[0].EndTime = Now + hoursLeft;
        }
        catch (Exception e) { Safe.Log("Effect line " + f.Id, e); }
    }

    // ---- a file per world

    static string Folder => Path.Combine(Mod.Root, "saves");

    static string WorldKey()
    {
        string slot = SaveGameSystem.GetCurrentSaveName() ?? "";
        string id = "";
        try
        {
            var info = string.IsNullOrEmpty(slot) ? null : SaveGameSlots.GetSaveSlotFromName(slot);
            if (info != null) id = info.m_GameId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception e) { Safe.Log("World key", e); }
        return slot + "|" + id + "|" + GameManager.m_SceneTransitionData.m_GameRandomSeed.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    static string FileOf(string key) => Path.Combine(Folder, new string(key.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_').ToArray()) + ".json");

    public static void Load()
    {
        until = new(); on.Clear(); Body.Forget();
        try
        {
            loadedKey = WorldKey();
            var path = FileOf(loadedKey);
            if (File.Exists(path)) until = JsonSerializer.Deserialize<Dictionary<string, float>>(File.ReadAllText(path)) ?? new();
            Safe.Debug($"Effects loaded for {loadedKey}: {until.Count}");
        }
        catch (Exception e) { MelonLogger.Error("Snack effects unreadable, starting clean: " + e.Message); }
        nextCheck = 0f;
    }

    public static void Save()
    {
        try
        {
            string key = WorldKey();
            if (string.IsNullOrEmpty(key) || key.StartsWith("|", StringComparison.Ordinal)) return;
            // nothing was ever eaten in this world: no file
            if (until.Count == 0 && !File.Exists(FileOf(key))) return;
            loadedKey = key;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FileOf(key), JsonSerializer.Serialize(until));
        }
        catch (Exception e) { Safe.Log("Saving snack effects", e); }
    }

    public static void Reset()
    {
        until = new(); on.Clear(); loadedKey = null; Body.Forget();
    }
}

/// <summary>A line in the status panel. Public fields and a public empty constructor, so AfflictionComponent's save can write and read it.</summary>
public class SnackAffliction : CustomAffliction, IBuff, IDuration, IInstance
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

    public SnackAffliction() : base("", "", "", null, "", AfflictionBodyArea.Stomach, true) { }

    public SnackAffliction(Fx f, float hours)
        : base("SNACKS_FX_" + f.Id + "_NAME", "SNACKS_FX_CAUSE", "SNACKS_FX_" + f.Id + "_DESC", f.Good ? null : f.Id == "DryLump" ? "SNACKS_FX_DRINK" : "SNACKS_FX_SELF", "SnacksByEMPEROR.Icons.fx_" + f.Id.ToLowerInvariant() + ".png",
            f.Id is "Sinew" ? AfflictionBodyArea.LegRight : f.Id is "Sweat" or "PepperBlood" ? AfflictionBodyArea.Chest : f.Id is "DryLump" ? AfflictionBodyArea.Neck : AfflictionBodyArea.Stomach, true)
    {
        EffectKind = f.Id;
        Buff = f.Good;
        BuffCold = f.Id == "PepperBlood"; BuffThirst = f.Id == "SaltHold";
        Duration = hours;
    }

    public override void OnUpdate() { }

    public void OnFoundExistingInstance(CustomAffliction existing)
    {
        if (existing is SnackAffliction s && Duration > 0f) s.EndTime = Math.Max(s.EndTime, Effects.Now + Duration);
    }
}

[HarmonyPatch(typeof(GearItem), nameof(GearItem.ApplyBuffs))]
internal static class EatHook
{
    static void Postfix(GearItem __instance, float normalizedValue)
    {
        try
        {
            if (normalizedValue > 0f && __instance.m_FoodItem != null && Defs.Is(__instance, out var def)) Effects.Eaten(def, normalizedValue);
        }
        catch (Exception e) { Safe.Log("Eating", e); }
    }
}

[HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.SaveGlobalData))]
internal static class FxSavePatch { static void Postfix() => Effects.Save(); }

[HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.RestoreGlobalData))]
internal static class FxLoadPatch { static void Postfix() => Effects.Load(); }

[HarmonyPatch(typeof(GameManager), nameof(GameManager.LaunchSandbox))]
internal static class FxNewGamePatch { static void Prefix() => Effects.Reset(); }

/// <summary>Each hook says once in the detailed log that the game called it with our effect on.</summary>
internal static class Seen
{
    static readonly HashSet<string> said = new();
    public static void Once(string what, float before, float after)
    {
        if (Mod.DebugLog && said.Add(what)) MelonLogger.Msg($"Effect hook {what}: {before:0.###} -> {after:0.###}");
    }
}

/// <summary>"Pepper in the blood": the game takes condition for freezing through Condition.AddHealth with the cause named; the loss is cut there.</summary>
[HarmonyPatch]
internal static class ConditionHooks
{
    static int depth;

    static void Cut(ref float hp, DamageSource cause)
    {
        if (hp >= 0f || cause != DamageSource.Freezing || !Effects.On("PepperBlood")) return;
        float was = hp; hp *= .6f; Seen.Once("condition loss " + cause, was, hp);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(Condition), nameof(Condition.AddHealth), typeof(float), typeof(DamageSource))]
    static void A(ref float hp, DamageSource cause) { if (depth++ == 0) Cut(ref hp, cause); }

    [HarmonyFinalizer, HarmonyPatch(typeof(Condition), nameof(Condition.AddHealth), typeof(float), typeof(DamageSource))]
    static Exception? AEnd(Exception? __exception) { depth = Math.Max(0, depth - 1); return __exception; }

    [HarmonyPrefix, HarmonyPatch(typeof(Condition), nameof(Condition.AddHealth), typeof(float), typeof(DamageSource), typeof(bool))]
    static void B(ref float hp, DamageSource cause) { if (depth++ == 0) Cut(ref hp, cause); }

    [HarmonyFinalizer, HarmonyPatch(typeof(Condition), nameof(Condition.AddHealth), typeof(float), typeof(DamageSource), typeof(bool))]
    static Exception? BEnd(Exception? __exception) { depth = Math.Max(0, depth - 1); return __exception; }
}

/// <summary>"Strong back": the slowdown of an overloaded pack is a multiplier below 1; 45% of the slowdown is taken away.</summary>
[HarmonyPatch(typeof(Encumber), nameof(Encumber.GetEncumbranceSlowdownMultiplier))]
internal static class CarryHook
{
    static void Postfix(ref float __result)
    {
        if (!Effects.On("Sinew") || __result >= 1f || __result <= 0f) return;
        float was = __result; __result = was + (1f - was) * .45f; Seen.Once("overload slowdown", was, __result);
    }
}

/// <summary>Effects the mod drives itself once a second, with no game hook: thirst, the worn clothes, the dry lump.</summary>
internal static class Body
{
    static float lastHour = -1f, lastThirst = -1f, lumpAt;
    static string lumpFrom = "";
    static readonly ClothingRegion[] regions = { ClothingRegion.Head, ClothingRegion.Hands, ClothingRegion.Chest, ClothingRegion.Legs, ClothingRegion.Feet };
    static readonly ClothingLayer[] layers = { ClothingLayer.Base, ClothingLayer.Mid, ClothingLayer.Top, ClothingLayer.Top2 };
    /// <summary>Percent of wetness an hour adds to (sweat) or takes from (sugar stove) each worn piece.</summary>
    const float SweatPerHour = 10f, DryPerHour = 25f;

    public static void Forget() { lastHour = -1f; lastThirst = -1f; }

    public static void LumpFrom(string id) { lumpFrom = id; lumpAt = Time.unscaledTime; }

    public static void Tick(float now)
    {
        float dt = lastHour < 0f ? 0f : now - lastHour;
        lastHour = now;
        bool jump = dt < 0f || dt > 1f;   // a load or a long sleep
        if (jump) dt = 0f;

        // thirst: a drop since the last look is a drink (or juicy food)
        var thirst = GameManager.GetThirstComponent();
        if (thirst != null)
        {
            float t = thirst.m_CurrentThirst;
            float drop = lastThirst >= 0f && !jump ? lastThirst - t : 0f;
            if (drop > .05f)
            {
                if (Effects.On("SaltHold"))
                {
                    float was = t; t = Math.Max(0f, t - drop * .25f); thirst.m_CurrentThirst = t;
                    Seen.Once("drink quenches more", was, t);
                }
                if (drop > 1.5f && Effects.On("DryLump") && Time.unscaledTime - lumpAt > 3f)
                {
                    Effects.End("DryLump");
                    HUDMessage.AddMessage(Texts.Russian ? "Сухой ком прошёл" : "The dry lump is gone", 3f, false, false);
                }
            }
            lastThirst = t;
        }

        bool sweat = Effects.On("Sweat"), dry = Effects.On("SugarDry");
        if (sweat != dry && dt > 0f)   // both at once cancel out
        {
            var pm = GameManager.GetPlayerManagerComponent();
            float step = (sweat ? SweatPerHour : -DryPerHour) * dt;
            int n = 0;
            foreach (var r in regions)
                foreach (var l in layers)
                {
                    var gi = pm.GetClothingInSlot(r, l);
                    var ci = gi != null ? gi.m_ClothingItem : null;
                    if (ci == null || ci.m_PercentFrozen > 0f) continue;
                    float w = ci.m_PercentWet;
                    if (!sweat && w <= 0f) continue;
                    ci.m_PercentWet = Mathf.Clamp(w + step, 0f, 100f); n++;
                }
            if (n > 0) Seen.Once((sweat ? "sweat" : "sugar stove") + ", pieces " + n, 0f, step);
        }
    }

    /// <summary>Any food finished while the dry lump is on gives 15% less; the lump is used up by it. The dry snack itself does not count.</summary>
    public static void AnyFoodEaten(GearItem gi, float calories)
    {
        if (!Effects.On("DryLump") || calories < 50f) return;
        if (Time.unscaledTime - lumpAt < 3f || (Defs.Is(gi, out var def) && def.Id == lumpFrom && Time.unscaledTime - lumpAt < 30f)) return;
        Effects.End("DryLump");
        GameManager.GetHungerComponent().AddReserveCalories(-calories * .15f);
        HUDMessage.AddMessage(Texts.Russian ? $"Сухой ком: −{calories * .15f:0} ккал" : $"Dry lump: −{calories * .15f:0} kcal", 4f, false, false);
    }
}

[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.OnEatingComplete))]
internal static class LumpPatch
{
    static void Prefix(PlayerManager __instance, bool success, bool playerCancel)
    {
        try
        {
            var gi = __instance.m_FoodItemEaten;
            if (gi == null) return;
            float before = __instance.m_FoodItemEatenStartingCalories;
            float leftNow = gi.m_FoodItem != null ? gi.m_FoodItem.m_CaloriesRemaining : 0f;
            Body.AnyFoodEaten(gi, success && !playerCancel ? before : Mathf.Max(0f, before - leftNow));
        }
        catch (Exception e) { Safe.Log("Dry lump", e); }
    }
}

/// <summary>"Instead of a smoke" reaches into SmokesByEMPEROR, when it is installed: after its own check each second, the craving is switched off while seeds work.</summary>
internal static class SmokesLink
{
    static System.Reflection.PropertyInfo? craving, state;
    static System.Reflection.MethodInfo? sync;

    public static bool Find()
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "SmokesByEMPEROR");
        var habit = asm?.GetType("SmokesByEMPEROR.Habit");
        if (habit == null) return false;
        const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        craving = habit.GetProperty("Craving", F);
        sync = habit.GetMethod("Sync", F);
        var tick = habit.GetMethod("Tick", F);
        if (craving == null || craving.GetSetMethod(true) == null || tick == null) return false;
        new HarmonyLib.Harmony("SnacksByEMPEROR.smokes").Patch(tick, postfix: new HarmonyMethod(typeof(SmokesLink).GetMethod(nameof(AfterTick), F)));
        MelonLogger.Msg("SmokesByEMPEROR found: seeds hold the craving off.");
        return true;
    }

    static void AfterTick()
    {
        try
        {
            if (!Effects.On("SeedHabit") || craving == null || !(bool)craving.GetValue(null)!) return;
            craving.GetSetMethod(true)!.Invoke(null, new object[] { false });
            // its status lines: the craving line goes, the addiction line stays
            sync?.Invoke(null, new object[] { "Craving", false, 0f });
            sync?.Invoke(null, new object[] { "Addiction", true, 0f });
            Seen.Once("craving held off", 1f, 0f);
        }
        catch (Exception e) { Safe.Log("Smokes craving", e); }
    }
}
