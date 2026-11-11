using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;
using Il2CppTLD.Trader;
using MelonLoader;

namespace FoodPackBySeagull;

public static class TraderSakes
{
	private static class G
	{
		public const string Gut = "0e552f88cb8b6b2419cdad907519fa1c";

		public const string Cloth = "5eb0ecbeb0e0ca74d9b62520611c3725";

		public const string Crow = "d47689c13241611419a66d4449e280b3";

		public const string Ptarmigan = "a19980035a051384f947e8ea44731387";

		public const string Maple = "32eeada95dbb4bd4db8887149f516d2e";

		public const string Birch = "52225fe8d4f285845ac464124cb2ef46";

		public const string Coal = "0742fba2a302a64418216eead1646397";

		public const string Hides = "d4cbecc3ac5fef14bb982e380ee992da";

		public const string Plants = "e1e63420b320a6e4d9393432918f5d67";

		public const string Fish = "1e172dba4b73b9c49b418f4f2d0ac1f3";

		public const string Meat = "e4abb03f915b9334f9d1e13f1fca665b";

		public const string MeatOrFish = "473132e5ce883ab4eb72fb53594b7190";

		public const string Kerosene = "095871882a4a44fbab401411f1165c05";

		public const string Water = "ee84a2e67c4c34f139368b6c21b44216";
	}

	private abstract record Ask;

	private record GearN(string Guid, int N) : Ask();

	private record CatN(string Guid, int N) : Ask();

	private record CatKg(string Guid, float Kg) : Ask();

	private record Litres(string Guid, float L) : Ask();

	private record Offer(string Sake, int Trust, Ask[] Asks, int PerSet = 1, bool Rotating = true);

	private const string Prefix = "Seagull_Trade_";

	private static readonly Offer[] Ingredients = new Offer[3]
	{
		new Offer("Sauerkraut", 50, new Ask[3]
		{
			new CatKg("1e172dba4b73b9c49b418f4f2d0ac1f3", 5f),
			new Litres("ee84a2e67c4c34f139368b6c21b44216", 2f),
			new GearN("32eeada95dbb4bd4db8887149f516d2e", 2)
		}, 2, Rotating: false),
		new Offer("WhippedCream", 100, new Ask[3]
		{
			new CatKg("e4abb03f915b9334f9d1e13f1fca665b", 3f),
			new Litres("ee84a2e67c4c34f139368b6c21b44216", 1f),
			new GearN("a19980035a051384f947e8ea44731387", 4)
		}, 2, Rotating: false),
		new Offer("IrishWhiskey", 200, new Ask[3]
		{
			new CatN("d4cbecc3ac5fef14bb982e380ee992da", 3),
			new Litres("095871882a4a44fbab401411f1165c05", 2f),
			new GearN("d47689c13241611419a66d4449e280b3", 4)
		}, 1, Rotating: false)
	};

	private static readonly Offer[] Offers = new Offer[8]
	{
		new Offer("Sake_Junmai", 200, new Ask[3]
		{
			new CatN("d4cbecc3ac5fef14bb982e380ee992da", 3),
			new GearN("0e552f88cb8b6b2419cdad907519fa1c", 3),
			new GearN("5eb0ecbeb0e0ca74d9b62520611c3725", 4)
		}),
		new Offer("Sake_Ginjo", 200, new Ask[3]
		{
			new CatN("e1e63420b320a6e4d9393432918f5d67", 12),
			new Litres("095871882a4a44fbab401411f1165c05", 3f),
			new GearN("d47689c13241611419a66d4449e280b3", 4)
		}),
		new Offer("Sake_Koshu", 200, new Ask[3]
		{
			new CatKg("1e172dba4b73b9c49b418f4f2d0ac1f3", 6f),
			new Litres("ee84a2e67c4c34f139368b6c21b44216", 4f),
			new GearN("32eeada95dbb4bd4db8887149f516d2e", 3)
		}),
		new Offer("Sake_Nigori", 200, new Ask[3]
		{
			new CatKg("e4abb03f915b9334f9d1e13f1fca665b", 6f),
			new GearN("0e552f88cb8b6b2419cdad907519fa1c", 2),
			new GearN("a19980035a051384f947e8ea44731387", 6)
		}),
		new Offer("Sake_Honjozo", 300, new Ask[4]
		{
			new CatKg("473132e5ce883ab4eb72fb53594b7190", 12f),
			new GearN("52225fe8d4f285845ac464124cb2ef46", 5),
			new GearN("32eeada95dbb4bd4db8887149f516d2e", 6),
			new GearN("5eb0ecbeb0e0ca74d9b62520611c3725", 8)
		}),
		new Offer("Sake_Daiginjo", 300, new Ask[4]
		{
			new CatN("e1e63420b320a6e4d9393432918f5d67", 15),
			new GearN("d47689c13241611419a66d4449e280b3", 6),
			new Litres("ee84a2e67c4c34f139368b6c21b44216", 4f),
			new GearN("0e552f88cb8b6b2419cdad907519fa1c", 2)
		}),
		new Offer("Sake_Genshu", 300, new Ask[3]
		{
			new CatN("d4cbecc3ac5fef14bb982e380ee992da", 4),
			new Litres("095871882a4a44fbab401411f1165c05", 4f),
			new GearN("0742fba2a302a64418216eead1646397", 10)
		}),
		new Offer("Sake_Taru", 300, new Ask[3]
		{
			new CatKg("e4abb03f915b9334f9d1e13f1fca665b", 10f),
			new CatN("d4cbecc3ac5fef14bb982e380ee992da", 3),
			new GearN("d47689c13241611419a66d4449e280b3", 6)
		})
	};

