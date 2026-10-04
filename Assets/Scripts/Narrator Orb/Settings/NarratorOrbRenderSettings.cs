using System;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Settings
{
	// Unity only: how the orb's light lands in URP and on the passthrough. Not part of the web preview's settings.
	[Serializable]
	internal sealed class NarratorOrbRenderSettings
	{
		#region Serialized fields

		[Tooltip("Adds each layer the way the browser does (sRGB, then summed), so one layer looks exactly as in the "
			+ "preview. Off: plain linear adding, which makes faint parts brighter and overlaps softer.")]
		[SerializeField] private bool _isWebBlendOn = true;

		[Tooltip("Multiplies all of the orb's light")]
		[Range(0f, 3f)]
		[SerializeField] private float _intensity = 1f;

		[Tooltip("Multiplies every particle's size (1 = the preview's sizes: shell about 1.0 mm, haze and dust "
			+ "0.95 mm, wisps 1.3 mm)")]
		[Range(0.25f, 4f)]
		[SerializeField] private float _particleSizeMultiplier = 1f;

		[Tooltip("Anti-shimmer: particles that would come out smaller than this many pixels are drawn at this size and "
			+ "fainter, keeping the same light. 0 = off.")]
		[Range(0f, 4f)]
		[SerializeField] private float _minParticlePixels;

		[Tooltip("What the orb writes into alpha, which decides how it sits on the passthrough")]
		[SerializeField] private NarratorOrbAlphaMode _alphaMode = NarratorOrbAlphaMode.Brightness;

		[Tooltip("Brightness mode: how strongly the glow's brightness covers the passthrough")]
		[Range(0f, 4f)]
		[SerializeField] private float _alphaGain = 1f;

		#endregion

		#region Properties

		internal bool IsWebBlendOn { get => _isWebBlendOn; set => _isWebBlendOn = value; }
		internal float Intensity { get => _intensity; set => _intensity = value; }
		internal float ParticleSizeMultiplier
		{
			get => _particleSizeMultiplier;
			set => _particleSizeMultiplier = value;
		}

		internal float MinParticlePixels { get => _minParticlePixels; set => _minParticlePixels = value; }
		internal NarratorOrbAlphaMode AlphaMode { get => _alphaMode; set => _alphaMode = value; }
		internal float AlphaGain { get => _alphaGain; set => _alphaGain = value; }

		#endregion
	}
}
