using System.Collections.Generic;
using System.Linq;
using DroneDDW.Characters.TestScene;
using DroneDDW.Voice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DroneDDW.Characters.Editor
{
	// Menu: DroneDDW > Characters > Check Portraits In Play Mode. Plays the character portraits test scene for 30
	// seconds: about three lines of the auto-cycle, then at 27 s it makes grandma the speaker and happy, as keys 1 and
	// 5 would. Reports every line (speaker, emotion, mouth opens, syllables), how the voice was read, and every error
	// and warning logged. Fails when the lines don't follow the auto-cycle's order, the pick doesn't take, or the line
	// plays but the mouth doesn't move with it. Also runs headless (-batchmode -executeMethod
	// DroneDDW.Characters.Editor.CharacterPortraitCheck.Run), quitting with code 1 on a failure. Survives the domain
	// reload of entering Play mode by keeping its state in SessionState.
	[InitializeOnLoad]
	internal static class CharacterPortraitCheck
	{
		#region Fields

		private const string MenuPath = "DroneDDW/Characters/Check Portraits In Play Mode";
		private const string RunningKey = "DroneDDW.Characters.CharacterPortraitCheck.Running";
		private const float TotalSeconds = 30f;
		private const float PickSeconds = 27f;
		private const float PickCheckSeconds = 29f;
		private const int MinAutoLines = 3;

		// A whole line passes 21 syllables; a frame hitch longer than the reader's jump tolerance can skip a few
		private const int MinLineSyllables = 15;

		// Of the time the line is heard, the mouth should be open for part, not never or always
		private const float MinOpenFraction = 0.2f;
		private const float MaxOpenFraction = 0.9f;

		private static readonly float[] SampleSeconds = { 6f, 15f, 24f };

		// The auto-cycle's order: grandma natural, student happy, worker angry, then each card's next emotion
		private static readonly (int Speaker, CharacterEmotion Emotion)[] AutoCycle =
		{
			(0, CharacterEmotion.Natural), (1, CharacterEmotion.Happy), (2, CharacterEmotion.Angry),
			(0, CharacterEmotion.Happy), (1, CharacterEmotion.Angry), (2, CharacterEmotion.Natural),
			(0, CharacterEmotion.Angry), (1, CharacterEmotion.Natural), (2, CharacterEmotion.Happy),
		};

		// Errors the XR runtime logs when no headset is connected (as in a headless run): reported, but not failures
		private static readonly string[] HeadsetMissingPrefixes = { "[MetaXRFeature]" };

		private static readonly List<string> Errors = new();
		private static readonly List<string> Warnings = new();
		private static readonly List<string> Failures = new();

		private static float StartTime = -1f;
		private static float LastTickTime;
		private static int NextSample;
		private static bool HasPicked;
		private static bool HasCheckedPick;
		private static int SeenLines;
		private static int AutoLines;
		private static string LineName;
		private static int LineMouthOpens;
		private static int LineSyllables;
		private static int TotalMouthOpens;
		private static int TotalSyllables;
		private static float HeardSeconds;
		private static float OpenSeconds;
		private static float MaxPlayheadSeconds;
		private static bool WasReadAhead;

		#endregion

		#region Properties

		// The lines were really heard: a playhead got past its first second (without audio output it stays at 0)
		private static bool IsAudioClockRunning => MaxPlayheadSeconds > 1f;

		#endregion

		#region Setup

		static CharacterPortraitCheck()
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
			if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CharacterPortraitTestSceneBuilder.ScenePath) == null)
			{
				Debug.LogError($"[Characters] Check: there is no {CharacterPortraitTestSceneBuilder.ScenePath}; run "
					+ "DroneDDW > Characters > Create Portrait Test Scene first.");
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

			EditorSceneManager.OpenScene(CharacterPortraitTestSceneBuilder.ScenePath);
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

			CharacterCardTestControls controls = Object.FindFirstObjectByType<CharacterCardTestControls>();
			if (controls == null || controls.LineCount == 0)
			{
				return;
			}

			float now = Time.realtimeSinceStartup;
			if (StartTime < 0f)
			{
				StartTime = now;
				LastTickTime = now;
				Debug.Log("[Characters] Check: playing the portraits test scene for 30 s, picking grandma and happy at "
					+ "27 s.");
			}

			float elapsed = now - StartTime;
			float deltaSeconds = now - LastTickTime;
			LastTickTime = now;
			TrackLines(controls);
			TrackSpeaker(controls.Speaker, deltaSeconds);
			if (NextSample < SampleSeconds.Length && elapsed >= SampleSeconds[NextSample])
			{
				Sample(controls, elapsed);
				NextSample++;
			}

			if (!HasPicked && elapsed >= PickSeconds)
			{
				// The pick cuts the current line short
				EndLine(isWhole: false);
				LineName = null;
				HasPicked = true;
				controls.SelectSpeaker(0);
				controls.SelectEmotion(CharacterEmotion.Happy);
			}

			if (!HasCheckedPick && elapsed >= PickCheckSeconds)
			{
				HasCheckedPick = true;
				CheckPick(controls);
			}

			if (elapsed >= TotalSeconds)
			{
				Finish();
			}
		}

		// A new line: checks it against the auto-cycle's order, and reports the line that ended
		private static void TrackLines(CharacterCardTestControls controls)
		{
			if (controls.LineCount == SeenLines)
			{
				return;
			}

			EndLine(isWhole: SeenLines > 0);
			SeenLines = controls.LineCount;
			CharacterCard speaker = controls.Speaker;
			LineName = $"line {SeenLines}, {speaker.Portraits.DisplayName} {speaker.Emotion}";
			if (HasPicked)
			{
				return;
			}

			(int expectedSpeaker, CharacterEmotion expectedEmotion) = AutoCycle[AutoLines % AutoCycle.Length];
			if (controls.SpeakerIndex != expectedSpeaker || speaker.Emotion != expectedEmotion)
			{
				Failures.Add($"{LineName} isn't the auto-cycle's next pair (speaker {expectedSpeaker}, "
					+ $"{expectedEmotion})");
			}

			AutoLines++;
		}

		// The speaker's mouth and voice while the line is heard
		private static void TrackSpeaker(CharacterCard speaker, float deltaSeconds)
		{
			LineMouthOpens = speaker.MouthOpenCount;
			LineSyllables = speaker.SyllableCount;
			if (!speaker.IsSpeaking)
			{
				return;
			}

			MaxPlayheadSeconds = Mathf.Max(MaxPlayheadSeconds, speaker.PlayheadSeconds);
			if (speaker.VoiceStatus != VoiceStatus.ClipAnalysis)
			{
				return;
			}

			WasReadAhead = true;
			HeardSeconds += deltaSeconds;
			if (speaker.IsMouthOpen)
			{
				OpenSeconds += deltaSeconds;
			}
		}

		// isWhole: the line played to its end, rather than being cut short by the check's pick
		private static void EndLine(bool isWhole)
		{
			if (LineName == null)
			{
				return;
			}

			TotalMouthOpens += LineMouthOpens;
			TotalSyllables += LineSyllables;
			Debug.Log($"[Characters] Check: {LineName} ended: the mouth opened {LineMouthOpens} times, "
				+ $"{LineSyllables} syllables passed.");
			if (isWhole && IsAudioClockRunning && LineSyllables < MinLineSyllables)
			{
				Failures.Add($"{LineName} passed only {LineSyllables} syllables (at least {MinLineSyllables} "
					+ "expected)");
			}
		}

		private static void Sample(CharacterCardTestControls controls, float elapsed)
		{
			CharacterCard speaker = controls.Speaker;
			VoiceAnalysis analysis = speaker.Analysis;
			string clipAnalysis = analysis == null
				? "no clip analysis yet"
				: $"{analysis.Onsets.Count} syllables in the clip";
			string playing = speaker.IsSpeaking ? $"speaking at {speaker.PlayheadSeconds:0.0} s" : "between lines";
			string openShare = HeardSeconds > 0f ? $"{OpenSeconds / HeardSeconds:P0}" : "-";
			Debug.Log($"[Characters] Check at {elapsed:0} s: {LineName}, {playing}, voice {speaker.VoiceStatus} "
				+ $"({clipAnalysis}); mouth {(speaker.IsMouthOpen ? "open" : "closed")}, opened "
				+ $"{speaker.MouthOpenCount} times for {speaker.SyllableCount} syllables this line; open {openShare} "
				+ "of the time heard so far.");
		}

		// After keys 1 and 5: grandma speaks, happy, and the auto-cycle has stopped
		private static void CheckPick(CharacterCardTestControls controls)
		{
			CharacterCard speaker = controls.Speaker;
			if (controls.SpeakerIndex != 0 || speaker.Emotion != CharacterEmotion.Happy || controls.IsAutoCycling)
			{
				Failures.Add($"after picking grandma and happy, the speaker was {speaker.Portraits.DisplayName} "
					+ $"{speaker.Emotion} with the auto-cycle {(controls.IsAutoCycling ? "on" : "off")}");
			}
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

			EndLine(isWhole: false);
			if (AutoLines < MinAutoLines)
			{
				Failures.Add($"only {AutoLines} lines started in {PickSeconds:0} s (at least {MinAutoLines} expected)");
			}

			if (IsAudioClockRunning)
			{
				CheckMouth();
			}
			else
			{
				Debug.Log("[Characters] Check: the audio clock didn't run (no audio output, as in some headless runs), "
					+ "so the mouths couldn't move; run the check from the menu to see them.");
			}

			foreach (string failure in Failures)
			{
				Debug.Log($"[Characters] Check failure: {failure}");
			}

			foreach (string error in Errors.Take(10))
			{
				Debug.Log($"[Characters] Check error: {error}");
			}

			foreach (string warning in Warnings.Distinct().Take(10))
			{
				Debug.Log($"[Characters] Check warning: {warning}");
			}

			bool isPassed = Failures.Count == 0 && Errors.Count == 0;
			Debug.Log($"[Characters] Check done: {(isPassed ? "passed" : "failed")}, {SeenLines} lines, "
				+ $"{Failures.Count} failures, {Errors.Count} errors, {Warnings.Count} warnings.");
			if (Application.isBatchMode)
			{
				EditorApplication.Exit(isPassed ? 0 : 1);
				return;
			}

			EditorApplication.ExitPlaymode();
		}

		// With the line heard: it was read ahead, and the mouth opened and closed with it
		private static void CheckMouth()
		{
			if (!WasReadAhead)
			{
				Failures.Add("the line played but was never read ahead (check its Load Type is Decompress On Load)");
				return;
			}

			if (TotalMouthOpens == 0 || TotalSyllables == 0)
			{
				Failures.Add($"the line played but the mouths opened {TotalMouthOpens} times for {TotalSyllables} "
					+ "syllables");
			}

			float openFraction = OpenSeconds / Mathf.Max(HeardSeconds, 1e-3f);
			Debug.Log($"[Characters] Check: over {HeardSeconds:0.0} s of the line heard, the mouths opened "
				+ $"{TotalMouthOpens} times for {TotalSyllables} syllables and were open {openFraction:P0} of the "
				+ "time.");
			if (openFraction < MinOpenFraction || openFraction > MaxOpenFraction)
			{
				Failures.Add($"the mouths were open {openFraction:P0} of the time heard (expected "
					+ $"{MinOpenFraction:P0} to {MaxOpenFraction:P0})");
			}
		}

		#endregion
	}
}
