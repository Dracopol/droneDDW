using DroneDDW.Parks.Audio;
using DroneDDW.Parks.Levels;
using DroneDDW.Parks.Table;
using UnityEngine;

namespace DroneDDW.Parks.Experience
{
	/// <summary>
	/// Wires the pieces together through their events:
	/// slider dragged over a detent, the table model previews that level and the ambience leans toward it;
	/// slider released, the park moves to that level;
	/// park level changed (from the slider, keys or code), model, slider and ambience follow.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Experience")]
	public sealed class ParkExperience : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private ParkLevelController _controller;
		[SerializeField] private ParkLevelSlider _slider;
		[SerializeField] private ParkModelPreview _preview;

		[SerializeField, Tooltip("Optional.")]
		private ParkAmbience _ambience;

		#endregion

		#region Setup

		private void OnEnable()
		{
			_controller.OnLevelChange += HandleLevelChange;
			if (_slider != null)
			{
				_slider.OnDetentChange += HandleDetentChange;
				_slider.OnLevelCommit += HandleLevelCommit;
			}
		}

		private void OnDisable()
		{
			_controller.OnLevelChange -= HandleLevelChange;
			if (_slider != null)
			{
				_slider.OnDetentChange -= HandleDetentChange;
				_slider.OnLevelCommit -= HandleLevelCommit;
			}
		}

		private void Start()
		{
			// The controller applied its start level in Awake, before this object subscribed.
			int level = _controller.CurrentLevel;
			if (_preview != null)
			{
				_preview.ShowLevel(level);
			}

			if (_slider != null)
			{
				_slider.SetDetent(level, false);
			}

			if (_ambience != null)
			{
				_ambience.SetLevelImmediate(level);
			}
		}

		#endregion

		#region Event handlers

		private void HandleDetentChange(int level)
		{
			if (_preview != null)
			{
				_preview.ShowLevel(level);
			}

			if (_ambience != null)
			{
				_ambience.PreviewLevel(level);
			}
		}

		private void HandleLevelCommit(int level)
		{
			if (_ambience != null)
			{
				// Released on the park's level, this leans back; a real change fades on from the lean instead.
				_ambience.EndPreview();
			}

			_controller.RequestLevel(level);
		}

		private void HandleLevelChange(ParkLevelChange change)
		{
			if (_preview != null)
			{
				_preview.ShowLevel(change.Next);
			}

			if (_slider != null && !_slider.IsDragging && _slider.Detent != change.Next)
			{
				_slider.SetDetent(change.Next, !change.IsImmediate);
			}

			if (_ambience == null)
			{
				return;
			}

			if (change.IsImmediate)
			{
				_ambience.SetLevelImmediate(change.Next);
			}
			else if (change.Previous != change.Next)
			{
				// A repeated request for the level the park is already moving to keeps the running fade.
				_ambience.FadeToLevel(change.Next);
			}
		}

		#endregion

		#region Internal

		internal void Configure(ParkLevelController controller, ParkLevelSlider slider, ParkModelPreview preview, ParkAmbience ambience)
		{
			_controller = controller;
			_slider = slider;
			_preview = preview;
			_ambience = ambience;
		}

		#endregion
	}
}
