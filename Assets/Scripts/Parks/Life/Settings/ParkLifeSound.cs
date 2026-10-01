using System;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// One kind of park sound: its clips (one is picked at random each time), how loud it is, how much its pitch
	/// varies, and how far it carries. It is at full volume up to the near distance and fades out completely by the
	/// far distance, so a loop can stop there without a jump.
	/// </summary>
	[Serializable]
	public sealed class ParkLifeSound
	{
		#region Serialized fields

		[SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();
		[SerializeField, Range(0f, 1f)] private float _volume = 1f;

		[SerializeField, Range(0f, 0.5f), Tooltip("Pitch varies at random by up to this much each time it plays.")]
		private float _pitchSpread = 0.05f;

		[SerializeField, Min(0.05f), Tooltip("Full volume up to this distance.")]
		private float _nearMeters = 1f;

		[SerializeField, Min(0.1f), Tooltip("Silent from this distance on.")]
		private float _farMeters = 30f;

		#endregion

		#region Properties

		public bool HasClip => _clips != null && Array.Exists(_clips, clip => clip != null);
		public float Volume => _volume;
		public float NearMeters => _nearMeters;
		public float FarMeters => Mathf.Max(_farMeters, _nearMeters + 0.1f);

		#endregion

		#region Setup

		public ParkLifeSound(float volume, float nearMeters, float farMeters, float pitchSpread = 0.05f)
		{
			_volume = volume;
			_nearMeters = nearMeters;
			_farMeters = farMeters;
			_pitchSpread = pitchSpread;
		}

		#endregion

		#region Public

		public AudioClip PickClip()
		{
			if (!HasClip)
			{
				return null;
			}

			AudioClip clip;
			do
			{
				clip = _clips[UnityEngine.Random.Range(0, _clips.Length)];
			}
			while (clip == null);

			return clip;
		}

		public float PickPitch()
		{
			return 1f + UnityEngine.Random.Range(-_pitchSpread, _pitchSpread);
		}

		#endregion

		#region Internal

		/// <summary>For the park builder, filling in the clips of a new settings asset.</summary>
		internal void SetClipsIfEmpty(params AudioClip[] clips)
		{
			if (!HasClip)
			{
				_clips = clips;
			}
		}

		#endregion
	}
}
