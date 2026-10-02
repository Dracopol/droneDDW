using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// Birds high over the park: small flocks crossing the sky in loose formation, and a buzzard circling high above the
	/// nature levels, crying now and then. When the park turns to efficiency, the buzzard spirals out and away.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Sky Birds")]
	public sealed class ParkSkyBirds : MonoBehaviour
	{
		#region Entities

		private sealed class FlockBird
		{
			public ParkCreature Creature;
			public Vector3 Offset;
			public float Phase;
		}

		#endregion

		#region Serialized fields

		[SerializeField] private ParkLife _life;

		[SerializeField, Tooltip("Birds a flock is made of, mixed.")]
		private ParkCreature[] _flockPrefabs = Array.Empty<ParkCreature>();

		[SerializeField] private ParkCreature _buzzardPrefab;

		[SerializeField, Tooltip("The buzzard circles around here.")]
		private Vector3 _center = new Vector3(0f, 0f, 10f);

		#endregion

		#region Fields

		private const int FlockBirds = 9;
		private const float FlockSpeed = 9f;
		private const float CrossingMeters = 75f;
		private const float BuzzardSpeed = 8f;
		private const float BuzzardDriftMeters = 25f;
		private const float BuzzardStartShare = 0.6f;
		private const float BuzzardStopShare = 0.35f;
		private const float LeaveRadiusMeters = 120f;
		private const int CallPriority = 100;

		private readonly List<FlockBird> _flock = new List<FlockBird>();
		private float _flockCredit;
		private bool _isFlockFlying;
		private int _flockCount;
		private Vector3 _flockFrom;
		private Vector3 _flockTo;
		private float _flockStart;
		private float _flockSeconds;

		private ParkCreature _buzzard;
		private bool _isBuzzardFlying;
		private bool _isBuzzardLeaving;
		private Vector3 _circleCenter;
		private float _radius;
		private float _height;
		private float _angle;
		private float _nextCry;
		private float _nextFlaps;
		private float _flapStart = float.NegativeInfinity;

		#endregion

		#region Setup

		private void Start()
		{
			for (int i = 0; i < FlockBirds; i++)
			{
				ParkCreature creature = Instantiate(_flockPrefabs[i % _flockPrefabs.Length], transform);
				creature.gameObject.SetActive(false);
				_flock.Add(new FlockBird { Creature = creature });
			}

			_buzzard = Instantiate(_buzzardPrefab, transform);
			_buzzard.gameObject.SetActive(false);
		}

		#endregion

		#region Update

		private void Update()
		{
			ParkBirdSettings settings = _life.Settings.Birds;
			float now = Time.time, deltaTime = Time.deltaTime;
			Flocks(settings, now, deltaTime);
			Buzzard(settings, now, deltaTime);
		}

		#endregion

		#region Internal

		internal void Configure(ParkLife life, ParkCreature[] flockPrefabs, ParkCreature buzzardPrefab, Vector3 center)
		{
			_life = life;
			_flockPrefabs = flockPrefabs;
			_buzzardPrefab = buzzardPrefab;
			_center = center;
		}

		#endregion

		#region Private

		private void Flocks(ParkBirdSettings settings, float now, float deltaTime)
		{
			_flockCredit = Mathf.Min(2f, _flockCredit + _life.Activity(settings.FlocksPerMinute) / 60f * deltaTime);
			if (!_isFlockFlying && _flockCredit >= 1f)
			{
				_flockCredit -= 1f;
				StartFlock(settings, now);
			}

			if (!_isFlockFlying)
			{
				return;
			}

			float t = (now - _flockStart) / _flockSeconds;
			if (t >= 1f)
			{
				_isFlockFlying = false;
				foreach (FlockBird bird in _flock)
				{
					bird.Creature.gameObject.SetActive(false);
				}

				return;
			}

			Quaternion facing = ParkFlight.Facing(_flockTo - _flockFrom);
			Vector3 center = Vector3.Lerp(_flockFrom, _flockTo, t);
			for (int i = 0; i < _flockCount; i++)
			{
				FlockBird bird = _flock[i];
				Vector3 sway = Vector3.up * (0.6f * Mathf.Sin(2f * Mathf.PI * 0.35f * now + bird.Phase * 6f));
				bird.Creature.Root.SetPositionAndRotation(center + facing * bird.Offset + sway, facing);
				bird.Creature.SetWings(ParkFlight.FlapGlide(now, bird.Phase, 12f, 50f, 0.6f, 1f), 0f);
			}
		}

		/// <summary>A chord across the sky from one side of the park to the other, a little off straight across.</summary>
		private void StartFlock(ParkBirdSettings settings, float now)
		{
			float angle = UnityEngine.Random.Range(0f, 360f);
			float height = UnityEngine.Random.Range(24f, 36f);
			Vector3 from = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * CrossingMeters;
			float across = angle + 180f + UnityEngine.Random.Range(-30f, 30f);
			Vector3 to = Quaternion.Euler(0f, across, 0f) * Vector3.forward * CrossingMeters;
			_flockFrom = from + Vector3.up * height;
			_flockTo = to + Vector3.up * height;
			_flockStart = now;
			_flockSeconds = Vector3.Distance(_flockFrom, _flockTo) / FlockSpeed;
			int size = Mathf.RoundToInt(UnityEngine.Random.Range(settings.FlockSize.x, settings.FlockSize.y));
			_flockCount = Mathf.Clamp(size, 1, _flock.Count);
			_isFlockFlying = true;
			for (int i = 0; i < _flock.Count; i++)
			{
				FlockBird bird = _flock[i];
				bool isFlying = i < _flockCount;
				bird.Creature.gameObject.SetActive(isFlying);
				bird.Offset = new Vector3(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(-1.5f, 1.5f),
					UnityEngine.Random.Range(-4f, 4f));
				bird.Phase = UnityEngine.Random.value;
			}
		}

		/// <summary>Circles high over the park while the level suits it; spirals away when it no longer does.</summary>
		private void Buzzard(ParkBirdSettings settings, float now, float deltaTime)
		{
			float wanted = _life.Activity(settings.Buzzard);
			if (!_isBuzzardFlying && wanted >= BuzzardStartShare)
			{
				_isBuzzardFlying = true;
				_isBuzzardLeaving = false;
				_circleCenter = ParkFlight.Around(_center, 15f);
				_radius = UnityEngine.Random.Range(25f, 35f);
				_height = UnityEngine.Random.Range(45f, 55f);
				_angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
				_nextCry = now + UnityEngine.Random.Range(5f, 20f);
				_nextFlaps = now + UnityEngine.Random.Range(8f, 20f);
				_buzzard.gameObject.SetActive(true);
			}
			else if (_isBuzzardFlying && !_isBuzzardLeaving && wanted < BuzzardStopShare)
			{
				_isBuzzardLeaving = true;
			}

			if (!_isBuzzardFlying)
			{
				return;
			}

			if (_isBuzzardLeaving)
			{
				_radius += 6f * deltaTime;
				_height += 2f * deltaTime;
				if (_radius > LeaveRadiusMeters)
				{
					_isBuzzardFlying = false;
					_buzzard.gameObject.SetActive(false);
					return;
				}
			}
			else
			{
				Vector3 drift = ParkFlight.Around(Vector3.zero, 1f) * deltaTime;
				_circleCenter = _center + Vector3.ClampMagnitude(_circleCenter + drift - _center, BuzzardDriftMeters);
				if (now >= _nextCry)
				{
					_life.Sounds.Play(settings.BuzzardCry, _buzzard.Root.position, 1f, 1f, CallPriority);
					_nextCry = now + UnityEngine.Random.Range(15f, 40f);
				}
			}

			_angle += BuzzardSpeed / _radius * deltaTime;
			Vector3 around = new Vector3(Mathf.Sin(_angle), 0f, Mathf.Cos(_angle)) * _radius;
			Vector3 position = _circleCenter + around + Vector3.up * _height;
			Vector3 heading = new Vector3(Mathf.Cos(_angle), 0f, -Mathf.Sin(_angle));
			_buzzard.Root.SetPositionAndRotation(position, ParkFlight.Facing(heading, 0f, -15f));

			// Mostly gliding; a few slow wingbeats now and then
			if (now >= _nextFlaps)
			{
				_flapStart = now;
				_nextFlaps = now + UnityEngine.Random.Range(12f, 25f);
			}

			float flapping = now - _flapStart;
			_buzzard.SetWings(flapping < 1f ? 25f * Mathf.Sin(2f * Mathf.PI * 3f * flapping) : 6f, 0f);
		}

		#endregion
	}
}
