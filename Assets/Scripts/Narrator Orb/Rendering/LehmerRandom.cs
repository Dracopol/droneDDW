namespace DroneDDW.NarratorOrb.Rendering
{
	// The web preview's seeded generators: seed = seed × multiplier mod (2^31 - 1), handed out divided by 2^31 - 1.
	// rnd (multiplier 16807) starts at 7, rnd2 (multiplier 48271) at 1013. Exact in 64-bit integers, as in JavaScript.
	internal sealed class LehmerRandom
	{
		#region Fields

		internal const long LayoutMultiplier = 16807;
		internal const long AttributeMultiplier = 48271;
		internal const long LayoutSeed = 7;
		internal const long AttributeSeed = 1013;

		private const long Modulus = 2147483647;

		private readonly long _multiplier;
		private long _seed;

		#endregion

		#region Properties

		internal long Seed => _seed;

		#endregion

		#region Setup

		internal LehmerRandom(long seed, long multiplier)
		{
			_seed = seed;
			_multiplier = multiplier;
		}

		#endregion

		#region Internal

		internal double Next()
		{
			_seed = _seed * _multiplier % Modulus;
			return (double)_seed / Modulus;
		}

		#endregion
	}
}
