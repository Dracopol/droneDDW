using UnityEngine;

namespace DroneDDW.Tutorial
{
	// An object that starts hidden and appears when a tutorial step needs it
	internal class Revealable : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("Optional, plays the reveal clip when the object appears")]
		[SerializeField] private AudioSource _audioSource;
		[SerializeField] private AudioClip _revealClip;

		#endregion

		#region Internal

		internal void Show()
		{
			if (gameObject.activeSelf)
				return;

			gameObject.SetActive(true);

			if (_audioSource != null && _revealClip != null)
				_audioSource.PlayOneShot(_revealClip);
		}

		#endregion
	}
}
