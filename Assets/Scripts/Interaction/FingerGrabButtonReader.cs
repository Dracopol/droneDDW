using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace DroneDDW.Interaction
{
	// A grab button for one hand: the thumb tip against any fingertip, each finger with its own distances, so the
	// fingertips don't have to touch. Goes in an interactor's select input as an Object Reference, like the rig's
	// pinch reader. Reads the joints from the hand subsystem, independent of which hand visuals are active.
	[DefaultExecutionOrder(XRInteractionUpdateOrder.k_XRInputDeviceButtonReader)]
	internal class FingerGrabButtonReader : MonoBehaviour, IXRInputButtonReader
	{
		#region Events

		// The finger holding the grab changed, null when the grab ends
		internal event Action<GrabFinger?> OnHoldingFingerChange;

		#endregion

		#region Serialized fields

		[SerializeField] private Handedness _handedness = Handedness.Right;

		[Tooltip("The XR Origin's camera offset. Hand joints are relative to it.")]
		[SerializeField] private Transform _trackingSpace;

		[Tooltip("The scene's Finger Grab Settings object. Both hands read their thresholds from it.")]
		[SerializeField] private FingerGrabSettingsHolder _settingsHolder;

		#endregion

		#region Fields

		private const int FingerCount = 4;
		private const float MinimumDistanceMeters = 0.001f;

		private static readonly XRHandJointID[] FingertipJoints =
		{
			XRHandJointID.IndexTip,
			XRHandJointID.MiddleTip,
			XRHandJointID.RingTip,
			XRHandJointID.LittleTip,
		};

		private static readonly List<XRHandSubsystem> Subsystems = new();

		private readonly float[] _distancesMeters = new float[FingerCount];
		private readonly Vector3[] _fingertipPositions = new Vector3[FingerCount];
		private readonly bool[] _isFingerClosed = new bool[FingerCount];
		private XRHandSubsystem _subsystem;
		private FingerGrabSettings _fallbackSettings;
		private Vector3 _thumbTipPosition;
		private bool _isTracked;
		private bool _isPerformed;
		private bool _wasPerformedThisFrame;
		private bool _wasCompletedThisFrame;
		private float _value;
		private GrabFinger? _holdingFinger;
		private GrabFinger _closestFinger;

		#endregion

		#region Properties

		internal Handedness Handedness => _handedness;
		internal bool IsTracked => _isTracked;

		// The holder's asset, or the built-in defaults when there is none
		internal FingerGrabSettings Settings
		{
			get
			{
				if (_settingsHolder != null && _settingsHolder.Settings != null)
				{
					return _settingsHolder.Settings;
				}

				if (_fallbackSettings == null)
				{
					Debug.LogWarning(
						$"[DDW Finger Grab] {name} has no Finger Grab Settings to read, using the defaults.", this);
					_fallbackSettings = ScriptableObject.CreateInstance<FingerGrabSettings>();
				}

				return _fallbackSettings;
			}
		}

		// The finger holding the current grab, null while not grabbing
		internal GrabFinger? HoldingFinger => _holdingFinger;

		// The finger nearest to grabbing, measured against its own grab distance
		internal GrabFinger ClosestFinger => _closestFinger;

		// World space, updated every frame while the hand is tracked
		internal Vector3 ThumbTipPosition => _thumbTipPosition;

		#endregion

		#region Setup

		private void OnDestroy()
		{
			if (_fallbackSettings != null)
			{
				Destroy(_fallbackSettings);
			}
		}

		#endregion

		#region Update

		private void Update()
		{
			bool wasPerformed = _isPerformed;
			_isTracked = TryReadJoints();
			UpdateFingers();
			UpdateHoldingFinger();

			_isPerformed = _holdingFinger.HasValue;
			_wasPerformedThisFrame = !wasPerformed && _isPerformed;
			_wasCompletedThisFrame = wasPerformed && !_isPerformed;
		}

		#endregion

		#region Public

		public bool ReadIsPerformed()
		{
			return _isPerformed;
		}

		public bool ReadWasPerformedThisFrame()
		{
			return _wasPerformedThisFrame;
		}

		public bool ReadWasCompletedThisFrame()
		{
			return _wasCompletedThisFrame;
		}

		// 1 while a finger is closed, otherwise how near the nearest finger is to grabbing (0 at its release distance)
		public float ReadValue()
		{
			return _value;
		}

		public bool TryReadValue(out float value)
		{
			value = _value;
			return _isTracked;
		}

		#endregion

		#region Internal

		internal FingerGrabThreshold GetThreshold(GrabFinger finger)
		{
			return Settings.GetThreshold(finger);
		}

		internal float GetDistanceMeters(GrabFinger finger)
		{
			return _distancesMeters[(int)finger];
		}

		internal bool IsFingerClosed(GrabFinger finger)
		{
			return _isFingerClosed[(int)finger];
		}

		// World space, updated every frame while the hand is tracked
		internal Vector3 GetFingertipPosition(GrabFinger finger)
		{
			return _fingertipPositions[(int)finger];
		}

		internal void Configure(Handedness handedness, Transform trackingSpace, FingerGrabSettingsHolder settingsHolder)
		{
			_handedness = handedness;
			_trackingSpace = trackingSpace;
			_settingsHolder = settingsHolder;
		}

		#endregion

		#region Private

		private bool TryReadJoints()
		{
			if (!TryGetSubsystem(out XRHandSubsystem subsystem))
			{
				return false;
			}

			XRHand hand = _handedness == Handedness.Left ? subsystem.leftHand : subsystem.rightHand;
			if (!hand.isTracked || !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumbTip))
			{
				return false;
			}

			_thumbTipPosition = ToWorld(thumbTip.position);
			for (int index = 0; index < FingerCount; index++)
			{
				// Distances between the raw joint poses are real-world meters, whatever the rig's scale
				if (hand.GetJoint(FingertipJoints[index]).TryGetPose(out Pose fingertip))
				{
					_distancesMeters[index] = Vector3.Distance(thumbTip.position, fingertip.position);
					_fingertipPositions[index] = ToWorld(fingertip.position);
				}
				else
				{
					_distancesMeters[index] = float.PositiveInfinity;
					_fingertipPositions[index] = _thumbTipPosition;
				}
			}

			return true;
		}

		// A finger closes below its grab distance and opens again only above its release distance
		private void UpdateFingers()
		{
			float closestRatio = float.PositiveInfinity;
			float closeness = 0f;

			for (int index = 0; index < FingerCount; index++)
			{
				FingerGrabThreshold threshold = GetThreshold((GrabFinger)index);
				if (!_isTracked || !threshold.IsEnabled)
				{
					_isFingerClosed[index] = false;
					continue;
				}

				float distanceMeters = _distancesMeters[index];
				float limitMeters = _isFingerClosed[index] ? threshold.ReleaseDistanceMeters : threshold.GrabDistanceMeters;
				_isFingerClosed[index] = distanceMeters <= limitMeters;

				float ratio = GetGrabRatio(index);
				if (ratio < closestRatio)
				{
					closestRatio = ratio;
					_closestFinger = (GrabFinger)index;
				}

				float fingerCloseness = _isFingerClosed[index]
					? 1f
					: Mathf.InverseLerp(threshold.ReleaseDistanceMeters, threshold.GrabDistanceMeters, distanceMeters);
				closeness = Mathf.Max(closeness, fingerCloseness);
			}

			_value = closeness;
		}

		// The holding finger keeps the grab while it stays closed. If it opens while another finger is still closed,
		// that finger takes over, so the grab doesn't drop.
		private void UpdateHoldingFinger()
		{
			GrabFinger? holdingFinger = _holdingFinger;
			if (!holdingFinger.HasValue || !_isFingerClosed[(int)holdingFinger.Value])
			{
				holdingFinger = FindMostClosedFinger();
			}

			if (holdingFinger == _holdingFinger)
			{
				return;
			}

			_holdingFinger = holdingFinger;
			OnHoldingFingerChange?.Invoke(holdingFinger);
		}

		private GrabFinger? FindMostClosedFinger()
		{
			GrabFinger? mostClosedFinger = null;
			float smallestRatio = float.PositiveInfinity;

			for (int index = 0; index < FingerCount; index++)
			{
				if (!_isFingerClosed[index])
				{
					continue;
				}

				float ratio = GetGrabRatio(index);
				if (ratio < smallestRatio)
				{
					smallestRatio = ratio;
					mostClosedFinger = (GrabFinger)index;
				}
			}

			return mostClosedFinger;
		}

		// Below 1 while the finger is inside its grab distance
		private float GetGrabRatio(int index)
		{
			float grabDistanceMeters = Mathf.Max(GetThreshold((GrabFinger)index).GrabDistanceMeters, MinimumDistanceMeters);
			return _distancesMeters[index] / grabDistanceMeters;
		}

		private Vector3 ToWorld(Vector3 trackingPosition)
		{
			return _trackingSpace != null ? _trackingSpace.TransformPoint(trackingPosition) : trackingPosition;
		}

		private bool TryGetSubsystem(out XRHandSubsystem subsystem)
		{
			if (_subsystem == null || !_subsystem.running)
			{
				_subsystem = null;
				SubsystemManager.GetSubsystems(Subsystems);
				foreach (XRHandSubsystem candidate in Subsystems)
				{
					if (candidate.running)
					{
						_subsystem = candidate;
						break;
					}
				}
			}

			subsystem = _subsystem;
			return subsystem != null;
		}

		#endregion
	}
}
