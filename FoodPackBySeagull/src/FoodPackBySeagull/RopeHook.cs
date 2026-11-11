using HarmonyLib;
using Il2Cpp;

namespace FoodPackBySeagull;

[HarmonyPatch(typeof(PlayerClimbRope), "UpdateStamina")]
internal static class RopeHook
{
	private static void Prefix(PlayerClimbRope __instance, out float[] __state)
	{
		__state = new float[3] { __instance.m_StaminaDrainPerSecondClimbingUp, __instance.m_StaminaDrainPerSecondClimbingDown, __instance.m_StaminaDrainPerSecondClimbingHolding };
		float num = Fx.Less("GripHands");
		__instance.m_StaminaDrainPerSecondClimbingUp *= num;
		__instance.m_StaminaDrainPerSecondClimbingDown *= num;
		__instance.m_StaminaDrainPerSecondClimbingHolding *= num;
	}

	private static void Postfix(PlayerClimbRope __instance, float[] __state)
	{
		__instance.m_StaminaDrainPerSecondClimbingUp = __state[0];
		__instance.m_StaminaDrainPerSecondClimbingDown = __state[1];
		__instance.m_StaminaDrainPerSecondClimbingHolding = __state[2];
	}
}
