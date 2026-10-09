using System;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Layout
{
    /// <summary>
    /// A point light (street lamp, drone, table) and the light pool under it.
    /// </summary>
    [Serializable]
    internal sealed class NeighborhoodLayoutLight
    {
        #region Serialized fields

        [SerializeField] private string _name = string.Empty;
        [SerializeField] private string _owner = string.Empty;
        [SerializeField] private int _mask = 7;
        [SerializeField] private string _kind = string.Empty;
        [SerializeField] private Vector3 _position = Vector3.zero;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private float _intensity = 1f;
        [SerializeField] private float _range = 10f;
        [SerializeField] private float _poolIntensity = 0f;
        [SerializeField] private float _poolRadius = 0f;

        #endregion

        #region Properties

        public string Name => _name;
        public string Owner => _owner;
        public int Mask => _mask;
        public string Kind => _kind;
        public Vector3 Position => _position;
        public Color Color => _color;
        public float Intensity => _intensity;
        public float Range => _range;
        public float PoolIntensity => _poolIntensity;
        public float PoolRadius => _poolRadius;

        #endregion
    }
}
