using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Gear;
using UnityEngine;

namespace MarkersBySeagull;

[HarmonyPatch]
internal static class IconPatches
{
	private static Texture2D Icon(string name)
	{
		if (!Marker.Is(name))
		{
			return null;
		}
		return Marker.Icon();
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(GearItem), "GetInventoryIconTexture")]
	private static void OfItem(GearItem __instance, ref Texture2D __result)
	{
		__result = Icon(__instance.name) ?? __result;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Utils), "GetInventoryIconTexture", new Type[] { typeof(GearItem) })]
	private static void ByItem(GearItem gi, ref Texture2D __result)
	{
		if (gi != null)
		{
			__result = Icon(gi.name) ?? __result;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Utils), "GetInventoryIconTexture", new Type[]
	{
		typeof(GearItem),
		typeof(float)
	})]
	private static void ByItemCondition(GearItem gi, ref Texture2D __result)
	{
		if (gi != null)
		{
			__result = Icon(gi.name) ?? __result;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Utils), "GetInventoryIconTexture", new Type[]
	{
		typeof(GearItemData),
		typeof(float)
	})]
	private static void ByData(GearItemData gearItemData, ref Texture2D __result)
	{
		if (gearItemData != null)
		{
			__result = Icon(gearItemData.name) ?? __result;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Utils), "GetInventoryGridIconTexture")]
	private static void Grid(string name, ref Texture2D __result)
	{
		__result = Icon(name) ?? __result;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Utils), "GetInventoryGridIconTextureNonNull")]
	private static void GridNonNull(string name, ref Texture2D __result)
	{
		__result = Icon(name) ?? __result;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Utils), "GetInventoryIconTextureFromPrefabName")]
	private static void ByName(string prefabName, ref Texture2D __result)
	{
		__result = Icon(prefabName) ?? __result;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(BlueprintData), "GetCraftingIcon")]
	private static bool Blueprint(BlueprintData __instance, ref Texture2D __result)
	{
		if (__instance == null || __instance != Craft.Blueprint)
		{
			return true;
		}
		__result = Marker.Icon();
		return __result == null;
	}
}
