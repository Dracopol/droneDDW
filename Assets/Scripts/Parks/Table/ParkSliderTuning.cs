using UnityEngine;

namespace DroneDDW.Parks.Table
{
	/// <summary>
	/// The scene's "Slider Tuning" object: points at the shared push and board follow settings, the slider's spheres view
	/// and the board, so selecting it in the Hierarchy shows every sphere, knob box and follow value in place (its
	/// inspector draws both assets), a switch for the spheres and boxes, and in Play mode what the board is doing. The
	/// values live in the assets, so changes made in Play mode are kept.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Slider Tuning")]
	public sealed class ParkSliderTuning : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField, Tooltip("Shared by every scene that uses it. Changes made in Play mode are kept.")]
		private ParkPushSettings _settings;

		[SerializeField, Tooltip("Shared by every scene that uses it. Changes made in Play mode are kept.")]
		private ParkBoardFollowSettings _followSettings;

		[SerializeField, Tooltip("The slider's Park Hand Spheres View, which draws the spheres and knob boxes.")]
		private Behaviour _view;

		[SerializeField, Tooltip("The board, whose follow state shows here in Play mode.")]
		private ParkBoardFollow _boardFollow;

		#endregion

		#region Properties

		internal ParkPushSettings Settings => _settings;
		internal ParkBoardFollowSettings FollowSettings => _followSettings;
		internal Behaviour View => _view;
		internal ParkBoardFollow BoardFollow => _boardFollow;

		#endregion

		#region Internal

		internal void Configure(ParkPushSettings settings, ParkBoardFollowSettings followSettings, Behaviour view,
			ParkBoardFollow boardFollow)
		{
			_settings = settings;
			_followSettings = followSettings;
			_view = view;
			_boardFollow = boardFollow;
		}

		#endregion
	}
}
