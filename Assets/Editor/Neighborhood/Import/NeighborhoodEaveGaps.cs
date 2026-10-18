using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Import
{
    /// <summary>
    /// Closes the slit under the houses' eaves in Street_Shared. Each roof side is a sloped slab whose lowest edge sits
    /// at the wall top's height but outside the wall, so where the slab passes over the wall face its underside is
    /// already higher (0.1 to 0.8 m, steeper roofs more) and the sky shows through. The walls are open-topped boxes, so
    /// the top edge of every wall face (a vertical face at least 2 m tall; windows and frames are smaller) under a roof
    /// slab is raised to the slab's underside, so walls and roofs meet; the walls stay under their own roofs, so nothing
    /// pokes through.
    /// </summary>
    internal static class NeighborhoodEaveGaps
    {
        #region Fields

        // Eave gaps measure 0.1 to 0.8 m; the next roof above a wall top is 1.8 m or more away (a neighbour's higher roof)
        private const float MaxGapMeters = 1f;
        private const float MinGapMeters = 0.002f;

        // Faces that count as a roof's underside (sloped, facing down) and as a wall (vertical and tall)
        private const float UndersideMaxUp = -0.2f;
        private const float UndersideMinUp = -0.97f;
        private const float WallMaxUp = 0.01f;
        private const float WallMinHeightMeters = 2f;

        private const float InsideTolerance = 0.001f;
        private const float KeyMillimetres = 1000f;

        #endregion

        #region Internal

        /// <summary>Raises the wall tops under roof slabs; returns how many wall corners moved.</summary>
        internal static int Close(Mesh mesh, Matrix4x4 meshToWorld)
        {
            Vector3[] local = mesh.vertices;
            Vector3[] world = new Vector3[local.Length];
            for (int index = 0; index < local.Length; index++)
            {
                world[index] = meshToWorld.MultiplyPoint3x4(local[index]);
            }

            int[] triangles = mesh.triangles;
            List<(Vector3 A, Vector3 B, Vector3 C)> undersides = new List<(Vector3, Vector3, Vector3)>();
            HashSet<Vector3Int> topCorners = new HashSet<Vector3Int>();
            for (int index = 0; index < triangles.Length; index += 3)
            {
                Vector3 a = world[triangles[index]];
                Vector3 b = world[triangles[index + 1]];
                Vector3 c = world[triangles[index + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f)
                {
                    continue;
                }

                float up = normal.normalized.y;
                if (up <= UndersideMaxUp && up >= UndersideMinUp)
                {
                    undersides.Add((a, b, c));
                }
                else if (Mathf.Abs(up) <= WallMaxUp)
                {
                    float top = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                    if (top - Mathf.Min(a.y, Mathf.Min(b.y, c.y)) < WallMinHeightMeters)
                    {
                        continue;
                    }

                    foreach (Vector3 corner in new[] { a, b, c })
                    {
                        if (top - corner.y < InsideTolerance)
                        {
                            topCorners.Add(ToKey(corner));
                        }
                    }
                }
            }

            Dictionary<Vector3Int, float> raised = new Dictionary<Vector3Int, float>();
            foreach (Vector3Int key in topCorners)
            {
                Vector3 corner = FromKey(key);
                float best = float.PositiveInfinity;
                foreach ((Vector3 a, Vector3 b, Vector3 c) in undersides)
                {
                    if (!TryGetHeightAbove(a, b, c, corner, out float height))
                    {
                        continue;
                    }

                    float gap = height - corner.y;
                    if (gap > MinGapMeters && gap <= MaxGapMeters && height < best)
                    {
                        best = height;
                    }
                }

                if (!float.IsPositiveInfinity(best))
                {
                    raised[key] = best;
                }
            }

            if (raised.Count == 0)
            {
                return 0;
            }

            // Every copy of a corner moves (the top face's and the side faces'), so the wall stays closed
            Matrix4x4 worldToMesh = meshToWorld.inverse;
            for (int index = 0; index < world.Length; index++)
            {
                if (raised.TryGetValue(ToKey(world[index]), out float height))
                {
                    Vector3 moved = world[index];
                    moved.y = height;
                    local[index] = worldToMesh.MultiplyPoint3x4(moved);
                }
            }

            mesh.vertices = local;
            mesh.RecalculateBounds();
            return raised.Count;
        }

        #endregion

        #region Private

        // The triangle's height straight above the point, when the point lies under it (seen from above)
        private static bool TryGetHeightAbove(Vector3 a, Vector3 b, Vector3 c, Vector3 point, out float height)
        {
            height = 0f;
            Vector2 p = new Vector2(point.x, point.z);
            Vector2 a2 = new Vector2(a.x, a.z);
            Vector2 v0 = new Vector2(b.x, b.z) - a2;
            Vector2 v1 = new Vector2(c.x, c.z) - a2;
            Vector2 v2 = p - a2;
            float denominator = v0.x * v1.y - v1.x * v0.y;
            if (Mathf.Abs(denominator) < 1e-9f)
            {
                return false;
            }

            float u = (v2.x * v1.y - v1.x * v2.y) / denominator;
            float v = (v0.x * v2.y - v2.x * v0.y) / denominator;
            if (u < -InsideTolerance || v < -InsideTolerance || u + v > 1f + InsideTolerance)
            {
                return false;
            }

            height = a.y + u * (b.y - a.y) + v * (c.y - a.y);
            return true;
        }

        private static Vector3Int ToKey(Vector3 point)
        {
            return new Vector3Int(Mathf.RoundToInt(point.x * KeyMillimetres), Mathf.RoundToInt(point.y * KeyMillimetres),
                Mathf.RoundToInt(point.z * KeyMillimetres));
        }

        private static Vector3 FromKey(Vector3Int key)
        {
            return new Vector3(key.x, key.y, key.z) / KeyMillimetres;
        }

        #endregion
    }
}
