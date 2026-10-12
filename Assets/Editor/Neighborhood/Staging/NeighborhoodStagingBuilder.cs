using System;
using System.Collections.Generic;
using DroneDDW.Neighborhood.Characters;
using DroneDDW.Neighborhood.Editor.Layout;
using DroneDDW.Neighborhood.Motion;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Staging
{
    /// <summary>
    /// Applies <see cref="NeighborhoodStaging"/> while the Neighborhood scene is built. Each person and their props
    /// move and turn as one group (lights and light pools hang under their props, held props and the leash under the
    /// people, so they come along); path and orbit routes among the props move with them. Then, version by version, it
    /// looks for what the moved people and props now overlap (street objects, the houses and the seat, the other
    /// people) and logs it: a hint to move the spot or to add the object to the spot's removed objects. The build goes
    /// on either way.
    /// </summary>
    internal static class NeighborhoodStagingBuilder
    {
        #region Entities

        /// <summary>A person after the move, with the props that moved with them.</summary>
        private sealed class StagedSpot
        {
            internal StagedSpot(NeighborhoodStagingSpot spot, Transform person, List<Transform> props)
            {
                Spot = spot;
                Person = person;
                Props = props;
            }

            internal NeighborhoodStagingSpot Spot { get; }
            internal Transform Person { get; }
            internal List<Transform> Props { get; }
        }

        /// <summary>Which 10 cm cells of the ground around a spot something covers, seen from above.</summary>
        private sealed class Footprint
        {
            private readonly bool[] _cells;
            private readonly int _size;
            private readonly float _minX;
            private readonly float _minZ;

            internal Footprint(Vector3 centre)
            {
                _size = Mathf.CeilToInt(SearchRadiusMetres * 2f / CellMetres);
                _cells = new bool[_size * _size];
                _minX = centre.x - SearchRadiusMetres;
                _minZ = centre.z - SearchRadiusMetres;
            }

            internal void Clear()
            {
                Array.Clear(_cells, 0, _cells.Length);
            }

            internal void AddCircle(Vector3 centre, float radius)
            {
                VisitCircle(centre, radius, true);
            }

            internal void Add(Transform root)
            {
                Visit(root, false, true);
            }

            internal bool Overlaps(Transform root, bool isWater)
            {
                return Visit(root, isWater, false);
            }

            internal bool OverlapsCircle(Vector3 centre, float radius)
            {
                return VisitCircle(centre, radius, false);
            }

            private bool VisitCircle(Vector3 centre, float radius, bool isMarking)
            {
                Vector2 middle = new Vector2(centre.x, centre.z);
                for (int x = 0; x < _size; x++)
                {
                    for (int z = 0; z < _size; z++)
                    {
                        if ((CellCentre(x, z) - middle).sqrMagnitude <= radius * radius && VisitCell(z * _size + x, isMarking))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            /// <summary>
            /// Walks the cells under every triangle at people's height (or every triangle, for water). Marks them, or
            /// returns true at the first one already marked.
            /// </summary>
            private bool Visit(Transform root, bool isWater, bool isMarking)
            {
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                {
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null)
                    {
                        continue;
                    }

                    Matrix4x4 world = filter.transform.localToWorldMatrix;
                    Vector3[] vertices = mesh.vertices;
                    int[] triangles = mesh.triangles;
                    if (vertices.Length == 0 && filter.TryGetComponent(out Renderer renderer))
                    {
                        // Mesh data not readable: its box stands in
                        vertices = BoxCorners(renderer.bounds);
                        triangles = BoxTriangles;
                        world = Matrix4x4.identity;
                    }

                    for (int i = 0; i + 2 < triangles.Length; i += 3)
                    {
                        Vector3 a = world.MultiplyPoint3x4(vertices[triangles[i]]);
                        Vector3 b = world.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                        Vector3 c = world.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                        float low = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
                        float high = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                        if (low > HeadroomMetres || (!isWater && high < GroundHeightMetres))
                        {
                            continue;
                        }

                        if (VisitTriangle(new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z), isMarking))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            // Cells whose centre lies within half a cell diagonal of the triangle, so posts seen from above (side faces
            // that flatten to lines) still cover their cells
            private bool VisitTriangle(Vector2 a, Vector2 b, Vector2 c, bool isMarking)
            {
                float reach = CellMetres * 0.71f;
                int minX = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - reach - _minX) / CellMetres));
                int maxX = Mathf.Min(_size - 1, Mathf.FloorToInt((Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + reach - _minX) / CellMetres));
                int minZ = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, Mathf.Min(b.y, c.y)) - reach - _minZ) / CellMetres));
                int maxZ = Mathf.Min(_size - 1, Mathf.FloorToInt((Mathf.Max(a.y, Mathf.Max(b.y, c.y)) + reach - _minZ) / CellMetres));
                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        if (DistanceToTriangle(CellCentre(x, z), a, b, c) <= reach && VisitCell(z * _size + x, isMarking))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            private bool VisitCell(int index, bool isMarking)
            {
                if (isMarking)
                {
                    _cells[index] = true;
                    return false;
                }

                return _cells[index];
            }

            private Vector2 CellCentre(int x, int z)
            {
                return new Vector2(_minX + (x + 0.5f) * CellMetres, _minZ + (z + 0.5f) * CellMetres);
            }
        }

        #endregion

        #region Fields

        private const float CellMetres = 0.1f;
        private const float SearchRadiusMetres = 5f;
        private const float PersonRadiusMetres = 0.35f;

        // Triangles entirely below this lie on the ground; entirely above it they hang over people's heads
        private const float GroundHeightMetres = 0.12f;
        private const float HeadroomMetres = 2.2f;

        // Surfaces people stand on: never in the way
        private static readonly HashSet<string> GroundGroups = new HashSet<string>
        {
            "Paving", "Plaza", "Paths", "LightInlays", "RobotLane", "StreetMarkings",
        };

        // In the way even though it is flat
        private static readonly HashSet<string> WaterGroups = new HashSet<string> { "Pond" };

        private static readonly string[] VersionNames = { "Nature", "Balanced", "Efficiency" };

        private static readonly int[] BoxTriangles =
        {
            0, 1, 2, 1, 3, 2, 4, 6, 5, 5, 6, 7, 0, 4, 1, 1, 4, 5, 2, 3, 6, 3, 7, 6, 0, 2, 4, 2, 6, 4, 1, 5, 3, 3, 5, 7,
        };

        #endregion

        #region Internal

        /// <summary>The street objects the spots leave out (every spot's removed objects).</summary>
        internal static HashSet<string> CollectRemovedObjects(NeighborhoodStaging staging)
        {
            HashSet<string> names = new HashSet<string>();
            if (staging == null || !staging.IsEnabled)
            {
                return names;
            }

            foreach (NeighborhoodStagingSpot spot in staging.Spots)
            {
                if (spot == null)
                {
                    continue;
                }

                foreach (string name in spot.RemovedObjects)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name.Trim());
                    }
                }
            }

            return names;
        }

        /// <summary>
        /// Moves every staged person with their props, then looks for overlaps. Runs once the people are built (their
        /// held props and the leash hang on their bones) and before the street shows its start version (the elements
        /// remember their place then). Returns the overlaps found, one line each.
        /// </summary>
        internal static List<string> Apply(NeighborhoodStaging staging, NeighborhoodLayout layout,
            IReadOnlyList<NeighborhoodCharacter> characters, IReadOnlyDictionary<string, Transform> nodes, Transform shared,
            Vector3 seat)
        {
            List<string> overlaps = new List<string>();
            if (staging == null || !staging.IsEnabled)
            {
                Debug.Log("[Neighborhood] Staging is off: the people stay where the street export put them.");
                return overlaps;
            }

            List<StagedSpot> staged = new List<StagedSpot>();
            foreach (NeighborhoodStagingSpot spot in staging.Spots)
            {
                NeighborhoodCharacter character = spot != null ? FindCharacter(characters, spot.Character) : null;
                if (character == null)
                {
                    Debug.LogWarning($"[Neighborhood] Staging: {(spot != null ? spot.Character.ToString() : "a spot")} "
                        + "has no person in the street; skipped.");
                    continue;
                }

                staged.Add(Move(spot, character.transform, FindProps(spot, nodes), seat));
            }

            Dictionary<string, NeighborhoodLayoutElement> elements = new Dictionary<string, NeighborhoodLayoutElement>();
            foreach (NeighborhoodLayoutElement element in layout.Elements)
            {
                elements[element.Name] = element;
            }

            foreach (StagedSpot spot in staged)
            {
                foreach (string overlap in FindOverlaps(spot, staged, layout, elements, nodes, shared))
                {
                    overlaps.Add(overlap);
                    Debug.LogWarning($"[Neighborhood] Staging: {overlap}. Move the spot, or add the object to its removed objects.");
                }
            }

            return overlaps;
        }

        /// <summary>"22° left of ahead, 8.0 m from the seat" for a point relative to the seat.</summary>
        internal static string Describe(Vector3 offset)
        {
            float bearing = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            string side = Mathf.Abs(bearing) < 0.5f
                ? "straight ahead"
                : $"{Mathf.Abs(bearing):0}° {(bearing < 0f ? "left" : "right")} of ahead";
            float distance = new Vector2(offset.x, offset.z).magnitude;
            return $"{side}, {distance:0.0} m from the seat";
        }

        #endregion

        #region Private

        private static StagedSpot Move(NeighborhoodStagingSpot spot, Transform person, List<Transform> props, Vector3 seat)
        {
            float yaw = NeighborhoodStagingMove.YawFacing(spot.Position, seat) + spot.TurnFromSeatDegrees;
            NeighborhoodStagingMove move = new NeighborhoodStagingMove(person.position, person.eulerAngles.y, spot.Position, yaw);
            MoveTransform(person, move);
            foreach (Transform prop in props)
            {
                MoveTransform(prop, move);
                if (prop.TryGetComponent(out NeighborhoodMover mover))
                {
                    mover.ApplyStaging(move.Matrix);
                }

                foreach (NeighborhoodLeash leash in prop.GetComponentsInChildren<NeighborhoodLeash>(true))
                {
                    leash.Refresh();
                }
            }

            Debug.Log($"[Neighborhood] Staging: {spot.Character} now {Describe(spot.Position - seat)}, "
                + $"{FacingDescription(spot.TurnFromSeatDegrees)}; {props.Count} props moved with them.");
            return new StagedSpot(spot, person, props);
        }

        private static NeighborhoodCharacter FindCharacter(IReadOnlyList<NeighborhoodCharacter> characters, NeighborhoodCharacterId id)
        {
            foreach (NeighborhoodCharacter character in characters)
            {
                if (character != null && character.Id == id)
                {
                    return character;
                }
            }

            return null;
        }

        private static List<Transform> FindProps(NeighborhoodStagingSpot spot, IReadOnlyDictionary<string, Transform> nodes)
        {
            List<Transform> props = new List<Transform>();
            foreach (string name in spot.Props)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (nodes.TryGetValue(name.Trim(), out Transform node) && node != null)
                {
                    props.Add(node);
                }
                else
                {
                    Debug.LogWarning($"[Neighborhood] Staging: {spot.Character}'s prop {name} is not in the street.");
                }
            }

            return props;
        }

        private static void MoveTransform(Transform target, NeighborhoodStagingMove move)
        {
            target.SetPositionAndRotation(move.MovePoint(target.position), move.MoveRotation(target.rotation));
        }

        private static string FacingDescription(float turnFromSeatDegrees)
        {
            if (Mathf.Abs(turnFromSeatDegrees) < 0.5f)
            {
                return "facing the seat";
            }

            string side = turnFromSeatDegrees > 0f ? "right" : "left";
            return $"turned {Mathf.Abs(turnFromSeatDegrees):0}° to their {side} from the seat";
        }

        /// <summary>
        /// What the person and their props overlap, version by version: street objects at people's height and water
        /// (not ground surfaces, objects someone holds, or objects flying or swimming by), the houses and the seat, and
        /// the other people. One line per object, with the versions it happens in.
        /// </summary>
        private static List<string> FindOverlaps(StagedSpot spot, IReadOnlyList<StagedSpot> staged, NeighborhoodLayout layout,
            IReadOnlyDictionary<string, NeighborhoodLayoutElement> elements, IReadOnlyDictionary<string, Transform> nodes,
            Transform shared)
        {
            HashSet<string> propNames = new HashSet<string>();
            foreach (Transform prop in spot.Props)
            {
                propNames.Add(prop.name);
            }

            Footprint footprint = new Footprint(spot.Person.position);
            Dictionary<string, List<string>> versionsByObstacle = new Dictionary<string, List<string>>();
            for (int version = 0; version < VersionNames.Length; version++)
            {
                int bit = 1 << version;
                footprint.Clear();
                footprint.AddCircle(spot.Person.position, PersonRadiusMetres);
                foreach (Transform prop in spot.Props)
                {
                    if (elements.TryGetValue(prop.name, out NeighborhoodLayoutElement element) && (element.Mask & bit) != 0)
                    {
                        footprint.Add(prop);
                    }
                }

                foreach (NeighborhoodLayoutElement element in layout.Elements)
                {
                    if ((element.Mask & bit) == 0 || element.AttachCharacter >= 0 || propNames.Contains(element.Name)
                        || GroundGroups.Contains(element.Group) || !nodes.TryGetValue(element.Name, out Transform node)
                        || node == null || IsPassingBy(node) || !IsNear(node, spot.Person.position)
                        || !footprint.Overlaps(node, WaterGroups.Contains(element.Group)))
                    {
                        continue;
                    }

                    AddOverlap(versionsByObstacle, element.Name, version);
                }

                if (shared != null && footprint.Overlaps(shared, false))
                {
                    AddOverlap(versionsByObstacle, "the houses or the seat", version);
                }

                foreach (StagedSpot other in staged)
                {
                    if (other != spot && footprint.OverlapsCircle(other.Person.position, PersonRadiusMetres))
                    {
                        AddOverlap(versionsByObstacle, $"the {other.Spot.Character}", version);
                    }
                }
            }

            List<string> overlaps = new List<string>();
            foreach (KeyValuePair<string, List<string>> pair in versionsByObstacle)
            {
                overlaps.Add($"{spot.Spot.Character}'s spot overlaps {pair.Key} ({string.Join(", ", pair.Value)})");
            }

            return overlaps;
        }

        private static void AddOverlap(Dictionary<string, List<string>> versionsByObstacle, string obstacle, int version)
        {
            if (!versionsByObstacle.TryGetValue(obstacle, out List<string> versions))
            {
                versions = new List<string>();
                versionsByObstacle.Add(obstacle, versions);
            }

            versions.Add(VersionNames[version]);
        }

        private static bool IsPassingBy(Transform node)
        {
            return node.TryGetComponent(out NeighborhoodMover mover)
                && (mover.Kind == NeighborhoodMotionKind.Path || mover.Kind == NeighborhoodMotionKind.Orbit);
        }

        private static bool IsNear(Transform node, Vector3 centre)
        {
            foreach (Renderer renderer in node.GetComponentsInChildren<Renderer>())
            {
                Bounds bounds = renderer.bounds;
                if (bounds.SqrDistance(new Vector3(centre.x, bounds.center.y, centre.z)) <= SearchRadiusMetres * SearchRadiusMetres)
                {
                    return true;
                }
            }

            return false;
        }

        private static float DistanceToTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            float first = Cross(b - a, point - a);
            float second = Cross(c - b, point - b);
            float third = Cross(a - c, point - c);
            bool hasNegative = first < 0f || second < 0f || third < 0f;
            bool hasPositive = first > 0f || second > 0f || third > 0f;
            if (!(hasNegative && hasPositive))
            {
                return 0f;
            }

            return Mathf.Min(DistanceToSegment(point, a, b), Mathf.Min(DistanceToSegment(point, b, c), DistanceToSegment(point, c, a)));
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            float t = lengthSquared > 1e-12f ? Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared) : 0f;
            return Vector2.Distance(point, a + segment * t);
        }

        private static float Cross(Vector2 first, Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
        }

        private static Vector3[] BoxCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return new[]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z), new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z),
            };
        }

        #endregion
    }
}
