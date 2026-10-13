using System;
using UnityEngine;

namespace DroneDDW.Experience
{
	// One subtitle: the clip it belongs to, who says it (empty for the orb) and the words
	[Serializable]
	internal sealed class SubtitleEntry
	{
		#region Serialized fields

		[SerializeField] private AudioClip _clip;

		[Tooltip("Shown before the words in the speaker's colour; empty for the orb, the narrator")]
		[SerializeField] private string _speaker = string.Empty;

		[SerializeField] private Color _speakerColor = Color.white;

		[TextArea(1, 4)]
		[SerializeField] private string _text = string.Empty;

		#endregion

		#region Properties

		internal AudioClip Clip => _clip;
		internal string Speaker => _speaker;
		internal Color SpeakerColor => _speakerColor;
		internal string Text => _text;

		#endregion

		#region Setup

		internal SubtitleEntry(AudioClip clip, string speaker, Color speakerColor, string text)
		{
			_clip = clip;
			_speaker = speaker ?? string.Empty;
			_speakerColor = speakerColor;
			_text = text ?? string.Empty;
		}

		// For Unity's serializer
		private SubtitleEntry()
		{
		}

		#endregion
	}
}
