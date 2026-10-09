using System;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Layout
{
    /// <summary>
    /// A character material from the kit: flat colour or the shared texture.
    /// </summary>
    [Serializable]
    internal sealed class NeighborhoodLayoutMaterial
    {
        #region Serialized fields

        [SerializeField] private string _name = string.Empty;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private bool _isTextured = false;
        [SerializeField] private float _smoothness = 0.2f;

        #endregion

        #region Properties

        public string Name => _name;
        public Color Color => _color;
        public bool IsTextured => _isTextured;
        public float Smoothness => _smoothness;

        #endregion
    }
}
