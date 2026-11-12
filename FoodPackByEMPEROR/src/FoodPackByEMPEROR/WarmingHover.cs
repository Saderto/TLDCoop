using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(CookingPotItem), "GetHoverText")]
internal static class WarmingHover
{
	private static void Postfix(CookingPotItem __instance, ref string __result)
	{
		try
		{
			GearItem gearItemBeingCooked = __instance.m_GearItemBeingCooked;
			CookingPotItem.CookingState cookingState = __instance.GetCookingState();
			ItemDef def;
			bool flag = cookingState == CookingPotItem.CookingState.Ready && Recipes.IsCookedDish(Items.TryGet(gearItemBeingCooked?.name, out def) ? def.Id : "");
			if (__result != null && gearItemBeingCooked != null && cookingState != CookingPotItem.CookingState.Ruined && (flag || DishWarming.Made(gearItemBeingCooked)))
			{
				__result = __result.Replace(Localization.Get("GAMEPLAY_Warming"), Localization.Get((cookingState == CookingPotItem.CookingState.Cooking) ? "GAMEPLAY_CookingProgress" : "GAMEPLAY_Cooked"));
			}
		}
		catch (Exception e)
		{
			Safe.Log("Dish warming", e);
		}
	}
}
