using System;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Settings
{
	// The morph between look A, neutral and look B (S.morph in the web preview)
	[Serializable]
	internal sealed class NarratorOrbMorphSettings
	{
		#region Serialized fields

		[Tooltip("The step the orb starts at: 0 A, 1 semi A, 2 neutral, 3 semi B, 4 B (step). Changing it in Play mode "
			+ "moves the orb there; SetStep doesn't change it.")]
		[Range(0, StepCount - 1)]
		[SerializeField] private int _step = 2;

		[Tooltip("Morph ease: SmoothDamp's smooth time, in seconds (smoothTime)")]
		[Range(0.1f, 1.5f)]
		[SerializeField] private float _smoothTimeSeconds = 0.45f;

		[Tooltip("Wisps lead the colour: dust and wisps reach the look's colour first, so semi steps read as two-tone. "
			+ "0 blends everything as one (wispsLead).")]
		[Range(0f, 1f)]
		[SerializeField] private float _wispsLead = 1f;

		[Tooltip("The morph value of each step: -1 full A, 0 neutral sphere, 1 full B (steps)")]
		[SerializeField] private float[] _steps = (float[])DefaultSteps.Clone();

		#endregion

		#region Fields

		internal const int StepCount = 5;

		private static readonly float[] DefaultSteps = { -1f, -0.5f, 0f, 0.5f, 1f };

		#endregion

		#region Properties

		internal int Step { get => _step; set => _step = value; }
		internal float SmoothTimeSeconds { get => _smoothTimeSeconds; set => _smoothTimeSeconds = value; }
		internal float WispsLead { get => _wispsLead; set => _wispsLead = value; }
		internal float[] Steps { get => _steps; set => _steps = value; }

		#endregion

		#region Internal

		// Five steps, and a start step among them
		internal void Validate()
		{
			_steps = NarratorOrbSettingsUtil.FitLength(_steps, DefaultSteps);
			_step = Mathf.Clamp(_step, 0, StepCount - 1);
		}

		#endregion
	}
}
