namespace DroneDDW.NarratorOrb
{
	// Where the orb's voice features come from right now
	internal enum NarratorOrbVoiceStatus
	{
		// Nothing is playing
		Idle = 0,

		// A clip started and is being analysed; the orb stays calm until it is done
		Analysing = 1,

		// The clip was analysed ahead, so the orb reads the features at the playhead
		ClipAnalysis = 2,

		// The clip can't be read ahead, so the orb follows the live output
		LiveFallback = 3,
	}
}
