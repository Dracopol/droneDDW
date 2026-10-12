using System;
using UnityEngine;

namespace DroneDDW.TicketScan
{
	// A way of reading tickets' QR codes with the headset. A scanner reports every code it sees through OnRead, over
	// and over while a code stays in view, and describes what it is doing in Status (waiting for a permission, starting
	// the camera...). The admin menu's check-in screen switches it on while it shows, so it costs nothing afterwards.
	public abstract class TicketScanner : MonoBehaviour
	{
		#region Events

		public event Action<TicketRead> OnRead;

		#endregion

		#region Properties

		// A few lines about the scanner's state. It builds the text on each get: read it a few times a second at most.
		public abstract string Status { get; }

		// The camera image the scanner reads, or null when it has none to show
		public virtual Texture Preview => null;

		// Whether it is looking for codes now; a scanner that was switched off frees the camera
		public abstract bool IsScanning { get; set; }

		#endregion

		#region Protected

		protected void RaiseRead(TicketRead read)
		{
			OnRead?.Invoke(read);
		}

		#endregion
	}
}
