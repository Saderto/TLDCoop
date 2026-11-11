using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(GearItem), "InstantiateAndDeserializeGearItem", new Type[]
{
	typeof(string),
	typeof(GearItemSaveDataProxy),
	typeof(Transform),
	typeof(bool),
	typeof(bool)
})]
internal static class LoadFromProxyPatch
{
	private static void Prefix(ref string prefabName, out ItemDef? __state)
	{
		__state = (Items.TryGet(prefabName, out ItemDef def) ? def : null);
		if (__state != null)
		{
			prefabName = Items.BaseOf(__state);
		}
	}

	private static void Postfix(GearItem __result, ItemDef? __state)
	{
		if (__state == null || __result == null)
		{
			return;
		}
		try
		{
			Items.Customize(__result, __state, restored: true);
		}
		catch (Exception e)
		{
			Safe.Log("Saved item " + __state.Id, e);
		}
	}
}
