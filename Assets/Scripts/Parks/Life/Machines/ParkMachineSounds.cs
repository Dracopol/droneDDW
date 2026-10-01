using System;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// The smart infrastructure's sounds: chargers, scooter docks and the service hub hum while they are shown; sensor
	/// poles ping, wayfinding totems chime and smart bins run their compactors now and then; beacons tick with each
	/// flash. Built on the efficiency ambience's 100 Hz hum, so the machines blend with it.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Machine Sounds")]
	public sealed class ParkMachineSounds : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private ParkLife _life;

		[SerializeField, Tooltip("Beacon ticks follow its flashes.")]
		private ParkMachineLights _lights;

		[SerializeField] private ParkHabitatSpot[] _chargers = Array.Empty<ParkHabitatSpot>();
		[SerializeField] private ParkHabitatSpot[] _docks = Array.Empty<ParkHabitatSpot>();
		[SerializeField] private ParkHabitatSpot[] _hubs = Array.Empty<ParkHabitatSpot>();
		[SerializeField] private ParkHabitatSpot[] _sensors = Array.Empty<ParkHabitatSpot>();
		[SerializeField] private ParkHabitatSpot[] _totems = Array.Empty<ParkHabitatSpot>();
		[SerializeField] private ParkHabitatSpot[] _bins = Array.Empty<ParkHabitatSpot>();

		[SerializeField, Tooltip("Beacons, in the same order as the machine lights' beacons.")]
		private ParkHabitatSpot[] _beacons = Array.Empty<ParkHabitatSpot>();

		#endregion

		#region Fields

		private const float FadeSeconds = 1f;
		private const int LoopPriority = 160;
		private const int OneShotPriority = 140;

		private ParkLoop[] _chargerLoops = Array.Empty<ParkLoop>();
		private ParkLoop[] _dockLoops = Array.Empty<ParkLoop>();
		private ParkLoop[] _hubLoops = Array.Empty<ParkLoop>();
		private float[] _nextPings = Array.Empty<float>();
		private float[] _nextChimes = Array.Empty<float>();
		private float[] _nextCompactions = Array.Empty<float>();

		#endregion

		#region Setup

		private void OnEnable()
		{
			if (_lights != null)
			{
				_lights.OnBeaconBlink += HandleBeaconBlink;
			}
		}

		private void OnDisable()
		{
			if (_lights != null)
			{
				_lights.OnBeaconBlink -= HandleBeaconBlink;
			}
		}

		private void Start()
		{
			ParkMachineSettings settings = _life.Settings.Machines;
			_chargerLoops = CreateLoops(_chargers, "Charger Hum", settings.ChargerHum);
			_dockLoops = CreateLoops(_docks, "Dock Hum", settings.DockHum);
			_hubLoops = CreateLoops(_hubs, "Hub Vents", settings.HubVents);
			_nextPings = Schedule(_sensors.Length, settings.SensorPingSeconds);
			_nextChimes = Schedule(_totems.Length, settings.TotemChimeSeconds);
			_nextCompactions = Schedule(_bins.Length, settings.BinSeconds);
		}

		#endregion

		#region Update

		private void Update()
		{
			ParkMachineSettings settings = _life.Settings.Machines;
			float deltaTime = Time.deltaTime;
			UpdateLoops(_chargerLoops, _chargers, deltaTime);
			UpdateLoops(_dockLoops, _docks, deltaTime);
			UpdateLoops(_hubLoops, _hubs, deltaTime);
			PlayWhenDue(_sensors, _nextPings, settings.SensorPing, settings.SensorPingSeconds, 3f);
			PlayWhenDue(_totems, _nextChimes, settings.TotemChime, settings.TotemChimeSeconds, 2f);
			PlayWhenDue(_bins, _nextCompactions, settings.BinCompactor, settings.BinSeconds, 0.6f);
		}

		#endregion

		#region Event handlers

		private void HandleBeaconBlink(int beacon)
		{
			if (beacon < _beacons.Length && _beacons[beacon].Presence > 0.5f)
			{
				_life.Sounds.Play(_life.Settings.Machines.BeaconTick, _beacons[beacon].Position, 1f, 1f, OneShotPriority);
			}
		}

		#endregion

		#region Internal

		internal void Configure(ParkLife life, ParkMachineLights lights, ParkHabitatSpot[] chargers, ParkHabitatSpot[] docks,
			ParkHabitatSpot[] hubs, ParkHabitatSpot[] sensors, ParkHabitatSpot[] totems, ParkHabitatSpot[] bins,
			ParkHabitatSpot[] beacons)
		{
			_life = life;
			_lights = lights;
			_chargers = chargers;
			_docks = docks;
			_hubs = hubs;
			_sensors = sensors;
			_totems = totems;
			_bins = bins;
			_beacons = beacons;
		}

		#endregion

		#region Private

		private ParkLoop[] CreateLoops(ParkHabitatSpot[] spots, string name, ParkLifeSound sound)
		{
			ParkLoop[] loops = new ParkLoop[spots.Length];
			for (int i = 0; i < spots.Length; i++)
			{
				loops[i] = new ParkLoop(_life, transform, $"{name} {i + 1}", sound, LoopPriority, 0f);
				loops[i].Transform.position = spots[i].Position;
			}

			return loops;
		}

		private static void UpdateLoops(ParkLoop[] loops, ParkHabitatSpot[] spots, float deltaTime)
		{
			for (int i = 0; i < loops.Length; i++)
			{
				loops[i].Update(spots[i].Presence, 1f, deltaTime, FadeSeconds);
			}
		}

		/// <summary>Plays a sound from each spot whose time has come, then sets its next time.</summary>
		private void PlayWhenDue(ParkHabitatSpot[] spots, float[] nextTimes, ParkLifeSound sound, Vector2 intervalSeconds,
			float heightMeters)
		{
			float now = Time.time;
			for (int i = 0; i < spots.Length; i++)
			{
				if (now < nextTimes[i])
				{
					continue;
				}

				nextTimes[i] = now + UnityEngine.Random.Range(intervalSeconds.x, intervalSeconds.y);
				if (spots[i].Presence > 0.5f)
				{
					_life.Sounds.Play(sound, spots[i].Position + Vector3.up * heightMeters, 1f, 1f, OneShotPriority);
				}
			}
		}

		private static float[] Schedule(int count, Vector2 intervalSeconds)
		{
			float[] times = new float[count];
			for (int i = 0; i < count; i++)
			{
				times[i] = Time.time + UnityEngine.Random.Range(0f, intervalSeconds.y);
			}

			return times;
		}

		#endregion
	}
}
