using System;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace FoodPackBySeagull;

public static class EffectTicks
{
	private static float last = -1f;

	private static float dysenteryBase = -1f;

	private static float nextLook;

	public static void Restart()
	{
		last = -1f;
	}

	public static void Run()
	{
		if (Time.unscaledTime < nextLook)
		{
			return;
		}
		nextLook = Time.unscaledTime + 0.25f;
		try
		{
			Look();
		}
		catch (NullReferenceException)
		{
		}
		catch (Exception e)
		{
			Safe.Log("Effect ticks", e);
		}
	}

	private static void Look()
	{
		TimeOfDay timeOfDayComponent = GameManager.GetTimeOfDayComponent();
		Dysentery dysenteryComponent = GameManager.GetDysenteryComponent();
		if (timeOfDayComponent == null || dysenteryComponent == null)
		{
			return;
		}
		float hoursPlayedNotPaused = timeOfDayComponent.GetHoursPlayedNotPaused();
		if (last >= 0f && hoursPlayedNotPaused < last - 0.01f)
		{
			ActiveEffects.Clear();
			Persist.State = new SaveState();
		}
		float num = ((last < 0f) ? 0f : Mathf.Max(0f, hoursPlayedNotPaused - last));
		last = hoursPlayedNotPaused;
		if (dysenteryBase < 0f)
		{
			dysenteryBase = dysenteryComponent.m_ChanceDysenteryAfterDrinkingUnsafeWater;
		}
		dysenteryComponent.m_ChanceDysenteryAfterDrinkingUnsafeWater = dysenteryBase * Fx.Less("StrongStomach");
		if (num <= 0f)
		{
			return;
		}
		if (Fx.On("Sobering"))
		{
			Alcohol.Sober(num, Fx.V("Sobering"));
		}
		float num2 = Fx.V("PainDull");
		if (!(num2 > 0f))
		{
			return;
		}
		List<SprainPain.Instance> list = GameManager.GetSprainPainComponent()?.m_ActiveInstances;
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				list[i].m_EndTime -= num * num2;
			}
		}
	}
}
