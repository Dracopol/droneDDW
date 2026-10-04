using System;

namespace DroneDDW.Voice
{
	// One second-order low or high pass (makeBiquad), in double precision like the JavaScript it is ported from
	internal sealed class Biquad
	{
		#region Fields

		private readonly double _b0;
		private readonly double _b1;
		private readonly double _b2;
		private readonly double _a1;
		private readonly double _a2;
		private double _state1;
		private double _state2;

		#endregion

		#region Setup

		private Biquad(double b0, double b1, double b2, double a1, double a2)
		{
			_b0 = b0;
			_b1 = b1;
			_b2 = b2;
			_a1 = a1;
			_a2 = a2;
		}

		#endregion

		#region Internal

		internal static Biquad CreateLowPass(double cutoffHertz, double quality, double sampleRate)
		{
			return Create(true, cutoffHertz, quality, sampleRate);
		}

		internal static Biquad CreateHighPass(double cutoffHertz, double quality, double sampleRate)
		{
			return Create(false, cutoffHertz, quality, sampleRate);
		}

		internal double Step(double input)
		{
			double output = _b0 * input + _state1;
			_state1 = _b1 * input - _a1 * output + _state2;
			_state2 = _b2 * input - _a2 * output;
			return output;
		}

		#endregion

		#region Private

		private static Biquad Create(bool isLowPass, double cutoffHertz, double quality, double sampleRate)
		{
			double frequency = Math.Min(cutoffHertz, sampleRate * 0.45);
			double omega = 2 * Math.PI * frequency / sampleRate;
			double cosine = Math.Cos(omega);
			double alpha = Math.Sin(omega) / (2 * quality);
			double b0;
			double b1;
			double b2;
			if (isLowPass)
			{
				b0 = (1 - cosine) / 2;
				b1 = 1 - cosine;
				b2 = (1 - cosine) / 2;
			}
			else
			{
				b0 = (1 + cosine) / 2;
				b1 = -(1 + cosine);
				b2 = (1 + cosine) / 2;
			}

			double a0 = 1 + alpha;
			double a1 = -2 * cosine;
			double a2 = 1 - alpha;
			return new Biquad(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
		}

		#endregion
	}
}
