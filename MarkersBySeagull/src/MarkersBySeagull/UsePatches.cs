using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace MarkersBySeagull;

[HarmonyPatch]
internal static class UsePatches
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(Panel_Inventory), "OnEquip")]
	private static bool Use(Panel_Inventory __instance)
	{
		try
		{
			GearItem gearItem = __instance.GetCurrentlySelectedItem()?.m_GearItem;
			if (gearItem == null || !Marker.Is(gearItem.name))
			{
				return true;
			}
			GearItem gearItem2 = gearItem.Drop(1, playSound: false);
			if (gearItem2 == null)
			{
				gearItem2 = gearItem;
			}
			Plant.ViaPlace.Add(gearItem2.GetInstanceID());
			Hud.Track(gearItem2);
			__instance.Enable(enable: false);
			PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
			if (playerManagerComponent == null || !playerManagerComponent.StartPlaceMesh(gearItem2.gameObject, PlaceMeshFlags.None))
			{
				MelonLogger.Msg("Marker: placement could not start, left at the feet.");
			}
			return false;
		}
		catch (Exception e)
		{
			Safe.Log("Use marker", e);
			return true;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(GearItem), "Drop")]
	private static void Dropped(GearItem __instance, GearItem __result)
	{
		try
		{
			GearItem[] array = new GearItem[2] { __result, __instance };
			foreach (GearItem gearItem in array)
			{
				if (gearItem != null && Marker.Is(gearItem.name) && !gearItem.m_InPlayerInventory)
				{
					Plant.ViaPlace.Remove(gearItem.GetInstanceID());
					Hud.Track(gearItem);
					Plant.Maybe(gearItem);
				}
			}
		}
		catch (Exception e)
		{
			Safe.Log("Drop marker", e);
		}
	}
}
