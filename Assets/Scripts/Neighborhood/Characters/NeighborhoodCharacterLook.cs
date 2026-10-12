using UnityEngine;

namespace DroneDDW.Neighborhood.Characters
{
    /// <summary>
    /// While a person speaks, they turn toward the participant, so it is clear from the seat who is talking. The upper
    /// spine, neck and head share the turn (20 / 30 / 50% by default) on top of whatever the pose clip does, within
    /// limits (70° to the side, 20° up or down); the turn eases in when a line starts and back out after it ends.
    /// Where the face points is measured from the head bone every frame, so a pose that looks down at the ducks turns
    /// up toward the viewer too. Turns are about world up and the face's horizontal right, so the bones' own axes
    /// don't matter.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Character Look")]
    public sealed class NeighborhoodCharacterLook : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField] private NeighborhoodCharacter _character;
        [SerializeField] private Transform _spine;
        [SerializeField] private Transform _neck;
        [SerializeField] private Transform _head;

        [SerializeField, Tooltip("Where the face points, in the head bone's own axes (measured from the bind pose when the scene is built).")]
        private Vector3 _faceForward = Vector3.forward;

        [SerializeField, Tooltip("What to look at. Empty: the main camera.")]
        private Transform _target;

        [SerializeField, Range(0f, 90f), Tooltip("Furthest turn to either side, in degrees.")]
        private float _maxYawDegrees = 70f;

        [SerializeField, Range(0f, 45f), Tooltip("Furthest turn up or down, in degrees.")]
        private float _maxPitchDegrees = 20f;

        [SerializeField, Range(0f, 1f), Tooltip("Share of the turn the upper spine takes.")]
        private float _spineShare = 0.2f;

        [SerializeField, Range(0f, 1f), Tooltip("Share of the turn the neck takes; the head takes the rest.")]
        private float _neckShare = 0.3f;

        [SerializeField, Min(0.01f), Tooltip("Seconds to turn toward the viewer when a line starts.")]
        private float _turnInSeconds = 0.35f;

        [SerializeField, Min(0.01f), Tooltip("Seconds to turn back after the line ends.")]
        private float _turnBackSeconds = 0.6f;

        #endregion

        #region Fields

        private const float MinimumFlatLength = 0.05f;

        private bool _isSpeaking;
        private float _weight;

        #endregion

        #region Properties

        /// <summary>0 = their own pose, 1 = fully turned toward the viewer.</summary>
        public float Weight => _weight;

        internal Transform Head => _head;
        internal Vector3 FaceForward => _faceForward;
        internal float MaxYawDegrees => _maxYawDegrees;
        internal float MaxPitchDegrees => _maxPitchDegrees;

        #endregion

        #region Setup

        private void OnEnable()
        {
            if (_character != null)
            {
                _character.OnSpeechStart += HandleSpeechStart;
                _character.OnSpeechEnd += HandleSpeechEnd;
                _isSpeaking = _character.IsSpeaking;
            }
        }

        private void OnDisable()
        {
            if (_character != null)
            {
                _character.OnSpeechStart -= HandleSpeechStart;
                _character.OnSpeechEnd -= HandleSpeechEnd;
            }

            _isSpeaking = false;
            _weight = 0f;
        }

        #endregion

        #region Update

        // After the animator has posed the bones this frame
        private void LateUpdate()
        {
            float goal = _isSpeaking ? 1f : 0f;
            float seconds = _isSpeaking ? _turnInSeconds : _turnBackSeconds;
            _weight = Mathf.MoveTowards(_weight, goal, Time.deltaTime / seconds);
            Transform target = ResolveTarget();
            if (_weight <= 0f || _head == null || target == null)
            {
                return;
            }

            Vector3 face = _head.rotation * _faceForward;
            if (!TryComputeTurn(face, target.position - _head.position, _maxYawDegrees, _maxPitchDegrees,
                out float yawDegrees, out float pitchDegrees))
            {
                return;
            }

            float eased = Mathf.SmoothStep(0f, 1f, _weight);
            Vector3 flatFace = Flatten(face);
            Vector3 right = Vector3.Cross(Vector3.up, Quaternion.AngleAxis(yawDegrees, Vector3.up) * flatFace).normalized;
            float headShare = Mathf.Max(0f, 1f - _spineShare - _neckShare);
            Turn(_spine, yawDegrees * eased * _spineShare, pitchDegrees * eased * _spineShare, right);
            Turn(_neck, yawDegrees * eased * _neckShare, pitchDegrees * eased * _neckShare, right);
            Turn(_head, yawDegrees * eased * headShare, pitchDegrees * eased * headShare, right);
        }

        #endregion

        #region Event handlers

        private void HandleSpeechStart()
        {
            _isSpeaking = true;
        }

        private void HandleSpeechEnd()
        {
            _isSpeaking = false;
        }

        #endregion

        #region Public

        /// <summary>Looks at this from now on (the experience passes its head camera). Null: the main camera.</summary>
        public void SetTarget(Transform target)
        {
            _target = target;
        }

        #endregion

        #region Internal

        internal void Configure(NeighborhoodCharacter character, Transform spine, Transform neck, Transform head, Vector3 faceForward)
        {
            _character = character;
            _spine = spine;
            _neck = neck;
            _head = head;
            _faceForward = faceForward.sqrMagnitude > 0f ? faceForward.normalized : Vector3.forward;
        }

        /// <summary>
        /// The turn from where the face points to the target: yaw about world up (positive = to the right) and pitch
        /// about the face's horizontal right (positive = down), each clamped to its limit. False when the face or the
        /// target points straight up or down (no sideways direction to turn in).
        /// </summary>
        internal static bool TryComputeTurn(Vector3 face, Vector3 toTarget, float maxYawDegrees, float maxPitchDegrees,
            out float yawDegrees, out float pitchDegrees)
        {
            yawDegrees = 0f;
            pitchDegrees = 0f;
            Vector3 flatFace = Flatten(face);
            Vector3 flatTarget = Flatten(toTarget);
            if (flatFace.magnitude < MinimumFlatLength * face.magnitude || flatTarget.magnitude < MinimumFlatLength * toTarget.magnitude)
            {
                return false;
            }

            yawDegrees = Mathf.Clamp(Vector3.SignedAngle(flatFace, flatTarget, Vector3.up), -maxYawDegrees, maxYawDegrees);
            pitchDegrees = Mathf.Clamp(Elevation(face) - Elevation(toTarget), -maxPitchDegrees, maxPitchDegrees);
            return true;
        }

        #endregion

        #region Private

        private Transform ResolveTarget()
        {
            if (_target != null)
            {
                return _target;
            }

            Camera main = Camera.main;
            return main != null ? main.transform : null;
        }

        private static void Turn(Transform bone, float yawDegrees, float pitchDegrees, Vector3 right)
        {
            if (bone == null)
            {
                return;
            }

            bone.rotation = Quaternion.AngleAxis(yawDegrees, Vector3.up) * Quaternion.AngleAxis(pitchDegrees, right) * bone.rotation;
        }

        private static Vector3 Flatten(Vector3 direction)
        {
            return new Vector3(direction.x, 0f, direction.z);
        }

        private static float Elevation(Vector3 direction)
        {
            return Mathf.Atan2(direction.y, Flatten(direction).magnitude) * Mathf.Rad2Deg;
        }

        #endregion
    }
}
