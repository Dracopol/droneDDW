using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Neighborhood.Utilities;
using DroneDDW.Neighborhood.Versions;
using UnityEngine;

namespace DroneDDW.Neighborhood.Characters
{
    /// <summary>
    /// One of the three people. Follows the street: when the wave reaches them, they cross-fade to the pose of
    /// the new version (a looping, breathing clip) and put on that version's face. Speaks voice lines through
    /// the audio source at the head: the face shows the line's emotion and the mouth opens with the sound
    /// (or moves at a steady rate when a line has no clip yet, or when a spatializer hides the output data).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Character")]
    public sealed class NeighborhoodCharacter : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField] private NeighborhoodCharacterId _id;
        [SerializeField] private Animator _animator;
        [SerializeField] private NeighborhoodFace _face;

        [SerializeField, Tooltip("Spatial audio source at the head.")]
        private AudioSource _voice;

        [SerializeField, Tooltip("The street; the character changes pose when its wave passes.")]
        private NeighborhoodVersionController _controller;

        [SerializeField, Tooltip("Pose and resting face per version.")]
        private NeighborhoodCharacterPose[] _poses = Array.Empty<NeighborhoodCharacterPose>();

        [SerializeField, Min(0f), Tooltip("Seconds to blend into a new pose.")]
        private float _crossFadeDuration = 0.6f;

        [Header("Talking")]
        [SerializeField, Range(0.05f, 0.9f), Tooltip("The mouth opens above this share of the line's recent peak loudness, "
            + "so it works the same near and far (3D attenuation) and for quiet and loud recordings.")]
        private float _mouthOpenShare = 0.35f;

        [SerializeField, Min(0.02f), Tooltip("Shortest time the mouth stays open or closed, so it does not flicker.")]
        private float _mouthMinHold = 0.07f;

        [SerializeField, Min(1f), Tooltip("Mouth movements per second for a line without a clip.")]
        private float _silentTalkRate = 6f;

        #endregion

        #region Fields

        private const int SampleCount = 256;
        private const float SilentSourceSeconds = 0.4f;
        private const float SilentLoudness = 0.00001f;
        private const float PeakMemorySeconds = 1.2f;

        private readonly float[] _samples = new float[SampleCount];
        private readonly NeighborhoodLifetime _lifetime = new NeighborhoodLifetime();
        private NeighborhoodVersion _version;
        private NeighborhoodEmotion _restingEmotion;
        private int _poseRequestId;
        private int _speechId;
        private bool _isSpeaking;
        private bool _hasClip;
        private float _mouthHoldUntil;
        private float _speechStartTime;
        private float _peakLoudness;
        private float _maxLoudness;

        #endregion

        #region Properties

        public NeighborhoodCharacterId Id => _id;
        public bool IsSpeaking => _isSpeaking;
        public NeighborhoodVersion Version => _version;
        public NeighborhoodFace Face => _face;
        public Transform VoiceAnchor => _voice != null ? _voice.transform : transform;

        #endregion

        #region Setup

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.OnVersionChange += HandleVersionChange;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnVersionChange -= HandleVersionChange;
            }

            _poseRequestId++;
            if (_isSpeaking)
            {
                StopSpeaking();
            }
        }

        private void Start()
        {
            // The controller applied its start version in Awake, before this component subscribed.
            if (_controller != null && _controller.HasVersion)
            {
                ShowVersion(_controller.CurrentVersion);
            }
        }

        private void OnDestroy()
        {
            _lifetime.End();
        }

        #endregion

        #region Update

        private void Update()
        {
            if (!_isSpeaking || _face == null)
            {
                return;
            }

            float now = Time.time;
            bool isOpen = IsMouthOpenAt(now);
            if (now < _mouthHoldUntil || isOpen == _face.IsMouthOpen)
            {
                return;
            }

            _face.SetMouthOpen(isOpen);
            _mouthHoldUntil = now + _mouthMinHold;
        }

        #endregion

        #region Event handlers

        private void HandleVersionChange(NeighborhoodVersionChange change)
        {
            int requestId = ++_poseRequestId;
            if (change.IsImmediate)
            {
                ShowVersion(change.Next);
                return;
            }

            ChangePoseAsync(change.Next, requestId, _lifetime.Token).Forget();
        }

        #endregion

        #region Public

        /// <summary>
        /// Speaks a line and returns when it ends, is replaced by another line, or is stopped.
        /// Without a clip the mouth moves for fallbackSeconds, so the flow can be tested before the WAVs are in.
        /// </summary>
        public async UniTask SpeakAsync(AudioClip clip, NeighborhoodEmotion emotion, float fallbackSeconds, CancellationToken cancellationToken)
        {
            int speechId = ++_speechId;
            _isSpeaking = true;
            _hasClip = clip != null && _voice != null;
            _mouthHoldUntil = 0f;
            _speechStartTime = Time.time;
            _peakLoudness = 0f;
            _maxLoudness = 0f;
            if (_face != null)
            {
                _face.SetEmotion(emotion);
            }

            if (_hasClip)
            {
                _voice.Stop();
                _voice.clip = clip;
                _voice.Play();
            }

            float endTime = Time.time + (_hasClip ? clip.length : Mathf.Max(0.2f, fallbackSeconds));
            try
            {
                while (Time.time < endTime)
                {
                    await NeighborhoodFrames.NextFrameAsync(cancellationToken);
                    if (speechId != _speechId)
                    {
                        return;
                    }
                }
            }
            finally
            {
                if (speechId == _speechId)
                {
                    EndSpeech();
                }
            }
        }

        public void StopSpeaking()
        {
            _speechId++;
            EndSpeech();
        }

        /// <summary>Jumps to a version's pose and face at once (no blend).</summary>
        public void ShowVersion(NeighborhoodVersion version)
        {
            ApplyVersion(version, true);
        }

        #endregion

        #region Internal

        internal void Configure(NeighborhoodCharacterId id, Animator animator, NeighborhoodFace face, AudioSource voice,
            NeighborhoodVersionController controller, NeighborhoodCharacterPose[] poses)
        {
            _id = id;
            _animator = animator;
            _face = face;
            _voice = voice;
            _controller = controller;
            _poses = poses ?? Array.Empty<NeighborhoodCharacterPose>();
        }

        internal NeighborhoodCharacterPose FindPose(NeighborhoodVersion version)
        {
            foreach (NeighborhoodCharacterPose pose in _poses)
            {
                if (pose != null && pose.Version == version)
                {
                    return pose;
                }
            }

            return null;
        }

        #endregion

        #region Private

        private async UniTask ChangePoseAsync(NeighborhoodVersion version, int requestId, CancellationToken cancellationToken)
        {
            try
            {
                await NeighborhoodFrames.WaitSecondsAsync(_controller.GetWaveDelay(transform.position, true), cancellationToken);
                if (requestId == _poseRequestId)
                {
                    ApplyVersion(version, false);
                }
            }
            catch (OperationCanceledException)
            {
                // Destroyed before the wave arrived.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void ApplyVersion(NeighborhoodVersion version, bool isImmediate)
        {
            _version = version;
            NeighborhoodCharacterPose pose = FindPose(version);
            if (pose == null)
            {
                return;
            }

            _restingEmotion = pose.Emotion;
            if (!_isSpeaking && _face != null)
            {
                _face.SetEmotion(_restingEmotion);
            }

            if (_animator == null || string.IsNullOrEmpty(pose.State))
            {
                return;
            }

            int stateHash = Animator.StringToHash(pose.State);
            if (isImmediate || _crossFadeDuration <= 0f)
            {
                _animator.Play(stateHash, 0, 0f);
            }
            else
            {
                _animator.CrossFadeInFixedTime(stateHash, _crossFadeDuration);
            }
        }

        /// <summary>
        /// Open while the line is loud compared with its recent peak. Moves at a steady rate when there is no clip,
        /// or when the clip's output stays silent (some spatializer plugins hide a source's output data).
        /// </summary>
        private bool IsMouthOpenAt(float now)
        {
            if (_hasClip)
            {
                float loudness = ReadLoudness();
                _maxLoudness = Mathf.Max(_maxLoudness, loudness);
                _peakLoudness = Mathf.Max(loudness, _peakLoudness * Mathf.Exp(-Time.deltaTime / PeakMemorySeconds));
                bool isSilentSource = now - _speechStartTime > SilentSourceSeconds && _maxLoudness < SilentLoudness;
                if (!isSilentSource)
                {
                    return loudness > SilentLoudness && loudness > _peakLoudness * _mouthOpenShare;
                }
            }

            return Mathf.Repeat(now * _silentTalkRate, 1f) < 0.5f;
        }

        private float ReadLoudness()
        {
            _voice.GetOutputData(_samples, 0);
            float sum = 0f;
            for (int i = 0; i < SampleCount; i++)
            {
                sum += _samples[i] * _samples[i];
            }

            return Mathf.Sqrt(sum / SampleCount);
        }

        private void EndSpeech()
        {
            _isSpeaking = false;
            if (_hasClip && _voice != null && _voice.isPlaying)
            {
                _voice.Stop();
            }

            _hasClip = false;
            if (_face == null)
            {
                return;
            }

            _face.SetMouthOpen(false);
            _face.SetEmotion(_restingEmotion);
        }

        #endregion
    }
}
