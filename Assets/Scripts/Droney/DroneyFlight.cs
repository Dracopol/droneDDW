using UnityEngine;

namespace DroneDDW.Droney
{
	// Flies Droney to her home spot near the player, or over to whatever she's pointing out, always facing the player.
	// She never enters the player's reach or the space above visible papers, and leans into her movement like a drone.
	internal class DroneyFlight : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private Transform _head;

		[Header("Home spot")]
		[Tooltip("Degrees from where the player faces, negative is to their left")]
		[Range(-180f, 180f)]
		[SerializeField] private float _homeAngleDegrees = -35f;

		[Min(0f)]
		[SerializeField] private float _homeDistanceMeters = 1f;
		[SerializeField] private float _homeHeightBelowHeadMeters = 0.15f;

		[Tooltip("How far the player can turn before the home spot follows")]
		[Range(0f, 90f)]
		[SerializeField] private float _homeFollowDeadZoneDegrees = 30f;

		[Header("Pointing things out")]
		[Tooltip("How high above a pointed out object she aims to hover")]
		[Min(0f)]
		[SerializeField] private float _pointingHeightMeters = 0.25f;

		[Header("Keeping clear")]
		[Tooltip("Horizontal distance from the player's head she never comes closer than")]
		[Min(0f)]
		[SerializeField] private float _reachRadiusMeters = 0.75f;

		[Tooltip("She stays out of the space above these while they're visible, e.g. the papers")]
		[SerializeField] private Collider[] _avoidedSurfaces;

		[Min(0f)]
		[SerializeField] private float _surfaceClearanceMeters = 0.1f;

		[Tooltip("How high above an avoided surface the kept clear space reaches")]
		[Min(0f)]
		[SerializeField] private float _surfaceColumnHeightMeters = 0.8f;

		[Header("Motion")]
		[Min(0.01f)]
		[SerializeField] private float _smoothTimeSeconds = 0.6f;

		[Min(0f)]
		[SerializeField] private float _maxSpeedMetersPerSecond = 0.8f;

		[Tooltip("Kept low, vertical motion is the least comfortable to watch")]
		[Min(0f)]
		[SerializeField] private float _maxVerticalSpeedMetersPerSecond = 0.3f;

		[Min(0.01f)]
		[SerializeField] private float _turnSmoothTimeSeconds = 0.35f;

		[Header("Lean")]
		[Range(0f, 45f)]
		[SerializeField] private float _maxLeanDegrees = 15f;
		[SerializeField] private float _leanDegreesPerAcceleration = 10f;

		[Min(0.01f)]
		[SerializeField] private float _leanSmoothTimeSeconds = 0.15f;

		#endregion

		#region Fields

		private const int ConstraintPasses = 3;

		private Vector3? _pointingPosition;
		private float _homeYawDegrees;
		private Vector3 _horizontalVelocity;
		private float _verticalVelocity;
		private Vector3 _previousVelocity;
		private float _yawDegrees;
		private float _yawVelocity;
		private float _pitchDegrees;
		private float _pitchVelocity;
		private float _rollDegrees;
		private float _rollVelocity;

		#endregion

		#region Properties

		internal float SpeedMetersPerSecond { get; private set; }

		#endregion

		#region Setup

		private void Start()
		{
			_homeYawDegrees = GetHeadYawDegrees();
			_yawDegrees = transform.eulerAngles.y;
		}

		#endregion

		#region Update

		private void LateUpdate()
		{
			float deltaSeconds = Time.deltaTime;

			if (deltaSeconds <= 0f)
				return;

			UpdateHomeYaw();
			Vector3 target = Constrain(_pointingPosition ?? GetHomePosition());
			Vector3 position = transform.position;

			// Horizontal and vertical are smoothed apart so vertical speed can be capped lower
			Vector3 horizontal = Vector3.SmoothDamp(
				Flat(position),
				Flat(target),
				ref _horizontalVelocity,
				_smoothTimeSeconds,
				_maxSpeedMetersPerSecond,
				deltaSeconds);
			float height = Mathf.SmoothDamp(
				position.y,
				target.y,
				ref _verticalVelocity,
				_smoothTimeSeconds,
				_maxVerticalSpeedMetersPerSecond,
				deltaSeconds);

			// Constraining the path too, not just the target, makes her fly around kept clear spaces
			position = Constrain(new Vector3(horizontal.x, height, horizontal.z));

			Vector3 velocity = new(_horizontalVelocity.x, _verticalVelocity, _horizontalVelocity.z);
			Vector3 acceleration = (velocity - _previousVelocity) / deltaSeconds;
			_previousVelocity = velocity;
			SpeedMetersPerSecond = velocity.magnitude;

			transform.SetPositionAndRotation(position, GetRotation(position, acceleration, deltaSeconds));
		}

		#endregion

		#region Internal

		// Hovers a little above the given point, as close as the kept clear spaces allow
		internal void PointOut(Vector3 position)
		{
			_pointingPosition = position + Vector3.up * _pointingHeightMeters;
		}

		internal void ReturnHome()
		{
			_pointingPosition = null;
		}

		#endregion

		#region Private

