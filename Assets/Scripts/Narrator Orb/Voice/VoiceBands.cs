using System;

namespace DroneDDW.NarratorOrb.Voice
{
	// Streams samples in and reports each hop's band levels (VoiceBands)
	internal sealed class VoiceBands
	{
		#region Fields

		private const double Quality = 0.707;

		private readonly int _hopSampleCount;
		private readonly Biquad _highPass80;
		private readonly Biquad _lowPass300;
		private readonly Biquad _highPass300;
		private readonly Biquad _lowPass3000;
		private readonly Biquad _highPass4000;
		private int _sampleCount;
		private double _fullSum;
		private double _lowSum;
		private double _midSum;
		private double _highSum;

		#endregion

		#region Setup

		internal VoiceBands(int sampleRate, double hopSeconds)
		{
			_hopSampleCount = Math.Max(1, (int)VoiceMathUtil.RoundHalfUp(sampleRate * hopSeconds));
			_highPass80 = Biquad.CreateHighPass(80, Quality, sampleRate);
			_lowPass300 = Biquad.CreateLowPass(300, Quality, sampleRate);
			_highPass300 = Biquad.CreateHighPass(300, Quality, sampleRate);
			_lowPass3000 = Biquad.CreateLowPass(3000, Quality, sampleRate);
			_highPass4000 = Biquad.CreateHighPass(4000, Quality, sampleRate);
		}

		#endregion

		#region Internal

		internal void Push(float[] samples, int offset, int count, Action<VoiceBandLevels> onHop)
		{
			for (int i = offset; i < offset + count; i++)
			{
				double full = _highPass80.Step(samples[i]);
				double low = _lowPass300.Step(full);
				double mid = _lowPass3000.Step(_highPass300.Step(full));
				double high = _highPass4000.Step(full);
				_fullSum += full * full;
				_lowSum += low * low;
				_midSum += mid * mid;
				_highSum += high * high;

				if (++_sampleCount != _hopSampleCount)
				{
					continue;
				}

				double inverse = 1.0 / _sampleCount;
				onHop(new VoiceBandLevels(ToDecibels(_fullSum, inverse), ToDecibels(_lowSum, inverse),
					ToDecibels(_midSum, inverse), ToDecibels(_highSum, inverse)));
				_sampleCount = 0;
				_fullSum = 0;
				_lowSum = 0;
				_midSum = 0;
				_highSum = 0;
			}
		}

		#endregion

		#region Private

		private static double ToDecibels(double sum, double inverseCount)
		{
			return 10 * Math.Log10(sum * inverseCount + 1e-12);
		}

		#endregion
	}
}
