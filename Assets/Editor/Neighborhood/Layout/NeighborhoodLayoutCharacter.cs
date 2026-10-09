using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Layout
{
    /// <summary>
    /// One of the three people: model, spot, and the pose clip and face per version.
    /// </summary>
    [Serializable]
    internal sealed class NeighborhoodLayoutCharacter
    {
        #region Serialized fields

        [SerializeField] private int _id = 0;
        [SerializeField] private string _name = string.Empty;
        [SerializeField] private string _model = string.Empty;
        [SerializeField] private Vector3 _position = Vector3.zero;
        [SerializeField] private float _rotationY = 0f;
        [SerializeField] private float _headHeight = 1.5f;
        [SerializeField] private List<NeighborhoodLayoutPose> _poses = new List<NeighborhoodLayoutPose>();
        [SerializeField] private List<string> _meshes = new List<string>();

        #endregion

        #region Properties

        public int Id => _id;
        public string Name => _name;
        public string Model => _model;
        public Vector3 Position => _position;
        public float RotationY => _rotationY;
        public float HeadHeight => _headHeight;
        public IReadOnlyList<NeighborhoodLayoutPose> Poses => _poses;
        public IReadOnlyList<string> Meshes => _meshes;

        #endregion
    }
}