	private static readonly int ShowPerSet = 1;

	private static readonly int ShowPerSetAt300 = 1;

	private static readonly float ChanceEach = 0f;

	private static readonly bool ShowAll = false;

	private static readonly System.Collections.Generic.Dictionary<string, TradeExchangeData> built = new System.Collections.Generic.Dictionary<string, TradeExchangeData>();

	public static int TestSetOffset;

	public static bool IsOurs(TradeExchangeData? d)
	{
		if (d != null && d.m_TradeExchangeName != null)
		{
			return d.m_TradeExchangeName.StartsWith("Seagull_Trade_", StringComparison.Ordinal);
		}
		return false;
	}

	public static string? SakeOf(TradeExchangeData d)
	{
		if (!IsOurs(d))
		{
			return null;
		}
		return d.m_TradeExchangeName.Substring("Seagull_Trade_".Length);
	}

	private static ExchangeItem Item(Ask a)
	{
		ExchangeItem exchangeItem = new ExchangeItem();
		if (!(a is GearN gearN))
		{
			if (!(a is CatN catN))
			{
				if (!(a is CatKg catKg))
				{
					if (a is Litres litres)
					{
						exchangeItem.m_ExchangeItemType = ExchangeItemType.Liquid;
						exchangeItem.m_LiquidType = new AssetReferenceLiquidType(litres.Guid);
						exchangeItem.m_LiquidVolume = ItemLiquidVolume.FromLiters(litres.L);
					}
				}
				else
				{
					exchangeItem.m_ExchangeItemType = ExchangeItemType.CategoryWeight;
					exchangeItem.m_ItemCategory = new AssetReferenceExchangeItemCategory(catKg.Guid);
					exchangeItem.m_Weight = ItemWeight.FromKilograms(catKg.Kg);
				}
			}
			else
			{
				exchangeItem.m_ExchangeItemType = ExchangeItemType.CategoryAmount;
				exchangeItem.m_ItemCategory = new AssetReferenceExchangeItemCategory(catN.Guid);
				exchangeItem.m_Amount = catN.N;
			}
		}
		else
		{
			exchangeItem.m_ExchangeItemType = ExchangeItemType.GearAmount;
			exchangeItem.m_GearItemData = new AssetReferenceGearItemData(gearN.Guid);
			exchangeItem.m_Amount = gearN.N;
		}
		return exchangeItem;
	}

	private static TradeExchangeData Build(Offer o)
	{
		if (built.TryGetValue(o.Sake, out TradeExchangeData value))
		{
			return value;
		}
		TradeExchangeData tradeExchangeData = new TradeExchangeData
		{
			m_TradeExchangeName = "Seagull_Trade_" + o.Sake,
			m_GainedItem = new AssetReferenceGearItemData(Items.ReferenceOf(o.Sake)),
			m_GainedItemAmount = 1,
			m_MaxExchangeInSingleTrade = 1,
			m_IsSpecialRequest = false,
			m_Description = new LocalizedString
			{
				m_LocalizationID = "SEAGULL_" + o.Sake + "_DESC"
			}
		};
		Il2CppSystem.Collections.Generic.List<ExchangeItem> list = new Il2CppSystem.Collections.Generic.List<ExchangeItem>();
		Il2CppSystem.Collections.Generic.List<ExchangeItem> list2 = new Il2CppSystem.Collections.Generic.List<ExchangeItem>();
		Ask[] asks = o.Asks;
		foreach (Ask a in asks)
		{
			list.Add(Item(a));
			list2.Add(Item(a));
		}
		tradeExchangeData.m_ExchangeItems = list;
		tradeExchangeData.m_CheapExchangeItems = list2;
		return built[o.Sake] = tradeExchangeData;
	}

	private static int SetNumber(TraderState state)
	{
		return state.m_LastAuroraNum + (Data.Settings.TestKeys ? TestSetOffset : 0);
	}

