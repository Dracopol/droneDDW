using System;
using DroneDDW.Neighborhood.Characters;
using UnityEngine;

namespace DroneDDW.Neighborhood.Voice
{
    /// <summary>One recorded line from the voice script. The ID is also the WAV name (GRA_01.wav).</summary>
    [Serializable]
    public sealed class NeighborhoodVoiceLine
    {
        #region Serialized fields

        [SerializeField] private string _id = string.Empty;
        [SerializeField] private NeighborhoodCharacterId _character;
        [SerializeField] private NeighborhoodEmotion _emotion;
        [SerializeField] private NeighborhoodVoiceSide _side;
        [SerializeField] private NeighborhoodVoiceStage _stage;

        [SerializeField, Tooltip("The change in the street that triggers the line (the When column).")]
        private string _trigger = string.Empty;

        [SerializeField, TextArea(1, 3)] private string _text = string.Empty;
        [SerializeField] private AudioClip _clip;

        #endregion

        #region Fields

        private const float SecondsPerWord = 0.36f;
        private const float PauseSeconds = 0.5f;

        #endregion

        #region Properties

        public string Id => _id;
        public NeighborhoodCharacterId Character => _character;
        public NeighborhoodEmotion Emotion => _emotion;
        public NeighborhoodVoiceSide Side => _side;
        public NeighborhoodVoiceStage Stage => _stage;
        public string Trigger => _trigger;
        public string Text => _text;
        public AudioClip Clip => _clip;

        /// <summary>The clip length, or a reading-speed estimate while the WAV is missing.</summary>
        public float Duration => _clip != null ? _clip.length : EstimateSeconds(_text);

        #endregion

        #region Setup

        public NeighborhoodVoiceLine()
        {
        }

        public NeighborhoodVoiceLine(string id, NeighborhoodCharacterId character, NeighborhoodEmotion emotion, NeighborhoodVoiceSide side,
            NeighborhoodVoiceStage stage, string trigger, string text, AudioClip clip)
        {
            _id = id;
            _character = character;
            _emotion = emotion;
            _side = side;
            _stage = stage;
            _trigger = trigger;
            _text = text;
            _clip = clip;
        }

        #endregion

        #region Internal

        internal void SetClip(AudioClip clip)
        {
            _clip = clip;
        }

        #endregion

        #region Private

        private static float EstimateSeconds(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 1f;
            }

            int words = 1;
            foreach (char letter in text)
            {
                if (letter == ' ')
                {
                    words++;
                }
            }

            return words * SecondsPerWord + PauseSeconds;
        }

        #endregion
    }
}
