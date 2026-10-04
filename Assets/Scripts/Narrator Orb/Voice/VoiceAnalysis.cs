using System;
using System.Collections.Generic;

namespace DroneDDW.NarratorOrb.Voice
{
	// A whole clip analysed ahead (analyzeClip): the features per hop, the syllable onsets and the reference levels.
	// Pure C#, so it runs on the thread pool.
	internal sealed class VoiceAnalysis
	{
		#region Fields

		private readonly float[] _levels;
		private readonly float[] _vowels;
		private readonly float[] _hisses;
		private readonly VoiceOnset[] _onsets;

		#endregion

		#region Properties

		internal VoiceAnalysisSettings Settings { get; }
		internal double HopSeconds => Settings.HopSeconds;
		internal int HopCount => _levels.Length;
		internal IReadOnlyList<VoiceOnset> Onsets => _onsets;
		internal double ReferenceFullDecibels { get; }
		internal double ReferenceMidDecibels { get; }
		internal double DurationSeconds { get; }

		#endregion

		#region Setup

		private VoiceAnalysis(VoiceAnalysisSettings settings, float[] levels, float[] vowels, float[] hisses,
			VoiceOnset[] onsets, double referenceFullDecibels, double referenceMidDecibels, double durationSeconds)
		{
			Settings = settings;
			_levels = levels;
			_vowels = vowels;
			_hisses = hisses;
			_onsets = onsets;
			ReferenceFullDecibels = referenceFullDecibels;
			ReferenceMidDecibels = referenceMidDecibels;
			DurationSeconds = durationSeconds;
		}

		#endregion

		#region Internal

		// The same steps as analyzeClip, on mono samples
		internal static VoiceAnalysis Analyze(float[] samples, int sampleRate, VoiceAnalysisSettings settings)
		{
			VoiceBands bands = new(sampleRate, settings.HopSeconds);
			List<VoiceBandLevels> raw = new();
			bands.Push(samples, 0, samples.Length, raw.Add);

			List<double> fulls = new(raw.Count);
			List<double> mids = new(raw.Count);
			foreach (VoiceBandLevels levels in raw)
			{
				if (levels.FullDecibels > -70)
				{
					fulls.Add(levels.FullDecibels);
				}

				if (levels.MidDecibels > -75)
				{
					mids.Add(levels.MidDecibels);
				}
			}

			double referenceFull = VoiceMathUtil.Percentile(fulls, 0.95);
			double referenceMid = VoiceMathUtil.Percentile(mids, 0.95);

			int count = raw.Count;
			float[] level = new float[count];
			float[] vowel = new float[count];
			float[] hiss = new float[count];
			List<VoiceOnset> onsets = new();
			OnsetDetector detector = new(settings.HopSeconds, settings);
			for (int i = 0; i < count; i++)
			{
				VoiceFeatures features = VoiceMathUtil.MapBands(raw[i], referenceFull, referenceMid, settings);
				level[i] = (float)features.Level;
				vowel[i] = (float)features.Vowel;
				hiss[i] = (float)features.Hiss;
				double strength = detector.Step(features.Vowel, features.Level);
				if (strength > 0)
				{
					onsets.Add(new VoiceOnset((i + 1) * settings.HopSeconds, strength));
				}
			}

			return new VoiceAnalysis(settings, level, vowel, hiss, onsets.ToArray(), referenceFull, referenceMid,
				(double)samples.Length / sampleRate);
		}

		// Averages interleaved channels into one (toMono); mono data comes back as it is
		internal static float[] ToMono(float[] interleaved, int channelCount)
		{
			if (channelCount <= 1)
			{
				return interleaved;
			}

			int frameCount = interleaved.Length / channelCount;
			float[] mono = new float[frameCount];
			for (int channel = 0; channel < channelCount; channel++)
			{
				for (int i = 0; i < frameCount; i++)
				{
					mono[i] += interleaved[i * channelCount + channel] / channelCount;
				}
			}

			return mono;
		}

		// The features at a time, interpolated between hop centres (sampleFeatures)
		internal VoiceFeatures Sample(double timeSeconds)
		{
			if (_levels.Length == 0)
			{
				return VoiceFeatures.Silent;
			}

			double position = timeSeconds / HopSeconds - 0.5;
			int index = (int)Math.Floor(position);
			double fraction = position - index;
			return new VoiceFeatures(Interpolate(_levels, index, fraction), Interpolate(_vowels, index, fraction),
				Interpolate(_hisses, index, fraction));
		}

		// The index of the first onset at or after a time (firstOnsetAfter)
		internal int FirstOnsetAfter(double timeSeconds)
		{
			int index = 0;
			while (index < _onsets.Length && _onsets[index].TimeSeconds < timeSeconds)
			{
				index++;
			}

			return index;
		}

		#endregion

		#region Private

		private static double Interpolate(float[] values, int index, double fraction)
		{
			double from = At(values, index);
			return from + (At(values, index + 1) - from) * fraction;
		}

		private static float At(float[] values, int index)
		{
			return values[Math.Max(0, Math.Min(values.Length - 1, index))];
		}

		#endregion
	}
}
