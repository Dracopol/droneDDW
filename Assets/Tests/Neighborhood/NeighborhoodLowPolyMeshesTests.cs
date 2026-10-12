using System.Linq;
using DroneDDW.Neighborhood.Editor.Building;
using NUnit.Framework;
using UnityEngine;

namespace DroneDDW.Neighborhood.Tests
{
	internal sealed class NeighborhoodLowPolyMeshesTests
	{
		#region Fields

		private Mesh _mesh;

		#endregion

		#region Setup

		[TearDown]
		public void DestroyMesh()
		{
			Object.DestroyImmediate(_mesh);
		}

		#endregion

		#region Tests

		[Test]
		public void PaletteCell_Uv_IsTheCellsMiddle()
		{
			Vector2 uv = NeighborhoodLowPolyMeshes.Cream.Uv;

			// Column 1, row 6 from the top of a 16 x 16 palette
			Assert.That(uv.x, Is.EqualTo(1.5f / 16f).Within(1e-6f));
			Assert.That(uv.y, Is.EqualTo(1f - 6.5f / 16f).Within(1e-6f));
		}

		[Test]
		public void CreatePrism_OpenBottom_HasEverySideAndATopFan()
		{
			_mesh = NeighborhoodLowPolyMeshes.CreatePrism("Prism", 12, 0.05f, 0f, 0.02f, NeighborhoodLowPolyMeshes.Slate,
				NeighborhoodLowPolyMeshes.Cream, false);

			Assert.That(_mesh.vertexCount, Is.EqualTo(12 * 4 + 13));
			Assert.That(_mesh.triangles.Length / 3, Is.EqualTo(12 * 2 + 12));
		}

		[Test]
		public void CreatePrism_WithBottom_IsFlatShadedAndFacesOut()
		{
			_mesh = NeighborhoodLowPolyMeshes.CreatePrism("Prism", 8, 0.05f, -0.01f, 0.02f, NeighborhoodLowPolyMeshes.Slate,
				NeighborhoodLowPolyMeshes.Cream, true);

			AssertFlatAndOutward(_mesh, Vector3.up * 0.005f);
		}

		[Test]
		public void CreatePrism_Colours_TopCreamSidesSlate()
		{
			_mesh = NeighborhoodLowPolyMeshes.CreatePrism("Prism", 12, 0.05f, 0f, 0.02f, NeighborhoodLowPolyMeshes.Slate,
				NeighborhoodLowPolyMeshes.Cream, false);

			Vector3[] normals = _mesh.normals;
			Vector2[] uvs = _mesh.uv;
			for (int vertex = 0; vertex < _mesh.vertexCount; vertex++)
			{
				Vector2 expected = normals[vertex].y > 0.99f
					? NeighborhoodLowPolyMeshes.Cream.Uv
					: NeighborhoodLowPolyMeshes.Slate.Uv;
				Assert.That(uvs[vertex], Is.EqualTo(expected), $"vertex {vertex}");
			}
		}

		[Test]
		public void CreateBoxes_LegAndTop_AreClosedFlatBoxes()
		{
			(Vector3 Min, Vector3 Max)[] boxes =
			{
				(new Vector3(-0.025f, 0f, -0.025f), new Vector3(0.025f, 0.68f, 0.025f)),
				(new Vector3(-0.09f, 0.68f, -0.09f), new Vector3(0.09f, 0.72f, 0.09f)),
			};

			_mesh = NeighborhoodLowPolyMeshes.CreateBoxes("Stand", boxes, NeighborhoodLowPolyMeshes.Wood,
				NeighborhoodLowPolyMeshes.Cream);

			Assert.That(_mesh.vertexCount, Is.EqualTo(2 * 6 * 4));
			Assert.That(_mesh.bounds.max.y, Is.EqualTo(0.72f).Within(1e-5f));
			Assert.That(_mesh.uv.Distinct().Count(), Is.EqualTo(2));
			AssertFacesOutOfBoxes(_mesh, boxes);
		}

		[Test]
		public void CreateRingSegment_LiesFaceUpBetweenTheRadii()
		{
			_mesh = NeighborhoodLowPolyMeshes.CreateRingSegment("Segment", 0.036f, 0.042f, 19.5f, 2, 0.0128f,
				NeighborhoodLowPolyMeshes.White);

			Assert.That(_mesh.vertexCount, Is.EqualTo(2 * 4));
			foreach (Vector3 vertex in _mesh.vertices)
			{
				float radius = new Vector2(vertex.x, vertex.z).magnitude;
				Assert.That(radius, Is.InRange(0.036f - 1e-5f, 0.042f + 1e-5f));
				Assert.That(vertex.y, Is.EqualTo(0.0128f).Within(1e-6f));
				Assert.That(Mathf.Abs(Mathf.Atan2(vertex.x, vertex.z) * Mathf.Rad2Deg), Is.LessThanOrEqualTo(9.75f + 1e-3f));
			}

			AssertFlatAndOutward(_mesh, Vector3.down);
		}

		#endregion

		#region Private

		// Every triangle's vertices share its own normal (flat shading), which faces away from the point given: a face is
		// front-facing when it winds clockwise, seen from that normal
		private static void AssertFlatAndOutward(Mesh mesh, Vector3 inside)
		{
			Vector3[] vertices = mesh.vertices;
			Vector3[] normals = mesh.normals;
			int[] triangles = mesh.triangles;
			for (int triangle = 0; triangle < triangles.Length; triangle += 3)
			{
				Vector3 a = vertices[triangles[triangle]];
				Vector3 b = vertices[triangles[triangle + 1]];
				Vector3 c = vertices[triangles[triangle + 2]];
				Vector3 faceNormal = Vector3.Cross(b - a, c - a).normalized;
				for (int corner = 0; corner < 3; corner++)
				{
					Assert.That(Vector3.Dot(normals[triangles[triangle + corner]], faceNormal), Is.GreaterThan(0.999f),
						$"triangle {triangle / 3}");
				}

				Vector3 middle = (a + b + c) / 3f;
				Assert.That(Vector3.Dot(faceNormal, middle - inside), Is.GreaterThan(0f), $"triangle {triangle / 3} faces in");
			}
		}

		// Every face of every box faces away from the middle of its own box
		private static void AssertFacesOutOfBoxes(Mesh mesh, (Vector3 Min, Vector3 Max)[] boxes)
		{
			Vector3[] vertices = mesh.vertices;
			int[] triangles = mesh.triangles;
			int trianglesPerBox = triangles.Length / boxes.Length;
			for (int triangle = 0; triangle < triangles.Length; triangle += 3)
			{
				(Vector3 min, Vector3 max) = boxes[triangle / trianglesPerBox];
				Vector3 a = vertices[triangles[triangle]];
				Vector3 b = vertices[triangles[triangle + 1]];
				Vector3 c = vertices[triangles[triangle + 2]];
				Vector3 faceNormal = Vector3.Cross(b - a, c - a).normalized;
				Vector3 middle = (a + b + c) / 3f;
				Assert.That(Vector3.Dot(faceNormal, middle - (min + max) * 0.5f), Is.GreaterThan(0f),
					$"triangle {triangle / 3} faces in");
			}
		}

		#endregion
	}
}
