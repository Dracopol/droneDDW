using UnityEngine;

namespace DroneDDW.Parks.Table
{

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-20)] 
    [AddComponentMenu("DroneDDW/Parks/Park Board Follow")]
    public sealed class ParkBoardFollow : MonoBehaviour
    {
        #region Entities

        internal enum FollowState
        {
            NotPlaced,

            Waiting,

            Following,

            Frozen,
        }

        #endregion

        #region Serialized fields

        [SerializeField, Tooltip("The XR camera. Falls back to Camera.main when left empty.")]
        private Transform _head;

        [SerializeField, Tooltip("The board's level slider. The board holds still while it is pushed or grabbed.")]
        private ParkLevelSlider _slider;

        [SerializeField, Tooltip("The time slider above the level slider. The board also holds still while it is "
            + "pushed or grabbed. Optional.")]
        private ParkLevelSlider _timeSlider;

        [SerializeField, Tooltip("The shared follow settings: position, dead zone and speeds. Uses the defaults when "
            + "empty.")]
        private ParkBoardFollowSettings _settings;

        #endregion

        #region Fields

        private const float SettleAngleDegrees = 0.5f;
        private const float SettleDistanceMeters = 0.005f;

        private const float MinFlatForwardLength = 0.2f;

        private Transform _transform;
        private FollowState _state;
        private bool _isMoving;
        private Vector3 _anchorPosition;
        private Vector3 _anchorVelocity;
        private float _yawDegrees;
        private float _yawVelocity;
        private float _turnOffsetDegrees;
        private float _moveOffsetMeters;

        #endregion

        #region Properties

        internal FollowState State => _state;

        internal float TurnOffsetDegrees => _turnOffsetDegrees;

        internal float MoveOffsetMeters => _moveOffsetMeters;

        internal ParkBoardFollowSettings Settings => ParkBoardFollowSettings.OrDefaults(_settings);

        #endregion

        #region Setup

        private void Awake()
        {
            _transform = transform;
        }

        #endregion

        #region Update

        private void LateUpdate()
        {
            Transform head = ResolveHead();

            if (head == null || head.localPosition == Vector3.zero)
            {
                return;
            }

            ParkBoardFollowSettings settings = Settings;
            float headYawDegrees = GetHeadYawDegrees(head);
            if (_state == FollowState.NotPlaced)
            {
                _anchorPosition = head.position;
                _yawDegrees = headYawDegrees;
                _state = FollowState.Waiting;
            }
            else
            {
                Follow(settings, head.position, headYawDegrees);
            }

            Quaternion rotation = Quaternion.Euler(0f, _yawDegrees, 0f);
            _transform.SetPositionAndRotation(_anchorPosition + rotation * settings.OffsetMeters, rotation);
        }

        #endregion

        #region Internal

        internal void Configure(Transform head, ParkLevelSlider slider, ParkBoardFollowSettings settings)
        {
            Configure(head, slider, null, settings);
        }

        internal void Configure(Transform head, ParkLevelSlider slider, ParkLevelSlider timeSlider,
            ParkBoardFollowSettings settings)
        {
            _head = head;
            _slider = slider;
            _timeSlider = timeSlider;
            _settings = settings;
        }

        #endregion

        #region Private

 
        private void Follow(ParkBoardFollowSettings settings, Vector3 headPosition, float headYawDegrees)
        {
            _turnOffsetDegrees = Mathf.Abs(Mathf.DeltaAngle(_yawDegrees, headYawDegrees));
            _moveOffsetMeters = Vector3.Distance(_anchorPosition, headPosition);
            if (IsAnySliderInUse())
            {
                _state = FollowState.Frozen;
                StopMoving();
                return;
            }

            if (!_isMoving)
            {
                if (_turnOffsetDegrees < settings.StartAngleDegrees && _moveOffsetMeters < settings.StartDistanceMeters)
                {
                    _state = FollowState.Waiting;
                    return;
                }

                _isMoving = true;
            }

            _state = FollowState.Following;
            _yawDegrees = Mathf.SmoothDampAngle(_yawDegrees, headYawDegrees, ref _yawVelocity, settings.TurnTimeSeconds);
            _anchorPosition = Vector3.SmoothDamp(_anchorPosition, headPosition, ref _anchorVelocity,
                settings.MoveTimeSeconds);
            if (_turnOffsetDegrees < SettleAngleDegrees && _moveOffsetMeters < SettleDistanceMeters)
            {
                StopMoving();
            }
        }

        private float GetHeadYawDegrees(Transform head)
        {
            Vector3 flatForward = head.forward;
            flatForward.y = 0f;
            if (flatForward.magnitude < MinFlatForwardLength)
            {
                return _yawDegrees;
            }

            return Quaternion.LookRotation(flatForward).eulerAngles.y;
        }

        private bool IsAnySliderInUse()
        {
            return IsSliderInUse(_slider) || IsSliderInUse(_timeSlider);
        }


        private static bool IsSliderInUse(ParkLevelSlider slider)
        {
            return slider != null && (slider.IsDragging || slider.Contact == ParkSliderContact.Pushed
                || slider.Contact == ParkSliderContact.Grabbed);
        }

        private void StopMoving()
        {
            _isMoving = false;
            _yawVelocity = 0f;
            _anchorVelocity = Vector3.zero;
        }

        private Transform ResolveHead()
        {
            if (_head != null)
            {
                return _head;
            }

            Camera main = Camera.main;
            return main != null ? main.transform : null;
        }

        #endregion
    }
}