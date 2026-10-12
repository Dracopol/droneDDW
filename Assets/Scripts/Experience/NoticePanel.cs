using TMPro;
using UnityEngine;

namespace DroneDDW.Experience
{
	// A short message in front of whoever wears the headset: for the admin after check-in ("Give the headset to
	// participant 104") and for the participant at the end. It stays in front of the head, a little below the eyes,
	// catching up smoothly as the head turns, so it can't be lost behind them.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/Notice Panel")]
	internal sealed class NoticePanel : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("The headset camera")]
		[SerializeField] private Transform _head;

		[SerializeField] private TMP_Text _text;

		[Min(0.2f)]
		[SerializeField] private float _distanceMeters = 0.9f;

		[SerializeField] private float _dropMeters = 0.1f;

		[Tooltip("How quickly it catches up with the head")]
		[Min(0f)]
		[SerializeField] private float _followSeconds = 0.25f;

		#endregion

		#region Fields

		// Looking more steeply than this up or down, the panel keeps the head's own up
		private const float MaxUprightDot = 0.98f;

		private Transform _transform;

		#endregion

		#region Properties

		internal bool IsShown => gameObject.activeSelf;
		internal string Text => _text.text;

		#endregion

		#region Setup

		private void Awake()
		{
			_transform = transform;
		}

		#endregion

		#region Update

		// Unscaled time: it follows while the session is paused too
		private void LateUpdate()
		{
			GetTargetPose(out Vector3 position, out Quaternion rotation);
			float blend = _followSeconds > 0f ? 1f - Mathf.Exp(-Time.unscaledDeltaTime / _followSeconds) : 1f;
			_transform.SetPositionAndRotation(Vector3.Lerp(_transform.position, position, blend),
				Quaternion.Slerp(_transform.rotation, rotation, blend));
		}

		#endregion

		#region Internal

		internal void Configure(Transform head, TMP_Text text)
		{
			_head = head;
			_text = text;
		}

		internal void Show(string message)
		{
			_text.text = message;
			gameObject.SetActive(true);
			GetTargetPose(out Vector3 position, out Quaternion rotation);
			_transform.SetPositionAndRotation(position, rotation);
		}

		internal void Hide()
		{
			gameObject.SetActive(false);
		}

		#endregion

		#region Private

		private void GetTargetPose(out Vector3 position, out Quaternion rotation)
		{
			Vector3 forward = _head.forward;
			Vector3 up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < MaxUprightDot ? Vector3.up : _head.up;
			rotation = Quaternion.LookRotation(forward, up);
			position = _head.position + rotation * new Vector3(0f, -_dropMeters, _distanceMeters);
		}

		#endregion
	}
}
