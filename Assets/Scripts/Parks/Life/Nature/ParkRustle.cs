using System;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// Leaf rustle from the big trees nearest the seat. Each loop swells with the wind at its tree, so a gust is heard
	/// rolling through the crowns as it bends them. When a tree leaves the park its loop fades out, then moves to the
	/// nearest shown tree not already rustling.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Life/Park Rustle")]
	public sealed class ParkRustle : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private ParkLife _life;
		[SerializeField] private ParkWind _wind;

		[SerializeField, Tooltip("Crowns of the big trees near the seat, nearest first.")]
		private ParkHabitatSpot[] _crowns = Array.Empty<ParkHabitatSpot>();

		#endregion

		#region Fields

		private const float CalmShare = 0.25f;
		private const int SoundPriority = 170;

		private ParkLoop[] _loops = Array.Empty<ParkLoop>();
		private int[] _assigned = Array.Empty<int>();

		#endregion

		#region Setup

		private void Start()
		{
			int count = Mathf.Min(_crowns.Length, _life.Settings.Wind.RustleTrees);
			_loops = new ParkLoop[count];
			_assigned = new int[count];
			for (int i = 0; i < count; i++)
			{
				_loops[i] = new ParkLoop(_life, transform, $"Rustle {i + 1}", _life.Settings.Wind.Rustle, SoundPriority, 0f);
				_assigned[i] = -1;
			}
		}

		#endregion

		#region Update

		private void Update()
		{
			ParkWindSettings settings = _life.Settings.Wind;
			float full = Mathf.Max(0.01f, settings.Breeze + settings.GustStrength);
			for (int i = 0; i < _loops.Length; i++)
			{
				int crown = _assigned[i];
				float presence = crown >= 0 ? _crowns[crown].Presence : 0f;
				if (presence < 0.5f && _loops[i].Level < 0.01f)
				{
					crown = NearestFree();
					_assigned[i] = crown;
					if (crown >= 0)
					{
						_loops[i].Transform.position = _crowns[crown].Position;
						presence = _crowns[crown].Presence;
					}
				}

				float wind = crown >= 0 ? Mathf.Clamp01(_wind.StrengthAt(_crowns[crown].Position) / full) : 0f;
				_loops[i].Update(presence * Mathf.Lerp(CalmShare, 1f, wind), 0.9f + 0.2f * wind, Time.deltaTime, 0.6f);
			}
		}

		#endregion

		#region Internal

		internal void Configure(ParkLife life, ParkWind wind, ParkHabitatSpot[] crowns)
		{
			_life = life;
			_wind = wind;
			_crowns = crowns;
		}

		#endregion

		#region Private

		/// <summary>The nearest shown crown no loop is using (the crowns come nearest first), or -1.</summary>
		private int NearestFree()
		{
			for (int crown = 0; crown < _crowns.Length; crown++)
			{
				if (_crowns[crown].Presence >= 0.5f && Array.IndexOf(_assigned, crown) < 0)
				{
					return crown;
				}
			}

			return -1;
		}

		#endregion
	}
}
