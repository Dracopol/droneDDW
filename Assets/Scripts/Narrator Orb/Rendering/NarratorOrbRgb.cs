using UnityEngine;

namespace DroneDDW.NarratorOrb.Rendering
{
	// A linear RGB colour in double precision, like a three.js Color
	internal readonly struct NarratorOrbRgb
	{
		#region Properties

		internal static NarratorOrbRgb White => new(1, 1, 1);

		internal double R { get; }
		internal double G { get; }
		internal double B { get; }

		#endregion

		#region Setup

		internal NarratorOrbRgb(double r, double g, double b)
		{
			R = r;
			G = g;
			B = b;
		}

		#endregion

		#region Internal

		internal NarratorOrbRgb Lerp(NarratorOrbRgb target, double alpha)
		{
			return new NarratorOrbRgb(R + (target.R - R) * alpha, G + (target.G - G) * alpha,
				B + (target.B - B) * alpha);
		}

		// For shader properties: set with SetVector, since SetColor would convert it to linear a second time
		internal Vector4 ToVector()
		{
			return new Vector4((float)R, (float)G, (float)B, 1f);
		}

		#endregion
	}
}
