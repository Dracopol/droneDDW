using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Core;
using DroneDDW.Interaction;
using UnityEngine;

namespace DroneDDW.Tutorial
{
	// Completes once the player holds the object, right away if they already do
	internal class GrabCondition : StepCondition
	{
		#region Serialized fields

		[SerializeField] private GrabMonitor _grabMonitor;

		#endregion

		#region Internal

		internal override async UniTask<int> WaitAsync(CancellationToken cancellationToken)
		{
			using CancellationTokenSource linkedSource =
				CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);

			if (!_grabMonitor.IsHeld)
			{
				await EventAwaitUtil.NextAsync(
					handler => _grabMonitor.OnGrab += handler,
					handler => _grabMonitor.OnGrab -= handler,
					linkedSource.Token);
			}

			return 0;
		}

		#endregion
	}
}
