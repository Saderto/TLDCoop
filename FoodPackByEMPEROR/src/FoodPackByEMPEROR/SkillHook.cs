using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace FoodPackByEMPEROR;

[HarmonyPatch(typeof(SkillsManager), "IncrementPointsAndNotify")]
internal static class SkillHook
{
	private static readonly Dictionary<SkillType, float> saved = new Dictionary<SkillType, float>();

	private static void Prefix(SkillType skillType, ref int numPoints)
	{
		bool flag;
		switch (skillType)
		{
		case SkillType.CarcassHarvesting:
		case SkillType.Cooking:
		case SkillType.ClothingRepair:
		case SkillType.ToolRepair:
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			float num = Fx.V("Focus") + Fx.V("Flawless");
			if (skillType == SkillType.CarcassHarvesting)
			{
				num = Fx.V("Flawless");
			}
			if (!(num <= 0f))
			{
				saved.TryGetValue(skillType, out var value);
				value += (float)numPoints * num;
				int num2 = Mathf.FloorToInt(value);
				saved[skillType] = value - (float)num2;
				numPoints += num2;
			}
		}
	}
}
