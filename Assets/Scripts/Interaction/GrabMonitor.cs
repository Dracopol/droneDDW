using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DroneDDW.Interaction
{
	// Grab state of an interactable as C# events
	internal class GrabMonitor : MonoBehaviour
	{
		#region Events

		internal event Action OnGrab;
		internal event Action OnRelease;

		#endregion

		#region Serialized fields

		[SerializeField] private XRGrabInteractable _grabInteractable;

		#endregion

		#region Properties

		internal bool IsHeld => _grabInteractable.isSelected;

		#endregion

		#region Setup

		private void OnEnable()
		{
			_grabInteractable.selectEntered.AddListener(HandleSelectEnter);
			_grabInteractable.selectExited.AddListener(HandleSelectExit);
		}

		private void OnDisable()
		{
			_grabInteractable.selectEntered.RemoveListener(HandleSelectEnter);
			_grabInteractable.selectExited.RemoveListener(HandleSelectExit);
		}

		#endregion

		#region Event handlers

		private void HandleSelectEnter(SelectEnterEventArgs args)
		{
			Debug.Log($"[DDW Grab] {name} grabbed by {args.interactorObject.transform.name}", this);
			OnGrab?.Invoke();
		}

		private void HandleSelectExit(SelectExitEventArgs args)
		{
			Debug.Log($"[DDW Grab] {name} released", this);
			OnRelease?.Invoke();
		}

		#endregion
	}
}
