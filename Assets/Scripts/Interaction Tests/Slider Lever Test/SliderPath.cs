using UnityEngine;

namespace DroneDDW.StepControls
{
	// A straight track along this transform's right axis, centred on it. The knob slides along it at a fixed height.
	internal class SliderPath : KnobPath
	{
		#region Serialized fields

		[Tooltip("Slides along the track, its pivot is the knob's centre")]
		[SerializeField] private Transform _knob;

		[Tooltip("How far the knob's centre travels from the first step to the last")]
		[Min(0.01f)]
		[SerializeField] private float _lengthMeters = 0.5f;

		[Tooltip("Half the knob's size along the track")]
		[Min(0f)]
		[SerializeField] private float _knobHalfLengthMeters = 0.025f;

		[Tooltip("How far from the knob's centre line a hand can be and still touch it")]
		[Min(0f)]
		[SerializeField] private float _knobReachMeters = 0.035f;

		#endregion

		#region Fields

		private Transform _transform;
		private float _knobHeightMeters;

		#endregion

		#region Setup

		private void Awake()
		{
			_transform = transform;
			_knobHeightMeters = _knob.localPosition.y;
		}

		#endregion

		#region Internal

		internal override void Apply(float position01)
		{
			_knob.localPosition = new Vector3((position01 - 0.5f) * _lengthMeters, _knobHeightMeters, 0f);
		}

		internal override float Measure()
		{
			return _knob.localPosition.x / _lengthMeters + 0.5f;
		}

		internal override float Measure(Rigidbody body)
		{
			return Project(body.position);
		}

		internal override float Project(Vector3 worldPoint)
		{
			return _transform.InverseTransformPoint(worldPoint).x / _lengthMeters + 0.5f;
		}

		internal override bool TryGetContact(
			Vector3 worldPoint,
			float radiusMeters,
			out float center01,
			out float halfWidth01)
		{
			Vector3 local = _transform.InverseTransformPoint(worldPoint);
			float heightOffsetMeters = local.y - _knobHeightMeters;
			float reachMeters = _knobReachMeters + radiusMeters;

			center01 = local.x / _lengthMeters + 0.5f;
			halfWidth01 = (_knobHalfLengthMeters + radiusMeters) / _lengthMeters;
			return heightOffsetMeters * heightOffsetMeters + local.z * local.z <= reachMeters * reachMeters;
		}

		internal override void ConfigureJoint(ConfigurableJoint joint, Rigidbody anchorBody)
		{
			joint.connectedBody = anchorBody;
			joint.autoConfigureConnectedAnchor = false;
			joint.anchor = Vector3.zero;
			joint.axis = Vector3.right;
			joint.secondaryAxis = Vector3.up;

			// Linear limits are symmetric around the connected anchor, so that sits at the middle of the track
			joint.connectedAnchor = new Vector3(0f, _knobHeightMeters, 0f);
			joint.linearLimit = new SoftJointLimit { limit = _lengthMeters * 0.5f };

			joint.xMotion = ConfigurableJointMotion.Limited;
			joint.yMotion = ConfigurableJointMotion.Locked;
			joint.zMotion = ConfigurableJointMotion.Locked;
			joint.angularXMotion = ConfigurableJointMotion.Locked;
			joint.angularYMotion = ConfigurableJointMotion.Locked;
			joint.angularZMotion = ConfigurableJointMotion.Locked;
		}

		internal override float GetVelocity01(Rigidbody body)
		{
			return Vector3.Dot(body.linearVelocity, _transform.right) / _lengthMeters;
		}

		internal override void AddAcceleration01(Rigidbody body, float acceleration01)
		{
			body.AddForce(_transform.right * (acceleration01 * _lengthMeters), ForceMode.Acceleration);
		}

		#endregion
	}
}
