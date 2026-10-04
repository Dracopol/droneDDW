namespace DroneDDW.NarratorOrb.Voice
{
	// What one moment of voice drives, each 0 to 1: loudness, vowels and hiss (mapBands)
	internal readonly struct VoiceFeatures
	{
		#region Properties

		internal static VoiceFeatures Silent => default;

		internal double Level { get; }
		internal double Vowel { get; }
		internal double Hiss { get; }

		#endregion

		#region Setup

		internal VoiceFeatures(double level, double vowel, double hiss)
		{
			Level = level;
			Vowel = vowel;
			Hiss = hiss;
		}

		#endregion
	}
}
