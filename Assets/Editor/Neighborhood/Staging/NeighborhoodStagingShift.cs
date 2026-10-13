using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Staging
{
    /// <summary>
    /// Street objects moved as one group, without anyone with them: for example the Balanced pond, brought nearer to
    /// where Grandma sits so its shore is as close to her as the Nature pond's.
    /// </summary>
    [Serializable]
    internal sealed class NeighborhoodStagingShift
    {
        #region Serialized fields

        [SerializeField, Tooltip("What the move is for, for the build log.")]
        private string _label = string.Empty;

        [SerializeField, Tooltip("How far the objects move, in street metres. Height is ignored.")]
        private Vector3 _offset;

        [SerializeField, Tooltip("Street objects (node names in Street_Elements.fbx) that move; routes of moving ones "
            + "(the ducks' circles) move with them.")]
        private List<string> _objects = new List<string>();

        #endregion

        #region Properties

        public string Label => _label;
        public Vector3 Offset => new Vector3(_offset.x, 0f, _offset.z);
        public IReadOnlyList<string> Objects => _objects;

        #endregion

        #region Setup

        internal NeighborhoodStagingShift(string label, Vector3 offset, IEnumerable<string> objects)
        {
            _label = label;
            _offset = offset;
            _objects = new List<string>(objects);
        }

        // For Unity's serializer
        private NeighborhoodStagingShift()
        {
        }

        #endregion
    }
}
