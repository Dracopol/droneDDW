using UnityEngine;

namespace DroneDDW.Parks.Table
{
	/// <summary>
	/// One sphere sampled along a hand bone for the push: where it is, in the world and in the slider's space, its
	/// radius with the touch margin, and whether it touches the handle.
	/// </summary>
	internal readonly struct ParkHandSphere
	{
		#region Properties

		public Vector3 WorldPosition { get; }
		public Vector3 SliderPosition { get; }
		public float RadiusMeters { get; }
		public bool IsTouching { get; }

		#endregion

		#region Setup

		public ParkHandSphere(Vector3 worldPosition, Vector3 sliderPosition, float radiusMeters, bool isTouching)
		{
			WorldPosition = worldPosition;
			SliderPosition = sliderPosition;
			RadiusMeters = radiusMeters;
			IsTouching = isTouching;
		}

		#endregion
	}
}
