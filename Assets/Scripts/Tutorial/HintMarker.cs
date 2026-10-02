using UnityEngine;

namespace DroneDDW.Tutorial
{
	// A glowing diamond that floats, bobs and spins above whatever the tutorial is pointing out
	internal class HintMarker : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private Renderer _renderer;

		[Min(0f)]
		[SerializeField] private float _bobHeightMeters = 0.02f;

		[Min(0f)]
		[SerializeField] private float _bobCyclesPerSecond = 1f;
		[SerializeField] private float _spinDegreesPerSecond = 90f;

		#endregion

		#region Fields

		// Stood on one corner, so the cube reads as a diamond
		private static readonly Quaternion DiamondRotation = Quaternion.Euler(45f, 0f, 45f);

		private HintTarget _target;
		private float _spinDegrees;

		#endregion

		#region Setup

		private void Awake()
		{
			Hide();
		}

		#endregion

		#region Update

		private void LateUpdate()
		{
			if (_target == null)
				return;

			float bobMeters = Mathf.Sin(Time.time * _bobCyclesPerSecond * 2f * Mathf.PI) * _bobHeightMeters;
			_spinDegrees += _spinDegreesPerSecond * Time.deltaTime;

			transform.SetPositionAndRotation(
				_target.Position + Vector3.up * (_target.MarkerHeightMeters + bobMeters),
				Quaternion.Euler(0f, _spinDegrees, 0f) * DiamondRotation);
		}

		#endregion

		#region Internal

		internal void Show(HintTarget target)
		{
			_target = target;
			_renderer.enabled = true;
		}

		internal void Hide()
		{
			_target = null;
			_renderer.enabled = false;
		}

		#endregion
	}
}
