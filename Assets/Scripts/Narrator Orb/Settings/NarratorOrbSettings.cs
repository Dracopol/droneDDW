using UnityEngine;

namespace DroneDDW.NarratorOrb.Settings
{
	// Every value of the web preview's settings (S), with the same defaults, plus how the orb renders in Unity. The orb
	// reads this every frame, so changes apply live in Play mode and are kept, like any asset. Paste JSON reads the web
	// page's "Copy settings" output.
	[CreateAssetMenu(fileName = "Narrator Orb Settings", menuName = "DroneDDW/Narrator Orb/Narrator Orb Settings")]
	internal sealed class NarratorOrbSettings : ScriptableObject
	{
		#region Serialized fields

		[Tooltip("How the voice is analysed")]
		[SerializeField] private NarratorOrbVoiceSettings _voice = new();

		[Tooltip("What the voice drives")]
		[SerializeField] private NarratorOrbReactionSettings _reaction = new();

		[Tooltip("How fast the orb follows the voice")]
		[SerializeField] private NarratorOrbTimeConstants _timeConstants = new();

		[Tooltip("The rings each syllable sends over the surface")]
		[SerializeField] private NarratorOrbRippleSettings _ripple = new();

		[Tooltip("The morph between look A, neutral and look B")]
		[SerializeField] private NarratorOrbMorphSettings _morph = new();

		[Tooltip("The shapes and colours of looks A and B")]
		[SerializeField] private NarratorOrbLookSettings _look = new();

		[Tooltip("Unity only: intensity, blending, particle size and passthrough alpha")]
		[SerializeField] private NarratorOrbRenderSettings _rendering = new();

		#endregion

		#region Properties

		internal NarratorOrbVoiceSettings Voice => _voice;
		internal NarratorOrbReactionSettings Reaction => _reaction;
		internal NarratorOrbTimeConstants TimeConstants => _timeConstants;
		internal NarratorOrbRippleSettings Ripple => _ripple;
		internal NarratorOrbMorphSettings Morph => _morph;
		internal NarratorOrbLookSettings Look => _look;
		internal NarratorOrbRenderSettings Rendering => _rendering;

		#endregion

		#region Setup

		private void OnValidate()
		{
			Validate();
		}

		#endregion

		#region Internal

		// Keeps the per-wisp and per-step lists at their lengths, so the orb can index them
		internal void Validate()
		{
			_timeConstants.Validate();
			_morph.Validate();
		}

		#endregion
	}
}
