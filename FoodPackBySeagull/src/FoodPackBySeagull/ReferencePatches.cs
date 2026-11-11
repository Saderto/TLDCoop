using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace FoodPackBySeagull;

[HarmonyPatch]
internal static class ReferencePatches
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GearItem), "InstantiateGearItem", new Type[] { typeof(AssetReference) })]
	private static bool Spawn(AssetReference assetReference, ref GearItem __result)
	{
		if (!Items.FromReference(assetReference, out string name))
		{
			return true;
		}
		__result = GearItem.InstantiateGearItem(name);
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(GearItem), "InstantiateAndDeserializeGearItem", new Type[]
	{
		typeof(AssetReference),
		typeof(string),
		typeof(Transform),
		typeof(bool),
		typeof(bool)
	})]
	private static bool LoadText(AssetReference assetReference, string serializedGearItem, Transform parent, bool applyPositioningFix, bool instantiatedInContainer, ref GearItem __result)
	{
		if (!Items.FromReference(assetReference, out string name))
		{
			return true;
		}
		__result = GearItem.InstantiateAndDeserializeGearItem(name, serializedGearItem, parent, applyPositioningFix, instantiatedInContainer);
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(GearItem), "InstantiateAndDeserializeGearItem", new Type[]
	{
		typeof(AssetReference),
		typeof(GearItemSaveDataProxy),
		typeof(Transform),
		typeof(bool),
		typeof(bool)
	})]
	private static bool LoadProxy(AssetReference assetReference, GearItemSaveDataProxy proxy, Transform parent, bool applyPositioningFix, bool instantiatedInContainer, ref GearItem __result)
	{
		if (!Items.FromReference(assetReference, out string name))
		{
			return true;
		}
		__result = GearItem.InstantiateAndDeserializeGearItem(name, proxy, parent, applyPositioningFix, instantiatedInContainer);
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PlayerManager), "InstantiateItemInPlayerInventory", new Type[]
	{
		typeof(AssetReferenceGearItem),
		typeof(int),
		typeof(float),
		typeof(PlayerManager.InventoryInstantiateFlags)
	})]
	private static bool Inventory(PlayerManager __instance, AssetReferenceGearItem assetReference, int units, float normalizedCondition, PlayerManager.InventoryInstantiateFlags flags, ref GearItem __result)
	{
		if (!Items.FromReference((AssetReference?)(object)assetReference, out string name))
		{
			return true;
		}
		__result = __instance.InstantiateItemInPlayerInventory(name, units, normalizedCondition, flags);
		return false;
	}
}
