using System;
using MelonLoader;

namespace ContainerLabelsByEMPEROR;

public class Mod : MelonMod
{
	public const string VersionText = "0.6.1";

	internal static MelonLogger.Instance Log;

	private static bool s_Logged;

	public override void OnInitializeMelon()
	{
		Log = base.LoggerInstance;
	}

	public override void OnSceneWasInitialized(int buildIndex, string sceneName)
	{
		Labels.Reload();
	}

	public override void OnGUI()
	{
		try
		{
			Editor.Draw();
		}
		catch (Exception ex)
		{
			if (!s_Logged)
			{
				s_Logged = true;
				Log.Error("draw: " + ex);
			}
		}
	}
}
