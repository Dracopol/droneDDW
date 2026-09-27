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

		[Tooltip("Optional, Droney's body switches to this material for the rest of the experience")]
		[SerializeField] private Material _droneyBodyMaterial;

		#endregion

		#region Properties

		internal AudioClip SuccessClip => _successClip;
		internal Material DroneyBodyMaterial => _droneyBodyMaterial;

		#endregion
	}
}
