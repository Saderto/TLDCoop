using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(SaveGameSystem), "RestoreGlobalData")]
internal static class LoadPatch
{
	private static void Postfix()
	{
		CoopApi.SavedPlayerState = null;
		Persist.Load();
	}
}
