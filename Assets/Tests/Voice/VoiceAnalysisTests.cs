using DroneDDW.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Voice.Tests
{
	// The clip analysis against the Narrator Orb web preview's results for the test narration: 21 syllables, loud
	// speech at -15.152 dB. The clip is imported as PCM at its own rate (24 kHz), so the samples are the preview's.
	public sealed class VoiceAnalysisTests
	{
		#region Fields

		private const string SampleClipPath = "Assets/Content/Audio/Voice/Narrator Orb/Narrator_TestSample.wav";

		private const double DecibelTolerance = 0.05;

		#endregion

		#region Private

		private static VoiceAnalysis AnalyseSample(VoiceAnalysisSettings settings)
		{
			AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SampleClipPath);
			Assert.That(clip, Is.Not.Null, $"Missing {SampleClipPath}");
			Assert.That(clip.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
			if (clip.loadState != AudioDataLoadState.Loaded)
			{
				clip.LoadAudioData();
			}

			float[] samples = new float[clip.samples * clip.channels];
			Assert.That(clip.GetData(samples, 0), Is.True, "Couldn't read the clip's samples");
			return VoiceAnalysis.Analyze(VoiceAnalysis.ToMono(samples, clip.channels), clip.frequency, settings);
		}

		private static VoiceAnalysisSettings WithOnsetThreshold(double onsetOnThreshold)
		{
			VoiceAnalysisSettings defaults = VoiceAnalysisSettings.Defaults;
			return new VoiceAnalysisSettings(defaults.HopSeconds, defaults.GateDecibels, defaults.VowelRangeDecibels,
				defaults.HissLowDecibels, defaults.HissHighDecibels, onsetOnThreshold, defaults.OnsetOffThreshold,
				defaults.OnsetMinGapSeconds);
		}

		#endregion

		#region Tests

		[Test]
		public void TestNarrationMatchesTheReference()
		{
			VoiceAnalysis analysis = AnalyseSample(VoiceAnalysisSettings.Defaults);

			Assert.That(analysis.Onsets.Count, Is.EqualTo(21));
			Assert.That(analysis.ReferenceFullDecibels, Is.EqualTo(-15.1523).Within(DecibelTolerance));
			Assert.That(analysis.ReferenceMidDecibels, Is.EqualTo(-16.7016).Within(DecibelTolerance));
			Assert.That(analysis.HopCount, Is.EqualTo(784));
			Assert.That(analysis.Onsets[0].TimeSeconds, Is.EqualTo(0.52).Within(0.005));
			Assert.That(analysis.Onsets[20].TimeSeconds, Is.EqualTo(7.00).Within(0.005));
		}

		// The preview finds 26 at a syllable threshold of 0.06 and 14 at 0.25
		[Test]
		public void SyllableThresholdMatchesTheReference()
		{
			Assert.That(AnalyseSample(WithOnsetThreshold(0.06)).Onsets.Count, Is.EqualTo(26));
			Assert.That(AnalyseSample(WithOnsetThreshold(0.25)).Onsets.Count, Is.EqualTo(14));
		}

		#endregion
	}
}
