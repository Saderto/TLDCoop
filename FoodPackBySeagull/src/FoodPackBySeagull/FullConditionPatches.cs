using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch]
internal static class FullConditionPatches
{
	private static void After(GearItem g)
	{
		if (g != null && Items.TryGet(g.name, out ItemDef def) && def.FullCondition)
		{
			Items.Full(g);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(GearItem), "RollGearCondition")]
	private static void Roll(GearItem __instance)
	{
		After(__instance);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(GearItem), "RollGearConditionAndWeight")]
	private static void RollWeight(GearItem __instance)
	{
		After(__instance);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(GearItem), "RollSeededGearConditionAndWeight")]
	private static void RollSeeded(GearItem __instance)
	{
		After(__instance);
	}
}
