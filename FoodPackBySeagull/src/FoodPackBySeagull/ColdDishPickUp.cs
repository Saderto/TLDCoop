using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(CookingPotItem), "PickUpCookedGearItem")]
internal static class ColdDishPickUp
{
	private static void Prefix(CookingPotItem __instance, out GearItem? __state)
	{
		__state = __instance.m_GearItemBeingCooked;
	}

	private static void Postfix(GearItem? __state)
	{
		try
		{
			if (!(__state == null) && !(__state.m_FoodItem == null) && Items.TryGet(__state.name, out ItemDef def) && def.Cold)
			{
				__state.m_FoodItem.m_HeatPercent = 0f;
			}
		}
		catch (Exception e)
		{
			Safe.Log("Cold dish pick-up", e);
		}
	}
}
