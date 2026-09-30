using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace DroneDDW.Interaction
{
	// Sits between the thumb tip and the grabbing fingertip (the closest finger while not grabbing), so grabs attach and
	// near hovering happens where the fingers actually close. Turns with the rig's pinch pose, so held objects keep the
	// orientation they have with the pinch.
	[DefaultExecutionOrder(XRInteractionUpdateOrder.k_XRInputDeviceButtonReader + 1)]
	internal class FingerGrabPoint : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private FingerGrabButtonReader _reader;

		[Tooltip("The rig's pinch pose. Gives the rotation, and the position while the hand isn't tracked.")]
		[SerializeField] private Transform _pinchPose;

		#endregion

		#region Fields

		private Transform _transform;

		#endregion

		#region Setup

		private void Awake()
		{
			_transform = transform;
		}

		#endregion

		#region Update

		private void Update()
		{
			if (!_reader.IsTracked)
			{
				_transform.SetPositionAndRotation(_pinchPose.position, _pinchPose.rotation);
				return;
			}

			GrabFinger finger = _reader.HoldingFinger ?? _reader.ClosestFinger;
			Vector3 midpoint = (_reader.ThumbTipPosition + _reader.GetFingertipPosition(finger)) * 0.5f;
			_transform.SetPositionAndRotation(midpoint, _pinchPose.rotation);
		}

		#endregion

		#region Internal

		internal void Configure(FingerGrabButtonReader reader, Transform pinchPose)
		{
			_reader = reader;
			_pinchPose = pinchPose;
		}

		#endregion
	}
}
