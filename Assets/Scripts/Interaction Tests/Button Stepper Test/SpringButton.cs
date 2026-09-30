using System;
using TMPro;
using UnityEngine;

namespace DroneDDW.Buttons
{
	// A button the hands push down, shows idle / pressed / selected colors and plays its own sounds.
	// Whatever moves the cap only has to report how far down it is.
	internal class SpringButton : MonoBehaviour
	{
		#region Events

		internal event Action<SpringButton> OnPress;

		#endregion

		#region Serialized fields

		[SerializeField] private ButtonCap _cap;

		[Range(0, 100)]
		[SerializeField] private int _percent;

		[Tooltip("Printed on the cap, set from the percent so the two can't disagree")]
		[SerializeField] private TMP_Text _valueText;

		[Header("Colors")]
		[SerializeField] private Renderer _capRenderer;
		[SerializeField] private Material _idleMaterial;
		[SerializeField] private Material _pressedMaterial;
		[SerializeField] private Material _selectedMaterial;

		[Header("Sounds")]
		[SerializeField] private AudioSource _audioSource;
		[SerializeField] private AudioClip _touchClip;
		[SerializeField] private AudioClip _pressClip;
		[SerializeField] private AudioClip _releaseClip;

		[Header("Thresholds, as a fraction of the cap's travel")]
		[Tooltip("How far the cap has to go down before the touch sound plays")]
		[Range(0f, 1f)]
		[SerializeField] private float _touchThreshold = 0.05f;

		[Tooltip("The cap has to come back up past this before the touch sound can play again")]
		[Range(0f, 1f)]
		[SerializeField] private float _touchResetThreshold = 0.01f;

		[Range(0f, 1f)]
		[SerializeField] private float _pressThreshold = 0.8f;

		[Tooltip("Below the press threshold, so a hand resting near it doesn't flicker between pressed and released")]
		[Range(0f, 1f)]
		[SerializeField] private float _releaseThreshold = 0.4f;

		#endregion

		#region Fields

		private bool _isTouched;
		private bool _isPressed;
		private bool _isSelected;

		#endregion

		#region Properties

		internal int Percent => _percent;

		internal string PercentText => $"{_percent}%";

		#endregion

		#region Setup

		private void Awake()
		{
			_valueText.text = PercentText;
			RefreshColor();
		}

		#endregion

		#region Update

		private void LateUpdate()
		{
			float depth = _cap.PressDepth01;

			if (!_isTouched && depth >= _touchThreshold)
			{
				_isTouched = true;
				_audioSource.PlayOneShot(_touchClip);
			}
			else if (_isTouched && depth <= _touchResetThreshold)
			{
				_isTouched = false;
			}

			if (!_isPressed && depth >= _pressThreshold)
			{
				Press();
			}
			else if (_isPressed && depth <= _releaseThreshold)
			{
				Release();
			}
		}

		#endregion

		#region Internal

		internal void SetSelected(bool isSelected)
		{
			_isSelected = isSelected;
			RefreshColor();
		}

		#endregion

		#region Private

		private void Press()
		{
			_isPressed = true;
			_audioSource.PlayOneShot(_pressClip);
			RefreshColor();

			Debug.Log($"[DDW Buttons] Pressed {PercentText}", this);
			OnPress?.Invoke(this);
		}

		private void Release()
		{
			_isPressed = false;
			_audioSource.PlayOneShot(_releaseClip);
			RefreshColor();
		}

		// Held down shows yellow even on the selected button, green returns once it's released
		private void RefreshColor()
		{
			Material material = _idleMaterial;

			if (_isPressed)
			{
				material = _pressedMaterial;
			}
			else if (_isSelected)
			{
				material = _selectedMaterial;
			}

			_capRenderer.sharedMaterial = material;
		}

		#endregion
	}
}
