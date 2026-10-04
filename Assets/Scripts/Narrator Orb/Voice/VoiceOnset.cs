namespace DroneDDW.NarratorOrb.Voice
{
	// A syllable found in a clip: when it starts (seconds from the clip start) and how strong it is (0.3 to 1)
	internal readonly struct VoiceOnset
	{
		#region Properties

		internal double TimeSeconds { get; }
		internal double Strength { get; }

		#endregion

		#region Setup

		internal VoiceOnset(double timeSeconds, double strength)
		{
			TimeSeconds = timeSeconds;
			Strength = strength;
		}

		#endregion
	}
}
