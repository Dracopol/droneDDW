using UnityEngine;

namespace DroneDDW.InteractionTests
{
	// One segment of a tracked hand skeleton, thick enough to stand in for the flesh around it
	internal readonly struct HandBone
	{
		#region Properties

		internal Transform Start { get; }
		internal Transform End { get; }
		internal float RadiusMeters { get; }

		#endregion

		#region Setup

		internal HandBone(Transform start, Transform end, float radiusMeters)
		{
			Start = start;
			End = end;
			RadiusMeters = radiusMeters;
		}

		#endregion
	}
}
