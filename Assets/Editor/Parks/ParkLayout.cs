using System;
using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Parks.Editor.Building
{
	/// <summary>
	/// One park's layout as exported by the Blender build script (Layouts/*_Layout.json).
	/// Positions are in Unity space: x = east, y = up, z = north, 1 unit = 1 m.
	/// The level park adds the level names; the A/B layouts leave them empty.
	/// </summary>
	[Serializable]
	internal sealed class ParkLayout
	{
		#region Serialized fields

		[SerializeField] private string _parkName = string.Empty;
		[SerializeField] private string _prefabFolder = string.Empty;
		[SerializeField] private List<string> _levelNames = new List<string>();
		[SerializeField] private List<ParkLayoutEntry> _entries = new List<ParkLayoutEntry>();

		#endregion

		#region Properties

		public string ParkName => _parkName;
		public string PrefabFolder => _prefabFolder;
		public IReadOnlyList<string> LevelNames => _levelNames;
		public IReadOnlyList<ParkLayoutEntry> Entries => _entries;

		#endregion
	}
}
