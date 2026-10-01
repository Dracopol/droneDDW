using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// Sets up audio sources for park sounds: fully 3D, through Meta XR Audio's head-related filtering when
	/// spatialized, falling off with distance from the sound's near distance and fading to silence by its far
	/// distance (Unity's own logarithmic curve never reaches zero, so a loop stopping at range would jump).
	/// </summary>
	internal static class ParkLifeAudio
	{
		#region Fields

		private const int CurveKeys = 16;
		private const float FadeStartShare = 0.7f;

		private static readonly Dictionary<int, AnimationCurve> Curves = new Dictionary<int, AnimationCurve>();

		#endregion

		#region Internal

		internal static AudioSource CreateSource(Transform parent, string name)
		{
			GameObject sourceObject = new GameObject(name);
			sourceObject.transform.SetParent(parent, false);
			AudioSource source = sourceObject.AddComponent<AudioSource>();
			source.playOnAwake = false;
			return source;
		}

		internal static void Configure(AudioSource source, ParkLifeSound sound, bool isSpatialized, int priority,
			float dopplerLevel)
		{
			source.spatialBlend = 1f;
			source.spatialize = isSpatialized;
			source.dopplerLevel = dopplerLevel;
			source.priority = priority;
			source.minDistance = sound.NearMeters;
			source.maxDistance = sound.FarMeters;
			source.rolloffMode = AudioRolloffMode.Custom;
			source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, GetRolloff(sound.NearMeters / sound.FarMeters));
		}

		#endregion

		#region Private

		/// <summary>
		/// Gain against distance as a share of the far distance: 1 up to the near share, then inverse distance, eased
		/// to zero over the last 30 percent. Keys are spaced geometrically, where the inverse curve bends most.
		/// </summary>
		private static AnimationCurve GetRolloff(float nearShare)
		{
			nearShare = Mathf.Clamp(nearShare, 0.001f, 0.99f);
			int key = Mathf.RoundToInt(nearShare * 10000f);
			if (Curves.TryGetValue(key, out AnimationCurve cached))
			{
				return cached;
			}

			Keyframe[] keys = new Keyframe[CurveKeys + 1];
			keys[0] = new Keyframe(0f, 1f);
			for (int i = 1; i <= CurveKeys; i++)
			{
				float share = nearShare * Mathf.Pow(1f / nearShare, (i - 1) / (float)(CurveKeys - 1));
				float fade = share <= FadeStartShare
					? 1f
					: 1f - Mathf.SmoothStep(0f, 1f, (share - FadeStartShare) / (1f - FadeStartShare));
				keys[i] = new Keyframe(share, nearShare / share * fade);
			}

			AnimationCurve curve = new AnimationCurve(keys);
			for (int i = 0; i < keys.Length; i++)
			{
				curve.SmoothTangents(i, 0f);
			}

			Curves[key] = curve;
			return curve;
		}

		#endregion
	}
}
