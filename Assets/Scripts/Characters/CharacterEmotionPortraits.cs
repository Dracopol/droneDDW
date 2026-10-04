using System;
using UnityEngine;

namespace DroneDDW.Characters
{
	// One emotion's two portraits: mouth closed and mouth open
	[Serializable]
	internal struct CharacterEmotionPortraits
	{
		#region Serialized fields

		[SerializeField] private Texture2D _closed;
		[SerializeField] private Texture2D _open;

		#endregion

		#region Properties

		internal Texture2D Closed => _closed;
		internal Texture2D Open => _open;

		#endregion

		#region Setup

		internal CharacterEmotionPortraits(Texture2D closed, Texture2D open)
		{
			_closed = closed;
			_open = open;
		}

		#endregion
	}
}
