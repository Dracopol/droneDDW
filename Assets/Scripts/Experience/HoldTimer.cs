using UnityEngine;

namespace DroneDDW.Experience
{
	// Completes once something has been true for long enough without a break, e.g. both hands tracked for a second;
	// a break starts it over. Fed the frame's time, so a paused session holds it still.
	internal sealed class HoldTimer
	{
		#region Fields

		private readonly float _holdSeconds;

		#endregion

		#region Properties

		// From 0 to 1, how long it has held so far
		internal float Progress { get; private set; }

		internal bool IsComplete => Progress >= 1f;

		#endregion

		#region Setup

		internal HoldTimer(float holdSeconds)
		{
			_holdSeconds = holdSeconds;
		}

		#endregion

		#region Internal

		// True once it has held long enough; it stays complete after that
		internal bool Update(bool isHolding, float deltaSeconds)
		{
			if (IsComplete)
			{
				return true;
			}

			if (!isHolding)
			{
				Progress = 0f;
				return false;
			}

			Progress = _holdSeconds <= 0f ? 1f : Mathf.Min(1f, Progress + deltaSeconds / _holdSeconds);
			return IsComplete;
		}

		#endregion
	}
}
