using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Neighborhood.Characters;
using DroneDDW.Neighborhood.Utilities;
using DroneDDW.Neighborhood.Versions;
using UnityEngine;

namespace DroneDDW.Neighborhood.Voice
{
    /// <summary>
    /// Plays the voice lines through the three characters, one at a time. By default the people react after
    /// every change the participant makes: when the street has settled, the reaction set for the new version
    /// and the current stage plays (Now, or FiveYearsOn once the experience moves on). A new change cuts off
    /// whatever is being said. Lines can also be played by ID, as a list, or as the introductions.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Voice Director")]
    public sealed class NeighborhoodVoiceDirector : MonoBehaviour
    {
        #region Events

        /// <summary>A line starts (its character is about to speak).</summary>
        public event Action<NeighborhoodVoiceLine> OnLineStart;

        /// <summary>A line ended or was cut off.</summary>
        public event Action<NeighborhoodVoiceLine> OnLineEnd;

        #endregion

        #region Serialized fields

        [SerializeField] private NeighborhoodVoiceLibrary _library;

        [SerializeField, Tooltip("Grandma, worker and student, in that order.")]
        private NeighborhoodCharacter[] _characters = Array.Empty<NeighborhoodCharacter>();

        [SerializeField] private NeighborhoodVersionController _controller;

        [SerializeField, Tooltip("Which reaction sets play after a change.")]
        private NeighborhoodVoiceStage _stage = NeighborhoodVoiceStage.Now;

        [SerializeField, Tooltip("Play the reaction set automatically when the street settles after a change.")]
        private bool _reactToChanges = true;

        [SerializeField, Min(0f), Tooltip("Seconds between the street settling and the first reaction.")]
        private float _reactionDelay = 0.5f;

        [SerializeField, Min(0f), Tooltip("Seconds of silence between two lines.")]
        private float _gap = 0.4f;

        #endregion

        #region Fields

        private readonly List<string> _buffer = new List<string>();
        private readonly NeighborhoodLifetime _lifetime = new NeighborhoodLifetime();
        private CancellationTokenSource _sequence;
        private int _sequenceId;
        private NeighborhoodVoiceLine _currentLine;
        private bool _isChangePending;
        private bool _isHeld;
        private bool _isReactionWaiting;

        #endregion

        #region Properties

        public NeighborhoodVoiceLibrary Library => _library;

        /// <summary>The line being spoken, or null.</summary>
        public NeighborhoodVoiceLine CurrentLine => _currentLine;
        public bool IsPlaying => _sequence != null;

        public NeighborhoodVoiceStage Stage
        {
            get => _stage;
            set => _stage = value;
        }

        public bool ReactToChanges
        {
            get => _reactToChanges;
            set => _reactToChanges = value;
        }

        #endregion

        #region Setup

        private void OnEnable()
        {
            if (_controller == null)
            {
                return;
            }

            _controller.OnVersionChange += HandleVersionChange;
            _controller.OnTransitionComplete += HandleTransitionComplete;
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnVersionChange -= HandleVersionChange;
                _controller.OnTransitionComplete -= HandleTransitionComplete;
            }

            Stop();
        }

        private void OnDestroy()
        {
            _lifetime.End();
        }

        #endregion

        #region Event handlers

        private void HandleVersionChange(NeighborhoodVersionChange change)
        {
            _isChangePending = !change.IsImmediate;
            _isReactionWaiting = false;
            if (_isChangePending)
            {
                Stop();
            }
        }

        private void HandleTransitionComplete(NeighborhoodVersion version)
        {
            if (!_isChangePending)
            {
                return;
            }

            _isChangePending = false;
            if (!_reactToChanges)
            {
                return;
            }

            if (_isHeld)
            {
                _isReactionWaiting = true;
                return;
            }

            StartSequence(ToArray(GetReactionIds(version, _stage)), _reactionDelay);
        }

        #endregion

        #region Public

        /// <summary>
        /// While the participant still moves the slider the street follows it, but the people wait: reactions to a
        /// version the street reaches meanwhile are kept until <see cref="ReleaseReactions"/>.
        /// </summary>
        public void HoldReactions()
        {
            _isHeld = true;
        }

        /// <summary>The slider was let go: the people react to where the street is (now, or once it gets there).</summary>
        public void ReleaseReactions()
        {
            _isHeld = false;
            if (!_isReactionWaiting || _controller == null || _controller.IsTransitioning)
            {
                return;
            }

            _isReactionWaiting = false;
            StartSequence(ToArray(GetReactionIds(_controller.CurrentVersion, _stage)), _reactionDelay);
        }

        /// <summary>Plays one line (cutting off anything playing).</summary>
        public void PlayLine(string lineId)
        {
            StartSequence(new[] { lineId }, 0f);
        }

