using UnityEngine;

namespace DroneDDW.StepControls
{
	// A rigidbody knob on a joint, pushed around by the hand colliders.
	// Each notch pulls it in with a spring force, so it settles into the nearest one once the hands let go.
	internal class PhysicsKnob : StepKnob
	{
		#region Serialized fields

		[SerializeField] private KnobPath _path;

		[Tooltip("Kinematic body the joint hangs off, it moves with the panel")]
		[SerializeField] private Rigidbody _anchorBody;

		[SerializeField] private Rigidbody _body;

		[Header("Notches")]
		[Tooltip("How fast the knob swings into a notch, higher pulls harder")]
		[Min(0.1f)]
		[SerializeField] private float _notchFrequencyHertz = 4f;

		[Tooltip("1 settles without overshooting, lower values bounce past the notch")]
		[Range(0f, 1f)]
		[SerializeField] private float _dampingRatio = 0.7f;

		[Header("Settled")]
		[Tooltip("How close to a notch counts as settled, as a fraction of the step")]
		[Range(0f, 0.5f)]
		[SerializeField] private float _settledDistance = 0.08f;

		[Tooltip("How slow counts as settled, in travel per second")]
		[Min(0f)]
		[SerializeField] private float _settledSpeed01 = 0.05f;

		#endregion

		#region Properties

		internal override float Position01 => Mathf.Clamp01(_path.Measure());

		// Nothing tells the knob which collider is pushing it, so anything but resting in a notch counts as held
		internal override bool IsHeld
		{
			get
			{
				float position01 = Position01;
				float distanceToNotch = Mathf.Abs(position01 - StepPosition01(NearestStep(position01))) / StepSize01;
				return distanceToNotch > _settledDistance || Mathf.Abs(_path.GetVelocity01(_body)) > _settledSpeed01;
			}
		}

		#endregion

		#region Setup

		// The path caches its layout in Awake
		private void Start()
		{
			ConfigurableJoint joint = _body.gameObject.AddComponent<ConfigurableJoint>();
			_path.ConfigureJoint(joint, _anchorBody);
		}

		#endregion

		#region Update

		private void FixedUpdate()
		{
			float angularFrequency = 2f * Mathf.PI * _notchFrequencyHertz;
			float position01 = _path.Measure(_body);
			float offset01 = position01 - StepPosition01(NearestStep(position01));

			// A sine over each step is a spring near every notch, and zero exactly between two
			float pull01 = -angularFrequency * angularFrequency * StepSize01 / (2f * Mathf.PI)
				* Mathf.Sin(2f * Mathf.PI * offset01 / StepSize01);
			float damping01 = -2f * _dampingRatio * angularFrequency * _path.GetVelocity01(_body);

			_path.AddAcceleration01(_body, pull01 + damping01);
		}

		#endregion

		#region Internal

		internal override void SnapTo(float position01)
		{
			_path.Apply(position01);
			_body.linearVelocity = Vector3.zero;
			_body.angularVelocity = Vector3.zero;
		}

		#endregion
	}
}
