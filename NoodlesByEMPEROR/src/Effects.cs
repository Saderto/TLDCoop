using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;

namespace NoodlesByEMPEROR;

/// <summary>What a bowl of hot noodles does besides warming: good and bad effects by what is in it (Def.Fx, set by tools/gen_cs.py).
/// They run on game time while the player plays; they are the eater's own (in co-op each player has theirs).
/// The hours left are written with the game's save (a file per world: slot name and world seed) and come back when that save is loaded.</summary>
public static class Effects
{
    public class Fx
    {
        public string Key = "", Ru = "", En = "", RuD = "", EnD = "";
        public bool Good;
        public float Hours;
    }

    // User rule 11-14: none of these may repeat an effect of the game or of our other mods (FoodPack's list is in food/tools_f88/data.py G1..G6).
    // Warmth (stronger for spicy) and thirst after dry noodles are plain item values, not effects.
    public static readonly Fx[] All =
    {
        new Fx { Key = "brothreserve", Good = true, Hours = 2f, Ru = "Бульонный запас", En = "Broth Reserve", RuD = "два часа жажда растёт на треть медленнее", EnD = "thirst grows a third slower for two hours" },
        new Fx { Key = "appetite", Good = true, Hours = 2f, Ru = "Раскрытый аппетит", En = "Whetted Appetite", RuD = "следующая еда в ближайшие два часа даст на 15% больше калорий", EnD = "the next food within two hours gives 15% more calories" },
        new Fx { Key = "emptycal", Good = false, Hours = 1.5f, Ru = "Пустые калории", En = "Empty Calories", RuD = "полтора часа калории сгорают на четверть быстрее", EnD = "calories burn a quarter faster for an hour and a half" },
        new Fx { Key = "heartburn", Good = false, Hours = 1f, Ru = "Изжога", En = "Heartburn", RuD = "час состояние само не восстанавливается", EnD = "condition does not recover by itself for an hour" },
        new Fx { Key = "tears", Good = false, Hours = .05f, Ru = "Слёзы градом", En = "Streaming Tears", RuD = "пару минут слезятся глаза, сразу хочется пить", EnD = "eyes water for a couple of minutes, instant thirst" },
    };

    static Fx? Find(string key) => All.FirstOrDefault(f => f.Key == key);

