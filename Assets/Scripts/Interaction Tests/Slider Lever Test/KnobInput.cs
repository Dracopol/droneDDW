using UnityEngine;

namespace DroneDDW.StepControls
{
	// Where a hand wants a script-moved knob to be, if a hand is moving it at all
	internal abstract class KnobInput : MonoBehaviour
	{
		#region Internal

		internal abstract bool TryGetTarget(float currentPosition01, out float targetPosition01);

		#endregion
	}
}
