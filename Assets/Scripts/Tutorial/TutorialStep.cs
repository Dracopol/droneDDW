using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Tutorial
{
	[Serializable]
	internal class TutorialStep
	{
		#region Serialized fields

		[Tooltip("Label in the step list and in logs")]
		[SerializeField] private string _name;

		[Tooltip("Appear when the step starts, and stay for the rest of the tutorial")]
		[SerializeField] private Revealable[] _revealedObjects;
		[SerializeField] private AudioClip _promptClip;

		[Tooltip("Leave empty for steps that only talk, like the intro and outro")]
		[SerializeField] private StepCondition _condition;

		[Tooltip("One per result the condition can return")]
		[SerializeField] private StepOutcome[] _outcomes;
		[SerializeField] private AudioClip _stuckClip;
		[SerializeField] private HintTarget _hintTarget;

		#endregion

		#region Properties

		internal string Name => _name;
		internal IReadOnlyList<Revealable> RevealedObjects => _revealedObjects;
		internal AudioClip PromptClip => _promptClip;
		internal StepCondition Condition => _condition;
		internal bool HasCondition => _condition != null;
		internal IReadOnlyList<StepOutcome> Outcomes => _outcomes;
		internal AudioClip StuckClip => _stuckClip;
		internal HintTarget HintTarget => _hintTarget;

		#endregion

		#region Internal

		internal AudioClip GetSuccessClip(int outcomeIndex)
		{
			return _outcomes.Length == 0 ? null : _outcomes[outcomeIndex].SuccessClip;
		}

		#endregion
	}
}
