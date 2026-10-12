using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace DroneDDW.Experience
{
	// The tracked hands, read straight from the hand subsystem so they don't depend on which hand visuals the rig
	// shows: whether a hand is tracked and where a joint is in the world. Joints are relative to the rig's tracking
	// space (the XR Origin's camera offset).
	internal static class HandJointsUtil
	{
		#region Fields

		private static readonly List<XRHandSubsystem> Subsystems = new();

		private static XRHandSubsystem Subsystem;

		#endregion

		#region Internal

		internal static bool IsTracked(Handedness handedness)
		{
			return TryGetHand(handedness, out XRHand hand) && hand.isTracked;
		}

		// Fails while the hand isn't tracked
		internal static bool TryGetJointPose(Handedness handedness, XRHandJointID joint, Transform trackingSpace,
			out Pose pose)
		{
			pose = default;
			if (!TryGetHand(handedness, out XRHand hand) || !hand.isTracked
				|| !hand.GetJoint(joint).TryGetPose(out Pose trackingPose))
			{
				return false;
			}

			pose = trackingSpace != null
				? new Pose(trackingSpace.TransformPoint(trackingPose.position), trackingSpace.rotation * trackingPose.rotation)
				: trackingPose;
			return true;
		}

		#endregion

		#region Private

		private static bool TryGetHand(Handedness handedness, out XRHand hand)
		{
			hand = default;
			if (!TryGetSubsystem(out XRHandSubsystem subsystem))
			{
				return false;
			}

			hand = handedness == Handedness.Left ? subsystem.leftHand : subsystem.rightHand;
			return true;
		}

		private static bool TryGetSubsystem(out XRHandSubsystem subsystem)
		{
			if (Subsystem == null || !Subsystem.running)
			{
				Subsystem = null;
				SubsystemManager.GetSubsystems(Subsystems);
				foreach (XRHandSubsystem candidate in Subsystems)
				{
					if (candidate.running)
					{
						Subsystem = candidate;
						break;
					}
				}
			}

			subsystem = Subsystem;
			return subsystem != null;
		}

		#endregion
	}
}
