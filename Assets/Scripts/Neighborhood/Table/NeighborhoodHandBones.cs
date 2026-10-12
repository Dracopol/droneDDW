using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace DroneDDW.Neighborhood.Table
{
	/// <summary>
	/// Both tracked hands as thick bone segments in world space, read straight from the hand subsystem, so they don't
	/// depend on which hand visuals the rig has switched on. Same bones as the Push Slider Test's hand skeleton; their
	/// thicknesses come from the shared push settings, read on every refresh, so tuning them applies at once. A hand that
	/// isn't tracked has no bones, so a lost hand can't keep pushing.
	/// Refreshed by whoever reads it (<see cref="NeighborhoodSliderHandDriver"/>), right before use.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Hand Bones")]
	public sealed class NeighborhoodHandBones : MonoBehaviour
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

		[SerializeField, Tooltip("The shared push settings, which hold the bone radii. Uses the defaults when empty.")]
		private NeighborhoodPushSettings _settings;

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

		private readonly List<NeighborhoodHandBone> _bones = new List<NeighborhoodHandBone>();
		private XRHandSubsystem _subsystem;

		#endregion

		#region Properties

		/// <summary>The bones of every tracked hand, as of the last <see cref="Refresh"/>.</summary>
		internal IReadOnlyList<NeighborhoodHandBone> Bones => _bones;

		#endregion

		#region Internal

		internal void Refresh()
		{
			_bones.Clear();
			if (!TryGetSubsystem(out XRHandSubsystem subsystem))
			{
				return;
			}

			NeighborhoodPushSettings settings = NeighborhoodPushSettings.OrDefaults(_settings);
			AddBones(subsystem.leftHand, settings);
			AddBones(subsystem.rightHand, settings);
		}

		internal void Configure(Transform trackingSpace, NeighborhoodPushSettings settings)
		{
			_trackingSpace = trackingSpace;
			_settings = settings;
		}

		/// <summary>Reads the hands relative to another rig, e.g. the experience's once it has loaded this scene.</summary>
		internal void SetTrackingSpace(Transform trackingSpace)
		{
			_trackingSpace = trackingSpace;
		}

		#endregion

		#region Private

		private void AddBones(XRHand hand, NeighborhoodPushSettings settings)
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
					_bones.Add(new NeighborhoodHandBone(ToWorld(startPose.position), ToWorld(endPose.position),
						GetRadiusMeters(thickness, settings)));
				}
			}
		}

		private static float GetRadiusMeters(Thickness thickness, NeighborhoodPushSettings settings)
		{
			switch (thickness)
			{
				case Thickness.Wrist: return settings.WristRadiusMeters;
				case Thickness.Palm: return settings.PalmRadiusMeters;
				case Thickness.ThumbBase: return settings.ThumbBaseRadiusMeters;
				case Thickness.FingerBase: return settings.FingerBaseRadiusMeters;
				case Thickness.FingerMiddle: return settings.FingerMiddleRadiusMeters;
				default: return settings.FingerTipRadiusMeters;
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
