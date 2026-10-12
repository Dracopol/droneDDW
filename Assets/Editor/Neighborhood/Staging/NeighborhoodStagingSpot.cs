using System;
using System.Collections.Generic;
using DroneDDW.Neighborhood.Characters;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Staging
{
    /// <summary>
    /// Where one person goes: their new place on the ground, which way they face (turned from looking at the seat),
    /// the street objects that move and turn with them (their bench, lamp, dog...) and the street objects left out
    /// because they would be in the way there.
    /// </summary>
    [Serializable]
    internal sealed class NeighborhoodStagingSpot
    {
        #region Serialized fields

        [SerializeField] private NeighborhoodCharacterId _character;

        [SerializeField, Tooltip("Where the person stands or sits, in street metres (the seat is at 0, 0). Height is ignored.")]
        private Vector3 _position;

        [SerializeField, Range(-180f, 180f), Tooltip("0 = they face the seat. Positive turns them to their right.")]
        private float _turnFromSeatDegrees;

        [SerializeField, Tooltip("Street objects (node names in Street_Elements.fbx) that move and turn with the person, "
            + "keeping their place around them.")]
        private List<string> _props = new List<string>();

        [SerializeField, Tooltip("Street objects left out of the scene: they would stand in the way at the new place.")]
        private List<string> _removedObjects = new List<string>();

        #endregion

        #region Properties

        public NeighborhoodCharacterId Character => _character;
        public Vector3 Position => new Vector3(_position.x, 0f, _position.z);
        public float TurnFromSeatDegrees => _turnFromSeatDegrees;
        public IReadOnlyList<string> Props => _props;
        public IReadOnlyList<string> RemovedObjects => _removedObjects;

        #endregion

        #region Setup

        internal NeighborhoodStagingSpot(NeighborhoodCharacterId character, Vector3 position, float turnFromSeatDegrees,
            IEnumerable<string> props, IEnumerable<string> removedObjects)
        {
            _character = character;
            _position = position;
            _turnFromSeatDegrees = turnFromSeatDegrees;
            _props = new List<string>(props);
            _removedObjects = new List<string>(removedObjects);
        }

        // For Unity's serializer
        private NeighborhoodStagingSpot()
        {
        }

        #endregion
    }
}
