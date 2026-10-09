using System;
using UnityEngine;

namespace DroneDDW.Neighborhood.Motion
{
    /// <summary>
    /// Keeps the street alive: drones fly the corridor and circle, robots drive the lane, ducks swim, the robin
    /// hops. Moves the object in world space from Time.time, so every mover is deterministic and nothing drifts.
    /// Works together with a Scale-style <see cref="Versions.NeighborhoodVersionElement"/> on the same object
    /// (the element only changes the scale).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Mover")]
    public sealed class NeighborhoodMover : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField] private NeighborhoodMotionKind _kind = NeighborhoodMotionKind.Hover;

        [SerializeField, Tooltip("Path: the closed loop, world space.")]
        private Vector3[] _points = Array.Empty<Vector3>();

        [SerializeField, Tooltip("Orbit: centre of the ellipse, world space (its height is the flying height).")]
        private Vector3 _center;

        [SerializeField, Min(0f)] private float _radiusX = 1f;
        [SerializeField, Min(0f)] private float _radiusZ = 1f;

        [SerializeField, Min(0f), Tooltip("Metres per second along the path or ellipse.")]
        private float _speed = 1f;

        [SerializeField, Range(0f, 1f), Tooltip("Where on the loop it starts (fraction).")]
        private float _phase;

        [SerializeField, Tooltip("Orbit direction seen from above.")]
        private bool _isClockwise;

        [SerializeField, Min(0f), Tooltip("Up and down movement in metres (Hop: jump height).")]
        private float _bobHeight;

        [SerializeField, Min(0.1f), Tooltip("Seconds per bob (Hop: average seconds between hops).")]
        private float _bobPeriod = 2f;

        [SerializeField, Tooltip("Turn to face the direction of travel.")]
        private bool _isFacingTravel = true;

        [SerializeField, Tooltip("The street pieces have their front on -Z.")]
        private bool _isFrontOnNegativeZ = true;

        [SerializeField, Min(0f), Tooltip("How quickly it turns towards its heading. 0 = at once.")]
        private float _turnRate = 5f;

        #endregion

        #region Fields

        private const float HopDuration = 0.22f;
        private const float HopTurnDegrees = 35f;
        private const float SwayDegrees = 6f;

        private float[] _segmentStarts = Array.Empty<float>();
        private float _loopLength;
        private Vector3 _basePosition;
        private Quaternion _baseRotation = Quaternion.identity;
        private bool _hasBase;
        private float _nextHopTime;
        private float _hopStartTime = -1f;
        private float _hopFromYaw;
        private float _hopToYaw;
        private float _yaw;

        #endregion

        #region Properties

        public NeighborhoodMotionKind Kind => _kind;

        #endregion

        #region Setup

        private void Awake()
        {
            CaptureBase();
            MeasurePath();
        }

        #endregion

        #region Update

        private void Update()
        {
            float time = Time.time;
            switch (_kind)
            {
                case NeighborhoodMotionKind.Path:
                    UpdatePath(time);
                    break;
                case NeighborhoodMotionKind.Orbit:
                    UpdateOrbit(time);
                    break;
                case NeighborhoodMotionKind.Hover:
                    UpdateHover(time);
                    break;
                case NeighborhoodMotionKind.Hop:
                    UpdateHop(time);
                    break;
            }
        }

        #endregion

        #region Internal

        internal void Configure(NeighborhoodMotionKind kind, Vector3[] points, Vector3 center, float radiusX, float radiusZ,
            float speed, float phase, bool isClockwise, float bobHeight, float bobPeriod, bool isFacingTravel)
        {
            _kind = kind;
            _points = points ?? Array.Empty<Vector3>();
            _center = center;
            _radiusX = radiusX;
            _radiusZ = radiusZ;
            _speed = speed;
            _phase = Mathf.Repeat(phase, 1f);
            _isClockwise = isClockwise;
            _bobHeight = bobHeight;
            _bobPeriod = Mathf.Max(0.1f, bobPeriod);
            _isFacingTravel = isFacingTravel;
        }

        /// <summary>Where a Path or Orbit mover is at a time (also used to place it in edit mode).</summary>
        internal Vector3 Evaluate(float time, out Vector3 direction)
        {
            direction = Vector3.zero;
            switch (_kind)
            {
                case NeighborhoodMotionKind.Path:
                    if (_loopLength <= 0f)
                    {
                        MeasurePath();
                    }

                    return _loopLength > 0f ? SamplePath(Mathf.Repeat(_phase * _loopLength + time * _speed, _loopLength), out direction) : transform.position;
                case NeighborhoodMotionKind.Orbit:
                    return SampleOrbit(time, out direction);
                default:
                    return transform.position;
            }
        }

        #endregion

        #region Private

        private void CaptureBase()
        {
            if (_hasBase)
            {
                return;
            }

            _basePosition = transform.position;
            _baseRotation = transform.rotation;
            _yaw = _baseRotation.eulerAngles.y;
            _hasBase = true;
        }

        private void MeasurePath()
        {
            int count = _points.Length;
            _segmentStarts = new float[count + 1];
            _loopLength = 0f;
            for (int i = 0; i < count; i++)
            {
                _segmentStarts[i] = _loopLength;
                _loopLength += Vector3.Distance(_points[i], _points[(i + 1) % count]);
            }

            _segmentStarts[count] = _loopLength;
        }

        private void UpdatePath(float time)
        {
            Vector3 position = Evaluate(time, out Vector3 direction);
            Apply(position + Vector3.up * Bob(time), direction);
        }

        private void UpdateOrbit(float time)
        {
            Vector3 position = SampleOrbit(time, out Vector3 direction);
            Apply(position + Vector3.up * Bob(time), direction);
        }

        private void UpdateHover(float time)
        {
            CaptureBase();
            float sway = Mathf.Sin((time / (_bobPeriod * 2.7f) + _phase) * Mathf.PI * 2f) * SwayDegrees;
            transform.SetPositionAndRotation(_basePosition + Vector3.up * Bob(time), _baseRotation * Quaternion.Euler(0f, sway, 0f));
        }

        private void UpdateHop(float time)
        {
            CaptureBase();
            if (_hopStartTime < 0f && time >= _nextHopTime)
            {
                _hopStartTime = time;
                _hopFromYaw = _yaw;
                _hopToYaw = _baseRotation.eulerAngles.y + UnityEngine.Random.Range(-HopTurnDegrees, HopTurnDegrees);
            }

            float lift = 0f;
            if (_hopStartTime >= 0f)
            {
                float t = Mathf.Clamp01((time - _hopStartTime) / HopDuration);
                lift = 4f * _bobHeight * t * (1f - t);
                _yaw = Mathf.LerpAngle(_hopFromYaw, _hopToYaw, t);
                if (t >= 1f)
                {
                    _hopStartTime = -1f;
                    _nextHopTime = time + _bobPeriod * UnityEngine.Random.Range(0.6f, 1.6f);
                }
            }

            Vector3 euler = _baseRotation.eulerAngles;
            transform.SetPositionAndRotation(_basePosition + Vector3.up * lift, Quaternion.Euler(euler.x, _yaw, euler.z));
        }

        private float Bob(float time)
        {
            return _bobHeight > 0f ? Mathf.Sin((time / _bobPeriod + _phase) * Mathf.PI * 2f) * _bobHeight : 0f;
        }

        private void Apply(Vector3 position, Vector3 direction)
        {
            Transform self = transform;
            direction.y = 0f;
            if (!_isFacingTravel || direction.sqrMagnitude < 1e-6f)
            {
                self.position = position;
                return;
            }

            Quaternion heading = Quaternion.LookRotation(_isFrontOnNegativeZ ? -direction : direction, Vector3.up);
            Quaternion rotation = _turnRate > 0f
                ? Quaternion.Slerp(self.rotation, heading, 1f - Mathf.Exp(-_turnRate * Time.deltaTime))
                : heading;
            self.SetPositionAndRotation(position, rotation);
        }

        private Vector3 SampleOrbit(float time, out Vector3 direction)
        {
            float meanRadius = Mathf.Max(0.05f, (_radiusX + _radiusZ) * 0.5f);
            float sign = _isClockwise ? -1f : 1f;
            float angle = _phase * Mathf.PI * 2f + sign * time * _speed / meanRadius;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            direction = new Vector3(-sin * _radiusX * sign, 0f, cos * _radiusZ * sign);
            return new Vector3(_center.x + cos * _radiusX, _center.y, _center.z + sin * _radiusZ);
        }

        /// <summary>Catmull-Rom through the loop points; distance is measured along the straight segments.</summary>
        private Vector3 SamplePath(float distance, out Vector3 direction)
        {
            int count = _points.Length;
            int segment = 0;
            while (segment < count - 1 && _segmentStarts[segment + 1] <= distance)
            {
                segment++;
            }

            float length = _segmentStarts[segment + 1] - _segmentStarts[segment];
            float t = length > 0f ? (distance - _segmentStarts[segment]) / length : 0f;
            Vector3 p0 = _points[(segment - 1 + count) % count];
            Vector3 p1 = _points[segment];
            Vector3 p2 = _points[(segment + 1) % count];
            Vector3 p3 = _points[(segment + 2) % count];
            float t2 = t * t;
            float t3 = t2 * t;
            direction = 0.5f * ((-p0 + p2) + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t + 3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2);
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        #endregion
    }
}
