using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Layout
{
    /// <summary>
    /// The default reaction set of a version at a stage.
    /// </summary>
    [Serializable]
    internal sealed class NeighborhoodLayoutReaction
    {
        #region Serialized fields

        [SerializeField] private int _version = 0;
        [SerializeField] private int _stage = 1;
        [SerializeField] private List<string> _lines = new List<string>();

        #endregion

        #region Properties

        public int Version => _version;
        public int Stage => _stage;
        public IReadOnlyList<string> Lines => _lines;

        #endregion
    }
}
