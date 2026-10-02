using UnityEngine;

namespace DroneDDW.Buttons
{
	// A rigidbody cap on a joint that only lets it slide along this transform's up axis.
	// A spring force pushes it back up, hand colliders push it down through the physics engine.
	internal class PhysicsButtonCap : ButtonCap
	{
		#region Serialized fields

		[Tooltip("Kinematic body on this button's root, the joint hangs off it")]
		[SerializeField] private Rigidbody _anchorBody;

		[Tooltip("The cap's pivot is its top face, it has to start at rest")]
		[SerializeField] private Rigidbody _capBody;

		[Tooltip("How far the cap can move down, it rests this high above this transform")]
		[Min(0.001f)]
		[SerializeField] private float _travelMeters = 0.02f;

		[Header("Spring")]
		[Tooltip("How fast the cap springs back, higher is snappier")]
		[Min(0.1f)]
		[SerializeField] private float _springFrequencyHertz = 8f;

		[Tooltip("1 returns without overshooting, lower values bounce more")]
		[Range(0f, 1f)]
		[SerializeField] private float _dampingRatio = 0.5f;

		#endregion

		#region Fields

		private Transform _transform;
		private Transform _capTransform;

		#endregion

		#region Properties

		// Reads the interpolated transform, so it matches what's drawn this frame
		internal override float PressDepth01 => Mathf.Clamp01(1f - _capTransform.localPosition.y / _travelMeters);

		#endregion

		#region Setup

		private void Awake()
		{
			_transform = transform;
			_capTransform = _capBody.transform;
			CreateJoint();
		}

		#endregion

		#region Update

		// Same spring as the scripted cap, applied as an acceleration so the cap's mass doesn't change the feel
		private void FixedUpdate()
		{
			float angularFrequency = 2f * Mathf.PI * _springFrequencyHertz;
			float stiffness = angularFrequency * angularFrequency;
			float damping = 2f * _dampingRatio * angularFrequency;

			Vector3 up = _transform.up;
			float heightMeters = _transform.InverseTransformPoint(_capBody.position).y;
			float velocityMetersPerSecond = Vector3.Dot(_capBody.linearVelocity, up);
			float acceleration = stiffness * (_travelMeters - heightMeters) - damping * velocityMetersPerSecond;

			_capBody.AddForce(up * acceleration, ForceMode.Acceleration);
		}

		#endregion

		#region Private

		private void CreateJoint()
		{
			ConfigurableJoint joint = _capBody.gameObject.AddComponent<ConfigurableJoint>();
			joint.connectedBody = _anchorBody;
			joint.autoConfigureConnectedAnchor = false;
			joint.anchor = Vector3.zero;
			joint.axis = Vector3.right;
			joint.secondaryAxis = Vector3.up;

			// Linear limits are symmetric around the connected anchor, so centre it on the travel
			joint.connectedAnchor = new Vector3(0f, _travelMeters * 0.5f, 0f);
			joint.linearLimit = new SoftJointLimit { limit = _travelMeters * 0.5f };

			joint.xMotion = ConfigurableJointMotion.Locked;
			joint.yMotion = ConfigurableJointMotion.Limited;
			joint.zMotion = ConfigurableJointMotion.Locked;
			joint.angularXMotion = ConfigurableJointMotion.Locked;
			joint.angularYMotion = ConfigurableJointMotion.Locked;
			joint.angularZMotion = ConfigurableJointMotion.Locked;
		}

		#endregion
	}
}
