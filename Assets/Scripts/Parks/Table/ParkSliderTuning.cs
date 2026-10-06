using UnityEngine;

namespace DroneDDW.Parks.Table
{
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Parks/Park Slider Tuning")]
    public sealed class ParkSliderTuning : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField, Tooltip("Shared by every scene that uses it. Changes made in Play mode are kept.")]
        private ParkPushSettings _settings;

        [SerializeField, Tooltip("Shared by every scene that uses it. Changes made in Play mode are kept.")]
        private ParkBoardFollowSettings _followSettings;

        [SerializeField, Tooltip("The slider's Park Hand Spheres View, which draws the spheres and knob boxes.")]
        private Behaviour _view;

        [SerializeField, Tooltip("The board, whose follow state shows here in Play mode.")]
        private ParkBoardFollow _boardFollow;

        [SerializeField, Tooltip("The time slider above the level slider. Optional.")]
        private ParkLevelSlider _timeSlider;

        #endregion

        #region Properties

        internal ParkPushSettings Settings => _settings;
        internal ParkBoardFollowSettings FollowSettings => _followSettings;
        internal Behaviour View => _view;
        internal ParkBoardFollow BoardFollow => _boardFollow;
        internal ParkLevelSlider TimeSlider => _timeSlider;

        #endregion

        #region Internal

        internal void Configure(ParkPushSettings settings, ParkBoardFollowSettings followSettings, Behaviour view,
            ParkBoardFollow boardFollow)
        {
            Configure(settings, followSettings, view, boardFollow, null);
        }

        internal void Configure(ParkPushSettings settings, ParkBoardFollowSettings followSettings, Behaviour view,
            ParkBoardFollow boardFollow, ParkLevelSlider timeSlider)
        {
            _settings = settings;
            _followSettings = followSettings;
            _view = view;
            _boardFollow = boardFollow;
            _timeSlider = timeSlider;
        }

        #endregion
    }
}