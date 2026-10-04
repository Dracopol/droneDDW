using System;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Settings
{
	// The two looks the orb morphs to: a shape and a base colour each (S.look in the web preview). The rest of each
	// palette is derived from the base colour.
	[Serializable]
	internal sealed class NarratorOrbLookSettings
	{
		#region Serialized fields

		[Tooltip("Shape at step 0 (shapeA)")]
		[SerializeField] private NarratorOrbShape _shapeA = NarratorOrbShape.Bloom;

		[Tooltip("Base colour at step 0 (colorA)")]
		[SerializeField] private Color32 _colorA = new(0x45, 0xd4, 0x83, 0xff);

		[Tooltip("Shape at step 4 (shapeB)")]
		[SerializeField] private NarratorOrbShape _shapeB = NarratorOrbShape.Cube;

		[Tooltip("Base colour at step 4 (colorB)")]
		[SerializeField] private Color32 _colorB = new(0xff, 0x9f, 0x43, 0xff);

		#endregion

		#region Properties

		internal NarratorOrbShape ShapeA { get => _shapeA; set => _shapeA = value; }
		internal Color32 ColorA { get => _colorA; set => _colorA = value; }
		internal NarratorOrbShape ShapeB { get => _shapeB; set => _shapeB = value; }
		internal Color32 ColorB { get => _colorB; set => _colorB = value; }

		#endregion
	}
}
