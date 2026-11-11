using MelonLoader;

namespace ComplexLogger;

public class Main : MelonMod
{
	public static FlaggedLoggingLevel CurrentLevel = FlaggedLoggingLevel.Error | FlaggedLoggingLevel.Critical | FlaggedLoggingLevel.Exception | FlaggedLoggingLevel.Always;

	internal static ComplexLogger<Main> Logger { get; } = new ComplexLogger<Main>();


	public static void AddLevel(FlaggedLoggingLevel level)
	{
		if (CurrentLevel.HasFlag(level))
		{
			Logger.Log($"Level has already been added: {level}", FlaggedLoggingLevel.Verbose, "AddLevel");
		}
		else
		{
			CurrentLevel |= level;
		}
	}

	public static bool RemoveLevel(FlaggedLoggingLevel level)
	{
		switch (level)
		{
		case FlaggedLoggingLevel.None:
			Logger.Log("Removing \"FlaggedLoggingLevel.None\" is not supported", FlaggedLoggingLevel.Verbose, "RemoveLevel");
			return false;
		case FlaggedLoggingLevel.Error:
			Logger.Log("Removing \"FlaggedLoggingLevel.Error\" is not supported", FlaggedLoggingLevel.Verbose, "RemoveLevel");
			return false;
		case FlaggedLoggingLevel.Critical:
			Logger.Log("Removing \"FlaggedLoggingLevel.Critical\" is not supported", FlaggedLoggingLevel.Verbose, "RemoveLevel");
			return false;
		case FlaggedLoggingLevel.Exception:
			Logger.Log("Removing \"FlaggedLoggingLevel.Exception\" is not supported", FlaggedLoggingLevel.Verbose, "RemoveLevel");
			return false;
		default:
			Logger.Log($"Removed {level}", FlaggedLoggingLevel.Verbose, "RemoveLevel");
			CurrentLevel &= ~level;
			return true;
		}
	}

	public override void OnInitializeMelon()
	{
		Settings.OnLoad();
	}
}
