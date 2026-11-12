using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Il2Cpp;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace FoodPackByEMPEROR;

public static class SpotCheck
{
	private class Stop
	{
		public OnceSpot Spot;

		public SpotOption Option;

		public int Number;
	}

	public class Result
	{
		public string Item = "";

		public string Scene = "";

		public string Status = "";

		public string Hit = "";

		public int Option;

		public float X;

		public float Y;

		public float Z;

		public float Yaw;

		public float Up;

		public float Moved;
	}

	private enum Step
	{
		Idle,
		GoToScene,
		WaitForScene,
		Teleport,
		Settle
	}

	private const int LoadDelayFrames = 90;

	private const int ShotDelayFrames = 45;

	private const float MinUp = 0.82f;

	private const float ProbeHeight = 40f;

	private static readonly List<Stop> stops = new List<Stop>();

	private static readonly List<Result> results = new List<Result>();

	private static int next;

	private static int wait;

	private static Step step = Step.Idle;

	private static bool sceneLoaded;

	public static bool Running => step != Step.Idle;

	private static string ShotDir => Path.Combine(Mod.Root, "spot_check");

	public static void SceneLoaded()
	{
		sceneLoaded = true;
	}

	public static void Toggle()
	{
		if (Running)
		{
			MelonLogger.Msg("Spot check stopped.");
			step = Step.Idle;
			return;
		}
		stops.Clear();
		results.Clear();
		foreach (OnceSpot item in from s in OneTimeSpawns.AllAutomatic()
			orderby s.Options.FirstOrDefault()?.Scene
			select s)
		{
			for (int i = 0; i < item.Options.Count; i++)
			{
				stops.Add(new Stop
				{
					Spot = item,
					Option = item.Options[i],
					Number = i
				});
			}
		}
		next = 0;
		Directory.CreateDirectory(ShotDir);
		MelonLogger.Msg($"Spot check: {stops.Count} spots. Ctrl+Shift+F11 again stops it.");
		step = Step.GoToScene;
	}

	public static void Tick()
	{
		if (step == Step.Idle)
		{
			return;
		}
		try
		{
			Advance();
		}
		catch (Exception e)
		{
			Safe.Log("Spot check", e);
			if (next < stops.Count)
			{
				Record(stops[next], "error", null);
			}
			next++;
			step = Step.GoToScene;
		}
	}

	private static void Advance()
	{
		if (wait > 0)
		{
			wait--;
			return;
		}
		switch (step)
		{
		case Step.GoToScene:
		{
			if (next >= stops.Count)
			{
				Finish();
				break;
			}
			string scene = stops[next].Option.Scene;
			if (string.Equals(GameManager.m_ActiveScene, scene, StringComparison.OrdinalIgnoreCase))
			{
				step = Step.Teleport;
				break;
			}
			sceneLoaded = false;
			MelonLogger.Msg("Spot check: loading " + scene);
			GameManager.LoadSceneWithLoadingScreen(scene);
			step = Step.WaitForScene;
			break;
		}
		case Step.WaitForScene:
			if (sceneLoaded)
			{
				wait = 90;
				step = Step.Teleport;
			}
			break;
		case Step.Teleport:
		{
			SpotOption option = stops[next].Option;
			GameManager.GetPlayerManagerComponent().TeleportPlayer(new Vector3(option.X + 3f, option.Y + 2f, option.Z), Quaternion.Euler(0f, -90f, 0f));
			wait = 45;
			step = Step.Settle;
			break;
		}
		case Step.Settle:
			Inspect(stops[next]);
			next++;
			step = Step.GoToScene;
			break;
		}
	}

