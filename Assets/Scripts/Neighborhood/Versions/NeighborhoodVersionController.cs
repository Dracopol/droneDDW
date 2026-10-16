using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Neighborhood.Utilities;
using UnityEngine;

namespace DroneDDW.Neighborhood.Versions
{
    /// <summary>
    /// Moves the street between its three versions. A change travels outwards from the wave origin (the seat)
    /// as a ring: each element waits until the ring reaches it, then leaves (shrinks or sinks) or arrives
    /// (grows or rises). Arrivals start a little after departures so old and new never pile up, everything
    /// close to the seat moves more slowly so nothing pops up next to the viewer, and far objects (the
    /// skyline) never wait longer than the maximum delay. A new request during a transition takes over from
    /// wherever each element is at that moment.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Version Controller")]
    public sealed class NeighborhoodVersionController : MonoBehaviour
    {
        #region Events

        /// <summary>A version was requested and the street starts moving (or snapped) to it.</summary>
        public event Action<NeighborhoodVersionChange> OnVersionChange;

        /// <summary>Progress of the running transition, 0 to 1, raised every frame while it runs.</summary>
        public event Action<float> OnTransitionProgress;

        /// <summary>Every element reached the given version.</summary>
        public event Action<NeighborhoodVersion> OnTransitionComplete;

        #endregion

        #region Serialized fields

        [SerializeField, Tooltip("Every object that changes between versions.")]
        private NeighborhoodVersionElement[] _elements = Array.Empty<NeighborhoodVersionElement>();

        [SerializeField, Tooltip("Where the wave starts; normally the seat. Falls back to this object.")]
        private Transform _waveOrigin;

        [SerializeField, Tooltip("The version the street shows when the scene starts.")]
        private NeighborhoodVersion _startVersion = NeighborhoodVersion.Balanced;

        [Header("Wave")]
        [SerializeField, Min(1f), Tooltip("Speed of the ring, metres per second. 20 m/s reaches the houses in under a second.")]
        private float _waveSpeed = 20f;

        [SerializeField, Min(0f), Tooltip("Nothing waits longer than this for the ring (the far streets and the skyline).")]
        private float _maxWaveDelay = 1.2f;

        [SerializeField, Min(0.05f), Tooltip("Seconds one element takes to leave or arrive.")]
        private float _elementDuration = 0.9f;

        [SerializeField, Min(0f), Tooltip("Extra wait before arrivals start, so departures clear the way first.")]
        private float _arriveDelay = 0.35f;

        [SerializeField, Min(0f), Tooltip("Elements closer to the wave origin than this move more slowly.")]
        private float _gentleRadius = 6f;

        [SerializeField, Min(1f), Tooltip("Duration multiplier inside the gentle radius.")]
        private float _gentleDurationScale = 1.8f;

        #endregion

        #region Fields

        private readonly List<NeighborhoodVersionElement> _moving = new List<NeighborhoodVersionElement>();
        private readonly NeighborhoodLifetime _lifetime = new NeighborhoodLifetime();
        private int _transitionId;
        private NeighborhoodVersion _currentVersion;
        private bool _hasVersion;
        private bool _isTransitioning;

        #endregion

        #region Properties

        /// <summary>The version the street is at, or moving to while a transition runs.</summary>
        public NeighborhoodVersion CurrentVersion => _currentVersion;
        public bool HasVersion => _hasVersion;
        public bool IsTransitioning => _isTransitioning;
        public NeighborhoodVersion StartVersion => _startVersion;
        public IReadOnlyList<NeighborhoodVersionElement> Elements => _elements;
        public Vector3 WaveOrigin => _waveOrigin != null ? _waveOrigin.position : transform.position;

        #endregion

        #region Setup

        private void Awake()
        {
            SetVersionImmediate(_startVersion);
        }

        private void OnDestroy()
        {
            _lifetime.End();
        }

        #endregion

        #region Public

        /// <summary>Starts a transition and forgets about it. Safe to call from input handlers.</summary>
        public void RequestVersion(NeighborhoodVersion version)
        {
            RequestVersionAsync(version, _lifetime.Token).Forget();
        }

        public void StepVersion(int delta)
        {
            RequestVersion(NeighborhoodVersions.Step(_currentVersion, delta));
        }

