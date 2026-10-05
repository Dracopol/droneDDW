using UnityEngine;

namespace DroneDDW.Parks.Table
{
	/// <summary>
	/// Keeps the floating board in front of and below the head, whether the viewer sits, stands or turns around. It
	/// turns only around the vertical axis, so it stays level, and it waits until the head has turned or moved far
	/// enough before gliding into place. It holds still only while the slider is being pushed or grabbed, so the knob
	/// never slides away under the hand. Otherwise the same rules as the Interaction Tests' head follow.
	/// Put it on the board's root, whose origin is the middle of the board's top.
	/// </summary>
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(-20)] // Moves the board before the level label and the miniature place themselves on it
	[AddComponentMenu("DroneDDW/Parks/Park Board Follow")]
	public sealed class ParkBoardFollow : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField, Tooltip("The XR camera. Falls back to Camera.main when left empty.")]
		private Transform _head;

		[SerializeField, Tooltip("The board's slider. The board holds still while it is pushed or grabbed.")]
		private ParkLevelSlider _slider;

		[SerializeField, Tooltip("Where the board's top sits relative to the eyes, turned with the head's yaw.")]
		private Vector3 _offsetMeters = new Vector3(0f, -0.48f, 0.75f);

		[Header("Dead zone")]
		[SerializeField, Range(0f, 180f), Tooltip("How far the head turns before the board follows.")]
		private float _startAngleDegrees = 20f;

		[SerializeField, Min(0f), Tooltip("How far the head moves before the board follows. Large on purpose, so "
			+ "leaning forward doesn't push the board away; standing up or sitting down does move it.")]
		private float _startDistanceMeters = 0.25f;

		[Header("Movement")]
		[SerializeField, Min(0.01f), Tooltip("Roughly how long the board takes to glide into place.")]
		private float _smoothTimeSeconds = 0.3f;

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
			Transform head = ResolveHead();

			// The camera sits exactly on its parent's origin until the headset reports its first pose
			if (head == null || head.localPosition == Vector3.zero)
			{
				return;
			}

			float headYawDegrees = GetHeadYawDegrees(head);
			Vector3 targetPosition = head.position + Quaternion.Euler(0f, headYawDegrees, 0f) * _offsetMeters;
			if (!_isPlaced)
			{
				_isPlaced = true;
				_yawDegrees = headYawDegrees;
				_transform.SetPositionAndRotation(targetPosition, Quaternion.Euler(0f, _yawDegrees, 0f));
				return;
			}

			if (IsSliderInUse())
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
			Vector3 position = Vector3.SmoothDamp(_transform.position, targetPosition, ref _positionVelocity,
				_smoothTimeSeconds);
			_transform.SetPositionAndRotation(position, Quaternion.Euler(0f, _yawDegrees, 0f));
			if (angleDegrees < SettleAngleDegrees && distanceMeters < SettleDistanceMeters)
			{
				StopMoving();
			}
		}

		#endregion

		#region Internal

		internal void Configure(Transform head, ParkLevelSlider slider, Vector3 offsetMeters)
		{
			_head = head;
			_slider = slider;
			_offsetMeters = offsetMeters;
		}

		#endregion

		#region Private

		/// <summary>Keeps the last yaw while looking almost straight down, so the board doesn't spin under the gaze.</summary>
		private float GetHeadYawDegrees(Transform head)
		{
			Vector3 flatForward = head.forward;
			flatForward.y = 0f;
			if (flatForward.magnitude < MinFlatForwardLength)
			{
				return _yawDegrees;
			}

			return Quaternion.LookRotation(flatForward).eulerAngles.y;
		}

		/// <summary>
		/// Pushed, grabbed, or still in the drag's short release delay. A hand merely near the knob doesn't count.
		/// </summary>
		private bool IsSliderInUse()
		{
			return _slider != null && (_slider.IsDragging || _slider.Contact == ParkSliderContact.Pushed
				|| _slider.Contact == ParkSliderContact.Grabbed);
		}

		private void StopMoving()
		{
			_isMoving = false;
			_yawVelocity = 0f;
			_positionVelocity = Vector3.zero;
		}

		private Transform ResolveHead()
		{
			if (_head != null)
			{
				return _head;
			}

			Camera main = Camera.main;
			return main != null ? main.transform : null;
		}

		#endregion
	}
}
