using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Layout
{
    /// <summary>
    /// The whole layout file (Layouts/Street_Layout.json) written by the Blender export.
    /// Positions are in Unity space: x = east, y = up, z = north, 1 unit = 1 m. The seat is at the origin.
    /// </summary>
    [Serializable]
    internal sealed class NeighborhoodLayout
    {
        #region Serialized fields

        [SerializeField] private string _streetName = string.Empty;
        [SerializeField] private List<string> _versionNames = new List<string>();
        [SerializeField] private int _startVersion = 1;
        [SerializeField] private float _seatHeight = 0.12f;
        [SerializeField] private float _eyeHeight = 1.2f;
        [SerializeField] private NeighborhoodLayoutTable _table = new NeighborhoodLayoutTable();
        [SerializeField] private List<NeighborhoodLayoutElement> _elements = new List<NeighborhoodLayoutElement>();
        [SerializeField] private List<NeighborhoodLayoutLight> _lights = new List<NeighborhoodLayoutLight>();
        [SerializeField] private List<NeighborhoodLayoutLeash> _leashes = new List<NeighborhoodLayoutLeash>();
        [SerializeField] private List<NeighborhoodLayoutCharacter> _characters = new List<NeighborhoodLayoutCharacter>();
        [SerializeField] private List<NeighborhoodLayoutMaterial> _characterMaterials = new List<NeighborhoodLayoutMaterial>();
        [SerializeField] private string _scriptVersion = string.Empty;
        [SerializeField] private List<NeighborhoodLayoutVoiceLine> _voiceLines = new List<NeighborhoodLayoutVoiceLine>();
        [SerializeField] private List<NeighborhoodLayoutReaction> _reactions = new List<NeighborhoodLayoutReaction>();
        [SerializeField] private NeighborhoodLayoutDusk _dusk = new NeighborhoodLayoutDusk();

        #endregion

        #region Properties

        public string NeighborhoodName => _streetName;
        public IReadOnlyList<string> VersionNames => _versionNames;
        public int StartVersion => _startVersion;
        public float SeatHeight => _seatHeight;
        public float EyeHeight => _eyeHeight;
        public NeighborhoodLayoutTable Table => _table;
        public IReadOnlyList<NeighborhoodLayoutElement> Elements => _elements;
        public IReadOnlyList<NeighborhoodLayoutLight> Lights => _lights;
        public IReadOnlyList<NeighborhoodLayoutLeash> Leashes => _leashes;
        public IReadOnlyList<NeighborhoodLayoutCharacter> Characters => _characters;
        public IReadOnlyList<NeighborhoodLayoutMaterial> CharacterMaterials => _characterMaterials;
        public string ScriptVersion => _scriptVersion;
        public IReadOnlyList<NeighborhoodLayoutVoiceLine> VoiceLines => _voiceLines;
        public IReadOnlyList<NeighborhoodLayoutReaction> Reactions => _reactions;
        public NeighborhoodLayoutDusk Dusk => _dusk;

        #endregion
    }
}
