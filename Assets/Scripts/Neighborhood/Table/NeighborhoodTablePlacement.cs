using UnityEngine;

namespace DroneDDW.Neighborhood.Table
{
	/// <summary>
	/// Puts the table where its settings say, measured from the seat: its near edge that far in front of the eyes and its
	/// top that high above the seat's floor. Everything on the table moves with it; the legs stretch down to the floor.
	/// The scene builders apply it once; the table tuning scene applies it every frame while the settings change.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Neighborhood/Table Placement")]
	public sealed class NeighborhoodTablePlacement : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private NeighborhoodTableSettings _settings;

		[SerializeField, Tooltip("The seat: on the floor, under the eyes.")]
		private Vector3 _seat;

		[SerializeField, Tooltip("The table's near edge, along its forward axis, in its own space.")]
		private float _nearEdgeLocalZ;

		[SerializeField, Tooltip("The top's upper face, in the table's own space.")]
		private float _topLocalY;

		[SerializeField, Tooltip("The top's thickness: the legs end under it.")]
		private float _topThicknessMeters;

		[SerializeField, Tooltip("The legs, a mesh one metre high standing on its pivot, stretched from the floor to the "
			+ "top.")]
		private Transform _legs;

		#endregion

		#region Properties

		public NeighborhoodTableSettings Settings => NeighborhoodTableSettings.OrDefaults(_settings);

		/// <summary>The seat, on the floor under the eyes.</summary>
		public Vector3 Seat => _seat;

		#endregion

		#region Public

		/// <summary>Moves the table to its settings and stretches its legs to the floor.</summary>
		public void Apply()
		{
			NeighborhoodTableSettings settings = Settings;
			float lift = settings.TopHeightMeters - _topLocalY;
			transform.SetPositionAndRotation(
				_seat + new Vector3(0f, lift, settings.EdgeDistanceMeters - _nearEdgeLocalZ), Quaternion.identity);
			if (_legs != null)
			{
				_legs.localPosition = new Vector3(0f, -lift, 0f);
				_legs.localScale = new Vector3(1f, Mathf.Max(0.01f, settings.TopHeightMeters - _topThicknessMeters), 1f);
			}
		}

		/// <summary>How far in front of the eyes a point is, along the floor.</summary>
		public float GetDistanceFromEyes(Vector3 point)
		{
			return point.z - _seat.z;
		}

		#endregion

		#region Internal

		internal void Configure(NeighborhoodTableSettings settings, Vector3 seat, float nearEdgeLocalZ, float topLocalY,
			float topThicknessMeters, Transform legs)
		{
			_settings = settings;
			_seat = seat;
			_nearEdgeLocalZ = nearEdgeLocalZ;
			_topLocalY = topLocalY;
			_topThicknessMeters = topThicknessMeters;
			_legs = legs;
		}

		#endregion
	}
}
