using UnityEngine;

namespace DroneDDW.Admin.Sessions
{
	// Where the player belongs in this scene: Reset player location puts their head above this point, facing its blue
	// (forward) arrow, and with an eye height, the eyes that high above it, whatever the chair and the headset's floor.
	// Without one the head keeps its tracked height, and this point's height doesn't matter.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Admin/Player Spot")]
	public sealed class PlayerSpot : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("How high above this point Reset player location puts the eyes, in metres; 0 keeps the head's tracked "
			+ "height")]
		[Min(0f)]
		[SerializeField] private float _eyeHeightMeters;

		#endregion

		#region Fields

		private const float GizmoRadiusMeters = 0.3f;
		private const int GizmoSegments = 32;

		#endregion

		#region Properties

		public Vector3 Position => transform.position;

		public bool HasEyeHeight => _eyeHeightMeters > 0f;

		// Where the eyes go, with an eye height
		public float EyeY => transform.position.y + _eyeHeightMeters;

		// Level with the floor
		public Vector3 Forward
		{
			get
			{
				Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
				return forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
			}
		}

		#endregion

		#region Internal

		internal void Configure(float eyeHeightMeters)
		{
			_eyeHeightMeters = Mathf.Max(0f, eyeHeightMeters);
		}

		#endregion

		#region Update

		private void OnDrawGizmos()
		{
			Gizmos.color = Color.cyan;
			Vector3 center = Position;
			Vector3 previous = center + Vector3.right * GizmoRadiusMeters;
			for (int segment = 1; segment <= GizmoSegments; segment++)
			{
				float angle = segment * Mathf.PI * 2f / GizmoSegments;
				Vector3 next = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * GizmoRadiusMeters;
				Gizmos.DrawLine(previous, next);
				previous = next;
			}

			Gizmos.color = Color.blue;
			Gizmos.DrawLine(center, center + Forward * GizmoRadiusMeters * 1.5f);
		}

		#endregion
	}
}
