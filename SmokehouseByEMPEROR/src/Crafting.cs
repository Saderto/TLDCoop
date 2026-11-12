using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace SmokehouseByEMPEROR;

/// <summary>The stations at the workbench. Barrel: 30 scrap metal, 6 reclaimed wood, 6 cloth, 4 stones, a hacksaw, 64 hours. Kettle: 15 scrap metal, 4 stones, a hacksaw, 24 hours.
/// Table: 8 reclaimed wood, 4 scrap metal, 2 cloth, 12 hours. Copies of one of the game's own workbench recipes, added to the list the first time the game asks for it in a loaded game.</summary>
[HarmonyPatch]
internal static class Crafting
{
    const string NamePrefix = "BLUEPRINT_GEAR_SH_";
    record Recipe(string Id, (string gear, int n)[] Needs, int Hours, string? Tool);
    static readonly Recipe[] Recipes =
    {
        new(Defs.Barrel, new[] { ("GEAR_ScrapMetal", 30), ("GEAR_ReclaimedWoodB", 6), ("GEAR_Cloth", 6), ("GEAR_Stone", 4) }, 64, "GEAR_Hacksaw"),
        new(Defs.Kettle, new[] { ("GEAR_ScrapMetal", 15), ("GEAR_Stone", 4) }, 24, "GEAR_Hacksaw"),
        new(Defs.Table, new[] { ("GEAR_ReclaimedWoodB", 8), ("GEAR_ScrapMetal", 4), ("GEAR_Cloth", 2) }, 12, null),
    };
    static readonly List<BlueprintData> made = new();
    static bool built;
    static int failed;
    static readonly Dictionary<string, GearItem> parts = new();

