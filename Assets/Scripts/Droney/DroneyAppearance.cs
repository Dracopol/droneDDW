using UnityEngine;

namespace DroneDDW.Droney
{
	// Swaps Droney's body to another material, e.g. the color picked in the practice choice
	internal class DroneyAppearance : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private Renderer _bodyRenderer;

		#endregion

		#region Internal

		internal void SetBodyMaterial(Material material)
		{
			_bodyRenderer.sharedMaterial = material;
			Debug.Log($"[DDW Droney] Body material set to {material.name}", this);
		}

		#endregion
	}
}
