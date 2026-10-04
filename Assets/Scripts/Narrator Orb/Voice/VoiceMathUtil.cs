using System;
using System.Collections.Generic;

namespace DroneDDW.NarratorOrb.Voice
{
	// The analysis helpers of the web preview: clamp01, smooth01, percentile and mapBands
	internal static class VoiceMathUtil
	{
		#region Internal

		internal static double Clamp01(double value)
		{
			if (value < 0)
			{
				return 0;
			}

			return value > 1 ? 1 : value;
		}

		internal static double SmoothStep(double edge0, double edge1, double value)
		{
			double t = Clamp01((value - edge0) / (edge1 - edge0));
			return t * t * (3 - 2 * t);
		}

		// The value at a fraction of the sorted values, or -60 dB when there are none
		internal static double Percentile(List<double> values, double fraction)
		{
			if (values.Count == 0)
			{
				return -60;
			}

			double[] sorted = values.ToArray();
			Array.Sort(sorted);
			return sorted[Math.Min(sorted.Length - 1, (int)Math.Floor(fraction * sorted.Length))];
		}

		// Raw band levels to 0..1 features, given the reference levels (the "loud speech" level)
		internal static VoiceFeatures MapBands(VoiceBandLevels bands, double referenceFullDecibels,
			double referenceMidDecibels, VoiceAnalysisSettings settings)
		{
			double gate = settings.GateDecibels;
			double vowelRange = settings.VowelRangeDecibels;
			double level = Clamp01((bands.FullDecibels - (referenceFullDecibels - gate)) / gate);
			double vowel = level > 0
				? Clamp01((bands.MidDecibels - (referenceMidDecibels - vowelRange)) / vowelRange)
				: 0;
			double lowMid = Math.Pow(10, bands.LowDecibels / 10) + Math.Pow(10, bands.MidDecibels / 10);
			double tilt = bands.HighDecibels - 10 * Math.Log10(lowMid + 1e-12);
			double hiss = level > 0.12 ? SmoothStep(settings.HissLowDecibels, settings.HissHighDecibels, tilt) : 0;
			return new VoiceFeatures(level, vowel, hiss);
		}

		// JavaScript's Math.round: halves round up
		internal static double RoundHalfUp(double value)
		{
			return Math.Floor(value + 0.5);
		}

		#endregion
	}
}
