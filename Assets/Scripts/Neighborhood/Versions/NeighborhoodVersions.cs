using UnityEngine;

namespace DroneDDW.Neighborhood.Versions
{
    /// <summary>
    /// Helpers for the three versions. A version mask has bit i set when an object is part of version i
    /// (1 = nature, 2 = balanced, 4 = efficiency, 7 = all three).
    /// </summary>
    public static class NeighborhoodVersions
    {
        #region Fields

        private const int VersionCount = 3;
        private const int AllVersionsMask = (1 << VersionCount) - 1;

        #endregion

        #region Properties

        public static int Count => VersionCount;
        public static int AllMask => AllVersionsMask;

        #endregion

        #region Public

        public static NeighborhoodVersion Clamp(int index)
        {
            return (NeighborhoodVersion)Mathf.Clamp(index, 0, VersionCount - 1);
        }

        public static NeighborhoodVersion Step(NeighborhoodVersion version, int delta)
        {
            return Clamp((int)version + delta);
        }

        /// <summary>0 at nature, 0.5 at balanced, 1 at efficiency.</summary>
        public static float ToBlend(NeighborhoodVersion version)
        {
            return (int)version / (float)(VersionCount - 1);
        }

        public static int ToMask(NeighborhoodVersion version)
        {
            return 1 << (int)version;
        }

        public static bool Contains(int versionMask, NeighborhoodVersion version)
        {
            int index = (int)version;
            return index >= 0 && index < VersionCount && (versionMask & (1 << index)) != 0;
        }

        #endregion
    }
}