    static GearItem Part(string gear)
    {
        if (parts.TryGetValue(gear, out var g) && g != null) return g;
        g = GearItem.InstantiateGearItem(gear) ?? throw new InvalidOperationException("no " + gear);
        g.gameObject.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(g.gameObject);
        return parts[gear] = g;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(BlueprintManager), nameof(BlueprintManager.GetAllBlueprints))]
    static void All(Il2CppSystem.Collections.Generic.List<BlueprintData> __result)
    {
        if (__result == null || failed >= 3) return;
        try
        {
            if (built)
            {
                foreach (var m in made) if (!__result.Contains(m)) __result.Add(m);
                Apply();
                Saws();
                return;
            }
            if (GameManager.GetPlayerManagerComponent() == null || GameManager.GetInventoryComponent() == null) return;   // not in a game yet
            BlueprintData? src = null;
            foreach (var b in __result)
                if (b != null && b.m_RequiredCraftingLocation == CraftingLocation.Workbench && !b.m_RequiresLitFire && b.m_CraftedResultGear != null) { src = b; break; }
            if (src == null) return;
            foreach (var r in Recipes)
            {
                if (made.Any(m => m.name == NamePrefix + r.Id)) continue;
                var n = Build(src, r);
                made.Add(n);
                __result.Add(n);
                Safe.Info($"Recipe added: {r.Id} (copied from {src.name}).");
            }
            built = true;
            Apply();
        }
        catch (Exception e)
        {
            failed++;
            MelonLogger.Error("Station recipes: " + e);
        }
    }

    static BlueprintData Build(BlueprintData src, Recipe r)
    {
        var result = Items.Template(r.Id) ?? throw new InvalidOperationException("no item " + r.Id);
        var n = UnityEngine.Object.Instantiate(src);
        n.name = NamePrefix + r.Id;
        n.hideFlags = HideFlags.HideAndDontSave;
        n.SetCraftedResult(result);
        n.m_CraftedResultGear = result;
        n.m_CraftedResultCount = 1;
        n.m_DurationMinutes = r.Hours * 60;
        n.m_RequiredCraftingLocation = CraftingLocation.Workbench;
        n.m_RequiresLitFire = false;
        n.m_RequiresLight = true;
        n.m_RequiredTool = null;
        if (r.Tool != null)
        {
            try
            {
                var tool = Part(r.Tool).GetComponent<ToolsItem>();
                if (tool != null) n.m_RequiredTool = tool;
                else Safe.Info($"Recipe {r.Id}: {r.Tool} has no tool part, no tool is asked");
            }
            catch (Exception e) { Safe.Log("Recipe tool", e); }
        }
        n.m_OptionalTools = new Il2CppReferenceArray<ToolsItem>(0L);
        n.m_Locked = false;
        n.m_IgnoreLockInSurvival = true;
        n.m_AppearsInStoryOnly = false;
        n.m_AppearsInSurvivalOnly = false;
        n.m_XPModesToDisable?.Clear();
        n.m_UsesPhoto = false;
        n.m_CanIncreaseRepairSkill = false;
        n.m_RequiredGear = Needs(r);
        n.m_RequiredLiquid = new Il2CppReferenceArray<BlueprintData.RequiredLiquid>(0L);
        n.m_RequiredPowder = new Il2CppReferenceArray<BlueprintData.RequiredPowder>(0L);
        return n;
    }

    static float nextSaws;
    static bool sawLogged;

    /// <summary>The hacksaw is no workbench tool in the game and its crafting time factor is zero: the barrel and the kettle took 0 minutes (11-12 owner).
    /// Every hacksaw in the pack, and ours in the recipe, gets the plain factor 1.</summary>
    static void Saws()
    {
        if (Time.unscaledTime < nextSaws) return;
        nextSaws = Time.unscaledTime + 1f;
        void Fix(ToolsItem? tool, string where)
        {
            if (tool == null) return;
            if (tool.m_CraftingAndRepairTimeModifier < .99f || tool.m_CraftingAndRepairTimeModifier > 1.01f)
            {
                MelonLogger.Msg($"Hacksaw ({where}): time factor {tool.m_CraftingAndRepairTimeModifier} set to 1, skill {tool.m_CraftingAndRepairSkillModifier}");
                tool.m_CraftingAndRepairTimeModifier = 1f;
            }
        }
        foreach (var r in Recipes)
            if (r.Tool != null && parts.TryGetValue(r.Tool, out var p) && p != null) Fix(p.GetComponent<ToolsItem>(), "recipe");
        var inv = GameManager.GetInventoryComponent();
        if (inv == null) return;
        foreach (var o in inv.m_Items)
        {
            var g = o?.m_GearItem;
            if (g != null && Defs.Clean(g.name) == "GEAR_Hacksaw") Fix(g.GetComponent<ToolsItem>(), "pack");
        }
    }

    static Il2CppReferenceArray<BlueprintData.RequiredGearItem> Needs(Recipe r)
    {
        var req = new Il2CppReferenceArray<BlueprintData.RequiredGearItem>(r.Needs.Length);
        for (int i = 0; i < r.Needs.Length; i++)
            req[i] = new BlueprintData.RequiredGearItem { m_Item = Part(r.Needs[i].gear), m_Count = r.Needs[i].n, m_Units = BlueprintData.RequiredGearItem.Units.Count, m_Weight = ItemWeight.FromKilograms(0f) };
        return req;
    }

    // ---- a station that was not finished (11-12 owner: an unfinished one is not placed or used, and is finished later).
    // The game hands over the whole station when the work is broken off, so the unfinished one is kept here, not as an item:
    // the materials are spent, the work done is remembered for this world, and the recipe then asks only for the hours that are left.
    const string JobPrefix = "Build:";
    static Job? Unfinished(string id) => Barrels.Jobs.FirstOrDefault(j => j.Kind == JobPrefix + id);
    static readonly Dictionary<string, int> baseline = new();
    static string? working;          // the station being built now
    static IntPtr workingOp;
    static bool ended;
    /// <summary>Hours the game asks for what is left of the station, with every modifier on it (a feat that speeds crafting, 11-12: the player was offered
    /// only 3/4 of the recipe's hours, so a station never came to its end). 0: unknown.</summary>
    static float offeredHours;
    /// <summary>The station whose last hours were just worked: when the game's own item is swept away, a clean finished one is given (0.4.24).
    /// The game keeps its own «в процессе N%» on the item it hands over, and by its count our stations were never done: the player got a kettle
    /// marked 78% that could be placed. So the game's item is never kept, whatever it thinks of it.</summary>
    static string? finished;
    static readonly Dictionary<string, bool> hadWip = new();
    static string shownState = "";
    static int refreshIn;

    /// <summary>An unfinished station counts while its piece is at hand (11-12 owner: the piece was thrown away and the workbench still asked for it).
    /// With the piece gone the recipe is the full one again, and beginning it starts a new station.</summary>
    static bool Active(string id)
    {
        var job = Unfinished(id);
        return job != null && (working == id || job.Kg < 1f || Count(id + "Wip") > 0);
    }

    /// <summary>The crafting window, if it is open, shows the recipe as it stands now (it kept the hours of the last go until another recipe was chosen).</summary>
    static void RefreshPanel()
    {
        try
        {
            var panel = InterfaceManager.GetPanel<Panel_Crafting>();
            if (panel == null || !panel.IsEnabled()) return;
            // the chosen recipe is drawn again from its data: materials, tool and hours (0.4.25 refreshed only the list, the right side stayed as it was)
            var scroll = panel.m_ScrollBehaviour;
            if (scroll != null) panel.RefreshSelectedBlueprint(scroll.SelectedIndex);
            var rc = panel.m_RequirementContainer;
            if (rc != null) { rc.RefreshMaterialDisplay(); rc.RefreshCraftingTimeDisplay(); }
        }
        catch (Exception e) { Safe.Log("Crafting window refresh", e); }
    }
    static (string id, int frames)? sweep;
    static (string id, int frames)? wipSweep;

    /// <summary>The recipes as they stand for this world: a begun station asks for no materials and only for the time that is left.</summary>
    public static void Apply()
    {
        try
        {
            foreach (var r in Recipes)
            {
                var b = made.FirstOrDefault(m => m != null && m.name == NamePrefix + r.Id);
                if (b == null) continue;
                var job = Active(r.Id) ? Unfinished(r.Id) : null;
                float left = job == null ? 1f : Mathf.Clamp(1f - job.Done, .02f, 1f);
                b.m_DurationMinutes = Math.Max(1, (int)Math.Round(r.Hours * 60 * left));
                // a begun station asks for the unfinished piece alone
                bool begun = b.m_RequiredGear != null && b.m_RequiredGear.Length == 1 && r.Needs.Length != 1;
                if (job != null && !begun)
                {
                    var one = new Il2CppReferenceArray<BlueprintData.RequiredGearItem>(1L);
                    one[0] = new BlueprintData.RequiredGearItem { m_Item = Part(Defs.Prefix + r.Id + "Wip"), m_Count = 1, m_Units = BlueprintData.RequiredGearItem.Units.Count, m_Weight = ItemWeight.FromKilograms(0f) };
                    b.m_RequiredGear = one;
                }
                else if (job == null && begun) b.m_RequiredGear = Needs(r);
            }
        }
        catch (Exception e) { Safe.Log("Station recipes state", e); }
    }

    static int Count(string id)
    {
        int n = 0;
        var inv = GameManager.GetInventoryComponent();
        if (inv == null) return 0;
        foreach (var o in inv.m_Items)
        {
            var g = o?.m_GearItem;
            if (g != null && g.name == Defs.Prefix + id) n++;
        }
        return n;
    }

    public static void Tick()
    {
        try
        {
            if (!built) return;
            Saws();
            if (refreshIn > 0 && --refreshIn == 0) { Apply(); RefreshPanel(); }
            if (sweep is { } s)
            {
                // whatever the game handed over for the broken-off work goes away again
                var inv = GameManager.GetInventoryComponent();
                int keep = baseline.TryGetValue(s.id, out var k) ? k : 0;
                if (inv != null && Count(s.id) > keep)
                {
                    GearItem? extra = null;
                    foreach (var o in inv.m_Items)
                    {
                        var g = o?.m_GearItem;
                        if (g != null && g.name == Defs.Prefix + s.id) extra = g;      // the newest is the last
                    }
                    if (extra != null) { inv.DestroyGear(extra.gameObject); Safe.Info($"Unfinished {s.id}: the item of the broken-off work is taken back"); }
                }
                sweep = s.frames > 1 ? (s.id, s.frames - 1) : null;
                if (sweep == null && finished == s.id)
                {
                    working = null; finished = null;
                    var pm = GameManager.GetPlayerManagerComponent();
                    if (inv != null)
                        foreach (var o in inv.m_Items)
                        {
                            var g = o?.m_GearItem;
                            if (g != null && g.name == Defs.Prefix + s.id + "Wip") { inv.DestroyGear(g.gameObject); break; }
                        }
                    var made = pm?.InstantiateItemInPlayerInventory(Defs.Prefix + s.id, 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
                    Safe.Info($"Station {s.id}: finished, a clean one is given ({(made != null ? "ok" : "FAILED")})");
                    baseline[s.id] = Count(s.id);
                    Apply(); RefreshPanel();
                }
                else if (sweep == null)
                {
                    working = null;
                    // the unfinished piece lies in the pack: one, whether the game took the old one for this go or not
                    if (Unfinished(s.id) is { } uj)
                    {
                        if (Count(s.id + "Wip") == 0)
                            GameManager.GetPlayerManagerComponent()?.InstantiateItemInPlayerInventory(Defs.Prefix + s.id + "Wip", 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
                        uj.Kg = 1f;      // the piece was handed over
                        Barrels.SaveJobs();
                    }
                    Apply(); RefreshPanel();
                }
                return;
            }
            if (wipSweep is { } ws)
            {
                // a finished station leaves no unfinished piece behind
                var inv = GameManager.GetInventoryComponent();
                if (inv != null)
                    foreach (var o in inv.m_Items)
                    {
                        var g = o?.m_GearItem;
                        if (g != null && g.name == Defs.Prefix + ws.id + "Wip") { inv.DestroyGear(g.gameObject); break; }
                    }
                wipSweep = ws.frames > 1 ? (ws.id, ws.frames - 1) : null;
                return;
            }
            if (working != null) return;
            if (Time.frameCount % 30 != 0) return;
            Adopt();
            foreach (var r in Recipes) baseline[r.Id] = Count(r.Id);
            string state = "";
            foreach (var r in Recipes)
            {
                bool wip = Count(r.Id + "Wip") > 0;
                hadWip[r.Id] = wip;
                // a piece picked up again after its station was begun anew: it is an unfinished station all the same
                if (wip && Unfinished(r.Id) == null) { Barrels.Jobs.Add(new Job { Kind = JobPrefix + r.Id, Done = .02f, Kg = 1f }); Barrels.SaveJobs(); }
                state += Active(r.Id) ? "1" : "0";
            }
            if (state != shownState) { shownState = state; Apply(); RefreshPanel(); }
            // a station begun under 0.4.6, which kept no piece in the pack: it gets one now
            foreach (var r in Recipes)
                if (Unfinished(r.Id) is { Kg: < 1f } old && GameManager.GetPlayerManagerComponent() is { } pm && GameManager.GetInventoryComponent() != null)
                {
                    pm.InstantiateItemInPlayerInventory(Defs.Prefix + r.Id + "Wip", 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
                    old.Kg = 1f; Barrels.SaveJobs(); Apply();
                }
        }
        catch (Exception e) { Safe.Log("Station build tick", e); }
    }

    /// <summary>A station in the pack that the game itself marks as work in progress (left by 0.4.23 and older): it becomes our unfinished piece
    /// with the same share of the work done, so that it is not placed and is finished by the recipe.</summary>
    static void Adopt()
    {
        var inv = GameManager.GetInventoryComponent();
        var pm = GameManager.GetPlayerManagerComponent();
        if (inv == null || pm == null) return;
        foreach (var r in Recipes)
        {
            GearItem? found = null; float pct = 0f;
            foreach (var o in inv.m_Items)
            {
                var g = o?.m_GearItem;
                if (g == null || g.name != Defs.Prefix + r.Id) continue;
                var wip = g.m_InProgressCraftItem;
                if (wip == null) continue;
                float p = wip.GetPercentCompleteNormalized();
                if (p >= .995f) continue;
                found = g; pct = p; break;
            }
            if (found == null) continue;
            inv.DestroyGear(found.gameObject);
            var job = Unfinished(r.Id);
            if (job == null) { job = new Job { Kind = JobPrefix + r.Id }; Barrels.Jobs.Add(job); }
            job.Done = Mathf.Clamp(Math.Max(job.Done, pct), 0f, .98f);
            job.Kg = 1f;
            if (Count(r.Id + "Wip") == 0) pm.InstantiateItemInPlayerInventory(Defs.Prefix + r.Id + "Wip", 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
            Barrels.SaveJobs();
            Apply();
            MelonLogger.Msg($"Station {r.Id}: the game's own unfinished one ({pct:0.00}) is turned into our unfinished piece");
        }
    }

    static string? Ours(CraftingOperation op)
    {
        var bp = op?.Blueprint;
        if (bp == null || bp.name == null || !bp.name.StartsWith(NamePrefix, StringComparison.Ordinal)) return null;
        return bp.name.Substring(NamePrefix.Length);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(CraftingOperation), nameof(CraftingOperation.StartCrafting))]
    static void Started(CraftingOperation __instance)
    {
        try
        {
            var id = Ours(__instance);
            if (id == null) return;
            // the unfinished piece is not at hand: this is a new station, the old record goes
            if (Unfinished(id) is { Kg: >= 1f } stale && !(hadWip.TryGetValue(id, out var hw) && hw))
            {
                Barrels.Jobs.Remove(stale); Barrels.SaveJobs();
                Safe.Info($"Station {id}: begun anew, the unfinished piece is not in the pack");
            }
            working = id; workingOp = __instance.Pointer; ended = false; sweep = null;
            offeredHours = 0f;
            try
            {
                var panel = InterfaceManager.GetPanel<Panel_Crafting>();
                if (panel != null) offeredHours = panel.GetAdjustedCraftingTime() / 60f;
            }
            catch (Exception e) { Safe.Log("Station build offered time", e); }
            Safe.Info($"Station {id}: work begun, done before {(Unfinished(id)?.Done ?? 0f):0.00}, in the pack {(baseline.TryGetValue(id, out var n) ? n : 0)}");
        }
        catch (Exception e) { Safe.Log("Station build start", e); }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(CraftingOperation), nameof(CraftingOperation.HandleInterrupt))]
    static void Interrupted(CraftingOperation __instance) => Ended(__instance, false);

    [HarmonyPostfix, HarmonyPatch(typeof(CraftingOperation), nameof(CraftingOperation.HandleSuccess))]
    static void Succeeded(CraftingOperation __instance) => Ended(__instance, true);

    /// <summary>A go at the workbench is over, broken off or worked to its end. The game lets the player choose how many hours to work now and hands
    /// the station over after any of them (0.4.9 took a go worked to its end for a finished station), so the hours are counted here:
    /// the station is built only when all the recipe's hours are in it.</summary>
    static void Ended(CraftingOperation op, bool success)
    {
        try
        {
            var id = Ours(op);
            if (id == null || ended || op.Pointer != workingOp) return;
            ended = true;
            var r = Recipes.First(x => x.Id == id);
            float total = op.m_RealTimeDuration, spent = op.m_RealTimeElapsed, go = op.m_HoursToSpendCrafting;
            float part = success || total <= 0f ? 1f : Mathf.Clamp01(spent / total);
            var job = Unfinished(id);
            float before = job?.Done ?? 0f;
            float left = Math.Max(.01f, r.Hours * (1f - before));
            if (go <= 0f) go = left;
            // the share of what was left that this go did: by the hours the game itself offered, not by the recipe's
            float full = offeredHours > .01f ? offeredHours : left;
            float share = Mathf.Clamp01(go * part / full);
            float after = before + (1f - before) * share;
            MelonLogger.Msg($"Station {id}: go over ({(success ? "worked through" : "broken off")}), {spent:0.0}/{total:0.0} s, {go:0.00} h chosen of {full:0.00} h offered ({left:0.00} h by the recipe), done {before:0.00} -> {after:0.00}");
            if (share >= .97f)
            {
                if (job != null) { Barrels.Jobs.Remove(job); Barrels.SaveJobs(); }
                Apply();
                finished = id;
                sweep = (id, 90);
                refreshIn = 4;
                return;
            }
            if (job == null) { job = new Job { Kind = JobPrefix + id }; Barrels.Jobs.Add(job); }
            job.Done = Mathf.Clamp(after, 0f, .98f);
            Barrels.SaveJobs();
            Apply();
            sweep = (id, 90);
            refreshIn = 4;
            Barrels.Say("B_PAUSED", (int)Math.Round(job.Done * 100f));
        }
        catch (Exception e) { Safe.Log("Station build end", e); }
    }

    [HarmonyPrefix, HarmonyPatch(typeof(BlueprintData), nameof(BlueprintData.GetCraftingIcon))]
    static bool Icon(BlueprintData __instance, ref Texture2D __result)
    {
        try
        {
            if (__instance == null || __instance.name == null || !__instance.name.StartsWith(NamePrefix, StringComparison.Ordinal)) return true;
            var t = Assets.Texture("icons/" + __instance.name.Substring(NamePrefix.Length) + ".png", compress: false);
            if (t == null) return true;
            __result = t;
            return false;
        }
        catch (Exception e)
        {
            Safe.Log("Recipe icon", e);
            return true;
        }
    }
}
