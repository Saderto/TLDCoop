using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace FoodPackByEMPEROR;

public class Mod : MelonMod
{
	public const string VersionText = "1.1.0";

	private bool showTestWindow;

	public static string Root => Path.Combine(MelonEnvironment.ModsDirectory, "FoodPackByEMPEROR");

	public override void OnInitializeMelon()
	{
		Data.Load(Root);
		Texts.Load(Root);
		Addons.LoadData();
		Items.Index();
		OneTimeSpawns.Load(Root);
		FoodSpots.Load(Root);
		Recipes.Load(Root);
		Loot.Load(Root);
		Addons.MergeLoot();
		base.LoggerInstance.Msg($"Loaded {Data.Items.Count} items, {Data.Effects.Count} effects, {Texts.Count} text keys.");
	}

	public override void OnSceneWasInitialized(int buildIndex, string sceneName)
	{
		try
		{
			OneTimeSpawns.SceneLoaded();
			SpotCheck.SceneLoaded();
		}
		catch (NullReferenceException)
		{
		}
	}

	public override void OnGUI()
	{
		if (Data.Settings.TestKeys)
		{
			TestKeys.OnGUI();
		}
		if (!showTestWindow)
		{
			return;
		}
		try
		{
			List<string> list = (from e in ActiveEffects.Running()
				select $"{Texts.Get("SEAGULL_FX_" + e.id + "_NAME")}: {e.value:0.##}, {e.hoursLeft:0.00} h, {e.share * 100f:0}%").ToList();
			if (list.Count == 0)
			{
				list.Add("-");
			}
			float currentCalorieBurnPerHour = GameManager.GetHungerComponent().m_CurrentCalorieBurnPerHour;
			list.Add($"{Texts.Get("SEAGULL_TEST_BURN")}: {currentCalorieBurnPerHour:0.0} / {CalorieBurnPatch.BurnWithoutEffects:0.0}");
			GUI.Box(new Rect(20f, 20f, 560f, 34 + list.Count * 24), "FoodPackByEMPEROR");
			for (int i = 0; i < list.Count; i++)
			{
				GUI.Label(new Rect(32f, 46 + i * 24, 540f, 24f), list[i]);
			}
		}
		catch (NullReferenceException)
		{
		}
	}

	public override void OnUpdate()
	{
		Recipes.Tick();
		EffectTicks.Run();
		OneTimeSpawns.Tick();
		EffectRow.Hover();
		PlayerStates.Tick();
		Addons.Tick();
		if (!Data.Settings.TestKeys)
		{
			return;
		}
		TestKeys.Tick();
		if (!Input.GetKey(KeyCode.LeftControl) || !Input.GetKey(KeyCode.LeftShift))
		{
			return;
		}
		try
		{
			if (Input.GetKeyDown(KeyCode.F11))
			{
				TestKeys.NextSpot();
			}
			if (Input.GetKeyDown(KeyCode.F10))
			{
				showTestWindow = !showTestWindow;
			}
			if (Input.GetKeyDown(KeyCode.F9) && Data.Items.Count > 0)
			{
				TestKeys.GiveAll();
			}
			if (Input.GetKeyDown(KeyCode.F12))
			{
				TestKeys.DumpTrader();
			}
			if (Input.GetKeyDown(KeyCode.F8))
			{
				TestKeys.TrustUp();
			}
			if (Input.GetKeyDown(KeyCode.F7))
			{
				TestKeys.NextTraderSet();
			}
			if (Input.GetKeyDown(KeyCode.F6))
			{
				TestKeys.RecordSpot();
			}
		}
		catch (Exception e)
		{
			Safe.Log("Test key", e);
		}
	}
}
