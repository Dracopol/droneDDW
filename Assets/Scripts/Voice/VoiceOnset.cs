namespace DroneDDW.Voice
{
	// A syllable found in a clip: when it starts (seconds from the clip start) and how strong it is (0.3 to 1)
	public readonly struct VoiceOnset
	{
		#region Properties

		public double TimeSeconds { get; }
		public double Strength { get; }

		#endregion

		#region Setup

		public VoiceOnset(double timeSeconds, double strength)
		{
			TimeSeconds = timeSeconds;
			Strength = strength;
		}

		#endregion
	}
}