        /// <summary>Plays lines in order (cutting off anything playing).</summary>
        public void PlaySequence(IReadOnlyList<string> lineIds)
        {
            StartSequence(ToArray(lineIds), 0f);
        }

        /// <summary>The three introductions (GRA_01, WOR_01, STU_01).</summary>
        public void PlayIntroductions()
        {
            if (_library == null)
            {
                return;
            }

            _library.GetLinesOfStage(NeighborhoodVoiceStage.Introduction, _buffer);
            StartSequence(_buffer.ToArray(), 0f);
        }

        /// <summary>The reaction set of a version at the current stage.</summary>
        public void PlayReactions(NeighborhoodVersion version)
        {
            StartSequence(ToArray(GetReactionIds(version, _stage)), 0f);
        }

        /// <summary>Cuts off the line being spoken and drops the rest of the sequence.</summary>
        public void Stop()
        {
            // Detach first: UniTask runs the cancelled sequence's continuation inside Cancel, and it must not touch it then.
            CancellationTokenSource sequence = _sequence;
            _sequence = null;
            _sequenceId++;
            if (sequence != null)
            {
                sequence.Cancel();
                sequence.Dispose();
            }

            foreach (NeighborhoodCharacter character in _characters)
            {
                if (character != null && character.IsSpeaking)
                {
                    character.StopSpeaking();
                }
            }
        }

        /// <summary>Speaks lines in order with a short gap between them; awaitable for scripted flows.</summary>
        public async UniTask PlaySequenceAsync(IReadOnlyList<string> lineIds, CancellationToken cancellationToken)
        {
            for (int i = 0; i < lineIds.Count; i++)
            {
                if (i > 0 && _gap > 0f)
                {
                    await NeighborhoodFrames.WaitSecondsAsync(_gap, cancellationToken);
                }

                await PlayLineAsync(lineIds[i], cancellationToken);
            }
        }

        public async UniTask PlayLineAsync(string lineId, CancellationToken cancellationToken)
        {
            if (_library == null || !_library.TryGetLine(lineId, out NeighborhoodVoiceLine line))
            {
                Debug.LogWarning($"[Neighborhood] No voice line with ID {lineId}.", this);
                return;
            }

            NeighborhoodCharacter character = GetCharacter(line.Character);
            if (character == null)
            {
                Debug.LogWarning($"[Neighborhood] {lineId}: no {line.Character} in the director's character list.", this);
                return;
            }

            if (line.Clip == null)
            {
                Debug.LogWarning($"[Neighborhood] {lineId}.wav is not linked yet; the face moves without sound.", this);
            }

            _currentLine = line;
            OnLineStart?.Invoke(line);
            try
            {
                await character.SpeakAsync(line.Clip, line.Emotion, line.Duration, cancellationToken);
            }
            finally
            {
                _currentLine = null;
                OnLineEnd?.Invoke(line);
            }
        }

        public IReadOnlyList<string> GetReactionIds(NeighborhoodVersion version, NeighborhoodVoiceStage stage)
        {
            return _library != null ? _library.GetReactions(version, stage) : Array.Empty<string>();
        }

        #endregion

        #region Internal

        internal void Configure(NeighborhoodVoiceLibrary library, NeighborhoodCharacter[] characters, NeighborhoodVersionController controller)
        {
            _library = library;
            _characters = characters ?? Array.Empty<NeighborhoodCharacter>();
            _controller = controller;
        }

        #endregion

        #region Private

        private NeighborhoodCharacter GetCharacter(NeighborhoodCharacterId id)
        {
            foreach (NeighborhoodCharacter character in _characters)
            {
                if (character != null && character.Id == id)
                {
                    return character;
                }
            }

            return null;
        }

        private void StartSequence(string[] lineIds, float delay)
        {
            Stop();
            if (lineIds.Length == 0)
            {
                return;
            }

            _sequence = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            RunSequenceAsync(lineIds, delay, ++_sequenceId, _sequence.Token).Forget();
        }

        private async UniTask RunSequenceAsync(string[] lineIds, float delay, int sequenceId, CancellationToken cancellationToken)
        {
            try
            {
                if (delay > 0f)
                {
                    await NeighborhoodFrames.WaitSecondsAsync(delay, cancellationToken);
                }

                await PlaySequenceAsync(lineIds, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Cut off by a new change, a new sequence or Stop (which releases the token source itself).
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                if (sequenceId == _sequenceId && _sequence != null)
                {
                    // Finished by itself: release its token source.
                    _sequence.Dispose();
                    _sequence = null;
                }
            }
        }

        private static string[] ToArray(IReadOnlyList<string> lineIds)
        {
            string[] copy = new string[lineIds.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = lineIds[i];
            }

            return copy;
        }

        #endregion
    }
}
