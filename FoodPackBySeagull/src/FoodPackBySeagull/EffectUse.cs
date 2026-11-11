using System.Collections.Generic;

namespace FoodPackBySeagull;

public class EffectUse
{
	public string Id = "";

	public float? Value;

	public float? Hours;

	public float? Chance;

	public List<EffectUse>? After;
}
