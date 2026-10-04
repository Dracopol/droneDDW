using System;
using DroneDDW.NarratorOrb.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace DroneDDW.NarratorOrb.Rendering
{
	// The orb's four materials as runtime copies (the assets stay untouched), and every value its shaders read: the
	// uniforms the web preview sets in frame(). Colours go in as linear vectors.
	internal sealed class NarratorOrbMaterials : IDisposable
	{
		#region Fields

		private static readonly int OrbTimeId = Shader.PropertyToID("_OrbTime");
		private static readonly int BreatheId = Shader.PropertyToID("_Breathe");
		private static readonly int MorphId = Shader.PropertyToID("_Morph");
		private static readonly int ShapeAId = Shader.PropertyToID("_ShapeA");
		private static readonly int ShapeBId = Shader.PropertyToID("_ShapeB");
		private static readonly int RippleOriginsId = Shader.PropertyToID("_RippleOrigins");
		private static readonly int RippleStrengthsId = Shader.PropertyToID("_RippleStrengths");
		private static readonly int RippleShapeId = Shader.PropertyToID("_RippleShape");
		private static readonly int RippleAmountId = Shader.PropertyToID("_RippleAmount");
		private static readonly int LevelId = Shader.PropertyToID("_Level");
		private static readonly int HissId = Shader.PropertyToID("_Hiss");
		private static readonly int SparkleId = Shader.PropertyToID("_Sparkle");
		private static readonly int ShellRotation0Id = Shader.PropertyToID("_ShellRotation0");
		private static readonly int ShellRotation1Id = Shader.PropertyToID("_ShellRotation1");
		private static readonly int ShellRotation2Id = Shader.PropertyToID("_ShellRotation2");
		private static readonly int DustRotation0Id = Shader.PropertyToID("_DustRotation0");
		private static readonly int DustRotation1Id = Shader.PropertyToID("_DustRotation1");
		private static readonly int DustRotation2Id = Shader.PropertyToID("_DustRotation2");
		private static readonly int NeutralDeepId = Shader.PropertyToID("_NeutralDeep");
		private static readonly int NeutralMidId = Shader.PropertyToID("_NeutralMid");
		private static readonly int NeutralIceId = Shader.PropertyToID("_NeutralIce");
		private static readonly int NeutralAccentId = Shader.PropertyToID("_NeutralAccent");
		private static readonly int NeutralFaintId = Shader.PropertyToID("_NeutralFaint");
		private static readonly int PoleDeepId = Shader.PropertyToID("_PoleDeep");
		private static readonly int PoleMidId = Shader.PropertyToID("_PoleMid");
		private static readonly int PoleIceId = Shader.PropertyToID("_PoleIce");
		private static readonly int PoleAccentId = Shader.PropertyToID("_PoleAccent");
		private static readonly int PoleFaintId = Shader.PropertyToID("_PoleFaint");
		private static readonly int BodyWeightId = Shader.PropertyToID("_BodyWeight");
		private static readonly int LeadWeightId = Shader.PropertyToID("_LeadWeight");
		private static readonly int WispPhaseId = Shader.PropertyToID("_WispPhase");
		private static readonly int WispLevelId = Shader.PropertyToID("_WispLevel");
		private static readonly int PokeId = Shader.PropertyToID("_Poke");
		private static readonly int PokeSpeedId = Shader.PropertyToID("_PokeSpeed");
		private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
		private static readonly int OpacityScaleId = Shader.PropertyToID("_OpacityScale");
		private static readonly int SizeMultiplierId = Shader.PropertyToID("_SizeMultiplier");
		private static readonly int MinParticlePixelsId = Shader.PropertyToID("_MinParticlePixels");
		private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
		private static readonly int WebBlendId = Shader.PropertyToID("_WebBlend");
		private static readonly int AlphaFromBrightnessId = Shader.PropertyToID("_AlphaFromBrightness");
		private static readonly int AlphaGainId = Shader.PropertyToID("_AlphaGain");
		private static readonly int AlphaSourceBlendId = Shader.PropertyToID("_AlphaSrcBlend");
		private static readonly int AlphaDestinationBlendId = Shader.PropertyToID("_AlphaDstBlend");

		private readonly Material _particles;
		private readonly Material _halos;
		private readonly Material _innerGlow;
		private readonly Material _rippleRings;
		private readonly Material[] _all;
		private readonly Material[] _rippling;
		private NarratorOrbAlphaMode? _appliedAlphaMode;

		#endregion

		#region Setup

		internal NarratorOrbMaterials(Renderer particles, Renderer halos, Renderer innerGlow, Renderer rippleRings)
		{
			_particles = CreateCopy(particles);
			_halos = CreateCopy(halos);
			_innerGlow = CreateCopy(innerGlow);
			_rippleRings = CreateCopy(rippleRings);
			_all = new[] { _particles, _halos, _innerGlow, _rippleRings };
			_rippling = new[] { _particles, _rippleRings };

			NarratorOrbPalette neutral = NarratorOrbPalette.Neutral;
			_particles.SetVector(NeutralDeepId, neutral.Deep.ToVector());
			_particles.SetVector(NeutralMidId, neutral.Mid.ToVector());
			_particles.SetVector(NeutralIceId, neutral.Ice.ToVector());
			_particles.SetVector(NeutralAccentId, neutral.Accent.ToVector());
			_particles.SetVector(NeutralFaintId, neutral.Faint.ToVector());
		}

		public void Dispose()
		{
			foreach (Material material in _all)
			{
				Object.Destroy(material);
			}
		}

		#endregion

		#region Internal

		// The shape every part follows: morph, the two shapes and the breathing scale
		internal void SetShape(float morph, NarratorOrbShape shapeA, NarratorOrbShape shapeB, float breathe)
		{
			foreach (Material material in _all)
			{
				material.SetFloat(MorphId, morph);
				material.SetFloat(ShapeAId, (float)shapeA);
				material.SetFloat(ShapeBId, (float)shapeB);
				material.SetFloat(BreatheId, breathe);
			}
		}

		internal void SetTime(float seconds)
		{
			foreach (Material material in _rippling)
			{
				material.SetFloat(OrbTimeId, seconds);
			}
		}

		internal void SetRipples(Vector4[] origins, float[] strengths, Vector4 shape, float amount)
		{
			foreach (Material material in _rippling)
			{
				material.SetVectorArray(RippleOriginsId, origins);
				material.SetFloatArray(RippleStrengthsId, strengths);
				material.SetVector(RippleShapeId, shape);
				material.SetFloat(RippleAmountId, amount);
			}
		}

		internal void SetVoice(float level, float hiss, float sparkle)
		{
			_particles.SetFloat(LevelId, level);
			_particles.SetFloat(HissId, hiss);
			_particles.SetFloat(SparkleId, sparkle);
		}

		// Each rotation as the three rows of its matrix
		internal void SetRotations(Vector4[] shellRows, Vector4[] dustRows)
		{
			_particles.SetVector(ShellRotation0Id, shellRows[0]);
			_particles.SetVector(ShellRotation1Id, shellRows[1]);
			_particles.SetVector(ShellRotation2Id, shellRows[2]);
			_particles.SetVector(DustRotation0Id, dustRows[0]);
			_particles.SetVector(DustRotation1Id, dustRows[1]);
			_particles.SetVector(DustRotation2Id, dustRows[2]);
		}

		// The look being approached, how far the body and the leaders have turned to it, and the glow shells' colours
		internal void SetPalette(NarratorOrbPalette pole, float bodyWeight, float leadWeight, NarratorOrbRgb rim,
			NarratorOrbRgb inner, NarratorOrbRgb ring)
		{
			_particles.SetVector(PoleDeepId, pole.Deep.ToVector());
			_particles.SetVector(PoleMidId, pole.Mid.ToVector());
			_particles.SetVector(PoleIceId, pole.Ice.ToVector());
			_particles.SetVector(PoleAccentId, pole.Accent.ToVector());
			_particles.SetVector(PoleFaintId, pole.Faint.ToVector());
			_particles.SetFloat(BodyWeightId, bodyWeight);
			_particles.SetFloat(LeadWeightId, leadWeight);
			_halos.SetVector(GlowColorId, rim.ToVector());
			_innerGlow.SetVector(GlowColorId, inner.ToVector());
			_rippleRings.SetVector(GlowColorId, ring.ToVector());
		}

		internal void SetWisps(Vector4 phases, Vector4 levels)
		{
			_particles.SetVector(WispPhaseId, phases);
			_particles.SetVector(WispLevelId, levels);
		}

		// Position (orb space) and amount in w, and how fast the poke moves
		internal void SetPoke(Vector4 poke, float speed)
		{
			_particles.SetVector(PokeId, poke);
			_particles.SetFloat(PokeSpeedId, speed);
		}

		// Scales the shells' baked base opacities
		internal void SetGlow(float haloOpacityScale, float innerOpacityScale)
		{
			_halos.SetFloat(OpacityScaleId, haloOpacityScale);
			_innerGlow.SetFloat(OpacityScaleId, innerOpacityScale);
		}

		internal void SetRendering(NarratorOrbRenderSettings rendering)
		{
			_particles.SetFloat(SizeMultiplierId, rendering.ParticleSizeMultiplier);
			_particles.SetFloat(MinParticlePixelsId, rendering.MinParticlePixels);
			foreach (Material material in _all)
			{
				material.SetFloat(IntensityId, rendering.Intensity);
				material.SetFloat(WebBlendId, rendering.IsWebBlendOn ? 1f : 0f);
				material.SetFloat(AlphaGainId, rendering.AlphaGain);
			}

			if (_appliedAlphaMode != rendering.AlphaMode)
			{
				ApplyAlphaMode(rendering.AlphaMode);
			}
		}

		#endregion

		#region Private

		private static Material CreateCopy(Renderer renderer)
		{
			Material copy = new(renderer.sharedMaterial);
			copy.name = $"{renderer.sharedMaterial.name} (Orb)";
			renderer.sharedMaterial = copy;
			return copy;
		}

		// Colour always adds (Blend One One with premultiplied colour); only the alpha channel's blend changes
		private void ApplyAlphaMode(NarratorOrbAlphaMode mode)
		{
			BlendMode source;
			float fromBrightness;
			switch (mode)
			{
				case NarratorOrbAlphaMode.Brightness:
					source = BlendMode.One;
					fromBrightness = 1f;
					break;
				case NarratorOrbAlphaMode.Untouched:
					source = BlendMode.Zero;
					fromBrightness = 0f;
					break;
				case NarratorOrbAlphaMode.SameAsColor:
					source = BlendMode.SrcAlpha;
					fromBrightness = 0f;
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
			}

			foreach (Material material in _all)
			{
				material.SetFloat(AlphaSourceBlendId, (float)source);
				material.SetFloat(AlphaDestinationBlendId, (float)BlendMode.One);
				material.SetFloat(AlphaFromBrightnessId, fromBrightness);
			}

			_appliedAlphaMode = mode;
		}

		#endregion
	}
}
