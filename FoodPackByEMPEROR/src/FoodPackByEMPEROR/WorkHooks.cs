using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace FoodPackByEMPEROR;

[HarmonyPatch]
internal static class WorkHooks
{
	private static float WorkTime => Fx.Less("Diligence", "Flawless") * Fx.More("Distracted");

	private static int Scale(int minutes, float k)
	{
		if (k != 1f)
		{
			return Mathf.Max(1, Mathf.RoundToInt((float)minutes * k));
		}
		return minutes;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Panel_Crafting), "GetFinalCraftingTimeWithAllModifiers")]
	private static void Craft(ref int __result)
	{
		__result = Scale(__result, WorkTime);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Panel_Repair), "GetModifiedRepairDuration")]
	private static void Repair(ref int __result)
	{
		__result = Scale(__result, WorkTime);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Panel_Inventory_Examine), "GetModifiedActionDuration")]
	private static void ExamineWork(ref int __result)
	{
		__result = Scale(__result, WorkTime);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Repairable), "GetConditionIncreaseFromRepair")]
	private static void RepairGain(ref float __result)
	{
		__result *= Fx.More("Nimble");
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(ResearchItem), "Read")]
	private static void Read(ref float timeOfDayHours)
	{
		timeOfDayHours *= Fx.More("ClearHead") * Fx.Less("Distracted");
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Panel_BodyHarvest), "GetHarvestDurationMinutes")]
	private static void HarvestShown(ref float __result)
	{
		__result *= Fx.Less("HotSip", "Flawless");
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Panel_BodyHarvest), "StartHarvest")]
	private static void HarvestStart(ref int durationMinutes)
	{
		durationMinutes = Scale(durationMinutes, Fx.Less("HotSip", "Flawless"));
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(FireManager), "CalculateFireStartSuccess")]
	private static void Fire(ref float __result)
	{
		__result = Mathf.Min(100f, __result * (1f + Fx.V2("HotSip")));
		if (Fx.On("IrishLuck"))
		{
			__result = 100f - (100f - __result) * (1f - Fx.V("IrishLuck"));
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(CookingPotItem), "ModifiedCookTimeMinutes")]
	private static void Cook(ref float __result)
	{
		__result *= Fx.Less("Flawless");
	}
}
