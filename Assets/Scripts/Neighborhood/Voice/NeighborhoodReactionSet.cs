using System;
using System.Collections.Generic;
using DroneDDW.Neighborhood.Versions;
using UnityEngine;

namespace DroneDDW.Neighborhood.Voice
{
    /// <summary>The lines that play, in order, after the street settles on a version at a stage.</summary>
    [Serializable]
    public sealed class NeighborhoodReactionSet
    {
        #region Serialized fields

        [SerializeField] private NeighborhoodVersion _version;
        [SerializeField] private NeighborhoodVoiceStage _stage = NeighborhoodVoiceStage.Now;

        [SerializeField, Tooltip("Line IDs in the order they are spoken, for example GRA_04, WOR_02, STU_03.")]
        private string[] _lineIds = Array.Empty<string>();

        #endregion

        #region Properties

        public NeighborhoodVersion Version => _version;
        public NeighborhoodVoiceStage Stage => _stage;
        public IReadOnlyList<string> LineIds => _lineIds;

        #endregion

        #region Setup

        public NeighborhoodReactionSet()
        {
        }

        public NeighborhoodReactionSet(NeighborhoodVersion version, NeighborhoodVoiceStage stage, string[] lineIds)
        {
            _version = version;
            _stage = stage;
            _lineIds = lineIds ?? Array.Empty<string>();
        }

        #endregion
    }
}
