using System.Collections.Generic;
using DroneDDW.Characters.Editor;
using DroneDDW.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Characters.Tests
{
	// The mouth with the default settings. On the test line, simulated at the headset's 72 frames per second as a card
	// reads it, the mouth should open about once per syllable, never flicker faster than the minimum hold times, and be
	// open for part of the line (about 56%).
	public sealed class MouthFlapTests
	{
		#region Fields

		private const float FrameSeconds = 1f / 72f;

		// Hold times are counted in whole frames, so a stretch can be a hair under its setting in float arithmetic
		private const float HoldTolerance = 1e-4f;

		// After this long closed, the smooth mouth has eased shut (five release times)
		private const float LongClosedSeconds = 0.3f;

		private readonly List<float> _openStretches = new();
		private readonly List<float> _closedStretches = new();
		private CharacterPortraitSettings _settings;
		private int _syllableCount;
		private int _openCount;
		private float _openFraction;
		private float _maxOpenness;
		private float _maxOpennessLongClosed;

		#endregion

		#region Setup

		[OneTimeSetUp]
		public void SimulateTestLine()
		{
			_settings = ScriptableObject.CreateInstance<CharacterPortraitSettings>();
			VoiceAnalysis analysis = AnalyseTestLine();
			IReadOnlyList<VoiceOnset> onsets = analysis.Onsets;
			_syllableCount = onsets.Count;

			MouthFlap flap = new();
			int onsetIndex = 0;
			int frameCount = 0;
			int openFrames = 0;
			int stretchFrames = 0;
			int closedFramesInRow = 0;
			bool wasOpen = false;
			bool isFirstStretch = true;
			for (int frame = 0; frame * (double)FrameSeconds < analysis.DurationSeconds; frame++)
			{
				// As the card's voice reader does: the features just ahead of the playhead, and the syllables passed
				double aheadSeconds = frame * (double)FrameSeconds + _settings.LeadSeconds;
				bool isSyllableStart = false;
				while (onsetIndex < onsets.Count && onsets[onsetIndex].TimeSeconds <= aheadSeconds)
				{
					isSyllableStart = true;
					onsetIndex++;
				}

				flap.Step((float)analysis.Sample(aheadSeconds).Vowel, isSyllableStart, FrameSeconds, _settings);
				frameCount++;
				openFrames += flap.IsOpen ? 1 : 0;
				_maxOpenness = Mathf.Max(_maxOpenness, flap.Openness);
				closedFramesInRow = flap.IsOpen ? 0 : closedFramesInRow + 1;
				if (closedFramesInRow * FrameSeconds >= LongClosedSeconds)
				{
					_maxOpennessLongClosed = Mathf.Max(_maxOpennessLongClosed, flap.Openness);
				}

				// A stretch ends when the mouth changes; the first closed one (before any speech) has no minimum
				if (frame > 0 && flap.IsOpen != wasOpen)
				{
					List<float> stretches = wasOpen ? _openStretches : _closedStretches;
					if (wasOpen || !isFirstStretch)
					{
						stretches.Add(stretchFrames * FrameSeconds);
					}

					isFirstStretch = false;
					stretchFrames = 0;
				}

				stretchFrames++;
				wasOpen = flap.IsOpen;
			}

			_openCount = flap.OpenCount;
			_openFraction = (float)openFrames / frameCount;
		}

		[OneTimeTearDown]
		public void DestroySettings()
		{
			Object.DestroyImmediate(_settings);
		}

		#endregion

		#region Private

		private static VoiceAnalysis AnalyseTestLine()
		{
			string path = CharacterPortraitTestSceneBuilder.LineClipPath;
			AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
			Assert.That(clip, Is.Not.Null, $"Missing {path}");
			Assert.That(clip.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
			if (clip.loadState != AudioDataLoadState.Loaded)
			{
				clip.LoadAudioData();
			}

			float[] samples = new float[clip.samples * clip.channels];
			Assert.That(clip.GetData(samples, 0), Is.True, "Couldn't read the clip's samples");
			return VoiceAnalysis.Analyze(VoiceAnalysis.ToMono(samples, clip.channels), clip.frequency,
				VoiceAnalysisSettings.Defaults);
		}

		// Steps the mouth for a while with a steady vowel level and no new syllables
		private void StepFor(MouthFlap flap, float vowel, float seconds)
		{
			for (float elapsed = 0f; elapsed < seconds; elapsed += FrameSeconds)
			{
				flap.Step(vowel, false, FrameSeconds, _settings);
			}
		}

		#endregion

		#region Tests

		[Test]
		public void MouthOpensAboutOncePerSyllable()
		{
			Assert.That(_syllableCount, Is.EqualTo(21));
			Assert.That(_openCount, Is.InRange(15, 30));
		}

		[Test]
		public void MouthKeepsTheMinimumHoldTimes()
		{
			Assert.That(_openStretches, Is.Not.Empty);
			Assert.That(_openStretches, Is.All.GreaterThanOrEqualTo(_settings.MinOpenSeconds - HoldTolerance));
			Assert.That(_closedStretches, Is.All.GreaterThanOrEqualTo(_settings.MinClosedSeconds - HoldTolerance));
		}

		[Test]
		public void MouthIsOpenForPartOfTheLine()
		{
			Assert.That(_openFraction, Is.InRange(0.35f, 0.75f));
		}

		[Test]
		public void SmoothMouthOpensFullyAndEasesShut()
		{
			Assert.That(_maxOpenness, Is.GreaterThan(0.9f));
			Assert.That(_maxOpennessLongClosed, Is.LessThan(0.01f));
		}

		// A held vowel keeps the mouth open; a new syllable closes it for a moment, then the vowel opens it again
		[Test]
		public void NewSyllableFlapsAnOpenMouth()
		{
			MouthFlap flap = new();
			StepFor(flap, 1f, 0.2f);
			Assert.That(flap.IsOpen, Is.True);
			Assert.That(flap.OpenCount, Is.EqualTo(1));

			flap.Step(1f, true, FrameSeconds, _settings);
			Assert.That(flap.IsOpen, Is.False);

			StepFor(flap, 1f, 0.1f);
			Assert.That(flap.IsOpen, Is.True);
			Assert.That(flap.OpenCount, Is.EqualTo(2));

			// Silence closes it once the vowel level has fallen, then the smooth mouth eases shut while it stays closed
			StepFor(flap, 0f, LongClosedSeconds);
			Assert.That(flap.IsOpen, Is.False);

			StepFor(flap, 0f, LongClosedSeconds);
			Assert.That(flap.Openness, Is.LessThan(0.01f));
		}

		#endregion
	}
}
