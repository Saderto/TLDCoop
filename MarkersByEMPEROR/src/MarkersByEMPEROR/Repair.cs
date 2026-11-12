using System;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.Gear;
using MelonLoader;
using UnityEngine;

namespace MarkersByEMPEROR;

internal static class Repair
{
	public static int GearLayer = 17;

	public static void Maybe(GearItem g)
	{
		try
		{
			if (!g.gameObject.activeInHierarchy || g.gameObject.layer != 2 || Cursor.visible)
			{
				return;
			}
			PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
			if ((playerManagerComponent != null && playerManagerComponent.IsInspectModeActive() && playerManagerComponent.GearItemBeingInspected() == g) || (playerManagerComponent != null && playerManagerComponent.IsInMeshPlacementMode() && playerManagerComponent.GetObjectToPlace() == g.gameObject))
			{
				return;
			}
			Inventory inventoryComponent = GameManager.GetInventoryComponent();
			bool flag = false;
			if (inventoryComponent != null)
			{
				List<GearItemObject>.Enumerator enumerator = inventoryComponent.m_Items.GetEnumerator();
				while (enumerator.MoveNext())
				{
					GearItemObject current = enumerator.Current;
					if (current != null && current.m_GearItem == g)
					{
						flag = true;
						break;
					}
				}
			}
			if (g.m_InPlayerInventory && !flag)
			{
				g.m_InPlayerInventory = false;
				MelonLogger.Msg("Marker repaired: was flagged as in the pack.");
			}
			if (g.m_InPlayerInventory && flag)
			{
				inventoryComponent.RemoveGear(g.gameObject);
				g.m_InPlayerInventory = false;
				MelonLogger.Msg("Marker repaired: was in the world and in the pack at once.");
			}
			foreach (Transform componentsInChild in g.GetComponentsInChildren<Transform>(includeInactive: true))
			{
				if (componentsInChild.gameObject.layer == 2)
				{
					componentsInChild.gameObject.layer = GearLayer;
				}
			}
		}
		catch (Exception e)
		{
			Safe.Log("Repair", e);
		}
	}
}
