using System;
using DroneDDW.Neighborhood.Characters;
using UnityEngine;

namespace DroneDDW.Experience
{
	// How a person in the Neighborhood is named in the subtitles, and in which colour
	[Serializable]
	internal sealed class SubtitleSpeaker
	{
		#region Serialized fields

		[SerializeField] private NeighborhoodCharacterId _character;
		[SerializeField] private string _name = string.Empty;
		[SerializeField] private Color _color = Color.white;

		#endregion

		#region Properties

		internal NeighborhoodCharacterId Character => _character;
		internal string Name => _name;
		internal Color Color => _color;

		#endregion

		#region Setup

		internal SubtitleSpeaker(NeighborhoodCharacterId character, string name, Color color)
		{
			_character = character;
			_name = name ?? string.Empty;
			_color = color;
		}

		// For Unity's serializer
		private SubtitleSpeaker()
		{
		}

		#endregion
	}
}
