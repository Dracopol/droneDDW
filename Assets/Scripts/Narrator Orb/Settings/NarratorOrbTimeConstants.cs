using System;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Settings
{
	// How fast the orb follows the voice, in seconds (S.timeConstants in the web preview). Release times get multiplied
	// by the reaction's smoothing.
	[Serializable]
	internal sealed class NarratorOrbTimeConstants
	{
		#region Serialized fields

		[Tooltip("How fast loudness rises (levelAttack)")]
		[Min(0f)]
		[SerializeField] private float _levelAttackSeconds = 0.03f;

		[Tooltip("How fast loudness falls (levelRelease)")]
		[Min(0f)]
		[SerializeField] private float _levelReleaseSeconds = 0.22f;

		[Tooltip("How fast hiss rises (hissAttack)")]
		[Min(0f)]
		[SerializeField] private float _hissAttackSeconds = 0.015f;

		[Tooltip("How fast hiss falls (hissRelease)")]
		[Min(0f)]
		[SerializeField] private float _hissReleaseSeconds = 0.14f;

		[Tooltip("How fast each of the four wisps lights up on vowels (wispAttack)")]
		[SerializeField] private float[] _wispAttackSeconds = (float[])DefaultWispAttackSeconds.Clone();

		[Tooltip("How fast each of the four wisps fades (wispRelease)")]
		[SerializeField] private float[] _wispReleaseSeconds = (float[])DefaultWispReleaseSeconds.Clone();

		[Tooltip("How fast the syllable pulse dies away, per second (pulseDecayPerSecond)")]
		[Min(0f)]
		[SerializeField] private float _pulseDecayPerSecond = 7f;

		#endregion

		#region Fields

		internal const int WispCount = 4;

		private static readonly float[] DefaultWispAttackSeconds = { 0.05f, 0.07f, 0.09f, 0.06f };
		private static readonly float[] DefaultWispReleaseSeconds = { 0.3f, 0.38f, 0.46f, 0.34f };

		#endregion

		#region Properties

		internal float LevelAttackSeconds { get => _levelAttackSeconds; set => _levelAttackSeconds = value; }
		internal float LevelReleaseSeconds { get => _levelReleaseSeconds; set => _levelReleaseSeconds = value; }
		internal float HissAttackSeconds { get => _hissAttackSeconds; set => _hissAttackSeconds = value; }
		internal float HissReleaseSeconds { get => _hissReleaseSeconds; set => _hissReleaseSeconds = value; }
		internal float[] WispAttackSeconds { get => _wispAttackSeconds; set => _wispAttackSeconds = value; }
		internal float[] WispReleaseSeconds { get => _wispReleaseSeconds; set => _wispReleaseSeconds = value; }
		internal float PulseDecayPerSecond { get => _pulseDecayPerSecond; set => _pulseDecayPerSecond = value; }

		#endregion

		#region Internal

		// One value per wisp: a missing value gets its default back
		internal void Validate()
		{
			_wispAttackSeconds = NarratorOrbSettingsUtil.FitLength(_wispAttackSeconds, DefaultWispAttackSeconds);
			_wispReleaseSeconds = NarratorOrbSettingsUtil.FitLength(_wispReleaseSeconds, DefaultWispReleaseSeconds);
		}

		#endregion
	}
}
