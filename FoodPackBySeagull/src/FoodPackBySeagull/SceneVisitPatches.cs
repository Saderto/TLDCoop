using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch]
internal static class SceneVisitPatches
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GameManager), "LoadSceneWithLoadingScreen")]
	private static void Reset(string __0)
	{
		OneTimeSpawns.ResetForLoad(__0);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveGameSystem), "RestoreGame")]
	private static void ResetRestore()
	{
		OneTimeSpawns.ResetForLoad();
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveGameSystem), "SaveSceneData")]
	private static void BeforeSave()
	{
		OneTimeSpawns.DecideBeforeSave();
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SaveGameSystem), "RestoreGame")]
	private static void Opening()
	{
		Persist.FromRestore = true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(SaveGameSystem), "LoadSceneData")]
	private static void Read(bool __result, string sceneSaveName)
	{
		OneTimeSpawns.SceneRead(__result, sceneSaveName);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(InaccessibleGearContainer), "InitializeAllForCurrentScene")]
	private static void Ready()
	{
		OneTimeSpawns.SceneLoaded();
		SpotCheck.SceneLoaded();
	}
}
