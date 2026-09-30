using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace DroneDDW.StepControls
{
	// The knob follows the hand that pinches it, keeping the gap between hand and knob from the moment of grabbing
	internal class GrabKnobInput : KnobInput
	{
		#region Serialized fields

		[Tooltip("On the knob's handle, doesn't move anything itself")]
		[SerializeField] private XRSimpleInteractable _interactable;

		[SerializeField] private KnobPath _path;

		#endregion

		#region Fields

		private IXRSelectInteractor _interactor;
		private bool _hasOffset;
		private float _offset01;

		#endregion

		#region Setup

		private void OnEnable()
		{
			_interactable.selectEntered.AddListener(HandleSelectEnter);
			_interactable.selectExited.AddListener(HandleSelectExit);
		}

		private void OnDisable()
		{
			_interactable.selectEntered.RemoveListener(HandleSelectEnter);
			_interactable.selectExited.RemoveListener(HandleSelectExit);
		}

		#endregion

		#region Event handlers

		// Grabbing with the other hand takes over, the offset is measured again for the new hand
		private void HandleSelectEnter(SelectEnterEventArgs args)
		{
			_interactor = args.interactorObject;
			_hasOffset = false;
			Debug.Log($"[DDW Step Controls] {name} grabbed by {args.interactorObject.transform.name}", this);
		}

		private void HandleSelectExit(SelectExitEventArgs args)
		{
			if (args.interactorObject == _interactor)
			{
				_interactor = null;
			}
		}

		#endregion

		#region Internal

		internal override bool TryGetTarget(float currentPosition01, out float targetPosition01)
		{
			targetPosition01 = currentPosition01;

			if (_interactor == null)
			{
				return false;
			}

			float handPosition01 = _path.Project(_interactor.GetAttachTransform(_interactable).position);

			if (!_hasOffset)
			{
				_offset01 = currentPosition01 - handPosition01;
				_hasOffset = true;
			}

			targetPosition01 = Mathf.Clamp01(handPosition01 + _offset01);
			return true;
		}

		#endregion
	}
}
