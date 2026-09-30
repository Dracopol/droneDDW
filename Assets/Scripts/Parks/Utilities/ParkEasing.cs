namespace DroneDDW.Parks.Utilities
{
	/// <summary>
	/// Easing curves for t in [0, 1]. Every curve starts at 0 and ends at 1, so a motion can be
	/// retargeted halfway without a jump.
	/// </summary>
	internal static class ParkEasing
	{
		#region Internal

		internal static float InCubic(float t)
		{
			return t * t * t;
		}

		internal static float OutCubic(float t)
		{
			float u = 1f - t;
			return 1f - u * u * u;
		}

		internal static float InOutCubic(float t)
		{
			if (t < 0.5f)
			{
				return 4f * t * t * t;
			}

			float u = -2f * t + 2f;
			return 1f - u * u * u * 0.5f;
		}

		/// <summary>Overshoots slightly past 1 before settling (overshoot 1.3 gives about 6%).</summary>
		internal static float OutBack(float t, float overshoot)
		{
			float u = t - 1f;
			return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
		}

		#endregion
	}
}
