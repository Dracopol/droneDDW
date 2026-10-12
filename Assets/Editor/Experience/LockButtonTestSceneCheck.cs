using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DroneDDW.Experience.TestScene;
using DroneDDW.Neighborhood.Table;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DroneDDW.Experience.Editor
{
	// Menu: DroneDDW > Experience > Check Lock Button Test Scenes. For each lock button test scene: checks that the
	// button's collar stays clear of the slider's track and end glyphs (and, in the corner, on the table's top), saves a
	// preview of the table from the seat and a close one of the button in the temp folder, then plays the scene and checks
	// that it starts without errors from our code and that the readout shows the button ready on the slider's notch.
	// Without a headset no hands push anything: what the XR runtime logs about the missing headset is reported, not
	// failed. Also runs headless (-batchmode -executeMethod DroneDDW.Experience.Editor.LockButtonTestSceneCheck.Run,
	// without -quit), quitting with code 1 on a failure.
	[InitializeOnLoad]
	internal static class LockButtonTestSceneCheck
	{
		#region Fields

		private const string MenuPath = "DroneDDW/Experience/Check Lock Button Test Scenes";
		private const string RunningKey = "DroneDDW.Experience.LockButtonCheck.Running";
		private const string SceneIndexKey = "DroneDDW.Experience.LockButtonCheck.SceneIndex";
		private const string FailuresKey = "DroneDDW.Experience.LockButtonCheck.Failures";
		private const string WarningsKey = "DroneDDW.Experience.LockButtonCheck.Warnings";
		private const string CodeMarker = "DroneDDW.";
		private const string LogMarker = "[Experience]";
		private const char Separator = '\u001f';
		private const float StartupSeconds = 3f;
		private const int ReportedWarnings = 15;
		private const int PreviewWidth = 1280;
		private const int PreviewHeight = 720;

		private static readonly string[] SceneNames =
		{
			LockButtonTestSceneBuilder.CornerSceneName,
			LockButtonTestSceneBuilder.StandSceneName,
		};

		private static readonly List<string> Failures = new();
		private static readonly List<string> Warnings = new();

		private static float StartTime = -1f;
		private static bool IsSceneChecked;

		#endregion

		#region Setup

		// Play mode may reload the scripts: carry on with the check afterwards
		static LockButtonTestSceneCheck()
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
			if (next < SceneNames.Length)
			{
				SessionState.SetInt(SceneIndexKey, next);
				StartScene(next);
				return;
			}

			Finish();
		}

		// Errors from our code fail the check; everything else (e.g. the missing headset) is reported
		private static void HandleLog(string message, string stackTrace, LogType type)
		{
			bool isError = type is LogType.Error or LogType.Exception or LogType.Assert;
			if (!isError && type != LogType.Warning)
			{
				return;
			}

			bool isOurs = message.StartsWith(LogMarker) || message.StartsWith("[Neighborhood]")
				|| (stackTrace != null && stackTrace.Contains(CodeMarker));
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
			foreach (string sceneName in SceneNames)
			{
				string scenePath = LockButtonTestSceneBuilder.GetScenePath(sceneName);
				if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
				{
					continue;
				}

				Debug.LogError($"{LogMarker} Check: there is no {scenePath}; run DroneDDW > Experience > Build Lock Button "
					+ "Test Scenes first.");
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
			string sceneName = SceneNames[index];
			EditorSceneManager.OpenScene(LockButtonTestSceneBuilder.GetScenePath(sceneName));
			Subscribe();
			CheckLayout(sceneName, index == 0);
			SavePreviews(sceneName);
			Append(FailuresKey, Failures);
			Append(WarningsKey, Warnings);
			Failures.Clear();
			Warnings.Clear();
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

		// Lets the scene run for a few seconds, then checks the readout and leaves Play mode
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
			CheckReadout(SceneManager.GetActiveScene().name);
			Append(FailuresKey, Failures);
			Append(WarningsKey, Warnings);
			EditorApplication.ExitPlaymode();
		}

		// The collar's footprint against the slider's track and glyphs, and the table's top for the corner button
		private static void CheckLayout(string sceneName, bool isOnTable)
		{
			NeighborhoodLockButton button = Object.FindAnyObjectByType<NeighborhoodLockButton>();
			Renderer collar = button != null ? button.transform.Find("Collar")?.GetComponent<Renderer>() : null;
			Renderer table = FindRenderer("Rig_Table");
			Renderer track = FindRenderer("Rig_Track");
			if (collar == null || table == null || track == null)
			{
				Failures.Add($"{sceneName}: the scene lacks the button's collar, the table or the slider's track");
				return;
			}

			Bounds footprint = collar.bounds;
			IEnumerable<Renderer> sliderParts = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
				.Where(part => part.name.StartsWith("Rig_Glyph_") || part == track);
			foreach (Renderer part in sliderParts)
			{
				Expect(sceneName, !OverlapsFromAbove(footprint, part.bounds),
					$"the button to stay clear of {part.name} (button x {footprint.min.x:0.###} to {footprint.max.x:0.###}, "
					+ $"{part.name} x {part.bounds.min.x:0.###} to {part.bounds.max.x:0.###})");
			}

			if (isOnTable)
			{
				Bounds top = table.bounds;
				Expect(sceneName, footprint.min.x >= top.min.x && footprint.max.x <= top.max.x
					&& footprint.min.z >= top.min.z && footprint.max.z <= top.max.z,
					$"the button to stay on the table's top (button x to {footprint.max.x:0.###}, table to {top.max.x:0.###})");
			}
			else
			{
				Renderer stand = FindRenderer("Lock Button Stand");
				Expect(sceneName, stand != null && !OverlapsFromAbove(stand.bounds, table.bounds),
					"a stand for the button beside the table, clear of it");
			}

			Debug.Log($"{LogMarker} Check {sceneName}: the button's collar spans x {footprint.min.x:0.###} to "
				+ $"{footprint.max.x:0.###} m, {footprint.size.x * 100f:0.#} cm wide.");
		}

		// Readable once the scene runs: the slider rests on its notch and the button is ready for a press
		private static void CheckReadout(string sceneName)
		{
			LockButtonTestReadout readout = Object.FindAnyObjectByType<LockButtonTestReadout>();
			TMP_Text label = readout != null ? readout.GetComponentInChildren<TMP_Text>() : null;
			if (label == null)
			{
				Failures.Add($"{sceneName}: the scene lacks its readout");
				return;
			}

			Debug.Log($"{LogMarker} Check {sceneName}: the readout says \"{label.text.Replace('\n', '|')}\"");
			Expect(sceneName, label.text.Contains("Slider: Balanced") && label.text.Contains("Button: ready"),
				$"the readout to show the slider on Balanced and the button ready, not \"{label.text}\"");
		}

		// From the seat's eyes towards the button, and a close three-quarter view of it, as PNGs in the temp folder
		private static void SavePreviews(string sceneName)
		{
			NeighborhoodLockButton button = Object.FindAnyObjectByType<NeighborhoodLockButton>();
			Camera head = Camera.main;
			if (button == null || head == null)
			{
				return;
			}

			string folder = Path.Combine(Path.GetTempPath(), "DroneDDW Lock Button Check");
			Directory.CreateDirectory(folder);
			Vector3 target = button.transform.position;
			Vector3 eyes = head.transform.position;
			SavePreview(Path.Combine(folder, $"{sceneName} - seat.png"), eyes, target + Vector3.left * 0.2f, 60f);
			Vector3 close = target + new Vector3(-0.18f, 0.2f, -0.22f);
			SavePreview(Path.Combine(folder, $"{sceneName} - close.png"), close, target, 40f);
			Debug.Log($"{LogMarker} Check {sceneName}: previews saved in {folder}");
		}

		private static void SavePreview(string path, Vector3 from, Vector3 target, float fieldOfView)
		{
			GameObject cameraObject = new("Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
			RenderTexture texture = new(PreviewWidth, PreviewHeight, 24);
			Texture2D image = new(PreviewWidth, PreviewHeight, TextureFormat.RGB24, false);
			try
			{
				Camera camera = cameraObject.AddComponent<Camera>();
				camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(target - from));
				camera.fieldOfView = fieldOfView;
				camera.nearClipPlane = 0.01f;
				camera.clearFlags = CameraClearFlags.SolidColor;
				camera.backgroundColor = new Color(0.55f, 0.57f, 0.6f);
				camera.targetTexture = texture;
				camera.Render();
				RenderTexture previous = RenderTexture.active;
				RenderTexture.active = texture;
				image.ReadPixels(new Rect(0, 0, PreviewWidth, PreviewHeight), 0, 0);
				image.Apply();
				RenderTexture.active = previous;
				File.WriteAllBytes(path, image.EncodeToPNG());
			}
			finally
			{
				Object.DestroyImmediate(cameraObject);
				Object.DestroyImmediate(texture);
				Object.DestroyImmediate(image);
			}
		}

		private static Renderer FindRenderer(string objectName)
		{
			return Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
				.FirstOrDefault(candidate => candidate.name == objectName);
		}

		// Seen from above: whether two boxes share any ground
		private static bool OverlapsFromAbove(Bounds first, Bounds second)
		{
			return first.min.x < second.max.x && second.min.x < first.max.x
				&& first.min.z < second.max.z && second.min.z < first.max.z;
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
			Debug.Log($"{LogMarker} Lock button check done: {(isPassed ? "passed" : "failed")}, {SceneNames.Length} scenes, "
				+ $"{failures.Length} failures, {warnings.Length} warnings.");
			if (Application.isBatchMode)
			{
				EditorApplication.Exit(isPassed ? 0 : 1);
			}
		}

		#endregion
	}
}
