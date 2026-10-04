using UnityEngine;

namespace DroneDDW.Characters
{
	// How talking portraits move: the mouth, the speaker highlight, the bounce on syllables and the hover, plus the
	// test scene's layout. Cards read it every frame, so changes apply live in Play mode and are kept, like any asset.
	[CreateAssetMenu(fileName = "Character Portrait Settings",
		menuName = "DroneDDW/Characters/Character Portrait Settings")]
	internal sealed class CharacterPortraitSettings : ScriptableObject
	{
		#region Serialized fields

		[Header("Voice")]
		[Tooltip("Reads the voice this far ahead of the playhead, in seconds, so the mouth doesn't trail the sound")]
		[SerializeField] private float _leadSeconds = 0.04f;

		[Header("Mouth")]
		[Tooltip("Flap swaps the closed and open portraits. Smooth keeps the closed one and opens the mouth gradually "
			+ "(experimental).")]
		[SerializeField] private CharacterMouthStyle _mouthStyle = CharacterMouthStyle.Flap;

		[Tooltip("How fast the mouth follows rising vowel sounds, in seconds")]
		[Min(0f)]
		[SerializeField] private float _attackSeconds = 0.02f;

		[Tooltip("How fast the mouth follows falling vowel sounds, in seconds")]
		[Min(0f)]
		[SerializeField] private float _releaseSeconds = 0.06f;

		[Tooltip("Vowel level (0 to 1) that opens the mouth")]
		[Range(0f, 1f)]
		[SerializeField] private float _openThreshold = 0.3f;

		[Tooltip("Vowel level (0 to 1) that closes it again. Lower than the open level, so it doesn't flicker.")]
		[Range(0f, 1f)]
		[SerializeField] private float _closeThreshold = 0.18f;

		[Tooltip("Shortest time the mouth stays open, in seconds")]
		[Min(0f)]
		[SerializeField] private float _minOpenSeconds = 0.07f;

		[Tooltip("Shortest time the mouth stays closed, in seconds")]
		[Min(0f)]
		[SerializeField] private float _minClosedSeconds = 0.05f;

		[Tooltip("A new syllable closes the mouth for a moment once it has been open this long, in seconds, so long "
			+ "phrases don't freeze it open")]
		[Min(0f)]
		[SerializeField] private float _syllableFlapSeconds = 0.12f;

		[Tooltip("Smooth mouth: how wide the mouth opens for the voice's vowel level")]
		[Range(0.5f, 4f)]
		[SerializeField] private float _smoothGain = 1.5f;

		[Header("Speaker highlight")]
		[Tooltip("How much bigger the speaking card gets")]
		[Range(1f, 1.3f)]
		[SerializeField] private float _highlightScale = 1.08f;

		[Tooltip("How far the speaking card comes towards you, in metres")]
		[Range(0f, 0.2f)]
		[SerializeField] private float _highlightForwardMeters = 0.06f;

		[Tooltip("Brightness of the cards that aren't speaking (1 = no dimming)")]
		[Range(0f, 1f)]
		[SerializeField] private float _dimBrightness = 0.6f;

		[Tooltip("How long the highlight takes to move between cards, in seconds")]
		[Min(0.01f)]
		[SerializeField] private float _highlightSmoothSeconds = 0.2f;

		[Header("Syllable bounce")]
		[Tooltip("How far each syllable stretches the portrait (0 = off)")]
		[Range(0f, 0.15f)]
		[SerializeField] private float _bounceAmount = 0.04f;

		[Tooltip("How fast the bounce dies away, per second")]
		[Min(0f)]
		[SerializeField] private float _bounceDecayPerSecond = 9f;

		[Header("Hover")]
		[Tooltip("How far the cards float up and down, in metres (0 = still)")]
		[Range(0f, 0.05f)]
		[SerializeField] private float _hoverAmplitudeMeters = 0.01f;

		[Tooltip("How long one float up and down takes, in seconds")]
		[Min(0.5f)]
		[SerializeField] private float _hoverPeriodSeconds = 4f;

		[Header("Test scene")]
		[Tooltip("Each new line goes to the next speaker and emotion, through all nine combinations")]
		[SerializeField] private bool _isAutoCycleOn = true;

		[Tooltip("Silence between lines, in seconds")]
		[Min(0f)]
		[SerializeField] private float _pauseBetweenLinesSeconds = 0.8f;

		[Tooltip("How far in front of your head the cards float, in metres")]
		[Range(0.5f, 3f)]
		[SerializeField] private float _cardDistanceMeters = 1.1f;

		[Tooltip("Distance between neighbouring cards, in metres")]
		[Range(0.2f, 1f)]
		[SerializeField] private float _cardSpacingMeters = 0.38f;

		[Tooltip("How far below your eyes the cards' centres sit, in metres")]
		[Range(-0.3f, 0.5f)]
		[SerializeField] private float _cardDropMeters = 0.08f;

		#endregion

		#region Properties

		internal float LeadSeconds => _leadSeconds;
		internal CharacterMouthStyle MouthStyle => _mouthStyle;
		internal float AttackSeconds => _attackSeconds;
		internal float ReleaseSeconds => _releaseSeconds;
		internal float OpenThreshold => _openThreshold;
		internal float CloseThreshold => _closeThreshold;
		internal float MinOpenSeconds => _minOpenSeconds;
		internal float MinClosedSeconds => _minClosedSeconds;
		internal float SyllableFlapSeconds => _syllableFlapSeconds;
		internal float SmoothGain => _smoothGain;
		internal float HighlightScale => _highlightScale;
		internal float HighlightForwardMeters => _highlightForwardMeters;
		internal float DimBrightness => _dimBrightness;
		internal float HighlightSmoothSeconds => _highlightSmoothSeconds;
		internal float BounceAmount => _bounceAmount;
		internal float BounceDecayPerSecond => _bounceDecayPerSecond;
		internal float HoverAmplitudeMeters => _hoverAmplitudeMeters;
		internal float HoverPeriodSeconds => _hoverPeriodSeconds;
		internal bool IsAutoCycleOn => _isAutoCycleOn;
		internal float PauseBetweenLinesSeconds => _pauseBetweenLinesSeconds;
		internal float CardDistanceMeters => _cardDistanceMeters;
		internal float CardSpacingMeters => _cardSpacingMeters;
		internal float CardDropMeters => _cardDropMeters;

		#endregion
	}
}
