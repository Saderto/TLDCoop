using System;
using MelonLoader;

namespace InventorySearchByEMPEROR;

public class Mod : MelonMod
{
	public const string VersionText = "0.6.0";

	internal static MelonLogger.Instance Log;

	public override void OnInitializeMelon()
	{
		Log = base.LoggerInstance;
	}

	public override void OnGUI()
	{
		try
		{
			Search.Draw();
		}
		catch (Exception e)
		{
			Once.Log("draw", e);
		}
	}
}
