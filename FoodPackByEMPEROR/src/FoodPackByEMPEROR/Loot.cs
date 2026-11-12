using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace FoodPackByEMPEROR;

public static class Loot
{
	public static Dictionary<string, List<LootEntry>> Tables = new Dictionary<string, List<LootEntry>>();

	public static HashSet<string> Food = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public static readonly HashSet<string> Unknown = new HashSet<string>();

	public static float Roll(int seed, Vector3 pos, int index)
	{
		return (float)(OneTimeSpawns.Fnv($"{OneTimeSpawns.Text(seed)}|{OneTimeSpawns.Text(Mathf.RoundToInt(pos.x * 100f))},{OneTimeSpawns.Text(Mathf.RoundToInt(pos.y * 100f))},{OneTimeSpawns.Text(Mathf.RoundToInt(pos.z * 100f))}|{OneTimeSpawns.Text(index)}") % 100000) / 100000f;
	}

	public static void Load(string root)
	{
		string path = Path.Combine(root, "data", "food_names.json");
		if (File.Exists(path))
		{
			Food = new HashSet<string>(JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(path)) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
		}
		string path2 = Path.Combine(root, "data", "loot.json");
		if (File.Exists(path2))
		{
			Tables = JsonConvert.DeserializeObject<Dictionary<string, List<LootEntry>>>(File.ReadAllText(path2)) ?? new Dictionary<string, List<LootEntry>>();
		}
	}
}
