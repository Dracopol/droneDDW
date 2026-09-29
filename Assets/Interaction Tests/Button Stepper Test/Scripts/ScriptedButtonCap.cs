using UnityEngine;

namespace DroneDDW.Buttons
{
	// Moves the cap along this transform's up axis with a mass-spring-damper, pushed down by any part of a tracked hand.
	// The hand is a one-sided limit: the cap can't be above it, but it springs back freely once the hand lets go.
	internal class ScriptedButtonCap : ButtonCap
	{
		#region Serialized fields

		[Tooltip("Moves along this transform's up axis, its pivot is the top face of the cap")]
		[SerializeField] private Transform _cap;

		[SerializeField] private HandSkeleton[] _hands;

		[Tooltip("How far the cap can move down, it rests this high above this transform")]
		[Min(0.001f)]
		[SerializeField] private float _travelMeters = 0.02f;

		[Tooltip("Radius of the cap's top face, hands outside it don't push")]
		[Min(0.001f)]
		[SerializeField] private float _capRadiusMeters = 0.06f;

		[Header("Spring")]
		[Tooltip("How fast the cap springs back, higher is snappier")]
		[Min(0.1f)]
		[SerializeField] private float _springFrequencyHertz = 8f;

		[Tooltip("1 returns without overshooting, lower values bounce more")]
		[Range(0f, 1f)]
		[SerializeField] private float _dampingRatio = 0.5f;

		[Tooltip("How much of its speed the cap keeps when it bounces off the top stop")]
		[Range(0f, 1f)]
		[SerializeField] private float _topStopBounce = 0.3f;

		[Header("Hand contact")]
		[Tooltip("How far below the cap top a hand may be and still start a push, covers fast hands between frames. "
			+ "Deeper hands came in from the side or underneath and are ignored.")]
		[Min(0f)]
		[SerializeField] private float _contactToleranceMeters = 0.03f;

		[Tooltip("During a push, hand points further below the bottom stop than this don't count, e.g. a hand under it")]
		[Min(0f)]
		[SerializeField] private float _maxDepthBelowBottomMeters = 0.08f;

		[Tooltip("Spacing of the spheres sampled along each hand bone")]
		[Min(0.002f)]
		[SerializeField] private float _sampleSpacingMeters = 0.01f;

		#endregion

		#region Fields

		private const float MaxSimulationStepSeconds = 1f / 240f;

		private Transform _transform;
		private float _heightMeters;
		private float _velocityMetersPerSecond;
		private bool _isInContact;

		#endregion

		#region Properties

		internal override float PressDepth01 => 1f - _heightMeters / _travelMeters;

		#endregion

		#region Setup

		private void Awake()
		{
			_transform = transform;
			_heightMeters = _travelMeters;
			ApplyHeight();
		}

		#endregion

		#region Update

		private void LateUpdate()
		{
			float deltaSeconds = Time.deltaTime;

			if (deltaSeconds <= 0f)
			{
				return;
			}

			SimulateSpring(deltaSeconds);

			bool isHandInside = TryGetLowestHandHeight(out float handHeightMeters);
			_isInContact = isHandInside && handHeightMeters <= _heightMeters + _contactToleranceMeters;

			// The hand holds the cap down, it only moves up again as fast as the hand lets it
			if (isHandInside && handHeightMeters < _heightMeters)
			{
				_heightMeters = Mathf.Max(handHeightMeters, 0f);
				_velocityMetersPerSecond = 0f;
			}

			ApplyHeight();
		}

		#endregion

		#region Private

		// Semi-implicit Euler in small steps, so a slow frame can't make the spring explode
		private void SimulateSpring(float deltaSeconds)
		{
			float angularFrequency = 2f * Mathf.PI * _springFrequencyHertz;
			float stiffness = angularFrequency * angularFrequency;
			float damping = 2f * _dampingRatio * angularFrequency;
			int stepCount = Mathf.CeilToInt(deltaSeconds / MaxSimulationStepSeconds);
			float stepSeconds = deltaSeconds / stepCount;

			for (int step = 0; step < stepCount; step++)
			{
				float offsetMeters = _heightMeters - _travelMeters;
				float acceleration = -stiffness * offsetMeters - damping * _velocityMetersPerSecond;

				_velocityMetersPerSecond += acceleration * stepSeconds;
				_heightMeters += _velocityMetersPerSecond * stepSeconds;

				if (_heightMeters > _travelMeters)
				{
					_heightMeters = _travelMeters;
					_velocityMetersPerSecond = -_velocityMetersPerSecond * _topStopBounce;
				}
				else if (_heightMeters < 0f)
				{
					_heightMeters = 0f;
					_velocityMetersPerSecond = Mathf.Max(_velocityMetersPerSecond, 0f);
				}
			}
		}

		// Lowest underside of any tracked hand over the cap's face, in this transform's space
		private bool TryGetLowestHandHeight(out float lowestHeightMeters)
		{
			lowestHeightMeters = float.PositiveInfinity;

			// A new push has to start at the cap top, an ongoing one may go all the way through the bottom stop
			float lowestCountedMeters = _isInContact
				? -_maxDepthBelowBottomMeters
				: _heightMeters - _contactToleranceMeters;

			foreach (HandSkeleton hand in _hands)
			{
				if (!hand.IsTracked)
				{
					continue;
				}

				foreach (HandBone bone in hand.Bones)
				{
					Vector3 start = _transform.InverseTransformPoint(bone.Start.position);
					Vector3 end = _transform.InverseTransformPoint(bone.End.position);
					float lengthMeters = Vector3.Distance(start, end);
					int segmentCount = Mathf.Max(1, Mathf.CeilToInt(lengthMeters / _sampleSpacingMeters));

					for (int sample = 0; sample <= segmentCount; sample++)
					{
						Vector3 point = Vector3.Lerp(start, end, (float)sample / segmentCount);
						float undersideMeters = point.y - bone.RadiusMeters;

						if (point.x * point.x + point.z * point.z > _capRadiusMeters * _capRadiusMeters
							|| undersideMeters < lowestCountedMeters)
						{
							continue;
						}

						lowestHeightMeters = Mathf.Min(lowestHeightMeters, undersideMeters);
					}
				}
			}

			return lowestHeightMeters < float.PositiveInfinity;
		}

		private void ApplyHeight()
		{
			_cap.localPosition = new Vector3(0f, _heightMeters, 0f);
		}

		#endregion
	}
}
