using UnityEngine;

namespace DroneDDW.Characters
{
	// Turns a voice into a talking mouth with two drawings. The mouth opens on vowel sounds and closes between them,
	// and a new syllable during a long open stretch closes it for a moment, so phrases read as separate syllables.
	// Minimum hold times keep it from flickering. Also gives a 0 to 1 openness for the smooth mouth, which eases back
	// to 0 whenever the flap closes.
	internal sealed class MouthFlap
	{
		#region Fields

		private float _envelope;
		private float _openness;
		private float _heldSeconds = float.MaxValue;
		private bool _isOpen;
		private int _openCount;

		#endregion

		#region Properties

		// The smoothed vowel level the mouth follows (0 to 1)
		internal float Envelope => _envelope;
		internal bool IsOpen => _isOpen;
		internal float Openness => _openness;

		// How often the mouth opened since the last Reset
		internal int OpenCount => _openCount;

		#endregion

		#region Internal

		// vowel: the voice's vowel level (0 to 1); isSyllableStart: a syllable began since the last step
		internal void Step(float vowel, bool isSyllableStart, float deltaSeconds, CharacterPortraitSettings settings)
		{
			_envelope = Follow(_envelope, vowel, settings.AttackSeconds, settings.ReleaseSeconds, deltaSeconds);
			_heldSeconds += deltaSeconds;
			if (_isOpen)
			{
				bool isQuiet = _envelope <= settings.CloseThreshold && _heldSeconds >= settings.MinOpenSeconds;
				bool isNewSyllable = isSyllableStart && _heldSeconds >= settings.SyllableFlapSeconds;
				if (isQuiet || isNewSyllable)
				{
					_isOpen = false;
					_heldSeconds = 0f;
				}
			}
			else if ((_envelope >= settings.OpenThreshold || isSyllableStart)
				&& _heldSeconds >= settings.MinClosedSeconds)
			{
				_isOpen = true;
				_heldSeconds = 0f;
				_openCount++;
			}

			float range = Mathf.Max(1e-4f, 1f - settings.CloseThreshold);
			float targetOpenness = _isOpen
				? Mathf.Clamp01((_envelope - settings.CloseThreshold) / range * settings.SmoothGain)
				: 0f;
			_openness = Follow(_openness, targetOpenness, settings.AttackSeconds, settings.ReleaseSeconds,
				deltaSeconds);
		}

		internal void Reset()
		{
			_envelope = 0f;
			_openness = 0f;
			_heldSeconds = float.MaxValue;
			_isOpen = false;
			_openCount = 0;
		}

		#endregion

		#region Private

		// Eases towards the target, over the attack time when rising and the release time when falling
		private static float Follow(float current, float target, float attackSeconds, float releaseSeconds,
			float deltaSeconds)
		{
			float timeConstant = Mathf.Max(1e-4f, target > current ? attackSeconds : releaseSeconds);
			return current + (target - current) * (1f - Mathf.Exp(-deltaSeconds / timeConstant));
		}

		#endregion
	}
}
