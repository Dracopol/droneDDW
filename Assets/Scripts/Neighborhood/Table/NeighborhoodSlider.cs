using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Neighborhood.Utilities;
using UnityEngine;

namespace DroneDDW.Neighborhood.Table
{
    /// <summary>
    /// The three-notch slider on the table (ported from the park's level slider). Put it on the handle's parent
    /// (Rig_Slider): the handle moves along this object's local X axis. While dragging it reports the detent it is
    /// over; when the drag ends it snaps to that detent and commits it. Input comes from
    /// <see cref="NeighborhoodSliderHandDriver"/> (push and finger grab).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Slider")]
    public sealed class NeighborhoodSlider : MonoBehaviour
    {
        #region Events

        public event Action<int> OnDetentChange;

        public event Action<int> OnVersionCommit;

        public event Action<NeighborhoodSliderContact> OnContactChange;

        #endregion

        #region Serialized fields

        [SerializeField, Tooltip("The part that slides (Rig_Handle), a child of this object.")]
        private Transform _handle;

        [SerializeField, Tooltip("Local X of detent 0.")]
        private float _minLocalX = -0.3f;

        [SerializeField, Tooltip("Local X of the last detent.")]
        private float _maxLocalX = 0.3f;

        [SerializeField, Min(0.01f), Tooltip("Seconds the handle takes to settle on a detent after release.")]
        private float _snapDuration = 0.18f;

        [Header("Detents")]
        [SerializeField, Range(2, 8), Tooltip("How many detents this slider has: 3, one per neighborhood version.")]
        private int _detentCount = 3;

        [SerializeField, Range(0f, 0.5f), Tooltip("How far past the halfway point between two detents the handle "
            + "has to go before the next one counts, as a fraction of a step, so it doesn't flicker when resting "
            + "in between.")]
        private float _detentHysteresis = 0.1f;

        [SerializeField, Min(0f), Tooltip("While grabbed, how far the hand goes past a detent before the handle "
            + "follows, so each detent catches it for a moment. 0 = off.")]
        private float _softDetentMeters = 0.012f;

        #endregion

        #region Fields

        private CancellationTokenSource _lifetime;
        private bool _isDragging;
        private float _grabOffset;
        private int _detent = -1;
        private int _snapId;
        private NeighborhoodSliderContact _contact;

        #endregion

        #region Properties

        public bool IsDragging => _isDragging;

        public int Detent => _detent;
        public int DetentCount => _detentCount;
        public NeighborhoodSliderContact Contact => _contact;
        public Transform Handle => _handle;

        public float HandleBlend => Mathf.InverseLerp(_minLocalX, _maxLocalX, HandleX);

        private float HandleX => _handle.localPosition.x;
        private float StepMeters => Mathf.Abs(_maxLocalX - _minLocalX) / (_detentCount - 1);

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

        #region Setup

        // Starts on the notch its handle stands at, so the choice is known (and the lock-in button can arm) before anyone
        // moves it. In Awake, so it holds as soon as a hidden table is shown.
        private void Awake()
        {
            if (_detent < 0 && _handle != null)
            {
                _detent = XToDetent(HandleX);
            }
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

        public void BeginDrag(Vector3 worldGrabPoint)
        {
            _snapId++;
            _isDragging = true;
            _grabOffset = HandleX - ToLocalX(worldGrabPoint);
            if (_detent < 0)
            {
                _detent = XToDetent(HandleX);
            }
        }

        public void DragTo(Vector3 worldPoint)
        {
            if (!_isDragging)
            {
                return;
            }

            float x = Mathf.Clamp(ToLocalX(worldPoint) + _grabOffset, Mathf.Min(_minLocalX, _maxLocalX), Mathf.Max(_minLocalX, _maxLocalX));
            if (_contact == NeighborhoodSliderContact.Grabbed)
            {
                x = ApplySoftDetents(x);
            }

            SetHandleX(x);
            int detent = XToDetent(x);
            if (detent == _detent || !HasLeftDetent(x))
            {
                return;
            }

            _detent = detent;
            OnDetentChange?.Invoke(detent);
        }

        public void EndDrag()
        {
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;
            SnapToAsync(DetentToX(_detent), ++_snapId, LifetimeToken).Forget();
            OnVersionCommit?.Invoke(_detent);
        }

        public void SetDetent(int detent, bool isAnimated)
        {
            if (_isDragging)
            {
                return;
            }

            _detent = ClampDetent(detent);
            int snapId = ++_snapId;
            if (isAnimated && isActiveAndEnabled)
            {
                SnapToAsync(DetentToX(_detent), snapId, LifetimeToken).Forget();
                return;
            }

            SetHandleX(DetentToX(_detent));
        }

        public void SetContact(NeighborhoodSliderContact contact)
        {
            if (contact == _contact)
            {
                return;
            }

            _contact = contact;
            OnContactChange?.Invoke(contact);
        }

        #endregion

        #region Internal

        internal void Configure(Transform handle, float minLocalX, float maxLocalX)
        {
            _handle = handle;
            _minLocalX = minLocalX;
            _maxLocalX = maxLocalX;
        }

        internal void Configure(Transform handle, float minLocalX, float maxLocalX, int detentCount)
        {
            Configure(handle, minLocalX, maxLocalX);
            _detentCount = Mathf.Clamp(detentCount, 2, 8);
        }

        #endregion

        #region Private

        private int ClampDetent(int detent)
        {
            return Mathf.Clamp(detent, 0, _detentCount - 1);
        }

        private float ToLocalX(Vector3 worldPoint)
        {
            return transform.InverseTransformPoint(worldPoint).x;
        }

        private float DetentToX(int detent)
        {
            return Mathf.Lerp(_minLocalX, _maxLocalX, detent / (float)(_detentCount - 1));
        }

        private int XToDetent(float x)
        {
            return ClampDetent(Mathf.RoundToInt(Mathf.InverseLerp(_minLocalX, _maxLocalX, x) * (_detentCount - 1)));
        }

        private bool HasLeftDetent(float x)
        {
            return _detent < 0 || Mathf.Abs(x - DetentToX(_detent)) >= StepMeters * (0.5f + _detentHysteresis);
        }

  
        private float ApplySoftDetents(float x)
        {
            float halfStepMeters = StepMeters * 0.5f;
            if (_softDetentMeters <= 0f || _softDetentMeters >= halfStepMeters)
            {
                return x;
            }

            float detentX = DetentToX(XToDetent(x));
            float offsetMeters = x - detentX;
            float stretchedMeters = Mathf.Max(0f, Mathf.Abs(offsetMeters) - _softDetentMeters)
                * halfStepMeters / (halfStepMeters - _softDetentMeters);
            return detentX + Mathf.Sign(offsetMeters) * stretchedMeters;
        }

        private void SetHandleX(float x)
        {
            Vector3 position = _handle.localPosition;
            position.x = x;
            _handle.localPosition = position;
        }

        private async UniTask SnapToAsync(float targetX, int snapId, CancellationToken cancellationToken)
        {
            try
            {
                float startX = HandleX;
                float elapsed = 0f;
                while (elapsed < _snapDuration)
                {
                    await UniTask.NextFrame(cancellationToken);
                    if (snapId != _snapId)
                    {
                        return;
                    }

                    elapsed += Time.deltaTime;
                    SetHandleX(Mathf.LerpUnclamped(startX, targetX, NeighborhoodEasing.OutCubic(Mathf.Clamp01(elapsed / _snapDuration))));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        #endregion
    }
}