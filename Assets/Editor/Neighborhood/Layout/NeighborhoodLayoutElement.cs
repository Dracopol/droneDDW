using System;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Layout
{
    /// <summary>
    /// One object that changes between versions: its node in Street_Elements.fbx, versions, transition,
    /// what it is attached to (a hand bone) and how it moves.
    /// </summary>
    [Serializable]
    internal sealed class NeighborhoodLayoutElement
    {
        #region Serialized fields

        [SerializeField] private string _name = string.Empty;
        [SerializeField] private string _group = string.Empty;
        [SerializeField] private int _mask = 7;
        [SerializeField] private int _style = 1;
        [SerializeField] private float _liftDepth = 0f;
        [SerializeField] private float _durationScale = 1f;
        [SerializeField] private Vector3 _position = Vector3.zero;
        [SerializeField] private int _attachCharacter = -1;
        [SerializeField] private string _attachBone = string.Empty;
        [SerializeField] private NeighborhoodLayoutMover _mover = new NeighborhoodLayoutMover();

        #endregion

        #region Properties

        public string Name => _name;
        public string Group => _group;
        public int Mask => _mask;
        public int Style => _style;
        public float LiftDepth => _liftDepth;
        public float DurationScale => _durationScale;
        public Vector3 Position => _position;
        public int AttachCharacter => _attachCharacter;
        public string AttachBone => _attachBone;
        public NeighborhoodLayoutMover Mover => _mover;

        #endregion
    }
}
