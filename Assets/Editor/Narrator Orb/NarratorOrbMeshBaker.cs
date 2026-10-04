using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace DroneDDW.NarratorOrb.Editor
{
	// The orb's meshes: the particle quads (four vertices per particle sharing its attributes, plus a corner id), the
	// six halo shells and the inner shell (each vertex carrying its shell's size factor and base opacity), and the
	// ripple ring sphere. The spheres are the preview's SphereGeometry(1, 64, 48); its winding stays front-facing in
	// Unity, because seeing the same coordinates in Unity's left-handed space mirrors the winding back.
	internal static class NarratorOrbMeshBaker
	{
		#region Entities

		// One particle vertex as the particle shader reads it. Public fields in the order of ParticleLayout, because
		// the vertex buffer copies the struct's memory as it is.
		[StructLayout(LayoutKind.Sequential)]
		private struct ParticleVertex
		{
			public Vector3 direction;
			public Vector4 random;
			public Vector3 radiusLayerCorner;
			public Vector3 seed;
		}

		#endregion

		#region Fields

		private const int SphereWidthSegments = 64;
		private const int SphereHeightSegments = 48;

		// The six halos (size factor, base opacity) and the inner shell, from the preview
		internal static readonly Vector2[] HaloShells =
		{
			new(1.015f, 0.1f), new(1.03f, 0.07f), new(1.05f, 0.05f), new(1.075f, 0.035f), new(1.1f, 0.025f),
			new(1.13f, 0.015f),
		};

		internal static readonly Vector2 InnerShell = new(0.97f, 0.18f);

		// ±1.5 m: wider than anything the swell, shapes, ripples or a poke can reach, so the orb is never culled
		private static readonly Bounds OrbBounds = new(Vector3.zero, new Vector3(3f, 3f, 3f));

		private static readonly VertexAttributeDescriptor[] ParticleLayout =
		{
			new(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
			new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
			new(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 3),
			new(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 3),
		};

		#endregion

		#region Internal

		// Bakes an evenly spaced share of each layer (all of it at scale 1) and returns how many particles that is
		internal static int BakeParticles(Mesh mesh, NarratorOrbParticleSet particles, float countScale)
		{
			List<int> kept = new(particles.Count);
			int previousLayer = -1;
			int indexInLayer = 0;
			for (int i = 0; i < particles.Count; i++)
			{
				if (particles.Layers[i] != previousLayer)
				{
					previousLayer = particles.Layers[i];
					indexInLayer = 0;
				}

				if (Math.Floor((indexInLayer + 1) * (double)countScale) > Math.Floor(indexInLayer * (double)countScale))
				{
					kept.Add(i);
				}

				indexInLayer++;
			}

			ParticleVertex[] vertices = new ParticleVertex[kept.Count * 4];
			int[] indices = new int[kept.Count * 6];
			for (int quad = 0; quad < kept.Count; quad++)
			{
				int particle = kept[quad];
				for (int corner = 0; corner < 4; corner++)
				{
					vertices[quad * 4 + corner] = new ParticleVertex
					{
						direction = particles.Directions[particle],
						random = particles.Randoms[particle],
						radiusLayerCorner = new Vector3(particles.Radii[particle], particles.Layers[particle], corner),
						seed = particles.Seeds[particle],
					};
				}

				int first = quad * 4;
				int triangle = quad * 6;
				indices[triangle] = first;
				indices[triangle + 1] = first + 1;
				indices[triangle + 2] = first + 2;
				indices[triangle + 3] = first;
				indices[triangle + 4] = first + 2;
				indices[triangle + 5] = first + 3;
			}

			mesh.Clear();
			mesh.SetVertexBufferParams(vertices.Length, ParticleLayout);
			mesh.SetVertexBufferData(vertices, 0, 0, vertices.Length);
			mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
			mesh.SetIndexBufferData(indices, 0, 0, indices.Length);
			mesh.subMeshCount = 1;
			mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length), MeshUpdateFlags.DontRecalculateBounds);
			mesh.bounds = OrbBounds;
			return kept.Count;
		}

		// One sphere per shell, its size factor and base opacity in the first UV
		internal static void BakeShells(Mesh mesh, IReadOnlyList<Vector2> shells)
		{
			List<Vector3> spherePositions = new();
			List<int> sphereIndices = new();
			BuildSphere(spherePositions, sphereIndices);

			List<Vector3> positions = new();
			List<Vector2> shellValues = new();
			List<int> indices = new();
			foreach (Vector2 shell in shells)
			{
				int offset = positions.Count;
				positions.AddRange(spherePositions);
				for (int i = 0; i < spherePositions.Count; i++)
				{
					shellValues.Add(shell);
				}

				foreach (int index in sphereIndices)
				{
					indices.Add(offset + index);
				}
			}

			mesh.Clear();
			mesh.indexFormat = positions.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
			mesh.SetVertices(positions);
			mesh.SetUVs(0, shellValues);
			mesh.SetTriangles(indices, 0, false);
			mesh.bounds = OrbBounds;
		}

		internal static void BakeSphere(Mesh mesh)
		{
			List<Vector3> positions = new();
			List<int> indices = new();
			BuildSphere(positions, indices);
			mesh.Clear();
			mesh.SetVertices(positions);
			mesh.SetTriangles(indices, 0, false);
			mesh.bounds = OrbBounds;
		}

		#endregion

		#region Private

		// THREE.SphereGeometry(1, 64, 48): rows from the top pole down, one triangle per quad at the poles
		private static void BuildSphere(List<Vector3> positions, List<int> indices)
		{
			int[,] grid = new int[SphereHeightSegments + 1, SphereWidthSegments + 1];
			for (int row = 0; row <= SphereHeightSegments; row++)
			{
				double theta = (double)row / SphereHeightSegments * Math.PI;
				for (int column = 0; column <= SphereWidthSegments; column++)
				{
					double phi = (double)column / SphereWidthSegments * Math.PI * 2;
					positions.Add(new Vector3((float)(-Math.Cos(phi) * Math.Sin(theta)), (float)Math.Cos(theta),
						(float)(Math.Sin(phi) * Math.Sin(theta))));
					grid[row, column] = positions.Count - 1;
				}
			}

			for (int row = 0; row < SphereHeightSegments; row++)
			{
				for (int column = 0; column < SphereWidthSegments; column++)
				{
					int a = grid[row, column + 1];
					int b = grid[row, column];
					int c = grid[row + 1, column];
					int d = grid[row + 1, column + 1];
					if (row != 0)
					{
						indices.Add(a);
						indices.Add(b);
						indices.Add(d);
					}

					if (row != SphereHeightSegments - 1)
					{
						indices.Add(b);
						indices.Add(c);
						indices.Add(d);
					}
				}
			}
		}

		#endregion
	}
}
