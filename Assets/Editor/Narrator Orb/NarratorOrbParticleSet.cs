using UnityEngine;

namespace DroneDDW.NarratorOrb.Editor
{
	// The web preview's particles as generated, one entry per particle (aDir, aRad, aRand, aLayer and aSeed), plus the
	// wisps' spans and the states the two seeded generators ended on
	internal sealed class NarratorOrbParticleSet
	{
		#region Properties

		internal Vector3[] Directions { get; }
		internal float[] Radii { get; }
		internal Vector4[] Randoms { get; }
		internal int[] Layers { get; }
		internal Vector3[] Seeds { get; }
		internal double[] WispSpans { get; }
		internal long LayoutSeed { get; set; }
		internal long AttributeSeed { get; set; }
		internal int Count => Layers.Length;

		#endregion

		#region Setup

		internal NarratorOrbParticleSet(int count, int wispCount)
		{
			Directions = new Vector3[count];
			Radii = new float[count];
			Randoms = new Vector4[count];
			Layers = new int[count];
			Seeds = new Vector3[count];
			WispSpans = new double[wispCount];
		}

		#endregion
	}
}
