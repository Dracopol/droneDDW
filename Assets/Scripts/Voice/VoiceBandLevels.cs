namespace DroneDDW.Voice
{
	// One hop's energy per band in dB, after an 80 Hz high pass: full (everything), low (80-300 Hz, voicing), mid
	// (300-3000 Hz, vowels and formants) and high (4000 Hz and up, the hiss of s, sh, t and f)
	internal readonly struct VoiceBandLevels
	{
		#region Properties

		internal double FullDecibels { get; }
		internal double LowDecibels { get; }
		internal double MidDecibels { get; }
		internal double HighDecibels { get; }

		#endregion

		#region Setup

		internal VoiceBandLevels(double fullDecibels, double lowDecibels, double midDecibels, double highDecibels)
		{
			FullDecibels = fullDecibels;
			LowDecibels = lowDecibels;
			MidDecibels = midDecibels;
			HighDecibels = highDecibels;
		}

		#endregion
	}
}
