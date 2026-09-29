using UnityEngine;

namespace DroneDDW.Buttons
{
	// Keeps the panel in front of and below the head, turning only around the vertical axis so it stays level.
	// It waits until the head has turned or moved far enough, and holds still while hands are near the buttons.
	internal class HeadFollow : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private Transform _head;
		[SerializeField] private HandSkeleton[] _hands;

		[Tooltip("Where the panel sits relative to the head, turned with the head's yaw")]
		[SerializeField] private Vector3 _offsetMeters = new(0f, -0.5f, 0f);

		[Header("Dead zone")]
		[Range(0f, 180f)]
		[SerializeField] private float _startAngleDegrees = 20f;

		[Tooltip("Large on purpose, so leaning forward in the chair doesn't push the panel away")]
		[Min(0f)]
		[SerializeField] private float _startDistanceMeters = 0.25f;

		[Header("Movement")]
		[Tooltip("Roughly how long the panel takes to glide into place")]
		[Min(0.01f)]
		[SerializeField] private float _smoothTimeSeconds = 0.3f;

		[Header("Pause near hands")]
		[Tooltip("In this transform's space. A tracked hand inside it stops the panel from moving.")]
		[SerializeField] private Bounds _handPauseBounds = new(new Vector3(0f, 0.1f, 0.4f), new Vector3(1.1f, 0.3f, 0.65f));

		[Tooltip("How long the hands have to stay away before the panel may move again")]
		[Min(0f)]
		[SerializeField] private float _resumeDelaySeconds = 1f;

		#endregion

		#region Fields

		private const float SettleAngleDegrees = 0.5f;
		private const float SettleDistanceMeters = 0.005f;

		// Below this the head looks almost straight up or down and its yaw is unreliable
		private const float MinFlatForwardLength = 0.2f;

		private Transform _transform;
		private bool _isPlaced;
		private bool _isMoving;
		private float _yawDegrees;
		private float _yawVelocity;
		private Vector3 _positionVelocity;
		private float _lastHandNearTime = float.NegativeInfinity;

		#endregion

		#region Setup

		private void Awake()
		{
			_transform = transform;
		}

		#endregion

		#region Update

		private void LateUpdate()
		{
			// The camera sits exactly on its parent's origin until the headset reports its first pose
			if (_head.localPosition == Vector3.zero)
			{
				return;
			}

			float headYawDegrees = GetHeadYawDegrees();
			Vector3 targetPosition = _head.position + Quaternion.Euler(0f, headYawDegrees, 0f) * _offsetMeters;

			if (!_isPlaced)
			{
				_isPlaced = true;
				_yawDegrees = headYawDegrees;
				_transform.SetPositionAndRotation(targetPosition, Quaternion.Euler(0f, _yawDegrees, 0f));
				return;
			}

			if (AreHandsNear())
			{
				_lastHandNearTime = Time.time;
			}

			if (Time.time - _lastHandNearTime < _resumeDelaySeconds)
			{
				StopMoving();
				return;
			}

			float angleDegrees = Mathf.Abs(Mathf.DeltaAngle(_yawDegrees, headYawDegrees));
			float distanceMeters = Vector3.Distance(_transform.position, targetPosition);

			if (!_isMoving)
			{
				if (angleDegrees < _startAngleDegrees && distanceMeters < _startDistanceMeters)
				{
					return;
				}

				_isMoving = true;
			}

			_yawDegrees = Mathf.SmoothDampAngle(_yawDegrees, headYawDegrees, ref _yawVelocity, _smoothTimeSeconds);
			Vector3 position = Vector3.SmoothDamp(
				_transform.position,
				targetPosition,
				ref _positionVelocity,
				_smoothTimeSeconds);
			_transform.SetPositionAndRotation(position, Quaternion.Euler(0f, _yawDegrees, 0f));

			if (angleDegrees < SettleAngleDegrees && distanceMeters < SettleDistanceMeters)
			{
				StopMoving();
			}
		}

		private void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.cyan;
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.DrawWireCube(_handPauseBounds.center, _handPauseBounds.size);
		}

		#endregion

		#region Private

		// Keeps the last yaw while looking almost straight down, so the panel doesn't spin under the player's gaze
		private float GetHeadYawDegrees()
		{
			Vector3 flatForward = _head.forward;
			flatForward.y = 0f;

			if (flatForward.magnitude < MinFlatForwardLength)
			{
				return _yawDegrees;
			}

			return Quaternion.LookRotation(flatForward).eulerAngles.y;
		}

		private bool AreHandsNear()
		{
			foreach (HandSkeleton hand in _hands)
			{
				if (!hand.IsTracked)
				{
					continue;
				}

				foreach (HandBone bone in hand.Bones)
				{
					if (IsInsidePauseBounds(bone.Start.position) || IsInsidePauseBounds(bone.End.position))
					{
						return true;
					}
				}
			}

			return false;
		}

		private bool IsInsidePauseBounds(Vector3 worldPosition)
		{
			return _handPauseBounds.Contains(_transform.InverseTransformPoint(worldPosition));
		}

		private void StopMoving()
		{
			_isMoving = false;
			_yawVelocity = 0f;
			_positionVelocity = Vector3.zero;
		}

		#endregion
	}
}
