using System;
using DroneDDW.NarratorOrb.Settings;
using DroneDDW.NarratorOrb.Voice;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Editor
{
	// In Play mode: the live values the web preview's meters show (loudness, vowels, hiss, syllables, morph) and step
	// buttons. Below the references, the settings asset in place, with Paste and Copy JSON.
	[CustomEditor(typeof(NarratorOrbController))]
	internal sealed class NarratorOrbControllerEditor : UnityEditor.Editor
	{
		#region Fields

		private UnityEditor.Editor _settingsEditor;
		private bool _isSettingsExpanded = true;

		#endregion

		#region Setup

		private void OnDisable()
		{
			if (_settingsEditor != null)
			{
				DestroyImmediate(_settingsEditor);
			}
		}

		#endregion

		#region Public

		public override void OnInspectorGUI()
		{
			NarratorOrbController orb = (NarratorOrbController)target;
			if (Application.isPlaying)
			{
				DrawLiveValues(orb);
				EditorGUILayout.Space();
			}

			DrawDefaultInspector();
			DrawSettings(orb.Settings);
		}

		public override bool RequiresConstantRepaint()
		{
			return Application.isPlaying;
		}

		#endregion

		#region Private

		private static void DrawLiveValues(NarratorOrbController orb)
		{
			EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
			DrawMeter("Loudness", orb.Loudness);
			DrawMeter("Vowels", orb.Vowels);
			DrawMeter("Hiss", orb.Hiss);

			VoiceAnalysis analysis = orb.Analysis;
			string found = analysis == null ? string.Empty : $" of {analysis.Onsets.Count} in the clip";
			EditorGUILayout.LabelField("Syllables", $"{orb.SyllableCount} fired{found}");
			EditorGUILayout.LabelField("Morph", $"{orb.Morph:0.00}  (step {orb.Step}, "
				+ $"{NarratorOrbController.GetStepName(orb.Step)})");
			EditorGUILayout.LabelField("Voice", DescribeVoice(orb.VoiceStatus, analysis));

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.PrefixLabel("Set step");
				for (int step = 0; step < NarratorOrbMorphSettings.StepCount; step++)
				{
					bool isCurrent = orb.Step == step;
					string label = $"{step} {NarratorOrbController.GetStepName(step)}";
					if (GUILayout.Toggle(isCurrent, label, EditorStyles.miniButton) && !isCurrent)
					{
						orb.SetStep(step);
					}
				}
			}
		}

		private static void DrawMeter(string label, float value)
		{
			Rect rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent(label));
			EditorGUI.ProgressBar(rect, Mathf.Clamp01(value), value.ToString("0.00"));
		}

		private static string DescribeVoice(NarratorOrbVoiceStatus status, VoiceAnalysis analysis)
		{
			switch (status)
			{
				case NarratorOrbVoiceStatus.Idle:
					return "Not playing";
				case NarratorOrbVoiceStatus.Analysing:
					return "Analysing the clip";
				case NarratorOrbVoiceStatus.ClipAnalysis:
					return analysis == null
						? "Clip analysed ahead"
						: $"Clip analysed ahead, loud speech at {analysis.ReferenceFullDecibels:0.0} dB";
				case NarratorOrbVoiceStatus.LiveFallback:
					return "Following the live output (the clip can't be read ahead)";
				default:
					throw new ArgumentOutOfRangeException(nameof(status), status, null);
			}
		}

		private void DrawSettings(NarratorOrbSettings settings)
		{
			EditorGUILayout.Space();
			if (settings == null)
			{
				EditorGUILayout.HelpBox("Assign a Narrator Orb Settings asset (Assets/Content/Data/Narrator Orb).",
					MessageType.Warning);
				return;
			}

			_isSettingsExpanded = EditorGUILayout.Foldout(_isSettingsExpanded, "Settings", true,
				EditorStyles.foldoutHeader);
			if (!_isSettingsExpanded)
			{
				return;
			}

			EditorGUILayout.HelpBox("These values live in the settings asset, shared by every orb that uses it. "
				+ "Changes made in Play mode apply live and are kept.", MessageType.Info);
			CreateCachedEditor(settings, null, ref _settingsEditor);
			_settingsEditor.OnInspectorGUI();
		}

		#endregion
	}
}
