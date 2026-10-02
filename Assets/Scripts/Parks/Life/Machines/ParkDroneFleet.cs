using System;
using DroneDDW.Parks.Levels;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// Flies the park's drones. Patrol drones circle their route for good. Pad drones rest on a landing pad, spin up,
	/// climb, cruise to a delivery point, hover there, then fly back to a free pad and land, the pad's markings flashing
	/// as they leave and arrive. Every drone bobs, leans into its motion and carries a rotor loop whose pitch and volume
	/// follow its speed and climb, with Doppler as it passes. A drone whose level is gone stays where it is, hidden.
	/// Each drone is a level element wrapping its body: the level transition scales the wrapper, this moves the body.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Drone Fleet")]
	public sealed class ParkDroneFleet : MonoBehaviour
	{
		#region Entities

		private enum Phase
		{
			Patrol,
			Parked,
			SpinUp,
			Climb,
			Cruise,
			Deliver,
			Descend,
			SpinDown,
		}

		private sealed class Drone
		{
			public Transform Body;
			public ParkLevelElement Element;
			public ParkLoop Sound;
			public Phase Phase;
			public int Route = -1;
			public int Waypoint;
			public Vector3 Position;
			public Vector3 Velocity;
			public float Yaw;
			public float Spin;
			public float Timer;
			public int Pad = -1;
			public Vector3 Destination;
			public bool IsReturning;
			public float BobPhase;
		}

		#endregion

		#region Serialized fields

		[SerializeField] private ParkLife _life;

		[SerializeField, Tooltip("Pad markings flash as drones take off and land.")]
		private ParkMachineLights _lights;

		[SerializeField, Tooltip("Each drone's level element (the wrapper).")]
		private ParkLevelElement[] _elements = Array.Empty<ParkLevelElement>();

		[SerializeField, Tooltip("Each drone's body, moved by the fleet.")]
		private Transform[] _bodies = Array.Empty<Transform>();

		[SerializeField, Tooltip("Each drone's patrol route, or -1 for a drone that flies trips from the pads.")]
		private int[] _routeIndices = Array.Empty<int>();

		[SerializeField] private ParkRoute[] _routes = Array.Empty<ParkRoute>();

		[SerializeField, Tooltip("Landing pads: the point a drone touches down on, and the pad.")]
		private ParkHabitatSpot[] _pads = Array.Empty<ParkHabitatSpot>();

		[SerializeField, Tooltip("Points drones fly to and hover at, as if delivering.")]
		private Vector3[] _deliveryPoints = Array.Empty<Vector3>();

		#endregion

		#region Fields

		private const float WaypointRadiusMeters = 4f;
		private const float ArriveRadiusMeters = 1.5f;
		private const float SlowRadiusMeters = 5f;
		private const float ResponseSeconds = 0.8f;
		private const float LandedHeightMeters = 0.03f;
		private const float PadReleaseHeightMeters = 2f;
		private const float YawFollowSeconds = 0.6f;
		private const float SpinLiftMeters = 0.05f;
		private const int SoundPriority = 64;

		private Drone[] _drones = Array.Empty<Drone>();
		private int[] _padOwners = Array.Empty<int>();

		#endregion

		#region Setup

		private void Start()
		{
			_padOwners = new int[_pads.Length];
			for (int i = 0; i < _padOwners.Length; i++)
			{
				_padOwners[i] = -1;
			}

			_drones = new Drone[_bodies.Length];
			for (int i = 0; i < _drones.Length; i++)
			{
				Drone drone = new Drone
				{
					Body = _bodies[i],
					Element = _elements[i],
					Route = _routeIndices[i],
					Position = _bodies[i].position,
					Yaw = _bodies[i].eulerAngles.y,
					BobPhase = UnityEngine.Random.value * 10f,
				};
				drone.Sound = new ParkLoop(_life, drone.Body, "Rotors", _life.Settings.Drones.Sound, SoundPriority, 1f);
				if (drone.Route >= 0 && drone.Route < _routes.Length && _routes[drone.Route].Count > 0)
				{
					drone.Phase = Phase.Patrol;
					drone.Waypoint = _routes[drone.Route].Nearest(drone.Position);
					drone.Spin = 1f;
				}
				else
				{
					Park(drone, i, NearestFreePad(drone.Position));
				}

				_drones[i] = drone;
			}
		}

		#endregion

		#region Update

		private void Update()
		{
			float deltaTime = Time.deltaTime;
			ParkDroneSettings settings = _life.Settings.Drones;
			for (int i = 0; i < _drones.Length; i++)
			{
				Drone drone = _drones[i];
				if (drone.Element != null && !drone.Element.gameObject.activeInHierarchy)
				{
					continue;
				}

				Fly(drone, i, settings, deltaTime);
				Present(drone, settings, deltaTime);
			}
		}

		/// <summary>Patrol routes, landing pads and the points drones deliver to.</summary>
		private void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.cyan;
			foreach (ParkRoute route in _routes)
			{
				route.DrawGizmo();
			}

			Gizmos.color = Color.yellow;
			foreach (ParkHabitatSpot pad in _pads)
			{
				Gizmos.DrawWireSphere(pad.Position, 0.6f);
			}

			foreach (Vector3 point in _deliveryPoints)
			{
				Gizmos.DrawWireCube(point, Vector3.one * 0.5f);
			}
		}

		#endregion

		#region Internal

		internal void Configure(ParkLife life, ParkMachineLights lights, ParkLevelElement[] elements, Transform[] bodies,
			int[] routeIndices, ParkRoute[] routes, ParkHabitatSpot[] pads, Vector3[] deliveryPoints)
		{
			_life = life;
			_lights = lights;
			_elements = elements;
			_bodies = bodies;
			_routeIndices = routeIndices;
			_routes = routes;
			_pads = pads;
			_deliveryPoints = deliveryPoints;
		}

		#endregion

		#region Private

		private void Fly(Drone drone, int index, ParkDroneSettings settings, float deltaTime)
		{
			switch (drone.Phase)
			{
				case Phase.Patrol:
					ParkRoute route = _routes[drone.Route];
					Steer(drone, route[drone.Waypoint], settings.CruiseSpeedMetersPerSecond, deltaTime);
					if ((route[drone.Waypoint] - drone.Position).sqrMagnitude < WaypointRadiusMeters * WaypointRadiusMeters)
					{
						drone.Waypoint = (drone.Waypoint + 1) % route.Count;
					}

					break;
				case Phase.Parked:
					drone.Spin = Mathf.MoveTowards(drone.Spin, 0f, deltaTime / settings.SpinSeconds);
					drone.Timer -= deltaTime;
					if (drone.Timer <= 0f && _deliveryPoints.Length > 0)
					{
						drone.Destination = _deliveryPoints[UnityEngine.Random.Range(0, _deliveryPoints.Length)];
						drone.IsReturning = false;
						drone.Phase = Phase.SpinUp;
						PulsePad(drone.Pad);
					}

					break;
				case Phase.SpinUp:
					drone.Spin = Mathf.MoveTowards(drone.Spin, 1f, deltaTime / settings.SpinSeconds);
					if (drone.Spin >= 1f)
					{
						drone.Phase = Phase.Climb;
					}

					break;
				case Phase.Climb:
					Vector3 top = new Vector3(drone.Position.x, settings.CruiseHeightMeters, drone.Position.z);
					Steer(drone, top, settings.ClimbSpeedMetersPerSecond, deltaTime);
					if (drone.Pad >= 0 && drone.Position.y > _pads[drone.Pad].Position.y + PadReleaseHeightMeters)
					{
						_padOwners[drone.Pad] = -1;
						drone.Pad = -1;
					}

					if (Mathf.Abs(drone.Position.y - top.y) < ArriveRadiusMeters * 0.5f)
					{
						drone.Phase = Phase.Cruise;
					}

					break;
				case Phase.Cruise:
					Vector3 target = drone.IsReturning
						? new Vector3(drone.Destination.x, settings.CruiseHeightMeters, drone.Destination.z)
						: drone.Destination;
					Steer(drone, target, settings.CruiseSpeedMetersPerSecond, deltaTime);
					if ((target - drone.Position).sqrMagnitude < ArriveRadiusMeters * ArriveRadiusMeters)
					{
						if (drone.IsReturning)
						{
							drone.Phase = Phase.Descend;
						}
						else
						{
							drone.Phase = Phase.Deliver;
							drone.Timer = Range(settings.DeliveryHoverSeconds);
						}
					}

					break;
				case Phase.Deliver:
					Steer(drone, drone.Destination, settings.CruiseSpeedMetersPerSecond, deltaTime);
					drone.Timer -= deltaTime;
					if (drone.Timer <= 0f)
					{
						// Head for a free pad; with none free, hover a little longer and look again
						int pad = NearestFreePad(drone.Position);
						if (pad >= 0)
						{
							_padOwners[pad] = index;
							drone.Pad = pad;
							drone.Destination = _pads[pad].Position;
							drone.IsReturning = true;
							drone.Phase = Phase.Cruise;
						}
						else
						{
							drone.Timer = 2f;
						}
					}

					break;
				case Phase.Descend:
					Vector3 landing = _pads[drone.Pad].Position;
					float closeness = Mathf.Clamp01((drone.Position.y - landing.y) / 3f);
					Steer(drone, landing, settings.ClimbSpeedMetersPerSecond * Mathf.Lerp(0.35f, 1f, closeness), deltaTime);
					if (drone.Position.y - landing.y < LandedHeightMeters)
					{
						drone.Position = landing;
						drone.Velocity = Vector3.zero;
						drone.Phase = Phase.SpinDown;
						PulsePad(drone.Pad);
					}

					break;
				case Phase.SpinDown:
					drone.Spin = Mathf.MoveTowards(drone.Spin, 0f, deltaTime / settings.SpinSeconds);
					if (drone.Spin <= 0f)
					{
						drone.Phase = Phase.Parked;
						drone.Timer = Range(settings.PadWaitSeconds);
					}

					break;
			}
		}

		/// <summary>Accelerates smoothly towards a point, slowing down over the last few metres.</summary>
		private static void Steer(Drone drone, Vector3 target, float speed, float deltaTime)
		{
			Vector3 toTarget = target - drone.Position;
			float distance = toTarget.magnitude;
			Vector3 desired = distance > 0.001f
				? toTarget / distance * speed * Mathf.Clamp01(distance / SlowRadiusMeters)
				: Vector3.zero;
			drone.Velocity = Vector3.Lerp(drone.Velocity, desired, 1f - Mathf.Exp(-deltaTime / ResponseSeconds));
			drone.Position += drone.Velocity * deltaTime;
		}

		/// <summary>Places the body (bobbing and leaning while airborne) and drives its rotor sound.</summary>
		private void Present(Drone drone, ParkDroneSettings settings, float deltaTime)
		{
			Vector3 flat = new Vector3(drone.Velocity.x, 0f, drone.Velocity.z);
			float speed01 = Mathf.Clamp01(flat.magnitude / settings.CruiseSpeedMetersPerSecond);
			float climb01 = Mathf.Clamp01(Mathf.Abs(drone.Velocity.y) / settings.ClimbSpeedMetersPerSecond);
			if (flat.sqrMagnitude > 0.04f)
			{
				float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
				drone.Yaw = Mathf.LerpAngle(drone.Yaw, yaw, 1f - Mathf.Exp(-deltaTime / YawFollowSeconds));
			}

			bool isAirborne = drone.Phase != Phase.Parked && drone.Phase != Phase.SpinDown;
			float bob = isAirborne
				? settings.BobMeters * drone.Spin * Mathf.Sin(2f * Mathf.PI * settings.BobHertz * (Time.time + drone.BobPhase))
				: 0f;
			float lift = drone.Phase == Phase.SpinUp ? SpinLiftMeters * drone.Spin : 0f;
			Quaternion lean = flat.sqrMagnitude > 0.0001f
				? Quaternion.AngleAxis(settings.TiltDegrees * speed01, Vector3.Cross(Vector3.up, flat.normalized))
				: Quaternion.identity;
			drone.Body.SetPositionAndRotation(drone.Position + Vector3.up * (bob + lift),
				lean * Quaternion.Euler(0f, drone.Yaw, 0f));

			float presence = drone.Element != null ? Mathf.Clamp01(drone.Element.Presence) : 1f;
			float volume = drone.Spin * Mathf.Lerp(settings.HoverVolume, 1f, Mathf.Max(speed01, climb01)) * presence;
			float pitch = Mathf.Lerp(settings.PitchRange.x, settings.PitchRange.y, Mathf.Max(speed01, climb01))
				* Mathf.Lerp(0.6f, 1f, drone.Spin);
			drone.Sound.Update(volume, pitch, deltaTime, 0.25f);
		}

		private void Park(Drone drone, int index, int pad)
		{
			drone.Phase = Phase.Parked;
			drone.Spin = 0f;
			drone.Timer = Range(_life.Settings.Drones.PadWaitSeconds);
			if (pad < 0)
			{
				return;
			}

			_padOwners[pad] = index;
			drone.Pad = pad;
			drone.Position = _pads[pad].Position;
		}

		/// <summary>The nearest pad that is shown at this level and not taken, or -1.</summary>
		private int NearestFreePad(Vector3 position)
		{
			int best = -1;
			for (int i = 0; i < _pads.Length; i++)
			{
				if (_padOwners[i] >= 0 || _pads[i].Presence < 0.5f)
				{
					continue;
				}

				if (best < 0 || (_pads[i].Position - position).sqrMagnitude < (_pads[best].Position - position).sqrMagnitude)
				{
					best = i;
				}
			}

			return best;
		}

		private void PulsePad(int pad)
		{
			if (_lights != null)
			{
				_lights.PulsePad(pad);
			}
		}

		private static float Range(Vector2 range)
		{
			return UnityEngine.Random.Range(range.x, range.y);
		}

		#endregion
	}
}
