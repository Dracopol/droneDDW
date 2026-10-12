using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.NarratorOrb;
using DroneDDW.Neighborhood.Table;
using UnityEngine;

namespace DroneDDW.Experience
{
	// The practice choice in passthrough, on the same table, slider and button the Neighborhood uses: the slider's notch
	// changes the orb (left: look A, middle: neutral, right: look B), already while it is being moved, and the button
	// locks the choice in. Nothing is recorded. The director decides when the slider and the button can be used.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/Practice Choice")]
	internal sealed class PracticeChoice : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("The practice table, hidden until this part")]
		[SerializeField] private ExperienceProp _table;

		[SerializeField] private NeighborhoodSlider _slider;
		[SerializeField] private NeighborhoodLockButton _lockButton;

		[Tooltip("Moves the slider with the hands")]
		[SerializeField] private NeighborhoodSliderHandDriver _sliderDriver;

		[SerializeField] private NarratorOrbController _orb;

		#endregion

		#region Fields

		// The orb's steps go from 0 (look A) through 2 (neutral) to 4 (look B); the slider has three notches
		private const int OrbStepsPerNotch = 2;
		private const int LastOrbStep = 4;

		#endregion

		#region Properties

		internal bool IsLocked => _lockButton.IsLocked;

		#endregion

		#region Setup

		private void OnEnable()
		{
			_slider.OnDetentChange += HandleDetentChange;
		}

		private void OnDisable()
		{
			_slider.OnDetentChange -= HandleDetentChange;
		}

		#endregion

		#region Event handlers

		private void HandleDetentChange(int detent)
		{
			_orb.SetStep(ToOrbStep(detent));
		}

		#endregion

		#region Internal

		internal void Configure(ExperienceProp table, NeighborhoodSlider slider, NeighborhoodLockButton lockButton,
			NeighborhoodSliderHandDriver sliderDriver, NarratorOrbController orb)
		{
			_table = table;
			_slider = slider;
			_lockButton = lockButton;
			_sliderDriver = sliderDriver;
			_orb = orb;
		}

		// The orb's step for a slider notch: 0 look A, 2 neutral, 4 look B
		internal static int ToOrbStep(int detent)
		{
			return Mathf.Clamp(detent * OrbStepsPerNotch, 0, LastOrbStep);
		}

		// The table appears with the slider wherever it rests and the orb matching it
		internal void Show()
		{
			_table.Show();
			_orb.SetStep(ToOrbStep(_slider.Detent));
		}

		internal void SetSliderEnabled(bool isEnabled)
		{
			_sliderDriver.IsInputEnabled = isEnabled;
		}

		internal void SetLockAvailable(bool isAvailable)
		{
			_lockButton.IsAvailable = isAvailable;
		}

		// The slider was moved to another notch and let go
		internal async UniTask WaitForSliderMovedAsync(CancellationToken cancellationToken)
		{
			int startDetent = _slider.Detent;
			await UniTask.WaitUntil(() => _slider.Detent != startDetent && !_slider.IsDragging,
				cancellationToken: cancellationToken);
		}

		internal async UniTask WaitForLockInAsync(CancellationToken cancellationToken)
		{
			await UniTask.WaitUntil(() => _lockButton.IsLocked, cancellationToken: cancellationToken);
		}

		#endregion
	}
}
