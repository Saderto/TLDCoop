namespace SmokesByEMPEROR;

/// <summary>11-09: Ctrl+Shift+B gives a cigar box and starts the game's own placement, so the player sets it where he likes.
/// Each press in a world gives a box not given before in that world (the list below, then round again); the usual loot boxes stay.</summary>
internal static class Boxes
{
    // the order the boxes come in: the famous houses first, then the rest of the catalogue
    static readonly string[] First = { "Cohibra", "Montecrusto", "Partagaz", "Romero", "Hoyo", "Padrone", "Arturo", "Bolivarr", "Uppmann", "Trinidud" };

    static string File => Path.Combine(Mod.Root, "boxes_placed.txt");
    static List<string>? given;

    static List<string> Order()
    {
        var all = new List<string>();
        foreach (var k in First) if (Defs.ById("Box" + k) != null) all.Add("Box" + k);
        foreach (var d in Defs.All) if (d.Kind == Kind.Box && !all.Contains(d.Id)) all.Add(d.Id);
        return all;
    }

    static List<string> Given()
    {
        if (given != null) return given;
        given = new();
        try { if (System.IO.File.Exists(File)) foreach (var l in System.IO.File.ReadAllLines(File)) if (l.Length > 0) given.Add(l.Trim()); } catch { }
        return given;
    }

    public static void Tick()
    {
        if (!Settings.PlaceBoxKey.Value) return;
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (!ctrl || !shift || !Input.GetKeyDown(KeyCode.B)) return;
        try { Place(); } catch (Exception e) { Safe.Log("Place a cigar box", e); }
    }

    static void Place()
    {
        var pm = GameManager.GetPlayerManagerComponent();
        if (pm == null) return;
        try { if (InterfaceManager.IsOverlayActiveImmediate() || pm.IsInPlacementMode()) return; } catch { }
        string save = "";
        try { save = SaveGameSystem.GetCurrentSaveName() ?? ""; } catch { }
        if (string.IsNullOrEmpty(save)) return;
        string world = PipeSpots.WorldKey(save);
        var order = Order();
        if (order.Count == 0) return;
        var mine = Given().Where(l => l.StartsWith(world + "|", StringComparison.Ordinal)).Select(l => l.Substring(world.Length + 1)).ToList();
        string? id = order.FirstOrDefault(b => !mine.Contains(b));
        if (id == null) id = order[mine.Count % order.Count];   // every box given once: round again
        var gi = pm.InstantiateItemInPlayerInventory(Defs.Prefix + id, 1, 1f, PlayerManager.InventoryInstantiateFlags.None);
        if (gi == null) { Safe.Info($"Cigar box {id}: could not be made"); return; }
        string line = world + "|" + id;
        Given().Add(line);
        try { System.IO.File.AppendAllText(File, line + Environment.NewLine); } catch (Exception e) { Safe.Log("Cigar boxes file", e); }
        bool placing = false;
        try { placing = pm.StartPlaceMesh(gi.gameObject, PlaceMeshFlags.UpdateInventoryOnSuccess); } catch (Exception e) { Safe.Log("Box placement", e); }
        Safe.Info($"Cigar box {id} given ({mine.Count + 1} in this world), placement {(placing ? "started" : "not started: it stays in the pack")}");
        try { HUDMessage.AddMessage(Texts.Get(placing ? "SMOKES_MSG_BOX_PLACE" : "SMOKES_MSG_BOX_PACK", Texts.Get("SMOKES_" + id + "_NAME")), false, true); } catch { }
    }
}
