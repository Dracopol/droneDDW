namespace DroneDDW.TicketScan
{
	// One sighting of a QR code by a scanner: its text, the ticket number in it (if it is a ticket) and what the
	// scanner measured. Measurements a scanner can't make are NaN.
	internal readonly struct TicketRead
	{
		#region Properties

		internal string Payload { get; }

		// The participant number, or -1 when the code isn't a ticket
		internal int Number { get; }

		internal bool IsTicket => Number >= 0;

		// From the headset camera to the code
		internal float DistanceMeters { get; }

		// How many camera pixels one module of the code covered: below about 3 codes get hard to read
		internal float PixelsPerModule { get; }

		// How long finding and decoding the code took
		internal float DecodeMilliseconds { get; }

		// Time.realtimeSinceStartup when the scanner saw the code
		internal float TimeSeconds { get; }

		#endregion

		#region Setup

		internal TicketRead(string payload, float distanceMeters, float pixelsPerModule, float decodeMilliseconds,
			float timeSeconds)
		{
			Payload = payload;
			Number = TicketCodeUtil.TryGetNumber(payload, out int number) ? number : -1;
			DistanceMeters = distanceMeters;
			PixelsPerModule = pixelsPerModule;
			DecodeMilliseconds = decodeMilliseconds;
			TimeSeconds = timeSeconds;
		}

		#endregion
	}
}
