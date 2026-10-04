using DroneDDW.NarratorOrb.Editor;
using NUnit.Framework;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Tests
{
	// The baked layout against values printed by the web preview's own layout code: same seeds, same call order
	public sealed class ParticleLayoutTests
	{
		#region Fields

		private const float Tolerance = 1e-6f;

		#endregion

		#region Private

		private static void AssertParticle(NarratorOrbParticleSet particles, int index, Vector3 direction, float radius,
			Vector4 random, Vector3 seed)
		{
			Assert.That(Vector3.Distance(particles.Directions[index], direction), Is.LessThan(Tolerance),
				$"direction of #{index}");
			Assert.That(particles.Radii[index], Is.EqualTo(radius).Within(Tolerance), $"radius of #{index}");
			Assert.That(Vector4.Distance(particles.Randoms[index], random), Is.LessThan(Tolerance),
				$"random values of #{index}");
			Assert.That(Vector3.Distance(particles.Seeds[index], seed), Is.LessThan(Tolerance), $"seeds of #{index}");
		}

		#endregion

		#region Tests

		[Test]
		public void LayerCountsMatchTheReference()
		{
			NarratorOrbParticleSet particles = NarratorOrbParticleGenerator.Generate();
			Assert.That(particles.Count, Is.EqualTo(37820));
			Assert.That(particles.Layers[21999], Is.EqualTo(0));
			Assert.That(particles.Layers[22000], Is.EqualTo(1));
			Assert.That(particles.Layers[24599], Is.EqualTo(1));
			Assert.That(particles.Layers[24600], Is.EqualTo(2));
			Assert.That(particles.Layers[25019], Is.EqualTo(2));
			Assert.That(particles.Layers[25020], Is.EqualTo(3));
			Assert.That(particles.Layers[28220], Is.EqualTo(4));
			Assert.That(particles.Layers[37819], Is.EqualTo(6));
		}

		[Test]
		public void GeneratorsEndWhereTheReferenceEnds()
		{
			NarratorOrbParticleSet particles = NarratorOrbParticleGenerator.Generate();
			Assert.That(particles.LayoutSeed, Is.EqualTo(187402010));
			Assert.That(particles.AttributeSeed, Is.EqualTo(1616505683));
			Assert.That(particles.AttributeSeed, Is.EqualTo(NarratorOrbRipples.JitterSeed));
		}

		// The shader holds these spans as constants (WispSpans in Narrator Orb Particles.shader)
		[Test]
		public void WispSpansMatchTheReference()
		{
			NarratorOrbParticleSet particles = NarratorOrbParticleGenerator.Generate();
			Assert.That(particles.WispSpans[0], Is.EqualTo(2.46178409).Within(1e-8));
			Assert.That(particles.WispSpans[1], Is.EqualTo(2.75801714).Within(1e-8));
			Assert.That(particles.WispSpans[2], Is.EqualTo(2.53889972).Within(1e-8));
			Assert.That(particles.WispSpans[3], Is.EqualTo(2.35235952).Within(1e-8));
		}

		[Test]
		public void SampledParticlesMatchTheReference()
		{
			NarratorOrbParticleSet particles = NarratorOrbParticleGenerator.Generate();
			AssertParticle(particles, 0, new Vector3(0.0130060008f, -0.999890447f, -0.00706896558f), 0.998194396f,
				new Vector4(0.532714307f, 0.329312325f, 0.0227701496f, 0.137870982f), Vector3.zero);
			AssertParticle(particles, 1, new Vector3(0.0275335573f, 0.504106045f, -0.863202751f), 1.00242186f,
				new Vector4(0.816757441f, 0.242004767f, 0.170189187f, 0.202223673f), Vector3.zero);
			AssertParticle(particles, 21999, new Vector3(0.645980000f, -0.705114841f, 0.292443037f), 0.996027589f,
				new Vector4(0.133166373f, 0.127330035f, 0.290174246f, 0.00102719245f), Vector3.zero);
			AssertParticle(particles, 22000, new Vector3(-0.336080670f, -0.928312063f, 0.159017310f), 0.668308258f,
				new Vector4(0.265687287f, 1f, 0.583607018f, 0.293884188f), Vector3.zero);
			AssertParticle(particles, 24599, new Vector3(0.247399211f, 0.897799134f, 0.364349186f), 0.506131172f,
				new Vector4(0.608079433f, 1f, 0.507738888f, 0.0643363521f), Vector3.zero);
			AssertParticle(particles, 24600, new Vector3(0.178283095f, 0.981443465f, 0.0705967322f), 1.06906366f,
				new Vector4(0.00234603928f, 1f, 0.579893947f, 0.0594528988f), Vector3.zero);
			AssertParticle(particles, 25019, new Vector3(0.997861803f, 0.0325470380f, 0.0566795357f), 1.12513101f,
				new Vector4(0.967164516f, 1f, 0.766209662f, 0.707958400f), Vector3.zero);
			AssertParticle(particles, 25020, Vector3.up, 1f,
				new Vector4(0.859886944f, 1f, 0.601628423f, 0.204595953f),
				new Vector3(0.134419024f, 0.194235742f, 0.190498337f));
			AssertParticle(particles, 28219, Vector3.up, 1f,
				new Vector4(0.591523290f, 1f, 0.421021670f, 0.137316748f),
				new Vector3(0.140725076f, -0.498880655f, 0.0636524409f));
			AssertParticle(particles, 28220, Vector3.up, 1f,
				new Vector4(0.416567624f, 1f, 0.135701463f, 0.445283026f),
				new Vector3(0.841870368f, -0.464465499f, 0.203717709f));
			AssertParticle(particles, 37819, Vector3.up, 1f,
				new Vector4(0.226820737f, 1f, 0.864054024f, 0.752744138f),
				new Vector3(0.207405478f, 0.0275554862f, 0.449862242f));
		}

		[Test]
		public void CountScaleKeepsAnEvenShareOfEachLayer()
		{
			NarratorOrbParticleSet particles = NarratorOrbParticleGenerator.Generate();
			Mesh mesh = new();
			try
			{
				Assert.That(NarratorOrbMeshBaker.BakeParticles(mesh, particles, 1f), Is.EqualTo(37820));
				Assert.That(mesh.vertexCount, Is.EqualTo(37820 * 4));
				Assert.That(NarratorOrbMeshBaker.BakeParticles(mesh, particles, 0.5f), Is.EqualTo(18910));
			}
			finally
			{
				Object.DestroyImmediate(mesh);
			}
		}

		#endregion
	}
}
