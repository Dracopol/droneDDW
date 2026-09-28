using UnityEngine;

namespace DroneDDW.Droney
{
	// Idle bob and slight turning on Droney's model, with a quicker, smaller wobble while she talks.
	// Sits on the model under the flying root, so it adds to the flight instead of fighting it.
	internal class DroneyHover : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private DroneyVoice _droneyVoice;

		[Header("Idle")]
		[Min(0f)]
		[SerializeField] private float _bobHeightMeters = 0.02f;

		[Min(0f)]
		[SerializeField] private float _bobCyclesPerSecond = 0.4f;

		[Range(0f, 45f)]
		[SerializeField] private float _yawDriftDegrees = 6f;

		[Min(0f)]
		[SerializeField] private float _yawDriftSpeed = 0.15f;

		[Header("Talking")]
		[Min(0f)]
		[SerializeField] private float _talkWobbleMeters = 0.004f;

		[Range(0f, 45f)]
		[SerializeField] private float _talkNodDegrees = 3f;

		[Min(0f)]
		[SerializeField] private float _talkWobbleSpeed = 4f;

		[Tooltip("How quickly the talking wobble fades in and out")]
		[Min(0.01f)]
		[SerializeField] private float _talkBlendSeconds = 0.2f;

		#endregion

		#region Fields

		private Vector3 _restLocalPosition;
		private Quaternion _restLocalRotation;
		private float _talkWeight;
		private float _talkWeightVelocity;

		#endregion

		#region Setup

		private void Awake()
		{
			_restLocalPosition = transform.localPosition;
			_restLocalRotation = transform.localRotation;
		}

		#endregion

		#region Update

		private void Update()
		{
			float time = Time.time;
			float targetTalkWeight = _droneyVoice.IsSpeaking ? 1f : 0f;
			_talkWeight = Mathf.SmoothDamp(_talkWeight, targetTalkWeight, ref _talkWeightVelocity, _talkBlendSeconds);

			float talkTime = time * _talkWobbleSpeed;
			Vector3 bob = Vector3.up * (Mathf.Sin(time * _bobCyclesPerSecond * 2f * Mathf.PI) * _bobHeightMeters);
			Vector3 wobble = new Vector3(Noise(talkTime, 0f), Noise(talkTime, 7.3f), Noise(talkTime, 13.1f))
				* (_talkWobbleMeters * _talkWeight);

			// The offsets are in meters, the parent is scaled, so they're converted into its space
			transform.localPosition = _restLocalPosition + transform.parent.InverseTransformVector(bob + wobble);

			float yawDegrees = Noise(time * _yawDriftSpeed, 21.7f) * _yawDriftDegrees;
			float nodDegrees = Noise(talkTime, 31.1f) * _talkNodDegrees * _talkWeight;
			transform.localRotation = Quaternion.Euler(nodDegrees, yawDegrees, 0f) * _restLocalRotation;
		}

		#endregion

		#region Private

		// Smooth random value from -1 to 1, the seed keeps each axis independent
		private static float Noise(float time, float seed)
		{
			return (Mathf.PerlinNoise(time + seed, seed) - 0.5f) * 2f;
		}

		#endregion
	}
}
