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

	/// <summary>1.1.3 (loot rule 11-15): the doors of one cupboard are containers at the very same spot; this one's number among them
	/// (scene order), so each door rolls on its own and they do not get the same food.</summary>
	public static int Door(Il2Cpp.Container c)
	{
		try
		{
			Transform parent = c.transform.parent;
			if (parent == null)
			{
				return 0;
			}
			Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<Il2Cpp.Container> all = parent.GetComponentsInChildren<Il2Cpp.Container>(includeInactive: true);
			Vector3 p = c.transform.position;
			int x = Mathf.RoundToInt(p.x * 100f), y = Mathf.RoundToInt(p.y * 100f), z = Mathf.RoundToInt(p.z * 100f);
			int n = 0;
			for (int i = 0; i < all.Length; i++)
			{
				Il2Cpp.Container o = all[i];
				if (o == null)
				{
					continue;
				}
				if (o.Pointer == c.Pointer)
				{
					return n;
				}
				Vector3 q = o.transform.position;
				if (Mathf.RoundToInt(q.x * 100f) == x && Mathf.RoundToInt(q.y * 100f) == y && Mathf.RoundToInt(q.z * 100f) == z)
				{
					n++;
				}
			}
		}
		catch
		{
		}
		return 0;
	}

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
