using System.Collections.Generic;

namespace FoodPackBySeagull;

public class EffectDef
{
	public string Id = "";

	public string Kind = "";

	public float Value;

	public float? Value2;

	public float Hours;

	public bool Negative;

	public bool EndOnWake;

	public bool HotOnly;

	public List<EffectUse> Also = new List<EffectUse>();

	public List<EffectUse> After = new List<EffectUse>();

	public string? Icon;
}
