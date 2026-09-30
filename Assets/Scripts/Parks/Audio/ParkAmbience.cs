using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DroneDDW.Parks.Levels;
using DroneDDW.Parks.Utilities;
using UnityEngine;

namespace DroneDDW.Parks.Audio
{
	/// <summary>
	/// Two looping ambience beds, nature and efficiency, blended with an equal-power crossfade:
	/// level 0 plays only nature, level 4 only efficiency, and the levels between mix both
	/// without a dip in loudness. While the slider shows a level that isn't committed yet, the blend
	/// leans part of the way toward it. Assign any looping clips to the two sources.
	/// </summary>
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Parks/Park Ambience")]
	public sealed class ParkAmbience : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField, Tooltip("Loop for the nature side (birds, wind, water).")]
		private AudioSource _natureSource;

		[SerializeField, Tooltip("Loop for the efficiency side (hum, drones, traffic).")]
		private AudioSource _efficiencySource;

		[SerializeField, Range(0f, 1f)] private float _volume = 0.8f;

		[SerializeField, Min(0f), Tooltip("Seconds every crossfade takes, whatever the size of the step. The park itself takes about 4 s.")]
		private float _fadeDuration = 3.5f;

		[Header("Preview")]
		[SerializeField, Range(0f, 1f), Tooltip("How far the blend leans toward a level shown on the slider "
			+ "before it is committed. 0 = no preview.")]
		private float _previewAmount = 0.5f;

		[SerializeField, Min(0f), Tooltip("Seconds the blend takes to lean toward a previewed level, or back.")]
		private float _previewFadeDuration = 0.6f;

		#endregion

		#region Fields

		private CancellationTokenSource _lifetime;
		private float _blend;
		private float _levelBlend;
		private bool _isPreviewing;
		private int _fadeId;

		#endregion

		#region Properties

		/// <summary>0 = only nature, 1 = only efficiency.</summary>
		public float Blend => _blend;

		private CancellationToken LifetimeToken
		{
			get
			{
				if (_lifetime == null)
				{
					_lifetime = new CancellationTokenSource();
				}

				return _lifetime.Token;
			}
		}

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
			if (_lifetime == null)
			{
				return;
			}

			_lifetime.Cancel();
			_lifetime.Dispose();
			_lifetime = null;
		}

		#endregion

		#region Public

		public void SetLevelImmediate(int level)
		{
			_fadeId++;
			SetLevelBlend(level);
			_blend = _levelBlend;
			ApplyVolumes();
		}

		public void FadeToLevel(int level)
		{
			SetLevelBlend(level);
			FadeToBlendSafeAsync(_levelBlend, _fadeDuration, LifetimeToken).Forget();
		}

		public async UniTask FadeToLevelAsync(int level, CancellationToken cancellationToken)
		{
			SetLevelBlend(level);
			await FadeToBlendAsync(_levelBlend, _fadeDuration, cancellationToken);
		}

		/// <summary>Leans part of the way toward a level shown on the slider but not committed yet.</summary>
		public void PreviewLevel(int level)
		{
			if (_previewAmount <= 0f)
			{
				return;
			}

			_isPreviewing = true;
			float blend = Mathf.Lerp(_levelBlend, ParkLevels.ToBlend(level), _previewAmount);
			FadeToBlendSafeAsync(blend, _previewFadeDuration, LifetimeToken).Forget();
		}

		/// <summary>Leans back to the park's level after a preview. Does nothing when there was no preview.</summary>
		public void EndPreview()
		{
			if (!_isPreviewing)
			{
				return;
			}

			_isPreviewing = false;
			FadeToBlendSafeAsync(_levelBlend, _previewFadeDuration, LifetimeToken).Forget();
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

		private void SetLevelBlend(int level)
		{
			_levelBlend = ParkLevels.ToBlend(level);
			_isPreviewing = false;
		}

		/// <summary>Fades from the current blend. A newer fade takes over from wherever this one got to.</summary>
		private async UniTask FadeToBlendAsync(float to, float duration, CancellationToken cancellationToken)
		{
			int fadeId = ++_fadeId;
			float from = _blend;
			if (Mathf.Approximately(from, to))
			{
				duration = 0f;
			}

			float elapsed = 0f;
			while (elapsed < duration)
			{
				await UniTask.NextFrame(cancellationToken);
				if (fadeId != _fadeId)
				{
					return;
				}

				elapsed += Time.deltaTime;
				_blend = Mathf.Lerp(from, to, ParkEasing.InOutCubic(Mathf.Clamp01(elapsed / duration)));
				ApplyVolumes();
			}

			_blend = to;
			ApplyVolumes();
		}

		private async UniTask FadeToBlendSafeAsync(float to, float duration, CancellationToken cancellationToken)
		{
			try
			{
				await FadeToBlendAsync(to, duration, cancellationToken);
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
			if (_natureSource != null)
			{
				_natureSource.volume = Mathf.Cos(angle) * _volume;
			}

			if (_efficiencySource != null)
			{
				_efficiencySource.volume = Mathf.Sin(angle) * _volume;
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
