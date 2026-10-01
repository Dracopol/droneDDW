using System;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>Life and water sounds on the ponds: ducks, frogs, fish, lapping, the fountain and the pool.</summary>
	[Serializable]
	public sealed class ParkPondSettings
	{
		#region Serialized fields

		[Header("Ducks")]
		[SerializeField, Tooltip("Ducks on the pond shown at each level (the formal pond at level 2 is small).")]
		private ParkLevelValues _ducks = new ParkLevelValues(3f, 3f, 1f, 0f, 0f);

		[SerializeField] private ParkLifeSound _quacks = new ParkLifeSound(0.55f, 2f, 45f, 0.06f);
		[SerializeField, Min(0f)] private float _quacksPerMinutePerDuck = 2f;
		[SerializeField] private ParkLifeSound _splash = new ParkLifeSound(0.5f, 2f, 40f, 0.08f);
		[SerializeField, Min(0.05f)] private float _duckSpeedMetersPerSecond = 0.35f;

		[Header("Frogs and fish")]
		[SerializeField] private ParkLifeSound _frogCroaks = new ParkLifeSound(0.5f, 1.5f, 35f, 0.08f);
		[SerializeField] private ParkLevelValues _croaksPerMinute = new ParkLevelValues(10f, 6f, 0f, 0f, 0f);
		[SerializeField] private ParkLifeSound _fishPlop = new ParkLifeSound(0.45f, 1.5f, 30f, 0.1f);
		[SerializeField] private ParkLevelValues _plopsPerMinute = new ParkLevelValues(3f, 2f, 1f, 0f, 0f);

		[Header("Water")]
		[SerializeField] private ParkLifeSound _lapping = new ParkLifeSound(0.35f, 2f, 25f, 0.04f);
		[SerializeField] private ParkLifeSound _fountain = new ParkLifeSound(0.55f, 3f, 45f, 0.02f);
		[SerializeField] private ParkLifeSound _trickle = new ParkLifeSound(0.3f, 2f, 20f, 0.03f);

		#endregion

		#region Properties

		public ParkLevelValues Ducks => _ducks;
		public ParkLifeSound Quacks => _quacks;
		public float QuacksPerMinutePerDuck => _quacksPerMinutePerDuck;
		public ParkLifeSound Splash => _splash;
		public float DuckSpeedMetersPerSecond => _duckSpeedMetersPerSecond;
		public ParkLifeSound FrogCroaks => _frogCroaks;
		public ParkLevelValues CroaksPerMinute => _croaksPerMinute;
		public ParkLifeSound FishPlop => _fishPlop;
		public ParkLevelValues PlopsPerMinute => _plopsPerMinute;
		public ParkLifeSound Lapping => _lapping;
		public ParkLifeSound Fountain => _fountain;
		public ParkLifeSound Trickle => _trickle;

		#endregion
	}
}
