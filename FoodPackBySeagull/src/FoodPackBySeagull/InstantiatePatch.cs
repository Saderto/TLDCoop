using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(GearItem), "InstantiateGearItem", new Type[] { typeof(string) })]
internal static class InstantiatePatch
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
		if (__state == null)
		{
			return;
		}
		if (__result == null)
		{
			MelonLogger.Error(__state.Id + " skipped: prefab " + __state.BasePrefab + " not found. The mod needs all DLC.");
			return;
		}
		try
		{
			Items.Customize(__result, __state, restored: false);
		}
		catch (Exception e)
		{
			Safe.Log("Item " + __state.Id, e);
		}
	}
}
