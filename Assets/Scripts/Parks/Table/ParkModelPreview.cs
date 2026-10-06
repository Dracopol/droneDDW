using System;
using DroneDDW.Parks.Levels;
using UnityEngine;

namespace DroneDDW.Parks.Table
{
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Parks/Park Model Preview")]
    public sealed class ParkModelPreview : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField, Tooltip("One miniature per level, level 0 first.")]
        private GameObject[] _models = Array.Empty<GameObject>();

        [SerializeField, Tooltip("Optional. One layer per time step: 2026, 2050, 2100.")]
        private GameObject[] _timeModels = Array.Empty<GameObject>();

        [SerializeField, Tooltip("Shows the layer of the committed time step. Optional.")]
        private ParkTimeController _timeController;

        [SerializeField, Tooltip("The cone on the pin (Rig_ViewCone). Its forward points north at rotation zero.")]
        private Transform _viewCone;

        [SerializeField, Tooltip("The real park, used as the north reference. Leave empty to use world axes.")]
        private Transform _parkRoot;

        [SerializeField, Tooltip("The XR camera. Falls back to Camera.main when left empty.")]
        private Transform _head;

        [SerializeField, Min(0f), Tooltip("How quickly the cone follows the head. 0 = no smoothing.")]
        private float _coneSmoothing = 10f;

        #endregion

        #region Fields

        private int _shownLevel = -1;
        private int _shownTime = -1;
        private float _coneYaw;
        private bool _hasConeYaw;

        #endregion

        #region Properties

        public int ShownLevel => _shownLevel;
        public int ShownTime => _shownTime;

        #endregion

        #region Setup

        private void OnEnable()
        {
            if (_timeController == null)
            {
                return;
            }

            _timeController.OnTimeChange += HandleTimeChange;
            ShowTime(_timeController.Step);
        }

        private void OnDisable()
        {
            if (_timeController != null)
            {
                _timeController.OnTimeChange -= HandleTimeChange;
            }
        }

        #endregion

        #region Update

        private void LateUpdate()
        {
            if (_viewCone == null)
            {
                return;
            }

            Transform head = ResolveHead();
            if (head == null)
            {
                return;
            }

            Vector3 forward = _parkRoot != null ? _parkRoot.InverseTransformDirection(head.forward) : head.forward;
            if (forward.x * forward.x + forward.z * forward.z < 0.0001f)
            {
                return;
            }

            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            if (!_hasConeYaw || _coneSmoothing <= 0f)
            {
                _coneYaw = yaw;
                _hasConeYaw = true;
            }
            else
            {
                _coneYaw = Mathf.LerpAngle(_coneYaw, yaw, 1f - Mathf.Exp(-_coneSmoothing * Time.deltaTime));
            }

            _viewCone.localRotation = Quaternion.Euler(0f, _coneYaw, 0f);
        }

        #endregion

        #region Public

        public void ShowLevel(int level)
        {
            level = ParkLevels.Clamp(level);
            _shownLevel = level;
            for (int i = 0; i < _models.Length; i++)
            {
                if (_models[i] != null)
                {
                    _models[i].SetActive(i == level);
                }
            }
        }

        public void ShowTime(int step)
        {
            step = ParkTimes.Clamp(step);
            _shownTime = step;
            for (int i = 0; i < _timeModels.Length; i++)
            {
                if (_timeModels[i] != null)
                {
                    _timeModels[i].SetActive(i == step);
                }
            }
        }

        #endregion

        #region Event handlers

        private void HandleTimeChange(ParkTimeChange change)
        {
            ShowTime(change.Next);
        }

        #endregion

        #region Internal

        internal void Configure(GameObject[] models, Transform viewCone, Transform parkRoot, Transform head)
        {
            _models = models ?? Array.Empty<GameObject>();
            _viewCone = viewCone;
            _parkRoot = parkRoot;
            _head = head;
        }

        internal void ConfigureTime(GameObject[] timeModels, ParkTimeController timeController)
        {
            _timeModels = timeModels ?? Array.Empty<GameObject>();
            _timeController = timeController;
        }

        #endregion

        #region Private

        private Transform ResolveHead()
        {
            if (_head != null)
            {
                return _head;
            }

            Camera main = Camera.main;
            return main != null ? main.transform : null;
        }

        #endregion
    }
}