namespace DroneDDW.Experience
{
	// When a waiting step's stuck lines come: the first one a while after the prompt ends, then one every repeat after
	// the previous stuck line ends (as in the Questionnaire tutorial: 8 s, then every 15 s)
	internal readonly struct StuckHintSchedule
	{
		#region Properties

		internal float FirstDelaySeconds { get; }
		internal float RepeatSeconds { get; }

		#endregion

		#region Setup

		internal StuckHintSchedule(float firstDelaySeconds, float repeatSeconds)
		{
			FirstDelaySeconds = firstDelaySeconds;
			RepeatSeconds = repeatSeconds;
		}

		#endregion

		#region Internal

		// How long to wait before stuck line `hint` (0 is the first): from the end of the prompt for the first, from the
		// end of the previous stuck line after that
		internal float GetDelaySeconds(int hint)
		{
			return hint == 0 ? FirstDelaySeconds : RepeatSeconds;
		}

		#endregion
	}
}
