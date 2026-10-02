using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// The park's birds in the trees and on the ground: songs from the canopies, songbirds flitting from tree to tree,
	/// birds hopping and pecking in the meadows around the seat, pigeons (and now and then a crow) walking the plaza at
	/// the efficiency levels, a woodpecker drumming on a trunk and crows calling in the distance. Birds only use the
	/// trees, meadows and paving the level shows; when their place goes, they fly off. Ground birds come and go by flying
	/// from and to nearby trees.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Birds")]
	public sealed class ParkBirds : MonoBehaviour
	{
		#region Entities

		private enum Mode
		{
			Hidden,
			Flight,
			Ground,
			Cling,
		}

		private enum Action
		{
			Idle,
			Hop,
			Peck,
			Turn,
			Walk,
			Drum,
		}

		private sealed class Bird
		{
			public ParkCreature Creature;
			public Mode Mode;
			public Vector3 Position;
			public Vector3 From;
			public Vector3 To;
			public float StartTime;
			public float Duration;
			public float ArcMeters;
			public float Yaw;
			public float Phase;
			public int Spot = -1;
			public bool LandsOnGround;
			public bool IsWalker;
			public bool IsCrow;
			public Action Action;
			public float ActionStart;
			public float ActionSeconds;
			public float TurnFrom;
			public float TurnTo;
			public Vector3 HopFrom;
			public Vector3 HopTo;
			public float LeaveTime;
		}

		#endregion

		#region Serialized fields

		[SerializeField] private ParkLife _life;

		[SerializeField, Tooltip("Tree canopies: where songs come from and where flights start and end.")]
		private ParkHabitatSpot[] _crowns = Array.Empty<ParkHabitatSpot>();

		[SerializeField, Tooltip("The meadows around the seat, where birds hop and peck.")]
		private ParkHabitatSpot[] _meadows = Array.Empty<ParkHabitatSpot>();

		[SerializeField, Tooltip("Tree trunks the woodpecker can cling to, at drumming height.")]
		private ParkHabitatSpot[] _trunks = Array.Empty<ParkHabitatSpot>();

		[SerializeField, Tooltip("The plaza around the seat, where pigeons walk (its radius is the outer edge).")]
		private ParkHabitatSpot _plaza;

		[SerializeField, Tooltip("Pigeons keep this far from the plaza's centre, off the seat platform.")]
		private float _plazaInnerMeters = 3.6f;

		[SerializeField, Tooltip("Lamp tops: pigeons fly in from them and coo from them.")]
		private ParkHabitatSpot[] _lamps = Array.Empty<ParkHabitatSpot>();

		[SerializeField] private ParkCreature[] _songbirdPrefabs = Array.Empty<ParkCreature>();
		[SerializeField] private ParkCreature _pigeonPrefab;

		[SerializeField, Tooltip("Walks the plaza with the pigeons, as one of them.")]
		private ParkCreature _crowPrefab;

		[SerializeField] private ParkCreature _woodpeckerPrefab;

		#endregion

		#region Fields

		private const int Flyers = 4;
		private const int GroundBirds = 4;
		private const int Pigeons = 3;
		private const float SongbirdSpeed = 7f;
		private const float PigeonSpeed = 6f;
		private const float WalkSpeed = 0.22f;
		private const float FlightRangeMeters = 35f;
		private const float NearCrownMeters = 30f;
		private const float FarFlightMeters = 45f;
		private const float WoodpeckerNearMeters = 10f;
		private const float WoodpeckerFarMeters = 28f;
		private const float DrumSeconds = 1.2f;
		private const int CallPriority = 110;

		private readonly List<Bird> _flyers = new List<Bird>();
		private readonly List<Bird> _groundBirds = new List<Bird>();

		// The plaza's walkers: pigeons, one of them a crow
		private readonly List<Bird> _pigeons = new List<Bird>();
		private readonly List<int> _candidates = new List<int>();
		private Bird _woodpecker;
		private float _songCredit;
		private float _flightCredit;
		private float _drumCredit;
		private float _cooCredit;
		private float _cawCredit;
		private float _nextGroundArrival;
		private float _nextPigeonArrival;

		#endregion

		#region Setup

		private void Start()
		{
			for (int i = 0; i < Flyers; i++)
			{
				_flyers.Add(CreateBird(_songbirdPrefabs[i % _songbirdPrefabs.Length], false));
			}

			for (int i = 0; i < GroundBirds; i++)
			{
				_groundBirds.Add(CreateBird(_songbirdPrefabs[(i + 1) % _songbirdPrefabs.Length], false));
			}

			for (int i = 0; i < Pigeons; i++)
			{
				_pigeons.Add(CreateBird(i == 0 && _crowPrefab != null ? _crowPrefab : _pigeonPrefab, true));
			}

			_woodpecker = CreateBird(_woodpeckerPrefab, false);
		}

		#endregion

		#region Update

		private void Update()
		{
			ParkBirdSettings settings = _life.Settings.Birds;
			float now = Time.time, deltaTime = Time.deltaTime;
			Songs(settings, deltaTime);
			Flights(settings, now, deltaTime);
			GroundLife(_groundBirds, settings, now, false, ref _nextGroundArrival);
			GroundLife(_pigeons, settings, now, true, ref _nextPigeonArrival);
			Woodpecker(settings, now, deltaTime);
			Calls(settings, deltaTime);
			foreach (Bird bird in _flyers)
			{
				Animate(bird, settings, now, deltaTime);
			}

			foreach (Bird bird in _groundBirds)
			{
				Animate(bird, settings, now, deltaTime);
			}

			foreach (Bird bird in _pigeons)
			{
				Animate(bird, settings, now, deltaTime);
			}

			Animate(_woodpecker, settings, now, deltaTime);
		}

		#endregion

		#region Internal

		internal void Configure(ParkLife life, ParkHabitatSpot[] crowns, ParkHabitatSpot[] meadows,
			ParkHabitatSpot[] trunks, ParkHabitatSpot plaza, ParkHabitatSpot[] lamps, ParkCreature[] songbirds,
			ParkCreature pigeon, ParkCreature crow, ParkCreature woodpecker)
		{
			_life = life;
			_crowns = crowns;
			_meadows = meadows;
			_trunks = trunks;
			_plaza = plaza;
			_lamps = lamps;
			_songbirdPrefabs = songbirds;
			_pigeonPrefab = pigeon;
			_crowPrefab = crow;
			_woodpeckerPrefab = woodpecker;
		}

		#endregion

		#region Private

		private void Songs(ParkBirdSettings settings, float deltaTime)
		{
			if (Credit(ref _songCredit, _life.Activity(settings.SongsPerMinute), deltaTime)
				&& PickSpot(_crowns, settings.SongDistanceMeters.x, settings.SongDistanceMeters.y, out int crown))
			{
				_life.Sounds.Play(settings.Songs, InCanopy(crown), 1f, 1f, CallPriority);
			}
		}

		/// <summary>Now and then a songbird flits from one tree to a neighbour, sometimes singing as it lands.</summary>
		private void Flights(ParkBirdSettings settings, float now, float deltaTime)
		{
			if (!Credit(ref _flightCredit, _life.Activity(settings.FlightsPerMinute), deltaTime))
			{
				return;
			}

			Bird bird = Free(_flyers);
			if (bird == null || !PickSpot(_crowns, 4f, FlightRangeMeters, out int from))
			{
				return;
			}

			int to = NearCrown(_crowns[from].Position, 6f, 25f);
			if (to < 0)
			{
				return;
			}

			StartFlight(bird, CanopyEdge(from, _crowns[to].Position), CanopyEdge(to, _crowns[from].Position), SongbirdSpeed,
				false, now);
			Flutter(bird.Position);
		}

		/// <summary>
		/// Keeps the right number of birds on the ground: those whose place has gone, that are too many or whose time is up
		/// fly off; missing ones fly in, one at a time.
		/// </summary>
		private void GroundLife(List<Bird> birds, ParkBirdSettings settings, float now, bool isPigeon, ref float nextArrival)
		{
			int target = isPigeon
				? (_plaza.RadiusMeters > _plazaInnerMeters && _plaza.Presence >= 0.5f
					? Mathf.RoundToInt(_life.Activity(settings.Pigeons))
					: 0)
				: Mathf.RoundToInt(_life.Activity(settings.GroundBirds));
			int present = 0;
			foreach (Bird bird in birds)
			{
				present += bird.Mode == Mode.Ground || (bird.Mode == Mode.Flight && bird.LandsOnGround) ? 1 : 0;
			}

			foreach (Bird bird in birds)
			{
				if (bird.Mode != Mode.Ground)
				{
					continue;
				}

				float placePresence = isPigeon ? _plaza.Presence : _meadows[bird.Spot].Presence;
				if (placePresence < 0.5f || present > target || now >= bird.LeaveTime)
				{
					FlyOff(bird, now);
					present--;
				}
			}

			if (present >= target || now < nextArrival)
			{
				return;
			}

			Bird arrival = Free(birds);
			if (arrival == null)
			{
				return;
			}

			Vector3 landing;
			if (isPigeon)
			{
				landing = OnPlaza();
				arrival.Spot = 0;
			}
			else if (PickSpot(_meadows, 0f, float.MaxValue, out int meadow))
			{
				landing = ParkFlight.Around(_meadows[meadow].Position, _meadows[meadow].RadiusMeters);
				arrival.Spot = meadow;
			}
			else
			{
				return;
			}

			Vector3 start = isPigeon ? LampOrFar(landing) : CrownOrFar(landing);
			StartFlight(arrival, start, landing, isPigeon ? PigeonSpeed : SongbirdSpeed, true, now);
			arrival.LeaveTime = now + UnityEngine.Random.Range(25f, 80f);
			nextArrival = now + UnityEngine.Random.Range(2f, 6f);
		}

		/// <summary>The woodpecker clings to a trunk on the seat's side of a nearby tree and drums now and then.</summary>
		private void Woodpecker(ParkBirdSettings settings, float now, float deltaTime)
		{
			float rate = _life.Activity(settings.DrumsPerMinute);
			bool isWanted = rate > 0.05f;
			Bird bird = _woodpecker;
			if (bird.Mode == Mode.Cling)
			{
				if (!isWanted || _trunks[bird.Spot].Presence < 0.5f)
				{
					FlyOff(bird, now);
				}
				else if (Credit(ref _drumCredit, rate, deltaTime))
				{
					BeginAction(bird, Action.Drum, DrumSeconds, now);
					_life.Sounds.Play(settings.Woodpecker, bird.Position, 1f, 1f, CallPriority);
				}
			}
			else if (bird.Mode == Mode.Hidden && isWanted
				&& PickSpot(_trunks, WoodpeckerNearMeters, WoodpeckerFarMeters, out int trunk))
			{
				Vector3 toListener = Flat(_life.ListenerPosition - _trunks[trunk].Position).normalized;
				bird.Spot = trunk;
				bird.Position = _trunks[trunk].Position + toListener * _trunks[trunk].RadiusMeters;
				bird.Yaw = Mathf.Atan2(-toListener.x, -toListener.z) * Mathf.Rad2Deg;
				bird.Mode = Mode.Cling;
				BeginAction(bird, Action.Idle, 1f, now);
				bird.Creature.gameObject.SetActive(true);
			}
		}

		/// <summary>
		/// Pigeons coo (from a pigeon on the plaza, or a lamp top); crows call from the plaza's crow when it is there,
		/// otherwise from far trees and lamps.
		/// </summary>
		private void Calls(ParkBirdSettings settings, float deltaTime)
		{
			if (Credit(ref _cooCredit, _life.Activity(settings.CoosPerMinute), deltaTime))
			{
				Bird pigeon = _pigeons.Find(bird => bird.Mode == Mode.Ground && !bird.IsCrow);
				if (pigeon != null)
				{
					_life.Sounds.Play(settings.PigeonCoo, pigeon.Position + Vector3.up * 0.2f, 1f, 1f, CallPriority);
				}
				else if (PickSpot(_lamps, 0f, 30f, out int lamp))
				{
					_life.Sounds.Play(settings.PigeonCoo, _lamps[lamp].Position, 1f, 1f, CallPriority);
				}
			}

			if (Credit(ref _cawCredit, _life.Activity(settings.CawsPerMinute), deltaTime))
			{
				Bird crow = _pigeons.Find(bird => bird.Mode == Mode.Ground && bird.IsCrow);
				if (crow != null && UnityEngine.Random.value < 0.5f)
				{
					_life.Sounds.Play(settings.CrowCaw, crow.Position + Vector3.up * 0.25f, 1f, 1f, CallPriority);
				}
				else if (PickSpot(_crowns, 25f, 60f, out int crown))
				{
					_life.Sounds.Play(settings.CrowCaw, InCanopy(crown), 1f, 1f, CallPriority);
				}
				else if (PickSpot(_lamps, 25f, 60f, out int lamp))
				{
					_life.Sounds.Play(settings.CrowCaw, _lamps[lamp].Position, 1f, 1f, CallPriority);
				}
			}
		}

		private void Animate(Bird bird, ParkBirdSettings settings, float now, float deltaTime)
		{
			switch (bird.Mode)
			{
				case Mode.Flight:
					float t = (now - bird.StartTime) / bird.Duration;
					if (t >= 1f)
					{
						Arrive(bird, settings, now);
						return;
					}

					Vector3 direction = bird.To - bird.From;
					bird.Position = ParkFlight.Arc(bird.From, bird.To, Mathf.SmoothStep(0f, 1f, t), bird.ArcMeters);
					float wings = t > 0.8f
						? 70f * Mathf.Sin(2f * Mathf.PI * 16f * now)
						: ParkFlight.FlapGlide(now, bird.Phase, bird.IsWalker ? 7f : 13f, 55f, 0.55f, 0.8f);
					bird.Creature.SetWings(wings, 0f);
					bird.Creature.SetHead(0f);
					bird.Creature.Root.SetPositionAndRotation(bird.Position, ParkFlight.Facing(direction));
					break;
				case Mode.Ground:
					Walk(bird, now, deltaTime);
					break;
				case Mode.Cling:
					float drum = bird.Action == Action.Drum && now - bird.ActionStart < bird.ActionSeconds
						? 25f * Mathf.Sin(2f * Mathf.PI * 16f * (now - bird.ActionStart))
						: 0f;
					bird.Creature.SetWings(-20f, 1f);
					bird.Creature.SetHead(drum);
					bird.Creature.Root.SetPositionAndRotation(bird.Position, Quaternion.Euler(-70f, bird.Yaw, 0f));
					break;
			}
		}

		/// <summary>On the ground: hops or walks, pecks, turns and pauses, wings folded.</summary>
		private void Walk(Bird bird, float now, float deltaTime)
		{
			float elapsed = now - bird.ActionStart;
			if (elapsed >= bird.ActionSeconds)
			{
				NextAction(bird, now);
				elapsed = 0f;
			}

			float share = Mathf.Clamp01(elapsed / bird.ActionSeconds);
			float head = 0f;
			switch (bird.Action)
			{
				case Action.Hop:
					bird.Position = Vector3.Lerp(bird.HopFrom, bird.HopTo, share) + Vector3.up * (0.045f * 4f * share * (1f - share));
					break;
				case Action.Walk:
					Vector3 forward = Quaternion.Euler(0f, bird.Yaw, 0f) * Vector3.forward;
					Vector3 next = bird.Position + forward * (WalkSpeed * deltaTime);
					if (OnPlazaRing(next))
					{
						bird.Position = next;
					}
					else
					{
						// At the ring's edge: turn back towards its middle, inwards from the outer edge, outwards from the inner
						Vector3 fromCenter = Flat(bird.Position - _plaza.Position);
						float outward = Mathf.Atan2(fromCenter.x, fromCenter.z) * Mathf.Rad2Deg;
						bool isOuter = fromCenter.magnitude > (_plazaInnerMeters + _plaza.RadiusMeters) * 0.5f;
						BeginTurn(bird, outward + (isOuter ? 180f : 0f) + UnityEngine.Random.Range(-60f, 60f), now);
					}

					head = 15f * Mathf.Sin(2f * Mathf.PI * 3f * elapsed);
					break;
				case Action.Peck:
					head = 65f * Mathf.Sin(Mathf.PI * share);
					break;
				case Action.Turn:
					bird.Yaw = Mathf.LerpAngle(bird.TurnFrom, bird.TurnTo, share);
					break;
			}

			bird.Creature.SetWings(-20f, 1f);
			bird.Creature.SetHead(head);
			bird.Creature.Root.SetPositionAndRotation(bird.Position, Quaternion.Euler(0f, bird.Yaw, 0f));
		}

		private void NextAction(Bird bird, float now)
		{
			float roll = UnityEngine.Random.value;
			if (bird.IsWalker)
			{
				if (roll < 0.4f)
				{
					BeginAction(bird, Action.Walk, UnityEngine.Random.Range(1f, 3f), now);
				}
				else if (roll < 0.65f)
				{
					BeginAction(bird, Action.Peck, 0.3f, now);
				}
				else if (roll < 0.85f)
				{
					BeginAction(bird, Action.Idle, UnityEngine.Random.Range(0.5f, 1.8f), now);
				}
				else
				{
					BeginTurn(bird, bird.Yaw + UnityEngine.Random.Range(-110f, 110f), now);
				}

				return;
			}

			if (roll < 0.3f)
			{
				BeginAction(bird, Action.Idle, UnityEngine.Random.Range(0.4f, 1.5f), now);
			}
			else if (roll < 0.6f)
			{
				// A hop to somewhere close, staying in the meadow
				ParkHabitatSpot meadow = _meadows[bird.Spot];
				Vector3 target = ParkFlight.Around(bird.Position, 0.35f);
				if (Flat(target - meadow.Position).magnitude > meadow.RadiusMeters)
				{
					Vector3 middle = new Vector3(meadow.Position.x, bird.Position.y, meadow.Position.z);
					target = Vector3.MoveTowards(bird.Position, middle, 0.3f);
				}

				target.y = meadow.Position.y;
				bird.HopFrom = bird.Position;
				bird.HopTo = target;
				Vector3 hop = target - bird.Position;
				if (Flat(hop).sqrMagnitude > 1e-6f)
				{
					bird.Yaw = Mathf.Atan2(hop.x, hop.z) * Mathf.Rad2Deg;
				}

				BeginAction(bird, Action.Hop, 0.18f, now);
			}
			else if (roll < 0.85f)
			{
				BeginAction(bird, Action.Peck, 0.25f, now);
			}
			else
			{
				BeginTurn(bird, bird.Yaw + UnityEngine.Random.Range(-100f, 100f), now);
			}
		}

		private static void BeginAction(Bird bird, Action action, float seconds, float now)
		{
			bird.Action = action;
			bird.ActionStart = now;
			bird.ActionSeconds = Mathf.Max(0.05f, seconds);
		}

		private static void BeginTurn(Bird bird, float yaw, float now)
		{
			bird.TurnFrom = bird.Yaw;
			bird.TurnTo = yaw;
			BeginAction(bird, Action.Turn, 0.3f, now);
		}

		private void StartFlight(Bird bird, Vector3 from, Vector3 to, float speed, bool landsOnGround, float now)
		{
			float distance = Vector3.Distance(from, to);
			bird.From = from;
			bird.To = to;
			bird.Position = from;
			bird.StartTime = now;
			bird.Duration = Mathf.Max(0.6f, distance / speed);
			bird.ArcMeters = Mathf.Clamp(distance * 0.2f, 0.3f, 3f);
			bird.Phase = UnityEngine.Random.value;
			bird.LandsOnGround = landsOnGround;
			bird.Mode = Mode.Flight;
			bird.Creature.Root.position = from;
			bird.Creature.gameObject.SetActive(true);
		}

		private void Arrive(Bird bird, ParkBirdSettings settings, float now)
		{
			if (bird.LandsOnGround)
			{
				bird.Position = bird.To;
				bird.Mode = Mode.Ground;
				bird.Yaw = Mathf.Atan2(bird.To.x - bird.From.x, bird.To.z - bird.From.z) * Mathf.Rad2Deg;
				BeginAction(bird, Action.Idle, UnityEngine.Random.Range(0.5f, 1.5f), now);
				return;
			}

			// Landed in a canopy, out of sight; sometimes it sings there
			if (UnityEngine.Random.value < 0.35f)
			{
				_life.Sounds.Play(settings.Songs, bird.To, 1f, 1f, CallPriority);
			}

			bird.Mode = Mode.Hidden;
			bird.Creature.gameObject.SetActive(false);
		}

		/// <summary>Takes off from wherever it is, to a nearby tree or away out of the park.</summary>
		private void FlyOff(Bird bird, float now)
		{
			Vector3 from = bird.Position;
			Vector3 to = bird.IsWalker ? LampOrFar(from) : CrownOrFar(from);
			StartFlight(bird, from, to, bird.IsWalker ? PigeonSpeed : SongbirdSpeed, false, now);
			Flutter(from);
		}

		private void Flutter(Vector3 position)
		{
			_life.Sounds.Play(_life.Settings.Birds.WingFlutter, position, 1f, 1f, CallPriority);
		}

		private Bird CreateBird(ParkCreature prefab, bool isWalker)
		{
			ParkCreature creature = Instantiate(prefab, transform);
			creature.gameObject.SetActive(false);
			return new Bird { Creature = creature, IsWalker = isWalker, IsCrow = prefab == _crowPrefab };
		}

		/// <summary>A random bird of the group that isn't out, so the colourings (and the crow) take turns.</summary>
		private static Bird Free(List<Bird> birds)
		{
			int start = UnityEngine.Random.Range(0, birds.Count);
			for (int i = 0; i < birds.Count; i++)
			{
				Bird bird = birds[(start + i) % birds.Count];
				if (bird.Mode == Mode.Hidden)
				{
					return bird;
				}
			}

			return null;
		}

		/// <summary>A random shown spot at a ground distance from the listener in a range.</summary>
		private bool PickSpot(ParkHabitatSpot[] spots, float minMeters, float maxMeters, out int index)
		{
			_candidates.Clear();
			Vector3 listener = _life.ListenerPosition;
			for (int i = 0; i < spots.Length; i++)
			{
				float distance = Flat(spots[i].Position - listener).magnitude;
				if (distance >= minMeters && distance <= maxMeters && spots[i].Presence >= 0.5f)
				{
					_candidates.Add(i);
				}
			}

			index = _candidates.Count > 0 ? _candidates[UnityEngine.Random.Range(0, _candidates.Count)] : -1;
			return index >= 0;
		}

		/// <summary>A random shown canopy at a ground distance from a point in a range, or -1.</summary>
		private int NearCrown(Vector3 point, float minMeters, float maxMeters)
		{
			_candidates.Clear();
			for (int i = 0; i < _crowns.Length; i++)
			{
				float distance = Flat(_crowns[i].Position - point).magnitude;
				if (distance >= minMeters && distance <= maxMeters && _crowns[i].Presence >= 0.5f)
				{
					_candidates.Add(i);
				}
			}

			return _candidates.Count > 0 ? _candidates[UnityEngine.Random.Range(0, _candidates.Count)] : -1;
		}

		private Vector3 InCanopy(int crown)
		{
			ParkHabitatSpot spot = _crowns[crown];
			return ParkFlight.Around(spot.Position, spot.RadiusMeters * 0.6f) + Vector3.up * UnityEngine.Random.Range(-1f, 1f);
		}

		/// <summary>The side of a canopy facing a point, where a bird leaves or enters it.</summary>
		private Vector3 CanopyEdge(int crown, Vector3 toward)
		{
			ParkHabitatSpot spot = _crowns[crown];
			Vector3 direction = Flat(toward - spot.Position).normalized;
			return spot.Position + direction * (spot.RadiusMeters * 0.8f) + Vector3.up * UnityEngine.Random.Range(-0.8f, 0.5f);
		}

		private Vector3 CrownOrFar(Vector3 point)
		{
			int crown = NearCrown(point, 3f, NearCrownMeters);
			return crown >= 0 ? CanopyEdge(crown, point) : FarPoint(point);
		}

		private Vector3 LampOrFar(Vector3 point)
		{
			_candidates.Clear();
			for (int i = 0; i < _lamps.Length; i++)
			{
				if (_lamps[i].Presence >= 0.5f && Flat(_lamps[i].Position - point).magnitude < NearCrownMeters)
				{
					_candidates.Add(i);
				}
			}

			return _candidates.Count > 0
				? _lamps[_candidates[UnityEngine.Random.Range(0, _candidates.Count)]].Position
				: FarPoint(point);
		}

		/// <summary>A point well away from the seat, up in the air, for birds leaving the park.</summary>
		private static Vector3 FarPoint(Vector3 point)
		{
			Vector3 away = Flat(point).sqrMagnitude > 1f ? Flat(point).normalized : new Vector3(0f, 0f, 1f);
			away = Quaternion.Euler(0f, UnityEngine.Random.Range(-50f, 50f), 0f) * away;
			return point + away * FarFlightMeters + Vector3.up * UnityEngine.Random.Range(10f, 18f);
		}

		private Vector3 OnPlaza()
		{
			float angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
			float outer = Mathf.Max(_plazaInnerMeters + 0.6f, _plaza.RadiusMeters - 0.3f);
			float radius = UnityEngine.Random.Range(_plazaInnerMeters + 0.3f, outer);
			return _plaza.Position + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
		}

		private bool OnPlazaRing(Vector3 point)
		{
			float distance = Flat(point - _plaza.Position).magnitude;
			return distance > _plazaInnerMeters && distance < _plaza.RadiusMeters;
		}

		/// <summary>Adds up a rate per minute and says when the next event is due.</summary>
		private static bool Credit(ref float credit, float perMinute, float deltaTime)
		{
			credit = Mathf.Min(2f, credit + perMinute / 60f * deltaTime);
			if (credit < 1f)
			{
				return false;
			}

			credit -= 1f;
			return true;
		}

		private static Vector3 Flat(Vector3 vector)
		{
			return new Vector3(vector.x, 0f, vector.z);
		}

		#endregion
	}
}
