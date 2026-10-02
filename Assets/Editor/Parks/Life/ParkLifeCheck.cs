using System.Collections.Generic;
using System.Linq;
using DroneDDW.Parks.Levels;
using DroneDDW.Parks.Life;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DroneDDW.Parks.Editor.Life
{
	/// <summary>
	/// Menu: DroneDDW / Parks / Check Park Life (Play Mode). Plays the Park Levels scene for about forty seconds,
	/// stepping through the levels, and reports what moved, what sounded, what sprayed and which creatures were out at
	/// each, plus every error, exception and warning logged. Also runs headless (-batchmode -executeMethod ...Run),
	/// quitting with code 1 on errors. Survives the domain reload of entering Play mode by keeping its state in
	/// SessionState.
	/// </summary>
	[InitializeOnLoad]
	internal static class ParkLifeCheck
	{
		#region Fields

		private const string MenuPath = "DroneDDW/Parks/Check Park Life (Play Mode)";
		private const string ScenePath = "Assets/Scenes/Park Levels.unity";
		private const string RunningKey = "DroneDDW.Parks.ParkLifeCheck.Running";
		private const float TotalSeconds = 40f;

		// When to ask for each level, and when to look at the park
		private static readonly (float Seconds, int Level)[] Requests = { (2f, 4), (12f, 3), (21f, 0), (30f, 2) };
		private static readonly float[] Samples = { 11f, 20f, 29f, 38f };

		private static readonly string[] MoverPrefixes =
		{
			"Park_LV_Drone_", "Park_LV_DeliveryRobot_", "Park_LV_RobotMower_",
		};

		// Errors the XR runtime logs when no headset is connected (as in a headless run): reported, but not failures
		private static readonly string[] HeadsetMissingPrefixes = { "[MetaXRFeature]" };

		private static float _startTime = -1f;
		private static int _nextRequest;
		private static int _nextSample;
		private static readonly List<string> Errors = new List<string>();
		private static readonly List<string> Warnings = new List<string>();
		private static readonly Dictionary<Transform, Vector3> LastPositions = new Dictionary<Transform, Vector3>();

		#endregion

		#region Setup

		static ParkLifeCheck()
		{
			if (!SessionState.GetBool(RunningKey, false))
			{
				return;
			}

			EditorApplication.update += Tick;
			Application.logMessageReceived += HandleLog;
		}

		#endregion

		#region Private

		[MenuItem(MenuPath)]
		private static void Run()
		{
			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}

			EditorSceneManager.OpenScene(ScenePath);
			SessionState.SetBool(RunningKey, true);
			EditorApplication.EnterPlaymode();
		}

		private static void Tick()
		{
			if (!EditorApplication.isPlaying)
			{
				return;
			}

			if (_startTime < 0f)
			{
				_startTime = Time.realtimeSinceStartup;
				Debug.Log("[Parks] Checking the park's life: level 2, then 4, 3, 0 and 2 again.");
				Sample(-1f);
			}

			float elapsed = Time.realtimeSinceStartup - _startTime;
			if (_nextRequest < Requests.Length && elapsed >= Requests[_nextRequest].Seconds)
			{
				ParkLevelController controller = Object.FindFirstObjectByType<ParkLevelController>();
				controller.RequestLevel(Requests[_nextRequest].Level);
				_nextRequest++;
			}

			if (_nextSample < Samples.Length && elapsed >= Samples[_nextSample])
			{
				Sample(elapsed);
				_nextSample++;
			}

			if (elapsed >= TotalSeconds)
			{
				Finish();
			}
		}

		/// <summary>Logs what each kind of mover did since the last look, what is playing and what is spraying.</summary>
		private static void Sample(float elapsed)
		{
			ParkLevelController controller = Object.FindFirstObjectByType<ParkLevelController>();
			List<string> parts = new List<string>();
			foreach (string prefix in MoverPrefixes)
			{
				List<Transform> bodies = Resources.FindObjectsOfTypeAll<Transform>()
					.Where(t => t.name == "Body" && t.parent != null && t.parent.name.StartsWith(prefix)
						&& t.gameObject.scene.IsValid())
					.ToList();
				int shown = 0, moving = 0;
				float meters = 0f;
				foreach (Transform body in bodies)
				{
					// A hidden mover's position means nothing (its wrapper has shrunk away), so only shown ones count
					if (!body.gameObject.activeInHierarchy)
					{
						LastPositions.Remove(body);
						continue;
					}

					shown++;
					if (LastPositions.TryGetValue(body, out Vector3 last))
					{
						float moved = Vector3.Distance(last, body.position);
						meters += moved;
						moving += moved > 0.5f ? 1 : 0;
					}

					LastPositions[body] = body.position;
				}

				parts.Add($"{prefix.Substring(8).TrimEnd('_')} {shown} shown, {moving} moved ({meters:F0} m)");
			}

			AudioSource[] sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
			IEnumerable<string> playing = sources.Where(s => s.isPlaying)
				.GroupBy(s => System.Text.RegularExpressions.Regex.Replace(s.name, @"\s*\d+$", string.Empty))
				.Select(g => $"{g.Key} x{g.Count()}");
			int droplets = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Sum(p => p.particleCount);
			string when = elapsed < 0f ? "start" : $"{elapsed:F0} s";
			Debug.Log($"[Parks] Life check at {when}, level {controller.CurrentLevel}: {string.Join("; ", parts)}; "
				+ $"{droplets} droplets; playing: {string.Join(", ", playing)}");
			Debug.Log($"[Parks] Life check at {when}, creatures: {string.Join(", ", Creatures())}");
		}

		/// <summary>The shown creatures by kind, with the nearest's and farthest's distance from the seat.</summary>
		private static IEnumerable<string> Creatures()
		{
			return Object.FindObjectsByType<ParkCreature>(FindObjectsSortMode.None)
				.GroupBy(c => c.name.Replace("(Clone)", string.Empty).Replace("Park_Life_", string.Empty))
				.OrderBy(g => g.Key)
				.Select(g =>
				{
					float[] meters = g.Select(c => c.transform.position.magnitude).ToArray();
					float lowest = g.Min(c => c.transform.position.y);
					return $"{g.Key} x{meters.Length} ({meters.Min():F0}-{meters.Max():F0} m, lowest {lowest:F1} m)";
				});
		}

		private static void HandleLog(string message, string stackTrace, LogType type)
		{
			bool isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
			if (isError && !HeadsetMissingPrefixes.Any(message.StartsWith))
			{
				Errors.Add($"{message}\n{stackTrace}");
			}
			else if (isError || type == LogType.Warning)
			{
				Warnings.Add(message);
			}
		}

		private static void Finish()
		{
			EditorApplication.update -= Tick;
			Application.logMessageReceived -= HandleLog;
			SessionState.SetBool(RunningKey, false);
			Debug.Log($"[Parks] Life check done: {Errors.Count} errors, {Warnings.Count} warnings.");
			foreach (string error in Errors.Take(10))
			{
				Debug.Log($"[Parks] Life check error: {error}");
			}

			foreach (string warning in Warnings.Distinct().Take(10))
			{
				Debug.Log($"[Parks] Life check warning: {warning}");
			}

			if (Application.isBatchMode)
			{
				EditorApplication.Exit(Errors.Count > 0 ? 1 : 0);
				return;
			}

			EditorApplication.ExitPlaymode();
		}

		#endregion
	}
}
