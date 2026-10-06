using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DroneDDW.Parks.Table
{

    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Parks/Park Time Controller")]
    public sealed class ParkTimeController : MonoBehaviour
    {
        #region Entities

        [Serializable]
        private sealed class StepObjects
        {
            public GameObject[] Objects = Array.Empty<GameObject>();
        }

        #endregion

        #region Serialized fields

        [SerializeField, Tooltip("The time slider (detent count 3).")]
        private ParkLevelSlider _slider;

        [SerializeField, Tooltip("Fog, sun and ambient light per step. Leave empty to change only the objects.")]
        private ParkTimeEnvironment _environment;

        [SerializeField, Tooltip("The scene's directional light.")]
        private Light _sun;

        [SerializeField, Tooltip("Index 0 = 2026, 1 = 2050, 2 = 2100. Only the chosen step's objects are active.")]
        private StepObjects[] _stepObjects = new StepObjects[ParkTimes.Count];

        [SerializeField, Min(0f), Tooltip("Seconds the look takes to blend to the next step. 0 = at once.")]
        private float _transitionSeconds = 2f;

        #endregion

        #region Fields

        private CancellationTokenSource _lifetime;
        private int _transitionId;

        #endregion

        #region Properties

        public int Step { get; private set; }
        public bool IsTransitioning { get; private set; }

        private CancellationToken LifetimeToken
        {
            get
            {
                if (_lifetime == null)
                {
                    _lifetime = new CancellationTokenSource();
                }

                return _lifetime.Token;
            }
        }

        #endregion

        #region Events

        public event Action<ParkTimeChange> OnTimeChange;

        #endregion

        #region Setup

        private void OnValidate()
        {
            if (_stepObjects == null || _stepObjects.Length != ParkTimes.Count)
            {
                Array.Resize(ref _stepObjects, ParkTimes.Count);
            }
        }

        private void OnEnable()
        {
            _slider.OnLevelCommit += HandleCommit;
        }

        private void OnDisable()
        {
            _slider.OnLevelCommit -= HandleCommit;
        }

        private void Start()
        {
            _slider.SetDetent(Step, false);
            ApplyObjects(Step);
            ApplyBlend(Step, Step, 1f);
        }

        private void OnDestroy()
        {
            if (_lifetime == null)
            {
                return;
            }

            _lifetime.Cancel();
            _lifetime.Dispose();
            _lifetime = null;
        }

        #endregion

        #region Public

        /// <summary>Switches to a time step. The slider follows if it isn't already there.</summary>
        public void SetStep(int step, bool immediate)
        {
            step = ParkTimes.Clamp(step);
            if (step == Step)
            {
                return;
            }

            int previous = Step;
            Step = step;
            if (_slider.Detent != step)
            {
                _slider.SetDetent(step, !immediate);
            }

            OnTimeChange?.Invoke(new ParkTimeChange(previous, step, immediate));
            if (immediate || _transitionSeconds <= 0f)
            {
                _transitionId++;
                IsTransitioning = false;
                ApplyObjects(step);
                ApplyBlend(step, step, 1f);
                return;
            }

            TransitionAsync(previous, step, ++_transitionId, LifetimeToken).Forget();
        }

        #endregion

        #region Event handlers

        private void HandleCommit(int step)
        {
            SetStep(step, false);
        }

        #endregion

        #region Private

        private async UniTask TransitionAsync(int from, int to, int transitionId, CancellationToken cancellationToken)
        {
            try
            {
                IsTransitioning = true;
                bool hasSwapped = false;
                float elapsed = 0f;
                while (elapsed < _transitionSeconds)
                {
                    float blend = Mathf.SmoothStep(0f, 1f, elapsed / _transitionSeconds);
                    ApplyBlend(from, to, blend);
                    if (!hasSwapped && blend >= 0.5f)
                    {
                        ApplyObjects(to);
                        hasSwapped = true;
                    }

                    await UniTask.NextFrame(cancellationToken);
                    if (transitionId != _transitionId)
                    {
                        return;
                    }

                    elapsed += Time.deltaTime;
                }

                ApplyObjects(to);
                ApplyBlend(to, to, 1f);
            }
            catch (OperationCanceledException)
            {
                // Destroyed mid-transition.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                if (transitionId == _transitionId)
                {
                    IsTransitioning = false;
                }
            }
        }

        private void ApplyBlend(int from, int to, float blend)
        {
            if (_environment == null)
            {
                return;
            }

            ParkTimeEnvironment.Step a = _environment.GetStep(from);
            ParkTimeEnvironment.Step b = _environment.GetStep(to);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(a.FogColor, b.FogColor, blend);
            RenderSettings.fogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, blend);
            RenderSettings.ambientLight = Color.Lerp(a.AmbientColor, b.AmbientColor, blend);
            if (_sun != null)
            {
                _sun.color = Color.Lerp(a.SunColor, b.SunColor, blend);
                _sun.intensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, blend);
            }
        }

        private void ApplyObjects(int step)
        {
            for (int i = 0; i < _stepObjects.Length; i++)
            {
                if (_stepObjects[i] == null)
                {
                    continue;
                }

                foreach (GameObject go in _stepObjects[i].Objects)
                {
                    if (go != null)
                    {
                        go.SetActive(i == step);
                    }
                }
            }
        }

        #endregion
    }
}