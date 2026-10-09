using System;
using DroneDDW.Neighborhood.Versions;
using UnityEngine;

namespace DroneDDW.Neighborhood.Characters
{
    /// <summary>What a character does in one version: the animator state (a looping pose clip) and the face.</summary>
    [Serializable]
    public sealed class NeighborhoodCharacterPose
    {
        #region Serialized fields

        [SerializeField] private NeighborhoodVersion _version;

        [SerializeField, Tooltip("Animator state to play, for example Pose_Nature.")]
        private string _state = string.Empty;

        [SerializeField, Tooltip("Face while not talking in this version.")]
        private NeighborhoodEmotion _emotion;

        #endregion

        #region Properties

        public NeighborhoodVersion Version => _version;
        public string State => _state;
        public NeighborhoodEmotion Emotion => _emotion;

        #endregion

        #region Setup

        public NeighborhoodCharacterPose()
        {
        }

        public NeighborhoodCharacterPose(NeighborhoodVersion version, string state, NeighborhoodEmotion emotion)
        {
            _version = version;
            _state = state;
            _emotion = emotion;
        }

        #endregion
    }
}
