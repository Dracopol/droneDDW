using System;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>How the delivery robots and the mowers drive and sound.</summary>
	[Serializable]
	public sealed class ParkRobotSettings
	{
		#region Serialized fields

		[Header("Delivery robots")]
		[SerializeField, Tooltip("The motor loop. Its pitch follows the speed.")]
		private ParkLifeSound _motor = new ParkLifeSound(0.4f, 1f, 25f, 0.03f);

		[SerializeField, Tooltip("Played when a robot sets off or arrives.")]
		private ParkLifeSound _beeps = new ParkLifeSound(0.5f, 1f, 20f, 0.02f);

		[SerializeField, Min(0.1f)] private float _speedMetersPerSecond = 1.1f;

		[SerializeField, Tooltip("Seconds a robot waits at each end of its route.")]
		private Vector2 _stopSeconds = new Vector2(3f, 8f);

		[SerializeField, Min(1f)] private float _turnDegreesPerSecond = 120f;

		[Header("Mowers")]
		[SerializeField] private ParkLifeSound _mower = new ParkLifeSound(0.45f, 1.5f, 35f, 0.03f);
		[SerializeField, Min(0.05f)] private float _mowerSpeedMetersPerSecond = 0.45f;
		[SerializeField, Min(0.1f)] private float _stripeSpacingMeters = 0.7f;
		[SerializeField, Min(1f)] private float _mowerTurnDegreesPerSecond = 60f;

		#endregion

		#region Properties

		public ParkLifeSound Motor => _motor;
		public ParkLifeSound Beeps => _beeps;
		public float SpeedMetersPerSecond => _speedMetersPerSecond;
		public Vector2 StopSeconds => _stopSeconds;
		public float TurnDegreesPerSecond => _turnDegreesPerSecond;
		public ParkLifeSound Mower => _mower;
		public float MowerSpeedMetersPerSecond => _mowerSpeedMetersPerSecond;
		public float StripeSpacingMeters => _stripeSpacingMeters;
		public float MowerTurnDegreesPerSecond => _mowerTurnDegreesPerSecond;

		#endregion
	}
}
