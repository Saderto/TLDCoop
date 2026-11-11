using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Il2Cpp;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace FoodPackBySeagull;

public static class OneTimeSpawns
{
	public class SpotOverride
	{
		public string Item = "";

		public string Scene = "";

		public int Option;

		public float X;

		public float Y;

		public float Z;

		public float Yaw;

		public bool Fixed;
	}

	private static List<OnceSpot> spots = new List<OnceSpot>();

	private static List<OnceSpot> automatic = new List<OnceSpot>();

	private static bool heightsChecked;

	private static int groundMask;

	private static bool decided;

	private static int fallbackFrame = -1;

	private static string? readKey;

	private static bool readSaved;

	private static string? expected;

	private static int settleWaited;

	private static List<SpotSet> sets = new List<SpotSet>();

	private static readonly Regex treeName = new Regex("(?i)((^|[^s])tree|pine|spruce|cedar|birch|foliage|bush|shrub)");

	private static int GroundMask
	{
		get
		{
			if (groundMask == 0)
			{
				return groundMask = LayerMask.GetMask("Default", "Ground", "TerrainObject", "Buildings", "GroundNoNavmesh");
			}
			return groundMask;
		}
	}

	public static bool Pending => fallbackFrame >= 0;

	public static bool Decided => decided;

	public static List<OnceSpot> AllAutomatic()
	{
		return automatic;
	}

	public static List<OnceSpot> All()
	{
		return spots;
	}

	public static int ChosenOption(OnceSpot s)
	{
		if (s.Options.Count != 0)
		{
			return (int)(Fnv(Text(GameManager.m_SceneTransitionData.m_GameRandomSeed) + "|" + s.Item) % (uint)s.Options.Count);
		}
		return -1;
	}

	public static void SceneLoaded()
	{
		if (!decided && fallbackFrame < 0)
		{
			fallbackFrame = Time.frameCount + 30;
		}
	}

	private static string SaveKey()
	{
		string sceneSaveFilenameCurrent = GameManager.m_SceneTransitionData.m_SceneSaveFilenameCurrent;
		if (!string.IsNullOrEmpty(sceneSaveFilenameCurrent))
		{
			return sceneSaveFilenameCurrent;
		}
		return GameManager.m_ActiveScene;
	}

	public static string BaseScene(string? scene)
	{
		return Regex.Replace(scene ?? "", "(_Light|_DARKWALKER|_DEADMANWALKING|_TrainTracks|_SANDBOX|_DLC\\d*|_WILDLIFE|_VFX)+$", "");
	}

	public static void ResetForLoad(string? target = null)
	{
		decided = false;
		fallbackFrame = -1;
		readKey = null;
		settleWaited = 0;
		expected = (string.IsNullOrEmpty(target) ? null : BaseScene(target));
	}

	public static void SceneRead(bool hadSave, string? scene)
	{
		if (!decided && !string.IsNullOrEmpty(scene))
		{
			readKey = scene;
			readSaved = hadSave;
			Decide();
		}
	}

	public static void Tick()
	{
		if (!decided && (readKey != null || (fallbackFrame >= 0 && Time.frameCount >= fallbackFrame)))
		{
			Decide();
		}
	}

