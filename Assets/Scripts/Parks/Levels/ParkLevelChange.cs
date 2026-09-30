namespace DroneDDW.Parks.Levels
{
	/// <summary>
	/// Sent when the park starts moving to a new level.
	/// Previous is -1 the first time a level is applied.
	/// </summary>
	public readonly struct ParkLevelChange
	{
		#region Setup

		public ParkLevelChange(int previous, int next, bool isImmediate)
		{
			Previous = previous;
			Next = next;
			IsImmediate = isImmediate;
		}

		#endregion

		#region Properties

		public int Previous { get; }
		public int Next { get; }

		/// <summary>True when the park snapped to the level without a transition.</summary>
		public bool IsImmediate { get; }

		#endregion
	}
}
