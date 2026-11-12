namespace SmokehouseByEMPEROR;

/// <summary>The barrel in the pack has a «Поставить» button (the game's equip button, as the cigarettes' «Закурить»): the pack closes and the
/// game's own placement starts. Dropped the plain way it stands upright; a standing barrel does not let the player walk through it.</summary>
[HarmonyPatch]
internal static class Place
{
    static GearItem? wanted, placing;
    static int wait;
    static readonly List<(GearItem g, Quaternion rot, int frames)> upright = new();
    static readonly List<GearItem> near = new();
    static float nextLook;
    static CharacterController? cc;
    const float Gap = .62f;

    [HarmonyPostfix, HarmonyPatch(typeof(ItemDescriptionPage), nameof(ItemDescriptionPage.UpdateButtons))]
    static void Buttons(ItemDescriptionPage __instance, GearItem gi)
    {
        try
        {
            if (!Defs.IsBarrel(gi)) return;
            if (__instance.m_MouseButtonEquip != null) __instance.m_MouseButtonEquip.SetActive(true);
            if (__instance.m_Label_MouseButtonEquip != null) __instance.m_Label_MouseButtonEquip.text = Texts.Get("ACT_PLACE");
            if (__instance.m_UIButton_ButtonEquip != null)
            {
                __instance.m_UIButton_ButtonEquip.gameObject.SetActive(true);
                __instance.m_UIButton_ButtonEquip.isEnabled = true;
            }
        }
        catch (Exception e) { Safe.Log("Inventory button", e); }
    }

    [HarmonyPrefix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(Panel_Inventory), nameof(Panel_Inventory.OnEquip))]
    static bool Equip(Panel_Inventory __instance)
    {
        GearItem? gi = null;
        try { gi = __instance.GetCurrentlySelectedItem()?.m_GearItem; }
        catch (Exception e) { Safe.Log("Selected item", e); }
        if (!Defs.IsBarrel(gi)) return true;
        try
        {
            Safe.Info("Inventory button: place the barrel");
            wanted = gi;
            wait = 12;
            __instance.Enable(false);
        }
        catch (Exception e) { Safe.Log("Place from the inventory", e); }
        return false;
    }

    [HarmonyPostfix, HarmonyPriority(Priority.First), HarmonyPatch(typeof(GearItem), nameof(GearItem.Drop))]
    static void Dropped(GearItem __instance, GearItem __result)
    {
        try
        {
            var g = __result != null ? __result : __instance;
            if (!Defs.IsBarrel(g)) return;
            float yaw = GameManager.GetPlayerTransform().eulerAngles.y + 180f;      // the firebox door towards the player
            var rot = Quaternion.Euler(0f, yaw, 0f);
            g.transform.rotation = rot;
            upright.Add((g, rot, 3));
        }
        catch (Exception e) { Safe.Log("Barrel drop", e); }
    }

    /// <summary>The barrel is never an item held in the hands (a pick-up of the game's may have put it there).</summary>
    public static void OutOfHands(PlayerManager pm, GearItem g)
    {
        try
        {
            var held = pm.m_ItemInHands;
            if (held == null || held.Pointer != g.Pointer) return;
            pm.UnequipItemInHandsSkipAnimation();
            if (pm.m_ItemInHands != null && pm.m_ItemInHands.Pointer == g.Pointer) pm.m_ItemInHands = null;
            Safe.Info("Barrel taken out of the hands");
        }
        catch (Exception e) { Safe.Log("Barrel out of the hands", e); }
    }

    /// <summary>Every frame.</summary>
    public static void Tick()
    {
        var pm = GameManager.GetPlayerManagerComponent();
        if (pm == null) return;
        // the click on the pack's button must be over (button up and a moment more), or the same click confirms the placement at once, at the feet
        if (wanted != null && (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0))) wait = 12;
        if (wanted != null && --wait <= 0)
        {
            var g = wanted;
            wanted = null;
            bool started = false;
            try
            {
                if (g != null && g.gameObject != null)
                {
                    OutOfHands(pm, g);
                    started = pm.StartPlaceMesh(g.gameObject, PlaceMeshFlags.UpdateInventoryOnSuccess);
                }
            }
            catch (Exception e) { Safe.Log("Barrel placement", e); }
            Safe.Info("Barrel placement " + (started ? "started" : "not started"));
            placing = started ? g : null;
            if (!started) Barrels.Say("MSG_NO_PLACE");
        }
        for (int i = upright.Count - 1; i >= 0; i--)
        {
            var u = upright[i];
            bool gone = true;
            try { gone = u.g == null || u.g.gameObject == null || u.g.m_InPlayerInventory; } catch { }
            if (gone || u.frames <= 0) { upright.RemoveAt(i); continue; }
            try { u.g.transform.rotation = u.rot; } catch { }
            upright[i] = (u.g, u.rot, u.frames - 1);
        }
        if (placing != null && !pm.IsInPlacementMode())
        {
            try
            {
                var p = placing.transform.position; var me = GameManager.GetPlayerTransform().position;
                Safe.Info($"Barrel placement over: in pack {placing.m_InPlayerInventory}, {Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(me.x, 0f, me.z)):0.00} m from the player, height {p.y - me.y:0.00}");
            }
            catch { }
            placing = null;
        }
        try { Push(pm); } catch (Exception e) { Safe.Log("Barrel push", e); }
    }

    /// <summary>A gear item has no collision with the player, so a standing barrel moves the player out of itself (walls are respected: it goes
    /// through the character controller), the way the coop mod keeps players out of each other.</summary>
    static void Push(PlayerManager pm)
    {
        var t = GameManager.GetPlayerTransform();
        if (t == null) return;
        var me = t.position;
        if (Time.unscaledTime >= nextLook)
        {
            nextLook = Time.unscaledTime + .5f;
            try { var held = pm.m_ItemInHands; if (held != null && Defs.IsBarrel(held)) OutOfHands(pm, held); } catch { }
            near.Clear();
            foreach (var c in Physics.OverlapSphere(me + Vector3.up * .5f, 2.5f))
            {
                var g = c != null ? c.GetComponentInParent<GearItem>() : null;
                if (g != null && Defs.IsBarrel(g) && !g.m_InPlayerInventory && !near.Any(n => n.Pointer == g.Pointer)) near.Add(g);
            }
        }
        if (near.Count == 0 || pm.IsInPlacementMode() || Window.IsOpen) return;
        var push = Vector3.zero;
        foreach (var g in near)
        {
            bool ok = false;
            try { ok = g != null && g.gameObject != null && g.gameObject.activeInHierarchy && !g.m_InPlayerInventory; } catch { }
            if (!ok) continue;
            var p = g!.transform.position;
            if (me.y - p.y > 1.3f || p.y - me.y > 1.6f) continue;
            var d = new Vector3(me.x - p.x, 0f, me.z - p.z);
            float dist = d.magnitude;
            if (dist >= Gap) continue;
            var dir = dist > .01f ? d / dist : -t.forward;
            push += dir * (Gap - dist);
        }
        if (push.sqrMagnitude < 1e-6f) return;
        push = Vector3.ClampMagnitude(push, .08f);
        if (cc == null || cc.transform != t) cc = t.GetComponent<CharacterController>() ?? t.GetComponentInParent<CharacterController>();
        if (cc != null && cc.enabled) cc.Move(push);
        else t.position += push;
    }
}
