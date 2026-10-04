using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneDDW.Characters.TestScene
{
	// Runs the character portraits test: the characters take turns saying the test line. With auto-cycle on, each new
	// line goes to the next speaker and emotion, through all nine combinations; otherwise the line repeats. Keys: 1, 2,
	// 3 make grandma, student or worker the speaker; 4, 5, 6 set the speaker's emotion to natural, happy or angry (both
	// stop the auto-cycle); A carries on with the auto-cycle; Space pauses and resumes; R places the cards in front of
	// your head again. The cards move in front of your head when the headset reports its first pose.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Characters/Character Card Test Controls")]
	internal sealed class CharacterCardTestControls : MonoBehaviour
	{
		#region Serialized fields

		[Tooltip("Grandma, student and worker, left to right")]
		[SerializeField] private CharacterCard[] _cards;

		[SerializeField] private CharacterPortraitSettings _settings;

		[Tooltip("The line every character says")]
		[SerializeField] private AudioClip _line;

		[Tooltip("The headset camera: the cards are placed in front of it")]
		[SerializeField] private Transform _head;

		#endregion

		#region Fields

		private const int EmotionCount = 3;

		// Below this the head looks almost straight up or down and its yaw is unreliable
		private const float MinFlatForwardLength = 0.2f;

		private const float FirstLineDelaySeconds = 1f;

		private int _speaker;
		private int _combo;
		private bool _isAutoCycling;
		private bool _isPaused;
		private bool _isPlaced;
		private float _pauseRemainingSeconds;

		#endregion

		#region Properties

		internal CharacterCard Speaker => _cards[_speaker];

		// The speaker's place in the row: 0 grandma, 1 student, 2 worker
		internal int SpeakerIndex => _speaker;

		internal bool IsAutoCycling => _isAutoCycling;
		internal bool IsPaused => _isPaused;

		// How many lines have started
		internal int LineCount { get; private set; }

		#endregion

		#region Setup

		// Every card reads the line ahead now; the first line waits a moment for that and for Play mode's first frames
		private void Start()
		{
			_isAutoCycling = _settings.IsAutoCycleOn;
			_pauseRemainingSeconds = FirstLineDelaySeconds;
			foreach (CharacterCard card in _cards)
			{
				card.Prepare(_line);
			}
		}

		#endregion

		#region Update

		private void Update()
		{
			// The camera sits exactly on its parent's origin until the headset reports its first pose
			if (!_isPlaced && _head.localPosition != Vector3.zero)
			{
				_isPlaced = true;
				PlaceInFrontOfHead();
			}

			ReadKeys();
			if (_isPaused || Speaker.IsSpeaking)
			{
				return;
			}

			_pauseRemainingSeconds -= Time.deltaTime;
			if (_pauseRemainingSeconds > 0f)
			{
				return;
			}

			if (_isAutoCycling)
			{
				StartCombo(LineCount == 0 ? 0 : (_combo + 1) % (_cards.Length * EmotionCount));
			}
			else
			{
				StartLine(_speaker, Speaker.Emotion);
			}
		}

		#endregion

		#region Internal

		internal void Configure(CharacterCard[] cards, CharacterPortraitSettings settings, AudioClip line,
			Transform head)
		{
			_cards = cards;
			_settings = settings;
			_line = line;
			_head = head;
		}

		// Keys 1 to 3: this card says the line from the start, and the auto-cycle stops
		internal void SelectSpeaker(int card)
		{
			_isAutoCycling = false;
			StartLine(card, _cards[card].Emotion);
		}

		// Keys 4 to 6: the speaker's emotion, mid-line, and the auto-cycle stops
		internal void SelectEmotion(CharacterEmotion emotion)
		{
			_isAutoCycling = false;
			Speaker.SetEmotion(emotion);
		}

		// Key A: carries on with the next combination
		internal void ResumeAutoCycle()
		{
			if (_isAutoCycling)
			{
				return;
			}

			_isAutoCycling = true;
			StartCombo((_combo + 1) % (_cards.Length * EmotionCount));
		}

		internal void TogglePause()
		{
			_isPaused = !_isPaused;
			if (_isPaused)
			{
				Speaker.Pause();
			}
			else
			{
				Speaker.Resume();
			}
		}

		// The cards in an arc around the head at eye level (a little lower), turned with the head's yaw only
		internal void PlaceInFrontOfHead()
		{
			Vector3 forward = _head.forward;
			forward.y = 0f;
			if (forward.magnitude < MinFlatForwardLength)
			{
				forward = _head.up;
				forward.y = 0f;
			}

			for (int card = 0; card < _cards.Length; card++)
			{
				Pose pose = GetCardPose(_head.position, forward, card, _cards.Length, _settings);
				_cards[card].PlaceAt(pose.position, pose.rotation);
			}

			Debug.Log($"[Characters] Test scene: placed the cards {_settings.CardDistanceMeters} m in front of the "
				+ $"head at {_head.position}.", this);
		}

		// Where a card goes in the arc: facing the head, at the settings' distance and drop, the cards spaced evenly
		// along the arc. flatForward is the head's facing, level with the floor.
		internal static Pose GetCardPose(Vector3 headPosition, Vector3 flatForward, int card, int cardCount,
			CharacterPortraitSettings settings)
		{
			Vector3 forward = flatForward.normalized;
			Vector3 right = Vector3.Cross(Vector3.up, forward);
			float distance = settings.CardDistanceMeters;
			float angle = (card - (cardCount - 1) * 0.5f) * settings.CardSpacingMeters / distance;
			Vector3 direction = forward * Mathf.Cos(angle) + right * Mathf.Sin(angle);
			Vector3 position = headPosition + Vector3.down * settings.CardDropMeters + direction * distance;
			return new Pose(position, Quaternion.LookRotation(direction, Vector3.up));
		}

		#endregion

		#region Private

		private void ReadKeys()
		{
			Keyboard keyboard = Keyboard.current;
			if (keyboard == null)
			{
				return;
			}

			for (int card = 0; card < _cards.Length; card++)
			{
				if (WasNumberPressed(keyboard, card + 1))
				{
					SelectSpeaker(card);
				}
			}

			for (int emotion = 0; emotion < EmotionCount; emotion++)
			{
				if (WasNumberPressed(keyboard, emotion + 4))
				{
					SelectEmotion((CharacterEmotion)emotion);
				}
			}

			if (keyboard.aKey.wasPressedThisFrame)
			{
				ResumeAutoCycle();
			}

			if (keyboard.spaceKey.wasPressedThisFrame)
			{
				TogglePause();
			}

			if (keyboard.rKey.wasPressedThisFrame)
			{
				PlaceInFrontOfHead();
			}
		}

		// Combination n: speaker n mod 3, emotion shifted by one every round, so nine lines show every pair once
		private void StartCombo(int combo)
		{
			_combo = combo;
			int speaker = combo % _cards.Length;
			int emotion = (speaker + combo / _cards.Length) % EmotionCount;
			StartLine(speaker, (CharacterEmotion)emotion);
		}

		private void StartLine(int speaker, CharacterEmotion emotion)
		{
			_speaker = speaker;
			_isPaused = false;
			_pauseRemainingSeconds = _settings.PauseBetweenLinesSeconds;
			for (int card = 0; card < _cards.Length; card++)
			{
				_cards[card].SetHighlighted(card == speaker);
				if (card != speaker)
				{
					_cards[card].Stop();
				}
			}

			Speaker.SetEmotion(emotion);
			Speaker.Speak(_line);
			LineCount++;
		}

		private static bool WasNumberPressed(Keyboard keyboard, int number)
		{
			switch (number)
			{
				case 1: return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
				case 2: return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
				case 3: return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
				case 4: return keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame;
				case 5: return keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame;
				case 6: return keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame;
				default: throw new ArgumentOutOfRangeException(nameof(number), number, null);
			}
		}

		#endregion
	}
}
