using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace MarkersBySeagull;

[HarmonyPatch(typeof(PlayerManager), "InstantiateItemInPlayerInventory", new Type[]
{
	typeof(string),
	typeof(int),
	typeof(float),
	typeof(PlayerManager.InventoryInstantiateFlags)
})]
internal static class AddToInventoryPatch
{
	private static bool Prefix(PlayerManager __instance, string name, int units, float normalizedCondition, PlayerManager.InventoryInstantiateFlags flags, ref GearItem __result)
	{
		if (!Marker.Is(name))
		{
			return true;
		}
		GearItem gearItem = GearItem.InstantiateGearItem("GEAR_SeagullMarker_TrailMarker");
		if (gearItem == null)
		{
			return false;
		}
		__result = __instance.InstantiateItemInPlayerInventoryFromPrefab(gearItem, Math.Max(1, units), normalizedCondition, flags);
		UnityEngine.Object.Destroy(gearItem.gameObject);
		return false;
	}
}
