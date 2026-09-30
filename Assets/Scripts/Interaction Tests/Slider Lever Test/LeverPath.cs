using UnityEngine;

namespace DroneDDW.StepControls
{
	// An arm that tips around this transform's right axis, from the minimum angle (towards the player) to the maximum.
	// Its handle is a ball at the end of a thin stick, both can be pushed.
	internal class LeverPath : KnobPath
	{
		#region Serialized fields

		[Tooltip("Tips around its own pivot, which sits on the lever's pivot axis. Upright is 0 degrees.")]
		[SerializeField] private Transform _arm;

		[Range(-90f, 0f)]
		[SerializeField] private float _minAngleDegrees = -35f;

		[Range(0f, 90f)]
		[SerializeField] private float _maxAngleDegrees = 35f;

		[Tooltip("From the pivot to the centre of the handle")]
		[Min(0.01f)]
		[SerializeField] private float _armLengthMeters = 0.18f;

		[Min(0f)]
		[SerializeField] private float _handleRadiusMeters = 0.03f;

		[Min(0f)]
		[SerializeField] private float _stickRadiusMeters = 0.01f;

		#endregion

		#region Fields

		// Closer to the pivot than this, a hand can't sensibly tip the arm
		private const float MinContactRadiusMeters = 0.04f;

		private Transform _transform;
		private Vector3 _pivotLocal;

		#endregion

		#region Properties

		private float RangeDegrees => _maxAngleDegrees - _minAngleDegrees;

		#endregion

		#region Setup

		private void Awake()
		{
			_transform = transform;
			_pivotLocal = _arm.localPosition;
		}

		#endregion

		#region Internal

		internal override void Apply(float position01)
		{
			_arm.localRotation = Quaternion.Euler(Mathf.Lerp(_minAngleDegrees, _maxAngleDegrees, position01), 0f, 0f);
		}

		internal override float Measure()
		{
			return AngleToPosition01(ArmAngleDegrees(_arm.localRotation));
		}

		internal override float Measure(Rigidbody body)
		{
			return AngleToPosition01(ArmAngleDegrees(Quaternion.Inverse(_transform.rotation) * body.rotation));
		}

		internal override float Project(Vector3 worldPoint)
		{
			Vector3 local = ToPivotSpace(worldPoint);
			return AngleToPosition01(Mathf.Atan2(local.z, local.y) * Mathf.Rad2Deg);
		}

		internal override bool TryGetContact(
			Vector3 worldPoint,
			float radiusMeters,
			out float center01,
			out float halfWidth01)
		{
			Vector3 local = ToPivotSpace(worldPoint);
			float radialMeters = Mathf.Sqrt(local.y * local.y + local.z * local.z);
			bool isAtHandle = radialMeters > _armLengthMeters - _handleRadiusMeters;
			float thicknessMeters = (isAtHandle ? _handleRadiusMeters : _stickRadiusMeters) + radiusMeters;

			center01 = AngleToPosition01(Mathf.Atan2(local.z, local.y) * Mathf.Rad2Deg);
			halfWidth01 = 0f;

			if (radialMeters < MinContactRadiusMeters
				|| radialMeters > _armLengthMeters + _handleRadiusMeters + radiusMeters
				|| Mathf.Abs(local.x) > thicknessMeters)
			{
				return false;
			}

			float halfAngleDegrees = Mathf.Asin(Mathf.Min(1f, thicknessMeters / radialMeters)) * Mathf.Rad2Deg;
			halfWidth01 = halfAngleDegrees / RangeDegrees;
			return true;
		}

		internal override void ConfigureJoint(ConfigurableJoint joint, Rigidbody anchorBody)
		{
			// Angular limits are measured from the pose the joint is set up in, so do that with the arm upright
			Quaternion rotation = _arm.localRotation;
			_arm.localRotation = Quaternion.identity;

			joint.connectedBody = anchorBody;
			joint.autoConfigureConnectedAnchor = false;
			joint.anchor = Vector3.zero;
			joint.connectedAnchor = _pivotLocal;
			joint.axis = Vector3.right;
			joint.secondaryAxis = Vector3.up;

			joint.xMotion = ConfigurableJointMotion.Locked;
			joint.yMotion = ConfigurableJointMotion.Locked;
			joint.zMotion = ConfigurableJointMotion.Locked;
			joint.angularXMotion = ConfigurableJointMotion.Limited;
			joint.angularYMotion = ConfigurableJointMotion.Locked;
			joint.angularZMotion = ConfigurableJointMotion.Locked;
			joint.lowAngularXLimit = new SoftJointLimit { limit = _minAngleDegrees };
			joint.highAngularXLimit = new SoftJointLimit { limit = _maxAngleDegrees };

			_arm.localRotation = rotation;
		}

		internal override float GetVelocity01(Rigidbody body)
		{
			return Vector3.Dot(body.angularVelocity, _transform.right) * Mathf.Rad2Deg / RangeDegrees;
		}

		internal override void AddAcceleration01(Rigidbody body, float acceleration01)
		{
			float angularAcceleration = acceleration01 * RangeDegrees * Mathf.Deg2Rad;
			body.AddTorque(_transform.right * angularAcceleration, ForceMode.Acceleration);
		}

		#endregion

		#region Private

		private Vector3 ToPivotSpace(Vector3 worldPoint)
		{
			return _transform.InverseTransformPoint(worldPoint) - _pivotLocal;
		}

		// Positive tips the top away from the player
		private static float ArmAngleDegrees(Quaternion armLocalRotation)
		{
			return Vector3.SignedAngle(Vector3.up, armLocalRotation * Vector3.up, Vector3.right);
		}

		private float AngleToPosition01(float angleDegrees)
		{
			return (angleDegrees - _minAngleDegrees) / RangeDegrees;
		}

		#endregion
	}
}
