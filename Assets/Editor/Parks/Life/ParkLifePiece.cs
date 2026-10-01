using DroneDDW.Parks.Editor.Building;
using DroneDDW.Parks.Levels;
using UnityEngine;

namespace DroneDDW.Parks.Editor.Life
{
	/// <summary>
	/// One placed park object as the life builder sees it: its layout entry, the object holding its level element
	/// (a wrapper, for props that move), and the body carrying its mesh.
	/// </summary>
	internal sealed class ParkLifePiece
	{
		#region Properties

		internal ParkLayoutEntry Entry { get; }
		internal GameObject Root { get; }
		internal Transform Body { get; }

		/// <summary>Null for objects in every level, which never change.</summary>
		internal ParkLevelElement Element { get; }

		internal string Prefab => Entry.Prefab;

		/// <summary>Where it stands (the park root sits at the origin, so layout positions are world positions).</summary>
		internal Vector3 Position => Entry.Position;

		/// <summary>Its renderer, found even while the piece is hidden at the level the park is built at.</summary>
		internal Renderer Renderer => Body.GetComponentInChildren<Renderer>(true);

		/// <summary>Its mesh's box in world space, from the mesh itself (hidden pieces have no renderer bounds).</summary>
		internal Bounds WorldBounds
		{
			get
			{
				MeshFilter filter = Body.GetComponentInChildren<MeshFilter>(true);
				if (filter == null || filter.sharedMesh == null)
				{
					return new Bounds(Position, Vector3.one);
				}

				Bounds local = filter.sharedMesh.bounds;
				Matrix4x4 matrix = filter.transform.localToWorldMatrix;
				Bounds world = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
				for (int corner = 0; corner < 8; corner++)
				{
					Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f,
						(corner & 4) == 0 ? -1f : 1f);
					world.Encapsulate(matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, sign)));
				}

				return world;
			}
		}

		#endregion

		#region Setup

		internal ParkLifePiece(ParkLayoutEntry entry, GameObject root, Transform body, ParkLevelElement element)
		{
			Entry = entry;
			Root = root;
			Body = body;
			Element = element;
		}

		#endregion
	}
}
