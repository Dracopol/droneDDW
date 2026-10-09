using DroneDDW.Neighborhood.Audio;
using DroneDDW.Neighborhood.Table;
using DroneDDW.Neighborhood.Versions;
using DroneDDW.Neighborhood.Voice;
using UnityEngine;

namespace DroneDDW.Neighborhood.Experience
{
    /// <summary>
    /// Wires the pieces together through their events:
    /// slider dragged over a notch, the table miniature previews that version;
    /// slider released, the street moves to that version;
    /// street version changed (from the slider, keys or code), miniature, slider and ambience follow;
    /// someone speaks, the ambience dips so the line is easy to hear.
    /// The voice director listens to the controller itself (reactions after each change).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Experience")]
    public sealed class NeighborhoodExperience : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField] private NeighborhoodVersionController _controller;
        [SerializeField] private NeighborhoodSlider _slider;
        [SerializeField] private NeighborhoodModelPreview _preview;

        [SerializeField, Tooltip("Optional.")]
        private NeighborhoodAmbience _ambience;

        [SerializeField, Tooltip("Optional.")]
        private NeighborhoodVoiceDirector _voice;

        #endregion

        #region Properties

        public NeighborhoodVersionController Controller => _controller;
        public NeighborhoodVoiceDirector Voice => _voice;

        #endregion

        #region Setup

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.OnVersionChange += HandleVersionChange;
            }

            if (_slider != null)
            {
                _slider.OnDetentChange += HandleDetentChange;
                _slider.OnVersionCommit += HandleVersionCommit;
            }

            if (_voice != null)
            {
                _voice.OnLineStart += HandleLineStart;
                _voice.OnLineEnd += HandleLineEnd;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnVersionChange -= HandleVersionChange;
            }

            if (_slider != null)
            {
                _slider.OnDetentChange -= HandleDetentChange;
                _slider.OnVersionCommit -= HandleVersionCommit;
            }

            if (_voice != null)
            {
                _voice.OnLineStart -= HandleLineStart;
                _voice.OnLineEnd -= HandleLineEnd;
            }
        }

        private void Start()
        {
            if (_controller == null)
            {
                Debug.LogError("[Neighborhood] NeighborhoodExperience has no version controller.", this);
                return;
            }

            // The controller applied its start version in Awake, before this object subscribed.
            NeighborhoodVersion version = _controller.CurrentVersion;
            if (_preview != null)
            {
                _preview.ShowVersion(version);
            }

            if (_slider != null)
            {
                _slider.SetDetent((int)version, false);
            }

            if (_ambience != null)
            {
                _ambience.SetVersionImmediate(version);
            }
        }

        #endregion

        #region Event handlers

        private void HandleDetentChange(int detent)
        {
            if (_preview != null)
            {
                _preview.ShowVersion(NeighborhoodVersions.Clamp(detent));
            }
        }

        private void HandleVersionCommit(int detent)
        {
            if (_controller != null)
            {
                _controller.RequestVersion(NeighborhoodVersions.Clamp(detent));
            }
        }

        private void HandleLineStart(NeighborhoodVoiceLine line)
        {
            if (_ambience != null)
            {
                _ambience.SetDucked(true);
            }
        }

        private void HandleLineEnd(NeighborhoodVoiceLine line)
        {
            if (_ambience != null)
            {
                _ambience.SetDucked(false);
            }
        }

        private void HandleVersionChange(NeighborhoodVersionChange change)
        {
            if (_preview != null)
            {
                _preview.ShowVersion(change.Next);
            }

            if (_slider != null && !_slider.IsDragging && _slider.Detent != (int)change.Next)
            {
                _slider.SetDetent((int)change.Next, !change.IsImmediate);
            }

            if (_ambience == null)
            {
                return;
            }

            if (change.IsImmediate)
            {
                _ambience.SetVersionImmediate(change.Next);
            }
            else
            {
                _ambience.FadeToVersion(change.Next);
            }
        }

        #endregion

        #region Internal

        internal void Configure(NeighborhoodVersionController controller, NeighborhoodSlider slider, NeighborhoodModelPreview preview,
            NeighborhoodAmbience ambience, NeighborhoodVoiceDirector voice)
        {
            _controller = controller;
            _slider = slider;
            _preview = preview;
            _ambience = ambience;
            _voice = voice;
        }

        #endregion
    }
}
