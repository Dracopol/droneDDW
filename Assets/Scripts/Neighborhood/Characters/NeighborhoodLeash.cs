using System;
using UnityEngine;

namespace DroneDDW.Neighborhood.Characters
{
    /// <summary>
    /// The student's leash: a line from the grip (under her hand bone, so it follows her pose) to the dog's
    /// collar, sagging a little in the middle. Lives under the dog of one version, so it comes and goes with it,
    /// and gets thinner as the dog shrinks.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Leash")]
    public sealed class NeighborhoodLeash : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField] private LineRenderer _line;

        [SerializeField, Tooltip("Point in the student's hand (child of her hand bone).")]
        private Transform _grip;

        [SerializeField, Tooltip("Point on the dog's collar.")]
        private Transform _collar;

        [SerializeField, Min(0f), Tooltip("How far the middle hangs down, as a fraction of the length.")]
        private float _sag = 0.06f;

        [SerializeField, Min(0.001f), Tooltip("Width at full size, in metres.")]
        private float _width = 0.014f;

        [SerializeField, Range(2, 24)] private int _pointCount = 10;

        #endregion

        #region Fields

        private Vector3[] _points = Array.Empty<Vector3>();

        #endregion

        #region Setup

        private void OnEnable()
        {
            Refresh();
        }

        #endregion

        #region Update

        private void LateUpdate()
        {
            Refresh();
        }

        #endregion

        #region Internal

        internal void Configure(LineRenderer line, Transform grip, Transform collar)
        {
            _line = line;
            _grip = grip;
            _collar = collar;
        }

        #endregion

        #region Private

        private void Refresh()
        {
            if (_line == null || _grip == null || _collar == null)
            {
                return;
            }

            if (_points.Length != _pointCount)
            {
                _points = new Vector3[_pointCount];
                _line.positionCount = _pointCount;
            }

            Vector3 start = _grip.position;
            Vector3 end = _collar.position;
            float drop = _sag * Vector3.Distance(start, end) * 4f;
            for (int i = 0; i < _pointCount; i++)
            {
                float t = i / (float)(_pointCount - 1);
                Vector3 point = Vector3.Lerp(start, end, t);
                point.y -= drop * t * (1f - t);
                _points[i] = point;
            }

            _line.SetPositions(_points);
            _line.widthMultiplier = _width * transform.lossyScale.x;
        }

        #endregion
    }
}
