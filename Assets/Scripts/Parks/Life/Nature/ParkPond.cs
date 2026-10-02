using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// Life on and around the ponds. Ducks paddle about the pond the level shows, dabbling and quacking, leaving faint
	/// wakes; when their pond goes they splash off and fly away, and when one returns they fly in and land. Frogs sit on
	/// the natural pond's edge and jump in with a plop, croaking from the reeds; fish plop and now and then leap. Water
	/// laps at the natural pond's edges, the formal pond's fountain splashes and the basin trickles. Everything that
	/// touches the water starts a ripple on the water shader.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Pond")]
	public sealed class ParkPond : MonoBehaviour
	{
		#region Entities

		private enum DuckMode
		{
			Hidden,
			Flying,
			Swimming,
			Dabbling,
		}

		private enum FrogMode
		{
			Hidden,
			Sitting,
			Jumping,
			Under,
		}

		private sealed class Duck
		{
			public ParkCreature Creature;
			public DuckMode Mode;
			public int Pond;
			public bool IsLanding;
			public Vector3 Position;
			public Vector3 Target;
			public Vector3 From;
			public Vector3 To;
			public float StartTime;
			public float Duration;
			public float Yaw;
			public float Phase;
			public float NextQuack;
			public float NextWake;
			public float NextDabble;
			public float DabbleEnd;
		}

		private sealed class Frog
		{
			public ParkCreature Creature;
			public FrogMode Mode;
			public Vector3 From;
			public Vector3 To;
			public float StartTime;
			public float Timer;
			public float Yaw;
			public int Bank = -1;
		}

		#endregion

		#region Serialized fields

		[SerializeField] private ParkLife _life;
		[SerializeField] private ParkWaterSurface _water;

		[SerializeField, Tooltip("Ponds ducks swim on, natural first: the water surface's centre, and its pond object.")]
		private ParkHabitatSpot[] _ponds = Array.Empty<ParkHabitatSpot>();

		[SerializeField, Tooltip("Half-width and half-length of the water ducks may swim on, for each pond.")]
		private Vector2[] _pondRadii = Array.Empty<Vector2>();

		[SerializeField, Tooltip("Reed clumps, where croaks come from.")]
		private ParkHabitatSpot[] _reeds = Array.Empty<ParkHabitatSpot>();

		[SerializeField, Tooltip("Points on the natural pond's bank where frogs sit, tied to the bank's object.")]
		private ParkHabitatSpot[] _frogEdges = Array.Empty<ParkHabitatSpot>();

		[SerializeField, Tooltip("Points along the natural pond's edge where water laps.")]
		private ParkHabitatSpot[] _lapping = Array.Empty<ParkHabitatSpot>();

		[SerializeField] private ParkHabitatSpot _fountain;

		[SerializeField, Tooltip("How far the fountain's row of jets reaches either side of its centre, along X.")]
		private float _fountainHalfWidthMeters = 5f;

		[SerializeField] private ParkHabitatSpot _trickle;
		[SerializeField] private ParkCreature[] _duckPrefabs = Array.Empty<ParkCreature>();
		[SerializeField] private ParkCreature _frogPrefab;
		[SerializeField] private ParkCreature _fishPrefab;

		#endregion

		#region Fields

		private const int Ducks = 3;
		private const int Frogs = 2;
		private const float DuckFlightSpeed = 9f;
		private const float DuckTurnDegreesPerSecond = 50f;
		private const float FarMeters = 60f;
		private const float FrogJumpSeconds = 0.45f;
		private const float FishLeapSeconds = 0.55f;
		private const float FountainRippleSeconds = 0.7f;
		private const int CallPriority = 110;
		private const int LoopPriority = 150;

		private readonly List<Duck> _ducks = new List<Duck>();
		private readonly List<Frog> _frogs = new List<Frog>();
		private ParkCreature _fish;
		private float _fishLeapStart = float.NegativeInfinity;
		private Vector3 _fishFrom;
		private Vector3 _fishTo;
		private ParkLoop[] _lappingLoops = Array.Empty<ParkLoop>();
		private ParkLoop _fountainLoop;
		private ParkLoop _trickleLoop;
		private float _croakCredit;
		private float _plopCredit;
		private float _nextArrival;
		private float _nextFountainRipple;

		#endregion

		#region Setup

		private void Start()
		{
			for (int i = 0; i < Ducks; i++)
			{
				ParkCreature creature = Instantiate(_duckPrefabs[i % _duckPrefabs.Length], transform);
				creature.gameObject.SetActive(false);
				_ducks.Add(new Duck { Creature = creature, Phase = UnityEngine.Random.value * 10f });
			}

			for (int i = 0; i < Frogs; i++)
			{
				ParkCreature creature = Instantiate(_frogPrefab, transform);
				creature.gameObject.SetActive(false);
				_frogs.Add(new Frog { Creature = creature, Timer = UnityEngine.Random.Range(1f, 5f) });
			}

			_fish = Instantiate(_fishPrefab, transform);
			_fish.gameObject.SetActive(false);

			ParkPondSettings settings = _life.Settings.Pond;
			_lappingLoops = new ParkLoop[_lapping.Length];
			for (int i = 0; i < _lapping.Length; i++)
			{
				_lappingLoops[i] = new ParkLoop(_life, transform, $"Lapping {i + 1}", settings.Lapping, LoopPriority, 0f);
				_lappingLoops[i].Transform.position = _lapping[i].Position;
			}

			_fountainLoop = new ParkLoop(_life, transform, "Fountain", settings.Fountain, LoopPriority, 0f);
			_fountainLoop.Transform.position = _fountain.Position;
			_trickleLoop = new ParkLoop(_life, transform, "Trickle", settings.Trickle, LoopPriority, 0f);
			_trickleLoop.Transform.position = _trickle.Position;
		}

		#endregion

		#region Update

		private void Update()
		{
			ParkPondSettings settings = _life.Settings.Pond;
			float now = Time.time, deltaTime = Time.deltaTime;
			int pond = ActivePond();
			UpdateDucks(settings, pond, now, deltaTime);
			UpdateFrogs(settings, now, deltaTime);
			UpdateFish(settings, pond, now, deltaTime);
			UpdateWater(now, deltaTime);
		}

		/// <summary>The water ducks swim on, the frogs' bank, the lapping spots and the fountain's row of jets.</summary>
		private void OnDrawGizmosSelected()
		{
			const int Segments = 48;
			Gizmos.color = Color.blue;
			for (int pond = 0; pond < _ponds.Length && pond < _pondRadii.Length; pond++)
			{
				Vector3 center = _ponds[pond].Position;
				Vector2 size = _pondRadii[pond];
				for (int i = 0; i < Segments; i++)
				{
					float from = 2f * Mathf.PI * i / Segments, to = 2f * Mathf.PI * (i + 1) / Segments;
					Gizmos.DrawLine(center + new Vector3(Mathf.Cos(from) * size.x, 0f, Mathf.Sin(from) * size.y),
						center + new Vector3(Mathf.Cos(to) * size.x, 0f, Mathf.Sin(to) * size.y));
				}
			}

			Gizmos.color = Color.green;
			foreach (ParkHabitatSpot bank in _frogEdges)
			{
				Gizmos.DrawWireSphere(bank.Position, 0.15f);
			}

			Gizmos.color = Color.cyan;
			foreach (ParkHabitatSpot spot in _lapping)
			{
				Gizmos.DrawWireSphere(spot.Position, 0.4f);
			}

			Vector3 jets = Vector3.right * _fountainHalfWidthMeters;
			Gizmos.DrawLine(_fountain.Position - jets, _fountain.Position + jets);
			Gizmos.DrawWireSphere(_trickle.Position, 0.4f);
		}

		#endregion

		#region Internal

		internal void Configure(ParkLife life, ParkWaterSurface water, ParkHabitatSpot[] ponds, Vector2[] pondRadii,
			ParkHabitatSpot[] reeds, ParkHabitatSpot[] frogEdges, ParkHabitatSpot[] lapping, ParkHabitatSpot fountain,
			float fountainHalfWidthMeters, ParkHabitatSpot trickle, ParkCreature[] ducks, ParkCreature frog, ParkCreature fish)
		{
			_life = life;
			_water = water;
			_ponds = ponds;
			_pondRadii = pondRadii;
			_reeds = reeds;
			_frogEdges = frogEdges;
			_lapping = lapping;
			_fountain = fountain;
			_fountainHalfWidthMeters = fountainHalfWidthMeters;
			_trickle = trickle;
			_duckPrefabs = ducks;
			_frogPrefab = frog;
			_fishPrefab = fish;
		}

		#endregion

		#region Private

		/// <summary>The first pond the level shows, or -1.</summary>
		private int ActivePond()
		{
			for (int i = 0; i < _ponds.Length; i++)
			{
				if (_ponds[i].Presence >= 0.5f)
				{
					return i;
				}
			}

			return -1;
		}

		private void UpdateDucks(ParkPondSettings settings, int pond, float now, float deltaTime)
		{
			int target = pond >= 0 ? Mathf.RoundToInt(_life.Activity(settings.Ducks)) : 0;
			int present = 0;
			foreach (Duck duck in _ducks)
			{
				bool isComing = duck.Mode == DuckMode.Flying && duck.IsLanding;
				present += duck.Mode == DuckMode.Swimming || duck.Mode == DuckMode.Dabbling || isComing ? 1 : 0;
			}

			foreach (Duck duck in _ducks)
			{
				bool isOnWater = duck.Mode == DuckMode.Swimming || duck.Mode == DuckMode.Dabbling;
				if (isOnWater && (duck.Pond != pond || present > target))
				{
					TakeOff(duck, settings, now);
					present--;
				}
			}

			if (present < target && now >= _nextArrival)
			{
				Duck arrival = _ducks.Find(duck => duck.Mode == DuckMode.Hidden);
				if (arrival != null)
				{
					Vector3 landing = OnPond(pond);
					Vector3 away = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward;
					Fly(arrival, landing + away * FarMeters + Vector3.up * 15f, landing, true, now);
					arrival.Pond = pond;
					_nextArrival = now + UnityEngine.Random.Range(3f, 8f);
				}
			}

			foreach (Duck duck in _ducks)
			{
				AnimateDuck(duck, settings, now, deltaTime);
			}
		}

		private void AnimateDuck(Duck duck, ParkPondSettings settings, float now, float deltaTime)
		{
			switch (duck.Mode)
			{
				case DuckMode.Flying:
					float t = (now - duck.StartTime) / duck.Duration;
					if (t >= 1f)
					{
						if (duck.IsLanding)
						{
							duck.Position = duck.To;
							duck.Target = OnPond(duck.Pond);
							duck.Mode = DuckMode.Swimming;
							duck.NextQuack = now + NextQuack(settings);
							duck.NextDabble = now + UnityEngine.Random.Range(15f, 40f);
							Splash(settings, duck.Position, 1f);
						}
						else
						{
							duck.Mode = DuckMode.Hidden;
							duck.Creature.gameObject.SetActive(false);
						}

						return;
					}

					duck.Position = ParkFlight.Arc(duck.From, duck.To, Mathf.SmoothStep(0f, 1f, t), duck.IsLanding ? 2f : 6f);
					float wings = duck.IsLanding && t > 0.85f
						? 60f * Mathf.Sin(2f * Mathf.PI * 9f * now)
						: 45f * Mathf.Sin(2f * Mathf.PI * 5f * now);
					duck.Creature.SetWings(wings, 0f);
					duck.Creature.SetHead(0f);
					duck.Creature.Root.SetPositionAndRotation(duck.Position, ParkFlight.Facing(duck.To - duck.From));
					break;
				case DuckMode.Swimming:
				case DuckMode.Dabbling:
					Swim(duck, settings, now, deltaTime);
					break;
			}
		}

		/// <summary>
		/// Paddles towards a point on the pond, bobbing; dabbles, quacks and leaves a faint wake now and then.
		/// </summary>
		private void Swim(Duck duck, ParkPondSettings settings, float now, float deltaTime)
		{
			bool isDabbling = duck.Mode == DuckMode.Dabbling;
			if (isDabbling && now >= duck.DabbleEnd)
			{
				duck.Mode = DuckMode.Swimming;
				isDabbling = false;
			}
			else if (!isDabbling && now >= duck.NextDabble)
			{
				duck.Mode = DuckMode.Dabbling;
				duck.DabbleEnd = now + UnityEngine.Random.Range(2f, 3.5f);
				duck.NextDabble = duck.DabbleEnd + UnityEngine.Random.Range(15f, 40f);
				isDabbling = true;
				Ripple(duck.Position, 0.4f);
			}

			if (!isDabbling)
			{
				Vector3 toTarget = duck.Target - duck.Position;
				toTarget.y = 0f;
				if (toTarget.magnitude < 0.4f)
				{
					duck.Target = OnPond(duck.Pond);
				}

				float wantedYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
				duck.Yaw = Mathf.MoveTowardsAngle(duck.Yaw, wantedYaw, DuckTurnDegreesPerSecond * deltaTime);
				Vector3 heading = Quaternion.Euler(0f, duck.Yaw, 0f) * Vector3.forward;
				duck.Position += heading * (settings.DuckSpeedMetersPerSecond * deltaTime);
				if (now >= duck.NextWake)
				{
					Ripple(duck.Position, 0.25f);
					duck.NextWake = now + 1.4f;
				}
			}

			if (now >= duck.NextQuack)
			{
				_life.Sounds.Play(settings.Quacks, duck.Position + Vector3.up * 0.15f, 1f, 1f, CallPriority);
				duck.NextQuack = now + NextQuack(settings);
			}

			float bob = 0.01f * Mathf.Sin(2f * Mathf.PI * 0.7f * now + duck.Phase);
			Vector3 position = new Vector3(duck.Position.x, _ponds[duck.Pond].Position.y + bob, duck.Position.z);
			duck.Creature.SetWings(-10f, 1f);
			duck.Creature.SetHead(isDabbling ? 100f : 0f);
			duck.Creature.Root.SetPositionAndRotation(position, Quaternion.Euler(isDabbling ? 35f : 0f, duck.Yaw, 0f));
		}

		/// <summary>Splashes off the water and flies out of the park.</summary>
		private void TakeOff(Duck duck, ParkPondSettings settings, float now)
		{
			Vector3 away = Quaternion.Euler(0f, duck.Yaw + UnityEngine.Random.Range(-40f, 40f), 0f) * Vector3.forward;
			Splash(settings, duck.Position, 0.7f);
			Fly(duck, duck.Position, duck.Position + away * FarMeters + Vector3.up * 20f, false, now);
		}

		private void Fly(Duck duck, Vector3 from, Vector3 to, bool isLanding, float now)
		{
			duck.From = from;
			duck.To = to;
			duck.Position = from;
			duck.StartTime = now;
			duck.Duration = Mathf.Max(1f, Vector3.Distance(from, to) / DuckFlightSpeed);
			duck.IsLanding = isLanding;
			duck.Mode = DuckMode.Flying;
			duck.Creature.Root.position = from;
			duck.Creature.gameObject.SetActive(true);
		}

		/// <summary>
		/// Frogs sit on the natural pond's bank, jump in with a plop, and later climb out somewhere else. A frog jumps in
		/// at once when its bank goes, and none climb out while the level shows no bank.
		/// </summary>
		private void UpdateFrogs(ParkPondSettings settings, float now, float deltaTime)
		{
			float croaks = _life.Activity(settings.CroaksPerMinute);
			bool isPondShown = _ponds.Length > 0 && _ponds[0].Presence >= 0.5f && croaks > 0.05f;
			foreach (Frog frog in _frogs)
			{
				if (!isPondShown)
				{
					if (frog.Mode != FrogMode.Hidden)
					{
						frog.Mode = FrogMode.Hidden;
						frog.Creature.gameObject.SetActive(false);
					}

					continue;
				}

				switch (frog.Mode)
				{
					case FrogMode.Hidden:
					case FrogMode.Under:
						frog.Timer -= deltaTime;
						if (frog.Timer <= 0f && PickBank(out int bank))
						{
							ParkHabitatSpot edge = _frogEdges[bank];
							Vector3 toWater = Vector3.ProjectOnPlane(_ponds[0].Position - edge.Position, Vector3.up).normalized;
							frog.Bank = bank;
							frog.From = edge.Position;
							frog.Yaw = Mathf.Atan2(toWater.x, toWater.z) * Mathf.Rad2Deg;
							frog.Mode = FrogMode.Sitting;
							frog.Timer = UnityEngine.Random.Range(12f, 40f);
							frog.Creature.Root.SetPositionAndRotation(frog.From, Quaternion.Euler(0f, frog.Yaw, 0f));
							frog.Creature.gameObject.SetActive(true);
							Ripple(frog.From + toWater * 0.3f, 0.3f);
						}

						break;
					case FrogMode.Sitting:
						frog.Timer -= deltaTime;
						if (frog.Timer <= 0f || _frogEdges[frog.Bank].Presence < 0.5f)
						{
							Vector3 forward = Quaternion.Euler(0f, frog.Yaw, 0f) * Vector3.forward;
							frog.To = frog.From + forward * 0.7f;
							frog.To.y = _ponds[0].Position.y;
							frog.StartTime = now;
							frog.Mode = FrogMode.Jumping;
						}

						break;
					case FrogMode.Jumping:
						float t = (now - frog.StartTime) / FrogJumpSeconds;
						if (t >= 1f)
						{
							_life.Sounds.Play(settings.FishPlop, frog.To, 0.8f, 1.2f, CallPriority);
							Ripple(frog.To, 0.8f);
							frog.Mode = FrogMode.Under;
							frog.Timer = UnityEngine.Random.Range(6f, 15f);
							frog.Creature.gameObject.SetActive(false);
							break;
						}

						frog.Creature.Root.SetPositionAndRotation(ParkFlight.Arc(frog.From, frog.To, t, 0.25f),
							Quaternion.Euler(-30f * (1f - t), frog.Yaw, 0f));
						break;
				}
			}

			if (_croakCredit < 2f)
			{
				_croakCredit += croaks / 60f * deltaTime;
			}

			if (_croakCredit >= 1f)
			{
				_croakCredit -= 1f;
				Frog sitting = _frogs.Find(frog => frog.Mode == FrogMode.Sitting);
				if (sitting != null && UnityEngine.Random.value < 0.3f)
				{
					_life.Sounds.Play(settings.FrogCroaks, sitting.From, 1f, 1f, CallPriority);
				}
				else if (PickReed(out Vector3 reed))
				{
					_life.Sounds.Play(settings.FrogCroaks, reed, 1f, 1f, CallPriority);
				}
			}
		}

		/// <summary>Plops in the shown pond, now and then with a fish leaping out first.</summary>
		private void UpdateFish(ParkPondSettings settings, int pond, float now, float deltaTime)
		{
			float leap = (now - _fishLeapStart) / FishLeapSeconds;
			if (_fish.gameObject.activeSelf)
			{
				if (leap >= 1f)
				{
					_life.Sounds.Play(settings.FishPlop, _fishTo, 1f, 1f, CallPriority);
					Ripple(_fishTo, 1f);
					_fish.gameObject.SetActive(false);
				}
				else
				{
					Vector3 direction = _fishTo - _fishFrom;
					float slope = Mathf.Lerp(40f, -40f, leap);
					_fish.SetTail(25f * Mathf.Sin(2f * Mathf.PI * 6f * now));
					_fish.Root.SetPositionAndRotation(ParkFlight.Arc(_fishFrom, _fishTo, leap, 0.35f),
						ParkFlight.Facing(direction, -slope));
				}
			}

			if (pond < 0)
			{
				return;
			}

			_plopCredit = Mathf.Min(2f, _plopCredit + _life.Activity(settings.PlopsPerMinute) / 60f * deltaTime);
			if (_plopCredit < 1f)
			{
				return;
			}

			_plopCredit -= 1f;
			Vector3 point = OnPond(pond);
			if (!_fish.gameObject.activeSelf && UnityEngine.Random.value < 0.35f)
			{
				Vector3 direction = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward;
				_fishFrom = point - direction * 0.3f + Vector3.down * 0.1f;
				_fishTo = point + direction * 0.3f;
				_fishLeapStart = now;
				_fish.Root.position = _fishFrom;
				_fish.gameObject.SetActive(true);
				Ripple(point - direction * 0.3f, 0.5f);
				return;
			}

			_life.Sounds.Play(settings.FishPlop, point, 1f, 1f, CallPriority);
			Ripple(point, 1f);
		}

		/// <summary>Lapping, the fountain and the basin follow their water; the fountain's jets ripple the pond.</summary>
		private void UpdateWater(float now, float deltaTime)
		{
			for (int i = 0; i < _lappingLoops.Length; i++)
			{
				_lappingLoops[i].Update(_lapping[i].Presence, 1f, deltaTime, 1.5f);
			}

			float fountain = _fountain.Presence;
			_fountainLoop.Update(fountain, 1f, deltaTime, 1.5f);
			_trickleLoop.Update(_trickle.Presence, 1f, deltaTime, 1.5f);
			if (fountain >= 0.5f && now >= _nextFountainRipple)
			{
				float along = UnityEngine.Random.Range(-_fountainHalfWidthMeters, _fountainHalfWidthMeters);
				Vector3 jet = _fountain.Position + Vector3.right * along;
				Ripple(new Vector3(jet.x, _fountain.Position.y, jet.z), 0.6f);
				_nextFountainRipple = now + FountainRippleSeconds;
			}
		}

		/// <summary>A random point on a pond's swimmable water, at its surface.</summary>
		private Vector3 OnPond(int pond)
		{
			float angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
			float radius = Mathf.Sqrt(UnityEngine.Random.value);
			Vector2 size = _pondRadii[pond];
			return _ponds[pond].Position
				+ new Vector3(Mathf.Cos(angle) * size.x * radius, 0f, Mathf.Sin(angle) * size.y * radius);
		}

		/// <summary>A random shown spot on the bank that no frog is sitting on, or false when there is none.</summary>
		private bool PickBank(out int bank)
		{
			int start = UnityEngine.Random.Range(0, _frogEdges.Length);
			for (int i = 0; i < _frogEdges.Length; i++)
			{
				bank = (start + i) % _frogEdges.Length;
				if (_frogEdges[bank].Presence >= 0.5f && !IsBankTaken(bank))
				{
					return true;
				}
			}

			bank = -1;
			return false;
		}

		private bool IsBankTaken(int bank)
		{
			foreach (Frog frog in _frogs)
			{
				if (frog.Bank == bank && (frog.Mode == FrogMode.Sitting || frog.Mode == FrogMode.Jumping))
				{
					return true;
				}
			}

			return false;
		}

		private bool PickReed(out Vector3 position)
		{
			position = default;
			for (int attempt = 0; attempt < 6 && _reeds.Length > 0; attempt++)
			{
				ParkHabitatSpot reed = _reeds[UnityEngine.Random.Range(0, _reeds.Length)];
				if (reed.Presence >= 0.5f)
				{
					position = reed.Position;
					return true;
				}
			}

			return false;
		}

		private void Splash(ParkPondSettings settings, Vector3 position, float strength)
		{
			_life.Sounds.Play(settings.Splash, position, strength, 1f, CallPriority);
			Ripple(position, strength);
		}

		private void Ripple(Vector3 position, float strength)
		{
			if (_water != null)
			{
				_water.AddRipple(position, strength);
			}
		}

		private static float NextQuack(ParkPondSettings settings)
		{
			float perMinute = Mathf.Max(0.01f, settings.QuacksPerMinutePerDuck);
			return 60f / perMinute * UnityEngine.Random.Range(0.5f, 1.5f);
		}

		#endregion
	}
}
