using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using Il2CppTLD.Cooking;
using MelonLoader;

namespace FoodPackBySeagull;

[HarmonyPatch]
internal static class RecipePanelProbe
{
	private static readonly HashSet<string> told = new HashSet<string>();

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(Panel_Cooking), "UpdateRecipeContent")]
	private static Exception? Content(Exception? __exception, RecipeData __0)
	{
		return Tell("content", __exception, __0);
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(Panel_Cooking), "UpdateRecipeIngredients")]
	private static Exception? Ingredients(Exception? __exception, RecipeData __0)
	{
		return Tell("ingredients", __exception, __0);
	}

	private static Exception? Tell(string what, Exception? e, RecipeData r)
	{
		try
		{
			if (Data.Settings.TestKeys && r != null && r.name.StartsWith("RECIPE_Seagull_", StringComparison.Ordinal) && told.Add(what + r.name + (e != null)))
			{
				MelonLogger.Msg($"Recipe panel {what} for {r.name}: {((e == null) ? "done" : (e.GetType().Name + ": " + e.Message))}");
			}
		}
		catch
		{
		}
		return e;
	}
}
