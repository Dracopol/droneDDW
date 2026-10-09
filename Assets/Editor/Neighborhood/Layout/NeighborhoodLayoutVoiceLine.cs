using System;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Layout
{
    /// <summary>
    /// A line of the voice script.
    /// </summary>
    [Serializable]
    internal sealed class NeighborhoodLayoutVoiceLine
    {
        #region Serialized fields

        [SerializeField] private string _id = string.Empty;
        [SerializeField] private int _character = 0;
        [SerializeField] private int _emotion = 0;
        [SerializeField] private int _side = 0;
        [SerializeField] private int _stage = 1;
        [SerializeField] private string _trigger = string.Empty;
        [SerializeField] private string _text = string.Empty;

        #endregion

        #region Properties

        public string Id => _id;
        public int Character => _character;
        public int Emotion => _emotion;
        public int Side => _side;
        public int Stage => _stage;
        public string Trigger => _trigger;
        public string Text => _text;

        #endregion
    }
}