	public static string ShownNow(TraderManager tm)
	{
		return string.Join(", ", from o in ShownThisSet(tm.CurrentTrust, SetNumber(tm.m_CurrentState))
			select o.Sake);
	}

	private static string BoughtMark(string sake, int aurora, int nth = 1)
	{
		return "Seagull_Bought_" + sake + "_" + OneTimeSpawns.Text(aurora) + ((nth > 1) ? ("_" + OneTimeSpawns.Text(nth)) : "");
	}

	private static int BoughtCount(TraderState state, string item, int aurora, int limit)
	{
		Il2CppSystem.Collections.Generic.List<string> unlockedExchangeNames = state.m_UnlockedExchangeNames;
		int num = 0;
		if (unlockedExchangeNames != null)
		{
			for (int i = 1; i <= limit; i++)
			{
				if (unlockedExchangeNames.Contains(BoughtMark(item, aurora, i)))
				{
					num++;
				}
			}
		}
		return num;
	}

	private static Offer? OfferOf(string item)
	{
		string item2 = item;
		return Offers.Concat(Ingredients).FirstOrDefault((Offer o) => o.Sake == item2);
	}

	public static void AddTo(TraderManager tm, Il2CppSystem.Collections.Generic.List<TradeExchangeData> list)
	{
		TraderState currentState = tm.m_CurrentState;
		if (currentState == null || list == null)
		{
			return;
		}
		int trust = tm.CurrentTrust;
		int aurora = SetNumber(currentState);
		foreach (Offer item in ShownThisSet(trust, aurora).Concat(Ingredients.Where((Offer i) => trust >= i.Trust)))
		{
			int num = item.PerSet - BoughtCount(currentState, item.Sake, aurora, item.PerSet);
			if (num <= 0)
			{
				continue;
			}
			TradeExchangeData tradeExchangeData = Build(item);
			tradeExchangeData.m_MaxExchangeInSingleTrade = num;
			bool flag = false;
			Il2CppSystem.Collections.Generic.List<TradeExchangeData>.Enumerator enumerator2 = list.GetEnumerator();
			while (enumerator2.MoveNext())
			{
				TradeExchangeData current2 = enumerator2.Current;
				if (current2 != null && current2.m_TradeExchangeName == tradeExchangeData.m_TradeExchangeName)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				list.Add(tradeExchangeData);
			}
		}
	}

	private static System.Collections.Generic.IEnumerable<Offer> ShownThisSet(int trust, int aurora)
	{
		System.Collections.Generic.List<Offer> list = Offers.Where((Offer o) => trust >= o.Trust).ToList();
		if (ShowAll)
		{
			return list;
		}
		if (ChanceEach > 0f)
		{
			return list.Where((Offer o) => Roll(o) % 1000 < (uint)(ChanceEach * 1000f));
		}
		int count = ((trust >= 300) ? ShowPerSetAt300 : ShowPerSet);
		return list.OrderBy(Roll).Take(count);
		uint Roll(Offer o)
		{
			return OneTimeSpawns.Fnv("TraderSake|" + OneTimeSpawns.Text(aurora) + "|" + o.Sake);
		}
	}

	public static void MarkBought(TraderManager tm)
	{
		TraderState currentState = tm.m_CurrentState;
		ActiveTradeState activeTradeState = currentState?.m_ActiveTrade;
		if (currentState == null || activeTradeState?.m_ExchangeStates == null)
		{
			return;
		}
		int num = SetNumber(currentState);
		Il2CppSystem.Collections.Generic.List<ActiveTradeState.ActiveExchangeState>.Enumerator enumerator = activeTradeState.m_ExchangeStates.GetEnumerator();
		while (enumerator.MoveNext())
		{
			TradeExchangeData tradeExchangeData = enumerator.Current?.m_Exchange;
			string text = ((tradeExchangeData != null) ? SakeOf(tradeExchangeData) : null);
			if (text == null)
			{
				continue;
			}
			int num2 = OfferOf(text)?.PerSet ?? 1;
			int num3 = BoughtCount(currentState, text, num, num2);
			if (num3 < num2)
			{
				if (currentState.m_UnlockedExchangeNames == null)
				{
					currentState.m_UnlockedExchangeNames = new Il2CppSystem.Collections.Generic.List<string>();
				}
				currentState.m_UnlockedExchangeNames.Add(BoughtMark(text, num, num3 + 1));
				MelonLogger.Msg($"Trader: {text} bought ({num3 + 1} of {num2} this set, set {num})");
			}
		}
	}

	public static GearItemData? DataOf(TradeExchangeData d)
	{
		string text = SakeOf(d);
		if (text == null)
		{
			return null;
		}
		return Items.Template(text)?.m_GearItemData;
	}
}
