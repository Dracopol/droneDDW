using System;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Layout
{
    /// <summary>
    /// Where the table stands and how big the miniature is.
    /// </summary>
    [Serializable]
    internal sealed class NeighborhoodLayoutTable
    {
        #region Serialized fields

        [SerializeField] private Vector3 _position = Vector3.zero;
        [SerializeField] private float _modelRootHeight = 0f;
        [SerializeField] private float _modelScale = 0.0186f;
        [SerializeField] private float _sliderHalfTravel = 0.3f;

        #endregion

        #region Properties

        public Vector3 Position => _position;
        public float ModelRootHeight => _modelRootHeight;
        public float ModelScale => _modelScale;
        public float SliderHalfTravel => _sliderHalfTravel;

        #endregion
    }
}
