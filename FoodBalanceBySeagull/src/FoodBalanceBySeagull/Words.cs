using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace FoodBalanceBySeagull;

[HarmonyPatch(typeof(Localization), "Get", new Type[] { typeof(string) })]
internal static class Words
{
	private static readonly Dictionary<string, (string ru, string en)> Table = new Dictionary<string, (string, string)>
	{
		["FOODBALANCE_SECTION_ALL"] = ("Вся еда", "All food"),
		["FOODBALANCE_GENERAL"] = ("Общий множитель", "Overall"),
		["FOODBALANCE_GENERAL_DESC"] = ("Умножается на ползунок источника. Действует только на ещё не посещённые места и не заполненные контейнеры.", "Multiplied by each source's slider. Applies only to places not yet visited and containers not yet filled."),
		["FOODBALANCE_SECTION_MODS"] = ("Моды на еду", "Food mods"),
		["FOODBALANCE_FOODPACK"] = ("Food Pack", "Food Pack"),
		["FOODBALANCE_WALTZ"] = ("Foods by Calorie Waltz", "Foods by Calorie Waltz"),
		["FOODBALANCE_RETRO"] = ("Retro Food", "Retro Food"),
		["FOODBALANCE_AUTUMN"] = ("Autumn Comforts", "Autumn Comforts"),
		["FOODBALANCE_MOD_DESC"] = ("Сколько еды этого мода на картах и в контейнерах. На картах шанс не поднимается выше 100%, поэтому 200% дают немного меньше двойного количества.", "How much of this mod's food on maps and in containers. On maps a chance stops at 100%, so 200% gives a little less than double."),
		["FOODBALANCE_SEAGULL"] = ("Food Pack by Seagull", "Food Pack by Seagull"),
		["FOODBALANCE_SEAGULL_DESC"] = ("Сколько нашей еды на картах и в контейнерах. Саке и карточки рецептов не меняются.", "How much of our food on maps and in containers. Sake and recipe cards don't change."),
		["FOODBALANCE_SECTION_GAME"] = ("Еда самой игры", "The game's own food"),
		["FOODBALANCE_GAME_BOXES"] = ("В контейнерах", "In containers"),
		["FOODBALANCE_GAME_BOXES_DESC"] = ("Сколько игровой еды в контейнерах: шкафах, рюкзаках, машинах. Вода не меняется.", "How much of the game's food in containers: cupboards, backpacks, cars. Water doesn't change."),
		["FOODBALANCE_GAME_MAPS"] = ("На картах", "On maps"),
		["FOODBALANCE_GAME_MAPS_DESC"] = ("Сколько игровой еды лежит на картах. Только уменьшение: при первом посещении места часть еды убирается.", "How much of the game's food lies on maps. Only less: part of it is removed on a place's first visit.")
	};

	private static void Postfix(string key, ref string __result)
	{
		if (key != null && key.StartsWith("FOODBALANCE_") && Table.TryGetValue(key, out (string, string) value))
		{
			string text;
			if (Localization.Language == "Russian")
			{
				(text, _) = value;
			}
			else
			{
				text = value.Item2;
			}
			__result = text;
		}
	}
}
