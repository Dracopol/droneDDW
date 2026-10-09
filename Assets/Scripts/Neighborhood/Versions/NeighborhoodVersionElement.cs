using System;
using DroneDDW.Neighborhood.Utilities;
using UnityEngine;

namespace DroneDDW.Neighborhood.Versions
{
    /// <summary>
    /// A street object that only exists in some versions. It holds no logic of its own:
    /// <see cref="NeighborhoodVersionController"/> schedules it and moves it every frame of a transition.
    /// Presence runs from 0 (gone) to 1 (in place) and is shown by scaling (Scale) or lowering (Lift)
    /// the object from the pose it has when the controller first touches it. Lights under the element
    /// fade with it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Version Element")]
    public sealed class NeighborhoodVersionElement : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField, Tooltip("Bit i set = part of version i (1 nature, 2 balanced, 4 efficiency).")]
        private int _versionMask = 7;

        [SerializeField, Tooltip("Scale grows and shrinks from the pivot; Lift rises out of and sinks into the ground.")]
        private NeighborhoodTransitionStyle _style = NeighborhoodTransitionStyle.Scale;

        [SerializeField, Min(0f), Tooltip("How far below its place a Lift element starts, in metres.")]
        private float _liftDepth = 1f;

        [SerializeField, Min(0.1f), Tooltip("Multiplies the controller's element duration (tall towers take longer).")]
        private float _durationScale = 1f;

        [SerializeField, Tooltip("Lights that belong to this object; their intensity follows its presence.")]
        private Light[] _lights = Array.Empty<Light>();

        #endregion

        #region Fields

        private const float MinimumScale = 0.001f;
        private const float MinimumDuration = 0.12f;
        private const float GrowOvershoot = 1.3f;

        private float[] _lightIntensities = Array.Empty<float>();
        private Vector3 _baseLocalPosition;
        private Vector3 _baseLocalScale = Vector3.one;
        private bool _hasBasePose;
        private bool _isAnimating;
        private float _presence = 1f;
        private float _from = 1f;
        private float _to = 1f;
        private float _startTime;
        private float _duration;

        #endregion

        #region Properties

        public int VersionMask => _versionMask;
        public NeighborhoodTransitionStyle Style => _style;
        public float LiftDepth => _liftDepth;
        public float DurationScale => _durationScale;

        /// <summary>0 = gone, 1 = fully in place. Briefly above 1 while a plant overshoots as it grows.</summary>
        public float Presence => _presence;
        public bool IsAnimating => _isAnimating;

        /// <summary>When the current motion ends (Time.time seconds).</summary>
        internal float EndTime => _startTime + _duration;

        #endregion

        #region Public

        public bool IsInVersion(NeighborhoodVersion version)
        {
            return NeighborhoodVersions.Contains(_versionMask, version);
        }

        #endregion

        #region Internal

        internal void Configure(int versionMask, NeighborhoodTransitionStyle style, float liftDepth, float durationScale, Light[] lights)
        {
            _versionMask = versionMask & NeighborhoodVersions.AllMask;
            _style = style;
            _liftDepth = Mathf.Max(0f, liftDepth);
            _durationScale = Mathf.Max(0.1f, durationScale);
            _lights = lights ?? Array.Empty<Light>();
        }

        /// <summary>Distance on the ground plane from the element to a point (where it is now, so movers count).</summary>
        internal float HorizontalDistanceTo(Vector3 point)
        {
            Vector3 position = transform.position;
            float x = position.x - point.x;
            float z = position.z - point.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        internal void ShowImmediate(bool isShown)
        {
            EnsureBasePose();
            _isAnimating = false;
            _presence = isShown ? 1f : 0f;
            _from = _presence;
            _to = _presence;
            ApplyPresence();
        }

        /// <summary>
        /// Points the element at a new state. Returns false when it already rests there.
        /// An element already moving towards the same state keeps its schedule, so a quick second
        /// request does not restart motion that is under way.
        /// </summary>
        internal bool Retarget(bool isShown, float startTime, float duration)
        {
            EnsureBasePose();
            float target = isShown ? 1f : 0f;
            if (_isAnimating && Mathf.Approximately(_to, target))
            {
                return true;
            }

            if (Mathf.Approximately(_presence, target))
            {
                _isAnimating = false;
                _presence = target;
                _from = target;
                _to = target;
                ApplyPresence();
                return false;
            }

            _from = _presence;
            _to = target;
            _startTime = startTime;
            _duration = _style == NeighborhoodTransitionStyle.None
                ? 0f
                : Mathf.Max(MinimumDuration, duration * _durationScale * Mathf.Abs(target - _from));
            _isAnimating = true;
            return true;
        }

        /// <summary>Moves the element to where it should be at this time. Returns true once it has arrived.</summary>
        internal bool Evaluate(float time)
        {
            if (!_isAnimating)
            {
                return true;
            }

            if (time < _startTime)
            {
                return false;
            }

            float t = _duration > 0f ? Mathf.Clamp01((time - _startTime) / _duration) : 1f;
            _presence = _from + (_to - _from) * Ease(t, _to > _from);
            if (t >= 1f)
            {
                _presence = _to;
                _isAnimating = false;
            }

            ApplyPresence();
            return !_isAnimating;
        }

        #endregion

        #region Private

        private void EnsureBasePose()
        {
            if (_hasBasePose)
            {
                return;
            }

            Transform self = transform;
            _baseLocalPosition = self.localPosition;
            _baseLocalScale = self.localScale;
            _lightIntensities = new float[_lights.Length];
            for (int i = 0; i < _lights.Length; i++)
            {
                _lightIntensities[i] = _lights[i] != null ? _lights[i].intensity : 0f;
            }

            _presence = gameObject.activeSelf ? 1f : 0f;
            _from = _presence;
            _to = _presence;
            _hasBasePose = true;
        }

        private float Ease(float t, bool isAppearing)
        {
            switch (_style)
            {
                case NeighborhoodTransitionStyle.Scale:
                    return isAppearing ? NeighborhoodEasing.OutBack(t, GrowOvershoot) : NeighborhoodEasing.InCubic(t);
                case NeighborhoodTransitionStyle.Lift:
                    return isAppearing ? NeighborhoodEasing.OutCubic(t) : NeighborhoodEasing.InCubic(t);
                default:
                    return 1f;
            }
        }

        private void ApplyPresence()
        {
            Transform self = transform;
            if (_presence <= 0f)
            {
                // Rest at the base pose while hidden so the scene never keeps a squashed object.
                if (_style == NeighborhoodTransitionStyle.Lift)
                {
                    self.localPosition = _baseLocalPosition;
                }

                self.localScale = _baseLocalScale;
                if (gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            switch (_style)
            {
                case NeighborhoodTransitionStyle.Scale:
                    self.localScale = _baseLocalScale * Mathf.Max(_presence, MinimumScale);
                    break;
                case NeighborhoodTransitionStyle.Lift:
                    float drop = _liftDepth * (1f - Mathf.Min(_presence, 1f));
                    self.localPosition = new Vector3(_baseLocalPosition.x, _baseLocalPosition.y - drop, _baseLocalPosition.z);
                    break;
            }

            float lightLevel = Mathf.Clamp01(_presence);
            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] != null)
                {
                    _lights[i].intensity = _lightIntensities[i] * lightLevel;
                }
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        #endregion
    }
}
