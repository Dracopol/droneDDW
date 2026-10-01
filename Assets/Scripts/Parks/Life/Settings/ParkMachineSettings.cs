using System;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>The smart infrastructure: how its lights glow and blink, and what it sounds like.</summary>
	[Serializable]
	public sealed class ParkMachineSettings
	{
		#region Serialized fields

		[Header("Sounds")]
		[SerializeField] private ParkLifeSound _chargerHum = new ParkLifeSound(0.35f, 0.8f, 10f, 0.02f);
		[SerializeField] private ParkLifeSound _dockHum = new ParkLifeSound(0.25f, 0.8f, 8f, 0.02f);
		[SerializeField] private ParkLifeSound _hubVents = new ParkLifeSound(0.5f, 4f, 45f, 0.02f);
		[SerializeField] private ParkLifeSound _sensorPing = new ParkLifeSound(0.35f, 2f, 35f, 0.04f);
		[SerializeField] private ParkLifeSound _totemChime = new ParkLifeSound(0.4f, 1f, 20f, 0f);
		[SerializeField] private ParkLifeSound _binCompactor = new ParkLifeSound(0.45f, 1f, 25f, 0.05f);
		[SerializeField] private ParkLifeSound _beaconTick = new ParkLifeSound(0.2f, 1f, 15f, 0.02f);

		[SerializeField, Tooltip("Seconds between pings from each sensor pole.")]
		private Vector2 _sensorPingSeconds = new Vector2(12f, 35f);

		[SerializeField, Tooltip("Seconds between chimes from each wayfinding totem.")]
		private Vector2 _totemChimeSeconds = new Vector2(25f, 70f);

		[SerializeField, Tooltip("Seconds between compactor runs in each smart bin.")]
		private Vector2 _binSeconds = new Vector2(40f, 120f);

		[Header("Lights (emission strength)")]
		[SerializeField, Min(0.1f)] private float _beaconBlinkSeconds = 1.2f;
		[SerializeField, Min(0f)] private float _beaconGlow = 2.5f;
		[SerializeField] private Vector2 _sensorGlow = new Vector2(0.3f, 1.2f);
		[SerializeField] private Vector2 _chargerGlow = new Vector2(0.4f, 1f);
		[SerializeField, Min(0f)] private float _screenGlow = 0.9f;
		[SerializeField, Min(0f)] private float _padGlow = 0.35f;

		[SerializeField, Min(0f), Tooltip("Pad markings flash this bright as a drone takes off or lands.")]
		private float _padPulseGlow = 2.5f;

		[SerializeField, Min(0f)] private float _lampGlow = 0.4f;
		[SerializeField, Min(0f)] private float _binGlow = 0.7f;

		#endregion

		#region Properties

		public ParkLifeSound ChargerHum => _chargerHum;
		public ParkLifeSound DockHum => _dockHum;
		public ParkLifeSound HubVents => _hubVents;
		public ParkLifeSound SensorPing => _sensorPing;
		public ParkLifeSound TotemChime => _totemChime;
		public ParkLifeSound BinCompactor => _binCompactor;
		public ParkLifeSound BeaconTick => _beaconTick;
		public Vector2 SensorPingSeconds => _sensorPingSeconds;
		public Vector2 TotemChimeSeconds => _totemChimeSeconds;
		public Vector2 BinSeconds => _binSeconds;
		public float BeaconBlinkSeconds => _beaconBlinkSeconds;
		public float BeaconGlow => _beaconGlow;
		public Vector2 SensorGlow => _sensorGlow;
		public Vector2 ChargerGlow => _chargerGlow;
		public float ScreenGlow => _screenGlow;
		public float PadGlow => _padGlow;
		public float PadPulseGlow => _padPulseGlow;
		public float LampGlow => _lampGlow;
		public float BinGlow => _binGlow;

		#endregion
	}
}
