using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using Newtonsoft.Json;

namespace FoodPackBySeagull;

public static class Data
{
	public static ModSettings Settings = new ModSettings();

	public static List<ItemDef> Items = new List<ItemDef>();

	public static List<EffectDef> Effects = new List<EffectDef>();

	private static Dictionary<string, EffectDef> effectById = new Dictionary<string, EffectDef>();

	public static EffectDef? Effect(string id)
	{
		if (!effectById.TryGetValue(id, out EffectDef value))
		{
			return null;
		}
		return value;
	}

	public static void Load(string root)
	{
		Items = Read<ItemDef>(Path.Combine(root, "data", "items.json"));
		Effects = Read<EffectDef>(Path.Combine(root, "data", "effects.json"));
		effectById = Effects.ToDictionary((EffectDef e) => e.Id);
		string path = Path.Combine(root, "data", "settings.json");
		if (File.Exists(path))
		{
			Settings = JsonConvert.DeserializeObject<ModSettings>(File.ReadAllText(path)) ?? new ModSettings();
		}
	}

	public static int AddEffects(List<EffectDef> more)
	{
		int num = 0;
		foreach (EffectDef item in more)
		{
			if (!effectById.ContainsKey(item.Id))
			{
				Effects.Add(item);
				effectById[item.Id] = item;
				num++;
			}
		}
		return num;
	}

	private static List<T> Read<T>(string path)
	{
		if (!File.Exists(path))
		{
			MelonLogger.Error("Missing data file: " + path);
			return new List<T>();
		}
		return JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path)) ?? new List<T>();
	}
}
