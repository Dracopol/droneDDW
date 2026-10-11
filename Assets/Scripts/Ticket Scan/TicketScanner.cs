using System;
using UnityEngine;

namespace DroneDDW.TicketScan
{
	// A way of reading tickets' QR codes with the headset. A scanner reports every code it sees through OnRead, over
	// and over while a code stays in view, and describes what it is doing in Status (waiting for a permission, starting
	// the camera...) for the test panel.
	internal abstract class TicketScanner : MonoBehaviour
	{
		#region Events

		internal event Action<TicketRead> OnRead;

		#endregion

		#region Properties

		// A few lines about the scanner's state. It builds the text on each get: read it a few times a second at most.
		internal abstract string Status { get; }

		// The camera image the scanner reads, or null when it has none to show
		internal virtual Texture Preview => null;

		#endregion

		#region Protected

		protected void RaiseRead(TicketRead read)
		{
			OnRead?.Invoke(read);
		}

		#endregion
	}
}
