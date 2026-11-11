using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace FoodPackBySeagull;

public static class InstantEffects
{
	private static bool Whole(string id, float share)
	{
		Dictionary<string, float> builtUp = Persist.State.BuiltUp;
		builtUp.TryGetValue(id, out var value);
		bool result = EffectMath.WholePortion(ref value, share);
		builtUp[id] = value;
		return result;
	}

	public static bool Try(string id, float value, float share)
	{
		switch (id)
		{
		case "GentleFood":
		{
			FoodPoisoning foodPoisoningComponent = GameManager.GetFoodPoisoningComponent();
			if (foodPoisoningComponent.HasFoodPoisoning())
			{
				foodPoisoningComponent.m_DurationHours *= 1f - value * share;
			}
			Dysentery dysenteryComponent = GameManager.GetDysenteryComponent();
			if (dysenteryComponent.HasDysentery())
			{
				dysenteryComponent.m_DurationHours *= 1f - value * share;
			}
			return true;
		}
		case "Clarity":
			if (Whole(id, share))
			{
				GameManager.GetHeadacheComponent().Cure();
			}
			return true;
		case "Deworm":
			if (Whole(id, share))
			{
				GameManager.GetIntestinalParasitesComponent().m_NumDosesTaken++;
			}
			return true;
		case "Nausea":
			if (Random.value < value * share)
			{
				GameManager.GetHungerComponent().RemoveReserveCalories(EatPatch.LastCalories * 0.5f);
			}
			return true;
		case "SugarRush":
		{
			Fatigue fatigueComponent = GameManager.GetFatigueComponent();
			fatigueComponent.m_CurrentFatigue = Mathf.Max(0f, fatigueComponent.m_CurrentFatigue - fatigueComponent.m_MaxFatigue * value * share);
			PlayerMovement playerMovementComponent = GameManager.GetPlayerMovementComponent();
			playerMovementComponent.AddSprintStamina(playerMovementComponent.m_MaxSprintStamina * value * share);
			return false;
		}
		case "RiceCalories":
			GameManager.GetHungerComponent().AddReserveCalories(value * share);
			return true;
		default:
			return false;
		}
	}
}
