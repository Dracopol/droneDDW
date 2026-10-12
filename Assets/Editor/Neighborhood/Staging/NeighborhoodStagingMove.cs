using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Staging
{
    /// <summary>
    /// One person's move on the ground, as a rigid transform: turn by the change in facing around where they stood,
    /// then shift to where they go. Everything that moves with them keeps its place around them.
    /// </summary>
    internal readonly struct NeighborhoodStagingMove
    {
        #region Properties

        internal Vector3 FromPosition { get; }
        internal Vector3 ToPosition { get; }

        /// <summary>The turn about the vertical axis (new facing minus old facing).</summary>
        internal Quaternion Turn { get; }

        /// <summary>The move as a matrix, for points kept in world space (path and orbit routes).</summary>
        internal Matrix4x4 Matrix => Matrix4x4.Translate(ToPosition) * Matrix4x4.Rotate(Turn) * Matrix4x4.Translate(-FromPosition);

        #endregion

        #region Setup

        internal NeighborhoodStagingMove(Vector3 fromPosition, float fromYawDegrees, Vector3 toPosition, float toYawDegrees)
        {
            FromPosition = fromPosition;
            ToPosition = toPosition;
            Turn = Quaternion.Euler(0f, toYawDegrees - fromYawDegrees, 0f);
        }

        #endregion

        #region Internal

        /// <summary>The yaw (degrees, 0 = +Z, 90 = +X) of someone at a point who looks at a target.</summary>
        internal static float YawFacing(Vector3 from, Vector3 target)
        {
            return Mathf.Atan2(target.x - from.x, target.z - from.z) * Mathf.Rad2Deg;
        }

        internal Vector3 MovePoint(Vector3 point)
        {
            return ToPosition + Turn * (point - FromPosition);
        }

        internal Quaternion MoveRotation(Quaternion rotation)
        {
            return Turn * rotation;
        }

        #endregion
    }
}
