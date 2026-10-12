using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DroneDDW.Admin;
using DroneDDW.Admin.Sessions;
using DroneDDW.Neighborhood.Experience;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DroneDDW.Experience.Editor
{
	// Menu: DroneDDW > Experience > Check Experience In Play Mode. Plays the Experience scene and runs one session
	// through, fast-forwarded (no lines, steps count as done, no headset needed): the admin menu opens on check-in,
	// participant 104 checks in and the menu closes, the director runs every part, the Neighborhood loads on top and
	// becomes the active scene, the choice is recorded as a row in a temporary sessions folder, the Neighborhood
	// unloads with passthrough back, and the goodbye ends the session, reloading the scene to check-in for the next
	// participant. The real session files are never touched. Reports every step and the errors logged, and also runs
	// headless (-batchmode -executeMethod DroneDDW.Experience.Editor.ExperienceCheck.Run), quitting with code 1 on a
	// failure.
	[InitializeOnLoad]
	internal static class ExperienceCheck
	{
		#region Entities

		private sealed class CheckStep
		{
			internal CheckStep(string name, Func<bool> isReady, Func<string> verify, Action after, float timeoutSeconds)
			{
				Name = name;
				IsReady = isReady;
				Verify = verify;
				After = after;
				TimeoutSeconds = timeoutSeconds;
			}

			internal string Name { get; }

			// The step is checked once this holds (or when it times out)
			internal Func<bool> IsReady { get; }

			// Null when the step went as expected, otherwise what went wrong
			internal Func<string> Verify { get; }

			internal Action After { get; }
			internal float TimeoutSeconds { get; }
		}

		#endregion

		#region Fields

		private const string MenuPath = "DroneDDW/Experience/Check Experience In Play Mode";
		private const string RunningKey = "DroneDDW.Experience.ExperienceCheck.Running";
		private const string ParticipantId = "104";
		private const string NeighborhoodSceneName = "Neighborhood";
		private const float StartDelaySeconds = 1.5f;
		private const float StepTimeoutSeconds = 10f;
		private const float SessionTimeoutSeconds = 60f;

		// Errors the XR runtime logs when no headset is connected (as in a headless run): reported, but not failures
		private static readonly string[] HeadsetMissingPrefixes =
		{
			"[MetaXRFeature]", "Failed to initialize Open XR", "Unable to set TrackingOrigin",
		};

		private static readonly List<CheckStep> Steps = new();
		private static readonly List<string> Failures = new();
		private static readonly List<string> Errors = new();
		private static readonly List<string> Warnings = new();
		private static readonly List<ExperiencePhase> Phases = new();

		private static float StartTime = -1f;
		private static int StepIndex = -1;
		private static float StepStartTime;
		private static string TempFolder;
		private static ExperienceDirector FirstDirector;
		private static string RecordedChoice;

		#endregion

		#region Setup

		static ExperienceCheck()
		{
			if (SessionState.GetBool(RunningKey, false))
			{
				Subscribe();
			}
		}

		#endregion

		#region Private

		[MenuItem(MenuPath)]
		private static void Run()
		{
			if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ExperienceSceneBuilder.ScenePath) == null)
			{
				Debug.LogError($"[Experience] Check: there is no {ExperienceSceneBuilder.ScenePath}; run DroneDDW > "
					+ "Experience > Build Experience Scene first.");
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

			EditorSceneManager.OpenScene(ExperienceSceneBuilder.ScenePath);
			SessionState.SetBool(RunningKey, true);
			Subscribe();
			EditorApplication.EnterPlaymode();
		}

		// Also called when Play mode starts without a domain reload
		private static void Subscribe()
		{
			EditorApplication.update -= Tick;
			EditorApplication.update += Tick;
			Application.logMessageReceived -= HandleLog;
			Application.logMessageReceived += HandleLog;
		}

		private static void Tick()
		{
			if (!EditorApplication.isPlaying)
			{
				return;
			}

			float now = Time.realtimeSinceStartup;
			if (StepIndex < 0)
			{
				if (FindDirector() == null)
				{
					return;
				}

				if (StartTime < 0f)
				{
					StartTime = now;
					return;
				}

				if (now - StartTime < StartDelaySeconds)
				{
					return;
				}

				Prepare();
				StartStep(0, now);
				return;
			}

			TrackPhase();
			CheckStep step = Steps[StepIndex];
			bool isTimedOut = now - StepStartTime >= step.TimeoutSeconds;
			if (!isTimedOut && !step.IsReady())
			{
				return;
			}

			string failure = isTimedOut && !step.IsReady()
				? $"timed out after {step.TimeoutSeconds:0} s"
				: step.Verify?.Invoke();
			if (failure != null)
			{
				Failures.Add($"{step.Name}: {failure}");
				Debug.Log($"[Experience] Check step {StepIndex + 1} failed, {step.Name}: {failure}");
			}
			else
			{
				Debug.Log($"[Experience] Check step {StepIndex + 1} passed: {step.Name}.");
				step.After?.Invoke();
			}

			if (failure == null && StepIndex + 1 < Steps.Count)
			{
				StartStep(StepIndex + 1, now);
			}
			else
			{
				Finish();
			}
		}

		private static void StartStep(int index, float now)
		{
			StepIndex = index;
			StepStartTime = now;
		}

		// The temporary sessions folder and the steps
		private static void Prepare()
		{
			TempFolder = Path.Combine(Path.GetTempPath(), "DroneDDW Experience Check");
			if (Directory.Exists(TempFolder))
			{
				Directory.Delete(TempFolder, true);
			}

			Directory.CreateDirectory(TempFolder);
			FindSession().UseSessionsFolder(TempFolder);
			FindMenu().UseSessionsFolder(TempFolder);
			FirstDirector = FindDirector();
			FirstDirector.IsFastForward = true;
			Debug.Log($"[Experience] Check: sessions go to {TempFolder}.");
			BuildSteps();
		}

		private static void BuildSteps()
		{
			Steps.Clear();
			Add("the admin menu opens on check-in, with the session waiting", () => true, CheckCheckInOpen,
				() => FindSession().StartSession(ParticipantId));
			Add("starting the session closes the check-in", () => !FindMenu().IsCheckingIn, CheckSessionStarted);
			Add("the session runs to the Neighborhood, loaded and active",
				() => FirstDirector != null && FirstDirector.Phase >= ExperiencePhase.NeighborhoodChoice,
				CheckInNeighborhood, timeoutSeconds: SessionTimeoutSeconds);
			Add("the choice is recorded and the Neighborhood unloads", () => FirstDirector == null
				|| FirstDirector.Phase >= ExperiencePhase.Outro, CheckBackInPassthrough,
				timeoutSeconds: SessionTimeoutSeconds);
			Add("the session's row is in the sessions file", () => true, CheckRow);
			Add("the goodbye ends the session, back on check-in", IsReloadedForNext, CheckReloaded,
				timeoutSeconds: SessionTimeoutSeconds);
			Add("every part ran, in order", () => true, CheckPhases);
		}

		private static void Add(string name, Func<bool> isReady, Func<string> verify, Action after = null,
			float timeoutSeconds = StepTimeoutSeconds)
		{
			Steps.Add(new CheckStep(name, isReady, verify, after, timeoutSeconds));
		}

		private static string CheckCheckInOpen()
		{
			AdminMenu menu = FindMenu();
			SessionControl session = FindSession();
			if (!menu.IsCheckingIn || menu.CurrentScreen != menu.CheckInScreen)
			{
				return $"the menu shows {Describe(menu.CurrentScreen)}, checking in: {menu.IsCheckingIn}";
			}

			if (!session.IsCheckingIn || session.HasParticipant)
			{
				return "Session Control isn't waiting for a participant";
			}

			return FirstDirector.Phase == ExperiencePhase.CheckIn ? null : $"the director is at {FirstDirector.Phase}";
		}

		private static string CheckSessionStarted()
		{
			AdminMenu menu = FindMenu();
			SessionControl session = FindSession();
			if (menu.IsOpen)
			{
				return "the menu stayed open";
			}

			return session.ParticipantId == ParticipantId && session.Group == "A"
				? null
				: $"the participant is {session.ParticipantId} in group {session.Group}";
		}

		private static string CheckInNeighborhood()
		{
			Scene neighborhood = SceneManager.GetSceneByName(NeighborhoodSceneName);
			if (!neighborhood.IsValid() || !neighborhood.isLoaded)
			{
				return "the Neighborhood isn't loaded";
			}

			if (SceneManager.GetActiveScene() != neighborhood)
			{
				return $"the active scene is {SceneManager.GetActiveScene().name}";
			}

			NeighborhoodStandaloneRig standaloneRig = Object.FindAnyObjectByType<NeighborhoodStandaloneRig>(
				FindObjectsInactive.Include);
			if (standaloneRig != null && Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
				.Count(camera => camera.isActiveAndEnabled && camera.CompareTag("MainCamera")) > 1)
			{
				return "the Neighborhood's own rig is on next to the Experience's";
			}

			return Object.FindAnyObjectByType<NeighborhoodExperience>() != null
				? null
				: "the Neighborhood has no NeighborhoodExperience";
		}

		private static string CheckBackInPassthrough()
		{
			if (FirstDirector == null)
			{
				return "the scene reloaded before the outro";
			}

			RecordedChoice = FirstDirector.RecordedChoice;
			if (string.IsNullOrEmpty(RecordedChoice))
			{
				return "no choice was recorded";
			}

			Scene neighborhood = SceneManager.GetSceneByName(NeighborhoodSceneName);
			return neighborhood.IsValid() && neighborhood.isLoaded ? "the Neighborhood is still loaded" : null;
		}

		private static string CheckRow()
		{
			string path = Path.Combine(TempFolder, "Sessions.csv");
			if (!File.Exists(path))
			{
				return $"there is no {path}";
			}

			List<string[]> rows = CsvUtil.Parse(File.ReadAllText(path), ';');
			if (rows.Count != 2)
			{
				return $"the file has {rows.Count} rows, expected the header and one session";
			}

			string header = string.Join("|", rows[0]);
			if (header != "Id|Group|Form choice|Date|Time")
			{
				return $"the header is {header}";
			}

			string[] row = rows[1];
			string date = DateTime.Now.ToString("yyyy-MM-dd");
			bool isTime = row.Length > 4 && TimeSpan.TryParse(row[4], out _);
			return row.Length == 5 && row[0] == ParticipantId && row[1] == "A" && row[2] == RecordedChoice
				&& row[3] == date && isTime
				? null
				: $"the row is {string.Join("|", row)}, expected {ParticipantId}|A|{RecordedChoice}|{date}|<time>";
		}

		// A new director (the scene reloaded) that waits at check-in
		private static bool IsReloadedForNext()
		{
			ExperienceDirector director = FindDirector();
			return director != null && director != FirstDirector && FindMenu() != null && FindMenu().IsCheckingIn;
		}

		private static string CheckReloaded()
		{
			SessionControl session = FindSession();
			if (session.HasParticipant)
			{
				return $"participant {session.ParticipantId} carried over";
			}

			AdminMenu menu = FindMenu();
			if (menu.CurrentScreen != menu.CheckInScreen)
			{
				return $"the menu shows {Describe(menu.CurrentScreen)}";
			}

			return SceneManager.sceneCount == 1 ? null : $"{SceneManager.sceneCount} scenes are loaded";
		}

		private static string CheckPhases()
		{
			ExperiencePhase[] expected = (ExperiencePhase[])Enum.GetValues(typeof(ExperiencePhase));
			List<ExperiencePhase> missing = expected.Where(phase => !Phases.Contains(phase)).ToList();
			if (missing.Count > 0)
			{
				return $"never reached {string.Join(", ", missing)}";
			}

			bool isOrdered = Phases.Zip(Phases.Skip(1), (first, second) => first <= second).All(isInOrder => isInOrder);
			return isOrdered ? null : $"the parts ran as {string.Join(" > ", Phases)}";
		}

		// The first session's parts as they pass (the check samples each frame, so a part that starts and ends in the
		// same frame is taken from the director's log instead)
		private static void TrackPhase()
		{
			if (FirstDirector != null && (Phases.Count == 0 || Phases[^1] != FirstDirector.Phase))
			{
				AddPhase(FirstDirector.Phase);
			}
		}

		private static void AddPhase(ExperiencePhase phase)
		{
			if (!Phases.Contains(phase))
			{
				Phases.Add(phase);
			}
		}

		private static ExperienceDirector FindDirector()
		{
			return Object.FindAnyObjectByType<ExperienceDirector>();
		}

		private static AdminMenu FindMenu()
		{
			return Object.FindAnyObjectByType<AdminMenu>();
		}

		private static SessionControl FindSession()
		{
			return Object.FindAnyObjectByType<SessionControl>();
		}

		private static string Describe(AdminMenuScreen screen)
		{
			return screen != null ? screen.name : "none";
		}

		private static void HandleLog(string message, string stackTrace, LogType type)
		{
			const string phasePrefix = "[Experience] ";
			if (type == LogType.Log && message.StartsWith(phasePrefix)
				&& Enum.TryParse(message.Substring(phasePrefix.Length), out ExperiencePhase phase)
				&& StepIndex >= 0 && (FirstDirector != null || Phases.Count == 0))
			{
				AddPhase(phase);
			}

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
			if (Directory.Exists(TempFolder))
			{
				Directory.Delete(TempFolder, true);
			}

			foreach (string failure in Failures)
			{
				Debug.Log($"[Experience] Check failure: {failure}");
			}

			foreach (string error in Errors.Take(10))
			{
				Debug.Log($"[Experience] Check error: {error}");
			}

			foreach (string warning in Warnings.Distinct().Take(15))
			{
				Debug.Log($"[Experience] Check warning: {warning}");
			}

			bool isPassed = Failures.Count == 0 && Errors.Count == 0;
			Debug.Log($"[Experience] Check done: {(isPassed ? "passed" : "failed")}, {Steps.Count} steps, "
				+ $"{Failures.Count} failures, {Errors.Count} errors, {Warnings.Count} warnings.");
			if (Application.isBatchMode)
			{
				EditorApplication.Exit(isPassed ? 0 : 1);
				return;
			}

			EditorApplication.ExitPlaymode();
		}

		#endregion
	}
}
