using System;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace DroneDDW.Interaction
{
	// Hand tracking state and palm poses, independent of which hand visuals the rig has active
	internal class HandTrackingMonitor : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private XRHandTrackingEvents _leftHandTracking;
		[SerializeField] private XRHandTrackingEvents _rightHandTracking;

		[Tooltip("Palm bones of the rig's hand skeletons, used to read where the player's palms are")]
		[SerializeField] private Transform _leftPalm;
		[SerializeField] private Transform _rightPalm;

		#endregion

		#region Setup

		private void OnEnable()
		{
			_leftHandTracking.trackingChanged.AddListener(HandleLeftHandTrackingChange);
			_rightHandTracking.trackingChanged.AddListener(HandleRightHandTrackingChange);
		}

		private void OnDisable()
		{
			_leftHandTracking.trackingChanged.RemoveListener(HandleLeftHandTrackingChange);
			_rightHandTracking.trackingChanged.RemoveListener(HandleRightHandTrackingChange);
		}

		private void OnValidate()
		{
			// XRHandTrackingEvents defaults to the right hand, so a freshly added left one is easy to miss
			WarnIfWrongHand(_leftHandTracking, Handedness.Left);
			WarnIfWrongHand(_rightHandTracking, Handedness.Right);
		}

		#endregion

		#region Event handlers

		private void HandleLeftHandTrackingChange(bool isTracked)
		{
			LogTrackingChange(Handedness.Left, isTracked);
		}

		private void HandleRightHandTrackingChange(bool isTracked)
		{
			LogTrackingChange(Handedness.Right, isTracked);
		}

		#endregion

		#region Internal

		internal bool IsHandTracked(Handedness handedness)
		{
			return handedness switch
			{
				Handedness.Left => _leftHandTracking.handIsTracked,
				Handedness.Right => _rightHandTracking.handIsTracked,
				_ => throw new ArgumentOutOfRangeException(nameof(handedness), handedness, null),
			};
		}

		// Fails while the hand isn't tracked, the skeleton keeps its last pose when tracking drops
		internal bool TryGetPalmPose(Handedness handedness, out Pose palmPose)
		{
			if (!IsHandTracked(handedness))
			{
				palmPose = default;
				return false;
			}

			Transform palm = handedness == Handedness.Left ? _leftPalm : _rightPalm;
			palmPose = new Pose(palm.position, palm.rotation);
			return true;
		}

		#endregion

		#region Private

		private void LogTrackingChange(Handedness handedness, bool isTracked)
		{
			Debug.Log($"[DDW Hands] {handedness} hand {(isTracked ? "tracked" : "lost")}", this);
		}

		private void WarnIfWrongHand(XRHandTrackingEvents handTracking, Handedness expectedHandedness)
		{
			if (handTracking == null || handTracking.handedness == expectedHandedness)
				return;

			Debug.LogWarning(
				$"{name}: the {expectedHandedness} hand slot points to an XRHandTrackingEvents "
				+ $"set to {handTracking.handedness}",
				this);
		}

		#endregion
	}
}
