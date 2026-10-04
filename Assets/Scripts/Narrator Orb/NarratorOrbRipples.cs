using DroneDDW.NarratorOrb.Rendering;
using UnityEngine;

namespace DroneDDW.NarratorOrb
{
	// The six syllable ripples (spawnRipple, ripData and ripS in the web preview). Each starts near the point facing
	// the listener, jittered with the preview's rnd2 stream, and the newest replaces the oldest.
	internal sealed class NarratorOrbRipples
	{
		#region Fields

		internal const int SlotCount = 6;

		// rnd2's state once the particle layout has used it, so the jitter continues the preview's stream
		internal const long JitterSeed = 1616505683;

		private readonly Vector4[] _origins = new Vector4[SlotCount];
		private readonly float[] _strengths = new float[SlotCount];
		private readonly LehmerRandom _jitter = new(JitterSeed, LehmerRandom.AttributeMultiplier);
		private int _nextSlot;

		#endregion

		#region Properties

		// Per slot: the start direction in orb space (xyz) and the start time on the orb's clock (w)
		internal Vector4[] Origins => _origins;

		// Per slot: 0 means unused
		internal float[] Strengths => _strengths;

		#endregion

		#region Setup

		internal NarratorOrbRipples()
		{
			for (int slot = 0; slot < SlotCount; slot++)
			{
				_origins[slot] = new Vector4(0f, 0f, 1f, -100f);
			}
		}

		#endregion

		#region Internal

		// listenerPosition: where the listener is, in orb space
		internal void Spawn(Vector3 listenerPosition, float strength, float spreadFromFront, float timeSeconds)
		{
			float x = (float)(_jitter.Next() * 2 - 1);
			float y = (float)(_jitter.Next() * 2 - 1);
			float z = (float)(_jitter.Next() * 2 - 1);
			Vector3 front = (listenerPosition.normalized + new Vector3(x, y, z) * spreadFromFront).normalized;
			_origins[_nextSlot] = new Vector4(front.x, front.y, front.z, timeSeconds);
			_strengths[_nextSlot] = strength;
			_nextSlot = (_nextSlot + 1) % SlotCount;
		}

		#endregion
	}
}
