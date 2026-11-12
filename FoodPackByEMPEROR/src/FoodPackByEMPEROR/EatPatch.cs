using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(GearItem), "ApplyBuffs")]
internal static class EatPatch
{
	public static float LastCalories;

	private static void Postfix(GearItem __instance, float normalizedValue)
	{
		try
		{
			if (normalizedValue <= 0f || __instance.m_FoodItem == null || !Items.TryGet(__instance.name, out ItemDef def))
			{
				return;
			}
			LastCalories = __instance.m_FoodItem.m_CaloriesTotal * normalizedValue;
			bool hot = __instance.m_FoodItem.IsHot();
			foreach (EffectUse effect in def.Effects)
			{
				try
				{
					StartEffect(effect, "SEAGULL_" + def.Id + "_NAME", normalizedValue, hot);
				}
				catch (Exception e)
				{
					Safe.Log("Effect " + effect.Id, e);
				}
			}
			if (!(def.AlcoholPercent > 0f))
			{
				return;
			}
			try
			{
				Alcohol.Drink(def.AlcoholLiters ?? def.WeightKg, def.AlcoholPercent, def.AlcoholUptakeMinutes, normalizedValue);
			}
			catch (Exception e2)
			{
				Safe.Log("Alcohol", e2);
			}
		}
		catch (Exception e3)
		{
			Safe.Log("Eating", e3);
		}
	}

	public static void StartEffect(EffectUse use, string cause, float share, bool hot = true)
	{
		EffectDef effectDef = Data.Effect(use.Id);
		if (effectDef == null)
		{
			MelonLogger.Error("Unknown effect " + use.Id);
		}
		else
		{
			if (effectDef.HotOnly && !hot)
			{
				return;
			}
			float? chance = use.Chance;
			if (chance.HasValue)
			{
				float valueOrDefault = chance.GetValueOrDefault();
				if (UnityEngine.Random.value >= valueOrDefault * share)
				{
					return;
				}
			}
			float value = use.Value ?? effectDef.Value;
			float num = use.Hours ?? effectDef.Hours;
			if (effectDef.Kind == "GameHeadache")
			{
				Dictionary<string, float> builtUp = Persist.State.BuiltUp;
				float now = Fx.Now;
				builtUp.TryGetValue("HeadacheCrumbs", out var value2);
				builtUp.TryGetValue("HeadacheCrumbsAt", out var value3);
				float num2 = EffectMath.HeadacheBite(ref value2, ref value3, now, share, GameEffects.Hurting(now));
				builtUp["HeadacheCrumbs"] = value2;
				builtUp["HeadacheCrumbsAt"] = value3;
				if (num2 > 0f)
				{
					GameEffects.Headache(num * num2, num);
				}
			}
			else if (!InstantEffects.Try(effectDef.Id, value, use.Chance.HasValue ? 1f : share))
			{
				FoodBuff foodBuff = new FoodBuff(effectDef, cause, value, num * share, num, share);
				foodBuff.Then = use.After;
				foodBuff.Start();
			}
			foreach (EffectUse item in effectDef.Also)
			{
				StartEffect(item, cause, share, hot);
			}
		}
	}
}
