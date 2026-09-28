using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace DroneDDW.Interaction
{
	// Lets hands hover and grab the object only up close, so the far pinch ray can't pull it over.
	// Added to the interactable's starting hover and select filters.
	internal class NearOnlyInteractionFilter : MonoBehaviour, IXRHoverFilter, IXRSelectFilter
	{
		#region Serialized fields

		[Tooltip("How close the hand has to be to the object's colliders")]
		[Min(0f)]
		[SerializeField] private float _maxDistanceMeters = 0.15f;

		#endregion

		#region Properties

		public bool canProcess => isActiveAndEnabled;

		#endregion

		#region Public

		public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
		{
			return IsNear(interactor, interactable);
		}

		public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
		{
			return IsNear(interactor, interactable);
		}

		#endregion

		#region Private

		private bool IsNear(IXRInteractor interactor, IXRInteractable interactable)
		{
			return interactable.GetDistanceSqrToInteractor(interactor) <= _maxDistanceMeters * _maxDistanceMeters;
		}

		#endregion
	}
}
