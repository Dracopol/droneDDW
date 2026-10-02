using System;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>The wind: a steady breeze, gust fronts rolling across the park, how plants bend, leaf rustle.</summary>
	[Serializable]
	public sealed class ParkWindSettings
	{
		#region Serialized fields

		[SerializeField, Range(0f, 360f), Tooltip("Compass direction the wind blows towards (0 = north, the pond).")]
		private float _directionDegrees = 60f;

		[SerializeField, Range(0f, 1f)] private float _breeze = 0.25f;
		[SerializeField, Range(0f, 2f)] private float _gustStrength = 0.9f;

		[SerializeField, Tooltip("Seconds between gust fronts.")]
		private Vector2 _gustIntervalSeconds = new Vector2(6f, 14f);

		[SerializeField, Min(0.5f)] private float _gustSpeedMetersPerSecond = 7f;
		[SerializeField, Min(1f)] private float _gustWidthMeters = 18f;

		[Header("Plants")]
		[SerializeField, Min(0f), Tooltip("How far a 7 m tree top moves in a full gust.")]
		private float _bendMeters = 0.35f;

		[SerializeField, Min(0f)] private float _swayHertz = 0.35f;
		[SerializeField, Range(0f, 1f)] private float _flutter = 0.4f;

		[Header("Rustle")]
		[SerializeField] private ParkLifeSound _rustle = new ParkLifeSound(0.4f, 2f, 25f, 0.05f);

		[SerializeField, Min(0), Tooltip("Rustle loops in the nearest big trees.")]
		private int _rustleTrees = 4;

		#endregion

		#region Properties

		public float DirectionDegrees => _directionDegrees;
		public float Breeze => _breeze;
		public float GustStrength => _gustStrength;
		public Vector2 GustIntervalSeconds => _gustIntervalSeconds;
		public float GustSpeedMetersPerSecond => _gustSpeedMetersPerSecond;
		public float GustWidthMeters => _gustWidthMeters;
		public float BendMeters => _bendMeters;
		public float SwayHertz => _swayHertz;
		public float Flutter => _flutter;
		public ParkLifeSound Rustle => _rustle;
		public int RustleTrees => _rustleTrees;

		#endregion
	}
}
