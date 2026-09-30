using System;
using UnityEngine;

namespace DroneDDW.Parks.Editor.Building
{
	/// <summary>
	/// One placed instance: which piece, its instance name, its category group and local transform.
	/// Level park entries also say which levels they belong to and how they enter and leave;
	/// unique entries (path chunks, ponds, buildings) are copied from the kit instead of becoming prefabs.
	/// </summary>
	[Serializable]
	internal sealed class ParkLayoutEntry
	{
		#region Serialized fields

		[SerializeField] private string _prefab = string.Empty;
		[SerializeField] private string _name = string.Empty;
		[SerializeField] private string _group = string.Empty;
		[SerializeField] private bool _unique;
		[SerializeField] private Vector3 _position = Vector3.zero;
		[SerializeField] private float _rotationY;
		[SerializeField] private Vector3 _scale = Vector3.one;
		[SerializeField] private int _levelMask = 31;
		[SerializeField] private int _style;
		[SerializeField] private float _liftDepth = 1f;

		#endregion

		#region Properties

		public string Prefab => _prefab;
		public string Name => _name;
		public string Group => _group;
		public bool Unique => _unique;
		public Vector3 Position => _position;
		public float RotationY => _rotationY;
		public Vector3 Scale => _scale;
		public int LevelMask => _levelMask;
		public int Style => _style;
		public float LiftDepth => _liftDepth;

		#endregion
	}
}
