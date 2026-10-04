namespace DroneDDW.Voice
{
	// Where a voice reader's features come from right now
	public enum VoiceStatus
	{
		// Nothing is playing
		Idle = 0,

		// A clip started and is being analysed; the features stay at zero until it is done
		Analysing = 1,

		// The clip was analysed ahead, so the features are read at the playhead
		ClipAnalysis = 2,

		// The clip can't be read ahead, so the reader follows the live output
		LiveFallback = 3,
	}
}
