using HarmonyLib;
using Il2Cpp;

namespace FoodBalanceBySeagull;

[HarmonyPatch]
internal static class SceneVisitPatches
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GameManager), "LoadSceneWithLoadingScreen")]
	private static void ResetLoad()
	{
		GameFoodOnMaps.Reset();
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveGameSystem), "RestoreGame")]
	private static void ResetRestore()
	{
		GameFoodOnMaps.Reset();
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveGameSystem), "SaveSceneData")]
	private static void BeforeSave()
	{
		GameFoodOnMaps.BeforeSave();
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SaveGameSystem), "LoadSceneData")]
	private static void Read(bool __result, string sceneSaveName)
	{
		GameFoodOnMaps.Read(__result, sceneSaveName);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(InaccessibleGearContainer), "InitializeAllForCurrentScene")]
	private static void Ready()
	{
		GameFoodOnMaps.Ready();
	}
}
