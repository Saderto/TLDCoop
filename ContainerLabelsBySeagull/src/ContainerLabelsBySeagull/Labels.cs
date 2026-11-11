using System;
using System.Collections.Generic;
using Il2Cpp;
using ModData;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContainerLabelsBySeagull;

internal static class Labels
{
	private static readonly ModDataManager s_Data = new ModDataManager("ContainerLabelsBySeagull");

	private static Dictionary<string, Entry> s_Map = new Dictionary<string, Entry>();

	public static void Reload()
	{
		try
		{
			string text = s_Data.Load("labels");
			s_Map = (string.IsNullOrEmpty(text) ? new Dictionary<string, Entry>() : (JsonConvert.DeserializeObject<Dictionary<string, Entry>>(text) ?? new Dictionary<string, Entry>()));
		}
		catch (Exception ex)
		{
			s_Map = new Dictionary<string, Entry>();
			Mod.Log.Warning("load: " + ex.Message);
		}
	}

	private static void Store()
	{
		try
		{
			s_Data.Save(JsonConvert.SerializeObject((object)s_Map), "labels");
		}
		catch (Exception ex)
		{
			Mod.Log.Warning("save: " + ex.Message);
		}
	}

	public static string Key(Container c)
	{
		if (c == null)
		{
			return null;
		}
		string text = null;
		try
		{
			text = ObjectGuid.MaybeGetGuidFromGameObject(c.gameObject);
		}
		catch
		{
		}
		if (string.IsNullOrEmpty(text))
		{
			Vector3 position = c.transform.position;
			text = $"{c.gameObject.name}@{Mathf.RoundToInt(position.x)},{Mathf.RoundToInt(position.y)},{Mathf.RoundToInt(position.z)}";
		}
		return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "|" + text;
	}

	public static Entry Get(Container c)
	{
		string text = Key(c);
		if (text == null || !s_Map.TryGetValue(text, out var value))
		{
			return null;
		}
		return value;
	}

	public static void Set(Container c, string text, int color)
	{
		string text2 = Key(c);
		if (text2 != null)
		{
			text = (text ?? "").Trim().Replace("[", "(").Replace("]", ")");
			if (text.Length == 0)
			{
				s_Map.Remove(text2);
			}
			else
			{
				s_Map[text2] = new Entry
				{
					Text = text,
					Color = Palette.Wrap(color)
				};
			}
			Store();
		}
	}
}
