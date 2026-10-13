using UnityEngine;

namespace DroneDDW.Voice
{
	// Makes a voice louder (or softer) than its recording, which an AudioSource's volume can't do above 1: it scales the
	// source's audio on the audio thread by its role's level in Voice Levels. Peaks over the knee are rounded off instead
	// of clipping, so a strong boost stays clean. It must sit on the AudioSource's object; the level is read every frame,
	// so tuning the asset in Play mode is heard at once.
	[DisallowMultipleComponent]
	[RequireComponent(typeof(AudioSource))]
	[AddComponentMenu("DroneDDW/Voice/Voice Gain")]
	public sealed class VoiceGain : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private VoiceLevels _levels;
		[SerializeField] private VoiceRole _role;

		#endregion

		#region Fields

		// Above this the limiter starts rounding peaks off towards full scale
		private const float Knee = 0.7f;

		// Written on the main thread, read on the audio thread
		private volatile float _gain = 1f;

		#endregion

		#region Properties

		public VoiceRole Role => _role;
		public float Decibels => _levels != null ? _levels.GetDecibels(_role) : 0f;

		#endregion

		#region Setup

		private void OnEnable()
		{
			_gain = ToGain(Decibels);
		}

		#endregion

		#region Update

		private void Update()
		{
			_gain = ToGain(Decibels);
		}

		// Audio thread
		private void OnAudioFilterRead(float[] data, int channels)
		{
			float gain = _gain;
			if (Mathf.Approximately(gain, 1f))
			{
				return;
			}

			for (int i = 0; i < data.Length; i++)
			{
				data[i] = Limit(data[i] * gain);
			}
		}

		#endregion

		#region Public

		// For the scene builders, which live in other assemblies
		public void Configure(VoiceLevels levels, VoiceRole role)
		{
			_levels = levels;
			_role = role;
		}

		#endregion

		#region Internal

		internal static float ToGain(float decibels)
		{
			return Mathf.Pow(10f, decibels / 20f);
		}

		// Unchanged up to the knee, then eased so it never passes full scale
		internal static float Limit(float sample)
		{
			float size = Mathf.Abs(sample);
			if (size <= Knee)
			{
				return sample;
			}

			float rounded = Knee + (1f - Knee) * (float)System.Math.Tanh((size - Knee) / (1f - Knee));
			return sample < 0f ? -rounded : rounded;
		}

		#endregion
	}
}
