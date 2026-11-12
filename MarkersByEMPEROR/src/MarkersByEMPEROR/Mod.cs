using System;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;

namespace MarkersByEMPEROR;

public class Mod : MelonMod
{
	public const string VersionText = "0.4.6";

	public static string Root => Path.Combine(MelonEnvironment.ModsDirectory, "MarkersByEMPEROR");

	public override void OnInitializeMelon()
	{
		Texts.Load(Root);
		Config.Load();
		base.LoggerInstance.Msg($"MarkersByEMPEROR {"0.4.6"}: {Texts.Count} text keys, HUD range {Config.Range} m.");
	}

	public override void OnSceneWasInitialized(int buildIndex, string sceneName)
	{
		ConsoleNames.Fill();
	}

	public override void OnUpdate()
	{
		try
		{
			Craft.Tick();
		}
		catch (Exception e)
		{
			Safe.Log("Craft", e);
		}
		try
		{
			Hud.Tick();
		}
		catch (Exception e2)
		{
			Safe.Log("Hud tick", e2);
		}
	}

	public override void OnGUI()
	{
		try
		{
			Hud.Draw();
		}
		catch (Exception e)
		{
			Safe.Log("Hud draw", e);
		}
		try
		{
			MapButton.Draw();
		}
		catch (Exception e2)
		{
			Safe.Log("Map button", e2);
		}
	}
}
