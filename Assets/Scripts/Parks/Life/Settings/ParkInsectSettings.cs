using System;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// Bees at the flowers, butterflies over the meadows, dragonflies over the reeds and crickets in the grass.
	/// </summary>
	[Serializable]
	public sealed class ParkInsectSettings
	{
		#region Serialized fields

		[SerializeField, Tooltip("Only flowers and meadows this close to the seat get insects.")]
		private float _rangeMeters = 12f;

		[SerializeField] private ParkLevelValues _bees = new ParkLevelValues(6f, 5f, 3f, 1f, 0f);
		[SerializeField] private ParkLifeSound _beeBuzz = new ParkLifeSound(0.4f, 0.3f, 6f, 0.08f);
		[SerializeField] private ParkLevelValues _butterflies = new ParkLevelValues(6f, 4f, 2f, 0f, 0f);
		[SerializeField] private ParkLevelValues _dragonflies = new ParkLevelValues(3f, 2f, 0f, 0f, 0f);
		[SerializeField] private ParkLifeSound _dragonflyWings = new ParkLifeSound(0.25f, 0.3f, 5f, 0.1f);

		[SerializeField, Tooltip("Cricket loudness in the shown meadows, at each level.")]
		private ParkLevelValues _crickets = new ParkLevelValues(1f, 0.8f, 0.4f, 0.2f, 0f);

		[SerializeField] private ParkLifeSound _cricketSong = new ParkLifeSound(0.35f, 1f, 18f, 0.04f);

		#endregion

		#region Properties

		public float RangeMeters => _rangeMeters;
		public ParkLevelValues Bees => _bees;
		public ParkLifeSound BeeBuzz => _beeBuzz;
		public ParkLevelValues Butterflies => _butterflies;
		public ParkLevelValues Dragonflies => _dragonflies;
		public ParkLifeSound DragonflyWings => _dragonflyWings;
		public ParkLevelValues Crickets => _crickets;
		public ParkLifeSound CricketSong => _cricketSong;

		#endregion
	}
}
