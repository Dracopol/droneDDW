using System;
using DroneDDW.Parks.Levels;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// Drives the delivery robots back and forth along their routes on the paths. A robot eases into each end, stops
	/// with a beep, turns round while it waits, and beeps again as it sets off. Its motor whine follows its speed.
	/// Each robot is a level element wrapping its body: the level transition scales the wrapper, this moves the body.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Robot Fleet")]
	public sealed class ParkRobotFleet : MonoBehaviour
	{
		#region Entities

		private sealed class Robot
		{
			public Transform Body;
			public ParkLevelElement Element;
			public ParkRoute Route;
			public ParkLoop Motor;
			public float Distance;
			public int Direction = 1;
			public float Speed;
			public float Yaw;
			public float StopTimer;
		}

		#endregion

		#region Serialized fields

		[SerializeField] private ParkLife _life;
		[SerializeField] private ParkLevelElement[] _elements = Array.Empty<ParkLevelElement>();
		[SerializeField] private Transform[] _bodies = Array.Empty<Transform>();

		[SerializeField, Tooltip("Each robot's route, driven from end to end and back.")]
		private ParkRoute[] _routes = Array.Empty<ParkRoute>();

		#endregion

		#region Fields

		private const float SlowDistanceMeters = 2f;
		private const float MinimumSpeedShare = 0.15f;
		private const float AccelerationMetersPerSecondSquared = 1f;
		private const float BumpMeters = 0.004f;
		private const float BumpHertz = 3f;
		private const int SoundPriority = 80;

		private Robot[] _robots = Array.Empty<Robot>();

		#endregion

		#region Setup

		private void Start()
		{
			_robots = new Robot[_bodies.Length];
			for (int i = 0; i < _robots.Length; i++)
			{
				Robot robot = new Robot { Body = _bodies[i], Element = _elements[i], Route = _routes[i] };
				robot.Distance = NearestDistance(robot.Route, robot.Body.position);
				robot.Route.Evaluate(robot.Distance, out Vector3 direction);
				robot.Yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
				robot.Motor = new ParkLoop(_life, robot.Body, "Motor", _life.Settings.Robots.Motor, SoundPriority, 1f);
				_robots[i] = robot;
			}
		}

		#endregion

		#region Update

		private void Update()
		{
			ParkRobotSettings settings = _life.Settings.Robots;
			float deltaTime = Time.deltaTime;
			foreach (Robot robot in _robots)
			{
				if (robot.Route.Count < 2 || (robot.Element != null && !robot.Element.gameObject.activeInHierarchy))
				{
					continue;
				}

				Drive(robot, settings, deltaTime);
			}
		}

		/// <summary>The routes the robots drive back and forth.</summary>
		private void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.green;
			foreach (ParkRoute route in _routes)
			{
				route.DrawGizmo();
			}
		}

		#endregion

		#region Internal

		internal void Configure(ParkLife life, ParkLevelElement[] elements, Transform[] bodies, ParkRoute[] routes)
		{
			_life = life;
			_elements = elements;
			_bodies = bodies;
			_routes = routes;
		}

		#endregion

		#region Private

		private void Drive(Robot robot, ParkRobotSettings settings, float deltaTime)
		{
			float length = robot.Route.Length;
			if (robot.StopTimer > 0f)
			{
				robot.StopTimer -= deltaTime;
				robot.Speed = 0f;
				if (robot.StopTimer <= 0f)
				{
					Beep(robot);
				}
			}
			else
			{
				float remaining = robot.Direction > 0 ? length - robot.Distance : robot.Distance;
				float speedShare = Mathf.Max(MinimumSpeedShare, Mathf.Clamp01(remaining / SlowDistanceMeters));
				float target = settings.SpeedMetersPerSecond * speedShare;
				robot.Speed = Mathf.MoveTowards(robot.Speed, target, AccelerationMetersPerSecondSquared * deltaTime);
				robot.Distance += robot.Speed * robot.Direction * deltaTime;
				if (robot.Distance <= 0f || robot.Distance >= length)
				{
					robot.Distance = Mathf.Clamp(robot.Distance, 0f, length);
					robot.Direction = -robot.Direction;
					robot.StopTimer = UnityEngine.Random.Range(settings.StopSeconds.x, settings.StopSeconds.y);
					Beep(robot);
				}
			}

			Vector3 position = robot.Route.Evaluate(robot.Distance, out Vector3 direction);
			float yaw = Mathf.Atan2(direction.x * robot.Direction, direction.z * robot.Direction) * Mathf.Rad2Deg;
			robot.Yaw = Mathf.MoveTowardsAngle(robot.Yaw, yaw, settings.TurnDegreesPerSecond * deltaTime);
			float speed01 = Mathf.Clamp01(robot.Speed / settings.SpeedMetersPerSecond);
			float bump = BumpMeters * speed01 * Mathf.Sin(2f * Mathf.PI * BumpHertz * Time.time);
			robot.Body.SetPositionAndRotation(position + Vector3.up * bump, Quaternion.Euler(0f, robot.Yaw, 0f));

			float presence = robot.Element != null ? Mathf.Clamp01(robot.Element.Presence) : 1f;
			robot.Motor.Update((0.25f + 0.75f * speed01) * presence, 0.85f + 0.3f * speed01, deltaTime, 0.3f);
		}

		private void Beep(Robot robot)
		{
			_life.Sounds.Play(_life.Settings.Robots.Beeps, robot.Body.position + Vector3.up);
		}

		/// <summary>The distance along a route nearest to a position, sampled every half metre.</summary>
		private static float NearestDistance(ParkRoute route, Vector3 position)
		{
			float best = 0f, bestSquared = float.MaxValue, length = route.Length;
			for (float distance = 0f; distance <= length; distance += 0.5f)
			{
				float squared = (route.Evaluate(distance, out _) - position).sqrMagnitude;
				if (squared < bestSquared)
				{
					bestSquared = squared;
					best = distance;
				}
			}

			return best;
		}

		#endregion
	}
}
