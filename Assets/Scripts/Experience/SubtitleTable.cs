using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Experience
{
	// The words of the experience's own lines, by clip: the orb's lines and the character cards' introductions. Made
	// by the Experience builder (Content/Data/Experience/Experience Subtitles.asset); it adds lines it doesn't know yet
	// but never changes a text, so corrections made here stay. The lines in the Neighborhood take their words from its
	// voice library.
	internal sealed class SubtitleTable : ScriptableObject
	{
		#region Serialized fields

		[SerializeField] private List<SubtitleEntry> _entries = new();

		#endregion

		#region Properties

		internal IReadOnlyList<SubtitleEntry> Entries => _entries;

		#endregion

		#region Internal

		internal bool TryGet(AudioClip clip, out SubtitleEntry entry)
		{
			foreach (SubtitleEntry candidate in _entries)
			{
				if (candidate != null && candidate.Clip == clip && clip != null)
				{
					entry = candidate;
					return true;
				}
			}

			entry = null;
			return false;
		}

		// Adds a line once; returns false when the clip already has its words
		internal bool AddIfMissing(SubtitleEntry entry)
		{
			if (entry == null || entry.Clip == null || TryGet(entry.Clip, out _))
			{
				return false;
			}

			_entries.Add(entry);
			return true;
		}

		#endregion
	}
}
