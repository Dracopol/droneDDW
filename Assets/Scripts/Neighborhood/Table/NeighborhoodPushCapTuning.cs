using System;
using UnityEngine;

namespace DroneDDW.Neighborhood.Table
{
	/// <summary>
	/// How a push button's cap moves and when it counts as touched and pressed: the Scripted Button Stepper Test's values
	/// (its ScriptedButtonCap and SpringButton), shown on the button that uses them.
	/// </summary>
	[Serializable]
	internal sealed class NeighborhoodPushCapTuning
	{
		#region Serialized fields

		[Header("Spring")]
		[SerializeField, Min(0.1f), Tooltip("How fast the cap springs back, higher is snappier.")]
		private float _springFrequencyHertz = 8f;

		[SerializeField, Range(0f, 1f), Tooltip("1 returns without overshooting, lower values bounce more.")]
		private float _dampingRatio = 0.5f;

		[SerializeField, Range(0f, 1f), Tooltip("How much of its speed the cap keeps when it bounces off the top stop.")]
		private float _topStopBounce = 0.3f;

		[Header("Hand contact")]
		[SerializeField, Min(0f), Tooltip("How far below the cap's top a hand may be and still start a push, which covers "
			+ "fast hands between frames. Deeper hands came in from the side or underneath and are ignored.")]
		private float _contactToleranceMeters = 0.03f;

		[SerializeField, Min(0f), Tooltip("During a push, hand points further below the bottom stop than this don't "
			+ "count, e.g. a hand under the table.")]
		private float _maxDepthBelowBottomMeters = 0.08f;

		[Header("Thresholds, as a fraction of the cap's travel")]
		[SerializeField, Range(0f, 1f), Tooltip("How far the cap has to go down before it counts as touched.")]
		private float _touchThreshold = 0.05f;

		[SerializeField, Range(0f, 1f), Tooltip("The cap has to come back up past this before it can be touched again.")]
		private float _touchResetThreshold = 0.01f;

		[SerializeField, Range(0f, 1f), Tooltip("How far the cap has to go down to count as pressed.")]
		private float _pressThreshold = 0.8f;

		[SerializeField, Range(0f, 1f), Tooltip("Below the press threshold, so a hand resting near it doesn't flicker "
			+ "between pressed and released.")]
		private float _releaseThreshold = 0.4f;

		#endregion

		#region Properties

		internal float SpringFrequencyHertz => _springFrequencyHertz;
		internal float DampingRatio => _dampingRatio;
		internal float TopStopBounce => _topStopBounce;
		internal float ContactToleranceMeters => _contactToleranceMeters;
		internal float MaxDepthBelowBottomMeters => _maxDepthBelowBottomMeters;
		internal float TouchThreshold => _touchThreshold;
		internal float TouchResetThreshold => _touchResetThreshold;
		internal float PressThreshold => _pressThreshold;
		internal float ReleaseThreshold => _releaseThreshold;

		#endregion
	}
}
