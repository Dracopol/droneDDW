#if PARKS_XRI
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
#if PARKS_XRI_3
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
#endif

namespace DroneDDW.Parks.Table
{
	/// <summary>
	/// Drives <see cref="ParkLevelSlider"/> from an XR Interaction Toolkit interactable on the handle
	/// (an XR Simple Interactable works; XRI 2.x and 3.x). Works with direct grabs, rays and near-far
	/// interactors: the handle follows the interactor's attach point along the track. Plays a short
	/// haptic tick on every detent and a firmer one when the level is committed.
	/// Compiled only when the XR Interaction Toolkit package is installed.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Slider XR Driver")]
	public sealed class ParkSliderXRDriver : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private ParkLevelSlider _slider;

		[SerializeField, Tooltip("The interactable on the handle.")]
		private XRBaseInteractable _interactable;

		[Header("Haptics")]
		[SerializeField, Range(0f, 1f)] private float _detentAmplitude = 0.3f;
		[SerializeField, Min(0f)] private float _detentDuration = 0.03f;
		[SerializeField, Range(0f, 1f)] private float _commitAmplitude = 0.6f;
		[SerializeField, Min(0f)] private float _commitDuration = 0.08f;

		#endregion

		#region Fields

		private IXRSelectInteractor _dragger;
		private IXRSelectInteractor _lastDragger;

		#endregion

		#region Setup

		private void OnEnable()
		{
			_slider.OnDetentChange += HandleDetentChange;
			_slider.OnLevelCommit += HandleLevelCommit;
		}

		private void OnDisable()
		{
			_slider.OnDetentChange -= HandleDetentChange;
			_slider.OnLevelCommit -= HandleLevelCommit;
			if (_dragger == null)
			{
				return;
			}

			_dragger = null;
			_slider.EndDrag();
		}

		#endregion

		#region Update

		private void Update()
		{
			IXRSelectInteractor interactor = _interactable.isSelected ? _interactable.firstInteractorSelecting : null;
			if (interactor != _dragger)
			{
				if (_dragger != null)
				{
					_dragger = null;
					_slider.EndDrag();
				}

				if (interactor != null)
				{
					_dragger = interactor;
					_lastDragger = interactor;
					_slider.BeginDrag(GetAttachPoint(interactor));
				}
			}

			if (_dragger != null)
			{
				_slider.DragTo(GetAttachPoint(_dragger));
			}
		}

		#endregion

		#region Event handlers

		private void HandleDetentChange(int detent)
		{
			SendPulse(_dragger, _detentAmplitude, _detentDuration);
		}

		private void HandleLevelCommit(int level)
		{
			SendPulse(_lastDragger, _commitAmplitude, _commitDuration);
		}

		#endregion

		#region Internal

		internal void Configure(ParkLevelSlider slider, XRBaseInteractable interactable)
		{
			_slider = slider;
			_interactable = interactable;
		}

		#endregion

		#region Private

		private Vector3 GetAttachPoint(IXRSelectInteractor interactor)
		{
			Transform attach = interactor.GetAttachTransform(_interactable);
			return attach != null ? attach.position : interactor.transform.position;
		}

		private static void SendPulse(IXRSelectInteractor interactor, float amplitude, float duration)
		{
			if (amplitude <= 0f || duration <= 0f)
			{
				return;
			}

#if PARKS_XRI_3
			if (interactor is XRBaseInputInteractor inputInteractor)
			{
				inputInteractor.SendHapticImpulse(amplitude, duration);
			}
#else
			if (interactor is XRBaseControllerInteractor controllerInteractor)
			{
				controllerInteractor.SendHapticImpulse(amplitude, duration);
			}
#endif
		}

		#endregion
	}
}
#endif
