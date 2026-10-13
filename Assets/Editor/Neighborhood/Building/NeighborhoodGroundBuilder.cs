using System.Collections.Generic;
using DroneDDW.Neighborhood.Editor.Layout;
using DroneDDW.Neighborhood.Editor.Staging;
using DroneDDW.Neighborhood.Motion;
using DroneDDW.Neighborhood.Versions;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Building
{
    /// <summary>
    /// The street export puts every object at ground level, but the ground surfaces (paving, plaza, paths, the robot
    /// lane) are 3 to 6.5 cm thick, so whatever stands on them sank in. This sets each object on top of the surface
    /// under it, in its own version(s): benches, lamps, bikes, the dog, robots on the lane (their route rises too).
    /// Mounted and flying things (window boxes, drones) are left where they are. Also reads a surface's top under a
    /// point, which the people use to ride on the ground.
    /// </summary>
    internal static class NeighborhoodGroundBuilder
    {
        #region Entities

        /// <summary>A ground surface, the versions it belongs to and the box around it.</summary>
        private readonly struct Surface
        {
            internal Surface(Transform node, int mask)
            {
                Node = node;
                Mask = mask;
                Bounds bounds = new Bounds(node.position, Vector3.zero);
                foreach (Renderer renderer in node.GetComponentsInChildren<Renderer>(true))
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                Bounds = bounds;
            }

            internal Transform Node { get; }
            internal int Mask { get; }
            internal Bounds Bounds { get; }
        }

        #endregion

        #region Fields

        // Objects placed higher than this hang on a wall or fly: the ground under them doesn't hold them
        private const float MaxStandingHeightMetres = 0.5f;

        // Less than this above bare ground: nothing to lift
        private const float MinLiftMetres = 0.002f;

        // Around a point: what stands there touches the ground here too (feet, a bench's legs, a lamp's base)
        private const float FootprintRadiusMetres = 0.15f;
        private const float SearchRadiusMetres = 1f;

        #endregion

        #region Internal

        /// <summary>
        /// Lifts every object standing on a ground surface by the surface's height above bare ground (keeping its own
        /// offset, like a climber planted below it). Runs once the staging has moved the props and before the street
        /// shows its start version (the elements remember their place then).
        /// </summary>
        internal static void PlaceElements(NeighborhoodLayout layout, IReadOnlyDictionary<string, Transform> elementNodes,
            float bareGroundY)
        {
            List<Surface> surfaces = new List<Surface>();
            foreach (NeighborhoodLayoutElement entry in layout.Elements)
            {
                if (NeighborhoodStagingBuilder.GroundGroups.Contains(entry.Group)
                    && elementNodes.TryGetValue(entry.Name, out Transform surface) && surface != null)
                {
                    surfaces.Add(new Surface(surface, entry.Mask));
                }
            }

            Dictionary<string, int> liftedByGroup = new Dictionary<string, int>();
            float highest = 0f;
            string highestName = string.Empty;
            foreach (NeighborhoodLayoutElement entry in layout.Elements)
            {
                if (NeighborhoodStagingBuilder.GroundGroups.Contains(entry.Group) || entry.AttachCharacter >= 0
                    || entry.Position.y > MaxStandingHeightMetres
                    || !elementNodes.TryGetValue(entry.Name, out Transform node) || node == null || IsInsideElement(node))
                {
                    continue;
                }

                float lift = ReadGround(surfaces, entry.Mask, node.position, bareGroundY) - bareGroundY;
                if (lift < MinLiftMetres)
                {
                    continue;
                }

                Lift(node, lift);
                liftedByGroup[entry.Group] = liftedByGroup.TryGetValue(entry.Group, out int count) ? count + 1 : 1;
                if (lift > highest)
                {
                    highest = lift;
                    highestName = entry.Name;
                }
            }

            List<string> groups = new List<string>();
            int total = 0;
            foreach (KeyValuePair<string, int> pair in liftedByGroup)
            {
                groups.Add($"{pair.Key} {pair.Value}");
                total += pair.Value;
            }

            groups.Sort();
            Debug.Log($"[Neighborhood] Set {total} objects on the paving under them (up to {highest * 100f:0.0} cm, "
                + $"{highestName}): {string.Join(", ", groups)}.");
        }

        /// <summary>
        /// The highest point of a surface's top under a point (the point itself and four around it, a footprint), if
        /// the surface reaches under it at all.
        /// </summary>
        internal static bool TryReadTop(Transform surface, Vector3 origin, out Vector3 top)
        {
            top = Vector3.zero;
            if (!IsNear(surface, origin))
            {
                return false;
            }

            Vector2[] footprint =
            {
                new Vector2(origin.x, origin.z),
                new Vector2(origin.x + FootprintRadiusMetres, origin.z), new Vector2(origin.x - FootprintRadiusMetres, origin.z),
                new Vector2(origin.x, origin.z + FootprintRadiusMetres), new Vector2(origin.x, origin.z - FootprintRadiusMetres),
            };
            bool isFound = false;
            foreach (MeshFilter filter in surface.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Matrix4x4 world = filter.transform.localToWorldMatrix;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    Vector3 a = world.MultiplyPoint3x4(vertices[triangles[i]]);
                    Vector3 b = world.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                    Vector3 c = world.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                    foreach (Vector2 point in footprint)
                    {
                        if (TryHeightAt(point, a, b, c, out float y) && (!isFound || y > top.y))
                        {
                            top = new Vector3(point.x, y, point.y);
                            isFound = true;
                        }
                    }
                }
            }

            return isFound;
        }

        #endregion

        #region Private

        // The top of the ground surfaces of the given versions under a point; bare ground if none
        private static float ReadGround(List<Surface> surfaces, int versionMask, Vector3 point, float bareGroundY)
        {
            float ground = bareGroundY;
            foreach (Surface surface in surfaces)
            {
                Bounds bounds = surface.Bounds;
                if ((surface.Mask & versionMask) != 0
                    && bounds.SqrDistance(new Vector3(point.x, bounds.center.y, point.z)) <= SearchRadiusMetres * SearchRadiusMetres
                    && TryReadTop(surface.Node, point, out Vector3 top))
                {
                    ground = Mathf.Max(ground, top.y);
                }
            }

            return ground;
        }

        // An object inside another street object moves with it
        private static bool IsInsideElement(Transform node)
        {
            return node.parent != null && node.parent.GetComponentInParent<NeighborhoodVersionElement>(true) != null;
        }

        // Robots on the lane go round a route in world space, which rises with them
        private static void Lift(Transform node, float lift)
        {
            if (node.TryGetComponent(out NeighborhoodMover mover)
                && (mover.Kind == NeighborhoodMotionKind.Path || mover.Kind == NeighborhoodMotionKind.Orbit))
            {
                mover.ApplyStaging(Matrix4x4.Translate(Vector3.up * lift));
                return;
            }

            node.position += Vector3.up * lift;
        }

        private static bool IsNear(Transform surface, Vector3 point)
        {
            foreach (Renderer renderer in surface.GetComponentsInChildren<Renderer>(true))
            {
                Bounds bounds = renderer.bounds;
                if (bounds.SqrDistance(new Vector3(point.x, bounds.center.y, point.z)) <= SearchRadiusMetres * SearchRadiusMetres)
                {
                    return true;
                }
            }

            return false;
        }

        // The triangle's height at a point seen from above, if the point lies inside it (side faces have no height)
        private static bool TryHeightAt(Vector2 point, Vector3 a, Vector3 b, Vector3 c, out float y)
        {
            y = 0f;
            Vector2 ab = new Vector2(b.x - a.x, b.z - a.z);
            Vector2 ac = new Vector2(c.x - a.x, c.z - a.z);
            Vector2 ap = new Vector2(point.x - a.x, point.y - a.z);
            float area = ab.x * ac.y - ab.y * ac.x;
            if (Mathf.Abs(area) < 1e-8f)
            {
                return false;
            }

            float u = (ap.x * ac.y - ap.y * ac.x) / area;
            float v = (ab.x * ap.y - ab.y * ap.x) / area;
            if (u < 0f || v < 0f || u + v > 1f)
            {
                return false;
            }

            y = a.y + u * (b.y - a.y) + v * (c.y - a.y);
            return true;
        }

        #endregion
    }
}
