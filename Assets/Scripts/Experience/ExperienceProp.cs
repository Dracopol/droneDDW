using UnityEngine;

namespace DroneDDW.Experience
{
	// Something that belongs to one part of the session only (the orb, the practice table, the cards, the ghost hands,
	// the passthrough light), shown and hidden as a whole. The scene builder saves it in the state the session starts
	// with.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/Experience Prop")]
	internal sealed class ExperienceProp : MonoBehaviour
	{
		#region Properties

		internal bool IsShown => gameObject.activeSelf;

		#endregion

		#region Internal

		internal void Show()
		{
			gameObject.SetActive(true);
		}

		internal void Hide()
		{
			gameObject.SetActive(false);
		}

		#endregion
	}
}
