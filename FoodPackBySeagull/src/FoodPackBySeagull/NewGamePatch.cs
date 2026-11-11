using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(GameManager), "LaunchSandbox")]
internal static class NewGamePatch
{
	private static void Prefix()
	{
		Persist.ResetForNewGame();
	}
}
