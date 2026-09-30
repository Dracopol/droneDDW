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
	/// without a dip in loudness. Assign any looping clips to the two sources.
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

		#endregion

		#region Fields

		private CancellationTokenSource _lifetime;
		private float _blend;
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
			_blend = ParkLevels.ToBlend(level);
			ApplyVolumes();
		}

		public void FadeToLevel(int level)
		{
			FadeToLevelSafeAsync(level, LifetimeToken).Forget();
		}

		public async UniTask FadeToLevelAsync(int level, CancellationToken cancellationToken)
		{
			int fadeId = ++_fadeId;
			float from = _blend;
			float to = ParkLevels.ToBlend(level);
			float duration = Mathf.Approximately(from, to) ? 0f : _fadeDuration;
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

		#endregion

		#region Internal

		internal void Configure(AudioSource natureSource, AudioSource efficiencySource)
		{
			_natureSource = natureSource;
			_efficiencySource = efficiencySource;
		}

		#endregion

		#region Private

		private async UniTask FadeToLevelSafeAsync(int level, CancellationToken cancellationToken)
		{
			try
			{
				await FadeToLevelAsync(level, cancellationToken);
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
