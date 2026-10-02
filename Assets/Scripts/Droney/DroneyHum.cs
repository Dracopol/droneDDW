using UnityEngine;

namespace DroneDDW.Droney
{
	// A quiet looping hum from Droney, a little higher and louder while she flies
	internal class DroneyHum : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("Looping, plays on awake")]
		[SerializeField] private AudioSource _audioSource;
		[SerializeField] private DroneyFlight _droneyFlight;

		[Range(0f, 1f)]
		[SerializeField] private float _idleVolume = 0.15f;

		[Range(0f, 1f)]
		[SerializeField] private float _extraVolume = 0.1f;

		[SerializeField] private float _idlePitch = 1f;
		[SerializeField] private float _extraPitch = 0.25f;

		[Tooltip("Speed at which the hum reaches its highest pitch and volume")]
		[Min(0.01f)]
		[SerializeField] private float _fullSpeedMetersPerSecond = 0.8f;

		#endregion

		#region Update

		private void Update()
		{
			float speedFraction = Mathf.Clamp01(_droneyFlight.SpeedMetersPerSecond / _fullSpeedMetersPerSecond);
			_audioSource.volume = _idleVolume + _extraVolume * speedFraction;
			_audioSource.pitch = _idlePitch + _extraPitch * speedFraction;
		}

		#endregion
	}
}
