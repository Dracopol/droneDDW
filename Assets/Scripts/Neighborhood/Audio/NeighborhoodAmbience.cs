using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Neighborhood.Utilities;
using DroneDDW.Neighborhood.Versions;
using UnityEngine;

namespace DroneDDW.Neighborhood.Audio
{
    /// <summary>
    /// Two looping ambience beds, nature and efficiency, blended with an equal-power crossfade:
    /// nature plays only the nature loop, efficiency only the efficiency loop, balanced both at -3 dB.
    /// Assign any looping clips to the two sources. While someone speaks, both dip to the duck level.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Ambience")]
    public sealed class NeighborhoodAmbience : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField, Tooltip("Loop for the nature side (birds, wind, water, ducks).")]
        private AudioSource _natureSource;

        [SerializeField, Tooltip("Loop for the efficiency side (hum, drones, traffic).")]
        private AudioSource _efficiencySource;

        [SerializeField, Range(0f, 1f)] private float _volume = 0.7f;

        [SerializeField, Min(0f), Tooltip("Seconds for a crossfade from nature to efficiency; one step takes half.")]
        private float _fadeDuration = 3f;

        [SerializeField, Range(0f, 1f), Tooltip("Share of the volume left while a voice line plays.")]
        private float _duckLevel = 0.45f;

        [SerializeField, Min(0.01f), Tooltip("Seconds to dip or come back.")]
        private float _duckDuration = 0.3f;

        #endregion

        #region Fields

        private readonly NeighborhoodLifetime _lifetime = new NeighborhoodLifetime();
        private float _blend = 0.5f;
        private float _duck = 1f;
        private float _duckTarget = 1f;
        private int _fadeId;

        #endregion

        #region Properties

        /// <summary>0 = only nature, 1 = only efficiency.</summary>
        public float Blend => _blend;

        #endregion

        #region Setup

        private void Start()
        {
            ApplyVolumes();
            PlayIfReady(_natureSource);
            PlayIfReady(_efficiencySource);
        }

        private void OnDestroy()
        {
            _lifetime.End();
        }

        #endregion

        #region Update

        private void Update()
        {
            if (Mathf.Approximately(_duck, _duckTarget))
            {
                return;
            }

            _duck = Mathf.MoveTowards(_duck, _duckTarget, Time.deltaTime * (1f - _duckLevel) / _duckDuration);
            ApplyVolumes();
        }

        #endregion

        #region Public

        public void SetVersionImmediate(NeighborhoodVersion version)
        {
            _fadeId++;
            _blend = NeighborhoodVersions.ToBlend(version);
            ApplyVolumes();
        }

        /// <summary>Dips both loops (true) or brings them back (false), smoothly.</summary>
        public void SetDucked(bool isDucked)
        {
            _duckTarget = isDucked ? _duckLevel : 1f;
        }

        public void FadeToVersion(NeighborhoodVersion version)
        {
            FadeToVersionSafeAsync(version, _lifetime.Token).Forget();
        }

        public async UniTask FadeToVersionAsync(NeighborhoodVersion version, CancellationToken cancellationToken)
        {
            int fadeId = ++_fadeId;
            float from = _blend;
            float to = NeighborhoodVersions.ToBlend(version);
            float duration = _fadeDuration * Mathf.Abs(to - from);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                await NeighborhoodFrames.NextFrameAsync(cancellationToken);
                if (fadeId != _fadeId)
                {
                    return;
                }

                elapsed += Time.deltaTime;
                _blend = Mathf.Lerp(from, to, NeighborhoodEasing.InOutCubic(Mathf.Clamp01(elapsed / duration)));
                ApplyVolumes();
            }

            _blend = to;
            ApplyVolumes();
        }

        #endregion

        #region Internal

        internal void Configure(AudioSource natureSource, AudioSource efficiencySource)
        {
            _natureSource = natureSource;
            _efficiencySource = efficiencySource;
        }

        #endregion

        #region Private

        private async UniTask FadeToVersionSafeAsync(NeighborhoodVersion version, CancellationToken cancellationToken)
        {
            try
            {
                await FadeToVersionAsync(version, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Destroyed mid-fade.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void ApplyVolumes()
        {
            float angle = _blend * Mathf.PI * 0.5f;
            float volume = _volume * _duck;
            if (_natureSource != null)
            {
                _natureSource.volume = Mathf.Cos(angle) * volume;
            }

            if (_efficiencySource != null)
            {
                _efficiencySource.volume = Mathf.Sin(angle) * volume;
            }
        }

        private static void PlayIfReady(AudioSource source)
        {
            if (source != null && source.clip != null && !source.isPlaying)
            {
                source.Play();
            }
        }

        #endregion
    }
}
