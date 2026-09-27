using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DroneDDW.Core
{
	internal static class EventAwaitUtil
	{
		#region Internal

		// Completes the next time the event fires; subscribe and unsubscribe add and remove the given handler
		internal static async UniTask NextAsync(
			Action<Action> subscribe,
			Action<Action> unsubscribe,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			UniTaskCompletionSource completionSource = new();
			Action handler = HandleEvent;
			subscribe(handler);

			try
			{
				await completionSource.Task.AttachExternalCancellation(cancellationToken);
			}
			finally
			{
				unsubscribe(handler);
			}

			void HandleEvent()
			{
				completionSource.TrySetResult();
			}
		}

		#endregion
	}
}
