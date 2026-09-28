using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DroneDDW.Droney
{
	internal class DroneyVoice : MonoBehaviour
	{
		#region Events

		internal event Action<AudioClip> OnLineStart;

		// Also raised when a line is cut off
		internal event Action<AudioClip> OnLineEnd;

		#endregion

		#region Serialized fields

		[SerializeField] private AudioSource _audioSource;

		#endregion

		#region Properties

		internal bool IsSpeaking => _audioSource.isPlaying;

		#endregion

		#region Internal

		// Plays the clip to the end, cancelling stops it; a missing clip completes immediately
		internal async UniTask PlayAsync(AudioClip clip, CancellationToken cancellationToken)
		{
			if (clip == null)
				return;

			using CancellationTokenSource linkedSource =
				CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);

			_audioSource.clip = clip;
			_audioSource.Play();
			OnLineStart?.Invoke(clip);

			try
			{
				await UniTask.WaitWhile(
					() => _audioSource.isPlaying,
					cancellationToken: linkedSource.Token,
					cancelImmediately: true);
			}
			catch (OperationCanceledException)
			{
				StopIfPlaying(clip);
				throw;
			}
			finally
			{
				OnLineEnd?.Invoke(clip);
			}
		}

		#endregion

		#region Private

		private void StopIfPlaying(AudioClip clip)
		{
			// Only stop our own clip, another line may have started since this one was cancelled
			if (_audioSource != null && _audioSource.clip == clip)
				_audioSource.Stop();
		}

		#endregion
	}
}