	private static void Inspect(Stop s)
	{
		SpotOption option = s.Option;
		Vector3 origin = new Vector3(option.X, option.Y + 40f, option.Z);
		if (!Physics.Raycast(new Vector3(option.X, option.Y + 1f, option.Z), Vector3.down, out var hitInfo, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && !Physics.Raycast(origin, Vector3.down, out hitInfo, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
		{
			Record(s, "no_ground", null);
			return;
		}
		string input = ((hitInfo.collider != null) ? hitInfo.collider.name : "");
		string status = "ok";
		if (Regex.IsMatch(input, "(?i)water|(^|[_\\s-])ice($|[_\\s-])|(^|[_\\s-])ice[A-Z0-9]"))
		{
			status = "water";
		}
		else if (Mathf.Abs(hitInfo.point.y - option.Y) > 3f)
		{
			status = "far";
		}
		else if (hitInfo.normal.y < 0.82f)
		{
			status = "steep";
		}
		else if (Physics.CheckSphere(hitInfo.point + Vector3.up * 0.25f, 0.12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
		{
			status = "inside";
		}
		Record(s, status, hitInfo);
		Photograph(s, hitInfo.point, status);
	}

	private static void Record(Stop s, string status, RaycastHit? hit)
	{
		SpotOption option = s.Option;
		Vector3 vector = hit?.point ?? new Vector3(option.X, option.Y, option.Z);
		results.Add(new Result
		{
			Item = s.Spot.Item,
			Scene = option.Scene,
			Option = s.Number,
			Status = status,
			Hit = ((hit?.collider != null) ? hit.Value.collider.name : ""),
			X = vector.x,
			Y = vector.y,
			Z = vector.z,
			Yaw = option.Yaw,
			Up = (hit?.normal.y ?? 0f),
			Moved = Mathf.Abs(vector.y - option.Y)
		});
		MelonLogger.Msg($"Spot {s.Spot.Item} #{s.Number} in {option.Scene}: {status}, moved {Mathf.Abs(vector.y - option.Y):0.00} m");
	}

	private static void Photograph(Stop s, Vector3 at, string status)
	{
		GearItem gearItem = Items.Spawn("GEAR_Seagull_" + s.Spot.Item);
		if (gearItem != null)
		{
			gearItem.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, s.Option.Yaw, 0f));
		}
		string obj = $"{s.Option.Scene}_{s.Spot.Item}_{s.Number}_{status}";
		Shot(obj + "_near.png", at + new Vector3(1.1f, 0.9f, 0.6f), at);
		Shot(obj + "_top.png", at + Vector3.up * 28f, at);
		if (gearItem != null)
		{
			UnityEngine.Object.Destroy(gearItem.gameObject);
		}
	}

	private static void Shot(string file, Vector3 from, Vector3 look)
	{
		GameObject gameObject = new GameObject("SeagullSpotCamera");
		RenderTexture renderTexture = new RenderTexture(1280, 720, 24);
		Texture2D texture2D = new Texture2D(1280, 720, TextureFormat.RGB24, mipChain: false);
		try
		{
			Camera camera = gameObject.AddComponent<Camera>();
			camera.transform.position = from;
			camera.transform.LookAt(look);
			camera.fieldOfView = 55f;
			camera.farClipPlane = 2000f;
			camera.targetTexture = renderTexture;
			camera.Render();
			RenderTexture.active = renderTexture;
			texture2D.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
			texture2D.Apply();
			File.WriteAllBytes(Path.Combine(ShotDir, file), texture2D.EncodeToPNG());
		}
		finally
		{
			RenderTexture.active = null;
			UnityEngine.Object.Destroy(texture2D);
			UnityEngine.Object.Destroy(renderTexture);
			UnityEngine.Object.Destroy(gameObject);
		}
	}

	private static void Finish()
	{
		step = Step.Idle;
		List<OnceSpot> list = new List<OnceSpot>();
		foreach (OnceSpot s in OneTimeSpawns.AllAutomatic())
		{
			List<SpotOption> list2 = (from r in results
				where r.Item == s.Item && r.Status == "ok"
				orderby r.Option
				select new SpotOption
				{
					Scene = r.Scene,
					X = r.X,
					Y = r.Y,
					Z = r.Z,
					Yaw = r.Yaw
				}).ToList();
			list.Add(new OnceSpot
			{
				Item = s.Item,
				Options = ((list2.Count > 0) ? list2 : s.Options)
			});
			if (list2.Count == 0)
			{
				MelonLogger.Warning("Spot check: no good spot for " + s.Item + ", the automatic ones stay.");
			}
		}
		File.WriteAllText(Path.Combine(Mod.Root, "data", "spots_checked.json"), JsonConvert.SerializeObject((object)list, (Formatting)1));
		File.WriteAllText(Path.Combine(ShotDir, "report.json"), JsonConvert.SerializeObject((object)results, (Formatting)1));
		string value = string.Join(", ", from r in results
			group r by r.Status into g
			select $"{g.Key} {g.Count()}");
		MelonLogger.Msg($"Spot check done: {results.Count} spots: {value}. Pictures: {ShotDir}. Restart the game to use data/spots_checked.json.");
	}
}
