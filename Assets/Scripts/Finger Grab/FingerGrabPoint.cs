using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace DroneDDW.FingerGrab
{
	// Sits between the joints of the grabbing pair (the closest pair while not grabbing): the thumb tip and a fingertip,
	// or the thumb's knuckle and a finger's knuckle. So grabs attach and near hovering happens where the hand actually
	// closes. When another pair takes over a grab, the point stays where it was and moves on with the new pair, so what
	// the hand holds doesn't jump. Turns with the rig's pinch pose, so held objects keep the orientation they have with
	// the pinch.
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
		private GrabPair? _holdingPair;

		// From the holding pair's centre to the point, in the pinch pose's space, after another pair took over the grab
		private Vector3 _handOffOffset;

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
				_holdingPair = null;
				_handOffOffset = Vector3.zero;
				_transform.SetPositionAndRotation(_pinchPose.position, _pinchPose.rotation);
				return;
			}

			GrabPair? holdingPair = _reader.HoldingPair;
			Quaternion rotation = _pinchPose.rotation;
			Vector3 center = _reader.GetGrabCenter(holdingPair ?? _reader.ClosestPair);
			if (!holdingPair.HasValue)
			{
				_handOffOffset = Vector3.zero;
			}
			else if (_holdingPair.HasValue && holdingPair != _holdingPair)
			{
				// The knuckles' centre is a few centimetres from the fingertips', so keep the point where it was
				_handOffOffset = Quaternion.Inverse(rotation) * (_transform.position - center);
			}

			_holdingPair = holdingPair;
			_transform.SetPositionAndRotation(center + rotation * _handOffOffset, rotation);
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
