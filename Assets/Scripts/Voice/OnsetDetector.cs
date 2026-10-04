using System;

namespace DroneDDW.Voice
{
	// Syllable onsets: rising edges of the vowel envelope, a fast follower against a slow one (OnsetDetector)
	internal sealed class OnsetDetector
	{
		#region Fields

		private readonly VoiceAnalysisSettings _settings;
		private readonly double _hopSeconds;
		private readonly double _fastAttack;
		private readonly double _fastRelease;
		private readonly double _slowAttack;
		private readonly double _slowRelease;
		private double _fast;
		private double _slow;
		private bool _isArmed = true;
		private double _secondsSinceOnset = 1e9;

		#endregion

		#region Setup

		internal OnsetDetector(double hopSeconds, VoiceAnalysisSettings settings)
		{
			_settings = settings;
			_hopSeconds = hopSeconds;
			_fastAttack = Coefficient(hopSeconds, 0.008);
			_fastRelease = Coefficient(hopSeconds, 0.04);
			_slowAttack = Coefficient(hopSeconds, 0.09);
			_slowRelease = Coefficient(hopSeconds, 0.2);
		}

		#endregion

		#region Internal

		// The onset's strength (0.3 to 1), or 0 when this hop starts no syllable
		internal double Step(double vowel, double level)
		{
			_fast += (vowel - _fast) * (vowel > _fast ? _fastAttack : _fastRelease);
			_slow += (vowel - _slow) * (vowel > _slow ? _slowAttack : _slowRelease);
			_secondsSinceOnset += _hopSeconds;
			double difference = _fast - _slow;
			if (!_isArmed && difference < _settings.OnsetOffThreshold)
			{
				_isArmed = true;
			}

			if (_isArmed && difference > _settings.OnsetOnThreshold && level > 0.2
				&& _secondsSinceOnset >= _settings.OnsetMinGapSeconds)
			{
				_isArmed = false;
				_secondsSinceOnset = 0;
				return Math.Min(1, Math.Max(0.3, difference / 0.35));
			}

			return 0;
		}

		#endregion

		#region Private

		private static double Coefficient(double hopSeconds, double timeConstantSeconds)
		{
			return 1 - Math.Exp(-hopSeconds / timeConstantSeconds);
		}

		#endregion
	}
}
