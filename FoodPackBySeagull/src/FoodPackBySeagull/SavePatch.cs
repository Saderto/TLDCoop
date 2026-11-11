using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(SaveGameSystem), "SaveGlobalData")]
internal static class SavePatch
{
	private static void Postfix()
	{
		Persist.Save();
		try
		{
			string text = PlayerStates.Export();
			if (text != null)
			{
				CoopApi.SavedPlayerState = text;
			}
		}
		catch
		{
		}
	}
}
