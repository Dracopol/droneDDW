using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Droney;
using UnityEngine;

namespace DroneDDW.Tutorial
{
	// Plays the tutorial steps in order, each one gated on its player action
	internal class TutorialRunner : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private DroneyVoice _droneyVoice;
		[SerializeField] private TutorialStep[] _steps;

		[Header("Stuck hints")]
		[Tooltip("Measured from the end of the prompt")]
		[Min(0f)]
		[SerializeField] private float _firstHintDelaySeconds = 8f;

		[Tooltip("Measured from the end of the previous stuck line")]
		[Min(0f)]
		[SerializeField] private float _hintRepeatSeconds = 15f;

		#endregion

		#region Setup

		private void OnValidate()
		{
			if (_steps == null)
				return;

			foreach (TutorialStep step in _steps)
			{
				if (!step.HasCondition || step.Outcomes.Count == step.Condition.OutcomeCount)
					continue;

				Debug.LogWarning(
					$"{name}: step \"{step.Name}\" has {step.Outcomes.Count} outcomes "
					+ $"but its condition can return {step.Condition.OutcomeCount} results",
					this);
			}
		}

		#endregion

		#region Internal

		internal async UniTask RunAsync(CancellationToken cancellationToken)
		{
			using CancellationTokenSource linkedSource =
				CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyCancellationToken);

			foreach (TutorialStep step in _steps)
				await RunStepAsync(step, linkedSource.Token);
		}

		#endregion

		#region Private

		private async UniTask RunStepAsync(TutorialStep step, CancellationToken cancellationToken)
		{
			Debug.Log($"[DDW Tutorial] {step.Name}: start", this);

			if (!step.HasCondition)
			{
				await _droneyVoice.PlayAsync(step.PromptClip, cancellationToken);
				return;
			}

			// Listen from the start of the prompt so acting early still counts, but never cut the prompt off
			UniTask<int> conditionTask = step.Condition.WaitAsync(cancellationToken).Preserve();
			await _droneyVoice.PlayAsync(step.PromptClip, cancellationToken);

			int outcomeIndex = conditionTask.Status.IsCompleted()
				? await conditionTask
				: await WaitWithHintsAsync(step, conditionTask, cancellationToken);

			Debug.Log($"[DDW Tutorial] {step.Name}: complete with outcome {outcomeIndex}", this);
			await _droneyVoice.PlayAsync(step.GetSuccessClip(outcomeIndex), cancellationToken);
		}

		private async UniTask<int> WaitWithHintsAsync(
			TutorialStep step,
			UniTask<int> conditionTask,
			CancellationToken cancellationToken)
		{
			using CancellationTokenSource hintSource =
				CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			RunHintsAsync(step, hintSource.Token).Forget();

			try
			{
				return await conditionTask;
			}
			finally
			{
				// Cuts off a stuck line that is still playing
				hintSource.Cancel();
				SetHintActive(step, false);
			}
		}

		// Repeats the stuck line and visual hint until cancelled
		private async UniTaskVoid RunHintsAsync(TutorialStep step, CancellationToken cancellationToken)
		{
			try
			{
				await UniTask.Delay(TimeSpan.FromSeconds(_firstHintDelaySeconds), cancellationToken: cancellationToken);

				while (true)
				{
					Debug.Log($"[DDW Tutorial] {step.Name}: stuck hint", this);
					SetHintActive(step, true);
					await _droneyVoice.PlayAsync(step.StuckClip, cancellationToken);
					await UniTask.Delay(TimeSpan.FromSeconds(_hintRepeatSeconds), cancellationToken: cancellationToken);
				}
			}
			catch (OperationCanceledException)
			{
				// The step completed or the tutorial stopped
			}
		}

		private void SetHintActive(TutorialStep step, bool isActive)
		{
			if (step.HintTarget != null)
				step.HintTarget.SetHighlighted(isActive);
		}

		#endregion
	}
}
