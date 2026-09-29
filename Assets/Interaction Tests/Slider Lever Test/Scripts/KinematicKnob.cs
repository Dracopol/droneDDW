using UnityEngine;

namespace DroneDDW.StepControls
{
	// A knob moved by a script. It follows the hand input with a pull into each notch,
	// and springs into the nearest notch once the hand lets go.
	internal class KinematicKnob : StepKnob
	{
		#region Serialized fields

		[SerializeField] private KnobPath _path;
		[SerializeField] private KnobInput _input;

		[Header("Notches")]
		[Tooltip("How much the knob lags the hand near a notch and catches up between notches. 0 is none.")]
		[Range(0f, 0.95f)]
		[SerializeField] private float _notchStickiness = 0.6f;

		[Tooltip("How fast the knob settles into the nearest notch once it's let go")]
		[Min(0.1f)]
		[SerializeField] private float _settleFrequencyHertz = 5f;

		[Tooltip("1 settles without overshooting, lower values bounce past the notch")]
		[Range(0f, 1f)]
		[SerializeField] private float _settleDampingRatio = 0.8f;

		#endregion

		#region Fields

		private const float MaxSimulationStepSeconds = 1f / 240f;

		// Where the hand has the knob, before the notch pull
		private float _rawPosition01 = 0.5f;
		private float _velocity01;
		private bool _isHeld;

		#endregion

		#region Properties

		internal override float Position01 => ApplyNotchPull(_rawPosition01);

		internal override bool IsHeld => _isHeld;

		#endregion

		#region Update

		private void LateUpdate()
		{
			float deltaSeconds = Time.deltaTime;

			if (deltaSeconds <= 0f)
			{
				return;
			}

			_isHeld = _input.TryGetTarget(_rawPosition01, out float targetPosition01);

			if (_isHeld)
			{
				_rawPosition01 = targetPosition01;
				_velocity01 = 0f;
			}
			else
			{
				Settle(deltaSeconds);
			}

			_path.Apply(Position01);
		}

		#endregion

		#region Internal

		internal override void SnapTo(float position01)
		{
			_rawPosition01 = position01;
			_velocity01 = 0f;
			_path.Apply(Position01);
		}

		#endregion

		#region Private

		// Spring-damper towards the nearest notch, in small steps so a slow frame can't make it explode
		private void Settle(float deltaSeconds)
		{
			float angularFrequency = 2f * Mathf.PI * _settleFrequencyHertz;
			float stiffness = angularFrequency * angularFrequency;
			float damping = 2f * _settleDampingRatio * angularFrequency;
			float notch01 = StepPosition01(NearestStep(_rawPosition01));
			int stepCount = Mathf.CeilToInt(deltaSeconds / MaxSimulationStepSeconds);
			float stepSeconds = deltaSeconds / stepCount;

			for (int step = 0; step < stepCount; step++)
			{
				float acceleration = -stiffness * (_rawPosition01 - notch01) - damping * _velocity01;
				_velocity01 += acceleration * stepSeconds;
				_rawPosition01 = Mathf.Clamp01(_rawPosition01 + _velocity01 * stepSeconds);
			}
		}

		// Moves slower than the hand around each notch and faster between them, without ever going backwards.
		// Both ends of each step land exactly on the halfway points, so the curve joins up from step to step.
		private float ApplyNotchPull(float rawPosition01)
		{
			float halfStep01 = StepSize01 * 0.5f;
			float notch01 = StepPosition01(NearestStep(rawPosition01));
			float offset01 = rawPosition01 - notch01;
			float pull01 = _notchStickiness * halfStep01 / Mathf.PI * Mathf.Sin(Mathf.PI * offset01 / halfStep01);

			return notch01 + offset01 - pull01;
		}

		#endregion
	}
}
