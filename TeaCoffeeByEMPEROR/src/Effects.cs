using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;

namespace TeaCoffeeByEMPEROR;

/// <summary>The drinks' own effects besides the game's buffs (owner's picks 11-14; Catalog.Fx, set per product by tools/catalog.py).
/// Owner's rule: none may repeat an effect of the game or of our other mods; the game already has cabin fever and insomnia buffs, so those were dropped.
/// Timed ones run on game time while the player plays and are the drinker's own (in co-op each player has theirs).
/// The hours left are written with the game's save (a file per world: slot name and world seed) and come back when that save is loaded.
/// "longheat" is a property of the drink itself (Items.Drink), not timed; "vitc" (Strong Ligaments) is timed.</summary>
public static class Effects
{
    static Catalog.F? Find(string key) => Catalog.Fx.FirstOrDefault(f => f.Key == key);

    static IEnumerable<Catalog.F> Of(Def d)
    {
        foreach (var k in d.P.Fx.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (Find(k) is { } f) yield return f;
    }

    public static bool Has(Def d, string key) => (" " + d.P.Fx + " ").Contains(" " + key + " ");

    static readonly Dictionary<string, float> left = new();   // effect -> game hours left
    static float lastHours = -1f, lastHp = -1f, mapRadius = -1f;
    static float[]? frostWas;   // the game's sprain chances before Strong Ligaments halved them

    public static bool On(string key) => left.ContainsKey(key);

    /// <summary>share: how much of the cup was drunk this time, 0..1.</summary>
    public static void Drunk(Def d, float share)
    {
        share = Mathf.Clamp01(share);
        if (share < .2f) return;
        var names = new List<string>();
        foreach (var f in Of(d))
        {
            if (f.Hours <= 0f) continue;
            names.Add((f.Good ? "+ " : "− ") + (Texts.Russian ? f.Ru : f.En));
            left[f.Key] = Mathf.Max(left.TryGetValue(f.Key, out var h) ? h : 0f, f.Hours * share);
        }
        Safe.Info($"Drunk {d.Id} x{share:0.00}: {(names.Count > 0 ? string.Join(", ", names) : "no timed effects of its own")}");
        if (names.Count > 0)
        {
            try { HUDMessage.AddMessage(string.Join("   ", names), 6f, false, false); }
            catch (Exception e) { Safe.Log("Effects message", e); }
        }
    }

    public static void Clear() { left.Clear(); lastHours = -1f; lastHp = -1f; RestoreMap(); RestoreFrost(); }

    // ---- shown in the status panel (AfflictionComponent's lines, as our other food mods do)

    public static float Now
    {
        get { try { return GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused(); } catch { return 0f; } }
    }

    static float nextLines;

    static void Lines()
    {
        if (Time.unscaledTime < nextLines) return;
        nextLines = Time.unscaledTime + 1f;
        try
        {
            if (GameManager.GetPlayerManagerComponent() == null || GameManager.IsMainMenuActive() || SaveGameSystem.IsRestoreInProgress()) return;
            var m = AfflictionManager.GetAfflictionManagerInstance();
            if (m == null) return;
            foreach (var f in Catalog.Fx)
            {
                if (f.Hours <= 0f) continue;
                bool want = left.TryGetValue(f.Key, out var h) && h > 0f;
                var have = m.m_Afflictions.OfType<DrinkAffliction>().Where(a => a.EffectKind == f.Key).ToList();
                for (int i = want ? 1 : 0; i < have.Count; i++) m.Remove(have[i]);
                if (!want) continue;
                if (have.Count == 0) { new DrinkAffliction(f, h).Start(); Safe.Info($"Status line on: {f.Key}, {h:0.00} h"); }
                else have[0].EndTime = Now + h;
            }
        }
        catch (Exception e) { Safe.Log("Status lines", e); }
    }

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

    // ---- what they do

    /// <summary>Wide Awake: no going to sleep while the fatigue bar is more than 40% full (the game counts tiredness up, so under 60% of it).</summary>
    public static bool BlocksSleep()
    {
        if (!On("nosleep")) return false;
        var f = GameManager.GetFatigueComponent();
        if (f == null || f.m_MaxFatigue <= 0f) return false;
        return f.m_CurrentFatigue / f.m_MaxFatigue < .6f;
    }

    /// <summary>Strong Ligaments: every chance of a sprain on a slope is halved while it lasts.</summary>
    static void SlowFrost()
    {
        if (frostWas != null) return;
        try
        {
            var s = GameManager.GetSprainsComponent();
            if (s == null) return;
            frostWas = new[] { s.m_BaseChanceWhenMovingOnSlope, s.m_ChanceIncreaseEachDegreeAboveMinSlope, s.m_ChanceIncreaseEncumbered, s.m_ChanceIncreaseExhausted, s.m_ChanceIncreaseSprinting };
            s.m_BaseChanceWhenMovingOnSlope = frostWas[0] * .5f; s.m_ChanceIncreaseEachDegreeAboveMinSlope = frostWas[1] * .5f;
            s.m_ChanceIncreaseEncumbered = frostWas[2] * .5f; s.m_ChanceIncreaseExhausted = frostWas[3] * .5f; s.m_ChanceIncreaseSprinting = frostWas[4] * .5f;
            Safe.Info($"Strong Ligaments: sprain chance on a slope {frostWas[0]:0.###} -> {frostWas[0] * .5f:0.###}, per degree {frostWas[1]:0.###} -> {frostWas[1] * .5f:0.###}");
        }
        catch (Exception e) { Safe.Log("Sprain chance", e); }
    }

    static void RestoreFrost()
    {
        if (frostWas == null) return;
        try
        {
            var s = GameManager.GetSprainsComponent();
            if (s != null)
            {
                s.m_BaseChanceWhenMovingOnSlope = frostWas[0]; s.m_ChanceIncreaseEachDegreeAboveMinSlope = frostWas[1];
                s.m_ChanceIncreaseEncumbered = frostWas[2]; s.m_ChanceIncreaseExhausted = frostWas[3]; s.m_ChanceIncreaseSprinting = frostWas[4];
            }
        }
        catch (Exception e) { Safe.Log("Sprain chance restore", e); }
        frostWas = null;
    }

    static void RestoreMap()
    {
        if (mapRadius <= 0f) return;
        try
        {
            var map = InterfaceManager.GetPanel<Panel_Map>();
            if (map != null) map.m_DetailSurveyRadiusMeters = mapRadius;
        }
        catch (Exception e) { Safe.Log("Map radius back", e); }
        mapRadius = -1f;
    }

    // Wide Awake. No hook on the game's sleep buttons: a patch on Panel_Rest.OnRest / OnFullRest fired thousands of times a second with no bed
    // at all (game test 11-15), so the rest panel is watched instead: while the drink works and the bar is too full, its sleep page turns to
    // the pass-time page (or closes when passing time is not allowed there), with one message per try.
    static bool restWasBlocked;

    static void WatchRestPanel()
    {
        try
        {
            if (!On("nosleep")) { restWasBlocked = false; return; }
            var panel = InterfaceManager.GetPanel<Panel_Rest>();
            if (panel == null || !panel.isActiveAndEnabled || !panel.IsEnabled()) { restWasBlocked = false; return; }
            if (panel.IsPassingTimeOnly() || panel.m_ShowPassTime) { restWasBlocked = false; return; }
            var rest = GameManager.GetRestComponent();
            if (rest != null && rest.IsSleeping()) return;
            if (!BlocksSleep()) return;
            if (panel.GetPassTimeAllowed()) panel.OnSelectPassTime(); else panel.OnCancel();
            if (!restWasBlocked)
            {
                restWasBlocked = true;
                HUDMessage.AddMessage(Texts.Russian ? "Не спится: напиток ещё действует" : "Wide awake: the drink is still working", 4f, false, false);
                Safe.Info("Sleep page turned away: Wide Awake");
            }
        }
        catch (Exception e) { Safe.Log("No sleep", e); }
    }

    public static void Tick()
    {
        Lines();
        WatchRestPanel();
        if (left.Count == 0) { lastHours = -1f; lastHp = -1f; return; }
        if (GameManager.GetPlayerManagerComponent() == null) return;
        var tod = GameManager.GetTimeOfDayComponent();
        if (tod == null) return;
        float now = tod.GetHoursPlayedNotPaused();
        if (lastHours < 0f || now < lastHours) { lastHours = now; return; }
        float raw = now - lastHours;
        float dt = Mathf.Min(raw, 1f);   // sleeping or passing time: at most an hour's worth per step
        lastHours = now;
        if (dt <= 0f) return;

        if (On("hardy"))
        {
            try
            {
                var c = GameManager.GetConditionComponent();
                float hp = c.m_CurrentHP;
                // only the slow loss of an empty need: a bite or a fall is a jump and passes as it is
                bool need = GameManager.GetHungerComponent().IsStarving() || GameManager.GetThirstComponent().IsDehydrated()
                    || GameManager.GetFatigueComponent().IsExhausted() || GameManager.GetFreezingComponent().IsFreezing();
                if (need && lastHp >= 0f && hp < lastHp && hp > 0f && (lastHp - hp) / raw < c.m_MaxHP * .6f)
                {
                    hp += (lastHp - hp) * .2f;
                    c.m_CurrentHP = hp;
                }
                lastHp = hp;
            }
            catch (Exception e) { Safe.Log("Hardiness", e); }
        }
        else lastHp = -1f;

        if (On("antidote"))
        {
            try
            {
                var cp = GameManager.GetChemicalPoisoningComponent();
                if (cp != null && cp.m_Toxicity > 0f && !cp.m_InHazardZone)
                    cp.m_Toxicity = Mathf.Max(0f, cp.m_Toxicity - cp.m_ToxicityLostPerHour * .25f * dt);
            }
            catch (Exception e) { Safe.Log("Antidote", e); }
        }

        if (On("map"))
        {
            if (mapRadius <= 0f)
            {
                try
                {
                    var map = InterfaceManager.GetPanel<Panel_Map>();
                    if (map != null && map.m_DetailSurveyRadiusMeters > 0f)
                    {
                        mapRadius = map.m_DetailSurveyRadiusMeters;
                        map.m_DetailSurveyRadiusMeters = mapRadius * 1.25f;
                        Safe.Info($"Mapmaker: survey radius {mapRadius:0} -> {mapRadius * 1.25f:0} m");
                    }
                }
                catch (Exception e) { Safe.Log("Map radius", e); }
            }
        }
        else RestoreMap();

        if (On("vitc")) SlowFrost(); else RestoreFrost();

        foreach (var k in left.Keys.ToList())
        {
            float h = left[k] - dt;
            if (h > 0f) { left[k] = h; continue; }
            left.Remove(k);
            if (k == "map") RestoreMap();
            if (k == "vitc") RestoreFrost();
            if (Find(k) is { } f)
            {
                Safe.Info("Effect over: " + k);
                try { HUDMessage.AddMessage(Texts.Russian ? f.Ru + ": прошло" : f.En + ": worn off", 4f, false, false); }
                catch (Exception e) { Safe.Log("Effects end message", e); }
            }
        }
    }
}

/// <summary>A line in the status panel. Public fields and a public empty constructor, so AfflictionComponent's save can write and read it.</summary>
public class DrinkAffliction : CustomAffliction, IBuff, IDuration, IInstance
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

