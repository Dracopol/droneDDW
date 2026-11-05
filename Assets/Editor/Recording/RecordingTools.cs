using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace DroneDDW.Recording.Editor
{
	/// <summary>
	/// Menu tools for recording gameplay footage.
	/// Tools/Recording/Toggle Recording (F9): in Play mode, spawns a smoothed spectator camera that follows the XR head
	/// (and survives scene loads) and records it to a 1080p60 MP4. Nothing needs to be placed in scenes.
	/// </summary>
	[InitializeOnLoad]
	public static class RecordingTools
	{
		#region Fields

		private const string RecordingsFolderName = "Recordings";
		private const int OutputWidthPixels = 1920;
		private const int OutputHeightPixels = 1080;
		private const float FrameRate = 60f;
		private const float FieldOfViewDegrees = 80f;
		private const float PositionSmoothingSeconds = 0.1f;
		private const float RotationSmoothingSeconds = 0.3f;

		private static RecorderController RecorderController;
		private static RecordingCamera ActiveRecordingCamera;
		private static string CurrentOutputPath;

		#endregion

		#region Setup

		static RecordingTools()
		{
			EditorApplication.playModeStateChanged += HandlePlayModeStateChange;
		}

		#endregion

		#region Event handlers

		private static void HandlePlayModeStateChange(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.ExitingPlayMode)
			{
				StopRecording();
			}
		}

		#endregion

		#region Private

		[MenuItem("Tools/Recording/Toggle Recording _F9")]
		private static void ToggleRecording()
		{
			if (RecorderController != null && RecorderController.IsRecording())
			{
				StopRecording();
				return;
			}

			StartRecording();
		}

		[MenuItem("Tools/Recording/Toggle Recording _F9", true)]
		private static bool ValidateToggleRecording()
		{
			return EditorApplication.isPlaying;
		}

		[MenuItem("Tools/Recording/Open Recordings Folder")]
		private static void OpenRecordingsFolder()
		{
			string folderPath = GetRecordingsFolderPath();
			Directory.CreateDirectory(folderPath);
			EditorUtility.RevealInFinder(folderPath + Path.DirectorySeparatorChar);
		}

		[MenuItem("Tools/Recording/Open Recorder Window")]
		private static void OpenRecorderWindow()
		{
			EditorWindow.GetWindow<RecorderWindow>("Recorder");
		}

		private static void StartRecording()
		{
			ActiveRecordingCamera = RecordingCamera.Create(
				OutputWidthPixels,
				OutputHeightPixels,
				FieldOfViewDegrees,
				PositionSmoothingSeconds,
				RotationSmoothingSeconds);

			RecorderControllerSettings controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
			controllerSettings.SetRecordModeToManual();
			controllerSettings.FrameRate = FrameRate;
			controllerSettings.FrameRatePlayback = FrameRatePlayback.Variable;
			controllerSettings.CapFrameRate = false;
			controllerSettings.ExitPlayMode = false;

			MovieRecorderSettings movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
			movieSettings.name = "Gameplay";
			movieSettings.Enabled = true;
			movieSettings.CaptureAudio = true;
			movieSettings.EncoderSettings = new CoreEncoderSettings
			{
				Codec = CoreEncoderSettings.OutputCodec.MP4,
				EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
			};
			movieSettings.ImageInputSettings = new RenderTextureInputSettings
			{
				RenderTexture = ActiveRecordingCamera.RenderTexture,
				OutputWidth = OutputWidthPixels,
				OutputHeight = OutputHeightPixels,
			};

			string folderPath = GetRecordingsFolderPath();
			Directory.CreateDirectory(folderPath);
			CurrentOutputPath = Path.Combine(folderPath, $"Gameplay_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
			movieSettings.OutputFile = CurrentOutputPath;

			controllerSettings.AddRecorderSettings(movieSettings);

			RecorderController = new RecorderController(controllerSettings);
			RecorderController.PrepareRecording();
			if (RecorderController.StartRecording())
			{
				Debug.Log($"[Recording] ● Recording started → {CurrentOutputPath}.mp4 (press F9 to stop)");
				return;
			}

			Debug.LogError("[Recording] Failed to start recording. Check the Console for Recorder errors.");
			RecorderController = null;
			DisposeRecordingCamera();
		}

		private static void StopRecording()
		{
			if (RecorderController == null || !RecorderController.IsRecording())
			{
				return;
			}

			RecorderController.StopRecording();
			RecorderController = null;
			DisposeRecordingCamera();
			Debug.Log($"[Recording] ■ Recording saved → {CurrentOutputPath}.mp4");
		}

		private static void DisposeRecordingCamera()
		{
			if (ActiveRecordingCamera != null)
			{
				ActiveRecordingCamera.Dispose();
			}

			ActiveRecordingCamera = null;
		}

		private static string GetRecordingsFolderPath()
		{
			string projectRoot = Directory.GetParent(Application.dataPath).FullName;
			return Path.Combine(projectRoot, RecordingsFolderName);
		}

		#endregion
	}
}
