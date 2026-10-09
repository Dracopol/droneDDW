namespace DroneDDW.Neighborhood.Versions
{
    /// <summary>
    /// Sent when the street starts moving to a version (or snaps to it).
    /// HasPrevious is false the first time a version is applied.
    /// </summary>
    public readonly struct NeighborhoodVersionChange
    {
        #region Properties

        public NeighborhoodVersion Previous { get; }
        public NeighborhoodVersion Next { get; }
        public bool HasPrevious { get; }

        /// <summary>True when the street snapped to the version without a transition.</summary>
        public bool IsImmediate { get; }

        #endregion

        #region Setup

        public NeighborhoodVersionChange(NeighborhoodVersion previous, NeighborhoodVersion next, bool hasPrevious, bool isImmediate)
        {
            Previous = previous;
            Next = next;
            HasPrevious = hasPrevious;
            IsImmediate = isImmediate;
        }

        #endregion
    }
}
