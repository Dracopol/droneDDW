using System;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Layout
{
    /// <summary>
    /// The student's leash in one version: grip in her hand, collar on that version's dog.
    /// </summary>
    [Serializable]
    internal sealed class NeighborhoodLayoutLeash
    {
        #region Serialized fields

        [SerializeField] private string _element = string.Empty;
        [SerializeField] private int _character = 2;
        [SerializeField] private string _bone = string.Empty;
        [SerializeField] private Vector3 _grip = Vector3.zero;
        [SerializeField] private Vector3 _collar = Vector3.zero;
        [SerializeField] private int _version = 0;

        #endregion

        #region Properties

        public string Element => _element;
        public int Character => _character;
        public string Bone => _bone;
        public Vector3 Grip => _grip;
        public Vector3 Collar => _collar;
        public int Version => _version;

        #endregion
    }
}
