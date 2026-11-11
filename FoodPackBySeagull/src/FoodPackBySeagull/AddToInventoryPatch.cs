using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace FoodPackBySeagull;

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
		if (!Items.TryGet(name, out ItemDef _))
		{
			return true;
		}
		GearItem gearItem = Items.Spawn(name);
		if (gearItem == null)
		{
			return false;
		}
		if (units <= 1)
		{
			StackableItem component = gearItem.GetComponent<StackableItem>();
			if ((object)component != null)
			{
				units = Math.Max(1, component.m_DefaultUnitsInItem);
			}
		}
		__result = __instance.InstantiateItemInPlayerInventoryFromPrefab(gearItem, units, normalizedCondition, flags);
		UnityEngine.Object.Destroy(gearItem.gameObject);
		return false;
	}
}
