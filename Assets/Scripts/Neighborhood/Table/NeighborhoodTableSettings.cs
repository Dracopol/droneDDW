using UnityEngine;

namespace DroneDDW.Neighborhood.Table
{
	/// <summary>
	/// Where the table stands, shared by every scene whose table uses this asset (the neighborhood, the experience's
	/// practice table and the table tuning scene): how far in front of the eyes its near edge is, and how high its top is
	/// above the floor the seat stands on. Read when the scenes are built, and live in the table tuning scene; an asset,
	/// so changes made in Play mode are kept.
	/// </summary>
	[CreateAssetMenu(fileName = "Neighborhood Table Settings", menuName = "DroneDDW/Neighborhood/Table Settings")]
	public sealed class NeighborhoodTableSettings : ScriptableObject
	{
		#region Serialized fields

		[SerializeField, Range(MinEdgeDistanceMeters, MaxEdgeDistanceMeters), Tooltip("How far in front of the eyes the "
			+ "table's near edge is, along the floor. The lock-in button, the slider and the map follow behind it.")]
		private float _edgeDistanceMeters = 0.25f;

		[SerializeField, Range(MinTopHeightMeters, MaxTopHeightMeters), Tooltip("How high the table's top is above the "
			+ "floor the seat stands on. The eyes are 1.2 m above that floor.")]
		private float _topHeightMeters = 0.72f;

		#endregion

		#region Fields

		public const float MinEdgeDistanceMeters = 0.05f;
		public const float MaxEdgeDistanceMeters = 0.8f;
		public const float MinTopHeightMeters = 0.55f;
		public const float MaxTopHeightMeters = 1f;

		private static NeighborhoodTableSettings _defaults;

		#endregion

		#region Properties

		public float EdgeDistanceMeters
		{
			get => _edgeDistanceMeters;
			set => _edgeDistanceMeters = Mathf.Clamp(value, MinEdgeDistanceMeters, MaxEdgeDistanceMeters);
		}

		public float TopHeightMeters
		{
			get => _topHeightMeters;
			set => _topHeightMeters = Mathf.Clamp(value, MinTopHeightMeters, MaxTopHeightMeters);
		}

		#endregion

		#region Internal

		/// <summary>The given settings, or an instance with the defaults.</summary>
		internal static NeighborhoodTableSettings OrDefaults(NeighborhoodTableSettings settings)
		{
			if (settings != null)
			{
				return settings;
			}

			if (_defaults == null)
			{
				_defaults = CreateInstance<NeighborhoodTableSettings>();
				_defaults.hideFlags = HideFlags.HideAndDontSave;
			}

			return _defaults;
		}

		#endregion
	}
}
