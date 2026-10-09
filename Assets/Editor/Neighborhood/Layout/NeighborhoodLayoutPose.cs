using System;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Layout
{
    /// <summary>
    /// A character's clip and resting face in one version.
    /// </summary>
    [Serializable]
    internal sealed class NeighborhoodLayoutPose
    {
        #region Serialized fields

        [SerializeField] private int _version = 0;
        [SerializeField] private string _clip = string.Empty;
        [SerializeField] private int _emotion = 0;

        #endregion

        #region Properties

        public int Version => _version;
        public string Clip => _clip;
        public int Emotion => _emotion;

        #endregion
    }
}
