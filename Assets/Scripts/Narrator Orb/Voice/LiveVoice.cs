using System;
using System.Collections.Generic;

namespace DroneDDW.NarratorOrb.Voice
{
	// Live analysis for sources that can't be read ahead (LiveVoice). The reference levels follow the loudest recent
	// speech and relax by 2.5 dB per second, so it adapts to any input gain. Not thread safe: one thread pushes.
	internal sealed class LiveVoice
	{
		#region Fields

		private const double RelaxDecibelsPerSecond = 2.5;

		private readonly VoiceAnalysisSettings _settings;
		private readonly VoiceBands _bands;
		private readonly OnsetDetector _detector;
		private readonly List<double> _pendingOnsets = new();
		private readonly Action<VoiceBandLevels> _handleHop;
		private readonly double _relaxDecibelsPerHop;
		private double _referenceFullDecibels = -30;
		private double _referenceMidDecibels = -34;

		#endregion

		#region Properties

		internal VoiceFeatures Latest { get; private set; }

		#endregion

		#region Setup

		internal LiveVoice(int sampleRate, VoiceAnalysisSettings settings)
		{
			_settings = settings;
			_bands = new VoiceBands(sampleRate, settings.HopSeconds);
			_detector = new OnsetDetector(settings.HopSeconds, settings);
			_relaxDecibelsPerHop = RelaxDecibelsPerSecond * settings.HopSeconds;
			_handleHop = HandleHop;
		}

		#endregion

		#region Event handlers

		private void HandleHop(VoiceBandLevels bands)
		{
			_referenceFullDecibels = Math.Max(bands.FullDecibels,
				Math.Max(-45, _referenceFullDecibels - _relaxDecibelsPerHop));
			_referenceMidDecibels = Math.Max(bands.MidDecibels,
				Math.Max(-50, _referenceMidDecibels - _relaxDecibelsPerHop));
			VoiceFeatures features = VoiceMathUtil.MapBands(bands, _referenceFullDecibels, _referenceMidDecibels,
				_settings);
			Latest = features;
			double strength = _detector.Step(features.Vowel, features.Level);
			if (strength > 0)
			{
				_pendingOnsets.Add(strength);
			}
		}

		#endregion

		#region Internal

		internal void Push(float[] samples, int count)
		{
			_bands.Push(samples, 0, count, _handleHop);
		}

		// Moves the onsets found since the last call into the list (takeOnsets)
		internal void TakeOnsets(List<double> destination)
		{
			destination.AddRange(_pendingOnsets);
			_pendingOnsets.Clear();
		}

		#endregion
	}
}
