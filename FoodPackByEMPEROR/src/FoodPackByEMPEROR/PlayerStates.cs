using System;
using System.Collections.Generic;
using System.Linq;
using AfflictionComponent.Components;
using Il2Cpp;
using Newtonsoft.Json;
using UnityEngine;

namespace FoodPackByEMPEROR;

public static class PlayerStates
{
	private class Buff
	{
		public string Id = "";

		public string Cause = "";

		public float Value;

		public float Left;

		public float Full;

		public float Share;

		public bool SawSleep;
	}

	private class Personal
	{
		public Dictionary<string, float> BuiltUp = new Dictionary<string, float>();

		public float CrumbsAge;

		public float HeadacheLeft;

		public List<Buff> Buffs = new List<Buff>();
	}

	private const string CrumbsAt = "HeadacheCrumbsAt";

	private static string? pending;

	private static int pendingFrame;

	private static AfflictionManager? seenManager;

	private static AfflictionManager? Manager => AfflictionManager.GetAfflictionManagerInstance();

	public static string? Export()
	{
		if (pending != null)
		{
			return pending;
		}
		Personal personal = new Personal();
		try
		{
			if (GameManager.GetTimeOfDayComponent() == null || SaveGameSystem.IsRestoreInProgress() || SaveGameSystem.IsSceneRestoreInProgress() || SceneManager.IsLoading() || GameManager.IsMainMenuActive() || Manager == null)
			{
				return null;
			}
			float now = Fx.Now;
			foreach (KeyValuePair<string, float> item in Persist.State.BuiltUp)
			{
				if (item.Key != "HeadacheCrumbsAt")
				{
					personal.BuiltUp[item.Key] = item.Value;
				}
			}
			if (Persist.State.BuiltUp.TryGetValue("HeadacheCrumbsAt", out var value))
			{
				personal.CrumbsAge = Math.Max(0f, now - value);
			}
			personal.HeadacheLeft = Math.Max(0f, Persist.State.HeadacheEnds - now);
			foreach (FoodBuff item2 in Manager.m_Afflictions.OfType<FoodBuff>())
			{
				if (item2.EndTime > now)
				{
					personal.Buffs.Add(new Buff
					{
						Id = item2.EffectId,
						Cause = item2.m_CauseText,
						Value = item2.Value,
						Left = item2.EndTime - now,
						Full = item2.FullHours,
						Share = item2.Share,
						SawSleep = item2.SawSleep
					});
				}
			}
		}
		catch (Exception e)
		{
			Safe.Log("Exporting the player", e);
			return null;
		}
		return JsonConvert.SerializeObject((object)personal);
	}

	public static void Import(string state)
	{
		pending = state ?? "";
		pendingFrame = Time.frameCount + 60;
	}

	public static void Forget()
	{
		pending = null;
	}

	public static void Tick()
	{
		if (pending == null)
		{
			return;
		}
		try
		{
			AfflictionManager manager = Manager;
			if (manager != seenManager)
			{
				seenManager = manager;
				pendingFrame = Math.Max(pendingFrame, Time.frameCount + 60);
			}
			else if (Time.frameCount >= pendingFrame && !SaveGameSystem.IsRestoreInProgress() && !SaveGameSystem.IsSceneRestoreInProgress() && !SceneManager.IsLoading() && !(manager == null))
			{
				string? state = pending;
				pending = null;
				Apply(state);
			}
		}
		catch (Exception e)
		{
			pending = null;
			Safe.Log("Importing the player", e);
		}
	}

	private static void Apply(string state)
	{
		Personal personal = null;
		try
		{
			if (state.Length > 0)
			{
				personal = JsonConvert.DeserializeObject<Personal>(state);
			}
		}
		catch (Exception e)
		{
			Safe.Log("Reading the player's state", e);
			return;
		}
		AfflictionManager manager = Manager;
		foreach (FoodBuff item in manager.m_Afflictions.OfType<FoodBuff>().ToList())
		{
			try
			{
				manager.Remove(item);
			}
			catch (Exception e2)
			{
				Safe.Log("Removing an effect", e2);
			}
		}
		ActiveEffects.Clear();
		Persist.State = new SaveState();
		if (personal == null)
		{
			return;
		}
		float now = Fx.Now;
		foreach (KeyValuePair<string, float> item2 in personal.BuiltUp)
		{
			Persist.State.BuiltUp[item2.Key] = item2.Value;
		}
		Persist.State.BuiltUp["HeadacheCrumbsAt"] = now - personal.CrumbsAge;
		Persist.State.HeadacheEnds = ((personal.HeadacheLeft > 0f) ? (now + personal.HeadacheLeft) : 0f);
		foreach (Buff buff in personal.Buffs)
		{
			EffectDef effectDef = Data.Effect(buff.Id);
			if (effectDef != null)
			{
				try
				{
					FoodBuff foodBuff = new FoodBuff(effectDef, buff.Cause, buff.Value, buff.Left, buff.Full, buff.Share);
					foodBuff.SawSleep = buff.SawSleep;
					foodBuff.Start();
				}
				catch (Exception e3)
				{
					Safe.Log("Restoring effect " + buff.Id, e3);
				}
			}
		}
	}
}
