using System;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>How the park's drones fly and sound.</summary>
	[Serializable]
	public sealed class ParkDroneSettings
	{
		#region Serialized fields

		[SerializeField, Tooltip("The rotor loop. Its pitch and volume rise with speed and climb.")]
		private ParkLifeSound _sound = new ParkLifeSound(0.55f, 3f, 90f, 0.04f);

		[SerializeField, Min(0.1f)] private float _cruiseSpeedMetersPerSecond = 5f;
		[SerializeField, Min(0.1f)] private float _climbSpeedMetersPerSecond = 2f;

		[SerializeField, Min(1f), Tooltip("Height a drone climbs to when it leaves a pad.")]
		private float _cruiseHeightMeters = 11f;

		[SerializeField, Tooltip("Seconds a drone rests on a pad between trips.")]
		private Vector2 _padWaitSeconds = new Vector2(6f, 16f);

		[SerializeField, Tooltip("Seconds a drone hovers where it delivers.")]
		private Vector2 _deliveryHoverSeconds = new Vector2(2f, 5f);

		[SerializeField, Min(0.1f), Tooltip("Seconds the rotors take to spin up before take-off, and down after landing.")]
		private float _spinSeconds = 1.5f;

		[SerializeField, Min(0f)] private float _bobMeters = 0.06f;
		[SerializeField, Min(0f)] private float _bobHertz = 0.7f;

		[SerializeField, Range(0f, 45f), Tooltip("Lean into the direction of travel at cruise speed.")]
		private float _tiltDegrees = 12f;

		[SerializeField, Range(0.5f, 2f), Tooltip("Rotor pitch while hovering, and at cruise speed.")]
		private Vector2 _pitchRange = new Vector2(0.92f, 1.1f);

		[SerializeField, Range(0f, 1f), Tooltip("Rotor volume while hovering, relative to cruising.")]
		private float _hoverVolume = 0.7f;

		#endregion

		#region Properties

		public ParkLifeSound Sound => _sound;
		public float CruiseSpeedMetersPerSecond => _cruiseSpeedMetersPerSecond;
		public float ClimbSpeedMetersPerSecond => _climbSpeedMetersPerSecond;
		public float CruiseHeightMeters => _cruiseHeightMeters;
		public Vector2 PadWaitSeconds => _padWaitSeconds;
		public Vector2 DeliveryHoverSeconds => _deliveryHoverSeconds;
		public float SpinSeconds => _spinSeconds;
		public float BobMeters => _bobMeters;
		public float BobHertz => _bobHertz;
		public float TiltDegrees => _tiltDegrees;
		public Vector2 PitchRange => _pitchRange;
		public float HoverVolume => _hoverVolume;

		#endregion
	}
}
