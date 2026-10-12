using DroneDDW.Neighborhood.Table;
using DroneDDW.Neighborhood.Versions;
using TMPro;
using UnityEngine;

namespace DroneDDW.Experience.TestScene
{
	// The lock button test scenes' readout, on a label above the table: the slider's notch, how far the button's cap is
	// down, how full its ring is, and the choice locked in. A few seconds after a lock-in it resets the button, so it can
	// be tried again and again.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/Test Scene/Lock Button Test Readout")]
	internal sealed class LockButtonTestReadout : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private NeighborhoodLockButton _lockButton;
		[SerializeField] private NeighborhoodSlider _slider;
		[SerializeField] private TMP_Text _label;

		[Tooltip("How long a lock-in shows before the button resets")]
		[Min(0.5f)]
		[SerializeField] private float _resetSeconds = 3f;

		#endregion

		#region Fields

		private float _lockedAtSeconds = -1f;
		private int _lockCount;

		#endregion

		#region Setup

		private void OnEnable()
		{
			_lockButton.OnLockIn += HandleLockIn;
		}

		private void OnDisable()
		{
			_lockButton.OnLockIn -= HandleLockIn;
		}

		// The slider starts on the notch its handle stands at, so the button can be tried straight away
		private void Start()
		{
			_slider.SetDetent(Mathf.RoundToInt(_slider.HandleBlend * (_slider.DetentCount - 1)), false);
		}

		#endregion

		#region Update

		private void Update()
		{
			if (_lockedAtSeconds >= 0f && Time.time - _lockedAtSeconds >= _resetSeconds)
			{
				_lockedAtSeconds = -1f;
				_lockButton.ResetLock();
			}

			_label.text = $"Slider: {DescribeSlider()}\nButton: {DescribeButton()}\nLocked in {_lockCount} times";
		}

		#endregion

		#region Event handlers

		private void HandleLockIn(int detent)
		{
			_lockedAtSeconds = Time.time;
			_lockCount++;
			Debug.Log($"[Experience] Lock button test: locked in {NeighborhoodVersions.Clamp(detent)}.", this);
		}

		#endregion

		#region Internal

		internal void Configure(NeighborhoodLockButton lockButton, NeighborhoodSlider slider, TMP_Text label)
		{
			_lockButton = lockButton;
			_slider = slider;
			_label = label;
		}

		#endregion

		#region Private

		private string DescribeSlider()
		{
			if (_slider.IsDragging || _slider.Detent < 0)
			{
				return "moving";
			}

			return NeighborhoodVersions.Clamp(_slider.Detent).ToString();
		}

		private string DescribeButton()
		{
			if (_lockButton.IsLocked)
			{
				float remaining = Mathf.Max(0f, _resetSeconds - (Time.time - _lockedAtSeconds));
				return $"locked in {NeighborhoodVersions.Clamp(_lockButton.LockedDetent)}, resets in {remaining:0} s";
			}

			if (!_lockButton.IsArmed)
			{
				return "waiting for the slider to rest on a notch";
			}

			if (_lockButton.IsPressed)
			{
				return $"held, ring {_lockButton.Progress:P0}";
			}

			return _lockButton.PressDepth01 > 0.01f ? $"pushed {_lockButton.PressDepth01:P0}" : "ready";
		}

		#endregion
	}
}
