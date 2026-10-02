using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DroneDDW.Core
{
	// An object that starts hidden and appears when needed, e.g. a tutorial prop or the VR room
	internal class Revealable : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("Optional, plays the reveal clip when the object appears")]
		[SerializeField] private AudioSource _audioSource;
		[SerializeField] private AudioClip _revealClip;

		[Tooltip("Grows in with a little overshoot instead of popping in. "
			+ "Keep off for physics objects, their colliders would grow into whatever they rest on.")]
		[SerializeField] private bool _scalesIn;

		[Min(0.01f)]
		[SerializeField] private float _scaleInSeconds = 0.25f;

		#endregion

		#region Fields

		private Vector3 _fullScale;
		private bool _hasFullScale;

		#endregion

		#region Internal

		internal void Show()
		{
			if (gameObject.activeSelf)
				return;

			gameObject.SetActive(true);

			if (_audioSource != null && _revealClip != null)
				_audioSource.PlayOneShot(_revealClip);

			if (_scalesIn)
				ScaleInAsync(destroyCancellationToken).Forget();
		}

		internal void Hide()
		{
			gameObject.SetActive(false);
		}

		#endregion

		#region Private

		private async UniTaskVoid ScaleInAsync(CancellationToken cancellationToken)
		{
			if (!_hasFullScale)
			{
				_fullScale = transform.localScale;
				_hasFullScale = true;
			}

			try
			{
				float elapsedSeconds = 0f;

				while (elapsedSeconds < _scaleInSeconds)
				{
					transform.localScale = _fullScale * EaseOutBack(elapsedSeconds / _scaleInSeconds);
					elapsedSeconds += Time.deltaTime;
					await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
				}

				transform.localScale = _fullScale;
			}
			catch (OperationCanceledException)
			{
				// Destroyed while growing in
			}
		}

		// Goes slightly past 1 before settling, which reads as a pop
		private static float EaseOutBack(float progress)
		{
			const float Overshoot = 1.70158f;
			float shifted = progress - 1f;
			return 1f + (Overshoot + 1f) * shifted * shifted * shifted + Overshoot * shifted * shifted;
		}

		#endregion
	}
}
