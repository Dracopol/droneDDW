using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DroneDDW.Experience
{
	// A participant action an onboarding step waits for. The action may already be done when the step starts, so a
	// condition checks the state rather than only waiting for events.
	internal abstract class ExperienceCondition : MonoBehaviour
	{
		#region Internal

		// Completes once the participant has done the action
		internal abstract UniTask WaitAsync(CancellationToken cancellationToken);

		// A stuck line is about to play: e.g. bring what the step is about back into view
		internal virtual void ShowHint()
		{
		}

		#endregion
	}
}