    public static IEnumerable<Fx> Of(Def d)
    {
        foreach (var k in d.Fx.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (Find(k) is { } f) yield return f;
    }

    /// <summary>For the item's description: " Эффекты: + Сырная шапка (…); − Пересол (…)."</summary>
    public static string Describe(Def d, bool rus)
    {
        return "";   // the effects are shown as icons on the item panel (EffectIcons), not as text (user 11-15)
    }

    static readonly Dictionary<string, float> left = new();   // effect -> game hours left
    static float lastHours = -1f, lastThirst = -1f, lastHp = -1f, tearTimer;

    /// <summary>share: how much of the bowl was eaten this time, 0..1.</summary>
    public static void Eaten(Def d, float share)
    {
        share = Mathf.Clamp01(share);
        if (share < .2f) return;
        var names = new List<string>();
        foreach (var f in Of(d))
        {
            names.Add((f.Good ? "+ " : "− ") + (Texts.Russian ? f.Ru : f.En));
            if (f.Key == "tears")
            {
                try { GameManager.GetThirstComponent().AddThirst(8f * share); }
                catch (Exception e) { Safe.Log("Tears", e); }
            }
            if (f.Hours > 0f) left[f.Key] = Mathf.Max(left.TryGetValue(f.Key, out var h) ? h : 0f, f.Hours * share);
        }
        nextSync = 0f;
        Safe.Info($"Eaten {d.Id} x{share:0.00}: {(names.Count > 0 ? string.Join(", ", names) : "no extra effects")}");
        if (names.Count > 0)
        {
            try { HUDMessage.AddMessage(string.Join("   ", names), 6f, false, false); }
            catch (Exception e) { Safe.Log("Effects message", e); }
        }
    }

    // ---- lines on the status screen (AfflictionComponent, as Snacks and Tea do): icon, name and time left. The truth is `left`, the lines follow it.

    static float nextSync;
    static readonly Dictionary<string, float> lineTried = new();

    public static float Now
    {
        get
        {
            try { return GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused(); }
            catch { return 0f; }
        }
    }

    static bool InGame()
    {
        try
        {
            return GameManager.GetPlayerManagerComponent() != null && !GameManager.IsMainMenuActive() && !SaveGameSystem.IsRestoreInProgress() && AfflictionManager.GetAfflictionManagerInstance() != null;
        }
        catch { return false; }
    }

    static void SyncLines()
    {
        if (Time.unscaledTime < nextSync) return;
        nextSync = Time.unscaledTime + 1f;
        if (!InGame()) return;
        try
        {
            var m = AfflictionManager.GetAfflictionManagerInstance();
            var all = m.m_Afflictions.OfType<NoodleAffliction>().ToList();
            float now = Now;
            foreach (var f in All)
            {
                bool want = left.TryGetValue(f.Key, out var h) && h > 0f;
                var have = all.Where(a => a.EffectKind == f.Key).ToList();
                for (int i = want ? 1 : 0; i < have.Count; i++) m.Remove(have[i]);
                if (!want) continue;
                if (have.Count == 0)
                {
                    // one try in 30 s: if the line does not get registered, it must not be started (with its notice and sound) again and again
                    if (lineTried.TryGetValue(f.Key, out var at) && Time.unscaledTime - at < 30f) continue;
                    lineTried[f.Key] = Time.unscaledTime;
                    new NoodleAffliction(f, h).Start(); Safe.Info($"Status line: {f.Key} {h:0.00} h");
                }
                else have[0].EndTime = now + h;
            }
        }
        catch (Exception e) { Safe.Log("Status lines", e); }
    }

    public static void Clear() { nextSync = 0f; left.Clear(); lastHours = -1f; lastThirst = -1f; lastHp = -1f; }

    // ---- kept with the save: a file per world (the slot's name and the world's seed: a new game in the same slot is another world)

    static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
    static string Folder => Path.Combine(Mod.Root, "saves");

    static string WorldKey()
    {
        string slot = SaveGameSystem.GetCurrentSaveName() ?? "";
        if (string.IsNullOrEmpty(slot)) return "";
        int seed = GameManager.m_SceneTransitionData != null ? GameManager.m_SceneTransitionData.m_GameRandomSeed : 0;
        return slot + "|" + seed.ToString(Inv);
    }

    static string FileOf(string key)
    {
        var safe = new string(key.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_').ToArray());
        return Path.Combine(Folder, safe + ".txt");
    }

    /// <summary>The game is writing its save: what is left of the effects goes with it.</summary>
    public static void Save()
    {
        try
        {
            string key = WorldKey();
            if (key.Length == 0) return;
            string path = FileOf(key);
            if (left.Count == 0) { if (File.Exists(path)) File.Delete(path); return; }
            Directory.CreateDirectory(Folder);
            File.WriteAllLines(path, left.Select(p => p.Key + "=" + p.Value.ToString("0.####", Inv)));
            Safe.Debug($"Effects saved for {key}: {left.Count}");
        }
        catch (Exception e) { Safe.Log("Saving effects", e); }
    }

    /// <summary>A save was loaded (or the next scene of the same game): the effects are what that save had.</summary>
    public static void Load()
    {
        Clear();
        try
        {
            string key = WorldKey();
            if (key.Length == 0) return;
            string path = FileOf(key);
            if (!File.Exists(path)) return;
            foreach (var line in File.ReadAllLines(path))
            {
                int i = line.IndexOf('=');
                if (i <= 0) continue;
                var f = Find(line.Substring(0, i).Trim());
                if (f == null || !float.TryParse(line.Substring(i + 1).Trim(), System.Globalization.NumberStyles.Float, Inv, out var h)) continue;
                if (h > 0f) left[f.Key] = Mathf.Min(h, f.Hours);
            }
            if (left.Count > 0) Safe.Info($"Effects for {key}: " + string.Join(", ", left.Select(p => $"{p.Key} {p.Value:0.00} h")));
        }
        catch (Exception e) { Safe.Log("Loading effects", e); }
    }

    /// <summary>Any food eaten while the appetite is whetted gives 15% more; the bonus is used up by it.</summary>
    public static void AnyFoodEaten(float calories)
    {
        if (!On("appetite") || calories < 50f) return;
        left.Remove("appetite");
        try
        {
            GameManager.GetHungerComponent().AddReserveCalories(calories * .15f);
            HUDMessage.AddMessage(Texts.Russian ? $"Раскрытый аппетит: +{calories * .15f:0} ккал" : $"Whetted Appetite: +{calories * .15f:0} kcal", 4f, false, false);
        }
        catch (Exception e) { Safe.Log("Appetite", e); }
    }

    public static void Tick()
    {
        SyncLines();
        if (left.Count == 0) { lastHours = -1f; lastThirst = -1f; lastHp = -1f; return; }
        if (GameManager.GetPlayerManagerComponent() == null) return;
        var tod = GameManager.GetTimeOfDayComponent();
        if (tod == null) return;
        float now = tod.GetHoursPlayedNotPaused();
        if (lastHours < 0f || now < lastHours) { lastHours = now; return; }
        float dt = Mathf.Min(now - lastHours, 1f);   // sleeping or passing time: at most an hour's worth per step
        lastHours = now;
        if (dt <= 0f) return;

        var thirst = GameManager.GetThirstComponent();
        if (On("brothreserve"))
        {
            float t = thirst.m_CurrentThirst;
            if (lastThirst >= 0f && t > lastThirst) { t = lastThirst + (t - lastThirst) * .67f; thirst.m_CurrentThirst = t; }
            lastThirst = t;
        }
        else lastThirst = -1f;
        if (On("emptycal"))
        {
            var hunger = GameManager.GetHungerComponent();
            hunger.AddReserveCalories(-hunger.m_CurrentCalorieBurnPerHour * .25f * dt);
        }
        if (On("heartburn"))
        {
            var c = GameManager.GetConditionComponent();
            float hp = c.m_CurrentHP;
            if (lastHp >= 0f && hp > lastHp && hp - lastHp < c.m_MaxHP * .01f) { hp = lastHp; c.m_CurrentHP = hp; }   // slow natural recovery only: medicine and sleep jumps pass
            lastHp = hp;
        }
        else lastHp = -1f;
        if (On("tears"))
        {
            tearTimer -= Time.deltaTime;
            if (tearTimer <= 0f)
            {
                tearTimer = 4f;
                try { GameManager.GetCameraEffects().WaterPulse(.6f); } catch (Exception e) { Safe.Log("Tears pulse", e); }
            }
        }

        foreach (var k in left.Keys.ToList())
        {
            float h = left[k] - dt;
            if (h > 0f) { left[k] = h; continue; }
            left.Remove(k);
            if (Find(k) is { } f)
            {
                try { HUDMessage.AddMessage((Texts.Russian ? f.Ru + ": прошло" : f.En + ": worn off"), 4f, false, false); }
                catch (Exception e) { Safe.Log("Effects end message", e); }
            }
        }
    }

    public static bool On(string key) => left.ContainsKey(key);
}

/// <summary>A line on the status screen. Public fields and a public empty constructor, so AfflictionComponent's save can write and read it.</summary>
public class NoodleAffliction : CustomAffliction, IBuff, IDuration, IInstance
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

    public NoodleAffliction() : base("", "", "", null, "", AfflictionBodyArea.Stomach, true) { }

    public NoodleAffliction(Effects.Fx f, float hours)
        : base("NOODLES_FX_" + f.Key + "_NAME", "NOODLES_FX_CAUSE", "NOODLES_FX_" + f.Key + "_DESC", f.Good ? null : "NOODLES_FX_SELF", "NoodlesByEMPEROR.Icons." + f.Key + ".png",
            f.Key == "tears" ? AfflictionBodyArea.Head : f.Key == "heartburn" ? AfflictionBodyArea.Chest : AfflictionBodyArea.Stomach, true)
    {
        EffectKind = f.Key;
        Buff = f.Good;
        BuffThirst = f.Key == "brothreserve"; BuffHunger = f.Key == "appetite";
        Duration = hours;
    }

    public override void OnUpdate() { }

    public void OnFoundExistingInstance(CustomAffliction existing)
    {
        if (existing is NoodleAffliction s && Duration > 0f) s.EndTime = Math.Max(s.EndTime, Effects.Now + Duration);
    }
}

/// <summary>Eating our hot noodles starts their effects.</summary>
[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.OnEatingComplete))]
internal static class EatenPatch
{
    static void Prefix(PlayerManager __instance, bool success, bool playerCancel, float progress)
    {
        try
        {
            var gi = __instance.m_FoodItemEaten;
            if (gi == null) return;
            float before = __instance.m_FoodItemEatenStartingCalories;
            float leftNow = gi.m_FoodItem != null ? gi.m_FoodItem.m_CaloriesRemaining : 0f;
            float eaten = success && !playerCancel ? before : Mathf.Max(0f, before - leftNow);
            Effects.AnyFoodEaten(eaten);
            if (!Defs.Is(gi, out var def) || def.Kind != Kind.Brewed) return;
            Effects.Eaten(def, eaten / Mathf.Max(1f, def.Kcal));
        }
        catch (Exception e) { Safe.Log("Eaten hook", e); }
    }
}

[HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.SaveGlobalData))]
internal static class EffectsSavePatch
{
    static void Postfix() => Effects.Save();
}

[HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.RestoreGlobalData))]
internal static class EffectsLoadPatch
{
    static void Postfix() => Effects.Load();
}

[HarmonyPatch(typeof(GameManager), nameof(GameManager.LaunchSandbox))]
internal static class EffectsNewGamePatch
{
    static void Prefix() => Effects.Clear();
}
