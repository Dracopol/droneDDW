using UnityEngine;

namespace DroneDDW.Parks.Table
{
	/// <summary>
	/// How the floating board follows the viewer, shared by every scene whose board uses this asset (the park and the
	/// slider tuning scene): where its top sits relative to the eyes, how far the head may turn or move before it follows,
	/// and how quickly it swings round with a turn and moves along with the head. Read live, so changes apply at once;
	/// an asset, so changes made in Play mode are kept.
	/// </summary>
	[CreateAssetMenu(fileName = "Park Board Follow Settings", menuName = "DroneDDW/Parks/Board Follow Settings")]
	public sealed class ParkBoardFollowSettings : ScriptableObject
	{
		#region Serialized fields

		[Header("Position (the board's top, relative to the eyes)")]
		[SerializeField, Range(0.45f, 1.2f), Tooltip("How far ahead of the eyes the middle of the board sits. The slider "
			+ "is 43 cm nearer than that.")]
		private float _distanceMeters = 0.75f;

		[SerializeField, Range(0.1f, 0.8f), Tooltip("How far below the eyes the board's top sits.")]
		private float _dropMeters = 0.48f;

		[Header("Dead zone")]
		[SerializeField, Range(0f, 90f), Tooltip("How far the head turns before the board follows.")]
		private float _startAngleDegrees = 20f;

		[SerializeField, Range(0f, 1f), Tooltip("How far the head moves before the board follows. Large on purpose, so "
			+ "leaning forward doesn't push the board away; standing up or sitting down does move it.")]
		private float _startDistanceMeters = 0.25f;

		[Header("Speed (lower is faster)")]
		[SerializeField, Range(0.05f, 2f), Tooltip("Roughly how long the board takes to swing round with a head turn.")]
		private float _turnTimeSeconds = 0.3f;

		[SerializeField, Range(0.05f, 2f), Tooltip("Roughly how long the board takes to follow the head moving: "
			+ "standing up, sitting down, leaning.")]
		private float _moveTimeSeconds = 0.3f;

		#endregion

		#region Fields

		private static ParkBoardFollowSettings _defaults;

		#endregion

		#region Properties

		internal float DistanceMeters => _distanceMeters;
		internal float DropMeters => _dropMeters;
		internal float StartAngleDegrees => _startAngleDegrees;
		internal float StartDistanceMeters => _startDistanceMeters;
		internal float TurnTimeSeconds => _turnTimeSeconds;
		internal float MoveTimeSeconds => _moveTimeSeconds;

		/// <summary>The board's top relative to the eyes, before turning it with the head's yaw.</summary>
		internal Vector3 OffsetMeters => new Vector3(0f, -_dropMeters, _distanceMeters);

		#endregion

		#region Internal

		/// <summary>The given settings, or an instance with the defaults for a board that has none.</summary>
		internal static ParkBoardFollowSettings OrDefaults(ParkBoardFollowSettings settings)
		{
			if (settings != null)
			{
				return settings;
			}

			if (_defaults == null)
			{
				_defaults = CreateInstance<ParkBoardFollowSettings>();
				_defaults.hideFlags = HideFlags.HideAndDontSave;
			}

			return _defaults;
		}

		#endregion
	}
}
