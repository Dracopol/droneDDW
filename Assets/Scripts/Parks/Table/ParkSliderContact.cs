namespace DroneDDW.Parks.Table
{
	/// <summary>How a hand is touching the slider's handle, as reported by the input each frame.</summary>
	public enum ParkSliderContact
	{
		None,

		/// <summary>Close enough to grab.</summary>
		InReach,

		Pushed,
		Grabbed,
	}
}
