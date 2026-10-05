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
		#region Entities

		private enum Thickness
		{
			Wrist,
			Palm,
			ThumbBase,
			FingerBase,
			FingerMiddle,
			FingerTip,
		}

		#endregion

		#region Serialized fields

		[SerializeField, Tooltip("The XR Origin's camera offset. Hand joints are relative to it.")]
		private Transform _trackingSpace;

		[Header("Bone radii (the push adds its touch margin on top)")]
		[SerializeField, Min(0f), Tooltip("Wrist to palm centre.")]
		private float _wristRadiusMeters = 0.02f;

		[SerializeField, Min(0f), Tooltip("The four segments fanning across the palm.")]
		private float _palmRadiusMeters = 0.015f;

		[SerializeField, Min(0f), Tooltip("The thumb's first segment, from the wrist.")]
		private float _thumbBaseRadiusMeters = 0.013f;

		[SerializeField, Min(0f), Tooltip("Each finger's first segment, and the thumb's middle one.")]
		private float _fingerBaseRadiusMeters = 0.01f;

		[SerializeField, Min(0f), Tooltip("Each finger's middle segment.")]
		private float _fingerMiddleRadiusMeters = 0.009f;

		[SerializeField, Min(0f), Tooltip("Each finger's and the thumb's last segment, up to the tip.")]
		private float _fingerTipRadiusMeters = 0.008f;

		#endregion

		#region Fields

		// The metacarpal joints sit near the wrist, so the metacarpal segments fan out across the palm
		private static readonly (XRHandJointID Start, XRHandJointID End, Thickness Thickness)[] BoneDefinitions =
		{
			(XRHandJointID.Wrist, XRHandJointID.Palm, Thickness.Wrist),

			(XRHandJointID.IndexMetacarpal, XRHandJointID.IndexProximal, Thickness.Palm),
			(XRHandJointID.MiddleMetacarpal, XRHandJointID.MiddleProximal, Thickness.Palm),
			(XRHandJointID.RingMetacarpal, XRHandJointID.RingProximal, Thickness.Palm),
			(XRHandJointID.LittleMetacarpal, XRHandJointID.LittleProximal, Thickness.Palm),

			(XRHandJointID.ThumbMetacarpal, XRHandJointID.ThumbProximal, Thickness.ThumbBase),
			(XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, Thickness.FingerBase),
			(XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip, Thickness.FingerTip),

			(XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, Thickness.FingerBase),
			(XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, Thickness.FingerMiddle),
			(XRHandJointID.IndexDistal, XRHandJointID.IndexTip, Thickness.FingerTip),

			(XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, Thickness.FingerBase),
			(XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, Thickness.FingerMiddle),
			(XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip, Thickness.FingerTip),

			(XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, Thickness.FingerBase),
			(XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, Thickness.FingerMiddle),
			(XRHandJointID.RingDistal, XRHandJointID.RingTip, Thickness.FingerTip),

			(XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, Thickness.FingerBase),
			(XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, Thickness.FingerMiddle),
			(XRHandJointID.LittleDistal, XRHandJointID.LittleTip, Thickness.FingerTip),
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

			foreach ((XRHandJointID start, XRHandJointID end, Thickness thickness) in BoneDefinitions)
			{
				if (hand.GetJoint(start).TryGetPose(out Pose startPose)
					&& hand.GetJoint(end).TryGetPose(out Pose endPose))
				{
					_bones.Add(new ParkHandBone(ToWorld(startPose.position), ToWorld(endPose.position),
						GetRadiusMeters(thickness)));
				}
			}
		}

		private float GetRadiusMeters(Thickness thickness)
		{
			switch (thickness)
			{
				case Thickness.Wrist: return _wristRadiusMeters;
				case Thickness.Palm: return _palmRadiusMeters;
				case Thickness.ThumbBase: return _thumbBaseRadiusMeters;
				case Thickness.FingerBase: return _fingerBaseRadiusMeters;
				case Thickness.FingerMiddle: return _fingerMiddleRadiusMeters;
				default: return _fingerTipRadiusMeters;
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
