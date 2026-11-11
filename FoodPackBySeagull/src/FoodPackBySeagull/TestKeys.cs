using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.Cooking;
using Il2CppTLD.Trader;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace FoodPackBySeagull;

public static class TestKeys
{
	private class Stop
	{
		public OnceSpot Spot;

		public int Option;
	}

	private const int LoadDelayFrames = 90;

	private static readonly System.Collections.Generic.List<Stop> stops = new System.Collections.Generic.List<Stop>();

	private static int index = -1;

	private static int wait = -1;

	private static string target = "";

	private static int waited;

	private static string line = "";

	private static float lineUntil;

	private static string CopiesFile => Path.Combine(Mod.Root, "data", "test_copies_" + OneTimeSpawns.WorldKey().Replace('|', '_') + ".json");

	public static int Copies(string item)
	{
		try
		{
			if (!File.Exists(CopiesFile))
			{
				return 0;
			}
			System.Collections.Generic.Dictionary<string, int> dictionary = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, int>>(File.ReadAllText(CopiesFile));
			int value;
			return (dictionary != null && dictionary.TryGetValue(item, out value)) ? value : 0;
		}
		catch (Exception e)
		{
			Safe.Log("Reading the test copies", e);
			return 0;
		}
	}

	public static void GiveAll()
	{
		PlayerManager playerManagerComponent = GameManager.GetPlayerManagerComponent();
		if (playerManagerComponent == null)
		{
			return;
		}
		System.Collections.Generic.HashSet<string> hashSet = new System.Collections.Generic.HashSet<string>(from s in OneTimeSpawns.All()
			select s.Item);
		System.Collections.Generic.Dictionary<string, int> dictionary = new System.Collections.Generic.Dictionary<string, int>();
		try
		{
			if (File.Exists(CopiesFile))
			{
				dictionary = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, int>>(File.ReadAllText(CopiesFile)) ?? new System.Collections.Generic.Dictionary<string, int>();
			}
		}
		catch (Exception e)
		{
			Safe.Log("Reading the test copies", e);
		}
		int num = 0;
		foreach (ItemDef item in Data.Items)
		{
			try
			{
				if (playerManagerComponent.InstantiateItemInPlayerInventory("GEAR_Seagull_" + item.Id) == null)
				{
					MelonLogger.Warning("Test: could not give " + item.Id);
					continue;
				}
				num++;
				if (hashSet.Contains(item.Id))
				{
					dictionary[item.Id] = dictionary.GetValueOrDefault(item.Id) + 1;
				}
			}
			catch (Exception e2)
			{
				Safe.Log("Giving " + item.Id, e2);
			}
		}
		try
		{
			File.WriteAllText(CopiesFile, JsonConvert.SerializeObject((object)dictionary, (Formatting)1));
		}
		catch (Exception e3)
		{
			Safe.Log("Writing the test copies", e3);
		}
		string value = UnlockRecipes();
		Show($"FoodPackBySeagull: {num} / {Data.Items.Count}; {value}");
		MelonLogger.Msg($"Test: gave {num} of {Data.Items.Count} items; {value}");
	}

	private static string UnlockRecipes()
	{
		try
		{
			RecipeBook book = RecipeBook.Instance;
			if (book == null)
			{
				return "no recipe book";
			}
			System.Collections.Generic.List<RecipeData> list = Recipes.Added.Values.ToList();
			int value = list.Count((RecipeData r) => book.AllRecipes.Contains(r));
			int value2 = list.Count((RecipeData r) => book.IsRecipeUnlocked(r));
			foreach (RecipeData item in list)
			{
				item.m_RequiredSkillLevel = 1;
				if (!book.IsRecipeUnlocked(item))
				{
					book.UnlockRecipe(item);
				}
			}
			int value3 = list.Count((RecipeData r) => book.IsRecipeUnlocked(r));
			return $"recipes in book {value}/{list.Count}, unlocked {value2} -> {value3}";
		}
		catch (Exception e)
		{
			Safe.Log("Unlocking the recipes", e);
			return "recipes: error";
		}
	}

