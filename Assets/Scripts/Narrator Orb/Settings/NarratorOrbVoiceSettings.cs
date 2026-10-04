using System;
using DroneDDW.Voice;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Settings
{
	// How the narrator's voice is analysed (S.voice in the web preview). Changing any of these re-runs the clip
	// analysis, except the lead.
	[Serializable]
	internal sealed class NarratorOrbVoiceSettings
	{
		#region Serialized fields

		[Tooltip("Analysis step: the voice is measured every this many seconds (hopSeconds)")]
		[Min(0.001f)]
		[SerializeField] private float _hopSeconds = 0.01f;

		[Tooltip("Noise gate: how far under the clip's loud parts still counts as voice, in dB (gateDb)")]
		[Range(16f, 44f)]
		[SerializeField] private float _gateDecibels = 30f;

		[Tooltip("The same idea for the vowel band, in dB (vowelRangeDb)")]
		[SerializeField] private float _vowelRangeDecibels = 24f;

		[Tooltip("Treble to voice ratio where hiss (s, sh, t, f) starts to show, in dB (hissLowDb)")]
		[SerializeField] private float _hissLowDecibels = -16f;

		[Tooltip("Treble to voice ratio where hiss is full on, in dB (hissHighDb)")]
		[SerializeField] private float _hissHighDecibels = -4f;

		[Tooltip("Syllable threshold: how fast the vowels must swell to fire a syllable. Lower finds more (onsetOn).")]
		[Range(0.04f, 0.25f)]
		[SerializeField] private float _onsetOnThreshold = 0.1f;

		[Tooltip("The syllable detector re-arms once the swell drops below this (onsetOff)")]
		[SerializeField] private float _onsetOffThreshold = 0.04f;

		[Tooltip("Shortest time between syllables, in seconds: at most about 9 per second (onsetMinGap)")]
		[Min(0f)]
		[SerializeField] private float _onsetMinGapSeconds = 0.11f;

		[Tooltip("Reads the voice this far ahead of the playhead, in seconds (leadSeconds). On Quest the sound leaves "
			+ "the speakers later than the playhead, so try lower or negative values there.")]
		[SerializeField] private float _leadSeconds = 0.04f;

		#endregion

		#region Properties

		internal float HopSeconds { get => _hopSeconds; set => _hopSeconds = value; }
		internal float GateDecibels { get => _gateDecibels; set => _gateDecibels = value; }
		internal float VowelRangeDecibels { get => _vowelRangeDecibels; set => _vowelRangeDecibels = value; }
		internal float HissLowDecibels { get => _hissLowDecibels; set => _hissLowDecibels = value; }
		internal float HissHighDecibels { get => _hissHighDecibels; set => _hissHighDecibels = value; }
		internal float OnsetOnThreshold { get => _onsetOnThreshold; set => _onsetOnThreshold = value; }
		internal float OnsetOffThreshold { get => _onsetOffThreshold; set => _onsetOffThreshold = value; }
		internal float OnsetMinGapSeconds { get => _onsetMinGapSeconds; set => _onsetMinGapSeconds = value; }
		internal float LeadSeconds { get => _leadSeconds; set => _leadSeconds = value; }

		#endregion

		#region Internal

		// The values the analysis runs with, as the doubles they were typed as (0.1 is exactly 0.1, as in the preview)
		internal VoiceAnalysisSettings ToAnalysisSettings()
		{
			return new VoiceAnalysisSettings(ToDouble(_hopSeconds), ToDouble(_gateDecibels),
				ToDouble(_vowelRangeDecibels), ToDouble(_hissLowDecibels), ToDouble(_hissHighDecibels),
				ToDouble(_onsetOnThreshold), ToDouble(_onsetOffThreshold), ToDouble(_onsetMinGapSeconds));
		}

		#endregion

		#region Private

		private static double ToDouble(float value)
		{
			return NarratorOrbSettingsUtil.ToExactDouble(value);
		}

		#endregion
	}
}
