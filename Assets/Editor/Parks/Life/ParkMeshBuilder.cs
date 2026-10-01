using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DroneDDW.Parks.Editor.Life
{
	/// <summary>
	/// Builds low-poly, flat-shaded meshes in the park's style: every face is flat, has its own vertices and samples a
	/// single palette swatch. Faces are added to the current bone, so a model's parts (body, wings, head, tail) can move
	/// on their own as one skinned mesh. The winding of each face is worked out from the direction it should face, so a
	/// face can be listed with its corners in either order.
	/// </summary>
	internal sealed class ParkMeshBuilder
	{
		#region Fields

		private readonly ParkPalette _palette;
		private readonly List<Vector3> _vertices = new List<Vector3>();
		private readonly List<Vector3> _normals = new List<Vector3>();
		private readonly List<Vector2> _uvs = new List<Vector2>();
		private readonly List<int> _triangles = new List<int>();
		private readonly List<BoneWeight> _weights = new List<BoneWeight>();

		#endregion

		#region Properties

		/// <summary>The bone that faces added from now on belong to.</summary>
		internal int Bone { get; set; }

		#endregion

		#region Setup

		internal ParkMeshBuilder(ParkPalette palette)
		{
			_palette = palette;
		}

		#endregion

		#region Internal

		/// <summary>One flat face from corners around its edge, turned to face outward.</summary>
		internal void Face(string hex, Vector3 outward, params Vector3[] corners)
		{
			Vector3 normal = Vector3.Cross(corners[1] - corners[0], corners[2] - corners[0]).normalized;
			if (Vector3.Dot(normal, outward) < 0f)
			{
				corners = corners.Reverse().ToArray();
				normal = -normal;
			}

			Vector2 uv = _palette.Uv(hex);
			int start = _vertices.Count;
			foreach (Vector3 corner in corners)
			{
				_vertices.Add(corner);
				_normals.Add(normal);
				_uvs.Add(uv);
				_weights.Add(new BoneWeight { boneIndex0 = Bone, weight0 = 1f });
			}

			for (int i = 1; i < corners.Length - 1; i++)
			{
				_triangles.Add(start);
				_triangles.Add(start + i);
				_triangles.Add(start + i + 1);
			}
		}

		/// <summary>
		/// A tapered block along Z, from a rear cross-section to a front one (width, height). Colours: its sides, top,
		/// underside, front and back.
		/// </summary>
		internal void Frustum(Vector3 rear, Vector2 rearSize, Vector3 front, Vector2 frontSize, string sides, string top,
			string bottom, string frontHex, string backHex)
		{
			Vector3 r = new Vector3(rearSize.x * 0.5f, rearSize.y * 0.5f, 0f);
			Vector3 f = new Vector3(frontSize.x * 0.5f, frontSize.y * 0.5f, 0f);
			Vector3 rlb = rear + new Vector3(-r.x, -r.y, 0f), rrb = rear + new Vector3(r.x, -r.y, 0f);
			Vector3 rlt = rear + new Vector3(-r.x, r.y, 0f), rrt = rear + new Vector3(r.x, r.y, 0f);
			Vector3 flb = front + new Vector3(-f.x, -f.y, 0f), frb = front + new Vector3(f.x, -f.y, 0f);
			Vector3 flt = front + new Vector3(-f.x, f.y, 0f), frt = front + new Vector3(f.x, f.y, 0f);
			Vector3 center = (rear + front) * 0.5f;
			Solid(center, top, rlt, flt, frt, rrt);
			Solid(center, bottom, rlb, rrb, frb, flb);
			Solid(center, sides, rlb, flb, flt, rlt);
			Solid(center, sides, rrb, rrt, frt, frb);
			Solid(center, frontHex, flb, frb, frt, flt);
			Solid(center, backHex, rlb, rlt, rrt, rrb);
		}

		internal void Box(Vector3 center, Vector3 size, string sides, string top = null, string bottom = null,
			string front = null, string back = null)
		{
			Vector2 section = new Vector2(size.x, size.y);
			Vector3 half = new Vector3(0f, 0f, size.z * 0.5f);
			Frustum(center - half, section, center + half, section, sides, top ?? sides, bottom ?? sides, front ?? sides,
				back ?? sides);
		}

		/// <summary>A four-sided point (a beak) from a rectangular base across Z to a tip.</summary>
		internal void Pyramid(Vector3 baseCenter, Vector2 baseSize, Vector3 tip, string hex)
		{
			Vector3 h = new Vector3(baseSize.x * 0.5f, baseSize.y * 0.5f, 0f);
			Vector3 lb = baseCenter + new Vector3(-h.x, -h.y, 0f), rb = baseCenter + new Vector3(h.x, -h.y, 0f);
			Vector3 lt = baseCenter + new Vector3(-h.x, h.y, 0f), rt = baseCenter + new Vector3(h.x, h.y, 0f);
			Vector3 center = (baseCenter * 3f + tip) * 0.25f;
			Solid(center, hex, lb, rb, tip);
			Solid(center, hex, rb, rt, tip);
			Solid(center, hex, rt, lt, tip);
			Solid(center, hex, lt, lb, tip);
			Solid(center, hex, lb, lt, rt, rb);
		}

		/// <summary>A flat two-sided plate (a wing, a tail, a fin): one colour above, one below.</summary>
		internal void Panel(string topHex, string bottomHex, Vector3 up, params Vector3[] corners)
		{
			Face(topHex, up, corners);
			Face(bottomHex, -up, corners);
		}

		/// <summary>Writes the faces into a mesh (cleared first, so an existing asset keeps its identity).</summary>
		internal Mesh Build(Mesh mesh, string name, Matrix4x4[] bindposes)
		{
			mesh.Clear();
			mesh.name = name;
			mesh.SetVertices(_vertices);
			mesh.SetNormals(_normals);
			mesh.SetUVs(0, _uvs);
			mesh.boneWeights = _weights.ToArray();
			mesh.bindposes = bindposes;
			mesh.SetTriangles(_triangles, 0);
			mesh.RecalculateBounds();
			return mesh;
		}

		#endregion

		#region Private

		private void Solid(Vector3 solidCenter, string hex, params Vector3[] corners)
		{
			Vector3 faceCenter = Vector3.zero;
			foreach (Vector3 corner in corners)
			{
				faceCenter += corner;
			}

			Face(hex, faceCenter / corners.Length - solidCenter, corners);
		}

		#endregion
	}
}
