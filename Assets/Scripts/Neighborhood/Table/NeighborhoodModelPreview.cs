using System;
using DroneDDW.Neighborhood.Versions;
using UnityEngine;

namespace DroneDDW.Neighborhood.Table
{
    /// <summary>
    /// The miniature street on the table: shows one of the three version models and turns the view cone
    /// around the you-are-here pin so it points where the viewer is looking in the real street.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Model Preview")]
    public sealed class NeighborhoodModelPreview : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField, Tooltip("One miniature per version: nature, balanced, efficiency.")]
        private GameObject[] _models = Array.Empty<GameObject>();

        [SerializeField, Tooltip("The cone on the pin (Rig_ViewCone). Its forward points north at rotation zero.")]
        private Transform _viewCone;

        [SerializeField, Tooltip("The real street, used as the north reference. Leave empty to use world axes.")]
        private Transform _streetRoot;

        [SerializeField, Tooltip("The XR camera. Falls back to Camera.main when left empty.")]
        private Transform _head;

        [SerializeField, Min(0f), Tooltip("How quickly the cone follows the head. 0 = no smoothing.")]
        private float _coneSmoothing = 10f;

        #endregion

        #region Fields

        private NeighborhoodVersion _shownVersion;
        private float _coneYaw;
        private bool _hasConeYaw;

        #endregion

        #region Properties

        public NeighborhoodVersion ShownVersion => _shownVersion;

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

            Vector3 forward = _streetRoot != null ? _streetRoot.InverseTransformDirection(head.forward) : head.forward;
            if (forward.x * forward.x + forward.z * forward.z < 0.0001f)
            {
                // Looking straight up or down: keep the last direction.
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

        public void ShowVersion(NeighborhoodVersion version)
        {
            _shownVersion = version;
            for (int i = 0; i < _models.Length; i++)
            {
                if (_models[i] != null)
                {
                    _models[i].SetActive(i == (int)version);
                }
            }
        }

        #endregion

        #region Internal

        internal void Configure(GameObject[] models, Transform viewCone, Transform streetRoot, Transform head)
        {
            _models = models ?? Array.Empty<GameObject>();
            _viewCone = viewCone;
            _streetRoot = streetRoot;
            _head = head;
        }

        /// <summary>Follows another head, e.g. the experience rig's once it has loaded this scene.</summary>
        internal void SetHead(Transform head)
        {
            _head = head;
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
