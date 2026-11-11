using System;
using System.IO;
using System.Text.Json;
using MelonLoader;

namespace MarkersBySeagull;

internal static class Config
{
	private class Raw
	{
		public float Range { get; set; } = 150f;


		public bool Hud { get; set; } = true;

	}

	public static float Range = 150f;

	public static bool HudOn = true;

	public static void Save()
	{
		try
		{
			File.WriteAllText(Path.Combine(Mod.Root, "settings.json"), JsonSerializer.Serialize(new Raw
			{
				Range = Range,
				Hud = HudOn
			}));
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("settings.json: " + ex.Message);
		}
	}

	public static void Load()
	{
		try
		{
			string path = Path.Combine(Mod.Root, "settings.json");
			if (File.Exists(path))
			{
				Raw raw = JsonSerializer.Deserialize<Raw>(File.ReadAllText(path));
				if (raw != null)
				{
					Range = Math.Clamp(raw.Range, 10f, 1000f);
					HudOn = raw.Hud;
				}
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("settings.json: " + ex.Message);
		}
	}
}
