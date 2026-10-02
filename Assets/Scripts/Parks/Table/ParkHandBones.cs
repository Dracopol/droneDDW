using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace DroneDDW.Parks.Table
{
	/// <summary>
	/// Both tracked hands as thick bone segments in world space, read straight from the hand subsystem, so they don't
	/// depend on which hand visuals the rig has switched on. Same bones and thicknesses as the Push Slider Test's hand
	/// skeleton. A hand that isn't tracked has no bones, so a lost hand can't keep pushing.
	/// Refreshed by whoever reads it (<see cref="ParkSliderHandDriver"/>), right before use.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Hand Bones")]
	public sealed class ParkHandBones : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField, Tooltip("The XR Origin's camera offset. Hand joints are relative to it.")]
		private Transform _trackingSpace;

		#endregion

		#region Fields

		private const float WristRadiusMeters = 0.02f;
		private const float PalmRadiusMeters = 0.015f;
		private const float ThumbBaseRadiusMeters = 0.013f;
		private const float FingerBaseRadiusMeters = 0.01f;
		private const float FingerMiddleRadiusMeters = 0.009f;
		private const float FingerTipRadiusMeters = 0.008f;

		// The metacarpal joints sit near the wrist, so the metacarpal segments fan out across the palm
		private static readonly (XRHandJointID Start, XRHandJointID End, float RadiusMeters)[] BoneDefinitions =
		{
			(XRHandJointID.Wrist, XRHandJointID.Palm, WristRadiusMeters),

			(XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, PalmRadiusMeters),
			(XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, PalmRadiusMeters),
			(XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, PalmRadiusMeters),
			(XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, PalmRadiusMeters),

			(XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, ThumbBaseRadiusMeters),
			(XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, FingerBaseRadiusMeters),
			(XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip, FingerTipRadiusMeters),

			(XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, FingerBaseRadiusMeters),
			(XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, FingerMiddleRadiusMeters),
			(XRHandJointID.IndexDistal, XRHandJointID.IndexTip, FingerTipRadiusMeters),

			(XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, FingerBaseRadiusMeters),
			(XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, FingerMiddleRadiusMeters),
			(XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip, FingerTipRadiusMeters),

			(XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, FingerBaseRadiusMeters),
			(XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, FingerMiddleRadiusMeters),
			(XRHandJointID.RingDistal, XRHandJointID.RingTip, FingerTipRadiusMeters),

			(XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, FingerBaseRadiusMeters),
			(XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, FingerMiddleRadiusMeters),
			(XRHandJointID.LittleDistal, XRHandJointID.LittleTip, FingerTipRadiusMeters),
		};

		private static readonly List<XRHandSubsystem> Subsystems = new List<XRHandSubsystem>();

		private readonly List<ParkHandBone> _bones = new List<ParkHandBone>();
		private XRHandSubsystem _subsystem;

		#endregion

		#region Properties

		/// <summary>The bones of every tracked hand, as of the last <see cref="Refresh"/>.</summary>
		internal IReadOnlyList<ParkHandBone> Bones => _bones;

		#endregion

		#region Internal

		internal void Refresh()
		{
			_bones.Clear();
			if (!TryGetSubsystem(out XRHandSubsystem subsystem))
			{
				return;
			}

			AddBones(subsystem.leftHand);
			AddBones(subsystem.rightHand);
		}

		internal void Configure(Transform trackingSpace)
		{
			_trackingSpace = trackingSpace;
		}

		#endregion

		#region Private

		private void AddBones(XRHand hand)
		{
			if (!hand.isTracked)
			{
				return;
			}

			foreach ((XRHandJointID start, XRHandJointID end, float radiusMeters) in BoneDefinitions)
			{
				if (hand.GetJoint(start).TryGetPose(out Pose startPose)
					&& hand.GetJoint(end).TryGetPose(out Pose endPose))
				{
					_bones.Add(new ParkHandBone(ToWorld(startPose.position), ToWorld(endPose.position), radiusMeters));
				}
			}
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
