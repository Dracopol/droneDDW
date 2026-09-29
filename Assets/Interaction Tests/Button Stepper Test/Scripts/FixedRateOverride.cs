using UnityEngine;

namespace DroneDDW.Buttons
{
	// Runs physics at a higher rate while enabled and puts the project's rate back afterwards
	internal class FixedRateOverride : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("Physics steps per second, at least the headset's refresh rate so rigidbodies don't lag behind")]
		[Min(1f)]
		[SerializeField] private float _rateHertz = 90f;

		#endregion

		#region Fields

		private float _originalStepSeconds;

		#endregion

		#region Setup

		private void OnEnable()
		{
			_originalStepSeconds = Time.fixedDeltaTime;
			Time.fixedDeltaTime = 1f / _rateHertz;
			Debug.Log($"[DDW Buttons] Physics runs at {_rateHertz:0} Hz", this);
		}

		private void OnDisable()
		{
			Time.fixedDeltaTime = _originalStepSeconds;
		}

		#endregion
	}
}
