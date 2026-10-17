using System;
using System.Globalization;
using UnityEngine;

namespace DroneDDW.Experience
{
	// Everything about the participant's session that is content rather than code: what the narrator says at each step
	// (all placeholder lines to begin with), the stuck-hint timing, how long things take, the texts shown in the
	// headset, and what the sessions file calls each choice. One asset (Content/Data/Experience), created once and never
	// overwritten by the scene builder, so swapping audio or timing here survives every rebuild. Read live.
	[CreateAssetMenu(fileName = "Experience Settings", menuName = "DroneDDW/Experience/Experience Settings")]
	public sealed class ExperienceSettings : ScriptableObject
	{
		#region Serialized fields

		[Header("Narration (the orb)")]
		[Tooltip("The orb appears and greets the participant")]
		[SerializeField] private AudioClip _welcome;

		[Tooltip("Line both hands up with the ghost hands")]
		[SerializeField] private GuidedStepLines _handCheck = new();

		[Tooltip("Move the slider to another notch; the orb changes with it")]
		[SerializeField] private GuidedStepLines _slider = new();

		[Tooltip("Hold the button to lock the practice choice in")]
		[SerializeField] private GuidedStepLines _practiceLockIn = new();

		[Tooltip("Before the characters introduce themselves")]
		[SerializeField] private AudioClip _charactersIntro;

		[Tooltip("Each character's line, in the cards' order: grandma, student, worker")]
		[SerializeField] private AudioClip[] _characterLines = Array.Empty<AudioClip>();

		[Tooltip("In the Neighborhood, before the choice")]
		[SerializeField] private AudioClip _neighborhoodWelcome;

		[Tooltip("Make the real choice in the Neighborhood and lock it in")]
		[SerializeField] private GuidedStepLines _neighborhoodLockIn = new();

		[Header("Neighborhood table")]
		[Tooltip("Plays as the slider, then the lock-in button, pop in after the Neighborhood welcome")]
		[SerializeField] private AudioClip _tablePopClip;

		[Tooltip("Between the slider popping in and the button popping in")]
		[Min(0f)]
		[SerializeField] private float _tablePopGapSeconds = 0.4f;

		[Tooltip("How long each one takes to grow in")]
		[Min(0.01f)]
		[SerializeField] private float _tablePopGrowSeconds = 0.25f;

		[Header("Narration (the orb), back in passthrough")]
		[Tooltip("Back in passthrough")]
		[SerializeField] private AudioClip _outro;

		[Tooltip("The last line: the participant can take the headset off")]
		[SerializeField] private AudioClip _goodbye;

		[Header("Stuck hints")]
		[Tooltip("From the end of a step's prompt to its first stuck line")]
		[Min(0f)]
		[SerializeField] private float _firstHintDelaySeconds = 8f;

		[Tooltip("From the end of a stuck line to the next one")]
		[Min(1f)]
		[SerializeField] private float _hintRepeatSeconds = 15f;

		[Header("Timing")]
		[Tooltip("How long the admin holds trigger or grip (Space in the Editor) to start, once the participant has the "
			+ "headset on and has adjusted it (the scene then recentres and the intro starts), and to end at the goodbye")]
		[Min(0.1f)]
		[SerializeField] private float _startHoldSeconds = 0.5f;

		[Tooltip("Between two lines in a row")]
		[Min(0f)]
		[SerializeField] private float _pauseBetweenLinesSeconds = 0.4f;

		[Tooltip("Each fade to black and back, when switching between passthrough and the Neighborhood")]
		[Min(0.05f)]
		[SerializeField] private float _fadeSeconds = 1f;

		[Tooltip("How long the view stays black after everything has switched")]
		[Min(0f)]
		[SerializeField] private float _blackHoldSeconds = 0.3f;

		[Header("Texts in the headset")]
		[Tooltip("For the admin after Start session; {0} is the participant number")]
		[SerializeField] private string _handoverText = "Give the headset to participant {0}";

		[Tooltip("For the participant once the headset is on, until the admin starts")]
		[SerializeField] private string _readyText =
			"Adjust the headset until it sits comfortably and the view is sharp.\nWe'll start in a moment.";

		[Tooltip("For the participant at the end")]
		[SerializeField] private string _goodbyeText = "Thank you! You can take off the headset now.";

		[Header("Sessions file")]
		[Tooltip("What the Form choice column says for each version: nature, balanced, efficiency")]
		[SerializeField] private string[] _formChoiceNames = { "Nature", "Balanced", "Efficiency" };

		#endregion

		#region Properties

		public AudioClip Welcome => _welcome;
		public GuidedStepLines HandCheck => _handCheck;
		public GuidedStepLines Slider => _slider;
		public GuidedStepLines PracticeLockIn => _practiceLockIn;
		public AudioClip CharactersIntro => _charactersIntro;
		public AudioClip NeighborhoodWelcome => _neighborhoodWelcome;
		public GuidedStepLines NeighborhoodLockIn => _neighborhoodLockIn;
		public AudioClip TablePopClip => _tablePopClip;
		public float TablePopGapSeconds => _tablePopGapSeconds;
		public float TablePopGrowSeconds => _tablePopGrowSeconds;
		public AudioClip Outro => _outro;
		public AudioClip Goodbye => _goodbye;
		public float FirstHintDelaySeconds => _firstHintDelaySeconds;
		public float HintRepeatSeconds => _hintRepeatSeconds;
		public float StartHoldSeconds => _startHoldSeconds;
		public float PauseBetweenLinesSeconds => _pauseBetweenLinesSeconds;
		public float FadeSeconds => _fadeSeconds;
		public float BlackHoldSeconds => _blackHoldSeconds;
		public string ReadyText => _readyText;
		public string GoodbyeText => _goodbyeText;

		#endregion

		#region Internal

		// The line of the character at this place in the cards' order, or null
		internal AudioClip GetCharacterLine(int character)
		{
			return character >= 0 && character < _characterLines.Length ? _characterLines[character] : null;
		}

		internal string GetHandoverText(string participantId)
		{
			return string.Format(_handoverText, participantId);
		}

		// The Form choice for a version (0 nature, 1 balanced, 2 efficiency), or the number when the name is missing
		internal string GetFormChoiceName(int version)
		{
			return version >= 0 && version < _formChoiceNames.Length && !string.IsNullOrWhiteSpace(_formChoiceNames[version])
				? _formChoiceNames[version]
				: version.ToString(CultureInfo.InvariantCulture);
		}

		// Every line set to the one clip, for a new asset before the real lines exist
		internal void UsePlaceholder(AudioClip clip, int characterCount)
		{
			_welcome = clip;
			_charactersIntro = clip;
			_neighborhoodWelcome = clip;
			_outro = clip;
			_goodbye = clip;
			foreach (GuidedStepLines lines in new[]
				{ _handCheck, _slider, _practiceLockIn, _neighborhoodLockIn })
			{
				lines.Configure(clip, clip, clip);
			}

			_characterLines = new AudioClip[characterCount];
			for (int character = 0; character < characterCount; character++)
			{
				_characterLines[character] = clip;
			}
		}

		#endregion
	}
}
