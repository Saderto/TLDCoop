using System.Collections.Generic;
using System.IO;
using Il2Cpp;
using Newtonsoft.Json;

namespace FoodPackByEMPEROR;

public static class Texts
{
	private static Dictionary<string, string> ru = new Dictionary<string, string>();

	private static Dictionary<string, string> en = new Dictionary<string, string>();

	public static int Count => en.Count;

	public static void Load(string root)
	{
		ru = Read(Path.Combine(root, "data", "text_ru.json"));
		en = Read(Path.Combine(root, "data", "text_en.json"));
	}

	public static int Merge(string root)
	{
		Dictionary<string, string> dictionary = Read(Path.Combine(root, "data", "text_ru.json"));
		Dictionary<string, string> dictionary2 = Read(Path.Combine(root, "data", "text_en.json"));
		foreach (KeyValuePair<string, string> item in dictionary)
		{
			ru[item.Key] = item.Value;
		}
		foreach (KeyValuePair<string, string> item2 in dictionary2)
		{
			en[item2.Key] = item2.Value;
		}
		return dictionary2.Count;
	}

	private static Dictionary<string, string> Read(string path)
	{
		if (!File.Exists(path))
		{
			return new Dictionary<string, string>();
		}
		return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path)) ?? new Dictionary<string, string>();
	}

	public static string Get(string key)
	{
		if (!((Localization.Language == "Russian") ? ru : en).TryGetValue(key, out string value) && !en.TryGetValue(key, out value))
		{
			return key;
		}
		return value;
	}
}
