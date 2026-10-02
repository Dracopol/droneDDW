using System;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>When and how the irrigation pipes spray.</summary>
	[Serializable]
	public sealed class ParkSprinklerSettings
	{
		#region Serialized fields

		[SerializeField] private ParkLifeSound _sound = new ParkLifeSound(0.35f, 1f, 25f, 0.04f);

		[SerializeField, Min(1f), Tooltip("Only pipe groups this close to the seat spray.")]
		private float _rangeMeters = 32f;

		[SerializeField, Min(1), Tooltip("How many groups may spray at the same time.")]
		private int _maxGroups = 2;

		[SerializeField, Tooltip("Seconds between the bursts of each group.")]
		private Vector2 _intervalSeconds = new Vector2(40f, 80f);

		[SerializeField, Tooltip("Seconds each burst lasts.")]
		private Vector2 _durationSeconds = new Vector2(8f, 12f);

		[SerializeField, Min(0f)] private float _dropletsPerSecondPerPipe = 40f;
		[SerializeField, Min(0.1f)] private float _sprayHeightMeters = 1.1f;

		#endregion

		#region Properties

		public ParkLifeSound Sound => _sound;
		public float RangeMeters => _rangeMeters;
		public int MaxGroups => _maxGroups;
		public Vector2 IntervalSeconds => _intervalSeconds;
		public Vector2 DurationSeconds => _durationSeconds;
		public float DropletsPerSecondPerPipe => _dropletsPerSecondPerPipe;
		public float SprayHeightMeters => _sprayHeightMeters;

		#endregion
	}
}
