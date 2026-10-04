using System;
using DroneDDW.Voice;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Characters.Editor
{
	// In Play mode: what the card's voice and mouth are doing, and emotion buttons. Below the references, the shared
	// settings asset in place, to tune the mouth, highlight, bounce and hover live.
	[CustomEditor(typeof(CharacterCard))]
	internal sealed class CharacterCardEditor : UnityEditor.Editor
	{
		#region Fields

		private static readonly CharacterEmotion[] Emotions =
		{
			CharacterEmotion.Natural, CharacterEmotion.Happy, CharacterEmotion.Angry,
		};

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
			CharacterCard card = (CharacterCard)target;
			if (Application.isPlaying)
			{
				DrawLiveValues(card);
				EditorGUILayout.Space();
			}

			DrawDefaultInspector();
			DrawSettings(card.Settings);
		}

		public override bool RequiresConstantRepaint()
		{
			return Application.isPlaying;
		}

		#endregion

		#region Private

		private static void DrawLiveValues(CharacterCard card)
		{
			EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
			string speaking = card.IsSpeaking ? $"Yes, at {card.PlayheadSeconds:0.0} s" : "No";
			EditorGUILayout.LabelField("Speaking", speaking);
			EditorGUILayout.LabelField("Mouth", card.IsMouthOpen ? "Open" : "Closed");
			DrawMeter("Vowel level", card.VowelEnvelope);
			DrawMeter("Smooth openness", card.MouthOpenness);

			VoiceAnalysis analysis = card.Analysis;
			string found = analysis == null ? string.Empty : $" of {analysis.Onsets.Count} in the clip";
			EditorGUILayout.LabelField("This line", $"{card.SyllableCount} syllables{found}, mouth opened "
				+ $"{card.MouthOpenCount} times");
			EditorGUILayout.LabelField("Voice", DescribeVoice(card.VoiceStatus));

			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.PrefixLabel("Emotion");
				foreach (CharacterEmotion emotion in Emotions)
				{
					bool isCurrent = card.Emotion == emotion;
					if (GUILayout.Toggle(isCurrent, emotion.ToString(), EditorStyles.miniButton) && !isCurrent)
					{
						card.SetEmotion(emotion);
					}
				}
			}
		}

		private static void DrawMeter(string label, float value)
		{
			Rect rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent(label));
			EditorGUI.ProgressBar(rect, Mathf.Clamp01(value), value.ToString("0.00"));
		}

		private static string DescribeVoice(VoiceStatus status)
		{
			switch (status)
			{
				case VoiceStatus.Idle:
					return "Not playing";
				case VoiceStatus.Analysing:
					return "Analysing the clip";
				case VoiceStatus.ClipAnalysis:
					return "Clip analysed ahead";
				case VoiceStatus.LiveFallback:
					return "Following the live output (the clip can't be read ahead)";
				default:
					throw new ArgumentOutOfRangeException(nameof(status), status, null);
			}
		}

		private void DrawSettings(CharacterPortraitSettings settings)
		{
			EditorGUILayout.Space();
			if (settings == null)
			{
				EditorGUILayout.HelpBox("Assign a Character Portrait Settings asset (Assets/Content/Data/Characters).",
					MessageType.Warning);
				return;
			}

			_isSettingsExpanded = EditorGUILayout.Foldout(_isSettingsExpanded, "Settings", true,
				EditorStyles.foldoutHeader);
			if (!_isSettingsExpanded)
			{
				return;
			}

			EditorGUILayout.HelpBox("These values live in the settings asset, shared by every card that uses it. "
				+ "Changes made in Play mode apply live and are kept.", MessageType.Info);
			CreateCachedEditor(settings, null, ref _settingsEditor);
			_settingsEditor.OnInspectorGUI();
		}

		#endregion
	}
}
