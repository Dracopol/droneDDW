using System;
using TMPro;
using UnityEngine;

namespace DroneDDW.StepControls
{
	// Turns a knob's position into one of its step percentages. Every step the knob moves onto counts straight away.
	internal class KnobStepper : MonoBehaviour
	{
		#region Events

		internal event Action<int> OnPercentChange;

		#endregion

		#region Serialized fields

		[SerializeField] private StepKnob _knob;

		[Tooltip("Selected when the scene starts, rounded to the nearest step")]
		[Range(0, 100)]
		[SerializeField] private int _initialPercent = 50;

		[Tooltip("How far past the halfway point between two steps the knob has to go before the next one counts, "
			+ "as a fraction of the step, so it doesn't flicker when resting in between")]
		[Range(0f, 0.5f)]
		[SerializeField] private float _stepHysteresis = 0.1f;

		[Header("Labels")]
		[SerializeField] private TMP_Text _label;

		[Tooltip("One per step, starting at 0%. The selected one is highlighted.")]
		[SerializeField] private TMP_Text[] _stepLabels;

		[SerializeField] private Color _stepLabelColor = Color.white;
		[SerializeField] private Color _selectedStepLabelColor = new(0.15f, 0.78f, 0.25f);

		[Header("Colors")]
		[SerializeField] private Renderer _knobRenderer;
		[SerializeField] private Material _heldMaterial;
		[SerializeField] private Material _settledMaterial;

		[Header("Sounds")]
		[SerializeField] private AudioSource _audioSource;
		[SerializeField] private AudioClip _grabClip;
		[SerializeField] private AudioClip _releaseClip;

		[Tooltip("Plays with the selection change clip each time the knob moves onto a new step")]
		[SerializeField] private AudioClip _notchClip;

		[SerializeField] private AudioClip _selectionChangeClip;

		#endregion

		#region Fields

		// Nothing is selected until Start picks the initial step
		private int _selectedStep = -1;
		private bool _wasHeld;

		#endregion

		#region Properties

		internal int SelectedPercent => StepToPercent(_selectedStep);

		#endregion

		#region Setup

		private void Start()
		{
			int initialStep = _knob.NearestStep(_initialPercent / 100f);
			_knob.SnapTo(_knob.StepPosition01(initialStep));
			Select(initialStep);
			RefreshColor(false);
		}

		#endregion

		#region Update

		private void LateUpdate()
		{
			bool isHeld = _knob.IsHeld;

			if (isHeld != _wasHeld)
			{
				_wasHeld = isHeld;
				_audioSource.PlayOneShot(isHeld ? _grabClip : _releaseClip);
				RefreshColor(isHeld);
			}

			float position01 = _knob.Position01;
			float distanceInSteps = Mathf.Abs(position01 - _knob.StepPosition01(_selectedStep)) / _knob.StepSize01;

			if (distanceInSteps < 0.5f + _stepHysteresis)
			{
				return;
			}

			Select(_knob.NearestStep(position01));
			_audioSource.PlayOneShot(_notchClip);
			_audioSource.PlayOneShot(_selectionChangeClip);
			OnPercentChange?.Invoke(SelectedPercent);
		}

		#endregion

		#region Private

		private void Select(int step)
		{
			_selectedStep = step;
			_label.text = $"{SelectedPercent}%";

			for (int index = 0; index < _stepLabels.Length; index++)
			{
				_stepLabels[index].color = index == step ? _selectedStepLabelColor : _stepLabelColor;
			}

			Debug.Log($"[DDW Step Controls] Selected {SelectedPercent}%", this);
		}

		private void RefreshColor(bool isHeld)
		{
			_knobRenderer.sharedMaterial = isHeld ? _heldMaterial : _settledMaterial;
		}

		private int StepToPercent(int step)
		{
			return Mathf.RoundToInt(100f * step / (_knob.StepCount - 1));
		}

		#endregion
	}
}
