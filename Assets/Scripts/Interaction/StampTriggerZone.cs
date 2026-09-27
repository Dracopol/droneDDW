using UnityEngine;

namespace DroneDDW.Interaction
{
	// Sits on the stamp's face, so a contact here means the face touched something, not the handle or the side
	internal class StampTriggerZone : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private Stamp _stamp;

		#endregion

		#region Event handlers

		private void OnTriggerEnter(Collider other)
		{
			_stamp.HandleFaceContact(other, transform.position);
		}

		#endregion
	}
}
