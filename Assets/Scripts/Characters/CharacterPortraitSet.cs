using System;
using UnityEngine;

namespace DroneDDW.Characters
{
	// One character's portraits, closed and open for each emotion, and the two parts of the image a card uses: the crop
	// it shows, and the mouth, where the open and closed portraits differ. DroneDDW > Characters > Build Portrait
	// Assets fills it from the files in Content/Textures/character_portraits and measures both rects from their pixels.
	[CreateAssetMenu(fileName = "Character Portraits", menuName = "DroneDDW/Characters/Character Portrait Set")]
	internal sealed class CharacterPortraitSet : ScriptableObject
	{
		#region Serialized fields

		[Tooltip("Shown on the card's label")]
		[SerializeField] private string _displayName = "Character";

		[Tooltip("The card's border colour")]
		[ColorUsage(false)]
		[SerializeField] private Color _accentColor = Color.white;

		[Tooltip("Natural, happy and angry, in that order")]
		[SerializeField] private CharacterEmotionPortraits[] _emotions = new CharacterEmotionPortraits[EmotionCount];

		[Tooltip("The part of the portraits a card shows, as a fraction of the image from its bottom left")]
		[SerializeField] private Rect _cropRect = new(0f, 0f, 1f, 1f);

		[Tooltip("The mouth, where the open and closed portraits differ, as a fraction of the image from its bottom "
			+ "left")]
		[SerializeField] private Rect _mouthRect;

		#endregion

		#region Fields

		private const int EmotionCount = 3;

		#endregion

		#region Properties

		internal string DisplayName => _displayName;
		internal Color AccentColor => _accentColor;
		internal Rect CropRect => _cropRect;
		internal Rect MouthRect => _mouthRect;

		#endregion

		#region Setup

		private void OnValidate()
		{
			if (_emotions == null || _emotions.Length != EmotionCount)
			{
				Array.Resize(ref _emotions, EmotionCount);
			}
		}

		#endregion

		#region Internal

		internal Texture2D GetPortrait(CharacterEmotion emotion, bool isMouthOpen)
		{
			CharacterEmotionPortraits portraits = _emotions[(int)emotion];
			return isMouthOpen ? portraits.Open : portraits.Closed;
		}

		// The builder's measurements, refreshed on every build
		internal void Configure(CharacterEmotionPortraits[] emotions, Rect cropRect, Rect mouthRect)
		{
			_emotions = emotions;
			_cropRect = cropRect;
			_mouthRect = mouthRect;
		}

		// Only set when the builder creates the asset, so later edits are kept
		internal void ConfigureLook(string displayName, Color accentColor)
		{
			_displayName = displayName;
			_accentColor = accentColor;
		}

		#endregion
	}
}
