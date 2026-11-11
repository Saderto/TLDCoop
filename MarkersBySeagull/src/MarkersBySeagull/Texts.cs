using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Il2Cpp;
using MelonLoader;

namespace MarkersBySeagull;

public static class Texts
{
	private static Dictionary<string, string> ru = new Dictionary<string, string>();

	private static Dictionary<string, string> en = new Dictionary<string, string>();

	public static int Count => en.Count;

	public static bool Russian
	{
		get
		{
			try
			{
				return Localization.Language == "Russian";
			}
			catch
			{
				return false;
			}
		}
	}

	public static void Load(string root)
	{
		ru = Read(Path.Combine(root, "data", "text_ru.json"));
		en = Read(Path.Combine(root, "data", "text_en.json"));
	}

	private static Dictionary<string, string> Read(string path)
	{
		try
		{
			return File.Exists(path) ? (JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new Dictionary<string, string>()) : new Dictionary<string, string>();
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Text file unreadable " + path + ": " + ex.Message);
			return new Dictionary<string, string>();
		}
	}

	public static string Get(string key)
	{
		if (!(Russian ? ru : en).TryGetValue(key, out var value) && !en.TryGetValue(key, out value))
		{
			return key;
		}
		return value;
	}

	public static string Get(string key, params object[] args)
	{
		try
		{
			return string.Format(Get(key), args);
		}
		catch
		{
			return Get(key);
		}
	}
}
