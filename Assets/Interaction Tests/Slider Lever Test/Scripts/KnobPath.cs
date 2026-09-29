using UnityEngine;

namespace DroneDDW.StepControls
{
	// The one-dimensional path a knob moves along, 0 at one end and 1 at the other.
	// Converts between that position and the knob's pose, hand points and physics forces.
	internal abstract class KnobPath : MonoBehaviour
	{
		#region Internal

		// Places the knob, for knobs a script moves
		internal abstract void Apply(float position01);

		// Reads the drawn knob
		internal abstract float Measure();

		// Reads the physics body, which can be ahead of the drawn knob when interpolating
		internal abstract float Measure(Rigidbody body);

		// Not clamped, so grab offsets keep working past the ends
		internal abstract float Project(Vector3 worldPoint);

		// Whether a sphere is level with the knob. If it is, where along the path the knob would be centred on it,
		// and how close the two centres can get along the path before they overlap.
		internal abstract bool TryGetContact(
			Vector3 worldPoint,
			float radiusMeters,
			out float center01,
			out float halfWidth01);

		internal abstract void ConfigureJoint(ConfigurableJoint joint, Rigidbody anchorBody);

		internal abstract float GetVelocity01(Rigidbody body);

		internal abstract void AddAcceleration01(Rigidbody body, float acceleration01);

		#endregion
	}
}
