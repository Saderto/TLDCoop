namespace TeaCoffeeByEMPEROR;

/// <summary>The pot and our packs. The game builds the cooked item from its own catalogue; ours is not there, so the pot has nothing to give
/// and the game falls over on taking it (NullReference in AddItemToPlayerInventory with no item at all, game tests of 11-14).
/// 1. We make the cup ourselves for looking at it in the pot;
/// 2. whenever the game then tries to put «nothing» into the pack while one of our brews is being taken, it gets that cup instead;
/// 3. the liquid in the pot has the colour of the drink.</summary>
/// <summary>The real cause (GameAssembly disassembled by the noodles thread, 11-14): the pot makes the cooked item with
/// PlayerManager.InstantiateItemAtPlayersFeet(cooked prefab's reference, units); that overload first asks the reference RuntimeKeyIsValid(),
/// ours is not a real addressable, so it gave nothing. Our references go to the by-name overload; the rest is the game's own flow.
/// PotPatches below stays as a second line.</summary>
[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.InstantiateItemAtPlayersFeet), new[] { typeof(AssetReferenceGearItem), typeof(int) })]
internal static class FeetPatch
{
    static bool Prefix(PlayerManager __instance, AssetReferenceGearItem assetReference, int numUnits, ref GearItem __result)
    {
        try
        {
            if (!Items.FromReference(assetReference, out var name)) return true;
            __result = __instance.InstantiateItemAtPlayersFeet(name, numUnits);
            Safe.Info($"Pot gives {name} x{numUnits}: {(__result != null ? "made" : "FAILED")}");
            return false;
        }
        catch (Exception e) { Safe.Log("Feet", e); return true; }
    }
}

[HarmonyPatch]
internal static class PotPatches
{
    static bool Ours(CookingPotItem pot, out Def raw, out Def cup)
    {
        raw = cup = null!;
        if (pot == null) return false;
        var g = pot.m_GearItemBeingCooked;
        if (g == null || !Defs.Is(g, out raw) || raw.Kind != Kind.Pack) return false;
        cup = Defs.ById(raw.Id + "Cup")!;
        return cup != null;
    }

    static bool Good(GearItem? g) => g != null && g && Defs.Is(g, out var d) && d.Kind == Kind.Cup;

    // the last brew the player looked at or took: what an empty hand-over is filled with
    static CookingPotItem? lastPot;
    static Def? lastCup;
    static GearItem? lastDrink;
    static float lastTime = -999f;
    static bool lastRuined;

    static GearItem? Make(CookingPotItem? pot, Def cup, bool ruined)
    {
        var drink = Items.Spawn(cup.Id);
        if (drink == null) { MelonLogger.Error($"Pot: could not make {cup.Id}"); return null; }
        try
        {
            if (pot != null && pot && pot.m_GearItemBeingCooked != null) pot.SetCookedGearProperties(pot.m_GearItemBeingCooked, drink);
            else if (drink.m_FoodItem != null) drink.m_FoodItem.m_HeatPercent = 100f;
        }
        catch (Exception e) { Safe.Log("Pot cooked properties", e); }
        if (ruined) drink.CurrentHP = 0f;
        drink.gameObject.name = Defs.Prefix + cup.Id;
        return drink;
    }

