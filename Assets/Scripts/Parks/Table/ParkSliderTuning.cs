using UnityEngine;

namespace DroneDDW.Parks.Table
{
	/// <summary>
	/// The scene's "Slider Tuning" object: points at the shared push settings and the slider's spheres view, so selecting
	/// it in the Hierarchy shows every sphere and knob box value in place (its inspector draws the asset) with a switch
	/// for the spheres and boxes. The values live in the asset, so changes made in Play mode are kept.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Slider Tuning")]
	public sealed class ParkSliderTuning : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField, Tooltip("Shared by every scene that uses it. Changes made in Play mode are kept.")]
		private ParkPushSettings _settings;

		[SerializeField, Tooltip("The slider's Park Hand Spheres View, which draws the spheres and knob boxes.")]
		private Behaviour _view;

		#endregion

		#region Properties

		internal ParkPushSettings Settings => _settings;
		internal Behaviour View => _view;

		#endregion

		#region Internal

		internal void Configure(ParkPushSettings settings, Behaviour view)
		{
			_settings = settings;
			_view = view;
		}

		#endregion
	}
}
