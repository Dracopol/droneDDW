using System;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Settings
{
	// The rings each syllable sends over the surface (S.ripple in the web preview). Distances are angles on the sphere.
	[Serializable]
	internal sealed class NarratorOrbRippleSettings
	{
		#region Serialized fields

		[Tooltip("How fast a ring travels over the surface, in radians per second (speed)")]
		[Min(0f)]
		[SerializeField] private float _speedRadiansPerSecond = 2.4f;

		[Tooltip("How wide a ring is, in radians (width)")]
		[Min(0.001f)]
		[SerializeField] private float _widthRadians = 0.32f;

		[Tooltip("How long a ring lasts, in seconds (life)")]
		[Min(0.001f)]
		[SerializeField] private float _lifeSeconds = 1.1f;

		[Tooltip("How far a ring's start may stray from the point facing the listener (spreadFromFront)")]
		[Min(0f)]
		[SerializeField] private float _spreadFromFront = 0.55f;

		#endregion

		#region Properties

		internal float SpeedRadiansPerSecond { get => _speedRadiansPerSecond; set => _speedRadiansPerSecond = value; }
		internal float WidthRadians { get => _widthRadians; set => _widthRadians = value; }
		internal float LifeSeconds { get => _lifeSeconds; set => _lifeSeconds = value; }
		internal float SpreadFromFront { get => _spreadFromFront; set => _spreadFromFront = value; }

		#endregion
	}
}