    static void Ensure(CookingPotItem pot, string where)
    {
        try
        {
            if (!Ours(pot, out var raw, out var cup)) return;
            if (pot.GetCookingState() == CookingPotItem.CookingState.Cooking) return;
            lastPot = pot; lastCup = cup; lastTime = Time.unscaledTime; lastRuined = pot.GetCookingState() == CookingPotItem.CookingState.Ruined;
            var have = pot.m_GearItemSpawnedForInspectMode;
            if (Good(have)) { lastDrink = have; Safe.Debug($"Pot ({where}): {cup.Id} is there"); return; }
            if (have != null && have) UnityEngine.Object.Destroy(have.gameObject);
            var drink = Make(pot, cup, lastRuined);
            if (drink == null) return;
            // kept switched on (a switched-off cup showed no model when looked at), out of sight under the floor until the game takes it
            drink.transform.position = pot.transform.position + Vector3.down * 40f;
            pot.m_GearItemSpawnedForInspectMode = drink;
            lastDrink = drink;
            Safe.Info($"Pot ({where}): made {cup.Id} for the brewed {raw.Id}, state {pot.GetCookingState()}");
        }
        catch (Exception e) { Safe.Log("Pot ensure " + where, e); }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.MaybeSpawnGearItemForInspectMode))]
    static void AfterSpawn(CookingPotItem __instance) => Ensure(__instance, "inspect");

    [HarmonyPrefix, HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.GetGearItemForInspectMode))]
    static void BeforeGet(CookingPotItem __instance) => Ensure(__instance, "get");

    [HarmonyPrefix, HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.StartInspectMode))]
    static void BeforeInspect(CookingPotItem __instance) => Ensure(__instance, "look");

    [HarmonyPrefix, HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.InspectModeCallback))]
    static void BeforeCallback(CookingPotItem __instance) => Ensure(__instance, "callback");

    [HarmonyPrefix, HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.PickUpCookedItem))]
    static void BeforePickUp(CookingPotItem __instance) => Ensure(__instance, "pick up");

    [HarmonyPrefix, HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.PickUpCookedGearItem))]
    static void BeforeTake(CookingPotItem __instance, bool addToInventory) => Ensure(__instance, "take " + addToInventory);

    /// <summary>The game hands «nothing» to the pack right after one of our brews was looked at or taken: it gets our cup.</summary>
    [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.AddItemToPlayerInventory))]
    static void FillEmpty(ref GearItem gi)
    {
        try
        {
            if (gi != null || lastCup == null || Time.unscaledTime - lastTime > 20f) return;
            var drink = Good(lastDrink) && !lastDrink!.m_InPlayerInventory ? lastDrink : Make(lastPot, lastCup, lastRuined);
            if (drink == null) return;
            if (!drink.gameObject.activeSelf) drink.gameObject.SetActive(true);
            if (lastPot != null && lastPot && lastPot.m_GearItemSpawnedForInspectMode != null && lastPot.m_GearItemSpawnedForInspectMode.Pointer == drink.Pointer)
                lastPot.m_GearItemSpawnedForInspectMode = null;
            gi = drink;
            Safe.Info($"Pot: the game was taking nothing, gave it {lastCup.Id}");
            lastCup = null; lastDrink = null; lastTime = -999f;
        }
        catch (Exception e) { Safe.Log("Pot fill empty", e); }
    }

    // ---------------------------------------------------------------- the drink's colour in the pot

    static readonly Dictionary<IntPtr, (string id, Material mat)> painted = new();
    static readonly Dictionary<int, Texture2D> flats = new();
    static bool loggedVisual;

    static Texture2D Flat(int rgb)
    {
        if (flats.TryGetValue(rgb, out var t) && t != null) return t;
        t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var c = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        var px = new Color32[16];
        for (int i = 0; i < 16; i++) px[i] = c;
        t.SetPixels32(px); t.Apply(false, true);
        t.hideFlags = HideFlags.HideAndDontSave;
        return flats[rgb] = t;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(CookingPotItem), nameof(CookingPotItem.UpdateVisuals))]
    static void AfterVisuals(CookingPotItem __instance)
    {
        try
        {
            if (!Ours(__instance, out var raw, out _)) return;
            if (__instance.GetCookingState() == CookingPotItem.CookingState.Ruined) return;   // burnt: the game's own look
            var r = __instance.m_GrubMeshRenderer;
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.sharedMaterial == null) return;
            if (painted.TryGetValue(__instance.Pointer, out var p) && p.id == raw.Id && p.mat != null)
            {
                // the game puts its own material back on every refresh: ours goes on again
                if (r.sharedMaterial.Pointer != p.mat.Pointer) r.sharedMaterial = p.mat;
                return;
            }
            // the game's liquid shader takes its colour from its textures (which one is not known by name): every picture that is not
            // a normal map becomes the drink's flat colour; with them taken away the liquid came out white (game test 11-15)
            var tex = Flat(raw.P.Liquid);
            var m = new Material(r.sharedMaterial);
            m.hideFlags = HideFlags.HideAndDontSave;
            var names = new List<string>();
            foreach (var n in m.GetTexturePropertyNames())
            {
                bool bump = n.IndexOf("norm", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("bump", StringComparison.OrdinalIgnoreCase) >= 0;
                names.Add(n + (m.GetTexture(n) != null ? "" : "(empty)") + (bump ? "(kept)" : ""));
                if (!bump) m.SetTexture(n, tex);
            }
            if (!loggedVisual)
            {
                var cols = new List<string>();
                var sh = m.shader;
                for (int i = 0; sh != null && i < sh.GetPropertyCount(); i++)
                    if (sh.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Color) cols.Add(sh.GetPropertyName(i) + "=" + m.GetColor(sh.GetPropertyName(i)));
                Safe.Info("Pot liquid material: textures " + string.Join(", ", names) + "; colours " + string.Join(", ", cols));
            }
            string was = r.sharedMaterial.name;
            r.sharedMaterial = m;
            painted[__instance.Pointer] = (raw.Id, m);
            if (!loggedVisual)
            {
                loggedVisual = true;
                Safe.Info($"Pot colour: {raw.Id} #{raw.P.Liquid:X6} on {r.name}, was {was}, shader {m.shader?.name}");
            }
        }
        catch (Exception e) { Safe.Log("Pot visuals", e); }
    }
}
