using DroneDDW.NarratorOrb.Settings;
using DroneDDW.NarratorOrb.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Tests
{
	// The clip analysis against the web preview's results for the sample narration: 64 syllables, loud speech at
	// -14.434 dB. The clip is imported as PCM at its own rate (24 kHz), so the samples are the preview's.
	public sealed class VoiceAnalysisTests
	{
		#region Fields

		private const string SampleClipPath = "Assets/Content/Audio/Voice/Narrator Orb/Narrator Sample.wav";
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
		public void SampleNarrationMatchesTheReference()
		{
			VoiceAnalysis analysis = AnalyseSample(VoiceAnalysisSettings.Defaults);

			Assert.That(analysis.Onsets.Count, Is.EqualTo(64));
			Assert.That(analysis.ReferenceFullDecibels, Is.EqualTo(-14.434).Within(DecibelTolerance));
			Assert.That(analysis.ReferenceMidDecibels, Is.EqualTo(-16.465).Within(DecibelTolerance));
			Assert.That(analysis.HopCount, Is.EqualTo(2803));
			Assert.That(analysis.Onsets[0].TimeSeconds, Is.EqualTo(0.52).Within(0.005));
			Assert.That(analysis.Onsets[63].TimeSeconds, Is.EqualTo(27.12).Within(0.005));
		}

		[Test]
		public void SettingsAssetDefaultsAreTheReferenceValues()
		{
			NarratorOrbSettings settings = ScriptableObject.CreateInstance<NarratorOrbSettings>();
			try
			{
				Assert.That(settings.Voice.ToAnalysisSettings(), Is.EqualTo(VoiceAnalysisSettings.Defaults));
				Assert.That(AnalyseSample(settings.Voice.ToAnalysisSettings()).Onsets.Count, Is.EqualTo(64));
			}
			finally
			{
				Object.DestroyImmediate(settings);
			}
		}

		// The preview finds 66 at a syllable threshold of 0.06 and 42 at 0.25
		[Test]
		public void SyllableThresholdMatchesTheReference()
		{
			Assert.That(AnalyseSample(WithOnsetThreshold(0.06)).Onsets.Count, Is.EqualTo(66));
			Assert.That(AnalyseSample(WithOnsetThreshold(0.25)).Onsets.Count, Is.EqualTo(42));
		}

		#endregion
	}
}
