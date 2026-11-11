using System;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace FoodPackBySeagull;

public static class Alcohol
{
	private const float PermilleDropPerGameSecond = 4.166667E-05f;

	private static bool looked;

	private static object? mod;

	private static PropertyInfo? system;

	private static PropertyInfo? state;

	private static PropertyInfo? permille;

	private static MethodInfo? addIntake;

	private static object? System
	{
		get
		{
			if (mod == null)
			{
				return null;
			}
			return system?.GetValue(mod);
		}
	}

	private static void Find()
	{
		if (looked)
		{
			return;
		}
		looked = true;
		Type type = (from a in AppDomain.CurrentDomain.GetAssemblies()
			select a.GetType("AlcoholMod.AlcoholMod", throwOnError: false)).FirstOrDefault((Type t) => t != null);
		if (type == null)
		{
			MelonLogger.Warning("AlcoholMod not found: drinks won't intoxicate.");
			return;
		}
		mod = type.GetProperty("Instance", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
		system = type.GetProperty("System", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		Type obj = system?.PropertyType;
		addIntake = obj?.GetMethod("AddIntake", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		state = obj?.GetProperty("State", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		permille = state?.PropertyType.GetProperty("AlcoholPermille", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		if (mod == null || addIntake == null || permille == null)
		{
			MelonLogger.Error("This AlcoholMod version doesn't match: drinks won't intoxicate.");
		}
	}

	public static void Drink(float weightKg, float percent, float uptakeMinutes, float share)
	{
		Find();
		object obj = System;
		if (obj == null || addIntake == null)
		{
			return;
		}
		if (state?.GetValue(obj) == null && state != null)
		{
			object obj2 = Activator.CreateInstance(state.PropertyType);
			MethodInfo method = obj.GetType().GetMethod("SetState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method != null)
			{
				method.Invoke(obj, new object[1] { obj2 });
			}
			else
			{
				state.SetValue(obj, obj2);
			}
			MelonLogger.Msg("AlcoholMod had no state yet: started an empty one.");
		}
		object obj3 = state?.GetValue(obj);
		if (obj3 != null)
		{
			PropertyInfo property = obj3.GetType().GetProperty("Uptakes");
			if (property != null && property.GetValue(obj3) == null)
			{
				property.SetValue(obj3, Activator.CreateInstance(property.PropertyType));
			}
		}
		addIntake.Invoke(obj, new object[2]
		{
			EffectMath.AlcoholDose(weightKg, percent, share),
			uptakeMinutes * 60f
		});
	}

	public static void Sober(float gameHours, float extraRate)
	{
		Find();
		object obj = System;
		if (obj == null)
		{
			return;
		}
		object obj2 = state?.GetValue(obj);
		if (obj2 != null && !(permille == null))
		{
			float num = (float)permille.GetValue(obj2);
			if (num > 0f)
			{
				permille.SetValue(obj2, Mathf.Max(0f, num - 0.15f * gameHours * extraRate));
			}
		}
	}
}
