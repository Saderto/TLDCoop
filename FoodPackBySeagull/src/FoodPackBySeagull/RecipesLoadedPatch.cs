using System;
using HarmonyLib;
using Il2CppTLD.Cooking;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(RecipeBook), "OnRecipesLoaded")]
internal static class RecipesLoadedPatch
{
	private static void Postfix(RecipeBook __instance)
	{
		try
		{
			Recipes.Later(__instance);
		}
		catch (Exception e)
		{
			Safe.Log("Recipes", e);
		}
	}
}
