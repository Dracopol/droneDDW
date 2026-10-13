using System;
using DroneDDW.Neighborhood.Voice;
using UnityEngine;

namespace DroneDDW.Experience
{
	// Subtitles for everything said in the experience, low in the view and following the head (a second notice panel):
	// the orb's lines and the cards' introductions by their clip (Subtitle Table), and the people in the Neighborhood
	// from its voice director while it is loaded. A speaker's name comes first, in their colour; the orb has none. One
	// line at a time: a new one replaces the one shown, and a line only takes its own subtitle away.
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/Experience Subtitles")]
	internal sealed class ExperienceSubtitles : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private NoticePanel _panel;
		[SerializeField] private SubtitleTable _table;

		[Tooltip("The people in the Neighborhood: their names and colours")]
		[SerializeField] private SubtitleSpeaker[] _speakers = Array.Empty<SubtitleSpeaker>();

		#endregion

		#region Fields

		private NeighborhoodVoiceDirector _neighborhoodVoice;
		private int _shownTicket;
		private int _lastTicket;
		private int _lineTicket;

		#endregion

		#region Setup

		private void OnDestroy()
		{
			DetachNeighborhood();
		}

		#endregion

		#region Event handlers

		private void HandleLineStart(NeighborhoodVoiceLine line)
		{
			SubtitleSpeaker speaker = FindSpeaker(line);
			_lineTicket = Show(speaker != null ? speaker.Name : line.Character.ToString(),
				speaker != null ? speaker.Color : Color.white, line.Text);
		}

		private void HandleLineEnd(NeighborhoodVoiceLine line)
		{
			Hide(_lineTicket);
		}

		#endregion

		#region Internal

		internal void Configure(NoticePanel panel, SubtitleTable table, SubtitleSpeaker[] speakers)
		{
			_panel = panel;
			_table = table;
			_speakers = speakers ?? Array.Empty<SubtitleSpeaker>();
		}

		// Shows the words of a clip the table knows; returns the ticket that takes it away again (0: nothing shown)
		internal int ShowClip(AudioClip clip)
		{
			return _table != null && _table.TryGet(clip, out SubtitleEntry entry)
				? Show(entry.Speaker, entry.SpeakerColor, entry.Text)
				: 0;
		}

		// Takes a subtitle away, unless a newer line has replaced it meanwhile
		internal void Hide(int ticket)
		{
			if (ticket == 0 || ticket != _shownTicket || _panel == null)
			{
				return;
			}

			_shownTicket = 0;
			_panel.Hide();
		}

		// The Neighborhood's people speak through its voice director while it is loaded
		internal void AttachNeighborhood(NeighborhoodVoiceDirector voice)
		{
			DetachNeighborhood();
			_neighborhoodVoice = voice;
			if (_neighborhoodVoice != null)
			{
				_neighborhoodVoice.OnLineStart += HandleLineStart;
				_neighborhoodVoice.OnLineEnd += HandleLineEnd;
			}
		}

		internal void DetachNeighborhood()
		{
			if (_neighborhoodVoice != null)
			{
				_neighborhoodVoice.OnLineStart -= HandleLineStart;
				_neighborhoodVoice.OnLineEnd -= HandleLineEnd;
				_neighborhoodVoice = null;
			}

			Hide(_lineTicket);
		}

		// The speaker's name in bold and their colour, then the words
		internal static string Format(string speaker, Color color, string text)
		{
			return string.IsNullOrWhiteSpace(speaker)
				? text
				: $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{speaker}:</b></color> {text}";
		}

		#endregion

		#region Private

		private int Show(string speaker, Color color, string text)
		{
			if (_panel == null || string.IsNullOrWhiteSpace(text))
			{
				return 0;
			}

			_shownTicket = ++_lastTicket;
			_panel.Show(Format(speaker, color, text));
			return _shownTicket;
		}

		private SubtitleSpeaker FindSpeaker(NeighborhoodVoiceLine line)
		{
			foreach (SubtitleSpeaker speaker in _speakers)
			{
				if (speaker != null && speaker.Character == line.Character)
				{
					return speaker;
				}
			}

			return null;
		}

		#endregion
	}
}
