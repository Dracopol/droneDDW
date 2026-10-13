using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DroneDDW.Experience.Editor
{
	// Menu: DroneDDW > Experience > Check Subtitles In Play Mode. Plays the Experience scene, shows the subtitle of an
	// orb line and of a card's introduction, and checks the strip is up with the right words (and the speaker's name for
	// the card), and that it is drawn over a solid wall put between it and the eyes. With "-subtitleCaptures <folder>" on the command line it saves what the head camera sees each time.
	// Runs headless too: -batchmode -executeMethod DroneDDW.Experience.Editor.SubtitleCheck.Run (no -quit), quitting
	// with code 1 on a failure.
	[InitializeOnLoad]
	internal static class SubtitleCheck
	{
		#region Fields

		private const string MenuPath = "DroneDDW/Experience/Check Subtitles In Play Mode";
		private const string RunningKey = "DroneDDW.Experience.SubtitleCheck.Running";
		private const string CapturesKey = "DroneDDW.Experience.SubtitleCheck.Captures";
		private const string CapturesArgument = "-subtitleCaptures";
		private const float StartDelaySeconds = 2f;
		private const int CaptureSize = 1200;
		private const float CaptureFieldOfViewDegrees = 90f;
		private const float WallLerp = 0.5f;
		private const float MaxBackgroundBrightness = 0.25f;
		private const float MinWallContrast = 0.15f;

		// In the strip's local space: inside its bottom-left padding (the strip is 0.9 x 0.14 m)
		private static readonly Vector3 PaddingPoint = new(-0.43f, -0.06f, 0f);
		private static readonly Vector3 WallSize = new(1.2f, 0.4f, 0.05f);

		private static float StartTime = -1f;

		#endregion

		#region Setup

		static SubtitleCheck()
		{
			if (SessionState.GetBool(RunningKey, false))
			{
				EditorApplication.update -= Tick;
				EditorApplication.update += Tick;
			}
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

			string[] arguments = Environment.GetCommandLineArgs();
			int index = Array.IndexOf(arguments, CapturesArgument);
			SessionState.SetString(CapturesKey, index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : string.Empty);
			StartTime = -1f;
			EditorSceneManager.OpenScene(ExperienceSceneBuilder.ScenePath);
			SessionState.SetBool(RunningKey, true);
			EditorApplication.update -= Tick;
			EditorApplication.update += Tick;
			EditorApplication.EnterPlaymode();
		}

		private static void Tick()
		{
			if (!EditorApplication.isPlaying)
			{
				return;
			}

			if (StartTime < 0f)
			{
				StartTime = Time.realtimeSinceStartup;
				return;
			}

			if (Time.realtimeSinceStartup - StartTime < StartDelaySeconds)
			{
				return;
			}

			EditorApplication.update -= Tick;
			SessionState.SetBool(RunningKey, false);
			string failure = null;
			try
			{
				failure = Check(SessionState.GetString(CapturesKey, string.Empty));
			}
			catch (Exception exception)
			{
				failure = exception.Message;
				Debug.LogException(exception);
			}

			Debug.Log($"[Experience] Subtitle check done: {(failure == null ? "passed" : "failed, " + failure)}.");
			if (Application.isBatchMode)
			{
				EditorApplication.Exit(failure == null ? 0 : 1);
				return;
			}

			EditorApplication.ExitPlaymode();
		}

		private static string Check(string folder)
		{
			ExperienceSubtitles subtitles = Object.FindAnyObjectByType<ExperienceSubtitles>();
			ExperienceSettings settings = AssetDatabase.LoadAssetAtPath<ExperienceSettings>(ExperienceSceneBuilder.SettingsPath);
			NoticePanel panel = FindPanel();
			if (subtitles == null || settings == null || panel == null)
			{
				return "the scene has no subtitles (rebuild the Experience scene)";
			}

			int ticket = subtitles.ShowClip(settings.Welcome);
			if (ticket == 0 || !panel.IsShown || !panel.Text.Contains("welcome", StringComparison.OrdinalIgnoreCase))
			{
				return $"the welcome's subtitle isn't up (shown: {panel.IsShown}, text: {panel.Text})";
			}

			Debug.Log($"[Experience] Subtitle check: the welcome shows \"{panel.Text}\".");
			Capture(folder, "orb line.png");
			subtitles.Hide(ticket);
			if (panel.IsShown)
			{
				return "the welcome's subtitle stayed up after its line";
			}

			ticket = subtitles.ShowClip(settings.GetCharacterLine(0));
			if (ticket == 0 || !panel.Text.Contains("Grandma:"))
			{
				return $"the first card's subtitle has no \"Grandma:\" (text: {panel.Text})";
			}

			Debug.Log($"[Experience] Subtitle check: the first card shows \"{panel.Text}\".");
			Capture(folder, "card line.png");

			// A solid wall, the table and the orb right in front of the strip: it must still be drawn over all of them. A
			// point inside the strip's bottom-left padding must show the dark background, not the wall behind it
			GameObject wall = PutInFrontOfSubtitles(panel);
			Vector3 padding = panel.transform.TransformPoint(PaddingPoint);
			Color overWall = Capture(folder, "over the table and the orb.png", padding);
			subtitles.Hide(ticket);
			Color wallAlone = Capture(folder, "the wall without the strip.png", padding);
			Object.Destroy(wall);
			Debug.Log($"[Experience] Subtitle check: the strip over the wall {overWall}, the wall alone {wallAlone}.");
			if (overWall.maxColorComponent > MaxBackgroundBrightness || wallAlone.maxColorComponent < overWall.maxColorComponent + MinWallContrast)
			{
				return $"something in front covers the strip (strip over the wall {overWall}, wall alone {wallAlone})";
			}

			return null;
		}

		private static GameObject PutInFrontOfSubtitles(NoticePanel panel)
		{
			GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
			wall.name = "Subtitle check wall";
			Object.Destroy(wall.GetComponent<Collider>());
			foreach (ExperienceProp prop in Object.FindObjectsByType<ExperienceProp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				prop.Show();
			}

			Camera head = Camera.main;
			NarratorOrb.NarratorOrbController orb = Object.FindAnyObjectByType<NarratorOrb.NarratorOrbController>();
			if (head != null && orb != null)
			{
				orb.SetVisible(true);
				orb.MoveTo(Vector3.Lerp(head.transform.position, panel.transform.position, 0.6f));
			}

			wall.transform.SetPositionAndRotation(Vector3.Lerp(head != null ? head.transform.position : Vector3.zero,
				panel.transform.position, WallLerp), panel.transform.rotation);
			wall.transform.localScale = WallSize;
			return wall;
		}

		private static NoticePanel FindPanel()
		{
			foreach (NoticePanel panel in Object.FindObjectsByType<NoticePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				if (panel.name == "Subtitles")
				{
					return panel;
				}
			}

			return null;
		}

		// What the head camera sees, from its pose, into a PNG (with a folder), and its colour at a point in the world
		private static Color Capture(string folder, string fileName, Vector3 point = default)
		{
			Camera head = Camera.main;
			if (head == null)
			{
				return Color.clear;
			}

			Color sample;
			GameObject cameraObject = new("Subtitle check camera");
			RenderTexture target = RenderTexture.GetTemporary(CaptureSize, CaptureSize, 24, RenderTextureFormat.ARGB32);
			RenderTexture previous = RenderTexture.active;
			try
			{
				Camera camera = cameraObject.AddComponent<Camera>();
				camera.enabled = false;
				camera.CopyFrom(head);
				camera.fieldOfView = CaptureFieldOfViewDegrees;
				camera.clearFlags = CameraClearFlags.SolidColor;
				camera.backgroundColor = new Color(0.35f, 0.38f, 0.42f, 1f);
				cameraObject.transform.SetPositionAndRotation(head.transform.position, head.transform.rotation);
				RenderPipeline.StandardRequest request = new() { destination = target };
				if (RenderPipeline.SupportsRenderRequest(camera, request))
				{
					RenderPipeline.SubmitRenderRequest(camera, request);
				}
				else
				{
					camera.targetTexture = target;
					camera.Render();
					camera.targetTexture = null;
				}

				RenderTexture.active = target;
				Texture2D image = new(CaptureSize, CaptureSize, TextureFormat.RGB24, false);
				image.ReadPixels(new Rect(0, 0, CaptureSize, CaptureSize), 0, 0);
				image.Apply();
				Vector3 viewport = camera.WorldToViewportPoint(point);
				sample = image.GetPixel((int)(viewport.x * CaptureSize), (int)(viewport.y * CaptureSize));
				if (!string.IsNullOrEmpty(folder))
				{
					Directory.CreateDirectory(folder);
					File.WriteAllBytes(Path.Combine(folder, fileName), image.EncodeToPNG());
				}

				Object.Destroy(image);
			}
			finally
			{
				RenderTexture.active = previous;
				RenderTexture.ReleaseTemporary(target);
				Object.Destroy(cameraObject);
			}

			return sample;
		}

		#endregion
	}
}
