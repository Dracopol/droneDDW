using DroneDDW.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DroneDDW.Characters
{
	// A floating card with a character's portrait that talks along with its voice. The voice is read like the Narrator
	// Orb's (analysed ahead, syllables included); the mouth flaps or opens smoothly with it, each syllable gives the
	// portrait a small bounce, the card hovers gently, and when it is the speaker it comes forward while the other
	// cards dim.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Characters/Character Card")]
	public sealed class CharacterCard : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private CharacterPortraitSet _portraits;

		[Tooltip("Shared by every card that uses this asset. Changes made in Play mode are kept.")]
		[SerializeField] private CharacterPortraitSettings _settings;

		[Tooltip("The character's voice, 3D at the card")]
		[SerializeField] private AudioSource _voice;

		[Tooltip("On the voice's object: follows the live output when a clip can't be read ahead")]
		[SerializeField] private VoiceLiveTap _liveTap;

		[SerializeField] private CharacterEmotion _startEmotion = CharacterEmotion.Natural;

		[Tooltip("Where this card is in the shared hover, 0 to 1, so the cards don't bob together")]
		[Range(0f, 1f)]
		[SerializeField] private float _hoverPhase;

		[Header("Parts")]
		[Tooltip("The card's canvas: hovers, and comes forward when speaking")]
		[SerializeField] private Transform _body;

		[SerializeField] private RawImage _portrait;

		[Tooltip("The smooth mouth: the open portrait's mouth, laid over the closed portrait")]
		[SerializeField] private RawImage _mouth;

		[SerializeField] private Image _border;
		[SerializeField] private TextMeshProUGUI _label;

		#endregion

		#region Fields

		private readonly MouthFlap _mouthFlap = new();
		private VoiceReader _voiceReader;
		private Transform _transform;
		private RectTransform _portraitTransform;
		private RectTransform _mouthTransform;
		private Vector3 _bodyRestPosition;
		private Vector3 _bodyRestScale;
		private CharacterEmotion _emotion;
		private bool _isHighlighted;
		private bool _hasSyllableStarted;
		private float _highlight;
		private float _highlightVelocity;
		private float _bounce;
		private int _syllableCount;

		#endregion

		#region Properties

		internal CharacterPortraitSet Portraits => _portraits;
		internal CharacterPortraitSettings Settings => _settings;
		internal CharacterEmotion Emotion => _emotion;
		// While a line plays; it stays true while the session is paused (the listener pauses the voice, it doesn't stop)
		public bool IsSpeaking => _voice.isPlaying;
		internal bool IsMouthOpen => _mouthFlap.IsOpen;
		internal float MouthOpenness => _mouthFlap.Openness;
		internal float VowelEnvelope => _mouthFlap.Envelope;

		// How often the mouth opened, and how many syllables passed, since the current line started
		internal int MouthOpenCount => _mouthFlap.OpenCount;
		internal int SyllableCount => _syllableCount;

		internal VoiceStatus VoiceStatus => _voiceReader?.Status ?? VoiceStatus.Idle;

		// The analysis of the clip being spoken, or null before it is done
		internal VoiceAnalysis Analysis => _voiceReader?.Analysis;

		// Where the line is, in seconds
		internal float PlayheadSeconds => _voice.time;

		#endregion

		#region Setup

		private void Awake()
		{
			_transform = transform;
			_portraitTransform = _portrait.rectTransform;
			_mouthTransform = _mouth.rectTransform;
			_bodyRestPosition = _body.localPosition;
			_bodyRestScale = _body.localScale;
			_emotion = _startEmotion;
			_voiceReader = new VoiceReader(_voice, _liveTap);
			_voiceReader.OnSyllable += HandleSyllable;
			FitPortrait();
			RefreshLabel();
		}

		private void OnDestroy()
		{
			if (_voiceReader != null)
			{
				_voiceReader.OnSyllable -= HandleSyllable;
				_voiceReader.Dispose();
			}
		}

		#endregion

		#region Update

		private void Update()
		{
			float deltaSeconds = Time.deltaTime;
			VoiceFeatures features = _voiceReader.Read(VoiceAnalysisSettings.Defaults, _settings.LeadSeconds,
				Time.unscaledDeltaTime);
			_mouthFlap.Step((float)features.Vowel, _hasSyllableStarted, deltaSeconds, _settings);
			_hasSyllableStarted = false;
			_bounce *= Mathf.Exp(-_settings.BounceDecayPerSecond * deltaSeconds);
			_highlight = Mathf.SmoothDamp(_highlight, _isHighlighted ? 1f : 0f, ref _highlightVelocity,
				_settings.HighlightSmoothSeconds);

			ShowMouth();
			ShowPose();
			ShowHighlight();
		}

		#endregion

		#region Event handlers

		private void HandleSyllable(float strength)
		{
			_hasSyllableStarted = true;
			_bounce = Mathf.Max(_bounce, strength);
			_syllableCount++;
		}

		#endregion

		#region Internal

		internal void SetEmotion(CharacterEmotion emotion)
		{
			_emotion = emotion;
			RefreshLabel();
		}

		public void SetHighlighted(bool isHighlighted)
		{
			_isHighlighted = isHighlighted;
		}

		// Reads a line ahead before it is spoken, so the mouth moves from its first syllable
		public void Prepare(AudioClip clip)
		{
			_voiceReader.Prepare(clip, VoiceAnalysisSettings.Defaults);
		}

		// Starts a line from the beginning
		public void Speak(AudioClip clip)
		{
			_voice.clip = clip;
			_voice.Play();
			_mouthFlap.Reset();
			_hasSyllableStarted = false;
			_syllableCount = 0;
		}

		public void Stop()
		{
			_voice.Stop();
		}

		internal void Pause()
		{
			_voice.Pause();
		}

		internal void Resume()
		{
			_voice.UnPause();
		}

		public void PlaceAt(Vector3 position, Quaternion rotation)
		{
			_transform.SetPositionAndRotation(position, rotation);
		}

		// The label's text, "Name · Emotion" with the name in bold
		internal static string GetLabel(CharacterPortraitSet portraits, CharacterEmotion emotion)
		{
			return $"<b>{portraits.DisplayName}</b>  ·  {emotion}";
		}

		internal void Configure(CharacterPortraitSet portraits, CharacterPortraitSettings settings, AudioSource voice,
			VoiceLiveTap liveTap, float hoverPhase, Transform body, RawImage portrait, RawImage mouth, Image border,
			TextMeshProUGUI label)
		{
			_portraits = portraits;
			_settings = settings;
			_voice = voice;
			_liveTap = liveTap;
			_hoverPhase = hoverPhase;
			_body = body;
			_portrait = portrait;
			_mouth = mouth;
			_border = border;
			_label = label;
		}

		#endregion

		#region Private

		// Shows the set's crop, and puts the smooth mouth exactly over the mouth inside it
		private void FitPortrait()
		{
			Rect crop = _portraits.CropRect;
			Rect mouth = _portraits.MouthRect;
			_portrait.uvRect = crop;
			_mouth.uvRect = mouth;
			_mouthTransform.anchorMin = new Vector2((mouth.xMin - crop.xMin) / crop.width,
				(mouth.yMin - crop.yMin) / crop.height);
			_mouthTransform.anchorMax = new Vector2((mouth.xMax - crop.xMin) / crop.width,
				(mouth.yMax - crop.yMin) / crop.height);
			_mouthTransform.offsetMin = Vector2.zero;
			_mouthTransform.offsetMax = Vector2.zero;
			_mouthTransform.pivot = new Vector2(0.5f, 0.5f);
			_mouth.enabled = false;
		}

		private void ShowMouth()
		{
			bool isSmooth = _settings.MouthStyle == CharacterMouthStyle.Smooth;
			_portrait.texture = _portraits.GetPortrait(_emotion, !isSmooth && _mouthFlap.IsOpen);
			float openness = isSmooth ? _mouthFlap.Openness : 0f;
			bool isMouthShown = openness > 0.01f;
			_mouth.enabled = isMouthShown;
			if (isMouthShown)
			{
				_mouth.texture = _portraits.GetPortrait(_emotion, true);
				_mouthTransform.localScale = new Vector3(1f, openness, 1f);
			}
		}

		// Hover, the speaker coming forward, and the syllable bounce (the portrait stretches up from its bottom edge)
		private void ShowPose()
		{
			float cycle = Time.time / _settings.HoverPeriodSeconds + _hoverPhase;
			float hover = _settings.HoverAmplitudeMeters * Mathf.Sin(cycle * 2f * Mathf.PI);
			float forward = _settings.HighlightForwardMeters * _highlight;
			_body.localPosition = _bodyRestPosition + new Vector3(0f, hover, -forward);
			_body.localScale = _bodyRestScale * (1f + (_settings.HighlightScale - 1f) * _highlight);

			float bounce = _settings.BounceAmount * _bounce;
			_portraitTransform.localScale = new Vector3(1f - bounce * 0.5f, 1f + bounce, 1f);
		}

		// The cards that aren't speaking dim, by tint rather than transparency, so they stay solid over passthrough
		private void ShowHighlight()
		{
			float brightness = Mathf.Lerp(_settings.DimBrightness, 1f, _highlight);
			Color tint = new(brightness, brightness, brightness, 1f);
			_portrait.color = tint;
			_mouth.color = tint;
			_border.color = _portraits.AccentColor * tint;
			if (_label != null)
			{
				_label.color = tint;
			}
		}

		// The test scene's cards show who speaks and how; the experience's have no label
		private void RefreshLabel()
		{
			if (_label != null)
			{
				_label.text = GetLabel(_portraits, _emotion);
			}
		}

		#endregion
	}
}
