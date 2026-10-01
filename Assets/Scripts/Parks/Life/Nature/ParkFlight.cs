using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>Small shared pieces of animal motion: arcing flights, wingbeats and facing a direction.</summary>
	internal static class ParkFlight
	{
		#region Internal

		/// <summary>A point on a flight from one place to another, rising by the arc height at the middle.</summary>
		internal static Vector3 Arc(Vector3 from, Vector3 to, float t, float arcMeters)
		{
			return Vector3.Lerp(from, to, t) + Vector3.up * (arcMeters * 4f * t * (1f - t));
		}

		/// <summary>
		/// A wing angle for flying in bursts: flapping for part of each cycle, gliding with wings held slightly up for the
		/// rest. The phase keeps birds of a flock out of step.
		/// </summary>
		internal static float FlapGlide(float time, float phase, float flapHertz, float amplitudeDegrees, float flapShare,
			float cycleSeconds)
		{
			float cycle = Mathf.Repeat(time + phase, cycleSeconds) / cycleSeconds;
			return cycle < flapShare
				? amplitudeDegrees * Mathf.Sin(2f * Mathf.PI * flapHertz * (time + phase))
				: amplitudeDegrees * 0.12f;
		}

		/// <summary>Faces along a direction on the ground, tipped up or down by a pitch.</summary>
		internal static Quaternion Facing(Vector3 direction, float pitchDegrees = 0f, float rollDegrees = 0f)
		{
			Vector3 flat = new Vector3(direction.x, 0f, direction.z);
			float yaw = flat.sqrMagnitude > 1e-6f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : 0f;
			return Quaternion.Euler(pitchDegrees, yaw, rollDegrees);
		}

		/// <summary>A random point on the ground within a radius of a centre.</summary>
		internal static Vector3 Around(Vector3 center, float radiusMeters)
		{
			Vector2 offset = Random.insideUnitCircle * radiusMeters;
			return center + new Vector3(offset.x, 0f, offset.y);
		}

		#endregion
	}
}
