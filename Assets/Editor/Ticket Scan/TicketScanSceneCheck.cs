using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DroneDDW.TicketScan.TestScene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DroneDDW.TicketScan.Editor
{
	// Menu: DroneDDW > Ticket Scan > Check Test Scenes In Play Mode. Plays the camera test scene, then the tracker one,
	// and checks in each that it starts without errors from the ticket scan code and that the panel shows the
	// scanner's state; then it hands the scanner a ticket read and another QR code, checks both reach the panel and its
	// log, and that Clear empties them. Without a headset the scanners only get as far as waiting for the camera or the
	// tracker: what the XR runtime and Meta's SDK log about that is reported, not failed. Also runs headless
	// (-batchmode -executeMethod DroneDDW.TicketScan.Editor.TicketScanSceneCheck.Run, without -quit), quitting with
	// code 1 on a failure.
	[InitializeOnLoad]
	internal static class TicketScanSceneCheck
	{
		#region Fields

		private const string MenuPath = "DroneDDW/Ticket Scan/Check Test Scenes In Play Mode";
		private const string RunningKey = "DroneDDW.TicketScan.Check.Running";
		private const string SceneIndexKey = "DroneDDW.TicketScan.Check.SceneIndex";
		private const string FailuresKey = "DroneDDW.TicketScan.Check.Failures";
		private const string WarningsKey = "DroneDDW.TicketScan.Check.Warnings";
		private const string CodeMarker = "DroneDDW.TicketScan";
		private const string LogMarker = "[Ticket Scan]";
		private const char Separator = '\u001f';
		private const float StartupSeconds = 3f;
		private const int ReportedWarnings = 15;

		private const string TicketPayload = "https://q.crowdtech.com/CheckSurvey?code=104";
		private const string OtherPayload = "https://example.com/not-a-ticket";

		private static readonly string[] ScenePaths =
		{
			TicketScanTestSceneBuilder.CameraScenePath,
			TicketScanTestSceneBuilder.TrackerScenePath,
		};

		private static readonly List<string> Failures = new();
		private static readonly List<string> Warnings = new();

		private static float StartTime = -1f;
		private static bool IsSceneChecked;

		#endregion

		#region Setup

		// Play mode may reload the scripts: carry on with the check afterwards
		static TicketScanSceneCheck()
		{
			if (SessionState.GetBool(RunningKey, false))
			{
				Subscribe();
			}
		}

		#endregion

		#region Event handlers

		private static void HandlePlayModeChange(PlayModeStateChange change)
		{
			if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RunningKey, false))
			{
				return;
			}

			int next = SessionState.GetInt(SceneIndexKey, 0) + 1;
			if (next < ScenePaths.Length)
			{
				SessionState.SetInt(SceneIndexKey, next);
				StartScene(next);
				return;
			}

			Finish();
		}

		// Errors from the ticket scan code fail the check; everything else (e.g. the missing headset) is reported
		private static void HandleLog(string message, string stackTrace, LogType type)
		{
			bool isError = type is LogType.Error or LogType.Exception or LogType.Assert;
			if (!isError && type != LogType.Warning)
			{
				return;
			}

			bool isOurs = message.StartsWith(LogMarker) || (stackTrace != null && stackTrace.Contains(CodeMarker));
			string firstLine = message.Split('\n')[0];
			if (isError && isOurs)
			{
				Failures.Add($"{SceneManager.GetActiveScene().name}: {firstLine}\n{stackTrace}");
			}
			else
			{
				Warnings.Add(firstLine);
			}
		}

		#endregion

		#region Private

		[MenuItem(MenuPath)]
		private static void Run()
		{
			foreach (string scenePath in ScenePaths)
			{
				if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
				{
					continue;
				}

				Debug.LogError($"{LogMarker} Check: there is no {scenePath}; build it with the DroneDDW > Ticket Scan "
					+ "menu first.");
				if (Application.isBatchMode)
				{
					EditorApplication.Exit(1);
				}

				return;
			}

			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				return;
			}

			SessionState.SetBool(RunningKey, true);
			SessionState.SetInt(SceneIndexKey, 0);
			SessionState.EraseString(FailuresKey);
			SessionState.EraseString(WarningsKey);
			StartScene(0);
		}

		private static void StartScene(int index)
		{
			Failures.Clear();
			Warnings.Clear();
			StartTime = -1f;
			IsSceneChecked = false;
			EditorSceneManager.OpenScene(ScenePaths[index]);
			Subscribe();
			EditorApplication.EnterPlaymode();
		}

		private static void Subscribe()
		{
			EditorApplication.update -= Tick;
			EditorApplication.update += Tick;
			EditorApplication.playModeStateChanged -= HandlePlayModeChange;
			EditorApplication.playModeStateChanged += HandlePlayModeChange;
			Application.logMessageReceived -= HandleLog;
			Application.logMessageReceived += HandleLog;
		}

		private static void Unsubscribe()
		{
			EditorApplication.update -= Tick;
			EditorApplication.playModeStateChanged -= HandlePlayModeChange;
			Application.logMessageReceived -= HandleLog;
		}

		// Lets the scene run for a few seconds, then checks it and leaves Play mode
		private static void Tick()
		{
			if (!EditorApplication.isPlaying || IsSceneChecked)
			{
				return;
			}

			float now = Time.realtimeSinceStartup;
			if (StartTime < 0f)
			{
				StartTime = now;
				return;
			}

			if (now - StartTime < StartupSeconds)
			{
				return;
			}

			IsSceneChecked = true;
			CheckScene(SceneManager.GetActiveScene().name);
			Append(FailuresKey, Failures);
			Append(WarningsKey, Warnings);
			EditorApplication.ExitPlaymode();
		}

		private static void CheckScene(string sceneName)
		{
			TicketScanTestControls controls = Object.FindAnyObjectByType<TicketScanTestControls>();
			TicketScanPanel panel = Object.FindAnyObjectByType<TicketScanPanel>();
			TicketScanner scanner = Object.FindAnyObjectByType<TicketScanner>();
			if (controls == null || panel == null || scanner == null)
			{
				Failures.Add($"{sceneName}: the scene lacks its test controls, panel or scanner");
				return;
			}

			Debug.Log($"{LogMarker} Check {sceneName}: the scanner says \"{panel.StatusText}\"");
			Expect(sceneName, panel.StatusText.Length > 0, "the panel shows the scanner's state");
			Expect(sceneName, panel.NumberText == "---", $"the panel waits for a code, not \"{panel.NumberText}\"");

			RaiseRead(scanner, new TicketRead(TicketPayload, 0.21f, 3.1f, 38f, Time.realtimeSinceStartup));
			Expect(sceneName, panel.NumberText == "104", $"a ticket read shows 104, not \"{panel.NumberText}\"");
			Expect(sceneName, panel.LogText.Contains("ticket 104 · ~21 cm · 3.1 px/module · 38 ms"),
				$"the log lists the ticket read, not \"{panel.LogText}\"");

			RaiseRead(scanner, new TicketRead(OtherPayload, float.NaN, float.NaN, float.NaN,
				Time.realtimeSinceStartup));
			Expect(sceneName, panel.NumberText == "Not a ticket",
				$"another QR code shows as not a ticket, not \"{panel.NumberText}\"");
			Expect(sceneName, panel.LogText.Split('\n').Length == 2 && panel.LogText.Contains("not a ticket"),
				$"the log lists both reads, not \"{panel.LogText}\"");

			controls.Clear();
			Expect(sceneName, panel.NumberText == "---" && panel.LogText.Length == 0,
				$"Clear empties the panel and its log, not \"{panel.NumberText}\" / \"{panel.LogText}\"");
		}

		// Hands the scanner a read as if it had seen the code itself
		private static void RaiseRead(TicketScanner scanner, TicketRead read)
		{
			MethodInfo raiseRead = typeof(TicketScanner).GetMethod("RaiseRead",
				BindingFlags.Instance | BindingFlags.NonPublic);
			if (raiseRead == null)
			{
				throw new MissingMethodException(nameof(TicketScanner), "RaiseRead");
			}

			raiseRead.Invoke(scanner, new object[] { read });
		}

		private static void Expect(string sceneName, bool isMet, string expectation)
		{
			if (!isMet)
			{
				Failures.Add($"{sceneName}: expected {expectation}");
			}
		}

		// Results go to the session state, which outlives the script reloads between scenes
		private static void Append(string key, List<string> lines)
		{
			IEnumerable<string> saved = Split(SessionState.GetString(key, string.Empty));
			SessionState.SetString(key, string.Join(Separator.ToString(), saved.Concat(lines)));
		}

		private static string[] Split(string text)
		{
			return text.Length == 0 ? Array.Empty<string>() : text.Split(Separator);
		}

		private static void Finish()
		{
			Unsubscribe();
			SessionState.SetBool(RunningKey, false);
			string[] failures = Split(SessionState.GetString(FailuresKey, string.Empty));
			string[] warnings = Split(SessionState.GetString(WarningsKey, string.Empty));
			foreach (string warning in warnings.Distinct().Take(ReportedWarnings))
			{
				Debug.Log($"{LogMarker} Check warning: {warning}");
			}

			foreach (string failure in failures)
			{
				Debug.Log($"{LogMarker} Check failure: {failure}");
			}

			bool isPassed = failures.Length == 0;
			Debug.Log($"{LogMarker} Check done: {(isPassed ? "passed" : "failed")}, {ScenePaths.Length} scenes, "
				+ $"{failures.Length} failures, {warnings.Length} warnings.");
			if (Application.isBatchMode)
			{
				EditorApplication.Exit(isPassed ? 0 : 1);
			}
		}

		#endregion
	}
}
