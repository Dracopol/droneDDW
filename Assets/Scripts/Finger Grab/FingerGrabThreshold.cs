using System;
using UnityEngine;

namespace DroneDDW.FingerGrab
{
	// How close the two joints of a grab pair (thumb tip and fingertip, or thumb knuckle and finger knuckle) have to come
	// to grab, and how far apart they have to go again to let go
	[Serializable]
	internal struct FingerGrabThreshold
	{
		#region Serialized fields

		[SerializeField] private bool _isEnabled;

		[Tooltip("Distance between the pair's two joints that starts a grab")]
		[Min(0f)]
		[SerializeField] private float _grabDistanceMeters;

		[Tooltip("Distance between the pair's two joints that ends the grab. "
			+ "Larger than the grab distance, so the grab doesn't flicker at the edge.")]
		[Min(0f)]
		[SerializeField] private float _releaseDistanceMeters;

		#endregion

		#region Properties

		internal bool IsEnabled => _isEnabled;
		internal float GrabDistanceMeters => _grabDistanceMeters;

		// Never below the grab distance, so a closed finger can always open again
		internal float ReleaseDistanceMeters => Mathf.Max(_releaseDistanceMeters, _grabDistanceMeters);

		#endregion

		#region Setup

		internal FingerGrabThreshold(float grabDistanceMeters, float releaseDistanceMeters)
		{
			_isEnabled = true;
			_grabDistanceMeters = grabDistanceMeters;
			_releaseDistanceMeters = releaseDistanceMeters;
		}

		#endregion
	}
}