		private void UpdateHomeYaw()
		{
			float offsetDegrees = Mathf.DeltaAngle(_homeYawDegrees, GetHeadYawDegrees());

			if (Mathf.Abs(offsetDegrees) > _homeFollowDeadZoneDegrees)
				_homeYawDegrees += offsetDegrees - Mathf.Sign(offsetDegrees) * _homeFollowDeadZoneDegrees;
		}

		private Vector3 GetHomePosition()
		{
			Quaternion direction = Quaternion.Euler(0f, _homeYawDegrees + _homeAngleDegrees, 0f);
			Vector3 home = _head.position + direction * Vector3.forward * _homeDistanceMeters;
			home.y = _head.position.y - _homeHeightBelowHeadMeters;
			return home;
		}

		private float GetHeadYawDegrees()
		{
			Vector3 forward = Flat(_head.forward);

			// Looking straight up or down leaves no horizontal forward, the top of the head points the right way then
			if (forward.sqrMagnitude < 0.01f)
				forward = Flat(_head.up);

			return Quaternion.LookRotation(forward).eulerAngles.y;
		}

		// Faces the player and leans into the acceleration, the same way the old manual flight test did
		private Quaternion GetRotation(Vector3 position, Vector3 acceleration, float deltaSeconds)
		{
			Vector3 toHead = Flat(_head.position - position);

			if (toHead.sqrMagnitude > 0.0001f)
			{
				float targetYawDegrees = Quaternion.LookRotation(toHead).eulerAngles.y;
				_yawDegrees = Mathf.SmoothDampAngle(
					_yawDegrees,
					targetYawDegrees,
					ref _yawVelocity,
					_turnSmoothTimeSeconds,
					Mathf.Infinity,
					deltaSeconds);
			}

			Quaternion heading = Quaternion.Euler(0f, _yawDegrees, 0f);
			Vector3 localAcceleration = Quaternion.Inverse(heading) * Flat(acceleration);
			float targetPitchDegrees = Mathf.Clamp(
				localAcceleration.z * _leanDegreesPerAcceleration, -_maxLeanDegrees, _maxLeanDegrees);
			float targetRollDegrees = Mathf.Clamp(
				-localAcceleration.x * _leanDegreesPerAcceleration, -_maxLeanDegrees, _maxLeanDegrees);

			_pitchDegrees = Mathf.SmoothDamp(
				_pitchDegrees,
				targetPitchDegrees,
				ref _pitchVelocity,
				_leanSmoothTimeSeconds,
				Mathf.Infinity,
				deltaSeconds);
			_rollDegrees = Mathf.SmoothDamp(
				_rollDegrees,
				targetRollDegrees,
				ref _rollVelocity,
				_leanSmoothTimeSeconds,
				Mathf.Infinity,
				deltaSeconds);

			return Quaternion.Euler(_pitchDegrees, _yawDegrees, _rollDegrees);
		}

		private Vector3 Constrain(Vector3 point)
		{
			// Pushing out of one kept clear space can push into another, a few passes settle it
			for (int pass = 0; pass < ConstraintPasses; pass++)
				point = PushOutOfReach(PushOutOfSurfaceColumns(point));

			return point;
		}

		private Vector3 PushOutOfReach(Vector3 point)
		{
			Vector3 fromHead = Flat(point - _head.position);
			float distanceMeters = fromHead.magnitude;

			if (distanceMeters >= _reachRadiusMeters)
				return point;

			Vector3 direction = distanceMeters > 0.001f ? fromHead / distanceMeters : Flat(_head.forward).normalized;
			Vector3 pushed = _head.position + direction * _reachRadiusMeters;
			return new Vector3(pushed.x, point.y, pushed.z);
		}

		// Out of the space above each visible avoided surface, through whichever side is nearest
		private Vector3 PushOutOfSurfaceColumns(Vector3 point)
		{
			foreach (Collider surface in _avoidedSurfaces)
			{
				if (!surface.gameObject.activeInHierarchy)
					continue;

				Bounds bounds = surface.bounds;
				Vector3 minimum = bounds.min - new Vector3(_surfaceClearanceMeters, 0f, _surfaceClearanceMeters);
				Vector3 maximum = bounds.max + new Vector3(_surfaceClearanceMeters, 0f, _surfaceClearanceMeters);
				float columnTop = bounds.max.y + _surfaceColumnHeightMeters;

				bool isInside = point.x > minimum.x && point.x < maximum.x
					&& point.z > minimum.z && point.z < maximum.z
					&& point.y > minimum.y && point.y < columnTop;

				if (!isInside)
					continue;

				float toMinimumX = point.x - minimum.x;
				float toMaximumX = maximum.x - point.x;
				float toMinimumZ = point.z - minimum.z;
				float toMaximumZ = maximum.z - point.z;
				float toTop = columnTop - point.y;
				float nearest = Mathf.Min(toMinimumX, toMaximumX, toMinimumZ, toMaximumZ, toTop);

				if (nearest == toMinimumX)
					point.x = minimum.x;
				else if (nearest == toMaximumX)
					point.x = maximum.x;
				else if (nearest == toMinimumZ)
					point.z = minimum.z;
				else if (nearest == toMaximumZ)
					point.z = maximum.z;
				else
					point.y = columnTop;
			}

			return point;
		}

		private static Vector3 Flat(Vector3 vector)
		{
			return new Vector3(vector.x, 0f, vector.z);
		}

		#endregion
	}
}
