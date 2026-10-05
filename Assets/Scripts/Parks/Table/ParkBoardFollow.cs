using UnityEngine;

namespace DroneDDW.Parks.Table
{
	/// <summary>
	/// Keeps the floating board in front of and below the head, whether the viewer sits, stands or turns around. It
	/// follows in two parts, each at its own speed: it swings round the head with a turn, and moves along with the head
	/// (standing up, sitting down, leaning); either starts only once the head has turned or moved past the dead zone. It
	/// turns only around the vertical axis, so it stays level, and holds still only while the slider is being pushed or
	/// grabbed, so the knob never slides away under the hand. Where it sits, the dead zone and both speeds come from the
	/// shared follow settings, read every frame, so tuning them applies at once.
	/// Put it on the board's root, whose origin is the middle of the board's top.
	/// </summary>
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(-20)] // Moves the board before the level label and the miniature place themselves on it
	[AddComponentMenu("DroneDDW/Parks/Park Board Follow")]
	public sealed class ParkBoardFollow : MonoBehaviour
	{
		#region Entities

		internal enum FollowState
		{
			/// <summary>Waiting for the headset's first pose.</summary>
			NotPlaced,

			/// <summary>The head is inside the dead zone.</summary>
			Waiting,

			Following,

			/// <summary>Holding still while the slider is in use.</summary>
			Frozen,
		}

		#endregion

		#region Serialized fields

		[SerializeField, Tooltip("The XR camera. Falls back to Camera.main when left empty.")]
		private Transform _head;

		[SerializeField, Tooltip("The board's slider. The board holds still while it is pushed or grabbed.")]
		private ParkLevelSlider _slider;

		[SerializeField, Tooltip("The shared follow settings: position, dead zone and speeds. Uses the defaults when "
			+ "empty.")]
		private ParkBoardFollowSettings _settings;

		#endregion

		#region Fields

		private const float SettleAngleDegrees = 0.5f;
		private const float SettleDistanceMeters = 0.005f;

		// Below this the head looks almost straight up or down and its yaw is unreliable
		private const float MinFlatForwardLength = 0.2f;

		private Transform _transform;
		private FollowState _state;
		private bool _isMoving;
		private Vector3 _anchorPosition;
		private Vector3 _anchorVelocity;
		private float _yawDegrees;
		private float _yawVelocity;
		private float _turnOffsetDegrees;
		private float _moveOffsetMeters;

		#endregion

		#region Properties

		internal FollowState State => _state;

		/// <summary>How far the head is turned from the board's yaw.</summary>
		internal float TurnOffsetDegrees => _turnOffsetDegrees;

		/// <summary>How far the head is from where the board last followed it to.</summary>
		internal float MoveOffsetMeters => _moveOffsetMeters;

		internal ParkBoardFollowSettings Settings => ParkBoardFollowSettings.OrDefaults(_settings);

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

			ParkBoardFollowSettings settings = Settings;
			float headYawDegrees = GetHeadYawDegrees(head);
			if (_state == FollowState.NotPlaced)
			{
				_anchorPosition = head.position;
				_yawDegrees = headYawDegrees;
				_state = FollowState.Waiting;
			}
			else
			{
				Follow(settings, head.position, headYawDegrees);
			}

			Quaternion rotation = Quaternion.Euler(0f, _yawDegrees, 0f);
			_transform.SetPositionAndRotation(_anchorPosition + rotation * settings.OffsetMeters, rotation);
		}

		#endregion

		#region Internal

		internal void Configure(Transform head, ParkLevelSlider slider, ParkBoardFollowSettings settings)
		{
			_head = head;
			_slider = slider;
			_settings = settings;
		}

		#endregion

		#region Private

		/// <summary>
		/// Moves the anchor (the head position the board hangs off) and the yaw towards the head once it has left the dead
		/// zone, each at its own speed, until both settle. Holds them while the slider is in use.
		/// </summary>
		private void Follow(ParkBoardFollowSettings settings, Vector3 headPosition, float headYawDegrees)
		{
			_turnOffsetDegrees = Mathf.Abs(Mathf.DeltaAngle(_yawDegrees, headYawDegrees));
			_moveOffsetMeters = Vector3.Distance(_anchorPosition, headPosition);
			if (IsSliderInUse())
			{
				_state = FollowState.Frozen;
				StopMoving();
				return;
			}

			if (!_isMoving)
			{
				if (_turnOffsetDegrees < settings.StartAngleDegrees && _moveOffsetMeters < settings.StartDistanceMeters)
				{
					_state = FollowState.Waiting;
					return;
				}

				_isMoving = true;
			}

			_state = FollowState.Following;
			_yawDegrees = Mathf.SmoothDampAngle(_yawDegrees, headYawDegrees, ref _yawVelocity, settings.TurnTimeSeconds);
			_anchorPosition = Vector3.SmoothDamp(_anchorPosition, headPosition, ref _anchorVelocity,
				settings.MoveTimeSeconds);
			if (_turnOffsetDegrees < SettleAngleDegrees && _moveOffsetMeters < SettleDistanceMeters)
			{
				StopMoving();
			}
		}

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
			_anchorVelocity = Vector3.zero;
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
