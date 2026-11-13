using HarmonyLib;
using Il2Cpp;

namespace FoodBalanceByEMPEROR;

// ModSettings writes a slider's hint once, when it builds the menu, so after switching the game language
// the hint stayed in the old language. After ModSettings shows a hint, swap our text to the current language.
[HarmonyPatch(typeof(Panel_CustomXPSetup), "UpdateMenuNavigation")]
internal static class HintLanguage
{
	[HarmonyPriority(0)]
	private static void Postfix(Panel_CustomXPSetup __instance)
	{
		UILabel label = __instance.m_TooltipLabel;
		if (label == null) return;
		string text = label.text;
		if (string.IsNullOrEmpty(text)) return;
		string[] pairs = new string[] {
			"Умножается на ползунок источника. Действует только на ещё не посещённые места и не заполненные контейнеры.", "Multiplied by each source's slider. Applies only to places not yet visited and containers not yet filled.",
			"Сколько еды этого мода на картах и в контейнерах. На картах шанс не поднимается выше 100%, поэтому 200% дают немного меньше двойного количества.", "How much of this mod's food on maps and in containers. On maps a chance stops at 100%, so 200% gives a little less than double.",
			"Сколько еды Food Pack by EMPEROR на картах и в контейнерах. Саке и карточки рецептов не меняются.", "How much Food Pack by EMPEROR food on maps and in containers. Sake and recipe cards don't change.",
			"Сколько игровой еды в контейнерах: шкафах, рюкзаках, машинах. Вода не меняется.", "How much of the game's food in containers: cupboards, backpacks, cars. Water doesn't change.",
			"Сколько игровой еды лежит на картах. Только уменьшение: при первом посещении места часть еды убирается.", "How much of the game's food lies on maps. Only less: part of it is removed on a place's first visit."
		};
		bool ru = Localization.Language == "Russian";
		for (int i = 0; i < pairs.Length; i += 2)
		{
			if (text == pairs[i] || text == pairs[i + 1])
			{
				string want = ru ? pairs[i] : pairs[i + 1];
				if (want != text) label.text = want;
				return;
			}
		}
	}
}
