using System;
using Il2Cpp;
using MelonLoader;
using ModData;
using Newtonsoft.Json;

namespace FoodPackByEMPEROR;

public static class Persist
{
	private static readonly ModDataManager manager = new ModDataManager("FoodPackBySeagull");

	public static SaveState State = new SaveState();

	public static bool FromRestore;

	public static void ResetForNewGame()
	{
		State = new SaveState();
		ActiveEffects.Clear();
		EffectTicks.Restart();
		PlayerStates.Forget();
	}

	public static void Save()
	{
		try
		{
			manager.Save(JsonConvert.SerializeObject((object)State));
		}
		catch (Exception e)
		{
			Safe.Log("Saving", e);
		}
	}

	public static void Load()
	{
		State = new SaveState();
		ActiveEffects.Clear();
		EffectTicks.Restart();
		string playerStateOverride = CoopApi.PlayerStateOverride;
		string playerStateOverrideSlot = CoopApi.PlayerStateOverrideSlot;
		CoopApi.PlayerStateOverride = null;
		CoopApi.PlayerStateOverrideSlot = null;
		bool fromRestore = FromRestore;
		FromRestore = false;
		if (playerStateOverride != null && (playerStateOverrideSlot == null || SaveGameSystem.GetCurrentSaveName() == playerStateOverrideSlot))
		{
			PlayerStates.Import(playerStateOverride);
			return;
		}
		if (fromRestore)
		{
			PlayerStates.Forget();
		}
		try
		{
			string text = manager.Load();
			if (!string.IsNullOrEmpty(text))
			{
				State = JsonConvert.DeserializeObject<SaveState>(text) ?? new SaveState();
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Mod save data unreadable, starting clean: " + ex.Message);
		}
	}
}
