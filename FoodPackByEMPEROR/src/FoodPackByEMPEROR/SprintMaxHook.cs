using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(PlayerMovement), "GetMaxSprintStaminaModifier")]
internal static class SprintMaxHook
{
	private static void Postfix(ref float __result)
	{
		__result *= Fx.More("Dash") * Fx.Less("HeavyBelly");
	}
}
