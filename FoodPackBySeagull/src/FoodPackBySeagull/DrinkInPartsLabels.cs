using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch]
internal static class DrinkInPartsLabels
{
	private static bool asDrink;

	private static bool InParts(GearItem? g)
	{
		if (g != null && Items.TryGet(g.name, out ItemDef def))
		{
			return def.InParts;
		}
		return false;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(ItemDescriptionPage), "GetEquipButtonLocalizationId")]
	private static void Button(GearItem gi, ref string __result)
	{
		if (__result == "GAMEPLAY_Eat" && InParts(gi))
		{
			__result = "GAMEPLAY_Drink";
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Panel_ActionsRadial), "GetActionText")]
	private static void Radial(RadialMenuArm arm, ref string __result)
	{
		try
		{
			if (arm != null && InParts(arm.m_GearItem) && __result == Localization.Get("GAMEPLAY_Eat"))
			{
				__result = Localization.Get("GAMEPLAY_Drink");
			}
		}
		catch (Exception e)
		{
			Safe.Log("Radial label", e);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(PlayerManager), "InitLabelsForGear")]
	private static void Before(PlayerManager __instance)
	{
		asDrink = InParts(__instance.m_Gear);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(PlayerManager), "InitLabelsForGear")]
	private static void After()
	{
		asDrink = false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Localization), "Get", new Type[] { typeof(string) })]
	private static void Key(ref string key)
	{
		if (asDrink && key == "GAMEPLAY_EatIt")
		{
			key = "GAMEPLAY_DrinkIt";
		}
	}
}
