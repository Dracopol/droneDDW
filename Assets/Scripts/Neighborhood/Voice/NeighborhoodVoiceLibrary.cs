using System;
using System.Collections.Generic;
using DroneDDW.Neighborhood.Versions;
using UnityEngine;

namespace DroneDDW.Neighborhood.Voice
{
    /// <summary>
    /// The lines of the voice script with their clips, and which lines react to which version.
    /// The scene builder fills it from the layout file and links every WAV whose name is a line ID.
    /// Edit the reaction sets here to change who says what after a change.
    /// </summary>
    [CreateAssetMenu(menuName = "DroneDDW/Neighborhood Voice Library", fileName = "Neighborhood_VoiceLibrary")]
    public sealed class NeighborhoodVoiceLibrary : ScriptableObject
    {
        #region Serialized fields

        [SerializeField, Tooltip("Which version of the voice script the lines come from.")]
        private string _scriptVersion = string.Empty;

        [SerializeField] private List<NeighborhoodVoiceLine> _lines = new List<NeighborhoodVoiceLine>();
        [SerializeField] private List<NeighborhoodReactionSet> _reactions = new List<NeighborhoodReactionSet>();

        #endregion

        #region Properties

        public string ScriptVersion => _scriptVersion;
        public IReadOnlyList<NeighborhoodVoiceLine> Lines => _lines;
        public IReadOnlyList<NeighborhoodReactionSet> Reactions => _reactions;

        #endregion

        #region Public

        public bool TryGetLine(string id, out NeighborhoodVoiceLine line)
        {
            foreach (NeighborhoodVoiceLine candidate in _lines)
            {
                if (candidate != null && string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    line = candidate;
                    return true;
                }
            }

            line = null;
            return false;
        }

        /// <summary>The line IDs that react to a version at a stage, in speaking order (empty when none).</summary>
        public IReadOnlyList<string> GetReactions(NeighborhoodVersion version, NeighborhoodVoiceStage stage)
        {
            foreach (NeighborhoodReactionSet set in _reactions)
            {
                if (set != null && set.Version == version && set.Stage == stage)
                {
                    return set.LineIds;
                }
            }

            return Array.Empty<string>();
        }

        /// <summary>Every line of a stage in script order (the introductions, for example).</summary>
        public void GetLinesOfStage(NeighborhoodVoiceStage stage, List<string> lineIds)
        {
            lineIds.Clear();
            foreach (NeighborhoodVoiceLine line in _lines)
            {
                if (line != null && line.Stage == stage)
                {
                    lineIds.Add(line.Id);
                }
            }
        }

        #endregion

        #region Internal

        internal void Configure(string scriptVersion, List<NeighborhoodVoiceLine> lines, List<NeighborhoodReactionSet> reactions)
        {
            _scriptVersion = scriptVersion ?? string.Empty;
            _lines = lines ?? new List<NeighborhoodVoiceLine>();
            _reactions = reactions ?? new List<NeighborhoodReactionSet>();
        }

        #endregion
    }
}
