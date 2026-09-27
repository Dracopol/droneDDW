using System;
using UnityEngine;

namespace DroneDDW.Tutorial
{
	// What happens for one result of a step's condition
	[Serializable]
	internal class StepOutcome
	{
		#region Serialized fields

		[SerializeField] private AudioClip _successClip;

		#endregion

		#region Properties

		internal AudioClip SuccessClip => _successClip;

		#endregion
	}
}
