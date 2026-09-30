using System;
using TMPro;
using UnityEngine;

namespace DroneDDW.Buttons
{
	// Keeps exactly one percentage selected across its buttons and shows it on the label
	internal class ButtonStepper : MonoBehaviour
	{
		#region Events

		internal event Action<int> OnPercentChange;

		#endregion

		#region Serialized fields

		[SerializeField] private SpringButton[] _buttons;

		[Tooltip("Selected when the scene starts, has to match one of the buttons")]
		[Range(0, 100)]
		[SerializeField] private int _initialPercent = 50;

		[SerializeField] private TMP_Text _label;

		[SerializeField] private AudioSource _audioSource;

		[Tooltip("Plays when the selection moves to a different percentage")]
		[SerializeField] private AudioClip _selectionChangeClip;

		#endregion

		#region Fields

		// Nothing is selected until Start picks the initial percentage
		private int _selectedPercent = -1;

		#endregion

		#region Properties

		internal int SelectedPercent => _selectedPercent;

		#endregion

		#region Setup

		private void OnEnable()
		{
			foreach (SpringButton button in _buttons)
			{
				button.OnPress += HandleButtonPress;
			}
		}

		private void OnDisable()
		{
			foreach (SpringButton button in _buttons)
			{
				button.OnPress -= HandleButtonPress;
			}
		}

		private void Start()
		{
			SpringButton initialButton = Array.Find(_buttons, button => button.Percent == _initialPercent);

			if (initialButton == null)
			{
				Debug.LogWarning($"[DDW Buttons] No button for the initial {_initialPercent}%", this);
				return;
			}

			Select(initialButton);
		}

		#endregion

		#region Event handlers

		// Pressing the selected button again keeps it selected, so an answer can't be cleared by accident
		private void HandleButtonPress(SpringButton button)
		{
			if (button.Percent == _selectedPercent)
			{
				return;
			}

			Select(button);
			_audioSource.PlayOneShot(_selectionChangeClip);
			OnPercentChange?.Invoke(_selectedPercent);
		}

		#endregion

		#region Private

		private void Select(SpringButton selectedButton)
		{
			_selectedPercent = selectedButton.Percent;

			foreach (SpringButton button in _buttons)
			{
				button.SetSelected(button.Percent == _selectedPercent);
			}

			_label.text = selectedButton.PercentText;
			Debug.Log($"[DDW Buttons] Selected {selectedButton.PercentText}", this);
		}

		#endregion
	}
}
