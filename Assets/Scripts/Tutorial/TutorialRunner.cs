using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Core;
using DroneDDW.Droney;
using UnityEngine;

namespace DroneDDW.Tutorial
{
	// Plays the tutorial steps in order, each one gated on its player action
	internal class TutorialRunner : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private DroneyVoice _droneyVoice;
		[SerializeField] private DroneyAppearance _droneyAppearance;
		[SerializeField] private DroneyFlight _droneyFlight;
		[SerializeField] private HintMarker _hintMarker;
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

			foreach (Revealable revealable in step.HiddenObjects)
				revealable.Hide();

			foreach (Revealable revealable in step.RevealedObjects)
				revealable.Show();

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
			StepOutcome outcome = step.GetOutcome(outcomeIndex);

			if (outcome == null)
				return;

			if (outcome.DroneyBodyMaterial != null)
				_droneyAppearance.SetBodyMaterial(outcome.DroneyBodyMaterial);

			await _droneyVoice.PlayAsync(outcome.SuccessClip, cancellationToken);
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
				_hintMarker.Hide();
				_droneyFlight.ReturnHome();
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
					BeginHint(step);
					await _droneyVoice.PlayAsync(step.StuckClip, cancellationToken);
					_droneyFlight.ReturnHome();
					await UniTask.Delay(TimeSpan.FromSeconds(_hintRepeatSeconds), cancellationToken: cancellationToken);
				}
			}
			catch (OperationCanceledException)
			{
				// The step completed or the tutorial stopped
			}
		}

		// The marker stays until the step completes, Droney only goes over while she says the stuck line
		private void BeginHint(TutorialStep step)
		{
			if (step.HintTarget == null)
				return;

			step.HintTarget.BeginHint();
			_hintMarker.Show(step.HintTarget);
			_droneyFlight.PointOut(step.HintTarget.Position);
		}

		#endregion
	}
}
