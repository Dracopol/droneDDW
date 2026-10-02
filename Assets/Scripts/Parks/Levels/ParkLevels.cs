using UnityEngine;

namespace DroneDDW.Parks.Levels
{
	/// <summary>
	/// The five park levels, from 0 (full nature) to 4 (full efficiency).
	/// A level mask has bit i set when an object is part of level i.
	/// </summary>
	public static class ParkLevels
	{
		#region Fields

		private const int Levels = 5;
		private const int AllLevelsMask = (1 << Levels) - 1;

		private static readonly string[] Names =
		{
			"Full nature",
			"Nature skewed",
			"Balanced",
			"Efficiency skewed",
			"Full efficiency",
		};

		#endregion

		#region Properties

		public static int Count => Levels;
		public static int AllMask => AllLevelsMask;

		#endregion

		#region Public

		public static int Clamp(int level)
		{
			return Mathf.Clamp(level, 0, Levels - 1);
		}

		public static string GetName(int level)
		{
			return Names[Clamp(level)];
		}

		/// <summary>0 at full nature, 1 at full efficiency.</summary>
		public static float ToBlend(int level)
		{
			return Clamp(level) / (float)(Levels - 1);
		}

		public static bool Contains(int levelMask, int level)
		{
			return level >= 0 && level < Levels && (levelMask & (1 << level)) != 0;
		}

		#endregion
	}
}
