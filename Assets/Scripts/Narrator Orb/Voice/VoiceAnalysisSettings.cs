using System;

namespace DroneDDW.NarratorOrb.Voice
{
	// The values the voice analysis runs with (VOICE_DEFAULTS in the web preview). A snapshot, so an analysis running
	// on another thread keeps its values while the settings asset is being edited.
	internal readonly struct VoiceAnalysisSettings : IEquatable<VoiceAnalysisSettings>
	{
		#region Properties

		// Same values as VOICE_DEFAULTS
		internal static VoiceAnalysisSettings Defaults => new(0.01, 30, 24, -16, -4, 0.1, 0.04, 0.11);

		internal double HopSeconds { get; }
		internal double GateDecibels { get; }
		internal double VowelRangeDecibels { get; }
		internal double HissLowDecibels { get; }
		internal double HissHighDecibels { get; }
		internal double OnsetOnThreshold { get; }
		internal double OnsetOffThreshold { get; }
		internal double OnsetMinGapSeconds { get; }

		#endregion

		#region Setup

		internal VoiceAnalysisSettings(
			double hopSeconds,
			double gateDecibels,
			double vowelRangeDecibels,
			double hissLowDecibels,
			double hissHighDecibels,
			double onsetOnThreshold,
			double onsetOffThreshold,
			double onsetMinGapSeconds)
		{
			HopSeconds = hopSeconds;
			GateDecibels = gateDecibels;
			VowelRangeDecibels = vowelRangeDecibels;
			HissLowDecibels = hissLowDecibels;
			HissHighDecibels = hissHighDecibels;
			OnsetOnThreshold = onsetOnThreshold;
			OnsetOffThreshold = onsetOffThreshold;
			OnsetMinGapSeconds = onsetMinGapSeconds;
		}

		#endregion

		#region Public

		public bool Equals(VoiceAnalysisSettings other)
		{
			return HopSeconds.Equals(other.HopSeconds)
				&& GateDecibels.Equals(other.GateDecibels)
				&& VowelRangeDecibels.Equals(other.VowelRangeDecibels)
				&& HissLowDecibels.Equals(other.HissLowDecibels)
				&& HissHighDecibels.Equals(other.HissHighDecibels)
				&& OnsetOnThreshold.Equals(other.OnsetOnThreshold)
				&& OnsetOffThreshold.Equals(other.OnsetOffThreshold)
				&& OnsetMinGapSeconds.Equals(other.OnsetMinGapSeconds);
		}

		public override bool Equals(object obj)
		{
			return obj is VoiceAnalysisSettings other && Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(HopSeconds, GateDecibels, VowelRangeDecibels, HissLowDecibels, HissHighDecibels,
				OnsetOnThreshold, OnsetOffThreshold, OnsetMinGapSeconds);
		}

		#endregion
	}
}
