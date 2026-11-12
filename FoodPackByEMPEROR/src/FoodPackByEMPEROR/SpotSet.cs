using System.Collections.Generic;

namespace FoodPackByEMPEROR;

public class SpotSet
{
	public string Id = "";

	public string Scene = "";

	public List<string> Items = new List<string>();

	public float X;

	public float Y;

	public float Z;

	public float Yaw;

	public List<float[]>? Places;
}
