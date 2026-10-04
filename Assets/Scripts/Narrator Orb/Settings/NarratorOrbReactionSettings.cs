using System;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Settings
{
	// What the voice drives (S.reaction in the web preview)
	[Serializable]
	internal sealed class NarratorOrbReactionSettings
	{
		#region Serialized fields

		[Tooltip("Swell: how much the orb grows with loudness, as a share of its size (swell)")]
		[Range(0f, 0.15f)]
		[SerializeField] private float _swell = 0.06f;

		[Tooltip("Glow: how much the rim halos brighten with loudness (glow)")]
		[Range(0f, 3f)]
		[SerializeField] private float _glow = 1.4f;

		[Tooltip("Syllable ripples: strength of the rings each syllable sends over the surface (ripples)")]
		[Range(0f, 2f)]
		[SerializeField] private float _ripples = 1f;

		[Tooltip("Wisps on vowels: how strongly the four wisps light up on vowels (wisps)")]
		[Range(0f, 1.5f)]
		[SerializeField] private float _wisps = 1f;

		[Tooltip("Sparkle on hiss: how much s, sh, t and f make the particles flash (sparkle)")]
		[Range(0f, 3f)]
		[SerializeField] private float _sparkle = 1.2f;

		[Tooltip("Smoothing: multiplies every release time, so higher values fade out slower (smoothing)")]
		[Range(0.4f, 2.5f)]
		[SerializeField] private float _smoothing = 1f;

		#endregion

		#region Properties

		internal float Swell { get => _swell; set => _swell = value; }
		internal float Glow { get => _glow; set => _glow = value; }
		internal float Ripples { get => _ripples; set => _ripples = value; }
		internal float Wisps { get => _wisps; set => _wisps = value; }
		internal float Sparkle { get => _sparkle; set => _sparkle = value; }
		internal float Smoothing { get => _smoothing; set => _smoothing = value; }

		#endregion
	}
}