	public static void NextSpot()
	{
		if (stops.Count == 0)
		{
			foreach (OnceSpot item in from s in OneTimeSpawns.All()
				orderby s.Item.StartsWith("Card_") ? 1 : 0, s.Options.FirstOrDefault()?.Scene, s.Item
				select s)
			{
				for (int i = 0; i < item.Options.Count; i++)
				{
					stops.Add(new Stop
					{
						Spot = item,
						Option = i
					});
				}
			}
			if (stops.Count == 0)
			{
				Show("FoodPackBySeagull: no spots");
				return;
			}
		}
		index = (index + 1) % stops.Count;
		Stop stop = stops[index];
		SpotOption spotOption = stop.Spot.Options[stop.Option];
		int num = OneTimeSpawns.ChosenOption(stop.Spot);
		bool flag = num == stop.Option;
		string value = Texts.Get("SEAGULL_" + stop.Spot.Item + "_NAME");
		string value2 = (flag ? Texts.Get("SEAGULL_TEST_HERE") : $"{Texts.Get("SEAGULL_TEST_ELSEWHERE")} {num + 1}");
		Show($"{index + 1} / {stops.Count}: {value} ({stop.Spot.Item}), {Texts.Get("SEAGULL_TEST_SPOT")} {stop.Option + 1} / {stop.Spot.Options.Count} — {value2}, {spotOption.Scene}", 60f);
		MelonLogger.Msg($"Test spot {index + 1}/{stops.Count}: {stop.Spot.Item} #{stop.Option} in {spotOption.Scene}{(flag ? " (this save's)" : "")}");
		if (string.Equals(OneTimeSpawns.BaseScene(GameManager.m_ActiveScene), OneTimeSpawns.BaseScene(spotOption.Scene), StringComparison.OrdinalIgnoreCase))
		{
			Arrive(stop);
			return;
		}
		wait = int.MaxValue;
		target = OneTimeSpawns.BaseScene(spotOption.Scene);
		waited = 0;
		GameManager.LoadSceneWithLoadingScreen(spotOption.Scene);
	}

	public static void Tick()
	{
		if (wait < 0)
		{
			return;
		}
		try
		{
			if (wait == int.MaxValue)
			{
				waited++;
				if (waited == 1200)
				{
					MelonLogger.Warning($"Test spot: still waiting for {target} (active {GameManager.m_ActiveScene}, loading {SceneManager.IsLoading()})");
				}
				if (!SceneManager.IsLoading() && string.Equals(OneTimeSpawns.BaseScene(GameManager.m_ActiveScene), target, StringComparison.OrdinalIgnoreCase))
				{
					wait = 90;
				}
			}
			else if (!SceneManager.IsLoading() && --wait <= 0)
			{
				wait = -1;
				Arrive(stops[index]);
			}
		}
		catch (Exception e)
		{
			wait = -1;
			Safe.Log("Test spot", e);
		}
	}

	private static void Arrive(Stop stop)
	{
		SpotOption spotOption = stop.Spot.Options[stop.Option];
		OneTimeSpawns.EnsureDecided();
		if (OneTimeSpawns.ChosenOption(stop.Spot) == stop.Option && OneTimeSpawns.ForcePlace(stop.Spot))
		{
			Show(line + " — " + Texts.Get("SEAGULL_TEST_PUT"), 60f);
		}
		Vector3 vector = new Vector3(spotOption.X, spotOption.Y, spotOption.Z);
		Vector3 point = vector;
		bool flag = spotOption.Fixed || OneTimeSpawns.Ground(vector, out point);
		Vector3 vector2 = point + Vector3.right * 1.6f + Vector3.up * 0.5f;
		bool flag2 = false;
		bool[] array = new bool[2] { true, false };
		foreach (bool flag3 in array)
		{
			Vector3[] array2 = new Vector3[4]
			{
				Vector3.right,
				Vector3.forward,
				Vector3.left,
				Vector3.back
			};
			foreach (Vector3 vector3 in array2)
			{
				if (OneTimeSpawns.Ground(point + vector3 * 1.6f, out var point2, 1.9f) && Mathf.Abs(point2.y - point.y) < 1.2f && (!flag3 || OneTimeSpawns.Open(point2, 120f)))
				{
					vector2 = point2 + Vector3.up * 0.2f;
					flag2 = true;
					break;
				}
			}
			if (flag2)
			{
				break;
			}
		}
		MelonLogger.Msg($"Test spot: ground {(flag ? "found" : "not found")} at {point.y:0.0} (file {spotOption.Y:0.0}), standing at {vector2.y:0.0}");
		try
		{
			GameManager.GetPlayerManagerComponent().TeleportPlayer(vector2, Quaternion.LookRotation(new Vector3(point.x - vector2.x, 0f, point.z - vector2.z) + Vector3.forward * 0.0001f));
		}
		catch (Exception ex)
		{
			MelonLogger.Warning("Teleport: " + ex.Message);
		}
		string text = "";
		foreach (GearItem item in UnityEngine.Object.FindObjectsOfType<GearItem>())
		{
			if (!Items.IsHidden(item) && Items.TryGet(item.name, out ItemDef def) && !(def.Id != stop.Spot.Item))
			{
				Vector3 position = item.transform.position;
				text = $" — {Texts.Get("SEAGULL_TEST_AT")} {position.x:0.0} {position.y:0.0} {position.z:0.0}, {Vector3.Distance(position, vector2):0.0} м";
				MelonLogger.Msg($"Test spot: {stop.Spot.Item} lies at {position.x:0.00} {position.y:0.00} {position.z:0.00}, {Vector3.Distance(position, vector2):0.0} m from the player");
				break;
			}
		}
		if (text.Length > 0)
		{
			Show(line + text, 60f);
		}
	}

	public static void RecordSpot()
	{
		try
		{
			if (index < 0 || index >= stops.Count)
			{
				Show("FoodPackBySeagull: F6 — go to a spot with F11 first");
				return;
			}
			Stop stop = stops[index];
			Vector3 position = GameManager.GetPlayerTransform().position;
			Vector3 point;
			Vector3 position2 = (OneTimeSpawns.Ground(position, out point) ? point : position);
			float y = GameManager.GetMainCamera().transform.eulerAngles.y;
			string text = OneTimeSpawns.BaseScene(GameManager.m_ActiveScene);
			string path = OneTimeSpawns.OverridePath(Mod.Root);
			System.Collections.Generic.List<OneTimeSpawns.SpotOverride> list = (File.Exists(path) ? (JsonConvert.DeserializeObject<System.Collections.Generic.List<OneTimeSpawns.SpotOverride>>(File.ReadAllText(path)) ?? new System.Collections.Generic.List<OneTimeSpawns.SpotOverride>()) : new System.Collections.Generic.List<OneTimeSpawns.SpotOverride>());
			list.RemoveAll((OneTimeSpawns.SpotOverride o) => o.Item == stop.Spot.Item && o.Option == stop.Option);
			list.Add(new OneTimeSpawns.SpotOverride
			{
				Item = stop.Spot.Item,
				Option = stop.Option,
				Scene = text,
				X = position2.x,
				Y = position2.y,
				Z = position2.z,
				Yaw = y,
				Fixed = true
			});
			File.WriteAllText(path, JsonConvert.SerializeObject((object)list, (Formatting)1));
			stop.Spot.Options[stop.Option] = new SpotOption
			{
				Scene = text,
				X = position2.x,
				Y = position2.y,
				Z = position2.z,
				Yaw = y,
				Fixed = true
			};
			bool flag = false;
			if (OneTimeSpawns.ChosenOption(stop.Spot) == stop.Option)
			{
				foreach (GearItem item in UnityEngine.Object.FindObjectsOfType<GearItem>())
				{
					if (!Items.IsHidden(item) && !item.m_InPlayerInventory && !item.m_InsideContainer && Items.TryGet(item.name, out ItemDef def) && !(def.Id != stop.Spot.Item))
					{
						item.transform.SetPositionAndRotation(position2, Quaternion.Euler(0f, y, 0f));
						flag = true;
					}
				}
			}
			string text2 = (flag ? Texts.Get("SEAGULL_TEST_MOVED") : ((OneTimeSpawns.ChosenOption(stop.Spot) == stop.Option) ? "" : Texts.Get("SEAGULL_TEST_NOTMOVED")));
			Show($"{stop.Spot.Item}, {Texts.Get("SEAGULL_TEST_SPOT")} {stop.Option + 1}: {Texts.Get("SEAGULL_TEST_SET")} ({position2.x:0.0} {position2.y:0.0} {position2.z:0.0}){((text2.Length > 0) ? (" — " + text2) : "")}", 20f);
			MelonLogger.Msg($"Test: spot {stop.Spot.Item} #{stop.Option} set by hand in {text} at {position2.x:0.00} {position2.y:0.00} {position2.z:0.00} yaw {y:0}{(flag ? ", item moved" : "")}");
		}
		catch (Exception e)
		{
			Safe.Log("Recording a spot", e);
		}
	}

	public static void DumpTrader()
	{
		try
		{
			TraderManager traderManager = UnityEngine.Object.FindObjectOfType<TraderManager>();
			if (traderManager == null)
			{
				Show("FoodPackBySeagull: no trader here");
				return;
			}
			System.Collections.Generic.List<object> list = new System.Collections.Generic.List<object>();
			Il2CppSystem.Collections.Generic.List<TradeExchange>.Enumerator enumerator = traderManager.GetPossibleExchanges().GetEnumerator();
			while (enumerator.MoveNext())
			{
				TradeExchange current = enumerator.Current;
				System.Collections.Generic.List<object> list2 = new System.Collections.Generic.List<object>();
				if (current.m_PossibleExchanges != null)
				{
					Il2CppSystem.Collections.Generic.List<TradeExchangeData>.Enumerator enumerator2 = current.m_PossibleExchanges.GetEnumerator();
					while (enumerator2.MoveNext())
					{
						TradeExchangeData d2 = enumerator2.Current;
						list2.Add(new
						{
							Name = d2.m_TradeExchangeName,
							Gives = Safe2(() => d2.GetGainedItemData()?.name),
							m_GainedItemAmount = d2.m_GainedItemAmount,
							m_MaxExchangeInSingleTrade = d2.m_MaxExchangeInSingleTrade,
							m_IsSpecialRequest = d2.m_IsSpecialRequest,
							Asks = d2.m_ExchangeItems?.ToArray().Select(Item).ToList(),
							AsksCheap = d2.m_CheapExchangeItems?.ToArray().Select(Item).ToList()
						});
					}
				}
				list.Add(new
				{
					Exchange = current.name,
					Flags = current.m_Flags.ToString(),
					Datas = list2
				});
			}
			TraderState currentState = traderManager.m_CurrentState;
			var anon = new
			{
				Trust = traderManager.CurrentTrust,
				MinTrust = traderManager.MinTrust,
				MaxTrust = traderManager.MaxTrust,
				MaxExchangesInTrade = traderManager.MaxExchangesInTrade,
				Unlocks = (from u in traderManager.m_TradeExchangeUnlocks?.ToArray()
					select new
					{
						TrustThreshold = u.TrustThreshold,
						Guid = u.Unlock?.AssetGUID
					}).ToList(),
				Unlocked = currentState?.m_UnlockedExchangeNames?.ToArray().ToList(),
				ThisAurora = (from d in currentState?.m_AvailableExchangesThisAurora?.ToArray()
					select d.m_TradeExchangeName).ToList(),
				LastAurora = currentState?.m_LastAuroraNum,
				Exchanges = list
			};
			File.WriteAllText(Path.Combine(Mod.Root, "data", "trader_dump.json"), JsonConvert.SerializeObject((object)anon, (Formatting)1));
			Show($"FoodPackBySeagull: trader dump, {list.Count} exchanges -> data/trader_dump.json");
		}
		catch (Exception e)
		{
			Safe.Log("Trader dump", e);
			Show("FoodPackBySeagull: trader dump failed, see the log");
		}
		static object Item(ExchangeItem i)
		{
			ExchangeItem j = i;
			return new
			{
				Type = j.m_ExchangeItemType.ToString(),
				Gear = Safe2(() => j.GetGearItemDataIfValid()?.name),
				Category = Safe2(() => j.GetExchangeItemCategoryIfValid()?.name),
				Powder = Safe2(() => j.GetPowderTypeIfValid()?.name),
				Liquid = Safe2(() => j.GetLiquidTypeIfValid()?.name),
				m_Amount = j.m_Amount,
				m_Calories = j.m_Calories,
				Kg = j.m_Weight.ToQuantity(1f),
				Litres = j.m_LiquidVolume.ToQuantity(1f)
			};
		}
	}

	private static string? Safe2(Func<string?> f)
	{
		try
		{
			return f();
		}
		catch
		{
			return null;
		}
	}

	public static void TrustUp()
	{
		try
		{
			TraderManager traderManager = UnityEngine.Object.FindObjectOfType<TraderManager>();
			if (traderManager == null)
			{
				Show("FoodPackBySeagull: no trader here");
				return;
			}
			traderManager.AddTrust(100);
			Show($"FoodPackBySeagull: trader trust {traderManager.CurrentTrust}");
			MelonLogger.Msg($"Test: trader trust now {traderManager.CurrentTrust}");
		}
		catch (Exception e)
		{
			Safe.Log("Trader trust", e);
		}
	}

	public static void NextTraderSet()
	{
		try
		{
			TraderSakes.TestSetOffset++;
			TraderManager traderManager = UnityEngine.Object.FindObjectOfType<TraderManager>();
			string value = ((traderManager != null && traderManager.m_CurrentState != null) ? TraderSakes.ShownNow(traderManager) : "?");
			Show($"FoodPackBySeagull: sake set +{TraderSakes.TestSetOffset}: {value}");
			MelonLogger.Msg($"Test: sake set offset {TraderSakes.TestSetOffset}, offered: {value}");
		}
		catch (Exception e)
		{
			Safe.Log("Next trader set", e);
		}
	}

	private static void Show(string text, float seconds = 8f)
	{
		line = text;
		lineUntil = Time.realtimeSinceStartup + seconds;
	}

	public static void OnGUI()
	{
		if (!string.IsNullOrEmpty(line) && !(Time.realtimeSinceStartup > lineUntil))
		{
			GUI.Box(new Rect(20f, Screen.height - 70, Math.Min(Screen.width - 40, 1100), 40f), "");
			GUI.Label(new Rect(32f, Screen.height - 62, Math.Min(Screen.width - 64, 1080), 30f), line);
		}
	}
}