        /// <summary>
        /// Moves the street to a version with the wave transition.
        /// Returns true when the street arrived, false when a newer request took over first.
        /// </summary>
        public async UniTask<bool> SetVersionAsync(NeighborhoodVersion version, CancellationToken cancellationToken)
        {
            version = NeighborhoodVersions.Clamp((int)version);
            if (_hasVersion && version == _currentVersion && !_isTransitioning)
            {
                return true;
            }

            int transitionId = ++_transitionId;
            NeighborhoodVersion previous = _currentVersion;
            bool hadVersion = _hasVersion;
            float startTime = Time.time;
            float endTime = PlanTransition(version, startTime);
            _currentVersion = version;
            _hasVersion = true;
            _isTransitioning = true;
            OnVersionChange?.Invoke(new NeighborhoodVersionChange(previous, version, hadVersion, false));

            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            try
            {
                bool isFinished = false;
                while (!isFinished)
                {
                    await NeighborhoodFrames.NextFrameAsync(linked.Token);
                    if (transitionId != _transitionId)
                    {
                        // A newer request re-planned every element; it owns them now.
                        return false;
                    }

                    float now = Time.time;
                    isFinished = EvaluateMoving(now);
                    float span = endTime - startTime;
                    OnTransitionProgress?.Invoke(span > 0f ? Mathf.Clamp01((now - startTime) / span) : 1f);
                }
            }
            finally
            {
                linked.Dispose();
                if (transitionId == _transitionId)
                {
                    _isTransitioning = false;
                }
            }

            OnTransitionComplete?.Invoke(version);
            return true;
        }

        /// <summary>Snaps the street to a version without a transition.</summary>
        public void SetVersionImmediate(NeighborhoodVersion version)
        {
            version = NeighborhoodVersions.Clamp((int)version);
            _transitionId++;
            _isTransitioning = false;
            _moving.Clear();
            NeighborhoodVersion previous = _currentVersion;
            bool hadVersion = _hasVersion;
            foreach (NeighborhoodVersionElement element in _elements)
            {
                if (element != null)
                {
                    element.ShowImmediate(element.IsInVersion(version));
                }
            }

            _currentVersion = version;
            _hasVersion = true;
            OnVersionChange?.Invoke(new NeighborhoodVersionChange(previous, version, hadVersion, true));
            OnTransitionComplete?.Invoke(version);
        }

        /// <summary>
        /// Seconds after a change starts until the ring reaches a point, plus the arrival wait when asked.
        /// Characters use it to change their pose when the ring passes them.
        /// </summary>
        public float GetWaveDelay(Vector3 position, bool isArriving)
        {
            Vector3 origin = WaveOrigin;
            float x = position.x - origin.x;
            float z = position.z - origin.z;
            float delay = Mathf.Min(Mathf.Sqrt(x * x + z * z) / _waveSpeed, _maxWaveDelay);
            return delay + (isArriving ? _arriveDelay : 0f);
        }

        #endregion

        #region Internal

        internal void Configure(NeighborhoodVersionElement[] elements, Transform waveOrigin, NeighborhoodVersion startVersion)
        {
            _elements = elements ?? Array.Empty<NeighborhoodVersionElement>();
            _waveOrigin = waveOrigin;
            _startVersion = startVersion;
        }

        /// <summary>Edit mode only: switches objects on and off to show one version while editing.</summary>
        internal void ShowVersionInEditor(NeighborhoodVersion version)
        {
            foreach (NeighborhoodVersionElement element in _elements)
            {
                if (element != null)
                {
                    element.gameObject.SetActive(element.IsInVersion(version));
                }
            }
        }

        internal int CountElementsInVersion(NeighborhoodVersion version)
        {
            int count = 0;
            foreach (NeighborhoodVersionElement element in _elements)
            {
                if (element != null && element.IsInVersion(version))
                {
                    count++;
                }
            }

            return count;
        }

        #endregion

        #region Private

        private async UniTask RequestVersionAsync(NeighborhoodVersion version, CancellationToken cancellationToken)
        {
            try
            {
                await SetVersionAsync(version, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The controller was destroyed mid-transition.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        /// <summary>Schedules every element that has to change. Returns the time the last one arrives.</summary>
        private float PlanTransition(NeighborhoodVersion version, float startTime)
        {
            _moving.Clear();
            Vector3 origin = WaveOrigin;
            float endTime = startTime;
            foreach (NeighborhoodVersionElement element in _elements)
            {
                if (element == null)
                {
                    continue;
                }

                bool isShown = element.IsInVersion(version);
                float distance = element.HorizontalDistanceTo(origin);
                float delay = Mathf.Min(distance / _waveSpeed, _maxWaveDelay) + (isShown ? _arriveDelay : 0f);
                float duration = _elementDuration * (distance < _gentleRadius ? _gentleDurationScale : 1f);
                if (!element.Retarget(isShown, startTime + delay, duration))
                {
                    continue;
                }

                _moving.Add(element);
                endTime = Mathf.Max(endTime, element.EndTime);
            }

            return endTime;
        }

        private bool EvaluateMoving(float time)
        {
            bool isFinished = true;
            foreach (NeighborhoodVersionElement element in _moving)
            {
                if (element != null && !element.Evaluate(time))
                {
                    isFinished = false;
                }
            }

            return isFinished;
        }

        #endregion
    }
}
