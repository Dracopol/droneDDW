using UnityEngine;

namespace DroneDDW.NarratorOrb.Rendering
{
	// The orb's baked meshes (sub-assets of this file) and what they were baked with. Saved in binary, because the
	// particle mesh alone is about 8 MB. Bake from this asset's inspector or with DroneDDW > Narrator Orb > Build
	// Orb Assets.
	[PreferBinarySerialization]
	internal sealed class NarratorOrbLayout : ScriptableObject
	{
		#region Serialized fields

		[Tooltip("Share of the preview's 37,820 particles to bake (1 = all). Lower values keep an evenly spaced "
			+ "part of every layer, in case Quest standalone needs fewer. Press Bake after changing it.")]
		[Range(0.1f, 1f)]
		[SerializeField] private float _particleCountScale = 1f;

		[HideInInspector]
		[SerializeField] private Mesh _particles;

		[HideInInspector]
		[SerializeField] private Mesh _halos;

		[HideInInspector]
		[SerializeField] private Mesh _innerGlow;

		[HideInInspector]
		[SerializeField] private Mesh _rippleRings;

		[HideInInspector]
		[SerializeField] private int _bakedParticleCount;

		[HideInInspector]
		[SerializeField] private float _bakedCountScale;

		#endregion

		#region Properties

		internal float ParticleCountScale => _particleCountScale;
		internal Mesh Particles => _particles;
		internal Mesh Halos => _halos;
		internal Mesh InnerGlow => _innerGlow;
		internal Mesh RippleRings => _rippleRings;
		internal int BakedParticleCount => _bakedParticleCount;
		internal float BakedCountScale => _bakedCountScale;

		#endregion

		#region Internal

		internal void SetMeshes(Mesh particles, Mesh halos, Mesh innerGlow, Mesh rippleRings)
		{
			_particles = particles;
			_halos = halos;
			_innerGlow = innerGlow;
			_rippleRings = rippleRings;
		}

		internal void SetBakeResult(int particleCount, float countScale)
		{
			_bakedParticleCount = particleCount;
			_bakedCountScale = countScale;
		}

		#endregion
	}
}
