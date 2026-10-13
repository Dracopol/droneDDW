using System;
using DroneDDW.Neighborhood.Versions;
using UnityEngine;

namespace DroneDDW.Neighborhood.Characters
{
    /// <summary>
    /// What a character does in one version: the animator state (a looping pose clip), the face, and whether they
    /// stand (and how far below their origin their soles reach) or sit.
    /// </summary>
    [Serializable]
    public sealed class NeighborhoodCharacterPose
    {
        #region Serialized fields

        [SerializeField] private NeighborhoodVersion _version;

        [SerializeField, Tooltip("Animator state to play, for example Pose_Nature.")]
        private string _state = string.Empty;

        [SerializeField, Tooltip("Face while not talking in this version.")]
        private NeighborhoodEmotion _emotion;

        [SerializeField, Tooltip("On: the person stands, their soles on the ground. Off: they sit, on a seat that "
            + "stands on the same ground. Either way they rise and sink with the ground (the paving comes and goes).")]
        private bool _isStanding;

        [SerializeField, Min(0f), Tooltip("How far below the person's origin their soles reach in this pose, in metres; "
            + "a standing person is raised by this so they rest on the ground.")]
        private float _soleDepth;

        #endregion

        #region Properties

        public NeighborhoodVersion Version => _version;
        public string State => _state;
        public NeighborhoodEmotion Emotion => _emotion;
        public bool IsStanding => _isStanding;
        public float SoleDepth => _soleDepth;

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

        #region Internal

        internal void SetStance(bool isStanding, float soleDepth)
        {
            _isStanding = isStanding;
            _soleDepth = Mathf.Max(0f, soleDepth);
        }

        #endregion
    }
}
