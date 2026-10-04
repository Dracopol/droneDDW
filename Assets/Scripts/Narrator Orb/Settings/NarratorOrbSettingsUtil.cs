using System;

namespace DroneDDW.NarratorOrb.Settings
{
	internal static class NarratorOrbSettingsUtil
	{
		#region Fields

		// Floats beyond this can't go through decimal
		private const float DecimalLimit = 1e20f;

		#endregion

		#region Internal

		// The values at the expected length: extra values are dropped, missing ones get their default
		internal static float[] FitLength(float[] values, float[] defaults)
		{
			if (values != null && values.Length == defaults.Length)
			{
				return values;
			}

			float[] fitted = (float[])defaults.Clone();
			if (values != null)
			{
				Array.Copy(values, fitted, Math.Min(values.Length, fitted.Length));
			}

			return fitted;
		}

		// The double the float was typed as: 0.1f becomes exactly 0.1, as the web preview has it
		internal static double ToExactDouble(float value)
		{
			if (float.IsNaN(value) || Math.Abs(value) > DecimalLimit)
			{
				return value;
			}

			return (double)(decimal)value;
		}

		#endregion
	}
}
