using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DroneDDW.Tutorial
{
	// The player action a tutorial step waits for
	internal abstract class StepCondition : MonoBehaviour
	{
		#region Properties

		// How many distinct results WaitAsync can return, the step needs one outcome per result
		internal virtual int OutcomeCount => 1;

		#endregion

		#region Internal

		// Completes once the player has done the action and returns the outcome index.
		// Check the current state before waiting on events: the action may already be done when the step starts.
		internal abstract UniTask<int> WaitAsync(CancellationToken cancellationToken);

		#endregion
	}
}
