using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch]
internal static class ToolWearGuard
{
	private static bool decaying;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(GearItem), "DecayOverTODHours")]
	private static void DecayBefore()
	{
		decaying = true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(GearItem), "DecayOverTODHours")]
	private static void DecayAfter()
	{
		decaying = false;
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(GearItem), "DecayOverTODHours")]
	private static Exception? DecayAlways(Exception? __exception)
	{
		decaying = false;
		return __exception;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(GearItem), "Degrade")]
	private static void Wear(GearItem __instance, ref float hp)
	{
		try
		{
			if (!decaying && !(hp <= 0f) && Fx.On("IrishLuck") && __instance.m_InPlayerInventory && IsTool(__instance))
			{
				hp *= 1f - Fx.V2("IrishLuck");
			}
		}
		catch (Exception e)
		{
			Safe.Log("Tool wear", e);
		}
	}

	private static bool IsTool(GearItem g)
	{
		ToolsItem toolsItem = g.m_ToolsItem;
		if (toolsItem != null && (toolsItem.m_CuttingToolType == ToolsItem.CuttingToolType.Knife || toolsItem.m_CuttingToolType == ToolsItem.CuttingToolType.Hatchet))
		{
			return true;
		}
		return g.name.StartsWith("GEAR_Prybar", StringComparison.Ordinal);
	}
}
