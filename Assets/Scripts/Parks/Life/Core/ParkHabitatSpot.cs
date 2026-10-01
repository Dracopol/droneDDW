using System;
using DroneDDW.Parks.Levels;
using UnityEngine;

namespace DroneDDW.Parks.Life
{
	/// <summary>
	/// A place in the park where life happens (a tree canopy, a flower clump, a meadow, a charger), tied to the park
	/// object that is there. Its presence follows that object through the levels, so life comes and goes with it.
	/// The position is fixed: an object lifting out of the ground moves, the spot does not.
	/// </summary>
	[Serializable]
	public struct ParkHabitatSpot
	{
		#region Serialized fields

		[SerializeField] private ParkLevelElement _element;
		[SerializeField] private Vector3 _position;
		[SerializeField] private float _radiusMeters;

		#endregion

		#region Properties

		public ParkLevelElement Element => _element;
		public Vector3 Position => _position;
		public float RadiusMeters => _radiusMeters;

		/// <summary>0 when its object is gone, 1 when fully in place. Always 1 for a spot without an object.</summary>
		public float Presence
		{
			get
			{
				if (_element == null)
				{
					return 1f;
				}

				return _element.gameObject.activeInHierarchy ? Mathf.Clamp01(_element.Presence) : 0f;
			}
		}

		#endregion

		#region Setup

		public ParkHabitatSpot(ParkLevelElement element, Vector3 position, float radiusMeters)
		{
			_element = element;
			_position = position;
			_radiusMeters = radiusMeters;
		}

		#endregion
	}
}
