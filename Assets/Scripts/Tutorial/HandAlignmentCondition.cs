using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Interaction;
using UnityEngine;

namespace DroneDDW.Tutorial
{
	// Shows ghost hands in front of the player and completes once the player's hands have lined up with them.
	// Each ghost locks in on its own, so the hands don't have to be held in place at the same time.
	internal class HandAlignmentCondition : StepCondition
	{
		#region Serialized fields

		[SerializeField] private HandTrackingMonitor _handTrackingMonitor;
		[SerializeField] private Transform _head;

		[Tooltip("Moved in front of the player when the step starts")]
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

		[Tooltip("The ghost hands' own hint target, its stuck hints bring them back in view")]
		[SerializeField] private HintTarget _hintTarget;

		[Tooltip("A stuck hint moves the ghost hands back in front of the player once they're this far out of view")]
		[Range(0f, 180f)]
		[SerializeField] private float _maxViewAngleDegrees = 45f;

		#endregion

		#region Event handlers

		private void HandleHintBegin()
		{
			Vector3 forward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
			Vector3 toGhostHands = Vector3.ProjectOnPlane(_ghostHandsRoot.position - _head.position, Vector3.up);

			if (Vector3.Angle(forward, toGhostHands) > _maxViewAngleDegrees)
				PlaceInFrontOfHead();
		}

		#endregion

		#region Internal

		internal override async UniTask<int> WaitAsync(CancellationToken cancellationToken)
		{
			using CancellationTokenSource linkedSource =
				CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);

			// Per ghost hand, from 0 to 1 when it locks in
			float[] progress = new float[_ghostHands.Length];

			PlaceInFrontOfHead();
			SetGhostHandsVisible(true);
			_hintTarget.OnHintBegin += HandleHintBegin;

			try
			{
				// Lining up is continuous, so it's checked every frame rather than on events
				while (!UpdateGhostHands(progress))
					await UniTask.NextFrame(linkedSource.Token);
			}
			finally
			{
				_hintTarget.OnHintBegin -= HandleHintBegin;
				SetGhostHandsVisible(false);
			}

			return 0;
		}

		#endregion

		#region Private

		private void PlaceInFrontOfHead()
		{
			Vector3 forward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);

			// Looking straight down leaves no horizontal forward, the top of the head points the right way then
			if (forward.sqrMagnitude < 0.01f)
				forward = Vector3.ProjectOnPlane(_head.up, Vector3.up);

			forward.Normalize();
			Vector3 position = _head.position
				+ forward * _forwardDistanceMeters
				+ Vector3.down * _heightBelowHeadMeters;

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

		// Returns true once enough ghost hands have locked in
		private bool UpdateGhostHands(float[] progress)
		{
			int lockedCount = 0;

			for (int index = 0; index < _ghostHands.Length; index++)
			{
				GhostHand ghostHand = _ghostHands[index];

				if (progress[index] < 1f)
				{
					progress[index] = GetNextProgress(progress[index], IsAligned(ghostHand));

					if (progress[index] >= 1f)
						_audioSource.PlayOneShot(_lockInClip);
				}

				ghostHand.SetProgress(progress[index]);

				if (progress[index] >= 1f)
					lockedCount++;
			}

			return _requiresAllGhostHands ? lockedCount == _ghostHands.Length : lockedCount > 0;
		}

		private float GetNextProgress(float progress, bool isAligned)
		{
			if (!isAligned)
				return 0f;

			if (_holdSeconds <= 0f)
				return 1f;

			return Mathf.Min(progress + Time.deltaTime / _holdSeconds, 1f);
		}

		private bool IsAligned(GhostHand ghostHand)
		{
			if (!_handTrackingMonitor.TryGetPalmPose(ghostHand.Handedness, out Pose palmPose))
				return false;

			// Both skeletons share the XR Hands joint axes, so the palms' up vectors are directly comparable
			Pose ghostPalmPose = ghostHand.PalmPose;
			float distanceMeters = Vector3.Distance(palmPose.position, ghostPalmPose.position);
			bool isCloseEnough = distanceMeters <= _positionToleranceMeters;
			bool isFacingSameWay = Vector3.Angle(palmPose.up, ghostPalmPose.up) <= _angleToleranceDegrees;
			return isCloseEnough && isFacingSameWay;
		}

		#endregion
	}
}
