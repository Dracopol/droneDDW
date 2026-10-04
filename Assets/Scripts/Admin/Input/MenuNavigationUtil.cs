using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Admin
{
	// Which button a thumbstick push moves the hover to: the nearest one lying that way, preferring buttons in line
	// with the hovered one. A button counts as lying that way within 45 degrees of the push; up and down also reach
	// buttons further to the side (the history's Go back sits above the rows but far to their left). Nothing wraps
	// around: at the end of a list the hover stays.
	internal static class MenuNavigationUtil
	{
		#region Fields

		// How much sideways distance counts against a button, compared with distance along the push
		private const float AcrossWeight = 2f;

		// Buttons less than this far along the push (in the menu's pixels) are beside the hovered one, not past it
		private const float MinAlongPixels = 1f;

		#endregion

		#region Internal

		// The index of the button the hover moves to from button `from`, or -1 when none lies that way. Centres are
		// in the menu's plane, y up.
		internal static int FindNeighbor(IReadOnlyList<Vector2> centers, int from, MenuDirection direction)
		{
			Vector2 axis = StickRepeat.ToVector(direction);
			int best = FindBest(centers, from, axis, isConeOnly: true);
			bool isVertical = direction == MenuDirection.Up || direction == MenuDirection.Down;
			if (best < 0 && isVertical)
			{
				best = FindBest(centers, from, axis, isConeOnly: false);
			}

			return best;
		}

		#endregion

		#region Private

		private static int FindBest(IReadOnlyList<Vector2> centers, int from, Vector2 axis, bool isConeOnly)
		{
			int best = -1;
			float bestScore = float.MaxValue;
			for (int candidate = 0; candidate < centers.Count; candidate++)
			{
				if (candidate == from)
				{
					continue;
				}

				Vector2 offset = centers[candidate] - centers[from];
				float along = Vector2.Dot(offset, axis);
				float across = Mathf.Abs(offset.x * axis.y - offset.y * axis.x);
				if (along < MinAlongPixels || (isConeOnly && across > along))
				{
					continue;
				}

				float score = along + AcrossWeight * across;
				if (score < bestScore)
				{
					bestScore = score;
					best = candidate;
				}
			}

			return best;
		}

		#endregion
	}
}
