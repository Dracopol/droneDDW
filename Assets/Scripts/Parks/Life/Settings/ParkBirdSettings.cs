using System;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// The park's birds: songs from the trees, visible flights, flocks, the buzzard, woodpecker and city birds.
	/// </summary>
	[Serializable]
	public sealed class ParkBirdSettings
	{
		#region Serialized fields

		[Header("Songbirds")]
		[SerializeField] private ParkLifeSound _songs = new ParkLifeSound(0.6f, 2f, 45f, 0.06f);

		[SerializeField, Tooltip("Songs per minute across the park. The nature ambience already has a busy chorus, so "
			+ "these stay sparse and close.")]
		private ParkLevelValues _songsPerMinute = new ParkLevelValues(14f, 10f, 5f, 1.5f, 0f);

		[SerializeField, Tooltip("Songs come from tree canopies this far from the seat.")]
		private Vector2 _songDistanceMeters = new Vector2(8f, 40f);

		[SerializeField, Tooltip("Visible flights from tree to tree, per minute.")]
		private ParkLevelValues _flightsPerMinute = new ParkLevelValues(4f, 3f, 1.5f, 0.5f, 0f);

		[SerializeField, Tooltip("Birds hopping and pecking in the meadows around the seat.")]
		private ParkLevelValues _groundBirds = new ParkLevelValues(3f, 2f, 1f, 0f, 0f);

		[SerializeField] private ParkLifeSound _wingFlutter = new ParkLifeSound(0.35f, 1f, 15f, 0.08f);

		[Header("Sky")]
		[SerializeField] private ParkLevelValues _flocksPerMinute = new ParkLevelValues(1.2f, 0.8f, 0.4f, 0.15f, 0f);
		[SerializeField] private Vector2 _flockSize = new Vector2(5f, 9f);

		[SerializeField, Tooltip("How likely the buzzard is to be circling.")]
		private ParkLevelValues _buzzard = new ParkLevelValues(1f, 0.6f, 0f, 0f, 0f);

		[SerializeField] private ParkLifeSound _buzzardCry = new ParkLifeSound(0.5f, 10f, 120f, 0.04f);

		[Header("Woodpecker")]
		[SerializeField] private ParkLifeSound _woodpecker = new ParkLifeSound(0.5f, 3f, 50f, 0.04f);
		[SerializeField] private ParkLevelValues _drumsPerMinute = new ParkLevelValues(2f, 1.2f, 0.3f, 0f, 0f);

		[Header("City birds")]
		[SerializeField, Tooltip("Pigeons walking and pecking on the plaza around the seat.")]
		private ParkLevelValues _pigeons = new ParkLevelValues(0f, 0f, 1f, 2f, 2f);

		[SerializeField] private ParkLifeSound _pigeonCoo = new ParkLifeSound(0.45f, 2f, 30f, 0.05f);
		[SerializeField] private ParkLevelValues _coosPerMinute = new ParkLevelValues(0f, 0.2f, 1f, 1.5f, 1f);
		[SerializeField] private ParkLifeSound _crowCaw = new ParkLifeSound(0.45f, 5f, 60f, 0.05f);
		[SerializeField] private ParkLevelValues _cawsPerMinute = new ParkLevelValues(0.2f, 0.3f, 0.8f, 0.8f, 0.4f);

		#endregion

		#region Properties

		public ParkLifeSound Songs => _songs;
		public ParkLevelValues SongsPerMinute => _songsPerMinute;
		public Vector2 SongDistanceMeters => _songDistanceMeters;
		public ParkLevelValues FlightsPerMinute => _flightsPerMinute;
		public ParkLevelValues GroundBirds => _groundBirds;
		public ParkLifeSound WingFlutter => _wingFlutter;
		public ParkLevelValues FlocksPerMinute => _flocksPerMinute;
		public Vector2 FlockSize => _flockSize;
		public ParkLevelValues Buzzard => _buzzard;
		public ParkLifeSound BuzzardCry => _buzzardCry;
		public ParkLifeSound Woodpecker => _woodpecker;
		public ParkLevelValues DrumsPerMinute => _drumsPerMinute;
		public ParkLevelValues Pigeons => _pigeons;
		public ParkLifeSound PigeonCoo => _pigeonCoo;
		public ParkLevelValues CoosPerMinute => _coosPerMinute;
		public ParkLifeSound CrowCaw => _crowCaw;
		public ParkLevelValues CawsPerMinute => _cawsPerMinute;

		#endregion
	}
}
