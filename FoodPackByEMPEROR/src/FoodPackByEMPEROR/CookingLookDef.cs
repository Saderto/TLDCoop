using System.Collections.Generic;

namespace FoodPackByEMPEROR;

public class CookingLookDef
{
	public Dictionary<string, Dictionary<string, string>> Meshes = new Dictionary<string, Dictionary<string, string>>();

	public Dictionary<string, string> Atlas = new Dictionary<string, string>();

	public string? RawTexture;
}
