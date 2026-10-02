using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// A few audio sources shared by every short park sound (calls, beeps, plops, pings). A sound beyond its far
	/// distance from the listener is skipped; when every source is busy, the one playing longest is taken over.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Sound Pool")]
	public sealed class ParkSoundPool : MonoBehaviour
	{
		#region Fields

		private AudioSource[] _sources = new AudioSource[0];
		private float[] _startTimes = new float[0];
		private ParkLife _life;

		#endregion

		#region Internal

		internal void Initialize(ParkLife life, int voices)
		{
			_life = life;
			_sources = new AudioSource[voices];
			_startTimes = new float[voices];
			for (int i = 0; i < voices; i++)
			{
				_sources[i] = ParkLifeAudio.CreateSource(transform, $"Voice {i + 1}");
			}
		}

		/// <summary>Plays one of the sound's clips once at a point, if the listener is close enough to hear it.</summary>
		internal void Play(ParkLifeSound sound, Vector3 position, float volumeScale = 1f, float pitchScale = 1f,
			int priority = 128)
		{
			if (_life == null || volumeScale <= 0f || !sound.HasClip)
			{
				return;
			}

			float far = sound.FarMeters;
			if ((position - _life.ListenerPosition).sqrMagnitude > far * far)
			{
				return;
			}

			int index = PickSource();
			AudioSource source = _sources[index];
			ParkLifeAudio.Configure(source, sound, _life.IsSpatialized, priority, 0f);
			source.transform.position = position;
			source.clip = sound.PickClip();
			source.pitch = sound.PickPitch() * pitchScale;
			source.volume = sound.Volume * volumeScale * _life.Volume;
			source.Play();
			_startTimes[index] = Time.time;
		}

		#endregion

		#region Private

		private int PickSource()
		{
			int oldest = 0;
			for (int i = 0; i < _sources.Length; i++)
			{
				if (!_sources[i].isPlaying)
				{
					return i;
				}

				if (_startTimes[i] < _startTimes[oldest])
				{
					oldest = i;
				}
			}

			return oldest;
		}

		#endregion
	}
}
