using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(CookingPotItem), "PickUpCookedGearItem")]
internal static class UnfinishedTakenOut
{
	private static void Prefix(CookingPotItem __instance, out (GearItem? g, float percent) __state)
	{
		__state = (g: null, percent: 0f);
		try
		{
			GearItem gearItemBeingCooked = __instance.m_GearItemBeingCooked;
			if (gearItemBeingCooked != null && __instance.GetCookingState() == CookingPotItem.CookingState.Cooking && DishWarming.Made(gearItemBeingCooked))
			{
				__state = (g: gearItemBeingCooked, percent: __instance.m_PercentCooked);
			}
		}
		catch (Exception e)
		{
			Safe.Log("Unfinished dish", e);
		}
	}

	private static void Postfix((GearItem? g, float percent) __state)
	{
		try
		{
			if (__state.g != null)
			{
				DishWarming.TakenOutUnfinished(__state.g, __state.percent);
			}
		}
		catch (Exception e)
		{
			Safe.Log("Unfinished dish", e);
		}
	}
}
