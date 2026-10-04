namespace DroneDDW.Voice
{
	// What one moment of voice drives, each 0 to 1: loudness, vowels and hiss (mapBands)
	public readonly struct VoiceFeatures
	{
		#region Properties

		public static VoiceFeatures Silent => default;

		public double Level { get; }
		public double Vowel { get; }
		public double Hiss { get; }

		#endregion

		#region Setup

		public VoiceFeatures(double level, double vowel, double hiss)
		{
			Level = level;
			Vowel = vowel;
			Hiss = hiss;
		}

		#endregion
	}
}
