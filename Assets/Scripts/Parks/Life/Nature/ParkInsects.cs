using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// The park's insects near the seat: bees working the flower clumps (each with a buzz you only hear close up),
	/// butterflies fluttering over the meadows, dragonflies hovering and darting over the reeds, and crickets singing from
	/// every meadow the level shows. Insects only live where their flowers, meadows or reeds are shown, so they thin out
	/// and vanish as the park turns to efficiency.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Insects")]
	public sealed class ParkInsects : MonoBehaviour
	{
		#region Entities

		private enum Kind
		{
			Bee,
			Butterfly,
			Dragonfly,
		}

		private sealed class Insect
		{
			public ParkCreature Creature;
			public Kind Kind;
			public ParkLoop Buzz;
			public int Spot = -1;
			public Vector3 Position;
			public Vector3 Target;
			public Vector3 Velocity;
			public float Timer;
			public bool IsResting;
			public float Phase;
			public Vector3 DartFrom;
			public float DartStart = float.NegativeInfinity;
			public float DartSeconds;
		}

		#endregion

		#region Serialized fields

		[SerializeField] private ParkLife _life;

		[SerializeField, Tooltip("Flower clumps near the seat, at their tops.")]
		private ParkHabitatSpot[] _flowers = Array.Empty<ParkHabitatSpot>();

		[SerializeField, Tooltip("Meadows near the seat, where butterflies flutter.")]
		private ParkHabitatSpot[] _meadows = Array.Empty<ParkHabitatSpot>();

		[SerializeField, Tooltip("Every meadow: crickets sing from those the level shows.")]
		private ParkHabitatSpot[] _cricketMeadows = Array.Empty<ParkHabitatSpot>();

		[SerializeField, Tooltip("Reed clumps, where dragonflies hunt.")]
		private ParkHabitatSpot[] _reeds = Array.Empty<ParkHabitatSpot>();

		[SerializeField] private ParkCreature _beePrefab;
		[SerializeField] private ParkCreature[] _butterflyPrefabs = Array.Empty<ParkCreature>();
		[SerializeField] private ParkCreature _dragonflyPrefab;

		#endregion

		#region Fields

		private const int Bees = 8;
		private const int Butterflies = 8;
		private const int Dragonflies = 4;
		private const float BeeSpeed = 0.9f;
		private const float ButterflySpeed = 0.6f;
		private const float ReedRangeMeters = 25f;
		private const int BuzzPriority = 190;
		private const int CricketPriority = 180;

		private readonly List<Insect> _insects = new List<Insect>();
		private readonly List<int> _candidates = new List<int>();
		private ParkLoop[] _cricketLoops = Array.Empty<ParkLoop>();

		#endregion

		#region Setup

		private void Start()
		{
			ParkInsectSettings settings = _life.Settings.Insects;
			for (int i = 0; i < Bees; i++)
			{
				Insect bee = Create(_beePrefab, Kind.Bee);
				bee.Buzz = new ParkLoop(_life, bee.Creature.Root, "Buzz", settings.BeeBuzz, BuzzPriority, 0f);
			}

			for (int i = 0; i < Butterflies; i++)
			{
				Create(_butterflyPrefabs[i % _butterflyPrefabs.Length], Kind.Butterfly);
			}

			for (int i = 0; i < Dragonflies; i++)
			{
				Insect dragonfly = Create(_dragonflyPrefab, Kind.Dragonfly);
				dragonfly.Buzz = new ParkLoop(_life, dragonfly.Creature.Root, "Wings", settings.DragonflyWings, BuzzPriority, 0f);
			}

			_cricketLoops = new ParkLoop[_cricketMeadows.Length];
			for (int i = 0; i < _cricketMeadows.Length; i++)
			{
				_cricketLoops[i] = new ParkLoop(_life, transform, $"Crickets {i + 1}", settings.CricketSong, CricketPriority, 0f);
				_cricketLoops[i].Transform.position = _cricketMeadows[i].Position + Vector3.up * 0.1f;
			}
		}

		#endregion

		#region Update

		private void Update()
		{
			ParkInsectSettings settings = _life.Settings.Insects;
			float now = Time.time, deltaTime = Time.deltaTime;
			Populate(Kind.Bee, _flowers, settings.RangeMeters, Mathf.RoundToInt(_life.Activity(settings.Bees)));
			Populate(Kind.Butterfly, _meadows, settings.RangeMeters, Mathf.RoundToInt(_life.Activity(settings.Butterflies)));
			Populate(Kind.Dragonfly, _reeds, ReedRangeMeters, Mathf.RoundToInt(_life.Activity(settings.Dragonflies)));
			foreach (Insect insect in _insects)
			{
				if (insect.Spot < 0)
				{
					continue;
				}

				switch (insect.Kind)
				{
					case Kind.Bee:
						Bee(insect, now, deltaTime);
						break;
					case Kind.Butterfly:
						Butterfly(insect, now, deltaTime);
						break;
					case Kind.Dragonfly:
						Dragonfly(insect, now, deltaTime);
						break;
				}
			}

			float crickets = _life.Activity(settings.Crickets);
			for (int i = 0; i < _cricketLoops.Length; i++)
			{
				_cricketLoops[i].Update(crickets * _cricketMeadows[i].Presence, 1f, deltaTime, 2f);
			}
		}

		#endregion

		#region Internal

		internal void Configure(ParkLife life, ParkHabitatSpot[] flowers, ParkHabitatSpot[] meadows,
			ParkHabitatSpot[] cricketMeadows, ParkHabitatSpot[] reeds, ParkCreature bee, ParkCreature[] butterflies,
			ParkCreature dragonfly)
		{
			_life = life;
			_flowers = flowers;
			_meadows = meadows;
			_cricketMeadows = cricketMeadows;
			_reeds = reeds;
			_beePrefab = bee;
			_butterflyPrefabs = butterflies;
			_dragonflyPrefab = dragonfly;
		}

		#endregion

		#region Private

		/// <summary>
		/// Sends away insects of a kind whose place has gone or that are too many, and adds missing ones at shown places
		/// near the seat.
		/// </summary>
		private void Populate(Kind kind, ParkHabitatSpot[] spots, float rangeMeters, int target)
		{
			int present = 0;
			foreach (Insect insect in _insects)
			{
				if (insect.Kind != kind || insect.Spot < 0)
				{
					continue;
				}

				if (spots[insect.Spot].Presence < 0.5f || present >= target)
				{
					Hide(insect);
					continue;
				}

				present++;
			}

			if (present >= target || !PickSpot(spots, rangeMeters, out int spot))
			{
				return;
			}

			Insect free = _insects.Find(insect => insect.Kind == kind && insect.Spot < 0);
			if (free == null)
			{
				return;
			}

			free.Spot = spot;
			free.Position = Around(spots[spot], kind);
			free.Target = Around(spots[spot], kind);
			free.Velocity = Vector3.zero;
			free.IsResting = false;
			free.Timer = UnityEngine.Random.Range(1f, 3f);
			free.Phase = UnityEngine.Random.value;
			free.Creature.Root.position = free.Position;
			free.Creature.gameObject.SetActive(true);
		}

		/// <summary>Wanders about its flower, settles on it now and then, and sometimes moves to a neighbour.</summary>
		private void Bee(Insect bee, float now, float deltaTime)
		{
			ParkHabitatSpot flower = _flowers[bee.Spot];
			bee.Timer -= deltaTime;
			if (bee.IsResting)
			{
				if (bee.Timer <= 0f)
				{
					bee.IsResting = false;
					bee.Target = Around(flower, Kind.Bee);
				}
			}
			else
			{
				Steer(bee, BeeSpeed, 0.25f, deltaTime);
				if ((bee.Target - bee.Position).sqrMagnitude < 0.01f)
				{
					float roll = UnityEngine.Random.value;
					if (roll < 0.3f)
					{
						bee.IsResting = true;
						bee.Timer = UnityEngine.Random.Range(1f, 3f);
						bee.Target = flower.Position;
						bee.Position = flower.Position + Vector3.up * 0.02f;
						bee.Velocity = Vector3.zero;
					}
					else if (roll < 0.4f && NearFlower(flower.Position, out int next))
					{
						bee.Spot = next;
						bee.Target = Around(_flowers[next], Kind.Bee);
					}
					else
					{
						bee.Target = Around(flower, Kind.Bee);
					}
				}
			}

			bee.Creature.SetWings(bee.IsResting ? 20f : UnityEngine.Random.Range(-35f, 35f), bee.IsResting ? 0.6f : 0f);
			Place(bee);
			bee.Buzz.Update(bee.IsResting ? 0.15f : 1f, 1f, deltaTime, 0.2f);
		}

		/// <summary>Flutters erratically about its meadow, gliding now and then, and settles with wings closed.</summary>
		private void Butterfly(Insect butterfly, float now, float deltaTime)
		{
			ParkHabitatSpot meadow = _meadows[butterfly.Spot];
			butterfly.Timer -= deltaTime;
			if (butterfly.IsResting)
			{
				butterfly.Creature.SetWings(85f, 0f);
				if (butterfly.Timer <= 0f)
				{
					butterfly.IsResting = false;
					butterfly.Target = Around(meadow, Kind.Butterfly);
				}

				Place(butterfly);
				return;
			}

			Steer(butterfly, ButterflySpeed, 0.6f, deltaTime);
			if ((butterfly.Target - butterfly.Position).sqrMagnitude < 0.04f || butterfly.Timer <= 0f)
			{
				if (UnityEngine.Random.value < 0.15f)
				{
					butterfly.IsResting = true;
					butterfly.Timer = UnityEngine.Random.Range(2f, 4f);
					butterfly.Velocity = Vector3.zero;
					butterfly.Position = new Vector3(butterfly.Position.x, meadow.Position.y + 0.25f, butterfly.Position.z);
				}
				else
				{
					butterfly.Target = Around(meadow, Kind.Butterfly);
					butterfly.Timer = UnityEngine.Random.Range(2f, 5f);
				}
			}

			float bob = 0.08f * Mathf.Sin(2f * Mathf.PI * 2f * now + butterfly.Phase * 6f);
			butterfly.Creature.SetWings(ParkFlight.FlapGlide(now, butterfly.Phase, 8f, 70f, 0.75f, 1.2f), 0f);
			Place(butterfly, bob);
		}

		/// <summary>Hovers over the reeds, then darts quickly to a new spot.</summary>
		private void Dragonfly(Insect dragonfly, float now, float deltaTime)
		{
			ParkHabitatSpot reed = _reeds[dragonfly.Spot];
			float dart = (now - dragonfly.DartStart) / dragonfly.DartSeconds;
			if (dart < 1f)
			{
				dragonfly.Position = Vector3.Lerp(dragonfly.DartFrom, dragonfly.Target, Mathf.SmoothStep(0f, 1f, dart));
			}
			else
			{
				dragonfly.Timer -= deltaTime;
				Vector3 tremble = new Vector3(Mathf.Sin(now * 7f), Mathf.Sin(now * 5f), Mathf.Cos(now * 6f));
				dragonfly.Position = dragonfly.Target + 0.01f * tremble;
				if (dragonfly.Timer <= 0f)
				{
					dragonfly.DartFrom = dragonfly.Position;
					dragonfly.Target = Around(reed, Kind.Dragonfly);
					dragonfly.DartStart = now;
					dragonfly.DartSeconds = UnityEngine.Random.Range(0.3f, 0.5f);
					dragonfly.Timer = UnityEngine.Random.Range(0.6f, 2f);
					dragonfly.Velocity = dragonfly.Target - dragonfly.DartFrom;
				}
			}

			dragonfly.Creature.SetWings(UnityEngine.Random.Range(-20f, 20f), 0f);
			dragonfly.Creature.Root.SetPositionAndRotation(dragonfly.Position, ParkFlight.Facing(dragonfly.Velocity));
			dragonfly.Buzz.Update(dart < 1f ? 1f : 0.6f, 1f, deltaTime, 0.15f);
		}

		/// <summary>Eases the velocity towards the target and moves on.</summary>
		private static void Steer(Insect insect, float speed, float responseSeconds, float deltaTime)
		{
			Vector3 toTarget = insect.Target - insect.Position;
			float distance = toTarget.magnitude;
			Vector3 desired = distance > 0.001f ? toTarget / distance * speed * Mathf.Clamp01(distance / 0.3f) : Vector3.zero;
			insect.Velocity = Vector3.Lerp(insect.Velocity, desired, 1f - Mathf.Exp(-deltaTime / responseSeconds));
			insect.Position += insect.Velocity * deltaTime;
		}

		private static void Place(Insect insect, float bobMeters = 0f)
		{
			Quaternion facing = insect.Velocity.sqrMagnitude > 0.0001f
				? ParkFlight.Facing(insect.Velocity)
				: insect.Creature.Root.rotation;
			insect.Creature.Root.SetPositionAndRotation(insect.Position + Vector3.up * bobMeters, facing);
		}

		private void Hide(Insect insect)
		{
			insect.Spot = -1;
			insect.Creature.gameObject.SetActive(false);
		}

		/// <summary>
		/// A point about a spot at the height its kind flies at: low over flowers, higher over meadows and reeds.
		/// </summary>
		private static Vector3 Around(ParkHabitatSpot spot, Kind kind)
		{
			switch (kind)
			{
				case Kind.Bee:
					return ParkFlight.Around(spot.Position, 0.5f) + Vector3.up * UnityEngine.Random.Range(0.05f, 0.35f);
				case Kind.Butterfly:
					return ParkFlight.Around(spot.Position, Mathf.Min(2.5f, spot.RadiusMeters))
						+ Vector3.up * UnityEngine.Random.Range(0.3f, 1.4f);
				default:
					// Reed spots sit halfway up the reeds: from there up to just under their tops
					return ParkFlight.Around(spot.Position, 3f) + Vector3.up * UnityEngine.Random.Range(-0.3f, 0.5f);
			}
		}

		/// <summary>A random shown spot within a range of the listener.</summary>
		private bool PickSpot(ParkHabitatSpot[] spots, float rangeMeters, out int index)
		{
			_candidates.Clear();
			Vector3 listener = _life.ListenerPosition;
			for (int i = 0; i < spots.Length; i++)
			{
				Vector3 offset = spots[i].Position - listener;
				offset.y = 0f;
				if (offset.magnitude <= rangeMeters && spots[i].Presence >= 0.5f)
				{
					_candidates.Add(i);
				}
			}

			index = _candidates.Count > 0 ? _candidates[UnityEngine.Random.Range(0, _candidates.Count)] : -1;
			return index >= 0;
		}

		private bool NearFlower(Vector3 position, out int index)
		{
			_candidates.Clear();
			for (int i = 0; i < _flowers.Length; i++)
			{
				if (_flowers[i].Presence >= 0.5f && (_flowers[i].Position - position).sqrMagnitude < 16f)
				{
					_candidates.Add(i);
				}
			}

			index = _candidates.Count > 0 ? _candidates[UnityEngine.Random.Range(0, _candidates.Count)] : -1;
			return index >= 0;
		}

		private Insect Create(ParkCreature prefab, Kind kind)
		{
			ParkCreature creature = Instantiate(prefab, transform);
			creature.gameObject.SetActive(false);
			Insect insect = new Insect { Creature = creature, Kind = kind };
			_insects.Add(insect);
			return insect;
		}

		#endregion
	}
}
