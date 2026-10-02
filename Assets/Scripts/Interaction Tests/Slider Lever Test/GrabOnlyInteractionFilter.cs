using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace DroneDDW.StepControls
{
	// Keeps fingertip pokes from grabbing the object, only a pinch should move it.
	// Added to the interactable's starting hover and select filters.
	internal class GrabOnlyInteractionFilter : MonoBehaviour, IXRHoverFilter, IXRSelectFilter
	{
		#region Properties

		public bool canProcess => isActiveAndEnabled;

		#endregion

		#region Public

		public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
		{
			return interactor is not XRPokeInteractor;
		}

		public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
		{
			return interactor is not XRPokeInteractor;
		}

		#endregion
	}
}
