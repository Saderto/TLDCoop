using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(SaveGameSystem), "RestoreGlobalData")]
internal static class LoadPatch
{
	private static void Postfix()
	{
		CoopApi.SavedPlayerState = null;
		Persist.Load();
	}
}
