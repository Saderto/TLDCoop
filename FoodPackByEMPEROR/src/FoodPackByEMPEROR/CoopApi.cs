using System;

namespace FoodPackByEMPEROR;

public static class CoopApi
{
	public static Func<float?>? BorrowedDetectMul;

	public static Func<string, bool>? AllowWorldSpawn;

	public static Func<string, float>? AmountScale;

	public static string? PlayerStateOverride;

	public static string? PlayerStateOverrideSlot;

	public static string? SavedPlayerState;

	public static bool WorldSpawnsPending => OneTimeSpawns.Pending;

	public static float LocalDetectMul()
	{
		return Fx.Less("Stealth", "CedarSpirit");
	}

	public static string? ExportPlayerState()
	{
		return PlayerStates.Export();
	}

	public static void ImportPlayerState(string state)
	{
		PlayerStates.Import(state);
	}
}
