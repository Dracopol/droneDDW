using UnityEngine;

namespace DroneDDW.Admin
{
	// Keeps the open menu locked in front of the head: it follows the head turning and tilting up or down, but not
	// rolling, so the text stays level when the head leans sideways. A little smoothing (settings) keeps it from shaking
	// with every small head movement.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Admin/Admin Menu Placement")]
	internal sealed class AdminMenuPlacement : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private AdminMenuSettings _settings;

		[Tooltip("The headset camera")]
		[SerializeField] private Transform _head;

		#endregion

		#region Fields

		// Looking more steeply than this up or down, the head's up direction sets the menu's roll instead of the world's
		private const float MaxUprightDot = 0.98f;

		private Transform _transform;

		#endregion

		#region Properties

		internal bool IsFollowing { get; set; }

		internal Transform Head => _head;

		#endregion

		#region Setup

		private void Awake()
		{
			_transform = transform;
		}

		#endregion

		#region Update

		// Unscaled time: the menu runs while the session is paused
		private void LateUpdate()
		{
			if (!IsFollowing)
			{
				return;
			}

			GetTargetPose(out Vector3 position, out Quaternion rotation);
			float smoothSeconds = _settings.LockSmoothSeconds;
			float blend = smoothSeconds > 0f ? 1f - Mathf.Exp(-Time.unscaledDeltaTime / smoothSeconds) : 1f;
			_transform.SetPositionAndRotation(Vector3.Lerp(_transform.position, position, blend),
				Quaternion.Slerp(_transform.rotation, rotation, blend));
		}

		#endregion

		#region Internal

		internal void Configure(AdminMenuSettings settings, Transform head)
		{
			_settings = settings;
			_head = head;
		}

		internal void ConfigureHead(Transform head)
		{
			_head = head;
		}

		// Snaps the menu in front of the head, e.g. when it opens
		internal void PlaceInFront()
		{
			GetTargetPose(out Vector3 position, out Quaternion rotation);
			_transform.SetPositionAndRotation(position, rotation);
		}

		#endregion

		#region Private

		// The settings' distance ahead of the head and drop below its gaze, facing the head, without its roll
		private void GetTargetPose(out Vector3 position, out Quaternion rotation)
		{
			Vector3 forward = _head.forward;
			Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < MaxUprightDot ? Vector3.up : _head.up;
			rotation = Quaternion.LookRotation(forward, up);
			position = _head.position + rotation * new Vector3(0f, -_settings.DropMeters, _settings.DistanceMeters);
		}

		#endregion
	}
}