    public DrinkAffliction() : base("", "", "", null, "", AfflictionBodyArea.Stomach, true) { }

    public DrinkAffliction(Catalog.F f, float hours)
        : base("TEACOFFEE_FX_" + f.Key + "_NAME", "TEACOFFEE_FX_CAUSE", "TEACOFFEE_FX_" + f.Key + "_DESC", null, "TeaCoffeeByEMPEROR.Icons.fx_" + f.Key + ".png",
            f.Key is "nosleep" or "map" ? AfflictionBodyArea.Head : f.Key is "relaxed" ? AfflictionBodyArea.LegRight : f.Key is "hardy" ? AfflictionBodyArea.Chest : AfflictionBodyArea.Stomach, true)
    {
        EffectKind = f.Key;
        Buff = f.Good;
        Duration = hours;
    }

    public override void OnUpdate() { }

    public void OnFoundExistingInstance(CustomAffliction existing)
    {
        if (existing is DrinkAffliction s && Duration > 0f) s.EndTime = Math.Max(s.EndTime, Effects.Now + Duration);
    }
}

/// <summary>Drinking one of ours starts its effects.</summary>
[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.OnEatingComplete))]
internal static class DrunkPatch
{
    static void Prefix(PlayerManager __instance, bool success, bool playerCancel, float progress)
    {
        try
        {
            var gi = __instance.m_FoodItemEaten;
            if (gi == null || !Defs.Is(gi, out var def) || def.Kind == Kind.Pack) return;
            Effects.Drunk(def, success && !playerCancel ? 1f : progress);
        }
        catch (Exception e) { Safe.Log("Drunk hook", e); }
    }
}

/// <summary>Mellow: sprinting is a tenth slower.</summary>
[HarmonyPatch(typeof(PlayerMovement), nameof(PlayerMovement.GetSprintMovementMultiplier))]
internal static class RelaxedPatch
{
    static void Postfix(ref float __result)
    {
        if (!Effects.On("relaxed")) return;
        Safe.Once("relaxedworks", $"Mellow: sprint multiplier {__result:0.00} -> {__result * .9f:0.00}");
        __result *= .9f;
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
