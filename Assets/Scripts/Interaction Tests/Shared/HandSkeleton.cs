using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace DroneDDW.InteractionTests
{
	// A tracked hand as thick bone segments, read from the joint transforms of the rig's hand visual
	internal class HandSkeleton : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("Skeleton driver of the hand visual, its joint transforms follow the tracked hand")]
		[SerializeField] private XRHandSkeletonDriver _skeletonDriver;

		[SerializeField] private XRHandTrackingEvents _trackingEvents;

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

		private readonly List<HandBone> _bones = new();

		#endregion

		#region Properties

		// The skeleton keeps its last pose when tracking drops, so check this before trusting the bones
		internal bool IsTracked => _trackingEvents.handIsTracked;

		internal IReadOnlyList<HandBone> Bones => _bones;

		#endregion

		#region Setup

		private void Awake()
		{
			Dictionary<XRHandJointID, Transform> joints = new();

			foreach (JointToTransformReference reference in _skeletonDriver.jointTransformReferences)
			{
				if (reference.jointTransform != null)
				{
					joints[reference.xrHandJointID] = reference.jointTransform;
				}
			}

			foreach ((XRHandJointID start, XRHandJointID end, float radiusMeters) in BoneDefinitions)
			{
				bool hasStart = joints.TryGetValue(start, out Transform startJoint);
				bool hasEnd = joints.TryGetValue(end, out Transform endJoint);

				if (!hasStart || !hasEnd)
				{
					Debug.LogWarning($"[DDW Buttons] {name} has no {start} or {end} joint, skipping that bone", this);
					continue;
				}

				_bones.Add(new HandBone(startJoint, endJoint, radiusMeters));
			}
		}

		#endregion
	}
}
