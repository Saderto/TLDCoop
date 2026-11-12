using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace FoodBalanceByEMPEROR;

public static class FoodBalance
{
	private class Sources
	{
		public Dictionary<string, string> Food = new Dictionary<string, string>();

		public HashSet<string> Protected = new HashSet<string>();
	}

	public static string SettingsOverride;

	private static string parsedFrom;

	private static BalanceValues parsed;

	private static Sources sources = new Sources();

	public static bool WorldSpawnsPending => GameFoodOnMaps.Pending;

	internal static BalanceValues Values
	{
		get
		{
			if (SettingsOverride == null)
			{
				return BalanceSettings.Values;
			}
			if ((object)parsedFrom != SettingsOverride)
			{
				parsedFrom = SettingsOverride;
				try
				{
					parsed = JsonConvert.DeserializeObject<BalanceValues>(SettingsOverride);
				}
				catch (Exception ex)
				{
					MelonLogger.Error("Settings from the host unreadable, using own: " + ex.Message);
					parsed = null;
				}
			}
			return parsed ?? BalanceSettings.Values;
		}
	}

	internal static int KnownFood => sources.Food.Count;

	internal static float GameOnMaps => Mathf.Clamp01(Values.General * Values.GameMaps / 10000f);

	public static string ExportSettings()
	{
		return JsonConvert.SerializeObject((object)Values);
	}

	public static void Load(string root)
	{
		string path = Path.Combine(root, "food_sources.json");
		if (File.Exists(path))
		{
			sources = JsonConvert.DeserializeObject<Sources>(File.ReadAllText(path)) ?? new Sources();
		}
	}

	private static string Key(string name)
	{
		int num = name.IndexOf('(');
		if (num > 0)
		{
			name = name.Substring(0, num);
		}
		name = name.Trim().ToLowerInvariant();
		if (!name.StartsWith("gear_"))
		{
			return name;
		}
		return name.Substring(5);
	}

	private static string SourceOf(string name)
	{
		string text = Key(name);
		if (text.StartsWith("seagull_"))
		{
			if (!text.StartsWith("seagull_sake_") && !text.StartsWith("seagull_card_"))
			{
				return "Seagull";
			}
			return null;
		}
		if (!sources.Food.TryGetValue(text, out var value))
		{
			return null;
		}
		return value;
	}

	private static float Slider(BalanceValues v, string source)
	{
		return source switch
		{
			"FoodPack" => v.FoodPack, 
			"CalorieWaltz" => v.CalorieWaltz, 
			"RetroFood" => v.RetroFood, 
			"AutumnComforts" => v.AutumnComforts, 
			"Seagull" => v.Seagull, 
			_ => 100f, 
		};
	}

	internal static float InContainer(string name)
	{
		string text = SourceOf(name);
		if (text == null)
		{
			return 1f;
		}
		BalanceValues values = Values;
		float num = ((text == "Game") ? values.GameContainers : Slider(values, text));
		return Mathf.Clamp(values.General * num / 10000f, 0f, 2f);
	}

	internal static float OnMap(string name, Vector3 at)
	{
		string text = SourceOf(name);
		if (text == null)
		{
			return 1f;
		}
		if (sources.Protected.Contains($"{Key(name)}|{Text(Mathf.RoundToInt(at.x * 100f))}|{Text(Mathf.RoundToInt(at.y * 100f))}|{Text(Mathf.RoundToInt(at.z * 100f))}"))
		{
			return 1f;
		}
		BalanceValues values = Values;
		return values.General * Slider(values, text) / 10000f;
	}

	private static float ForFoodPack(string name)
	{
		string text = SourceOf(name);
		if (text == null)
		{
			return 1f;
		}
		BalanceValues values = Values;
		return values.General * Slider(values, text) / 10000f;
	}

	internal static bool IsGameFood(string name)
	{
		return SourceOf(name) == "Game";
	}

	internal static float Roll(string s)
	{
		uint num = 2166136261u;
		foreach (char c in s)
		{
			num = (num ^ c) * 16777619;
		}
		return (float)(num % 100000) / 100000f;
	}

	internal static string Text(int n)
	{
		return n.ToString(CultureInfo.InvariantCulture);
	}

	internal static string Cm(Vector3 p)
	{
		return $"{Text(Mathf.RoundToInt(p.x * 100f))},{Text(Mathf.RoundToInt(p.y * 100f))},{Text(Mathf.RoundToInt(p.z * 100f))}";
	}

	internal static void ConnectFoodPack()
	{
		try
		{
			FieldInfo fieldInfo = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly a) => a.GetName().Name == "FoodPackByEMPEROR")?.GetType("FoodPackByEMPEROR.CoopApi")?.GetField("AmountScale", BindingFlags.Static | BindingFlags.Public);
			if (!(fieldInfo == null))
			{
				fieldInfo.SetValue(null, new Func<string, float>(ForFoodPack));
				MelonLogger.Msg("Connected to FoodPackByEMPEROR.");
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("FoodPackByEMPEROR: " + ex.Message);
		}
	}
}
