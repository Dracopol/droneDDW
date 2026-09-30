namespace DroneDDW.Parks.Levels
{
	/// <summary>
	/// How an element enters and leaves the park. Values match the "_style" codes in the layout file.
	/// </summary>
	public enum ParkTransitionStyle
	{
		/// <summary>Switches on or off at once (used for objects that never change).</summary>
		None = 0,

		/// <summary>Grows out of its base or shrinks back into it (plants and props).</summary>
		Scale = 1,

		/// <summary>Rises out of the ground or sinks into it (paths, paving, water, buildings).</summary>
		Lift = 2,
	}
}
