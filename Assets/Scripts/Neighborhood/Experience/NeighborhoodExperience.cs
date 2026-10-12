using System;
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
    /// someone speaks, the ambience dips so the line is easy to hear;
    /// the lock-in button locks the choice in: the slider stays put and OnLockIn tells the experience, which records it.
    /// The voice director listens to the controller itself (reactions after each change).
    /// When the experience loads this scene on top of its own, it attaches its rig (AttachRig) and decides when the
    /// slider and the button can be used.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Experience")]
    public sealed class NeighborhoodExperience : MonoBehaviour
    {
        #region Events

        /// <summary>The participant locked in their choice: the version on the slider then.</summary>
        public event Action<NeighborhoodVersion> OnLockIn;

        #endregion

        #region Serialized fields

        [SerializeField] private NeighborhoodVersionController _controller;
        [SerializeField] private NeighborhoodSlider _slider;
        [SerializeField] private NeighborhoodModelPreview _preview;

        [SerializeField, Tooltip("Optional.")]
        private NeighborhoodAmbience _ambience;

        [SerializeField, Tooltip("Optional.")]
        private NeighborhoodVoiceDirector _voice;

        [SerializeField, Tooltip("The table's lock-in button. Optional.")]
        private NeighborhoodLockButton _lockButton;

        [SerializeField, Tooltip("The hands the slider and the button read. Optional.")]
        private NeighborhoodHandBones _handBones;

#if NEIGHBORHOOD_XRI
        [SerializeField, Tooltip("Moves the slider with the hands. Optional.")]
        private NeighborhoodSliderHandDriver _sliderDriver;
#endif

        #endregion

        #region Properties

        public NeighborhoodVersionController Controller => _controller;
        public NeighborhoodVoiceDirector Voice => _voice;

        /// <summary>The version the street shows (or is turning into).</summary>
        public NeighborhoodVersion CurrentVersion => _controller.CurrentVersion;

        public bool IsLocked => _lockButton != null && _lockButton.IsLocked;

        /// <summary>The version locked in; only meaningful once <see cref="IsLocked"/>.</summary>
        public NeighborhoodVersion LockedVersion { get; private set; }

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

            if (_lockButton != null)
            {
                _lockButton.OnLockIn += HandleLockIn;
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

            if (_lockButton != null)
            {
                _lockButton.OnLockIn -= HandleLockIn;
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

        // The slider stays where it was locked in
        private void HandleLockIn(int detent)
        {
            NeighborhoodVersion version = NeighborhoodVersions.Clamp(detent);
            LockedVersion = version;
            SetSliderEnabled(false);
            Debug.Log($"[Neighborhood] Locked in {version}.", this);
            OnLockIn?.Invoke(version);
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

        #region Public

        /// <summary>
        /// Uses another rig's head and hands, e.g. the experience's once it has loaded this scene on top of its own
        /// (the scene's own rig copy stays off then).
        /// </summary>
        public void AttachRig(Transform head, Transform trackingSpace)
        {
            if (_preview != null)
            {
                _preview.SetHead(head);
            }

            if (_handBones != null)
            {
                _handBones.SetTrackingSpace(trackingSpace);
            }
        }

        /// <summary>Whether the hands can move the slider (off once locked in, and while the session is paused).</summary>
        public void SetSliderEnabled(bool isEnabled)
        {
#if NEIGHBORHOOD_XRI
            if (_sliderDriver != null)
            {
                _sliderDriver.IsInputEnabled = isEnabled && !IsLocked;
            }
#endif
        }

        /// <summary>Whether the lock-in button can be used.</summary>
        public void SetLockEnabled(bool isEnabled)
        {
            if (_lockButton != null)
            {
                _lockButton.IsAvailable = isEnabled;
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

        internal void ConfigureTable(NeighborhoodLockButton lockButton, NeighborhoodHandBones handBones,
            Component sliderDriver)
        {
            _lockButton = lockButton;
            _handBones = handBones;
#if NEIGHBORHOOD_XRI
            _sliderDriver = sliderDriver as NeighborhoodSliderHandDriver;
#endif
        }

        #endregion
    }
}
