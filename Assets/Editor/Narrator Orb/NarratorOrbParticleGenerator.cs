using System;
using DroneDDW.NarratorOrb.Rendering;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Editor
{
	// The web preview's particle layout, made with the same seeded generators in the same call order: 22000 shell,
	// 2600 haze, 420 dust and 4 × 3200 wisp particles. Values are kept as floats, as in the preview's Float32Arrays.
	internal static class NarratorOrbParticleGenerator
	{
		#region Fields

		internal const int ShellCount = 22000;
		internal const int HazeCount = 2600;
		internal const int DustCount = 420;
		internal const int WispCount = 4;
		internal const int WispParticleCount = 3200;
		internal const int TotalCount = ShellCount + HazeCount + DustCount + WispCount * WispParticleCount;

		#endregion

		#region Internal

		internal static NarratorOrbParticleSet Generate()
		{
			LehmerRandom random = new(LehmerRandom.LayoutSeed, LehmerRandom.LayoutMultiplier);
			LehmerRandom attributes = new(LehmerRandom.AttributeSeed, LehmerRandom.AttributeMultiplier);
			NarratorOrbParticleSet set = new(TotalCount, WispCount);
			int index = 0;

			// Layer 0: shell, dense particles on the surface
			for (int i = 0; i < ShellCount; i++)
			{
				Vector3 direction = RandomDirection(random);
				double radius = 1 + Gauss(random) * 0.01;
				double mix = random.Next();
				double ice = random.Next();
				Add(set, ref index, direction, radius,
					new Vector4((float)mix, (float)ice, (float)attributes.Next(), (float)attributes.Next()), 0,
					Vector3.zero);
			}

			// Layer 1: interior haze
			for (int i = 0; i < HazeCount; i++)
			{
				Vector3 direction = RandomDirection(random);
				double radius = Math.Cbrt(random.Next()) * 0.95;
				double mix = random.Next();
				Add(set, ref index, direction, radius,
					new Vector4((float)mix, 1f, (float)attributes.Next(), (float)attributes.Next()), 1, Vector3.zero);
			}

			// Layer 2: outer dust
			for (int i = 0; i < DustCount; i++)
			{
				Vector3 direction = RandomDirection(random);
				double radius = 1.03 + Math.Pow(random.Next(), 2.5) * 0.18;
				double mix = random.Next();
				Add(set, ref index, direction, radius,
					new Vector4((float)mix, 1f, (float)attributes.Next(), (float)attributes.Next()), 2, Vector3.zero);
			}

			// Layers 3 to 6: the four wisps, particles streaming along great-circle paths
			for (int wisp = 0; wisp < WispCount; wisp++)
			{
				for (int i = 0; i < WispParticleCount; i++)
				{
					double along = random.Next();
					double across = Gauss(random);
					double height = Math.Abs(Gauss(random));

					// Drawn like the other layers' attributes, though the wisps only use the seeds
					float mix = (float)attributes.Next();
					float flash = (float)attributes.Next();
					float spare = (float)attributes.Next();
					Add(set, ref index, Vector3.up, 1, new Vector4(mix, 1f, flash, spare), 3 + wisp,
						new Vector3((float)along, (float)across, (float)height));
				}

				set.WispSpans[wisp] = 2.3 + random.Next() * 0.6;
			}

			set.LayoutSeed = random.Seed;
			set.AttributeSeed = attributes.Seed;
			return set;
		}

		#endregion

		#region Private

		private static void Add(NarratorOrbParticleSet set, ref int index, Vector3 direction, double radius,
			Vector4 random, int layer, Vector3 seed)
		{
			set.Directions[index] = direction;
			set.Radii[index] = (float)radius;
			set.Randoms[index] = random;
			set.Layers[index] = layer;
			set.Seeds[index] = seed;
			index++;
		}

		// randDir: a direction spread evenly over the sphere
		private static Vector3 RandomDirection(LehmerRandom random)
		{
			double u = random.Next() * 2 - 1;
			double t = random.Next() * Math.PI * 2;
			double s = Math.Sqrt(1 - u * u);
			return new Vector3((float)(s * Math.Cos(t)), (float)u, (float)(s * Math.Sin(t)));
		}

		// gauss: roughly normal, from three draws
		private static double Gauss(LehmerRandom random)
		{
			return (random.Next() + random.Next() + random.Next() - 1.5) / 1.5;
		}

		#endregion
	}
}
