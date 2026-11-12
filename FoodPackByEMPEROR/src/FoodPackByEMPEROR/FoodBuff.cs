using System;
using System.Collections.Generic;
using AfflictionComponent.Components;
using AfflictionComponent.Enums;
using AfflictionComponent.Interfaces;
using Il2Cpp;
using Newtonsoft.Json;

namespace FoodPackByEMPEROR;

public class FoodBuff : CustomAffliction, IBuff, IDuration, IInstance
{
	[JsonProperty]
	public string EffectId = "";

	[JsonProperty]
	public float Value;

	[JsonProperty]
	public float FullHours;

	[JsonProperty]
	public bool SawSleep;

	[JsonProperty]
	public float Share = 1f;

	[JsonIgnore]
	private bool ended;

	public List<EffectUse>? Then;

	public bool Buff { get; set; } = true;


	public bool BuffCold { get; set; }

	public bool BuffFatigue { get; set; }

	public bool BuffHunger { get; set; }

	public bool BuffThirst { get; set; }

	public float Duration { get; set; }

	public float EndTime { get; set; }

	public InstanceType Type { get; set; } = InstanceType.Single;


	[JsonConstructor]
	private FoodBuff()
		: base("", "", "", null, "", AfflictionBodyArea.Stomach, customSprite: true)
	{
	}

	public FoodBuff(EffectDef def, string cause, float value, float hours, float fullHours, float share)
		: base("SEAGULL_FX_" + def.Id + "_NAME", cause, "SEAGULL_FX_" + def.Id + "_DESC", null, Resource(def.Icon), AfflictionBodyArea.Stomach, customSprite: true)
	{
		EffectId = def.Id;
		Value = value;
		Duration = hours;
		FullHours = fullHours;
		Share = share;
		Buff = !def.Negative;
		BuffHunger = def.Kind == "CalorieBurn" && !def.Negative;
	}

	private static string Resource(string? icon)
	{
		return "FoodPackByEMPEROR.Icons." + icon;
	}

	public override void OnUpdate()
	{
		try
		{
			ActiveEffects.Set(EffectId, EndTime, Value, Share);
			EffectDef effectDef = Data.Effect(EffectId);
			if (effectDef != null && effectDef.EndOnWake)
			{
				if (GameManager.GetRestComponent().IsSleeping())
				{
					SawSleep = true;
				}
				else if (SawSleep)
				{
					EndTime = 0f;
				}
			}
			if (GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused() <= EndTime || ended)
			{
				return;
			}
			ended = true;
			ActiveEffects.Remove(EffectId);
			if (effectDef != null)
			{
				foreach (EffectUse item in effectDef.After)
				{
					EatPatch.StartEffect(item, m_CauseText, Share);
				}
			}
			if (Then == null)
			{
				return;
			}
			foreach (EffectUse item2 in Then)
			{
				EatPatch.StartEffect(item2, m_CauseText, Share);
			}
		}
		catch (Exception e)
		{
			Safe.Log("FoodBuff " + EffectId, e);
		}
	}

	public void OnFoundExistingInstance(CustomAffliction existing)
	{
		if (existing is FoodBuff foodBuff)
		{
			float hoursPlayedNotPaused = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused();
			EffectMath.Running b = default(EffectMath.Running);
			b.Cause = foodBuff.m_CauseText;
			b.End = foodBuff.EndTime;
			b.Share = foodBuff.Share;
			b.Full = foodBuff.FullHours;
			b.Value = foodBuff.Value;
			EffectMath.Running running = EffectMath.Add(b, hoursPlayedNotPaused, m_CauseText, Duration, FullHours, Share, Value);
			foodBuff.m_CauseText = running.Cause;
			foodBuff.EndTime = running.End;
			foodBuff.Share = running.Share;
			foodBuff.FullHours = running.Full;
			foodBuff.Value = running.Value;
		}
	}
}
