using UnityEngine;

namespace DroneDDW.Neighborhood.Table
{
	/// <summary>
	/// A push button's cap, moved the way the Scripted Button Stepper Test moves its caps: a mass-spring-damper resting at
	/// the top of its travel, with the hand as a one-sided limit. The cap can't be above the lowest part of a hand over it,
	/// but springs back freely once the hand lets go. Heights run along the button's up axis, from the bottom of the travel
	/// (0) to the top (the travel). Also tells touches and presses from the depth, with the Spring Button's thresholds and
	/// their hysteresis. Plain C#: each frame the button calls <see cref="Spring"/>, finds the hand, then calls
	/// <see cref="ApplyHand"/>.
	/// </summary>
	internal sealed class NeighborhoodPushCap
	{
		#region Fields

		// Small steps, so a slow frame can't make the spring explode
		private const float MaxSimulationStepSeconds = 1f / 240f;

		private readonly NeighborhoodPushCapTuning _tuning;

		private float _velocityMetersPerSecond;

		#endregion

		#region Properties

		internal float TravelMeters { get; }

		/// <summary>How high the cap stands: 0 pushed all the way down, <see cref="TravelMeters"/> at rest.</summary>
		internal float HeightMeters { get; private set; }

		/// <summary>How far the cap is down, from 0 (up) to 1 (all the way).</summary>
		internal float Depth01 => 1f - HeightMeters / TravelMeters;

		internal bool IsTouched { get; private set; }
		internal bool IsPressed { get; private set; }

		/// <summary>A hand is pushing the cap, or was a moment ago.</summary>
		internal bool IsInContact { get; private set; }

		/// <summary>Held all the way down, whatever the hands do, until <see cref="Unlatch"/>.</summary>
		internal bool IsLatched { get; private set; }

		/// <summary>
		/// The lowest hand underside that counts now: a new push has to start near the cap's top (so a hand coming in from
		/// the side or from underneath doesn't push it), an ongoing push counts all the way through the bottom stop.
		/// </summary>
		internal float LowestCountedMeters => IsInContact
			? -_tuning.MaxDepthBelowBottomMeters
			: HeightMeters - _tuning.ContactToleranceMeters;

		#endregion

		#region Setup

		internal NeighborhoodPushCap(float travelMeters, NeighborhoodPushCapTuning tuning)
		{
			TravelMeters = Mathf.Max(0.001f, travelMeters);
			_tuning = tuning ?? new NeighborhoodPushCapTuning();
			HeightMeters = TravelMeters;
		}

		#endregion

		#region Internal

		/// <summary>Springs the cap back towards the top for a frame. Nothing moves while time stands still.</summary>
		internal void Spring(float deltaSeconds)
		{
			if (deltaSeconds <= 0f || IsLatched)
			{
				return;
			}

			float angularFrequency = 2f * Mathf.PI * _tuning.SpringFrequencyHertz;
			float stiffness = angularFrequency * angularFrequency;
			float damping = 2f * _tuning.DampingRatio * angularFrequency;
			int stepCount = Mathf.CeilToInt(deltaSeconds / MaxSimulationStepSeconds);
			float stepSeconds = deltaSeconds / stepCount;

			// Semi-implicit Euler
			for (int step = 0; step < stepCount; step++)
			{
				float offsetMeters = HeightMeters - TravelMeters;
				float acceleration = -stiffness * offsetMeters - damping * _velocityMetersPerSecond;
				_velocityMetersPerSecond += acceleration * stepSeconds;
				HeightMeters += _velocityMetersPerSecond * stepSeconds;
				if (HeightMeters > TravelMeters)
				{
					HeightMeters = TravelMeters;
					_velocityMetersPerSecond = -_velocityMetersPerSecond * _tuning.TopStopBounce;
				}
				else if (HeightMeters < 0f)
				{
					HeightMeters = 0f;
					_velocityMetersPerSecond = Mathf.Max(_velocityMetersPerSecond, 0f);
				}
			}
		}

		/// <summary>
		/// Lets the hand hold the cap down: the lowest underside of a hand over the cap's face, in the cap's heights;
		/// positive infinity when there is none. A hand below <see cref="LowestCountedMeters"/> doesn't count. Then updates
		/// touched and pressed.
		/// </summary>
		internal void ApplyHand(float handUndersideMeters)
		{
			if (IsLatched)
			{
				IsInContact = false;
				UpdateThresholds();
				return;
			}

			bool isHandInside = !float.IsPositiveInfinity(handUndersideMeters)
				&& handUndersideMeters >= LowestCountedMeters;
			IsInContact = isHandInside && handUndersideMeters <= HeightMeters + _tuning.ContactToleranceMeters;

			// The hand holds the cap down: it only comes up again as fast as the hand lets it
			if (isHandInside && handUndersideMeters < HeightMeters)
			{
				HeightMeters = Mathf.Max(handUndersideMeters, 0f);
				_velocityMetersPerSecond = 0f;
			}

			UpdateThresholds();
		}

		/// <summary>Holds the cap all the way down, e.g. once the button has locked in.</summary>
		internal void Latch()
		{
			IsLatched = true;
			HeightMeters = 0f;
			_velocityMetersPerSecond = 0f;
			UpdateThresholds();
		}

		/// <summary>Lets the cap spring back up.</summary>
		internal void Unlatch()
		{
			IsLatched = false;
		}

		#endregion

		#region Private

		private void UpdateThresholds()
		{
			float depth = Depth01;
			if (!IsTouched && depth >= _tuning.TouchThreshold)
			{
				IsTouched = true;
			}
			else if (IsTouched && depth <= _tuning.TouchResetThreshold)
			{
				IsTouched = false;
			}

			if (!IsPressed && depth >= _tuning.PressThreshold)
			{
				IsPressed = true;
			}
			else if (IsPressed && depth <= _tuning.ReleaseThreshold)
			{
				IsPressed = false;
			}
		}

		#endregion
	}
}
