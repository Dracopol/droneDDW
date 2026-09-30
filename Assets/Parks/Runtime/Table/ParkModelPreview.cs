using System;
using DroneDDW.Parks.Levels;
using UnityEngine;

namespace DroneDDW.Parks.Table
{
	/// <summary>
	/// The miniature park on the table: shows one of the five level models and turns the view cone
	/// around the you-are-here pin so it points where the viewer is looking in the real park.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Model Preview")]
	public sealed class ParkModelPreview : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField, Tooltip("One miniature per level, level 0 first.")]
		private GameObject[] _models = Array.Empty<GameObject>();

		[SerializeField, Tooltip("The cone on the pin (Rig_ViewCone). Its forward points north at rotation zero.")]
		private Transform _viewCone;

		[SerializeField, Tooltip("The real park, used as the north reference. Leave empty to use world axes.")]
		private Transform _parkRoot;

		[SerializeField, Tooltip("The XR camera. Falls back to Camera.main when left empty.")]
		private Transform _head;

		[SerializeField, Min(0f), Tooltip("How quickly the cone follows the head. 0 = no smoothing.")]
		private float _coneSmoothing = 10f;

		#endregion

		#region Fields

		private int _shownLevel = -1;
		private float _coneYaw;
		private bool _hasConeYaw;

		#endregion

		#region Properties

		public int ShownLevel => _shownLevel;

		#endregion

		#region Update

		private void LateUpdate()
		{
			if (_viewCone == null)
			{
				return;
			}

			Transform head = ResolveHead();
			if (head == null)
			{
				return;
			}

			Vector3 forward = _parkRoot != null ? _parkRoot.InverseTransformDirection(head.forward) : head.forward;
			if (forward.x * forward.x + forward.z * forward.z < 0.0001f)
			{
				// Looking straight up or down: keep the last direction.
				return;
			}

			float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
			if (!_hasConeYaw || _coneSmoothing <= 0f)
			{
				_coneYaw = yaw;
				_hasConeYaw = true;
			}
			else
			{
				_coneYaw = Mathf.LerpAngle(_coneYaw, yaw, 1f - Mathf.Exp(-_coneSmoothing * Time.deltaTime));
			}

			_viewCone.localRotation = Quaternion.Euler(0f, _coneYaw, 0f);
		}

		#endregion

		#region Public

		public void ShowLevel(int level)
		{
			level = ParkLevels.Clamp(level);
			_shownLevel = level;
			for (int i = 0; i < _models.Length; i++)
			{
				if (_models[i] != null)
				{
					_models[i].SetActive(i == level);
				}
			}
		}

		#endregion

		#region Internal

		internal void Configure(GameObject[] models, Transform viewCone, Transform parkRoot, Transform head)
		{
			_models = models ?? Array.Empty<GameObject>();
			_viewCone = viewCone;
			_parkRoot = parkRoot;
			_head = head;
		}

		#endregion

		#region Private

		private Transform ResolveHead()
		{
			if (_head != null)
			{
				return _head;
			}

			Camera main = Camera.main;
			return main != null ? main.transform : null;
		}

		#endregion
	}
}
