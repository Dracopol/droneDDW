using UnityEngine;

namespace DroneDDW.Admin.Sessions
{
	// Where the player belongs in this scene: Reset player location puts their head above this point, facing its blue
	// (forward) arrow. Its height doesn't matter; the head keeps its tracked height.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Admin/Player Spot")]
	public sealed class PlayerSpot : MonoBehaviour
	{
		#region Fields

		private const float GizmoRadiusMeters = 0.3f;
		private const int GizmoSegments = 32;

		#endregion

		#region Properties

		public Vector3 Position => transform.position;

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
