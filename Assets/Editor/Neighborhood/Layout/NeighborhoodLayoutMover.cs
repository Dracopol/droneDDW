using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Layout
{
    /// <summary>
    /// Motion for a drone, robot, duck or the robin (kind 0 = none).
    /// </summary>
    [Serializable]
    internal sealed class NeighborhoodLayoutMover
    {
        #region Serialized fields

        [SerializeField] private int _kind = 0;
        [SerializeField] private List<Vector3> _points = new List<Vector3>();
        [SerializeField] private Vector3 _center = Vector3.zero;
        [SerializeField] private float _radiusX = 0f;
        [SerializeField] private float _radiusZ = 0f;
        [SerializeField] private float _speed = 0f;
        [SerializeField] private float _phase = 0f;
        [SerializeField] private float _bobHeight = 0f;
        [SerializeField] private float _bobPeriod = 2f;
        [SerializeField] private bool _faceTravel = true;
        [SerializeField] private bool _clockwise = false;

        #endregion

        #region Properties

        public int Kind => _kind;
        public IReadOnlyList<Vector3> Points => _points;
        public Vector3 Center => _center;
        public float RadiusX => _radiusX;
        public float RadiusZ => _radiusZ;
        public float Speed => _speed;
        public float Phase => _phase;
        public float BobHeight => _bobHeight;
        public float BobPeriod => _bobPeriod;
        public bool FaceTravel => _faceTravel;
        public bool Clockwise => _clockwise;

        #endregion
    }
}
