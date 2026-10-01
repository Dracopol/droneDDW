using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace DroneDDW.FingerGrab
{
	// A grab button for one hand: the thumb tip against any fingertip, or the thumb's knuckle against any finger's
	// middle knuckle, each pair with its own distances, so nothing has to touch. Goes in an interactor's select input as
	// an Object Reference, like the rig's pinch reader. Reads the joints from the hand subsystem, independent of which
	// hand visuals are active.
	[DefaultExecutionOrder(XRInteractionUpdateOrder.k_XRInputDeviceButtonReader)]
	internal class FingerGrabButtonReader : MonoBehaviour, IXRInputButtonReader
	{
		#region Events

		// The pair holding the grab changed, null when the grab ends
		internal event Action<GrabPair?> OnHoldingPairChange;

		#endregion

		#region Serialized fields

		[SerializeField] private Handedness _handedness = Handedness.Right;

		[Tooltip("The XR Origin's camera offset. Hand joints are relative to it.")]
		[SerializeField] private Transform _trackingSpace;

		[Tooltip("The scene's Finger Grab Settings object. Both hands read their thresholds from it.")]
		[SerializeField] private FingerGrabSettingsHolder _settingsHolder;

		#endregion

		#region Fields

		private const int PairCount = 8;
		private const float MinimumDistanceMeters = 0.001f;

		// Each pair's two joints, in GrabPair order: the thumb tip with the fingertips, then the thumb's middle joint
		// (where it bends) with the fingers' middle knuckles
		private static readonly XRHandJointID[] ThumbJoints =
		{
			XRHandJointID.ThumbTip,
			XRHandJointID.ThumbTip,
			XRHandJointID.ThumbTip,
			XRHandJointID.ThumbTip,
			XRHandJointID.ThumbDistal,
			XRHandJointID.ThumbDistal,
			XRHandJointID.ThumbDistal,
			XRHandJointID.ThumbDistal,
		};

		private static readonly XRHandJointID[] FingerJoints =
		{
			XRHandJointID.IndexTip,
			XRHandJointID.MiddleTip,
			XRHandJointID.RingTip,
			XRHandJointID.LittleTip,
			XRHandJointID.IndexIntermediate,
			XRHandJointID.MiddleIntermediate,
			XRHandJointID.RingIntermediate,
			XRHandJointID.LittleIntermediate,
		};

		private static readonly List<XRHandSubsystem> Subsystems = new();

		private readonly float[] _distancesMeters = new float[PairCount];
		private readonly Vector3[] _grabCenters = new Vector3[PairCount];
		private readonly bool[] _isPairClosed = new bool[PairCount];
		private XRHandSubsystem _subsystem;
		private FingerGrabSettings _fallbackSettings;
		private Vector3 _thumbTipPosition;
		private bool _isTracked;
		private bool _isPerformed;
		private bool _wasPerformedThisFrame;
		private bool _wasCompletedThisFrame;
		private float _value;
		private GrabPair? _holdingPair;
		private GrabPair _closestPair;

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

		// The pair holding the current grab, null while not grabbing
		internal GrabPair? HoldingPair => _holdingPair;

		// The pair nearest to grabbing, measured against its own grab distance
		internal GrabPair ClosestPair => _closestPair;

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
			UpdatePairs();
			UpdateHoldingPair();

			_isPerformed = _holdingPair.HasValue;
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

		// 1 while a pair is closed, otherwise how near the nearest pair is to grabbing (0 at its release distance)
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

		internal FingerGrabThreshold GetThreshold(GrabPair pair)
		{
			return Settings.GetThreshold(pair);
		}

		internal float GetDistanceMeters(GrabPair pair)
		{
			return _distancesMeters[(int)pair];
		}

		internal bool IsPairClosed(GrabPair pair)
		{
			return _isPairClosed[(int)pair];
		}

		// Midway between the pair's two joints, world space, updated every frame while the hand is tracked
		internal Vector3 GetGrabCenter(GrabPair pair)
		{
			return _grabCenters[(int)pair];
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
			for (int index = 0; index < PairCount; index++)
			{
				// Distances between the raw joint poses are real-world meters, whatever the rig's scale
				if (hand.GetJoint(ThumbJoints[index]).TryGetPose(out Pose thumbJoint)
					&& hand.GetJoint(FingerJoints[index]).TryGetPose(out Pose fingerJoint))
				{
					_distancesMeters[index] = Vector3.Distance(thumbJoint.position, fingerJoint.position);
					_grabCenters[index] = ToWorld((thumbJoint.position + fingerJoint.position) * 0.5f);
				}
				else
				{
					_distancesMeters[index] = float.PositiveInfinity;
					_grabCenters[index] = _thumbTipPosition;
				}
			}

			return true;
		}

		// A pair closes below its grab distance and opens again only above its release distance
		private void UpdatePairs()
		{
			float closestRatio = float.PositiveInfinity;
			float closeness = 0f;

			for (int index = 0; index < PairCount; index++)
			{
				FingerGrabThreshold threshold = GetThreshold((GrabPair)index);
				if (!_isTracked || !threshold.IsEnabled)
				{
					_isPairClosed[index] = false;
					continue;
				}

				float distanceMeters = _distancesMeters[index];
				float limitMeters = _isPairClosed[index] ? threshold.ReleaseDistanceMeters : threshold.GrabDistanceMeters;
				_isPairClosed[index] = distanceMeters <= limitMeters;

				float ratio = GetGrabRatio(index);
				if (ratio < closestRatio)
				{
					closestRatio = ratio;
					_closestPair = (GrabPair)index;
				}

				float pairCloseness = _isPairClosed[index]
					? 1f
					: Mathf.InverseLerp(threshold.ReleaseDistanceMeters, threshold.GrabDistanceMeters, distanceMeters);
				closeness = Mathf.Max(closeness, pairCloseness);
			}

			_value = closeness;
		}

		// The holding pair keeps the grab while it stays closed. If it opens while another pair is still closed, that
		// pair takes over, so the grab doesn't drop (a pinch can close into a fist, or a fist open into a pinch).
		private void UpdateHoldingPair()
		{
			GrabPair? holdingPair = _holdingPair;
			if (!holdingPair.HasValue || !_isPairClosed[(int)holdingPair.Value])
			{
				holdingPair = FindMostClosedPair();
			}

			if (holdingPair == _holdingPair)
			{
				return;
			}

			_holdingPair = holdingPair;
			OnHoldingPairChange?.Invoke(holdingPair);
		}

		private GrabPair? FindMostClosedPair()
		{
			GrabPair? mostClosedPair = null;
			float smallestRatio = float.PositiveInfinity;

			for (int index = 0; index < PairCount; index++)
			{
				if (!_isPairClosed[index])
				{
					continue;
				}

				float ratio = GetGrabRatio(index);
				if (ratio < smallestRatio)
				{
					smallestRatio = ratio;
					mostClosedPair = (GrabPair)index;
				}
			}

			return mostClosedPair;
		}

		// Below 1 while the pair is inside its grab distance
		private float GetGrabRatio(int index)
		{
			float grabDistanceMeters = Mathf.Max(GetThreshold((GrabPair)index).GrabDistanceMeters, MinimumDistanceMeters);
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
