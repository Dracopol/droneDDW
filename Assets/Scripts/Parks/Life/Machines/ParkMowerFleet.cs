using System;
using DroneDDW.Parks.Levels;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// Drives the robot mowers in stripes across their lawns: straight runs along the lawn, joined by U-turns one stripe
	/// further on, stepping back the other way once they reach the lawn's edge. Each mower hums while it works.
	/// Each mower is a level element wrapping its body: the level transition scales the wrapper, this moves the body.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Mower Fleet")]
	public sealed class ParkMowerFleet : MonoBehaviour
	{
		#region Entities

		private sealed class Mower
		{
			public Transform Body;
			public ParkLevelElement Element;
			public Rect Lawn;
			public ParkLoop Sound;
			public float X;
			public float Z;
			public int RunDirection = 1;
			public int StepDirection = 1;
			public bool IsTurning;
			public float TurnAngle;
			public Vector2 TurnCenter;
			public float Yaw;
		}

		#endregion

		#region Serialized fields

		[SerializeField] private ParkLife _life;
		[SerializeField] private ParkLevelElement[] _elements = Array.Empty<ParkLevelElement>();
		[SerializeField] private Transform[] _bodies = Array.Empty<Transform>();

		[SerializeField, Tooltip("The lawn each mower works, on the ground plane (x and z).")]
		private Rect[] _lawns = Array.Empty<Rect>();

		#endregion

		#region Fields

		private const float EdgeMarginMeters = 0.6f;
		private const int SoundPriority = 96;

		private Mower[] _mowers = Array.Empty<Mower>();

		#endregion

		#region Setup

		private void Start()
		{
			_mowers = new Mower[_bodies.Length];
			for (int i = 0; i < _mowers.Length; i++)
			{
				Vector3 start = _bodies[i].position;
				Rect lawn = _lawns[i];
				Mower mower = new Mower
				{
					Body = _bodies[i],
					Element = _elements[i],
					Lawn = lawn,
					X = Mathf.Clamp(start.x, lawn.xMin + EdgeMarginMeters, lawn.xMax - EdgeMarginMeters),
					Z = Mathf.Clamp(start.z, lawn.yMin + EdgeMarginMeters, lawn.yMax - EdgeMarginMeters),
				};
				mower.Yaw = mower.RunDirection > 0 ? 90f : -90f;
				mower.Sound = new ParkLoop(_life, mower.Body, "Mower", _life.Settings.Robots.Mower, SoundPriority, 1f);
				_mowers[i] = mower;
			}
		}

		#endregion

		#region Update

		private void Update()
		{
			ParkRobotSettings settings = _life.Settings.Robots;
			float deltaTime = Time.deltaTime;
			foreach (Mower mower in _mowers)
			{
				if (mower.Element != null && !mower.Element.gameObject.activeInHierarchy)
				{
					continue;
				}

				Mow(mower, settings, deltaTime);
			}
		}

		/// <summary>The lawn each mower stripes.</summary>
		private void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.green;
			foreach (Rect lawn in _lawns)
			{
				Gizmos.DrawWireCube(new Vector3(lawn.center.x, 0.05f, lawn.center.y), new Vector3(lawn.width, 0f, lawn.height));
			}
		}

		#endregion

		#region Internal

		internal void Configure(ParkLife life, ParkLevelElement[] elements, Transform[] bodies, Rect[] lawns)
		{
			_life = life;
			_elements = elements;
			_bodies = bodies;
			_lawns = lawns;
		}

		#endregion

		#region Private

		private void Mow(Mower mower, ParkRobotSettings settings, float deltaTime)
		{
			float speed = settings.MowerSpeedMetersPerSecond;
			float radius = settings.StripeSpacingMeters * 0.5f;
			float xMin = mower.Lawn.xMin + EdgeMarginMeters, xMax = mower.Lawn.xMax - EdgeMarginMeters;
			if (!mower.IsTurning)
			{
				mower.X += mower.RunDirection * speed * deltaTime;
				mower.Yaw = mower.RunDirection > 0 ? 90f : -90f;
				if ((mower.RunDirection > 0 && mower.X >= xMax) || (mower.RunDirection < 0 && mower.X <= xMin))
				{
					mower.X = Mathf.Clamp(mower.X, xMin, xMax);
					float nextZ = mower.Z + mower.StepDirection * settings.StripeSpacingMeters;
					if (nextZ < mower.Lawn.yMin + EdgeMarginMeters || nextZ > mower.Lawn.yMax - EdgeMarginMeters)
					{
						mower.StepDirection = -mower.StepDirection;
					}

					mower.IsTurning = true;
					mower.TurnAngle = 0f;
					mower.TurnCenter = new Vector2(mower.X, mower.Z + mower.StepDirection * radius);
				}
			}
			else
			{
				// A half circle one stripe wide around the turn centre: it leaves the run going straight on and
				// joins the next stripe heading back, so the path stays smooth at both ends
				mower.TurnAngle = Mathf.Min(Mathf.PI, mower.TurnAngle + speed / radius * deltaTime);
				float sin = Mathf.Sin(mower.TurnAngle), cos = Mathf.Cos(mower.TurnAngle);
				mower.X = mower.TurnCenter.x + radius * sin * mower.RunDirection;
				mower.Z = mower.TurnCenter.y - radius * cos * mower.StepDirection;
				mower.Yaw = Mathf.Atan2(cos * mower.RunDirection, sin * mower.StepDirection) * Mathf.Rad2Deg;
				if (mower.TurnAngle >= Mathf.PI)
				{
					mower.IsTurning = false;
					mower.RunDirection = -mower.RunDirection;
				}
			}

			Vector3 position = new Vector3(mower.X, mower.Body.position.y, mower.Z);
			mower.Body.SetPositionAndRotation(position, Quaternion.Euler(0f, mower.Yaw, 0f));
			float presence = mower.Element != null ? Mathf.Clamp01(mower.Element.Presence) : 1f;
			mower.Sound.Update(presence, 1f + 0.02f * Mathf.Sin(Time.time * 1.3f), deltaTime, 0.5f);
		}

		#endregion
	}
}
