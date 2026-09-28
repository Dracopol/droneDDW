using System;
using UnityEngine;

namespace DroneDDW.Droney
{
	// The caption text for each of Droney's voice clips, lines left empty show no caption
	[CreateAssetMenu(menuName = "DroneDDW/Droney Caption Table")]
	internal class DroneyCaptionTable : ScriptableObject
	{
		#region Entities

		[Serializable]
		private class Entry
		{
			[SerializeField] private AudioClip _clip;

			[TextArea(2, 4)]
			[SerializeField] private string _text;

			internal AudioClip Clip => _clip;
			internal string Text => _text;
		}

		#endregion

		#region Serialized fields

		[SerializeField] private Entry[] _entries;

		#endregion

		#region Internal

		internal bool TryGetCaption(AudioClip clip, out string caption)
		{
			foreach (Entry entry in _entries)
			{
				if (entry.Clip != clip || string.IsNullOrWhiteSpace(entry.Text))
					continue;

				caption = entry.Text;
				return true;
			}

			caption = null;
			return false;
		}

		#endregion
	}
}
