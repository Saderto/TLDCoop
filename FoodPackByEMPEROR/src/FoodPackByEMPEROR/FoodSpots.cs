using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace FoodPackByEMPEROR;

public static class FoodSpots
{
	private static Dictionary<string, List<FoodSpot>> byScene = new Dictionary<string, List<FoodSpot>>(StringComparer.OrdinalIgnoreCase);

	public static void Load(string root)
	{
		string path = Path.Combine(root, "data", "food_spots.json");
		if (File.Exists(path))
		{
			byScene = (from s in JsonConvert.DeserializeObject<List<FoodSpot>>(File.ReadAllText(path)) ?? new List<FoodSpot>()
				group s by s.Scene).ToDictionary<IGrouping<string, FoodSpot>, string, List<FoodSpot>>((IGrouping<string, FoodSpot> g) => g.Key, (IGrouping<string, FoodSpot> g) => g.ToList(), StringComparer.OrdinalIgnoreCase);
		}
	}

	public static bool AtSpotOfOther(string item, Vector3 p, string scene)
	{
		string scene2 = scene;
		string item2 = item;
		return byScene.Any<KeyValuePair<string, List<FoodSpot>>>((KeyValuePair<string, List<FoodSpot>> kv) => !string.Equals(kv.Key, scene2, StringComparison.OrdinalIgnoreCase) && kv.Value.Any((FoodSpot s) => s.Item == item2 && Mathf.Abs(s.X - p.x) < 0.05f && Mathf.Abs(s.Y - p.y) < 0.05f && Mathf.Abs(s.Z - p.z) < 0.05f));
	}

	public static void Place(string scene, string saveKey, int seed, List<GearItem> lying)
	{
		if (!byScene.TryGetValue(scene, out List<FoodSpot> value))
		{
			return;
		}
		string text = (string.Equals(saveKey, scene, StringComparison.OrdinalIgnoreCase) ? "" : (saveKey + "|"));
		int num = 0;
		int num2 = 0;
		foreach (FoodSpot item in value)
		{
			float f = (float)item.ChancePercent * (CoopApi.AmountScale?.Invoke("GEAR_Seagull_" + item.Item) ?? 1f);
			if (OneTimeSpawns.Fnv(OneTimeSpawns.Text(seed) + "|" + text + item.Id) % 100 >= (uint)Mathf.Clamp(Mathf.RoundToInt(f), 0, 100))
			{
				continue;
			}
			if (OneTimeSpawns.AlreadyThere(lying, item.Item, new Vector3(item.X, item.Y, item.Z)))
			{
				num2++;
				continue;
			}
			GearItem gearItem = Items.Spawn("GEAR_Seagull_" + item.Item);
			if (!(gearItem == null))
			{
				gearItem.SkipSpawnChanceRoll();
				gearItem.transform.SetPositionAndRotation(new Vector3(item.X, item.Y, item.Z), Quaternion.Euler(0f, item.Yaw, 0f));
				Rigidbody component = gearItem.GetComponent<Rigidbody>();
				if ((object)component != null)
				{
					component.isKinematic = true;
				}
				num++;
			}
		}
		MelonLogger.Msg($"Food spots in {scene}: {num} of {value.Count}{((num2 > 0) ? $", {num2} already there" : "")}");
	}
}
