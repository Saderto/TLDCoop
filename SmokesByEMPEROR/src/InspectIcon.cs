namespace SmokesByEMPEROR;

/// <summary>11-10: lighter fluid and the butane can are built on the painkiller bottle, so the inspect view showed the first aid cross
/// over their name (11-10: the cigarettes, cigars, pipes and tobacco too, all on that base). While one of them is inspected, the first aid icon is swapped for the fire starting one (the same the lighters get).</summary>
internal static class InspectIcon
{
    static bool logged;
    const string FireIcon = "ico_inv_Materials";
    const string TobaccoIcon = "ico_collections_matchbooks";   // the same picture as our «Табак» inventory tab

    public static void Tick()
    {
        var pm = GameManager.GetPlayerManagerComponent();
        if (pm == null || !pm.IsInspectModeActive()) return;
        var gi = pm.GearItemBeingInspected();
        if (gi == null || !Defs.Is(gi, out var def) || def.Kind == Kind.Lighter) return;   // lighters are on the fire striker base and already show fire
        var hud = InterfaceManager.GetPanel<Panel_HUD>();
        var icons = hud?.m_InspectModeItemTypeIcons;
        if (icons == null) return;
        if (!logged)
        {
            logged = true;
            var names = new List<string>();
            foreach (var s in icons) if (s != null) names.Add($"{s.spriteName}{(s.gameObject.activeSelf ? "*" : "")}");
            Safe.Info("Inspect type icons: " + string.Join(", ", names));
        }
        // log 11-10: the slots are «ico_Radial_firstAid», «ico_inv_Materials»…; the fire starting icon is «ico_inv_Materials»
        foreach (var s in icons)
        {
            if (s == null) continue;
            if ((s.spriteName ?? "").IndexOf("firstAid", StringComparison.OrdinalIgnoreCase) >= 0) {
                string want = def.Kind == Kind.Refill ? FireIcon : TobaccoIcon;
                if (want != FireIcon && s.atlas != null && s.atlas.GetSprite(want) == null) want = FireIcon;   // not in the HUD atlas
                s.spriteName = want;
            }
        }
    }
}
