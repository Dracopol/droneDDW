using System;
using System.Collections.Generic;
using System.Linq;
using DroneDDW.Voice;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DroneDDW.NarratorOrb.Editor
{
	// Menu: DroneDDW > Narrator Orb > Check In Play Mode. Plays the Narrator Orb test scene for twelve seconds, asking
	// for step 4 at 4 s and step 0 at 8 s, and reports how the voice was read (the clip analysis, syllables fired, the
	// peaks of loudness, vowels and hiss), where the morph went and every error and warning logged. Then compiles the
	// orb's shaders for Quest (Vulkan and GLES3, with multiview) and Link (D3D11, with single-pass instancing), where
	// any error or warning fails the check. Also runs headless (-batchmode -executeMethod
	// DroneDDW.NarratorOrb.Editor.NarratorOrbCheck.Run), quitting with code 1 on a failure. Survives the domain reload
	// of entering Play mode by keeping its state in SessionState.
	[InitializeOnLoad]
	internal static class NarratorOrbCheck
	{
		#region Fields

		private const string MenuPath = "DroneDDW/Narrator Orb/Check In Play Mode";
		private const string RunningKey = "DroneDDW.NarratorOrb.NarratorOrbCheck.Running";
		private const string ShadersFolder = "Assets/Content/Shaders/Narrator Orb";
		private const float TotalSeconds = 12f;
		private const int ExpectedSyllables = 21;
		private const float MorphTolerance = 0.05f;

		// When to ask for each step, when to look, and the morph each look expects
		private static readonly (float Seconds, int Step)[] StepRequests = { (4f, 4), (8f, 0) };
		private static readonly (float Seconds, float Morph)[] Samples = { (3f, 0f), (7f, 1f), (11f, -1f) };

		// Errors the XR runtime logs when no headset is connected (as in a headless run): reported, but not failures
		private static readonly string[] HeadsetMissingPrefixes = { "[MetaXRFeature]" };

		private static readonly string[] ShaderFileNames =
		{
			"Narrator Orb Particles.shader", "Narrator Orb Glow.shader", "Narrator Orb Ripple Rings.shader",
		};

		// Where the orb draws, and the stereo keyword each uses: Quest standalone (multiview) and Link (instancing)
		private static readonly (ShaderCompilerPlatform Platform, BuildTarget Target, string Stereo)[] ShaderTargets =
		{
			(ShaderCompilerPlatform.Vulkan, BuildTarget.Android, "STEREO_MULTIVIEW_ON"),
			(ShaderCompilerPlatform.GLES3x, BuildTarget.Android, "STEREO_MULTIVIEW_ON"),
			(ShaderCompilerPlatform.D3D, BuildTarget.StandaloneWindows64, "STEREO_INSTANCING_ON"),
		};

		private static readonly ShaderType[] ShaderStages = { ShaderType.Vertex, ShaderType.Fragment };

		private static readonly List<string> Errors = new();
		private static readonly List<string> Warnings = new();
		private static readonly List<string> Failures = new();

		private static float StartTime = -1f;
		private static int NextRequest;
		private static int NextSample;
		private static float PeakLoudness;
		private static float PeakVowels;
		private static float PeakHiss;
		private static int NonFiniteFrames;
		private static float FirstAudioSeconds = -1f;
		private static float LastAudioSeconds = -1f;

		#endregion

		#region Setup

		static NarratorOrbCheck()
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
			if (AssetDatabase.LoadAssetAtPath<SceneAsset>(NarratorOrbTestSceneBuilder.ScenePath) == null)
			{
				Debug.LogError($"[Narrator Orb] Check: there is no {NarratorOrbTestSceneBuilder.ScenePath}; run "
					+ "DroneDDW > Narrator Orb > Create Test Scene first.");
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

			EditorSceneManager.OpenScene(NarratorOrbTestSceneBuilder.ScenePath);
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

			NarratorOrbController orb = Object.FindFirstObjectByType<NarratorOrbController>();
			if (orb == null)
			{
				return;
			}

			if (StartTime < 0f)
			{
				StartTime = Time.realtimeSinceStartup;
				Debug.Log("[Narrator Orb] Check: playing the test scene for 12 s, step 4 at 4 s and step 0 at 8 s.");
			}

			float elapsed = Time.realtimeSinceStartup - StartTime;
			Track(orb);
			if (NextRequest < StepRequests.Length && elapsed >= StepRequests[NextRequest].Seconds)
			{
				orb.SetStep(StepRequests[NextRequest].Step);
				NextRequest++;
			}

			if (NextSample < Samples.Length && elapsed >= Samples[NextSample].Seconds)
			{
				Sample(orb, elapsed, Samples[NextSample].Morph);
				NextSample++;
			}

			if (elapsed >= TotalSeconds)
			{
				Finish(orb);
			}
		}

		private static void Track(NarratorOrbController orb)
		{
			PeakLoudness = Mathf.Max(PeakLoudness, orb.Loudness);
			PeakVowels = Mathf.Max(PeakVowels, orb.Vowels);
			PeakHiss = Mathf.Max(PeakHiss, orb.Hiss);
			if (!orb.AreValuesFinite())
			{
				NonFiniteFrames++;
			}

			AudioSource voice = orb.Voice;
			if (voice != null && voice.isPlaying)
			{
				LastAudioSeconds = voice.time;
				if (FirstAudioSeconds < 0f)
				{
					FirstAudioSeconds = LastAudioSeconds;
				}
			}
		}

		// Logs what the orb is doing, and checks the morph reached the step asked for
		private static void Sample(NarratorOrbController orb, float elapsed, float expectedMorph)
		{
			VoiceAnalysis analysis = orb.Analysis;
			string clipAnalysis = analysis == null
				? "no clip analysis yet"
				: $"{analysis.Onsets.Count} syllables found, reference {analysis.ReferenceFullDecibels:0.0} dB";
			Debug.Log($"[Narrator Orb] Check at {elapsed:0} s: voice {orb.VoiceStatus} ({clipAnalysis}), "
				+ $"{orb.SyllableCount} ripples fired, audio at {LastAudioSeconds:0.0} s; peaks so far: loudness "
				+ $"{PeakLoudness:0.00}, vowels {PeakVowels:0.00}, hiss {PeakHiss:0.00}; morph {orb.Morph:0.00} at "
				+ $"step {orb.Step} ({NarratorOrbController.GetStepName(orb.Step)}).");

			if (Mathf.Abs(orb.Morph - expectedMorph) > MorphTolerance)
			{
				Failures.Add($"at {elapsed:0} s the morph was {orb.Morph:0.00}, not {expectedMorph:0.00}");
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

		private static void Finish(NarratorOrbController orb)
		{
			EditorApplication.update -= Tick;
			Application.logMessageReceived -= HandleLog;
			SessionState.SetBool(RunningKey, false);

			VoiceAnalysis analysis = orb.Analysis;
			bool isAudioClockRunning = LastAudioSeconds > FirstAudioSeconds + 1f;
			if (analysis == null)
			{
				Failures.Add("the sample narration was never analysed");
			}
			else if (analysis.Onsets.Count != ExpectedSyllables)
			{
				Failures.Add($"the analysis found {analysis.Onsets.Count} syllables, not {ExpectedSyllables}");
			}

			if (isAudioClockRunning && orb.SyllableCount == 0)
			{
				Failures.Add("the narration played but no syllable fired a ripple");
			}

			if (NonFiniteFrames > 0)
			{
				Failures.Add($"{NonFiniteFrames} frames had values that weren't numbers");
			}

			if (!isAudioClockRunning)
			{
				Debug.Log("[Narrator Orb] Check: the audio clock didn't run (no audio output, as in some headless "
					+ "runs), so no syllables could fire; run the check from the menu to see them.");
			}

			CheckShaders();
			foreach (string failure in Failures)
			{
				Debug.Log($"[Narrator Orb] Check failure: {failure}");
			}

			foreach (string error in Errors.Take(10))
			{
				Debug.Log($"[Narrator Orb] Check error: {error}");
			}

			foreach (string warning in Warnings.Distinct().Take(10))
			{
				Debug.Log($"[Narrator Orb] Check warning: {warning}");
			}

			bool isPassed = Failures.Count == 0 && Errors.Count == 0;
			Debug.Log($"[Narrator Orb] Check done: {(isPassed ? "passed" : "failed")}, {Failures.Count} failures, "
				+ $"{Errors.Count} errors, {Warnings.Count} warnings.");
			if (Application.isBatchMode)
			{
				EditorApplication.Exit(isPassed ? 0 : 1);
				return;
			}

			EditorApplication.ExitPlaymode();
		}

		// Compiles every pass of the orb's shaders, with and without the stereo keyword, for each platform it draws on
		private static void CheckShaders()
		{
			int variantCount = 0;
			foreach (string fileName in ShaderFileNames)
			{
				Shader shader = AssetDatabase.LoadAssetAtPath<Shader>($"{ShadersFolder}/{fileName}");
				if (shader == null)
				{
					Failures.Add($"{fileName} is missing");
					continue;
				}

				ShaderData data = ShaderUtil.GetShaderData(shader);
				for (int subshader = 0; subshader < data.SubshaderCount; subshader++)
				{
					ShaderData.Subshader passes = data.GetSubshader(subshader);
					for (int pass = 0; pass < passes.PassCount; pass++)
					{
						variantCount += CompilePass(shader.name, passes.GetPass(pass));
					}
				}
			}

			Debug.Log($"[Narrator Orb] Check: compiled {variantCount} shader variants for Quest (Vulkan and GLES3 with "
				+ "multiview) and Link (D3D11 with single-pass instancing).");
		}

		// Returns how many variants it compiled; every error or warning is a failure
		private static int CompilePass(string shaderName, ShaderData.Pass pass)
		{
			int variantCount = 0;
			foreach ((ShaderCompilerPlatform platform, BuildTarget target, string stereo) in ShaderTargets)
			{
				foreach (string[] keywords in new[] { Array.Empty<string>(), new[] { stereo } })
				{
					foreach (ShaderType stage in ShaderStages)
					{
						ShaderData.VariantCompileInfo result = pass.CompileVariant(stage, keywords, platform, target);
						variantCount++;
						string variant = $"{shaderName} {stage} on {platform} {string.Join(" ", keywords)}";
						foreach (ShaderMessage message in result.Messages)
						{
							Failures.Add($"{variant}: {message.severity} {message.message} ({message.file}:"
								+ $"{message.line})");
						}

						if (!result.Success && result.Messages.Length == 0)
						{
							Failures.Add($"{variant} didn't compile");
						}
					}
				}
			}

			return variantCount;
		}

		#endregion
	}
}
