using System;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(GearItem), "RollForFoodPoisoning")]
internal static class FoodPoisoningHook
{
	private const float RawMeat = 75f;

	private const float RawDrink = 10f;

	private static void Prefix(GearItem __instance, out float[]? __state)
	{
		__state = null;
		FoodItem foodItem = __instance.m_FoodItem;
		if (foodItem == null)
		{
			return;
		}
		float num = Fx.Less("StrongStomach");
		float percent;
		bool flag = DishWarming.IsUnfinished(__instance, out percent);
		if (!(num >= 1f) || flag)
		{
			__state = new float[3] { foodItem.m_ChanceFoodPoisoning, foodItem.m_ChanceFoodPoisoningLowCondition, foodItem.m_ChanceFoodPoisoningRuined };
			if (flag)
			{
				float val = (foodItem.m_IsDrink ? 10f : 75f) * (1f - Math.Clamp(percent, 0f, 1f));
				foodItem.m_ChanceFoodPoisoning = Math.Max(foodItem.m_ChanceFoodPoisoning, val);
				foodItem.m_ChanceFoodPoisoningLowCondition = Math.Max(foodItem.m_ChanceFoodPoisoningLowCondition, val);
				foodItem.m_ChanceFoodPoisoningRuined = Math.Max(foodItem.m_ChanceFoodPoisoningRuined, val);
			}
			foodItem.m_ChanceFoodPoisoning *= num;
			foodItem.m_ChanceFoodPoisoningLowCondition *= num;
			foodItem.m_ChanceFoodPoisoningRuined *= num;
		}
	}

	private static void Postfix(GearItem __instance, float[]? __state)
	{
		if (__state != null)
		{
			FoodItem foodItem = __instance.m_FoodItem;
			foodItem.m_ChanceFoodPoisoning = __state[0];
			foodItem.m_ChanceFoodPoisoningLowCondition = __state[1];
			foodItem.m_ChanceFoodPoisoningRuined = __state[2];
		}
	}
}
