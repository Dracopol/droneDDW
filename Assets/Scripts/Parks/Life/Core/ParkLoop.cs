using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// A looping park sound on its own source (a drone's rotors, a fountain, crickets). It fades towards the volume
	/// it is given and only plays while it can be heard, so distant loops cost no voices. Each loop starts at a random
	/// point of its clip with its own slight detune, so copies of one clip never play in step.
	/// </summary>
	internal sealed class ParkLoop
	{
		#region Fields

		private readonly AudioSource _source;
		private readonly ParkLifeSound _sound;
		private readonly ParkLife _life;
		private readonly float _detune;
		private float _level;

		#endregion

		#region Properties

		internal Transform Transform => _source.transform;
		internal float Level => _level;

		#endregion

		#region Setup

		internal ParkLoop(ParkLife life, Transform parent, string name, ParkLifeSound sound, int priority, float dopplerLevel)
		{
			_life = life;
			_sound = sound;
			_source = ParkLifeAudio.CreateSource(parent, name);
			ParkLifeAudio.Configure(_source, sound, life.IsSpatialized, priority, dopplerLevel);
			_source.loop = true;
			_source.clip = sound.PickClip();
			_source.volume = 0f;
			_detune = sound.PickPitch();
		}

		#endregion

		#region Internal

		/// <summary>Moves the volume towards a target (0 to 1 of the sound's volume) and starts or stops playback.</summary>
		internal void Update(float target, float pitchScale, float deltaTime, float fadeSeconds = 0.5f)
		{
			_level = fadeSeconds > 0f
				? Mathf.MoveTowards(_level, Mathf.Clamp01(target), deltaTime / fadeSeconds)
				: Mathf.Clamp01(target);
			if (_source.clip == null || !_source.isActiveAndEnabled)
			{
				return;
			}

			float far = _sound.FarMeters;
			bool isAudible = _level > 0.001f && (_source.transform.position - _life.ListenerPosition).sqrMagnitude < far * far;
			if (isAudible && !_source.isPlaying)
			{
				_source.time = Random.value * _source.clip.length;
				_source.Play();
			}
			else if (!isAudible && _source.isPlaying)
			{
				_source.Stop();
			}

			_source.volume = _sound.Volume * _level * _life.Volume;
			_source.pitch = _detune * pitchScale;
		}

		#endregion
	}
}
