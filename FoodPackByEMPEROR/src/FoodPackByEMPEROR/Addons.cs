using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FoodPackByEMPEROR;

public static class Addons
{
	public static readonly List<string> Roots = new List<string>();

	public static readonly List<ItemDef> Items = new List<ItemDef>();

	public static bool TestKeys;

	private static int nextGroup;

	public static void Tick()
	{
		if (!TestKeys || Items.Count == 0 || !Input.GetKey(KeyCode.LeftControl) || !Input.GetKey(KeyCode.LeftShift))
		{
			return;
		}
		try
		{
			if (Input.GetKeyDown(KeyCode.F4))
			{
				List<string> list = Items.Select((ItemDef i) => i.Group ?? i.Id).Distinct().ToList();
				string g = list[nextGroup % list.Count];
				nextGroup++;
				int value = Give(Items.Where((ItemDef i) => (i.Group ?? i.Id) == g));
				HUDMessage.AddMessage($"{g}: {value}  ({(nextGroup - 1) % list.Count + 1}/{list.Count})");
			}
			if (Input.GetKeyDown(KeyCode.F3))
			{
				HUDMessage.AddMessage($"Add-ons: {Give(Items)} / {Items.Count}");
			}
		}
		catch (Exception e)
		{
			Safe.Log("Add-on test key", e);
		}
	}

	private static int Give(IEnumerable<ItemDef> defs)
	{
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		if (playerManagerComponent == null)
		{
			return 0;
		}
		int num = 0;
		foreach (ItemDef def in defs)
		{
			if (playerManagerComponent.InstantiateItemInPlayerInventory("GEAR_Seagull_" + def.Id) != null)
			{
				num++;
			}
			else
			{
				MelonLogger.Warning("Add-on test: could not give " + def.Id);
			}
		}
		return num;
	}

	private static string Abs(string root, string? p)
	{
		if (!string.IsNullOrEmpty(p) && !Path.IsPathRooted(p))
		{
			return Path.Combine(root, p);
		}
		return p;
	}

	public static void LoadData()
	{
		Roots.Clear();
		string[] directories = Directory.GetDirectories(MelonEnvironment.ModsDirectory);
		foreach (string text in directories)
		{
			if (!string.Equals(Path.GetFullPath(text), Path.GetFullPath(Mod.Root), StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(text, "data", "foodpack_addon.json")))
			{
				Roots.Add(text);
			}
		}
		Items.Clear();
		foreach (string root in Roots)
		{
			try
			{
				if (((JToken)JObject.Parse(File.ReadAllText(Path.Combine(root, "data", "foodpack_addon.json")))).Value<bool?>((object)"TestKeys").GetValueOrDefault())
				{
					TestKeys = true;
				}
				HashSet<string> hashSet = new HashSet<string>(Data.Items.Select((ItemDef i) => i.Id));
				int num = 0;
				foreach (ItemDef item in Read<List<ItemDef>>(root, "items.json") ?? new List<ItemDef>())
				{
					if (!hashSet.Add(item.Id))
					{
						MelonLogger.Warning(Path.GetFileName(root) + ": item " + item.Id + " is already there, skipped");
						continue;
					}
					item.Texture = Abs(root, item.Texture);
					item.Icon = Abs(root, item.Icon);
					item.SingleMesh = Abs(root, item.SingleMesh);
					item.FoodMesh = Abs(root, item.FoodMesh);
					foreach (string item2 in item.Meshes.Keys.ToList())
					{
						item.Meshes[item2] = Abs(root, item.Meshes[item2]);
					}
					foreach (VariantDef variant in item.Variants)
					{
						variant.Texture = Abs(root, variant.Texture);
						variant.Mesh = Abs(root, variant.Mesh);
						variant.Icon = Abs(root, variant.Icon);
					}
					if (item.ExtraMesh != null)
					{
						item.ExtraMesh.Mesh = Abs(root, item.ExtraMesh.Mesh);
						item.ExtraMesh.Texture = Abs(root, item.ExtraMesh.Texture);
					}
					Data.Items.Add(item);
					Items.Add(item);
					num++;
				}
				int value = Data.AddEffects(Read<List<EffectDef>>(root, "effects.json") ?? new List<EffectDef>());
				int value2 = Texts.Merge(root);
				MelonLogger.Msg($"Add-on {Path.GetFileName(root)}: {num} items, {value} effects, {value2} text keys.");
			}
			catch (Exception e)
			{
				Safe.Log("Add-on " + root, e);
			}
		}
	}

	public static void MergeLoot()
	{
		foreach (string root in Roots)
		{
			try
			{
				foreach (KeyValuePair<string, List<LootEntry>> item in Read<Dictionary<string, List<LootEntry>>>(root, "loot.json") ?? new Dictionary<string, List<LootEntry>>())
				{
					if (!Loot.Tables.TryGetValue(item.Key, out List<LootEntry> value))
					{
						value = (Loot.Tables[item.Key] = new List<LootEntry>());
					}
					value.AddRange(item.Value);
				}
				foreach (string item2 in Read<List<string>>(root, "food_names.json") ?? new List<string>())
				{
					Loot.Food.Add(item2);
				}
			}
			catch (Exception e)
			{
				Safe.Log("Add-on loot " + root, e);
			}
		}
	}

	private static T? Read<T>(string root, string file) where T : class
	{
		string path = Path.Combine(root, "data", file);
		if (!File.Exists(path))
		{
			return null;
		}
		return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
	}
}
