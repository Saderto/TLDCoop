using System.IO;
using MelonLoader;
using MelonLoader.Utils;

namespace FoodBalanceByEMPEROR;

public class Mod : MelonMod
{
	public const string VersionText = "1.0.3";

	public static string Root => Path.Combine(MelonEnvironment.ModsDirectory, "FoodBalanceByEMPEROR");

	public override void OnInitializeMelon()
	{
		BalanceSettings.Load();
		FoodBalance.Load(Root);
		base.LoggerInstance.Msg($"Loaded {FoodBalance.KnownFood} kinds of food.");
	}

	public override void OnLateInitializeMelon()
	{
		FoodBalance.ConnectFoodPack();
	}

	public override void OnUpdate()
	{
		GameFoodOnMaps.Tick();
	}
}
