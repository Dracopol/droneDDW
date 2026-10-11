using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Android;

namespace DroneDDW.TicketScan
{
	// Asks for an Android runtime permission, such as the headset cameras', and waits for the answer. The ticket scan
	// scenes ask for theirs themselves rather than through OVRManager on startup, so the panel can show the answer.
	internal static class AndroidPermissionUtil
	{
		#region Internal

		// True once granted. Off the headset (in the Editor, also over Link) there is nothing to ask: always true.
		internal static UniTask<bool> RequestAsync(string permission, CancellationToken cancellationToken)
		{
			if (Application.platform != RuntimePlatform.Android || Permission.HasUserAuthorizedPermission(permission))
			{
				return UniTask.FromResult(true);
			}

			return RequestOnHeadsetAsync(permission, cancellationToken);
		}

		#endregion

		#region Private

		private static async UniTask<bool> RequestOnHeadsetAsync(string permission, CancellationToken cancellationToken)
		{
			bool isAnswered = false;
			PermissionCallbacks callbacks = new();
			callbacks.PermissionGranted += HandleAnswer;
			callbacks.PermissionDenied += HandleAnswer;
			Permission.RequestUserPermission(permission, callbacks);

			// When the permission dialog is already open for another request, no answer comes back: watch the
			// permission itself too (MRUK does the same)
			await UniTask.WaitUntil(() => isAnswered || Permission.HasUserAuthorizedPermission(permission),
				cancellationToken: cancellationToken);
			return Permission.HasUserAuthorizedPermission(permission);

			void HandleAnswer(string _)
			{
				isAnswered = true;
			}
		}

		#endregion
	}
}
