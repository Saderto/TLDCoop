using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace FoodBalanceBySeagull;

public static class GameFoodOnMaps
{
	private static bool decided;

	private static int fallbackFrame = -1;

	private static string pendingKey;

	private static int pendingFrame;

	private static List<(GearItem gear, string place)> candidates = new List<(GearItem, string)>();

	internal static bool Pending
	{
		get
		{
			if (fallbackFrame < 0)
			{
				return pendingKey != null;
			}
			return true;
		}
	}

	private static bool Busy
	{
		get
		{
			if (!SaveGameSystem.IsRestoreInProgress() && !SaveGameSystem.IsSceneRestoreInProgress())
			{
				return SceneManager.IsLoading();
			}
			return true;
		}
	}

	internal static void Reset()
	{
		decided = false;
		fallbackFrame = -1;
		pendingKey = null;
		candidates.Clear();
	}

	internal static void Ready()
	{
		if (!decided)
		{
			fallbackFrame = Time.frameCount + 30;
		}
	}

	internal static void Read(bool hadSave, string scene)
	{
		if (decided)
		{
			return;
		}
		decided = true;
		fallbackFrame = -1;
		if (hadSave || FoodBalance.GameOnMaps >= 1f)
		{
			return;
		}
		try
		{
			Collect();
			pendingKey = (string.IsNullOrEmpty(scene) ? SaveKey() : scene);
			pendingFrame = Time.frameCount + 60;
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Game food on maps: " + ex.Message);
			candidates.Clear();
		}
	}

	private static void Collect()
	{
		candidates.Clear();
		foreach (GearItem item in UnityEngine.Object.FindObjectsOfType<GearItem>())
		{
			if (!item.m_InPlayerInventory && !item.m_InsideContainer && !item.m_BeenInPlayerInventory && !(item.m_MissionObject != null) && !(item.m_NarrativeCollectibleItem != null) && FoodBalance.IsGameFood(item.name))
			{
				candidates.Add((item, FoodBalance.Cm(item.transform.position)));
			}
		}
	}

	internal static void BeforeSave()
	{
		try
		{
			if (fallbackFrame >= 0)
			{
				string text = SaveKey();
				string currentSaveName = SaveGameSystem.GetCurrentSaveName();
				Read(!string.IsNullOrEmpty(currentSaveName) && SaveGameSlots.HasFilenameInSlot(currentSaveName, text), text);
			}
			if (pendingKey != null)
			{
				string saveKey = pendingKey;
				pendingKey = null;
				Thin(saveKey);
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Game food before saving: " + ex.Message);
			fallbackFrame = -1;
			pendingKey = null;
			candidates.Clear();
		}
	}

	private static string SaveKey()
	{
		string sceneSaveFilenameCurrent = GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent;
		if (!string.IsNullOrEmpty(sceneSaveFilenameCurrent))
		{
			return sceneSaveFilenameCurrent;
		}
		return GameManager.m_ActiveScene;
	}

	public static void Tick()
	{
		try
		{
			if (fallbackFrame >= 0 && Time.frameCount >= fallbackFrame && !Busy)
			{
				string text = SaveKey();
				string currentSaveName = SaveGameSystem.GetCurrentSaveName();
				Read(!string.IsNullOrEmpty(currentSaveName) && SaveGameSlots.HasFilenameInSlot(currentSaveName, text), text);
			}
			if (pendingKey != null && Time.frameCount >= pendingFrame && !Busy)
			{
				string saveKey = pendingKey;
				pendingKey = null;
				Thin(saveKey);
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Game food on maps: " + ex.Message);
			fallbackFrame = -1;
			pendingKey = null;
			candidates.Clear();
		}
	}

	private static void Thin(string saveKey)
	{
		float gameOnMaps = FoodBalance.GameOnMaps;
		int gameRandomSeed = GameManager.m_SceneTransitionData.m_GameRandomSeed;
		int num = 0;
		int num2 = 0;
		foreach (var (gearItem, value) in candidates)
		{
			if (!(gearItem == null) && !gearItem.m_InPlayerInventory && !gearItem.m_InsideContainer)
			{
				num++;
				if (!(FoodBalance.Roll($"{FoodBalance.Text(gameRandomSeed)}|{saveKey}|{value}|balance") < gameOnMaps))
				{
					UnityEngine.Object.Destroy(gearItem.gameObject);
					num2++;
				}
			}
		}
		candidates.Clear();
		MelonLogger.Msg($"Game food in {saveKey}: removed {num2} of {num}");
	}
}