	private static bool Settled(out string scene)
	{
		scene = BaseScene(GameManager.m_ActiveScene);
		if (SaveGameSystem.IsRestoreInProgress() || SaveGameSystem.IsSceneRestoreInProgress() || SceneManager.IsLoading() || string.IsNullOrEmpty(scene))
		{
			return false;
		}
		if (expected == null || string.Equals(scene, expected, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return settleWaited > 1800;
	}

	private static void Decide()
	{
		try
		{
			if (!Settled(out string scene))
			{
				if (++settleWaited == 1200)
				{
					MelonLogger.Warning($"Scene check still waiting: the game names {GameManager.m_ActiveScene}, asked for {expected ?? "-"}, loading {SceneManager.IsLoading()}, restore {SaveGameSystem.IsRestoreInProgress()}");
				}
				return;
			}
			string text = SaveKey();
			bool flag;
			if (readKey != null && string.Equals(readKey, text, StringComparison.OrdinalIgnoreCase))
			{
				flag = readSaved;
			}
			else
			{
				string currentSaveName = SaveGameSystem.GetCurrentSaveName();
				flag = !string.IsNullOrEmpty(currentSaveName) && SaveGameSlots.HasFilenameInSlot(currentSaveName, text);
			}
			decided = true;
			fallbackFrame = -1;
			MelonLogger.Msg($"Scene {text} ({scene}): {(flag ? "visited before" : "first visit")}{((readKey != null && !string.Equals(readKey, text, StringComparison.OrdinalIgnoreCase)) ? (", the game read " + readKey) : "")}");
			RemoveStrays(scene);
			if (!flag)
			{
				Place(scene, text);
			}
		}
		catch (Exception e)
		{
			Safe.Log("Scene check", e);
			decided = true;
			fallbackFrame = -1;
		}
	}

	private static void RemoveStrays(string scene)
	{
		string scene2 = scene;
		foreach (GearItem item in UnityEngine.Object.FindObjectsOfType<GearItem>())
		{
			if (!Items.IsHidden(item) && !item.m_InPlayerInventory && !item.m_InsideContainer && Items.TryGet(item.name, out ItemDef d))
			{
				Vector3 p = item.transform.position;
				OnceSpot onceSpot = spots.FirstOrDefault((OnceSpot x) => x.Item == d.Id);
				if ((onceSpot != null && onceSpot.Options.Any((SpotOption o) => !string.Equals(BaseScene(o.Scene), scene2, StringComparison.OrdinalIgnoreCase) && Mathf.Abs(p.x - o.X) < 0.05f && Mathf.Abs(p.z - o.Z) < 0.05f)) || FoodSpots.AtSpotOfOther(d.Id, p, scene2))
				{
					MelonLogger.Msg($"Removed {d.Id} from {scene2} at {p.x:0.0} {p.y:0.0} {p.z:0.0}: that place belongs to another scene");
					UnityEngine.Object.Destroy(item.gameObject);
				}
			}
		}
	}

	public static void Load(string root)
	{
		string path = Path.Combine(root, "data", "spots.json");
		if (File.Exists(path))
		{
			automatic = JsonConvert.DeserializeObject<List<OnceSpot>>(File.ReadAllText(path)) ?? new List<OnceSpot>();
		}
		spots = automatic;
		string path2 = Path.Combine(root, "data", "spots_checked.json");
		if (File.Exists(path2))
		{
			spots = JsonConvert.DeserializeObject<List<OnceSpot>>(File.ReadAllText(path2)) ?? automatic;
			heightsChecked = true;
		}
		ApplyOverrides(root);
		string path3 = Path.Combine(root, "data", "sets.json");
		if (File.Exists(path3))
		{
			sets = JsonConvert.DeserializeObject<List<SpotSet>>(File.ReadAllText(path3)) ?? new List<SpotSet>();
		}
	}

	public static List<SpotSet> Sets()
	{
		return sets;
	}

	private static void PlaceSets(string scene)
	{
		List<GearItem> ours = OursLying();
		foreach (SpotSet set in sets)
		{
			if (!string.Equals(BaseScene(set.Scene), scene, StringComparison.OrdinalIgnoreCase) || set.Items.Count == 0)
			{
				continue;
			}
			Vector3 at = ((set.Places != null && set.Places.Count == set.Items.Count) ? new Vector3(set.Places[0][0], set.Places[0][1], set.Places[0][2]) : new Vector3(set.X, set.Y, set.Z));
			if (AlreadyThere(ours, set.Items[0], at))
			{
				MelonLogger.Msg(set.Id + ": already lies in " + scene + ", not laid again");
				continue;
			}
			if (set.Places != null && set.Places.Count == set.Items.Count)
			{
				for (int i = 0; i < set.Items.Count; i++)
				{
					float[] array = set.Places[i];
					Spawn(new OnceSpot
					{
						Item = set.Items[i]
					}, new SpotOption
					{
						Scene = set.Scene,
						X = array[0],
						Y = array[1],
						Z = array[2],
						Yaw = ((array.Length > 3) ? array[3] : set.Yaw),
						Fixed = true
					}, scene);
				}
				continue;
			}
			Vector3 vector = new Vector3(set.X, set.Y, set.Z);
			List<Vector3> list = new List<Vector3> { vector };
			Spawn(new OnceSpot
			{
				Item = set.Items[0]
			}, new SpotOption
			{
				Scene = set.Scene,
				X = set.X,
				Y = set.Y,
				Z = set.Z,
				Yaw = set.Yaw,
				Fixed = true
			}, scene);
			for (int j = 1; j < set.Items.Count; j++)
			{
				if (!Beside(vector, list, out var found))
				{
					MelonLogger.Warning($"{set.Id}: no room beside {set.Items[0]} for {set.Items[j]}; left out");
				}
				else
				{
					list.Add(found);
					Spawn(new OnceSpot
					{
						Item = set.Items[j]
					}, new SpotOption
					{
						Scene = set.Scene,
						X = found.x,
						Y = found.y,
						Z = found.z,
						Yaw = set.Yaw + 35f * (float)j,
						Fixed = true
					}, scene);
				}
			}
		}
	}

	private static bool Beside(Vector3 at, List<Vector3> taken, out Vector3 found)
	{
		found = at;
		float[] array = new float[6] { 0.16f, 0.13f, 0.19f, 0.22f, 0.25f, 0.12f };
		foreach (float num in array)
		{
			for (int j = 0; j < 16; j++)
			{
				float f2 = (float)j * Mathf.PI / 8f;
				foreach (Vector3 f in Floors(at + new Vector3(Mathf.Cos(f2) * num, 0f, Mathf.Sin(f2) * num)))
				{
					if (!(Mathf.Abs(f.y - at.y) > 0.012f) && Steady(f) && Open(f, 0.32f) && !taken.Any((Vector3 t) => (t - f).magnitude < 0.1f))
					{
						found = f;
						return true;
					}
				}
			}
		}
		return false;
	}

	public static string OverridePath(string root)
	{
		return Path.Combine(root, "data", "spots_override.json");
	}

	public static void ApplyOverrides(string root)
	{
		try
		{
			string path = OverridePath(root);
			if (!File.Exists(path))
			{
				return;
			}
			List<SpotOverride> list = JsonConvert.DeserializeObject<List<SpotOverride>>(File.ReadAllText(path)) ?? new List<SpotOverride>();
			foreach (SpotOverride o in list)
			{
				List<OnceSpot>[] array = new List<OnceSpot>[2] { spots, automatic };
				for (int i = 0; i < array.Length; i++)
				{
					OnceSpot onceSpot = array[i].FirstOrDefault((OnceSpot x) => x.Item == o.Item);
					if (onceSpot != null && o.Option >= 0 && o.Option < onceSpot.Options.Count)
					{
						SpotOption spotOption = onceSpot.Options[o.Option];
						if (!spotOption.Fixed || !(Mathf.Abs(spotOption.X - o.X) < 0.05f) || !(Mathf.Abs(spotOption.Z - o.Z) < 0.05f))
						{
							onceSpot.Options[o.Option] = new SpotOption
							{
								Scene = o.Scene,
								X = o.X,
								Y = o.Y,
								Z = o.Z,
								Yaw = o.Yaw,
								Fixed = o.Fixed
							};
						}
					}
				}
			}
			MelonLogger.Msg($"Spots set by hand: {list.Count}");
		}
		catch (Exception e)
		{
			Safe.Log("Spots set by hand", e);
		}
	}

	public static string WorldKey()
	{
		string text = SaveGameSystem.GetCurrentSaveName() ?? "";
		string text2 = "";
		try
		{
			SlotData slotData = (string.IsNullOrEmpty(text) ? null : SaveGameSlots.GetSaveSlotFromName(text));
			if (slotData != null)
			{
				text2 = slotData.m_GameId.ToString(CultureInfo.InvariantCulture);
			}
		}
		catch (Exception e)
		{
			Safe.Log("World key", e);
		}
		return text + "|" + text2 + "|" + Text(GameManager.m_SceneTransitionData.m_GameRandomSeed);
	}

	public static bool AlreadyThere(List<GearItem> ours, string item, Vector3 at)
	{
		string item2 = item;
		ItemDef def;
		return ours.Any((GearItem g) => g != null && Items.TryGet(g.name, out def) && def.Id == item2 && (g.transform.position - at).sqrMagnitude < 0.01f);
	}

	public static List<GearItem> OursLying()
	{
		ItemDef def;
		return (from g in UnityEngine.Object.FindObjectsOfType<GearItem>(includeInactive: true)
			where !Items.IsHidden(g) && !g.m_InPlayerInventory && !g.m_InsideContainer && Items.TryGet(g.name, out def)
			select g).ToList();
	}

	public static string Text(int n)
	{
		return n.ToString(CultureInfo.InvariantCulture);
	}

	public static void DecideBeforeSave()
	{
		if (!decided && (readKey != null || Pending))
		{
			Decide();
		}
	}

	public static uint Fnv(string s)
	{
		uint num = 2166136261u;
		foreach (char c in s)
		{
			num = (num ^ c) * 16777619;
		}
		return num;
	}

	private static void Place(string scene, string saveKey)
	{
		try
		{
			PlaceAll(scene, saveKey);
		}
		catch (Exception e)
		{
			Safe.Log("Placing items", e);
		}
	}

	private static void PlaceAll(string scene, string saveKey)
	{
		if (SpotCheck.Running || string.IsNullOrEmpty(scene) || (CoopApi.AllowWorldSpawn != null && !CoopApi.AllowWorldSpawn(scene)))
		{
			return;
		}
		int gameRandomSeed = GameManager.m_SceneTransitionData.m_GameRandomSeed;
		MelonLogger.Msg("World " + WorldKey() + ", first visit of " + saveKey);
		FoodSpots.Place(scene, saveKey, gameRandomSeed, OursLying());
		foreach (OnceSpot s in spots)
		{
			if (s.Options.Count == 0)
			{
				continue;
			}
			SpotOption spotOption = s.Options[(int)(Fnv(Text(gameRandomSeed) + "|" + s.Item) % (uint)s.Options.Count)];
			if (string.Equals(BaseScene(spotOption.Scene), scene, StringComparison.OrdinalIgnoreCase))
			{
				_ = "GEAR_Seagull_" + s.Item;
				if (UnityEngine.Object.FindObjectsOfType<GearItem>(includeInactive: true).Count((GearItem x) => !Items.IsHidden(x) && Items.TryGet(x.name, out ItemDef def) && def.Id == s.Item) <= (Data.Settings.TestKeys ? TestKeys.Copies(s.Item) : 0))
				{
					Spawn(s, spotOption, scene);
				}
			}
		}
		PlaceSets(scene);
	}

	private static bool Solid(Collider c)
	{
		if (c != null && !c.isTrigger && c.GetComponentInParent<GearItem>() == null && c.GetComponentInParent<RandomSpawnObject>() == null && !(c is CharacterController) && c.GetComponentInParent<vp_FPSController>() == null && c.attachedRigidbody == null)
		{
			return !Tree(c);
		}
		return false;
	}

	private static bool Tree(Collider c)
	{
		Transform transform = c.transform;
		int num = 0;
		while (num < 4 && transform != null)
		{
			if (treeName.IsMatch(transform.name))
			{
				return true;
			}
			num++;
			transform = transform.parent;
		}
		return false;
	}

	public static bool Open(Vector3 p, float height)
	{
		foreach (RaycastHit item in Physics.RaycastAll(p + Vector3.up * 0.05f, Vector3.up, height, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
		{
			if (Solid(item.collider))
			{
				return false;
			}
		}
		return true;
	}

	private static List<Vector3> Floors(Vector3 at)
	{
		List<Vector3> list = new List<Vector3>();
		foreach (RaycastHit item in from h in Physics.RaycastAll(at + Vector3.up * 60f, Vector3.down, 160f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
			orderby h.distance
			select h)
		{
			if (Solid(item.collider) && item.normal.y >= 0.5f)
			{
				list.Add(item.point);
			}
		}
		return list;
	}

	private static bool Steady(Vector3 p)
	{
		Vector3[] array = new Vector3[4]
		{
			new Vector3(0.12f, 0f, 0f),
			new Vector3(-0.12f, 0f, 0f),
			new Vector3(0f, 0f, 0.12f),
			new Vector3(0f, 0f, -0.12f)
		};
		foreach (Vector3 vector in array)
		{
			bool flag = false;
			foreach (RaycastHit item in Physics.RaycastAll(p + vector + Vector3.up * 0.3f, Vector3.down, 0.45f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
			{
				if (Solid(item.collider) && item.normal.y >= 0.8f && Mathf.Abs(item.point.y - p.y) < 0.06f)
				{
					flag = true;
				}
			}
			if (!flag)
			{
				return false;
			}
		}
		return true;
	}

	private static Vector3 Settle(Vector3 p)
	{
		if (Steady(p))
		{
			return p;
		}
		float[] array = new float[4] { 0.2f, 0.4f, 0.6f, 0.8f };
		foreach (float num in array)
		{
			for (int j = 0; j < 8; j++)
			{
				float f = (float)j * Mathf.PI / 4f;
				foreach (Vector3 item in Floors(p + new Vector3(Mathf.Cos(f) * num, 0f, Mathf.Sin(f) * num)))
				{
					if (Mathf.Abs(item.y - p.y) < 0.15f && Steady(item))
					{
						return item;
					}
				}
			}
		}
		return p;
	}

	public static bool Ground(Vector3 at, out Vector3 point, float room = 0.6f)
	{
		point = at;
		(float, float, float)[] array = new(float, float, float)[3]
		{
			(8f, 2f, 120f),
			(2.5f, 2.5f, room),
			(8f, 2f, room)
		};
		for (int i = 0; i < array.Length; i++)
		{
			var (num, num2, height) = array[i];
			foreach (Vector3 item in Around())
			{
				Vector3 p = default(Vector3);
				float num3 = float.MaxValue;
				foreach (Vector3 item2 in Floors(at + item))
				{
					float num4 = item2.y - at.y;
					if (!(num4 < 0f - num) && !(num4 > num2) && Open(item2, height))
					{
						float num5 = ((num4 <= 0.3f) ? Mathf.Abs(num4) : (num4 * 2.5f)) + item.magnitude * 0.5f;
						if (num5 < num3)
						{
							num3 = num5;
							p = item2;
						}
					}
				}
				if (num3 < float.MaxValue)
				{
					point = Settle(p);
					return true;
				}
			}
		}
		return false;
	}

	private static IEnumerable<Vector3> Around()
	{
		yield return Vector3.zero;
		float[] array = new float[3] { 1.5f, 3f, 5f };
		foreach (float r in array)
		{
			for (int i = 0; i < 8; i++)
			{
				float f = (float)i * Mathf.PI / 4f;
				yield return new Vector3(Mathf.Cos(f) * r, 0f, Mathf.Sin(f) * r);
			}
		}
	}

	public static bool Headroom(Vector3 p)
	{
		return Open(p, 1.9f);
	}

	private static GearItem? Spawn(OnceSpot s, SpotOption o, string scene)
	{
		GearItem gearItem = Items.Spawn("GEAR_Seagull_" + s.Item);
		if (gearItem == null)
		{
			return null;
		}
		gearItem.SkipSpawnChanceRoll();
		Vector3 point = new Vector3(o.X, o.Y, o.Z);
		if (!o.Fixed && !heightsChecked && !Ground(point, out point))
		{
			MelonLogger.Warning(s.Item + ": no ground at the spot in " + scene + ", using its height");
		}
		gearItem.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, o.Yaw, 0f));
		Rigidbody component = gearItem.GetComponent<Rigidbody>();
		if ((object)component != null)
		{
			component.isKinematic = true;
		}
		MelonLogger.Msg($"Placed {s.Item} in {scene} at {point.x:0.0} {point.y:0.0} {point.z:0.0} (file height {o.Y:0.0})");
		return gearItem;
	}

	public static void EnsureDecided()
	{
		if (!decided)
		{
			MelonLogger.Msg("Test: deciding " + SaveKey() + " now (it was still waiting)");
			Decide();
		}
	}

	public static bool ForcePlace(OnceSpot s)
	{
		OnceSpot s2 = s;
		ItemDef def;
		List<GearItem> list = (from x in UnityEngine.Object.FindObjectsOfType<GearItem>(includeInactive: true)
			where !Items.IsHidden(x) && Items.TryGet(x.name, out def) && def.Id == s2.Item
			select x).ToList();
		if (list.Any((GearItem x) => !x.m_InPlayerInventory && !x.m_InsideContainer) || list.Count > TestKeys.Copies(s2.Item))
		{
			return false;
		}
		SpotOption o = s2.Options[ChosenOption(s2)];
		return Spawn(s2, o, BaseScene(GameManager.m_ActiveScene)) != null;
	}
}
