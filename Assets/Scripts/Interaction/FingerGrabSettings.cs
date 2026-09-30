using System;
using UnityEngine;

namespace DroneDDW.Interaction
{
	// The finger grab's tuning, shared by both hands and every scene that uses this asset: each finger's grab and release
	// distance, and whether the tuning labels show. An asset, so changes made in Play mode are kept.
	[CreateAssetMenu(fileName = "Finger Grab Settings", menuName = "DroneDDW/Interaction/Finger Grab Settings")]
	internal class FingerGrabSettings : ScriptableObject
	{
		#region Serialized fields

		[Tooltip("Shows the label by each hand with every finger's distance and thresholds")]
		[SerializeField] private bool _isReadoutVisible = true;

		[Header("Thumb tip to fingertip")]
		[SerializeField] private FingerGrabThreshold _index = new(0.03f, 0.045f);
		[SerializeField] private FingerGrabThreshold _middle = new(0.03f, 0.045f);
		[SerializeField] private FingerGrabThreshold _ring = new(0.035f, 0.05f);
		[SerializeField] private FingerGrabThreshold _little = new(0.035f, 0.05f);

		#endregion

		#region Properties

		internal bool IsReadoutVisible => _isReadoutVisible;

		#endregion

		#region Internal

		internal FingerGrabThreshold GetThreshold(GrabFinger finger)
		{
			return finger switch
			{
				GrabFinger.Index => _index,
				GrabFinger.Middle => _middle,
				GrabFinger.Ring => _ring,
				GrabFinger.Little => _little,
				_ => throw new ArgumentOutOfRangeException(nameof(finger), finger, null),
			};
		}

		#endregion
	}
}
