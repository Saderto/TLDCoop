using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace FoodPackByEMPEROR;

[HarmonyPatch]
internal static class StealthHook
{
	private static readonly HashSet<IntPtr> Shrunk = new HashSet<IntPtr>();

	private static void Shrink(BaseAi ai, out float[]? state)
	{
		state = null;
		float num = CoopApi.BorrowedDetectMul?.Invoke() ?? CoopApi.LocalDetectMul();
		if (!(num >= 1f) && Shrunk.Add(ai.Pointer))
		{
			state = new float[5] { ai.m_DetectionRange, ai.m_DetectionRangeWhileFeeding, ai.m_HearFootstepsRange, ai.m_HearFootstepsRangeWhileFeeding, ai.m_HearFootstepsRangeWhileSleeping };
			ai.m_DetectionRange *= num;
			ai.m_DetectionRangeWhileFeeding *= num;
			ai.m_HearFootstepsRange *= num;
			ai.m_HearFootstepsRangeWhileFeeding *= num;
			ai.m_HearFootstepsRangeWhileSleeping *= num;
		}
	}

	private static void Restore(BaseAi ai, float[]? state)
	{
		if (state != null)
		{
			ai.m_DetectionRange = state[0];
			ai.m_DetectionRangeWhileFeeding = state[1];
			ai.m_HearFootstepsRange = state[2];
			ai.m_HearFootstepsRangeWhileFeeding = state[3];
			ai.m_HearFootstepsRangeWhileSleeping = state[4];
			Shrunk.Remove(ai.Pointer);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(BaseAi), "ScanForNewTarget")]
	private static void ScanBefore(BaseAi __instance, out float[]? __state)
	{
		Shrink(__instance, out __state);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(BaseAi), "ScanForNewTarget")]
	private static void ScanDone(BaseAi __instance, float[]? __state)
	{
		Restore(__instance, __state);
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(BaseAi), "ScanForNewTarget")]
	private static void ScanAfter(BaseAi __instance, float[]? __state)
	{
		Restore(__instance, __state);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(BaseAi), "DoTargetDetectedWhileFeeding")]
	private static void FeedingBefore(BaseAi __instance, out float[]? __state)
	{
		Shrink(__instance, out __state);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(BaseAi), "DoTargetDetectedWhileFeeding")]
	private static void FeedingDone(BaseAi __instance, float[]? __state)
	{
		Restore(__instance, __state);
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(BaseAi), "DoTargetDetectedWhileFeeding")]
	private static void FeedingAfter(BaseAi __instance, float[]? __state)
	{
		Restore(__instance, __state);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(BaseAi), "ProcessFootstepAudioEvent")]
	private static void StepBefore(BaseAi __instance, out float[]? __state)
	{
		Shrink(__instance, out __state);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(BaseAi), "ProcessFootstepAudioEvent")]
	private static void StepDone(BaseAi __instance, float[]? __state)
	{
		Restore(__instance, __state);
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(BaseAi), "ProcessFootstepAudioEvent")]
	private static void StepAfter(BaseAi __instance, float[]? __state)
	{
		Restore(__instance, __state);
	}
}
