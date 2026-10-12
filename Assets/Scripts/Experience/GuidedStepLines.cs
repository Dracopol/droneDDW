using System;
using UnityEngine;

namespace DroneDDW.Experience
{
	// What the narrator says around one onboarding step: the prompt that asks for the action, the stuck line repeated
	// while the participant hasn't done it yet, and the success line once they have. Any of them may be left empty.
	[Serializable]
	public sealed class GuidedStepLines
	{
		#region Serialized fields

		[Tooltip("Asks for the action")]
		[SerializeField] private AudioClip _prompt;

		[Tooltip("Repeated while the action isn't done: after the settings' first hint delay, then every hint repeat")]
		[SerializeField] private AudioClip _stuck;

		[Tooltip("Once the action is done")]
		[SerializeField] private AudioClip _success;

		#endregion

		#region Properties

		public AudioClip Prompt => _prompt;
		public AudioClip Stuck => _stuck;
		public AudioClip Success => _success;

		#endregion

		#region Internal

		internal void Configure(AudioClip prompt, AudioClip stuck, AudioClip success)
		{
			_prompt = prompt;
			_stuck = stuck;
			_success = success;
		}

		#endregion
	}
}
