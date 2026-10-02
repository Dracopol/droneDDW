using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DroneDDW.Interaction
{
	// A sheet the stamp can mark, owns its own stamp mark
	internal class Paper : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private Collider _collider;
		[SerializeField] private DecalProjector _stampMark;

		#endregion

		#region Properties

		internal Collider Collider => _collider;

		#endregion

		#region Setup

		private void Awake()
		{
			ClearMark();
		}

		#endregion

		#region Internal

		internal void ShowMark(Vector3 stampFacePosition, float stampYawDegrees)
		{
			// Decals project along their local +Z, so tilt 90 degrees to point down, keeping the stamp's yaw
			Quaternion markRotation = Quaternion.Euler(90f, stampYawDegrees, 0f);
			Vector3 markPosition = new(stampFacePosition.x, _collider.bounds.max.y, stampFacePosition.z);

			// Centre the projection box on the paper surface, whatever the projector's pivot is set to
			markPosition -= markRotation * _stampMark.pivot;

			_stampMark.transform.SetPositionAndRotation(markPosition, markRotation);
			_stampMark.enabled = true;
		}

		internal void ClearMark()
		{
			_stampMark.enabled = false;
		}

		#endregion
	}
}
