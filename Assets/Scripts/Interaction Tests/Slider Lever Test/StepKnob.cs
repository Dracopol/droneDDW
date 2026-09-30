using UnityEngine;

namespace DroneDDW.StepControls
{
	// Something the hands move from one end of its travel to the other, with evenly spread steps including both ends
	internal abstract class StepKnob : MonoBehaviour
	{
		#region Serialized fields

		[Min(2)]
		[SerializeField] private int _stepCount = 5;

		#endregion

		#region Properties

		internal int StepCount => _stepCount;

		// 0 at the first step, 1 at the last
		internal abstract float Position01 { get; }

		// Drives the yellow color and the grab and release sounds
		internal abstract bool IsHeld { get; }

		internal float StepSize01 => 1f / (_stepCount - 1);

		#endregion

		#region Internal

		internal abstract void SnapTo(float position01);

		internal int NearestStep(float position01)
		{
			return Mathf.Clamp(Mathf.RoundToInt(position01 / StepSize01), 0, _stepCount - 1);
		}

		internal float StepPosition01(int step)
		{
			return step * StepSize01;
		}

		#endregion
	}
}
