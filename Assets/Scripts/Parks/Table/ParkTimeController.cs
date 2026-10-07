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
        #region Serialized fields

        [SerializeField, Tooltip("The time slider (detent count 3).")]
        private ParkLevelSlider _slider;

        [SerializeField, Min(0f), Tooltip("Seconds the environment takes to glide to the next step. 0 = at once.")]
        private float _transitionSeconds = 3f;

        #endregion

        #region Fields

        private CancellationTokenSource _lifetime;
        private int _transitionId;

        #endregion

        #region Properties

        public ParkLevelSlider Slider => _slider;

        public int Step { get; private set; }

        public float Progress { get; private set; }

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
            Progress = Step;
            _slider.SetDetent(Step, false);
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
                Progress = step;
                return;
            }

            TransitionAsync(step, ++_transitionId, LifetimeToken).Forget();
        }

        #endregion

        #region Event handlers

        private void HandleCommit(int step)
        {
            SetStep(step, false);
        }

        #endregion

        #region Private

        private async UniTask TransitionAsync(int target, int transitionId, CancellationToken cancellationToken)
        {
            try
            {
                IsTransitioning = true;
                float start = Progress;
                float elapsed = 0f;
                while (elapsed < _transitionSeconds)
                {
                    await UniTask.NextFrame(cancellationToken);
                    if (transitionId != _transitionId)
                    {
                        return;
                    }

                    elapsed += Time.deltaTime;
                    float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _transitionSeconds));
                    Progress = Mathf.Lerp(start, target, blend);
                }

                Progress = target;
            }
            catch (OperationCanceledException)
            {
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

        #endregion
    }
}