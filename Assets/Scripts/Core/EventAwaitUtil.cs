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

		// Completes with the event's value the next time it fires
		internal static async UniTask<T> NextAsync<T>(
			Action<Action<T>> subscribe,
			Action<Action<T>> unsubscribe,
			CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			UniTaskCompletionSource<T> completionSource = new();
			Action<T> handler = HandleEvent;
			subscribe(handler);

			try
			{
				return await completionSource.Task.AttachExternalCancellation(cancellationToken);
			}
			finally
			{
				unsubscribe(handler);
			}

			void HandleEvent(T value)
			{
				completionSource.TrySetResult(value);
			}
		}

		#endregion
	}
}
