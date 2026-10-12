using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Building
{
    /// <summary>
    /// Low-poly meshes in the street models' style for parts the Blender export doesn't have (the lock-in button and its
    /// stand): flat-shaded prisms, boxes and ring segments. Every face is coloured by pointing its UVs at the middle of one
    /// cell of Street_Palette.png, so they render with the shared palette materials, like the FBX models.
    /// </summary>
    internal static class NeighborhoodLowPolyMeshes
    {
        #region Entities

        /// <summary>A cell of the 16 × 16 palette, counted from its top left corner.</summary>
        internal readonly struct PaletteCell
        {
            internal PaletteCell(int column, int row)
            {
                Column = column;
                Row = row;
            }

            internal int Column { get; }
            internal int Row { get; }

            /// <summary>The middle of the cell in UV space, where V runs up and the rows run down.</summary>
            internal Vector2 Uv => new Vector2((Column + 0.5f) / CellsPerSide, 1f - (Row + 0.5f) / CellsPerSide);
        }

        /// <summary>The faces gathered so far, each with its own vertices so every face is flat shaded.</summary>
        private sealed class MeshParts
        {
            internal readonly List<Vector3> Vertices = new List<Vector3>();
            internal readonly List<Vector3> Normals = new List<Vector3>();
            internal readonly List<Vector2> Uvs = new List<Vector2>();
            internal readonly List<int> Triangles = new List<int>();

            /// <summary>A quad given clockwise as seen from its front: bottom left, top left, top right, bottom right.</summary>
            internal void AddQuad(Vector3 bottomLeft, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight,
                PaletteCell cell)
            {
                int first = Vertices.Count;
                Vector3 normal = Vector3.Cross(topLeft - bottomLeft, topRight - bottomLeft).normalized;
                foreach (Vector3 vertex in new[] { bottomLeft, topLeft, topRight, bottomRight })
                {
                    Vertices.Add(vertex);
                    Normals.Add(normal);
                    Uvs.Add(cell.Uv);
                }

                Triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }

            /// <summary>A flat polygon as a fan around its middle, its corners given clockwise as seen from its front.</summary>
            internal void AddFan(Vector3 middle, IReadOnlyList<Vector3> corners, Vector3 normal, PaletteCell cell)
            {
                int first = Vertices.Count;
                Vertices.Add(middle);
                foreach (Vector3 corner in corners)
                {
                    Vertices.Add(corner);
                }

                for (int vertex = 0; vertex <= corners.Count; vertex++)
                {
                    Normals.Add(normal);
                    Uvs.Add(cell.Uv);
                }

                for (int corner = 0; corner < corners.Count; corner++)
                {
                    Triangles.AddRange(new[] { first, first + 1 + corner, first + 1 + (corner + 1) % corners.Count });
                }
            }

            internal Mesh ToMesh(string name)
            {
                Mesh mesh = new Mesh { name = name };
                mesh.SetVertices(Vertices);
                mesh.SetNormals(Normals);
                mesh.SetUVs(0, Uvs);
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }

        #endregion

        #region Fields

        internal const int CellsPerSide = 16;

        /// <summary>The table's wood, #c4a27e (its sides and legs in Street_Table.fbx).</summary>
        internal static readonly PaletteCell Wood = new PaletteCell(2, 6);

        /// <summary>Cream, #ede8df: the table's top and the slider handle's top.</summary>
        internal static readonly PaletteCell Cream = new PaletteCell(1, 6);

        /// <summary>Slate, #5e6770: the slider's track and handle.</summary>
        internal static readonly PaletteCell Slate = new PaletteCell(3, 6);

        /// <summary>Almost white, #f3f5f7: for parts tinted at runtime, so the tint shows as it is.</summary>
        internal static readonly PaletteCell White = new PaletteCell(13, 3);

        #endregion

        #region Internal

        /// <summary>
        /// An upright prism around the Y axis from <paramref name="bottomY"/> to <paramref name="topY"/>, with
        /// <paramref name="sides"/> flat sides, the first facing +Z. The bottom is left open when nothing will see it.
        /// </summary>
        internal static Mesh CreatePrism(string name, int sides, float radius, float bottomY, float topY,
            PaletteCell sideCell, PaletteCell topCell, bool hasBottom)
        {
            MeshParts parts = new MeshParts();
            float stepRadians = 2f * Mathf.PI / sides;
            Vector3[] top = new Vector3[sides];
            Vector3[] bottom = new Vector3[sides];
            for (int side = 0; side < sides; side++)
            {
                // Corners at the sides' edges, so the first side's middle faces +Z; the angle runs clockwise seen from above
                float angle = (side - 0.5f) * stepRadians;
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius;
                top[side] = direction + Vector3.up * topY;
                bottom[side] = direction + Vector3.up * bottomY;
            }

            for (int side = 0; side < sides; side++)
            {
                // Seen from outside, the corner further clockwise is on the left
                int next = (side + 1) % sides;
                parts.AddQuad(bottom[next], top[next], top[side], bottom[side], sideCell);
            }

            parts.AddFan(Vector3.up * topY, top, Vector3.up, topCell);
            if (hasBottom)
            {
                Vector3[] reversed = (Vector3[])bottom.Clone();
                System.Array.Reverse(reversed);
                parts.AddFan(Vector3.up * bottomY, reversed, Vector3.down, sideCell);
            }

            return parts.ToMesh(name);
        }

        /// <summary>
        /// Boxes as one mesh, each given by its minimum and maximum corner and with its top face in its own colour, e.g. a
        /// stand's leg and its top.
        /// </summary>
        internal static Mesh CreateBoxes(string name, IReadOnlyList<(Vector3 Min, Vector3 Max)> boxes, PaletteCell sideCell,
            PaletteCell topCell)
        {
            MeshParts parts = new MeshParts();
            foreach ((Vector3 min, Vector3 max) in boxes)
            {
                AddBox(parts, min, max, sideCell, topCell);
            }

            return parts.ToMesh(name);
        }

        /// <summary>
        /// A flat arc lying face up at height <paramref name="y"/>, between two radii, centred on +Z and spanning
        /// <paramref name="angleDegrees"/> in a few straight steps: one segment of a ring.
        /// </summary>
        internal static Mesh CreateRingSegment(string name, float innerRadius, float outerRadius, float angleDegrees,
            int steps, float y, PaletteCell cell)
        {
            MeshParts parts = new MeshParts();
            float startRadians = -0.5f * angleDegrees * Mathf.Deg2Rad;
            float stepRadians = angleDegrees * Mathf.Deg2Rad / steps;
            for (int step = 0; step < steps; step++)
            {
                Vector3 from = Direction(startRadians + step * stepRadians);
                Vector3 to = Direction(startRadians + (step + 1) * stepRadians);
                Vector3 lift = Vector3.up * y;

                // Seen from above, with +Z up and +X right, the angle runs clockwise
                parts.AddQuad(from * innerRadius + lift, from * outerRadius + lift, to * outerRadius + lift,
                    to * innerRadius + lift, cell);
            }

            return parts.ToMesh(name);
        }

        /// <summary>
        /// Saves the mesh at the path. A mesh already there is overwritten in place, so it keeps its GUID and every scene
        /// that uses it gets the new shape. Returns the saved asset.
        /// </summary>
        internal static Mesh Save(Mesh mesh, string assetPath)
        {
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, assetPath);
                return mesh;
            }

            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        #endregion

        #region Private

        private static Vector3 Direction(float radians)
        {
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        // The six faces, each clockwise as seen from outside
        private static void AddBox(MeshParts parts, Vector3 min, Vector3 max, PaletteCell sideCell, PaletteCell topCell)
        {
            // Top, seen from above with +Z up and +X right
            parts.AddQuad(new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), topCell);

            // Bottom, seen from below
            parts.AddQuad(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), sideCell);

            // Front (-Z), looking along +Z: +X is on the right
            parts.AddQuad(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, min.z), sideCell);

            // Back (+Z), looking along -Z: -X is on the right
            parts.AddQuad(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(min.x, min.y, max.z), sideCell);

            // Right (+X), looking along -X: +Z is on the right
            parts.AddQuad(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z), new Vector3(max.x, min.y, max.z), sideCell);

            // Left (-X), looking along +X: -Z is on the right
            parts.AddQuad(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, min.z), sideCell);
        }

        #endregion
    }
}
