using ModSettings;

namespace ComplexLogger;

public class Settings : JsonModSettings
{
	[Name("Trace")]
	public bool TRACE;

	[Name("Debug")]
	public bool DEBUG;

	[Name("Verbose")]
	public bool VERBOSE;

	[Name("Warning")]
	public bool WARNING;

	internal static Settings Instance { get; } = new Settings();


	protected override void OnConfirm()
	{
		base.OnConfirm();
		Instance.UpdateLevels();
	}

	public void UpdateLevels()
	{
		if (TRACE)
		{
			Main.AddLevel(FlaggedLoggingLevel.Trace);
		}
		else
		{
			Main.RemoveLevel(FlaggedLoggingLevel.Trace);
		}
		if (DEBUG)
		{
			Main.AddLevel(FlaggedLoggingLevel.Debug);
		}
		else
		{
			Main.RemoveLevel(FlaggedLoggingLevel.Debug);
		}
		if (VERBOSE)
		{
			Main.AddLevel(FlaggedLoggingLevel.Warning);
		}
		else
		{
			Main.RemoveLevel(FlaggedLoggingLevel.Warning);
		}
	}

	internal static void OnLoad()
	{
		Instance.AddToModSettings("Complex Logger");
		Instance.UpdateLevels();
		Instance.RefreshGUI();
	}
}
