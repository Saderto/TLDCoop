using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.Cooking;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(RecipeItem), "Unlock")]
internal static class CardPatch
{
	private static void Prefix(RecipeItem __instance)
	{
		try
		{
			GearItem component = __instance.GetComponent<GearItem>();
			if (component == null || !Items.TryGet(component.name, out ItemDef def) || def.CardFor == null)
			{
				return;
			}
			List<RecipeData> list = new List<RecipeData>();
			foreach (RecipeData item in Recipes.ForDish(def.CardFor))
			{
				list.Add(item);
			}
			__instance.m_Recipes = list;
		}
		catch (Exception e)
		{
			Safe.Log("Recipe card", e);
		}
	}
}
