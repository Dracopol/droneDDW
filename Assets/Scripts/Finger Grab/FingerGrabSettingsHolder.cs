using UnityEngine;

namespace DroneDDW.FingerGrab
{
	// The scene's finger grab settings, on the "Finger Grab Settings" object. Both hands read from here, so swapping the
	// asset switches them together. Its inspector shows the asset's values in place.
	internal class FingerGrabSettingsHolder : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("Shared by every scene that uses it. Changes made in Play mode are kept.")]
		[SerializeField] private FingerGrabSettings _settings;

		#endregion

		#region Properties

		internal FingerGrabSettings Settings => _settings;

		#endregion

		#region Internal

		internal void Configure(FingerGrabSettings settings)
		{
			_settings = settings;
		}

		#endregion
	}
}
