using UnityEngine;

namespace DroneDDW.Parks.Table
{
	/// <summary>One bone of a tracked hand as a capsule: its two joints in world space and its thickness.</summary>
	internal readonly struct ParkHandBone
	{
		#region Properties

		public Vector3 Start { get; }
		public Vector3 End { get; }
		public float RadiusMeters { get; }

		#endregion

		#region Setup

		public ParkHandBone(Vector3 start, Vector3 end, float radiusMeters)
		{
			Start = start;
			End = end;
			RadiusMeters = radiusMeters;
		}

		#endregion
	}
}
