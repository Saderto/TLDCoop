using Il2Cpp;
using Il2CppTLD.Gear;
using MelonLoader;
using UnityEngine;

namespace FoodPackByEMPEROR;

public static class GameEffects
{
	private static HeadacheData? dish;

	private static HeadacheData? current;

	public static bool Hurting(float now)
	{
		if (Persist.State.HeadacheEnds > now)
		{
			return GameManager.GetHeadacheComponent().HasHeadache();
		}
		return false;
	}

	public static void Headache(float hours, float fullHours)
	{
		if (dish == null)
		{
			GearItem gearItem = GearItem.InstantiateGearItem("GEAR_CookedPiePeach");
			dish = ((!(gearItem != null)) ? null : gearItem.GetComponent<CausesHeadacheDebuff>()?.m_HeadacheSettings);
			if (gearItem != null)
			{
				Object.Destroy(gearItem.gameObject);
			}
			if (dish == null)
			{
				MelonLogger.Error("No headache: GEAR_CookedPiePeach or its headache data not found.");
				return;
			}
		}
		Headache headacheComponent = GameManager.GetHeadacheComponent();
		float hoursPlayedNotPaused = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
		hours = EffectMath.HeadacheHours(hours, Hurting(hoursPlayedNotPaused) ? (Persist.State.HeadacheEnds - hoursPlayedNotPaused) : 0f, fullHours);
		if (current != null)
		{
			headacheComponent.RemoveHeadache(current);
		}
		current = Object.Instantiate(dish);
		current.hideFlags = HideFlags.HideAndDontSave;
		current.m_HeadacheDurationHours = hours;
		Persist.State.HeadacheEnds = hoursPlayedNotPaused + hours;
		headacheComponent.ApplyHeadache(current);
		MelonLogger.Msg($"Headache applied for {hours:0.00} h: active {headacheComponent.HasHeadache()}, count {headacheComponent.GetAfflictionsCount()}");
	}
}
