using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace DroneDDW.Experience
{
	// The hand check, ported from the Questionnaire tutorial: ghost hands appear in front of the participant and the
	// step completes once their own hands have lined up with them. Each ghost locks in on its own (with a sound), so the
	// hands don't have to be held in place at the same time. The participant's palms are read from the hand subsystem.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/Hand Alignment Condition")]
	internal sealed class HandAlignmentCondition : ExperienceCondition
	{
		#region Serialized fields

		[Tooltip("The headset camera")]
		[SerializeField] private Transform _head;

		[Tooltip("The XR Origin's camera offset: the hand joints are relative to it")]
		[SerializeField] private Transform _trackingSpace;

		[Tooltip("Moved in front of the participant when the step starts")]
		[SerializeField] private Transform _ghostHandsRoot;

		[SerializeField] private GhostHand[] _ghostHands;

		[Tooltip("Off: one locked in ghost hand is enough")]
		[SerializeField] private bool _requiresAllGhostHands = true;

		[Header("Placement")]
		[Min(0f)]
		[SerializeField] private float _forwardDistanceMeters = 0.4f;

		[SerializeField] private float _heightBelowHeadMeters = 0.2f;

		[Header("Alignment")]
		[Min(0f)]
		[SerializeField] private float _positionToleranceMeters = 0.1f;

		[Range(0f, 180f)]
		[SerializeField] private float _angleToleranceDegrees = 50f;

		[Tooltip("How long a hand has to stay lined up before its ghost locks in")]
		[Min(0f)]
		[SerializeField] private float _holdSeconds = 0.5f;

		[Header("Feedback")]
		[SerializeField] private AudioSource _audioSource;
		[SerializeField] private AudioClip _lockInClip;

		[Tooltip("A stuck line brings the ghost hands back in front of the participant once they're this far out of view")]
		[Range(0f, 180f)]
		[SerializeField] private float _maxViewAngleDegrees = 45f;

		#endregion

		#region Internal

		internal void Configure(Transform head, Transform trackingSpace, Transform ghostHandsRoot, GhostHand[] ghostHands,
			AudioSource audioSource, AudioClip lockInClip)
		{
			_head = head;
			_trackingSpace = trackingSpace;
			_ghostHandsRoot = ghostHandsRoot;
			_ghostHands = ghostHands;
			_audioSource = audioSource;
			_lockInClip = lockInClip;
		}

		internal override async UniTask WaitAsync(CancellationToken cancellationToken)
		{
			HoldTimer[] holds = new HoldTimer[_ghostHands.Length];
			for (int index = 0; index < holds.Length; index++)
			{
				holds[index] = new HoldTimer(_holdSeconds);
			}

			PlaceInFrontOfHead();
			SetGhostHandsVisible(true);
			try
			{
				// Lining up is continuous, so it's checked every frame rather than on events
				while (!UpdateGhostHands(holds))
				{
					await UniTask.NextFrame(cancellationToken);
				}
			}
			finally
			{
				SetGhostHandsVisible(false);
			}
		}

		// The ghost hands come back in front of the participant if they've turned away from them
		internal override void ShowHint()
		{
			Vector3 forward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
			Vector3 toGhostHands = Vector3.ProjectOnPlane(_ghostHandsRoot.position - _head.position, Vector3.up);
			if (Vector3.Angle(forward, toGhostHands) > _maxViewAngleDegrees)
			{
				PlaceInFrontOfHead();
			}
		}

		#endregion

		#region Private

		private void PlaceInFrontOfHead()
		{
			Vector3 forward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);

			// Looking straight down leaves no horizontal forward, the top of the head points the right way then
			if (forward.sqrMagnitude < 0.01f)
			{
				forward = Vector3.ProjectOnPlane(_head.up, Vector3.up);
			}

			forward.Normalize();
			Vector3 position = _head.position + forward * _forwardDistanceMeters + Vector3.down * _heightBelowHeadMeters;
			_ghostHandsRoot.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
		}

		private void SetGhostHandsVisible(bool isVisible)
		{
			foreach (GhostHand ghostHand in _ghostHands)
			{
				ghostHand.SetProgress(0f);
				ghostHand.SetVisible(isVisible);
			}
		}

		// True once enough ghost hands have locked in
		private bool UpdateGhostHands(HoldTimer[] holds)
		{
			int lockedCount = 0;
			for (int index = 0; index < _ghostHands.Length; index++)
			{
				GhostHand ghostHand = _ghostHands[index];
				HoldTimer hold = holds[index];
				bool wasComplete = hold.IsComplete;
				if (hold.Update(IsAligned(ghostHand), Time.deltaTime))
				{
					lockedCount++;
					if (!wasComplete && _audioSource != null)
					{
						_audioSource.PlayOneShot(_lockInClip);
					}
				}

				ghostHand.SetProgress(hold.Progress);
			}

			return _requiresAllGhostHands ? lockedCount == _ghostHands.Length : lockedCount > 0;
		}

		private bool IsAligned(GhostHand ghostHand)
		{
			if (!HandJointsUtil.TryGetJointPose(ghostHand.Handedness, XRHandJointID.Palm, _trackingSpace,
				out Pose palmPose))
			{
				return false;
			}

			// The ghost models are XR Hands' own, whose bones share the joints' axes, so the up vectors compare directly
			Pose ghostPalmPose = ghostHand.PalmPose;
			bool isCloseEnough = Vector3.Distance(palmPose.position, ghostPalmPose.position) <= _positionToleranceMeters;
			bool isFacingSameWay = Vector3.Angle(palmPose.up, ghostPalmPose.up) <= _angleToleranceDegrees;
			return isCloseEnough && isFacingSameWay;
		}

		#endregion
	}
}
